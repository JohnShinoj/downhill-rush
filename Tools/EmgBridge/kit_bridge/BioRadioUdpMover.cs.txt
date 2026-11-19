using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

// Attach to a Cube. Start Python bridge, then Play in Unity.
// Movement uses one selected EMG channel's RMS, not the raw signal.
public class BioRadioUdpMover : MonoBehaviour
{
    public int udpPort = 50557;
    public int channelToUse = 1;
    public float activationThresholdMv = 0.10f; // Calibrate for each participant
    public float maxExpectedRmsMv = 1.0f;        // Calibrate for each participant
    public float moveSpeed = 2f;

    UdpClient receiver;
    Thread thread;
    volatile bool listening;
    readonly object gate = new object();
    float latestRms;
    bool received;

    [Serializable]
    class EmgMessage
    {
        public int channel;
        public float raw_mv;
        public float rms_mv;
        public int sample_count;
        public float sample_rate_hz;
    }

    void Start()
    {
        try
        {
            receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, udpPort));
            receiver.Client.ReceiveTimeout = 500;
            listening = true;
            thread = new Thread(ReceiveLoop) { IsBackground = true };
            thread.Start();
            Debug.Log("BioRadio UDP listening on 127.0.0.1:" + udpPort);
        }
        catch (Exception ex) { Debug.LogError("UDP startup: " + ex.Message); }
    }

    void ReceiveLoop()
    {
        IPEndPoint sender = new IPEndPoint(IPAddress.Loopback, 0);
        while (listening)
        {
            try
            {
                byte[] bytes = receiver.Receive(ref sender);
                EmgMessage msg = JsonUtility.FromJson<EmgMessage>(Encoding.UTF8.GetString(bytes));
                if (msg != null && msg.channel == channelToUse)
                {
                    lock (gate) { latestRms = msg.rms_mv; received = true; }
                }
            }
            catch (SocketException) { } // Timeout during shutdown
            catch (ObjectDisposedException) { break; }
            catch (Exception) { }
        }
    }

    void Update()
    {
        float rms;
        bool hasData;
        lock (gate) { rms = latestRms; hasData = received; }
        if (!hasData) return;
        float denom = Mathf.Max(0.0001f, maxExpectedRmsMv - activationThresholdMv);
        float activation = Mathf.Clamp01((rms - activationThresholdMv) / denom);
        transform.Translate(Vector3.right * activation * moveSpeed * Time.deltaTime, Space.World);
    }

    void OnDestroy()
    {
        listening = false;
        if (receiver != null) receiver.Close();
        if (thread != null && thread.IsAlive) thread.Join(1000);
    }
}
