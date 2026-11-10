using System.Collections.Generic;
using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// One EMG channel from a recording, played as if it were live (see BiosignalInput):
    ///  - idle: a quiet REST stretch of the recording loops through the filters,
    ///  - Trigger(): the stream switches straight to the start of the next real contraction.
    /// Every switch is offset-matched (the new segment continues from the last value fed),
    /// so the band-pass never sees an artificial step.
    /// </summary>
    public sealed class EmgReplayChannel
    {
        private const double Fs = EmgChannelDetector.SampleRate;
        private const double FilterDelay = 0.05;     // causal envelope lags ~50 ms (measured)
        private const double RestLength = 0.6;       // seconds of rest that loop while idle
        private const double RestSearch = 2.0;       // look this far back before each flex for the quietest rest

        public readonly string Name;
        public readonly EmgChannelDetector Detector;
        public int FlexCount => onsets.Count;
        public int NextIndex => next;
        public bool Flexing { get; private set; }
        public int Events { get; private set; }
        public string Status = "";

        // 3 s plot at 100 Hz
        public const int PlotLen = 300;
        public readonly float[] Env = new float[PlotLen], RawMin = new float[PlotLen], RawMax = new float[PlotLen];
        public readonly bool[] Fired = new bool[PlotLen];
        public int Head;

        private readonly float[] signal;
        private readonly List<int> onsets = new List<int>();
        private readonly List<int> restStarts = new List<int>();
        private readonly float clipAfter;
        private int next, pos, segStart, segEnd, decim;
        private float offset, lastFed, curMin, curMax;

        public EmgReplayChannel(string name, float[] signalMv, float on, float off, float clipAfterSeconds)
        {
            Name = name;
            signal = signalMv;
            clipAfter = clipAfterSeconds;
            Detector = new EmgChannelDetector(on, off, 0.6);
            FindFlexes(on, off);
            Status = $"{onsets.Count} flexes loaded";
            if (onsets.Count > 0) BeginRest();
        }

        /// <summary>Runs the SAME causal detector over the whole file once to find each contraction and a quiet rest before it.</summary>
        private void FindFlexes(float on, float off)
        {
            var scan = new EmgChannelDetector(on, off, 0.6);
            var env = new float[signal.Length];
            for (int i = 0; i < signal.Length; i++)
            {
                scan.Push(signal[i]);
                env[i] = (float)scan.Envelope;
                if (!scan.ConsumeOnset()) continue;
                int onset = i - (int)(FilterDelay * Fs);
                int searchFrom = onset - (int)(RestSearch * Fs);
                if (searchFrom < 0 || onset + (int)(clipAfter * Fs) >= signal.Length) continue;

                // quietest RestLength window (lowest peak envelope) inside [onset-2.0 s, onset-0.1 s]
                int len = (int)(RestLength * Fs), best = searchFrom;
                float bestPeak = float.MaxValue;
                for (int s = searchFrom; s + len <= onset - (int)(0.1 * Fs); s += 20)
                {
                    float peak = 0f;
                    for (int k = s; k < s + len; k++) if (env[k] > peak) peak = env[k];
                    if (peak < bestPeak) { bestPeak = peak; best = s; }
                }
                onsets.Add(onset);
                restStarts.Add(best);
            }
        }

        /// <summary>"The player flexes": switch to the next real contraction. Ignored while one is still playing.</summary>
        public void Trigger()
        {
            if (onsets.Count == 0 || Flexing) return;
            BeginSegment(onsets[next], onsets[next] + (int)(clipAfter * Fs));
            Flexing = true;
            Status = $"flex {next + 1}/{onsets.Count}  (t = {onsets[next] / Fs:F1} s in file)";
        }

        /// <summary>Feed 'n' samples through the filters. Returns true if the threshold was crossed.</summary>
        public bool Stream(int n)
        {
            if (onsets.Count == 0) return false;
            bool fired = false;
            for (int i = 0; i < n; i++)
            {
                if (pos >= segEnd)
                {
                    if (Flexing) { next = (next + 1) % onsets.Count; BeginRest(); Status = $"{onsets.Count} flexes loaded"; }
                    else BeginSegment(segStart, segEnd);   // loop the rest
                }
                float raw = signal[pos++] - offset;
                lastFed = raw;
                if (raw < curMin) curMin = raw;
                if (raw > curMax) curMax = raw;
                Detector.Push(raw);
                if (Detector.ConsumeOnset()) { fired = true; Events++; Fired[Head] = true; }
                if (++decim >= 10)
                {
                    decim = 0;
                    Head = (Head + 1) % PlotLen;
                    Env[Head] = (float)Detector.Envelope; RawMin[Head] = curMin; RawMax[Head] = curMax; Fired[Head] = false;
                    curMin = curMax = 0f;
                }
            }
            return fired;
        }

        private void BeginRest()
        {
            Flexing = false;
            BeginSegment(restStarts[next], restStarts[next] + (int)(RestLength * Fs));
        }

        private void BeginSegment(int from, int to)
        {
            segStart = from; segEnd = to; pos = from;
            offset = signal[from] - lastFed;
        }
    }
}
