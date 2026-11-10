using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkiGame.Sensors
{
    /// <summary>
    /// IGameInput driven by the sensors:
    ///   Wii Balance Board COP, left/right lean  -> Steer
    ///   Wii Balance Board COP, forward/back lean -> SpeedControl (optional)
    ///   EMG forearm flexor (FCU) contraction     -> JumpTriggered
    ///   EMG biceps contraction                   -> BackflipTriggered
    ///
    /// Each signal can come from a recorded file (Replay, loops in real time) or from a text
    /// file that another program keeps appending to (LiveFile, opened read-only).
    /// Settings: Assets/Resources/SensorSettings.asset. Installed automatically by SensorBootstrap.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("Ski Game/Input/Sensor Input (EMG + COP)")]
    public class SensorInput : GameInputBase
    {
        public SensorSettings Settings { get; private set; }
        public CopProcessor Cop { get; private set; }
        public EmgProcessor Emg { get; private set; }

        public bool CopRunning { get; private set; }
        public bool EmgRunning { get; private set; }
        public string CopStatus { get; private set; } = "";
        public string EmgStatus { get; private set; } = "";
        /// <summary>Measured input rate of the live COP file (lines per second).</summary>
        public float CopLiveRate { get; private set; }
        public float EmgLiveRate { get; private set; }
        public bool CopStale { get; private set; }
        public double CopReplayTime => copReplayTime;
        public double EmgReplayTime => emgReplayPos / System.Math.Max(1.0, Settings != null ? Settings.emgSampleRate : 1000.0);

        // Values carried over a scene reload (Restart) so the player does not have to recalibrate.
        private static bool carriedNeutral;
        private static double carriedNeutralX, carriedNeutralY;
        private static bool carriedEmg;
        private static double carriedJumpRest, carriedJumpMax, carriedFlipRest, carriedFlipMax;

        private RecordedCop copRecording;
        private RecordedEmg emgRecording;
        private TextFileTailer copTail, emgTail;
        private double copReplayTime, copAccumulator, emgReplayPos;
        private readonly List<string> lines = new List<string>(256);
        private readonly List<double> values = new List<double>(16);
        private readonly List<Vector3> copSamples = new List<Vector3>(64);   // x, y, weight
        private Vector3 copHold = new Vector3(float.NaN, float.NaN, 0f);
        private float lastCopLineTime = -999f, lastEmgLineTime = -999f;
        private float rateWindowStart; private int copRateCount, emgRateCount;
        private bool keyboardBroken;

        private static readonly KeyCode[] LeftKeys = { KeyCode.A, KeyCode.LeftArrow };
        private static readonly KeyCode[] RightKeys = { KeyCode.D, KeyCode.RightArrow };
        private static readonly KeyCode[] UpKeys = { KeyCode.W, KeyCode.UpArrow };
        private static readonly KeyCode[] DownKeys = { KeyCode.S, KeyCode.DownArrow };

        private void Awake()
        {
            Settings = Resources.Load<SensorSettings>("SensorSettings");
            if (Settings == null) Settings = ScriptableObject.CreateInstance<SensorSettings>();
            ConfigureResponse(Settings.steerResponseTime, Settings.steerReleaseTime, Settings.speedResponseTime,
                              Settings.steerDeadZone, Settings.speedDeadZone);
            Cop = new CopProcessor(Settings);
            Emg = new EmgProcessor(Settings);
            StartCop();
            StartEmg();
            rateWindowStart = Time.unscaledTime;
        }

        // ------------------------------------------------------------------ setup

        private void StartCop()
        {
            CopRunning = false;
            switch (Settings.copSource)
            {
                case SignalSource.Replay:
                    string path = SensorSettings.Resolve(Settings.copReplayFile);
                    try
                    {
                        copRecording = RecordedCop.Load(path);
                        CopRunning = true;
                        CopStatus = $"Replay {System.IO.Path.GetFileName(path)}  ({copRecording.SampleRate:0.#} Hz, {copRecording.Duration:0} s, loops)";
                    }
                    catch (System.Exception e)
                    {
                        CopStatus = "COP replay file not loaded: " + e.Message;
                        Debug.LogWarning("[SkiGame] " + CopStatus + "  (" + path + ")");
                    }
                    break;
                case SignalSource.LiveFile:
                    copTail = new TextFileTailer(SensorSettings.Resolve(Settings.copLiveFile));
                    CopRunning = true;
                    CopStatus = "Live " + System.IO.Path.GetFileName(copTail.Path);
                    if (carriedNeutral) Cop.SetNeutral(carriedNeutralX, carriedNeutralY);
                    break;
                default:
                    CopStatus = "Off (keyboard steering)";
                    break;
            }
        }

        private void StartEmg()
        {
            EmgRunning = false;
            switch (Settings.emgSource)
            {
                case SignalSource.Replay:
                    string path = SensorSettings.Resolve(Settings.emgReplayFile);
                    try
                    {
                        emgRecording = RecordedEmg.Load(path, Settings.jumpColumn, Settings.backflipColumn);
                        EmgProcessor.CalibrateFromRecording(emgRecording, Settings, Emg);
                        EmgRunning = true;
                        EmgStatus = $"Replay {System.IO.Path.GetFileName(path)}  ({emgRecording.Count / Settings.emgSampleRate:0} s, loops)";
                    }
                    catch (System.Exception e)
                    {
                        EmgStatus = "EMG replay file not loaded: " + e.Message;
                        Debug.LogWarning("[SkiGame] " + EmgStatus + "  (" + path + ")");
                    }
                    break;
                case SignalSource.LiveFile:
                    if (string.IsNullOrWhiteSpace(Settings.emgLiveFile))
                    {
                        EmgStatus = "Live EMG file not set (Sensor Settings > Emg Live File)";
                        break;
                    }
                    emgTail = new TextFileTailer(SensorSettings.Resolve(Settings.emgLiveFile));
                    EmgRunning = true;
                    EmgStatus = "Live " + System.IO.Path.GetFileName(emgTail.Path);
                    if (carriedEmg)
                    {
                        Emg.Jump.SetCalibration(carriedJumpRest, carriedJumpMax);
                        Emg.Backflip.SetCalibration(carriedFlipRest, carriedFlipMax);
                    }
                    else Emg.BeginCalibration(Settings.emgCalibrationSeconds);
                    break;
                default:
                    EmgStatus = "Off (keyboard Space / F)";
                    break;
            }
        }

        // ------------------------------------------------------------------ per frame

        protected override void Update()
        {
            HandleHotkeys();
            if (CopRunning) { if (copRecording != null) UpdateCopReplay(Time.deltaTime); else UpdateCopLive(Time.unscaledDeltaTime); }
            if (EmgRunning) { if (emgRecording != null) UpdateEmgReplay(Time.deltaTime); else UpdateEmgLive(); }
            UpdateRates();
            base.Update();   // reads ReadSteer / ReadSpeedControl / ReadJumpRequest / ReadBackflipRequest
        }

        private void UpdateCopReplay(float dt)
        {
            copAccumulator += dt;
            int guard = 0;
            while (copAccumulator >= Cop.TickInterval && guard++ < 50)
            {
                copAccumulator -= Cop.TickInterval;
                copReplayTime += Cop.TickInterval;
                copRecording.Sample(copReplayTime, out double x, out double y, out double w);
                if (Settings.replayInvertX) x = -x;
                if (Settings.replayInvertY) y = -y;
                Cop.Tick(x, y, w);
            }
            if (guard >= 50) copAccumulator = 0;
        }

        private void UpdateCopLive(float dt)
        {
            lines.Clear();
            copTail.ReadNewLines(lines);
            copSamples.Clear();
            foreach (string line in lines)
            {
                if (LineParser.Parse(line, values) == 0) continue;
                double x = LineParser.Get(values, Settings.liveColumnX);
                double y = LineParser.Get(values, Settings.liveColumnY);
                double w = Settings.liveColumnWeight >= 0 ? LineParser.Get(values, Settings.liveColumnWeight) : double.NaN;
                if (double.IsNaN(x) || double.IsNaN(y)) continue;
                if (Settings.liveInvertX) x = -x;
                if (Settings.liveInvertY) y = -y;
                copSamples.Add(new Vector3((float)x, (float)y, (float)w));
            }
            if (copSamples.Count > 0) { lastCopLineTime = Time.unscaledTime; copRateCount += copSamples.Count; }

            CopStale = Time.unscaledTime - lastCopLineTime > Settings.copStaleTimeout;
            if (copTail.LastError != null) CopStatus = "Live file: " + copTail.LastError;
            else CopStatus = "Live " + System.IO.Path.GetFileName(copTail.Path) + (CopStale ? "  (no new data)" : "");

            // Spread the samples that arrived this frame over the internal ticks of this frame.
            copAccumulator += dt;
            int ticks = 0;
            while (copAccumulator >= Cop.TickInterval && ticks < 50) { copAccumulator -= Cop.TickInterval; ticks++; }
            if (ticks >= 50) copAccumulator = 0;
            for (int i = 0; i < ticks; i++)
            {
                if (copSamples.Count > 0) copHold = copSamples[Mathf.Min(copSamples.Count - 1, i * copSamples.Count / ticks)];
                if (CopStale) Cop.Tick(double.NaN, double.NaN, 0);
                else Cop.Tick(copHold.x, copHold.y, float.IsNaN(copHold.z) ? double.NaN : copHold.z);
            }
            if (ticks == 0 && copSamples.Count > 0) copHold = copSamples[copSamples.Count - 1];
            if (Cop.HasNeutral) { carriedNeutral = true; carriedNeutralX = Cop.NeutralX; carriedNeutralY = Cop.NeutralY; }
        }

        private void UpdateEmgReplay(float dt)
        {
            int n = emgRecording.Count;
            double next = emgReplayPos + dt * Settings.emgSampleRate;
            int from = (int)emgReplayPos, to = (int)next;
            if (to - from > 5000) from = to - 5000;          // after a long hitch, skip ahead
            for (int i = from; i < to; i++)
            {
                int k = i % n;
                Emg.Process(emgRecording.Jump[k], emgRecording.Backflip[k]);
            }
            emgReplayPos = next;
        }

        private void UpdateEmgLive()
        {
            lines.Clear();
            emgTail.ReadNewLines(lines);
            int got = 0;
            foreach (string line in lines)
            {
                if (LineParser.Parse(line, values) == 0) continue;
                double a = LineParser.Get(values, Settings.jumpColumn), b = LineParser.Get(values, Settings.backflipColumn);
                if (double.IsNaN(a) || double.IsNaN(b)) continue;
                Emg.Process(a, b);
                got++;
            }
            if (got > 0) { lastEmgLineTime = Time.unscaledTime; emgRateCount += got; }
            bool stale = Time.unscaledTime - lastEmgLineTime > 0.5f;
            EmgStatus = emgTail.LastError != null ? "Live EMG file: " + emgTail.LastError
                      : "Live " + System.IO.Path.GetFileName(emgTail.Path) + (stale ? "  (no new data)" : "")
                        + (Emg.Calibrating ? $"  CALIBRATING {Emg.Jump.CalibrationProgress * 100f:0}%" : "");
            if (!Emg.Calibrating && (Emg.Jump.LastCalibrationSucceeded || Emg.Backflip.LastCalibrationSucceeded))
            {
                carriedEmg = true;
                carriedJumpRest = Emg.Jump.RestLevel; carriedJumpMax = Emg.Jump.MaxLevel;
                carriedFlipRest = Emg.Backflip.RestLevel; carriedFlipMax = Emg.Backflip.MaxLevel;
            }
        }

        private void UpdateRates()
        {
            float span = Time.unscaledTime - rateWindowStart;
            if (span < 1f) return;
            CopLiveRate = copRateCount / span;
            EmgLiveRate = emgRateCount / span;
            copRateCount = emgRateCount = 0;
            rateWindowStart = Time.unscaledTime;
        }

        private void HandleHotkeys()
        {
            if (KeyDown(Settings.recalibrateCopKey))
            {
                Cop.BeginNeutralCalibration();
                carriedNeutral = false;
                Debug.Log("[SkiGame] COP: re-measuring the neutral stance - stand still for " + Settings.copNeutralSeconds + " s.");
            }
            if (KeyDown(Settings.recalibrateEmgKey))
            {
                Emg.BeginCalibration(Settings.emgCalibrationSeconds);
                carriedEmg = false;
                Debug.Log("[SkiGame] EMG: calibrating for " + Settings.emgCalibrationSeconds + " s - rest, then do a few strong contractions of each muscle.");
            }
        }

        // ------------------------------------------------------------------ IGameInput sources

        protected override float ReadSteer()
        {
            float k = KeyAxis(LeftKeys, RightKeys);
            if (k != 0f || !CopRunning) return k;
            return Cop.Steer;
        }

        protected override float ReadSpeedControl()
        {
            float k = KeyAxis(DownKeys, UpKeys);
            if (k != 0f || !CopRunning) return k;
            return Cop.Speed;
        }

        protected override bool ReadJumpRequest()
        {
            bool emg = EmgRunning && Emg.Jump.ConsumeRequest();
            return emg || Key(KeyCode.Space);
        }

        protected override bool ReadBackflipRequest()
        {
            bool emg = EmgRunning && Emg.Backflip.ConsumeRequest();
            return emg || Key(KeyCode.F);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            copTail?.Dispose();
            emgTail?.Dispose();
        }

        private void OnDestroy()
        {
            copTail?.Dispose();
            emgTail?.Dispose();
        }

        // ------------------------------------------------------------------ keyboard (optional)

        private float KeyAxis(KeyCode[] negative, KeyCode[] positive)
        {
            if (!Settings.keyboardAlsoWorks) return 0f;
            float v = 0f;
            foreach (var k in positive) if (Key(k)) { v += 1f; break; }
            foreach (var k in negative) if (Key(k)) { v -= 1f; break; }
            return v;
        }

        private bool Key(KeyCode key)
        {
            if (keyboardBroken || !Settings.keyboardAlsoWorks) return false;
            try { return Input.GetKey(key); }
            catch (System.InvalidOperationException) { keyboardBroken = true; return false; }
        }

        private bool KeyDown(KeyCode key)
        {
            if (keyboardBroken || key == KeyCode.None) return false;
            try { return Input.GetKeyDown(key); }
            catch (System.InvalidOperationException) { keyboardBroken = true; return false; }
        }
    }

    /// <summary>
    /// Puts SensorInput on the Player whenever a scene loads (also after Restart), if
    /// SensorSettings.useSensors is on. No scene editing needed.
    /// </summary>
    public static class SensorBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var settings = Resources.Load<SensorSettings>("SensorSettings");
            if (settings == null || !settings.useSensors) return;
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null) return;

            var sensor = player.GetComponent<SensorInput>();
            if (sensor == null) sensor = player.gameObject.AddComponent<SensorInput>();
            sensor.enabled = true;
            foreach (var kb in player.GetComponents<KeyboardInput>()) kb.enabled = false;
            player.SetInputSource(sensor);

            if (Object.FindAnyObjectByType<SensorDebugHUD>() == null)
                player.gameObject.AddComponent<SensorDebugHUD>();
            Debug.Log($"[SkiGame] Sensor input active.  COP: {sensor.CopStatus}   EMG: {sensor.EmgStatus}");
        }
    }
}
