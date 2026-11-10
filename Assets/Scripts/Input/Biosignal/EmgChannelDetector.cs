using System.Collections.Generic;

namespace SkiGame
{
    /// <summary>
    /// Turns ONE raw EMG channel (mV, sampled at 1000 Hz) into discrete "flex" events.
    ///
    /// Pipeline (all causal, sample by sample, so it works in real time):
    ///   1. Band-pass 20-450 Hz, 4th-order Butterworth (8 poles, 4 biquads)
    ///        - removes motion artefacts / baseline drift below 20 Hz
    ///        - removes high-frequency noise above 450 Hz (Nyquist = 500 Hz)
    ///   2. Full-wave rectification  |x|
    ///   3. Low-pass 5 Hz, 2nd-order Butterworth  -> linear envelope (muscle activation level)
    ///   4. Hysteresis threshold: ON when envelope rises above OnThreshold,
    ///      OFF only when it falls below OffThreshold (lower), plus a refractory period,
    ///      so one contraction = one event even if the envelope wobbles.
    ///
    /// Coefficients were designed with scipy.signal.butter(..., fs=1000, output='sos').
    /// They are ONLY valid for fs = 1000 Hz (BioRadio setting) - re-run Tools/emg_analysis.py
    /// if the sampling rate changes.
    /// </summary>
    public sealed class EmgChannelDetector
    {
        public const double SampleRate = 1000.0;

        // butter(4, [20, 450], 'bandpass', fs=1000, output='sos')
        private static readonly double[,] BandPass20to450 =
        {
            { 0.5597939753636623, 1.1195879507273245, 0.5597939753636623, 1.0,  1.4727072982383067, 0.5512194161882683 },
            { 1.0, -2.0, 1.0, 1.0, -1.775225707139887,  0.7898310129469704 },
            { 1.0,  2.0, 1.0, 1.0,  1.703066904412495,  0.7912205716038404 },
            { 1.0, -2.0, 1.0, 1.0, -1.894566110555583,  0.9097098295426526 },
        };

        // butter(2, 5, fs=1000, output='sos')
        private static readonly double[,] LowPass5 =
        {
            { 0.00024135904904198078, 0.00048271809808396156, 0.00024135904904198078, 1.0, -1.9555782403150352, 0.9565436765112031 },
        };

        private readonly BiquadCascade bandPass = new BiquadCascade(BandPass20to450);
        private readonly BiquadCascade lowPass = new BiquadCascade(LowPass5);

        public double OnThreshold;    // mV (envelope units)
        public double OffThreshold;   // mV
        public double RefractorySeconds;

        /// <summary>Envelope must be ignored for this long after (re)start while the filters settle.</summary>
        public double WarmUpSeconds = 1.0;

        public double Envelope { get; private set; }
        public bool Active { get; private set; }
        public int EventCount { get; private set; }

        // auto-calibration
        public bool Calibrating { get; private set; }
        private readonly List<double> calBuffer = new List<double>();
        private long calSamplesWanted;

        private long sampleIndex;
        private long lastOnsetSample = long.MinValue / 2;
        private int pendingOnsets;

        public EmgChannelDetector(double on, double off, double refractorySeconds)
        {
            OnThreshold = on;
            OffThreshold = off;
            RefractorySeconds = refractorySeconds;
        }

        /// <summary>Feed ONE raw sample (mV). Call this for every sample, in order.</summary>
        public void Push(double raw)
        {
            double filtered = bandPass.Process(raw);
            Envelope = lowPass.Process(System.Math.Abs(filtered));
            sampleIndex++;

            if (sampleIndex < WarmUpSeconds * SampleRate) return;

            if (Calibrating)
            {
                calBuffer.Add(Envelope);
                if (calBuffer.Count >= calSamplesWanted) FinishCalibration();
                return;
            }

            if (!Active)
            {
                bool refractoryOver = sampleIndex - lastOnsetSample > RefractorySeconds * SampleRate;
                if (Envelope > OnThreshold && refractoryOver)
                {
                    Active = true;
                    lastOnsetSample = sampleIndex;
                    pendingOnsets++;
                    EventCount++;
                }
            }
            else if (Envelope < OffThreshold)
            {
                Active = false;
            }
        }

        /// <summary>Returns true once per detected contraction (then clears it).</summary>
        public bool ConsumeOnset()
        {
            if (pendingOnsets == 0) return false;
            pendingOnsets--;
            return true;
        }

        /// <summary>
        /// Record the envelope for 'seconds' (user should rest AND flex a few times),
        /// then set: rest = 20th percentile, max = 95th percentile,
        /// ON = rest + onFraction*(max-rest), OFF = rest + offFraction*(max-rest).
        /// </summary>
        public void StartCalibration(double seconds) { calBuffer.Clear(); calSamplesWanted = (long)(seconds * SampleRate); Calibrating = true; }

        public double CalOnFraction = 0.30, CalOffFraction = 0.12;

        private void FinishCalibration()
        {
            calBuffer.Sort();
            double rest = calBuffer[(int)(0.20 * (calBuffer.Count - 1))];
            double max = calBuffer[(int)(0.95 * (calBuffer.Count - 1))];
            OnThreshold = rest + CalOnFraction * (max - rest);
            OffThreshold = rest + CalOffFraction * (max - rest);
            calBuffer.Clear();
            Calibrating = false;
            Active = false;
            UnityEngine.Debug.Log($"[SkiGame] EMG calibrated: rest={rest:F4} mV, max={max:F4} mV, ON={OnThreshold:F4}, OFF={OffThreshold:F4}");
        }

        public void Reset()
        {
            bandPass.Reset(); lowPass.Reset();
            Envelope = 0; Active = false; sampleIndex = 0; pendingOnsets = 0;
            lastOnsetSample = long.MinValue / 2;
        }
    }
}
