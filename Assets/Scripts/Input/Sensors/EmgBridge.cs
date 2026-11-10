using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace SkiGame.Sensors
{
    /// <summary>One UDP message from the BioRadio bridge (JSON, one per channel per update).</summary>
    [Serializable]
    public class EmgBridgePacket
    {
        public int channel;
        public float raw_mv;
        public float rms_mv;
        public long sample_count;
        public float sample_rate_hz;
        /// <summary>Every sample since the previous message (ski_emg_bridge.py). Empty with the kit's original bridge.</summary>
        public float[] samples;
    }

    /// <summary>
    /// Receives EMG from the BioRadio Python bridge on 127.0.0.1:port (background thread).
    /// Works with:
    ///   - Tools/EmgBridge/ski_emg_bridge.py : sends every sample -> the game runs the full filter chain
    ///   - the kit's bioradio_unity_bridge.py : sends only the batch RMS -> used directly as the envelope
    /// </summary>
    public sealed class EmgBridgeReceiver : IDisposable
    {
        public int Port { get; }
        public string Error { get; private set; }
        public long PacketsReceived => Interlocked.Read(ref packets);
        public long SamplesLost => Interlocked.Read(ref lost);
        public float LastPacketTime { get; set; } = -999f;   // set on the main thread when packets are drained

        private readonly ConcurrentQueue<EmgBridgePacket> queue = new ConcurrentQueue<EmgBridgePacket>();
        private UdpClient client;
        private Thread thread;
        private volatile bool running;
        private long packets, lost;
        private readonly long[] lastCount = new long[16];

        public EmgBridgeReceiver(int port)
        {
            Port = port;
            for (int i = 0; i < lastCount.Length; i++) lastCount[i] = -1;
            try
            {
                client = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
                client.Client.ReceiveTimeout = 500;
                client.Client.ReceiveBufferSize = 1 << 20;
                running = true;
                thread = new Thread(Loop) { IsBackground = true, Name = "EMG bridge UDP" };
                thread.Start();
            }
            catch (Exception e)
            {
                Error = $"Cannot listen on UDP {port}: {e.Message} (is another program or a second Unity using it?)";
                client = null;
            }
        }

        private void Loop()
        {
            var from = new IPEndPoint(IPAddress.Any, 0);
            while (running)
            {
                try
                {
                    byte[] data = client.Receive(ref from);
                    var p = JsonUtility.FromJson<EmgBridgePacket>(Encoding.UTF8.GetString(data));
                    if (p == null) continue;
                    Interlocked.Increment(ref packets);
                    int ch = Mathf.Clamp(p.channel, 0, lastCount.Length - 1);
                    int n = p.samples != null ? p.samples.Length : 0;
                    if (n > 0 && lastCount[ch] >= 0 && p.sample_count - lastCount[ch] > n)
                        Interlocked.Add(ref lost, p.sample_count - lastCount[ch] - n);    // dropped UDP packets
                    lastCount[ch] = p.sample_count;
                    queue.Enqueue(p);
                    while (queue.Count > 2000) queue.TryDequeue(out _);                   // never grow without bound
                }
                catch (SocketException) { /* timeout - check 'running' again */ }
                catch (ObjectDisposedException) { break; }
                catch (Exception) { /* malformed message - ignore */ }
            }
        }

        public bool TryDequeue(out EmgBridgePacket p) => queue.TryDequeue(out p);

        public void Dispose()
        {
            running = false;
            try { client?.Close(); } catch { /* ignore */ }
            if (thread != null && thread.IsAlive) thread.Join(600);
            client = null;
        }
    }

    /// <summary>
    /// TEST ONLY: plays a recording into the bridge port in exactly the bridge's format (20 ms batches,
    /// one JSON message per channel), so the whole live path can be tested without the BioRadio.
    /// </summary>
    public sealed class EmgBridgeSelfTest : IDisposable
    {
        private readonly Thread thread;
        private volatile bool running = true;

        public EmgBridgeSelfTest(RecordedEmg rec, int port, double fs, int jumpChannel, int backflipChannel)
        {
            thread = new Thread(() => Run(rec, port, fs, jumpChannel, backflipChannel)) { IsBackground = true, Name = "EMG self-test sender" };
            thread.Start();
        }

        private void Run(RecordedEmg rec, int port, double fs, int jumpCh, int flipCh)
        {
            using (var udp = new UdpClient())
            {
                var target = new IPEndPoint(IPAddress.Loopback, port);
                var clock = System.Diagnostics.Stopwatch.StartNew();
                long sent = 0;
                var sb = new StringBuilder(4096);
                while (running)
                {
                    long due = (long)(clock.Elapsed.TotalSeconds * fs);
                    int n = (int)Math.Min(200, due - sent);
                    if (n > 0)
                    {
                        Send(udp, target, sb, jumpCh, rec.Jump, sent, n, fs, rec.Count);
                        Send(udp, target, sb, flipCh, rec.Backflip, sent, n, fs, rec.Count);
                        sent += n;
                    }
                    Thread.Sleep(20);
                }
            }
        }

        private static void Send(UdpClient udp, IPEndPoint target, StringBuilder sb, int ch, float[] data, long start, int n, double fs, int count)
        {
            sb.Clear();
            sb.Append("{\"channel\":").Append(ch).Append(",\"sample_count\":").Append(start + n)
              .Append(",\"sample_rate_hz\":").Append(fs.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(",\"samples\":[");
            double sum = 0; float last = 0;
            for (int i = 0; i < n; i++)
            {
                float v = data[(int)((start + i) % count)];
                if (i > 0) sb.Append(',');
                sb.Append(v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture));
                sum += v * v; last = v;
            }
            sb.Append("],\"raw_mv\":").Append(last.ToString(System.Globalization.CultureInfo.InvariantCulture))
              .Append(",\"rms_mv\":").Append(Math.Sqrt(sum / n).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('}');
            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            try { udp.Send(bytes, bytes.Length, target); } catch { /* ignore */ }
        }

        public void Dispose()
        {
            running = false;
            if (thread.IsAlive) thread.Join(500);
        }
    }

    /// <summary>
    /// TEST ONLY: writes a recorded COP file into a temporary text file in the live WiiMote format
    /// (10 tab-separated columns, ~85 lines/s), so the live-file path can be tested without the board.
    /// The real WiiMote1_All.txt is never touched.
    /// </summary>
    public sealed class CopLiveFileSelfTest : IDisposable
    {
        public string Path { get; }
        private readonly Thread thread;
        private volatile bool running = true;

        public CopLiveFileSelfTest(RecordedCop rec, bool invertX, bool invertY)
        {
            Path = System.IO.Path.Combine(Application.temporaryCachePath, "cop_selftest_live.txt");
            System.IO.File.WriteAllText(Path, "");
            thread = new Thread(() => Run(rec, invertX, invertY)) { IsBackground = true, Name = "COP self-test writer" };
            thread.Start();
        }

        private void Run(RecordedCop rec, bool invertX, bool invertY)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            using (var fs = new System.IO.FileStream(Path, System.IO.FileMode.Append, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite))
            using (var w = new System.IO.StreamWriter(fs))
            {
                while (running)
                {
                    double t = clock.Elapsed.TotalSeconds;
                    rec.Sample(t, out double x, out double y, out double weight);
                    // The game flips the live file's X/Y according to the settings, so pre-flip here.
                    if (invertX) x = -x;
                    if (invertY) y = -y;
                    var now = DateTime.Now;
                    w.Write(string.Format(ci, "{0:0.000000}\t{1:0.000000}\t{2:0.000000}\t{3:0.000000}\t{4:0.000000}\t{5:0.000000}\t{6:0.000000}\t{7}\t{8}\t{9}\n",
                        weight * 0.25, weight * 0.25, weight * 0.25, weight * 0.25, x, y, weight, now.Hour, now.Minute, now.Second));
                    w.Flush();
                    Thread.Sleep(12);   // ~85 lines per second, like the real board software
                }
            }
        }

        public void Dispose()
        {
            running = false;
            if (thread.IsAlive) thread.Join(500);
        }
    }
}
