using System;

namespace SkiGame.Sensors
{
    /// <summary>
    /// One second-order IIR section (biquad), Direct Form II transposed, double precision.
    /// Coefficients follow the RBJ "Audio EQ Cookbook" (bilinear transform with pre-warping),
    /// which gives exact Butterworth responses when cascaded with the right Q values.
    /// All filters are CAUSAL (they only use past samples), so they can run in real time.
    /// </summary>
    public sealed class Biquad
    {
        private double b0, b1, b2, a1, a2;
        private double z1, z2;

        public static Biquad LowPass(double fs, double f0, double q) => Make(fs, f0, q, 0);
        public static Biquad HighPass(double fs, double f0, double q) => Make(fs, f0, q, 1);
        public static Biquad Notch(double fs, double f0, double q) => Make(fs, f0, q, 2);

        private static Biquad Make(double fs, double f0, double q, int kind)
        {
            f0 = Math.Min(f0, fs * 0.49);                 // keep below Nyquist
            double w = 2.0 * Math.PI * f0 / fs;
            double c = Math.Cos(w), alpha = Math.Sin(w) / (2.0 * q);
            double nb0, nb1, nb2;
            switch (kind)
            {
                case 0: nb0 = (1 - c) / 2; nb1 = 1 - c; nb2 = (1 - c) / 2; break;          // low-pass
                case 1: nb0 = (1 + c) / 2; nb1 = -(1 + c); nb2 = (1 + c) / 2; break;       // high-pass
                default: nb0 = 1; nb1 = -2 * c; nb2 = 1; break;                             // notch
            }
            double a0 = 1 + alpha;
            return new Biquad
            {
                b0 = nb0 / a0, b1 = nb1 / a0, b2 = nb2 / a0,
                a1 = -2 * c / a0, a2 = (1 - alpha) / a0
            };
        }

        public double Process(double x)
        {
            double y = b0 * x + z1;
            z1 = b1 * x - a1 * y + z2;
            z2 = b2 * x - a2 * y;
            return y;
        }

        /// <summary>Sets the internal state as if the input had been constant at <paramref name="x"/> forever.</summary>
        /// <returns>The steady-state output for that input.</returns>
        public double Prime(double x)
        {
            // Steady state for a constant input: y = H(1) * x.
            double gain = (b0 + b1 + b2) / (1 + a1 + a2);
            double y = gain * x;
            z2 = b2 * x - a2 * y;
            z1 = b1 * x - a1 * y + z2;
            return y;
        }

        public void Reset() { z1 = z2 = 0; }
    }

    /// <summary>A chain of biquads applied one after another.</summary>
    public sealed class FilterChain
    {
        private readonly Biquad[] stages;
        public FilterChain(params Biquad[] stages) { this.stages = stages; }

        public double Process(double x)
        {
            for (int i = 0; i < stages.Length; i++) x = stages[i].Process(x);
            return x;
        }

        public void Prime(double x)
        {
            for (int i = 0; i < stages.Length; i++) x = stages[i].Prime(x);
        }

        public void Reset() { foreach (var s in stages) s.Reset(); }

        /// <summary>Q values of the two sections of a 4th-order Butterworth filter.</summary>
        public static readonly double[] Butterworth4Q = { 0.54119610, 1.30656296 };
        /// <summary>Q of a 2nd-order Butterworth filter.</summary>
        public const double Butterworth2Q = 0.70710678;

        public static FilterChain Butterworth4HighPass(double fs, double fc) =>
            new FilterChain(Biquad.HighPass(fs, fc, Butterworth4Q[0]), Biquad.HighPass(fs, fc, Butterworth4Q[1]));

        public static FilterChain Butterworth4LowPass(double fs, double fc) =>
            new FilterChain(Biquad.LowPass(fs, fc, Butterworth4Q[0]), Biquad.LowPass(fs, fc, Butterworth4Q[1]));

        public static FilterChain Butterworth2LowPass(double fs, double fc) =>
            new FilterChain(Biquad.LowPass(fs, fc, Butterworth2Q));
    }
}
