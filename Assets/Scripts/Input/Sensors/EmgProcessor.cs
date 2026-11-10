using System;
using System.Collections.Generic;

namespace SkiGame.Sensors
{
    /// <summary>
    /// Real-time processing of ONE EMG channel, sample by sample (all filters causal):
    ///
    ///   raw (mV) -> 4th-order Butterworth high-pass 20 Hz   (motion artefact, electrode drift)
    ///            -> 4th-order Butterworth low-pass 450 Hz   (high-frequency noise)
    ///            -> notch 50 Hz, Q 30                        (mains hum, India)
    ///            -> full-wave rectification |x|
    ///            -> 2nd-order Butterworth low-pass 8 Hz      (linear envelope)
    ///            -> normalise: (env - rest) / (max - rest)   (per-person calibration)
    ///            -> hysteresis: ON above 'on', OFF below 'off', must stay above 'on' for minOn ms
    ///
    /// The output is a boolean "muscle active". The game turns its rising edge into one
    /// jump / backflip (GameInputBase: edge + cooldown; PlayerController: buffer + coyote time).
    /// </summary>
    public sealed class EmgChannel
    {
        public string Name { get; }
        public double RestLevel { get; private set; }
        public double MaxLevel { get; private set; }
        public double Envelope { get; private set; }     // mV
        public double Normalized { get; private set; }   // 0 = rest, 1 = calibrated contraction
        public bool Active { get; private set; }
        public int Activations { get; private set; }
        public bool Calibrating => calibrationTarget > 0;
        public float CalibrationProgress => calibrationTarget > 0 ? (float)calibrationValues.Count / calibrationTarget : 1f;

        public double OnThreshold, OffThreshold;

        private readonly FilterChain band;
        private readonly Biquad notch;
        private readonly FilterChain envelope;
        private readonly int minOnSamples;
        private int aboveCount;
        private bool primed;
        private bool risingEdgeLatch;

        // Calibration: every 10th envelope value is stored (100 values per second at 1 kHz).
        private readonly List<double> calibrationValues = new List<double>();
        private int calibrationTarget, decimator;
        public string LastCalibrationMessage { get; private set; } = "";
        public bool LastCalibrationSucceeded { get; private set; }

        public EmgChannel(string name, SensorSettings s, double rest, double max, double on, double off)
        {
            Name = name;
            double fs = s.emgSampleRate;
            band = s.emgLowPassHz > 0 && s.emgLowPassHz < fs * 0.48
                ? new FilterChain(Biquad.HighPass(fs, s.emgHighPassHz, FilterChain.Butterworth4Q[0]),
                                  Biquad.HighPass(fs, s.emgHighPassHz, FilterChain.Butterworth4Q[1]),
                                  Biquad.LowPass(fs, s.emgLowPassHz, FilterChain.Butterworth4Q[0]),
                                  Biquad.LowPass(fs, s.emgLowPassHz, FilterChain.Butterworth4Q[1]))
                : FilterChain.Butterworth4HighPass(fs, s.emgHighPassHz);
            notch = s.emgNotchHz > 0 && s.emgNotchHz < fs * 0.48 ? Biquad.Notch(fs, s.emgNotchHz, s.emgNotchQ) : null;
            envelope = FilterChain.Butterworth2LowPass(fs, s.emgEnvelopeHz);
            minOnSamples = Math.Max(1, (int)Math.Round(s.emgMinOnMs * fs / 1000.0));
            SetCalibration(rest, max);
            OnThreshold = on;
            OffThreshold = off;
        }

        public void SetCalibration(double rest, double max)
        {
            RestLevel = rest;
            MaxLevel = Math.Max(rest + 1e-6, max);
        }

        public void BeginCalibration(double seconds, double fs)
        {
            calibrationValues.Clear();
            decimator = 0;
            calibrationTarget = Math.Max(10, (int)(seconds * fs / 10));
        }

        public void Process(double x)
        {
            if (!primed) { band.Prime(x); notch?.Prime(0); envelope.Prime(0); primed = true; }   // no start-up burst from the DC offset

            double y = band.Process(x);
            if (notch != null) y = notch.Process(y);
            Envelope = Math.Max(0, envelope.Process(Math.Abs(y)));
            Normalized = (Envelope - RestLevel) / (MaxLevel - RestLevel);

            if (!Active)
            {
                aboveCount = Normalized > OnThreshold ? aboveCount + 1 : 0;
                if (aboveCount >= minOnSamples)
                {
                    Active = true;
                    risingEdgeLatch = true;
                    Activations++;
                }
            }
            else if (Normalized < OffThreshold)
            {
                Active = false;
                aboveCount = 0;
            }

            if (calibrationTarget > 0 && ++decimator >= 10)
            {
                decimator = 0;
                calibrationValues.Add(Envelope);
                if (calibrationValues.Count >= calibrationTarget) FinishCalibration();
            }
        }

        private void FinishCalibration()
        {
            calibrationTarget = 0;
            calibrationValues.Sort();
            double rest = Percentile(calibrationValues, 0.10);
            double max = Percentile(calibrationValues, 0.99);
            if (max < rest * 3 || max - rest < 1e-4)
            {
                LastCalibrationMessage = $"{Name}: no clear contractions found (rest {rest:0.0000}, peak {max:0.0000} mV) - kept previous values.";
                LastCalibrationSucceeded = false;
                return;
            }
            SetCalibration(rest, max);
            LastCalibrationSucceeded = true;
            LastCalibrationMessage = $"{Name}: rest {rest:0.0000} mV, contraction {max:0.0000} mV";
        }

        private static double Percentile(List<double> sorted, double p)
        {
            if (sorted.Count == 0) return 0;
            int i = (int)Math.Round(p * (sorted.Count - 1));
            return sorted[Math.Max(0, Math.Min(sorted.Count - 1, i))];
        }

        /// <summary>
        /// The request the game reads this frame: "active now" OR "became active at any point since the last call",
        /// so a contraction shorter than one frame is never lost.
        /// </summary>
        public bool ConsumeRequest()
        {
            bool r = Active || risingEdgeLatch;
            risingEdgeLatch = false;
            return r;
        }
    }

    /// <summary>The two EMG channels: jump (forearm flexor, FCU) and backflip (biceps).</summary>
    public sealed class EmgProcessor
    {
        public readonly EmgChannel Jump, Backflip;
        public readonly double SampleRate;
        public long SamplesProcessed { get; private set; }

        public EmgProcessor(SensorSettings s)
        {
            SampleRate = s.emgSampleRate;
            Jump = new EmgChannel("Jump (FCU)", s, s.jumpRestMv, s.jumpMaxMv, s.jumpOnThreshold, s.jumpOffThreshold);
            Backflip = new EmgChannel("Backflip (biceps)", s, s.backflipRestMv, s.backflipMaxMv, s.backflipOnThreshold, s.backflipOffThreshold);
        }

        public void Process(double jumpMv, double backflipMv)
        {
            Jump.Process(jumpMv);
            Backflip.Process(backflipMv);
            SamplesProcessed++;
        }

        public void BeginCalibration(double seconds)
        {
            Jump.BeginCalibration(seconds, SampleRate);
            Backflip.BeginCalibration(seconds, SampleRate);
        }

        public bool Calibrating => Jump.Calibrating || Backflip.Calibrating;

        /// <summary>
        /// Calibrates from a recording before play starts: runs the same filters over the first
        /// <paramref name="seconds"/> and takes the 10th / 99th percentile of each envelope.
        /// </summary>
        public static void CalibrateFromRecording(RecordedEmg rec, SensorSettings s, EmgProcessor target)
        {
            var temp = new EmgProcessor(s);
            temp.BeginCalibration(s.emgCalibrationSeconds);
            int n = Math.Min(rec.Count, (int)(s.emgCalibrationSeconds * s.emgSampleRate) + 10);
            for (int i = 0; i < n; i++) temp.Process(rec.Jump[i], rec.Backflip[i]);
            if (!temp.Jump.Calibrating && temp.Jump.LastCalibrationSucceeded)
                target.Jump.SetCalibration(temp.Jump.RestLevel, temp.Jump.MaxLevel);
            if (!temp.Backflip.Calibrating && temp.Backflip.LastCalibrationSucceeded)
                target.Backflip.SetCalibration(temp.Backflip.RestLevel, temp.Backflip.MaxLevel);
        }
    }
}
