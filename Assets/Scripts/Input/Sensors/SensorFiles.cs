using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SkiGame.Sensors
{
    /// <summary>Parses one text line of numbers separated by tabs, spaces, commas or semicolons.</summary>
    public static class LineParser
    {
        private static readonly char[] Separators = { '\t', ' ', ',', ';' };

        /// <summary>Fills <paramref name="values"/> with every column; non-numbers (e.g. "-1.#IND00") become NaN.</summary>
        /// <returns>Number of columns, or 0 for an empty line.</returns>
        public static int Parse(string line, List<double> values)
        {
            values.Clear();
            if (string.IsNullOrWhiteSpace(line)) return 0;
            string[] parts = line.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
            foreach (string p in parts)
            {
                string s = p.Trim().Trim('\'', '"', '{', '}');
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && !double.IsNaN(v) && !double.IsInfinity(v))
                    values.Add(v);
                else
                    values.Add(double.NaN);
            }
            return values.Count;
        }

        public static double Get(List<double> values, int column) =>
            column >= 0 && column < values.Count ? values[column] : double.NaN;
    }

    /// <summary>
    /// Follows a text file that another program keeps appending to (like "tail -f"),
    /// strictly READ-ONLY: the file is opened with FileAccess.Read and FileShare.ReadWrite | Delete,
    /// so the writing program is never blocked and nothing is ever written.
    /// Reading starts at the CURRENT END of the file (old history is skipped), so even a
    /// multi-GB log is fine: only the newly added bytes are read each frame.
    /// </summary>
    public sealed class TextFileTailer : IDisposable
    {
        public string Path { get; }
        public bool IsOpen => stream != null;
        public string LastError { get; private set; }
        /// <summary>Total complete lines received since opening.</summary>
        public long LinesReceived { get; private set; }

        private FileStream stream;
        private long position;
        private readonly StringBuilder pending = new StringBuilder();
        private readonly byte[] buffer = new byte[64 * 1024];
        private const long MaxBacklogBytes = 256 * 1024;   // if far behind, jump close to the end

        public TextFileTailer(string path) { Path = path; }

        private bool TryOpen()
        {
            if (stream != null) return true;
            try
            {
                if (!File.Exists(Path)) { LastError = "File not found"; return false; }
                stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.SequentialScan);
                position = stream.Length;     // start at the end: only NEW data is used
                pending.Clear();
                LastError = null;
                return true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                stream = null;
                return false;
            }
        }

        /// <summary>Appends every newly completed line to <paramref name="lines"/>. Returns false if the file can't be read.</summary>
        public bool ReadNewLines(List<string> lines)
        {
            if (!TryOpen()) return false;
            try
            {
                long length = stream.Length;
                if (length < position)            // file was truncated / restarted by the writer
                {
                    position = 0;
                    pending.Clear();
                }
                if (length - position > MaxBacklogBytes)
                {
                    position = length - MaxBacklogBytes;
                    pending.Clear();
                    pending.Append('\u0001');      // marker: first (partial) line must be dropped
                }
                while (position < length)
                {
                    stream.Seek(position, SeekOrigin.Begin);
                    int toRead = (int)Math.Min(buffer.Length, length - position);
                    int n = stream.Read(buffer, 0, toRead);
                    if (n <= 0) break;
                    position += n;
                    pending.Append(Encoding.ASCII.GetString(buffer, 0, n));
                }

                int start = 0;
                string text = pending.ToString();
                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] != '\n') continue;
                    string line = text.Substring(start, i - start).TrimEnd('\r');
                    start = i + 1;
                    if (line.Length > 0 && line[0] == '\u0001') continue;   // partial line after a jump
                    lines.Add(line);
                    LinesReceived++;
                }
                pending.Clear();
                if (start < text.Length) pending.Append(text, start, text.Length - start);
                LastError = null;
                return true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Dispose();                          // re-open next time
                return false;
            }
        }

        public void Dispose()
        {
            try { stream?.Dispose(); } catch { /* ignore */ }
            stream = null;
        }
    }

    /// <summary>A recorded COP file (Wii Balance Board Recorder export: Time CpX CpY BL BR TL TR Weight ...).</summary>
    public sealed class RecordedCop
    {
        public double[] Time, X, Y, Weight;
        public double Duration => Time.Length > 0 ? Time[Time.Length - 1] : 0;
        public double SampleRate { get; private set; }

        public static RecordedCop Load(string path)
        {
            var t = new List<double>(); var x = new List<double>(); var y = new List<double>(); var w = new List<double>();
            var v = new List<double>();
            foreach (string line in File.ReadLines(path))
            {
                if (LineParser.Parse(line, v) < 3) continue;
                double tt = v[0], cx = v[1], cy = v[2];
                if (double.IsNaN(tt) || double.IsNaN(cx) || double.IsNaN(cy)) continue;     // header / bad line
                t.Add(tt); x.Add(cx); y.Add(cy);
                w.Add(v.Count > 7 && !double.IsNaN(v[7]) ? v[7] : 70.0);
            }
            if (t.Count < 2) throw new InvalidDataException("No COP samples found in " + path);

            // The Recorder writes seconds (its manual says ms): detect milliseconds by the step size.
            double step = (t[t.Count - 1] - t[0]) / (t.Count - 1);
            double scale = step > 5 ? 0.001 : 1.0;
            double t0 = t[0];
            for (int i = 0; i < t.Count; i++) t[i] = (t[i] - t0) * scale;

            return new RecordedCop
            {
                Time = t.ToArray(), X = x.ToArray(), Y = y.ToArray(), Weight = w.ToArray(),
                SampleRate = (t.Count - 1) / Math.Max(1e-6, t[t.Count - 1])
            };
        }

        /// <summary>Linear interpolation at time <paramref name="time"/> (s, wraps around for looping).</summary>
        public void Sample(double time, out double x, out double y, out double weight)
        {
            double d = Duration;
            if (d > 0) time %= d;
            if (time < 0) time += d;
            int hi = Array.BinarySearch(Time, time);
            if (hi < 0) hi = ~hi;
            hi = Math.Max(1, Math.Min(Time.Length - 1, hi));
            int lo = hi - 1;
            double f = (time - Time[lo]) / Math.Max(1e-9, Time[hi] - Time[lo]);
            f = Math.Max(0, Math.Min(1, f));
            x = X[lo] + (X[hi] - X[lo]) * f;
            y = Y[lo] + (Y[hi] - Y[lo]) * f;
            weight = Weight[lo] + (Weight[hi] - Weight[lo]) * f;
        }
    }

    /// <summary>A recorded two-muscle EMG file (BioRadio CSV export: sample, time_s, ch1_mv, ch2_mv).</summary>
    public sealed class RecordedEmg
    {
        public float[] Jump, Backflip;
        public int Count => Jump.Length;

        public static RecordedEmg Load(string path, int jumpColumn, int backflipColumn)
        {
            var j = new List<float>(140000); var b = new List<float>(140000);
            var v = new List<double>();
            foreach (string line in File.ReadLines(path))
            {
                if (LineParser.Parse(line, v) == 0) continue;
                double a = LineParser.Get(v, jumpColumn), c = LineParser.Get(v, backflipColumn);
                if (double.IsNaN(a) || double.IsNaN(c)) continue;   // header
                j.Add((float)a); b.Add((float)c);
            }
            if (j.Count == 0) throw new InvalidDataException("No EMG samples found in " + path);
            return new RecordedEmg { Jump = j.ToArray(), Backflip = b.ToArray() };
        }
    }
}
