namespace SkiGame
{
    /// <summary>
    /// Causal IIR filter made of second-order sections ("biquads"), processed sample by
    /// sample in Direct Form II Transposed. Same maths as scipy.signal.sosfilt, so the
    /// coefficients designed in Python (Tools/emg_analysis.py) can be pasted in unchanged.
    /// Each row = { b0, b1, b2, a0(=1), a1, a2 }.
    /// </summary>
    public sealed class BiquadCascade
    {
        private readonly double[,] sos;
        private readonly double[] z1, z2;   // two state values per section

        public BiquadCascade(double[,] sos)
        {
            this.sos = sos;
            int n = sos.GetLength(0);
            z1 = new double[n];
            z2 = new double[n];
        }

        public double Process(double x)
        {
            for (int s = 0; s < z1.Length; s++)
            {
                double y = sos[s, 0] * x + z1[s];
                z1[s] = sos[s, 1] * x - sos[s, 4] * y + z2[s];
                z2[s] = sos[s, 2] * x - sos[s, 5] * y;
                x = y;   // output of this section feeds the next one
            }
            return x;
        }

        public void Reset()
        {
            System.Array.Clear(z1, 0, z1.Length);
            System.Array.Clear(z2, 0, z2.Length);
        }
    }
}
