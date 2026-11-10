using System;
using System.Collections.Generic;

namespace SkiGame.Sensors
{
    /// <summary>
    /// Real-time processing of ONE EMG channel, sample by sample (all filters causal):
    ///
    ///   raw (mV) -> 4th-order Butterworth high-pass 20 Hz   (motion artefact, electrode drift, DC offset)
    ///            -> 4th-order Butterworth low-pass 450 Hz   (high-frequency noise)
    ///            -> notch 50 Hz, Q 30                        (mains hum, India)
    ///            -> full-wave rectification |x|
    ///            -> 2nd-order Butterworth low-pass 5 Hz      (linear envelope)
    ///            -> normalise: (env - rest) / (max - rest)   (per-person calibration)
    ///            -> hysteresis: ON above 'on', OFF below 'off', must stay above 'on' for minOn ms
    ///
    /// The output is a boolean "muscle active". The game turns its rising edge into one
    /// jump / backflip (GameInputBase: edge + cooldown; PlayerController: buffer + coyote time).
    ///
    /// If only an RMS value arrives (the unmodified BioRadio kit bridge), ProcessEnvelope() skips
    /// the filters and uses that value as the envelope - it works, but with less noise rejection.
    /// </summary>
    public sealed class EmgChannel
    {
        public string Name { get; }
        public double SampleRate { get; }
        public double RestLevel { get; private set; }
        public double MaxLevel { get; private set; }
        public double Envelope { get; private set; }     // mV
        public double Normalized { get; private set; }   // 0 = rest, 1 = calibrated contraction
        public bool Active { get; private set; }
        public int Activations { get; private set; }
        public bool Calibrating => calibrationTargetSamples > 0;
        public float CalibrationProgress => calibrationTargetSamples > 0 ? (float)calibrationSamplesSeen / calibrationTargetSamples : 1f;
        public string LastCalibrationMessage { get; private set; } = "";
        public bool LastCalibrationSucceeded { get; private set; }

        public double OnThreshold, OffThreshold;

        private readonly FilterChain band;
        private readonly Biquad notch;
        private readonly FilterChain envelope;
        private readonly double minOnSeconds;
        private double aboveSeconds;
        private bool primed;
        private bool risingEdgeLatch;

        // Automatic calibration (replay / live CSV): every 10th envelope value is kept.
        private readonly List<double> calibrationValues = new List<double>();
        private long calibrationTargetSamples, calibrationSamplesSeen;
        private int decimator;

        // Guided calibration: the wizard records envelope values for each phase.
        private List<double> recording;

        public EmgChannel(string name, SensorSettings s, double fs, double rest, double max, double on, double off)
        {
            Name = name;
            SampleRate = fs;
            band = s.emgLowPassHz > 0 && s.emgLowPassHz < fs * 0.48
                ? new FilterChain(Biquad.HighPass(fs, s.emgHighPassHz, FilterChain.Butterworth4Q[0]),
                                  Biquad.HighPass(fs, s.emgHighPassHz, FilterChain.Butterworth4Q[1]),
                                  Biquad.LowPass(fs, s.emgLowPassHz, FilterChain.Butterworth4Q[0]),
                                  Biquad.LowPass(fs, s.emgLowPassHz, FilterChain.Butterworth4Q[1]))
                : FilterChain.Butterworth4HighPass(fs, s.emgHighPassHz);
            notch = s.emgNotchHz > 0 && s.emgNotchHz < fs * 0.48 ? Biquad.Notch(fs, s.emgNotchHz, s.emgNotchQ) : null;
            envelope = FilterChain.Butterworth2LowPass(fs, s.emgEnvelopeHz);
            minOnSeconds = Math.Max(0, s.emgMinOnMs / 1000.0);
            SetCalibration(rest, max);
            OnThreshold = on;
            OffThreshold = off;
        }

        public void SetCalibration(double rest, double max)
        {
            RestLevel = rest;
            MaxLevel = Math.Max(rest + 1e-6, max);
        }

        /// <summary>One raw sample (mV). Runs the full filter chain.</summary>
        public void Process(double x)
        {
            if (!primed) { band.Prime(x); notch?.Prime(0); envelope.Prime(0); primed = true; }   // no start-up burst from the DC offset

            double y = band.Process(x);
            if (notch != null) y = notch.Process(y);
            Detect(Math.Max(0, envelope.Process(Math.Abs(y))), 1);
        }

        /// <summary>An already-smoothed level (e.g. the RMS of a batch) that stands for <paramref name="samples"/> samples.</summary>
        public void ProcessEnvelope(double level, int samples)
        {
            Detect(Math.Max(0, level), Math.Max(1, samples));
        }

        private void Detect(double env, int samples)
        {
            Envelope = env;
            Normalized = (Envelope - RestLevel) / (MaxLevel - RestLevel);
            double dt = samples / SampleRate;

            if (!Active)
            {
                aboveSeconds = Normalized > OnThreshold ? aboveSeconds + dt : 0;
                if (aboveSeconds >= minOnSeconds - 1e-9)
                {
                    Active = true;
                    risingEdgeLatch = true;
                    Activations++;
                }
            }
            else if (Normalized < OffThreshold)
            {
                Active = false;
                aboveSeconds = 0;
            }

            recording?.Add(Envelope);

            if (calibrationTargetSamples > 0)
            {
                calibrationSamplesSeen += samples;
                decimator += samples;
                if (decimator >= 10) { decimator = 0; calibrationValues.Add(Envelope); }
                if (calibrationSamplesSeen >= calibrationTargetSamples) FinishCalibration();
            }
        }

        // ------------------------------------------------------------ automatic calibration

        public void BeginCalibration(double seconds)
        {
            calibrationValues.Clear();
            decimator = 0;
            calibrationSamplesSeen = 0;
            calibrationTargetSamples = Math.Max(100, (long)(seconds * SampleRate));
        }

        private void FinishCalibration()
        {
            calibrationTargetSamples = 0;
            calibrationValues.Sort();
            double rest = Percentile(calibrationValues, 0.10);
            double max = Percentile(calibrationValues, 0.99);
            ApplyCalibration(rest, max);
        }

        /// <summary>Uses rest / max if they look like a real contraction (max at least 3x rest).</summary>
        public bool ApplyCalibration(double rest, double max)
        {
            if (max < rest * 3 || max - rest < 1e-4)
            {
                LastCalibrationMessage = $"{Name}: no clear contraction (rest {rest:0.0000}, peak {max:0.0000} mV) - kept previous values";
                LastCalibrationSucceeded = false;
                return false;
            }
            SetCalibration(rest, max);
            LastCalibrationSucceeded = true;
            LastCalibrationMessage = $"{Name}: rest {rest:0.0000} mV, contraction {max:0.0000} mV";
            return true;
        }

        // ------------------------------------------------------------ guided calibration

        public void StartRecording() => recording = new List<double>(8192);

        public List<double> StopRecording()
        {
            var r = recording ?? new List<double>();
            recording = null;
            return r;
        }

        public static double Percentile(List<double> values, double p)
        {
            if (values.Count == 0) return 0;
            var sorted = new List<double>(values);
            sorted.Sort();
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

        public EmgProcessor(SensorSettings s, double sampleRate = 0)
        {
            SampleRate = sampleRate > 0 ? sampleRate : s.emgSampleRate;
            Jump = new EmgChannel("Jump (forearm)", s, SampleRate, s.jumpRestMv, s.jumpMaxMv, s.jumpOnThreshold, s.jumpOffThreshold);
            Backflip = new EmgChannel("Backflip (biceps)", s, SampleRate, s.backflipRestMv, s.backflipMaxMv, s.backflipOnThreshold, s.backflipOffThreshold);
        }

        public void Process(double jumpMv, double backflipMv)
        {
            Jump.Process(jumpMv);
            Backflip.Process(backflipMv);
            SamplesProcessed++;
        }

        public void BeginCalibration(double seconds)
        {
            Jump.BeginCalibration(seconds);
            Backflip.BeginCalibration(seconds);
        }

        public bool Calibrating => Jump.Calibrating || Backflip.Calibrating;

        /// <summary>
        /// Calibrates from a recording before play starts: runs the same filters over the first
        /// <paramref name="seconds"/> and takes the 10th / 99th percentile of each envelope.
        /// </summary>
        public static void CalibrateFromRecording(RecordedEmg rec, SensorSettings s, EmgProcessor target)
        {
            var temp = new EmgProcessor(s, target.SampleRate);
            temp.BeginCalibration(s.emgCalibrationSeconds);
            int n = Math.Min(rec.Count, (int)(s.emgCalibrationSeconds * target.SampleRate) + 10);
            for (int i = 0; i < n; i++) temp.Process(rec.Jump[i], rec.Backflip[i]);
            if (temp.Jump.LastCalibrationSucceeded) target.Jump.SetCalibration(temp.Jump.RestLevel, temp.Jump.MaxLevel);
            if (temp.Backflip.LastCalibrationSucceeded) target.Backflip.SetCalibration(temp.Backflip.RestLevel, temp.Backflip.MaxLevel);
        }
    }
}
