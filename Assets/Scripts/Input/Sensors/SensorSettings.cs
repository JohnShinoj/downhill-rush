using UnityEngine;

namespace SkiGame.Sensors
{
    public enum SignalSource
    {
        /// <summary>Not used - the keyboard controls this channel.</summary>
        Off,
        /// <summary>Play back a recorded file in real time (loops).</summary>
        Replay,
        /// <summary>Follow a text file that another program keeps appending to (read-only).</summary>
        LiveFile,
        /// <summary>EMG only: receive live samples from the BioRadio Python bridge over UDP (127.0.0.1).</summary>
        LiveBridge
    }

    /// <summary>
    /// Every setting for the EMG (jump / backflip) and Wii Balance Board COP (steering / speed)
    /// controls. Lives in Assets/Resources/SensorSettings.asset so it is found automatically.
    /// The filter numbers were chosen from the analysis in SENSORS.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Ski Game/Sensor Settings", fileName = "SensorSettings")]
    public class SensorSettings : ScriptableObject
    {
        [Header("Master switch  (F6 in game switches LIVE <-> REPLAY for a quick test)")]
        [Tooltip("Use the EMG + balance-board input. Off = keyboard only (the old behaviour).")]
        public bool useSensors = true;
        [Tooltip("Keyboard keys keep working alongside the sensors (handy for testing).")]
        public bool keyboardAlsoWorks = true;

        // ------------------------------------------------------------------ COP
        [Header("COP (Wii Balance Board) - source")]
        [Tooltip("Off / Replay (recorded file) / LiveFile (WiiMote1_All.txt). LiveBridge is EMG-only and means Off here.")]
        public SignalSource copSource = SignalSource.LiveFile;
        [Tooltip("Recorded file from the Wii Balance Board Recorder (Time, CpX, CpY, ... Weight). Relative paths start at the project folder.")]
        public string copReplayFile = "Assets/StreamingAssets/John14.txt";
        [Tooltip("The live file written by the board software. Opened READ-ONLY; it is never changed.")]
        public string copLiveFile = @"C:\Users\praka\OneDrive\Desktop\Balance Board\Balance Board\WiiMote1_All.txt";

        [Header("COP - live file columns (0-based, whitespace separated)")]
        [Tooltip("Column with COP X (medio-lateral). WiiMote1_All.txt: column 4 (same as the professor's script).")]
        public int liveColumnX = 4;
        [Tooltip("Column with COP Y (antero-posterior). WiiMote1_All.txt: column 5.")]
        public int liveColumnY = 5;
        [Tooltip("Column with the total weight in kg (used to detect 'nobody on the board'). -1 = none.")]
        public int liveColumnWeight = 6;
        [Tooltip("Flip left/right. The professor's script draws -X, i.e. +X in WiiMote1_All.txt = LEFT, so this is on for the live file.")]
        public bool liveInvertX = true;
        [Tooltip("Flip forward/back for the live file.")]
        public bool liveInvertY = false;
        [Tooltip("Flip left/right for the recorded (Recorder) files. Off: +CpX = right in John14-16.")]
        public bool replayInvertX = false;
        [Tooltip("Flip forward/back for the recorded files. Off: +CpY = forward in John14-16.")]
        public bool replayInvertY = false;

        [Header("COP - filtering (see SENSORS.md)")]
        [Tooltip("Internal processing rate (Hz). Input samples are held / interpolated onto this clock.")]
        public float copProcessRate = 100f;
        [Tooltip("2nd-order Butterworth low-pass cut-off (Hz). Voluntary leans are < ~2 Hz; sway jitter and sensor noise are above.")]
        public float copLowPassHz = 3f;
        [Tooltip("A sample that jumps more than this (cm) from the previous one is treated as a glitch and ignored (max 3 in a row).")]
        public float copMaxJumpCm = 6f;
        [Tooltip("Below this total weight (kg) nobody is on the board: steering and speed go to 0.")]
        public float copMinWeightKg = 20f;
        [Tooltip("If the live file stops updating for this long (s) the input goes to neutral.")]
        public float copStaleTimeout = 0.5f;

        [Header("COP - calibration and scaling")]
        [Tooltip("Seconds of quiet standing averaged at the start to find the neutral (centre) position. F3 re-does it.")]
        public float copNeutralSeconds = 2f;
        [Tooltip("Lean (cm from neutral) that gives FULL steering. Recorded leans reach ~12-13 cm, so 10 cm = comfortable full turn.")]
        public float steerFullScaleCm = 10f;
        [Tooltip("Lean (cm from neutral) that gives full boost / full brake. Forward-back leans reach ~7 cm.")]
        public float speedFullScaleCm = 6f;
        [Tooltip("Use forward / back lean for speed (forward = faster, back = brake).")]
        public bool copControlsSpeed = true;

        [Header("COP - game-side response (applied to the input smoothing)")]
        [Tooltip("Dead zone on steering (fraction of full scale). 0.08 x 10 cm = 0.8 cm of stance sway ignored.")]
        [Range(0f, 0.5f)] public float steerDeadZone = 0.08f;
        [Tooltip("Dead zone on speed. Leaning sideways also moves COP back by ~1.5-2 cm, so this is larger (0.3 x 6 cm = 1.8 cm).")]
        [Range(0f, 0.6f)] public float speedDeadZone = 0.3f;
        [Tooltip("Extra ramp time on steering (s). The low-pass already smooths, so this is shorter than for the keyboard.")]
        public float steerResponseTime = 0.05f;
        public float steerReleaseTime = 0.04f;
        public float speedResponseTime = 0.15f;

        // ------------------------------------------------------------------ EMG
        [Header("EMG - source")]
        [Tooltip("Off / Replay (recorded file) / LiveFile (growing CSV) / LiveBridge (BioRadio Python bridge over UDP).")]
        public SignalSource emgSource = SignalSource.LiveBridge;
        [Tooltip("Recorded BioRadio export (sample, time_s, ch1_mv, ch2_mv). Relative paths start at the project folder.")]
        public string emgReplayFile = "Assets/StreamingAssets/john_final.csv";
        [Tooltip("Live EMG text/CSV file another program keeps appending to (same columns as the export). Opened read-only.")]
        public string emgLiveFile = "";
        [Header("EMG - live bridge (Tools/EmgBridge/ski_emg_bridge.py)")]
        [Tooltip("UDP port the bridge sends to (the BioRadio kit uses 50557).")]
        public int bridgePort = 50557;
        [Tooltip("BioRadio channel number used for JUMP (forearm flexor, FCU).")]
        public int bridgeJumpChannel = 1;
        [Tooltip("BioRadio channel number used for BACKFLIP (biceps).")]
        public int bridgeBackflipChannel = 2;
        [Tooltip("TEST ONLY: Unity sends the recorded EMG to itself in the bridge format, to check the UDP path without hardware.")]
        public bool bridgeSelfTest = false;
        [Tooltip("TEST ONLY: Unity writes the recorded COP into a temporary file in the live format and follows it, to check the live-file path without the board. The real WiiMote file is not touched.")]
        public bool copLiveSelfTest = false;

        [Tooltip("Sampling rate of the EMG (Hz). BioRadio: 1000. With the bridge it is taken from the bridge automatically.")]
        public float emgSampleRate = 1000f;
        [Tooltip("Column of the JUMP muscle (FCU) in the file. john_final.csv: 2 = ch1_mv.")]
        public int jumpColumn = 2;
        [Tooltip("Column of the BACKFLIP muscle (biceps) in the file. john_final.csv: 3 = ch2_mv.")]
        public int backflipColumn = 3;

        [Header("EMG - filtering (see SENSORS.md)")]
        [Tooltip("Band-pass low edge: 4th-order Butterworth high-pass (Hz). Removes motion artefact and baseline drift.")]
        public float emgHighPassHz = 20f;
        [Tooltip("Band-pass high edge: 4th-order Butterworth low-pass (Hz).")]
        public float emgLowPassHz = 450f;
        [Tooltip("Mains notch (Hz). India = 50. 0 = off.")]
        public float emgNotchHz = 50f;
        public float emgNotchQ = 30f;
        [Tooltip("Envelope: full-wave rectify, then 2nd-order Butterworth low-pass at this frequency (Hz).")]
        public float emgEnvelopeHz = 5f;

        [Header("EMG - detection (fractions of the calibrated range: 0 = rest, 1 = strong contraction)")]
        [Range(0f, 1f)] public float jumpOnThreshold = 0.15f;
        [Range(0f, 1f)] public float jumpOffThreshold = 0.08f;
        [Range(0f, 1f)] public float backflipOnThreshold = 0.40f;
        [Range(0f, 1f)] public float backflipOffThreshold = 0.10f;
        [Tooltip("The envelope must stay above the ON threshold this long (ms) before it counts. Rejects spikes.")]
        public float emgMinOnMs = 20f;

        [Header("EMG - calibration")]
        [Tooltip("Replay: seconds of the recording used to measure rest (10th percentile) and contraction (99th percentile).")]
        public float emgCalibrationSeconds = 30f;
        [Tooltip("Guided calibration (F4): seconds to RELAX, then to flex the FOREARM, then to flex the BICEPS.")]
        public float calibRelaxSeconds = 4f, calibFlexSeconds = 6f;
        [Tooltip("Remember each player's EMG calibration between sessions.")]
        public bool saveCalibration = true;
        [Tooltip("Used until calibration finishes (live) - envelope values in mV measured from john_final.csv.")]
        public float jumpRestMv = 0.0049f;
        public float jumpMaxMv = 0.229f;
        public float backflipRestMv = 0.0036f;
        public float backflipMaxMv = 0.566f;

        [Header("Hotkeys")]
        public KeyCode debugPanelKey = KeyCode.F2;
        public KeyCode recalibrateCopKey = KeyCode.F3;
        public KeyCode recalibrateEmgKey = KeyCode.F4;
        public KeyCode switchProfileKey = KeyCode.F6;

        /// <summary>Resolves a path: absolute stays as is, relative starts at the project folder.</summary>
        public static string Resolve(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            if (System.IO.Path.IsPathRooted(path)) return path;
            string root = System.IO.Path.GetDirectoryName(Application.dataPath);
            string full = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, path));
            if (System.IO.File.Exists(full)) return full;
            // In a built game the project folder doesn't exist: look in StreamingAssets by file name.
            string alt = System.IO.Path.Combine(Application.streamingAssetsPath, System.IO.Path.GetFileName(path));
            return System.IO.File.Exists(alt) ? alt : full;
        }
    }
}
