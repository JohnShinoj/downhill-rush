using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// EMG demo using a recording instead of live acquisition, behaving like live:
    ///   Ch1 forearm (SPACE = "I flex my forearm") -> JUMP
    ///   Ch2 biceps  (F     = "I flex my biceps")  -> BACKFLIP (the game only accepts it while airborne)
    ///
    /// Both channels stream through the filters ALL the time at 1000 Hz (real speed):
    ///   - idle: real RESTING EMG from the recording loops through them,
    ///   - key press: the stream switches straight to the start of the next real contraction.
    /// Filter chain (EmgChannelDetector): band-pass 20-450 Hz -> rectify -> low-pass 5 Hz envelope.
    /// The game action happens ONLY when the envelope crosses that channel's ON threshold
    /// (~60-70 ms after the flex starts, the same delay as live). The keys never act directly.
    ///
    /// A/D steer and W/S speed are plain keyboard for now. F2 toggles the live plots.
    /// </summary>
    [AddComponentMenu("Ski Game/Input/Biosignal Input (EMG demo)")]
    public class BiosignalInput : GameInputBase
    {
        [Header("Recording")]
        [Tooltip("Relative paths are looked up in Assets/StreamingAssets. Absolute paths also work.")]
        [SerializeField] private string emgFile = "john_final.csv";
        [Tooltip("0-based columns. john_final.csv = sample,time_s,ch1_mv,ch2_mv")]
        [SerializeField] private int forearmColumn = 2;
        [SerializeField] private int bicepsColumn = 3;

        [Header("Forearm -> JUMP (envelope, mV)")]
        [SerializeField] private float jumpOn = 0.040f;
        [SerializeField] private float jumpOff = 0.020f;
        [SerializeField] private KeyCode forearmKey = KeyCode.Space;

        [Header("Biceps -> BACKFLIP (envelope, mV)")]
        [SerializeField] private float backflipOn = 0.151f;
        [SerializeField] private float backflipOff = 0.068f;
        [SerializeField] private KeyCode bicepsKey = KeyCode.F;

        [Header("Clips / display")]
        [Tooltip("Seconds of signal played after each contraction starts.")]
        [SerializeField, Range(0.5f, 2f)] private float clipAfter = 0.9f;
        [SerializeField] private bool showPlot = true;
        [SerializeField] private KeyCode plotKey = KeyCode.F2;

        private EmgReplayChannel forearm, biceps;
        private double sampleDebt;
        private bool jumpPulse, flipPulse, keyboardBroken;
        private string loadError;

        private void Start()
        {
            string path = Path.IsPathRooted(emgFile) ? emgFile : Path.Combine(Application.streamingAssetsPath, emgFile);
            if (!File.Exists(path)) { loadError = "EMG file not found: " + path; Debug.LogError("[SkiGame] " + loadError); return; }

            var c1 = new List<float>(140000);
            var c2 = new List<float>(140000);
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var ns = System.Globalization.NumberStyles.Float;
            foreach (string line in File.ReadLines(path))
            {
                string[] p = line.Split(',');
                if (p.Length > Mathf.Max(forearmColumn, bicepsColumn) &&
                    float.TryParse(p[forearmColumn], ns, ci, out float a) &&
                    float.TryParse(p[bicepsColumn], ns, ci, out float b))
                { c1.Add(a); c2.Add(b); }                    // header line fails to parse -> skipped
            }
            forearm = new EmgReplayChannel("Ch1 forearm -> JUMP", c1.ToArray(), jumpOn, jumpOff, clipAfter);
            biceps = new EmgReplayChannel("Ch2 biceps -> BACKFLIP", c2.ToArray(), backflipOn, backflipOff, clipAfter);
            Debug.Log($"[SkiGame] {c1.Count} samples loaded; forearm flexes {forearm.FlexCount}, biceps flexes {biceps.FlexCount}.");
        }

        protected override void Update()
        {
            if (Key(plotKey, true)) showPlot = !showPlot;
            jumpPulse = flipPulse = false;
            if (forearm != null)
            {
                if (Key(forearmKey, true)) forearm.Trigger();
                if (Key(bicepsKey, true)) biceps.Trigger();

                sampleDebt += Time.deltaTime * EmgChannelDetector.SampleRate;   // samples "arriving" this frame
                int n = (int)sampleDebt;
                sampleDebt -= n;
                jumpPulse = forearm.Stream(n);
                flipPulse = biceps.Stream(n);
            }
            base.Update();
        }

        // ---------------- GameInputBase hooks ----------------
        protected override float ReadSteer() => (Key(KeyCode.D) || Key(KeyCode.RightArrow) ? 1f : 0f) - (Key(KeyCode.A) || Key(KeyCode.LeftArrow) ? 1f : 0f);
        protected override float ReadSpeedControl() => (Key(KeyCode.W) || Key(KeyCode.UpArrow) ? 1f : 0f) - (Key(KeyCode.S) || Key(KeyCode.DownArrow) ? 1f : 0f);
        protected override bool ReadJumpRequest() => jumpPulse;       // ONLY the filtered forearm EMG can jump
        protected override bool ReadBackflipRequest() => flipPulse;   // ONLY the filtered biceps EMG can flip

        private bool Key(KeyCode k, bool down = false)
        {
            if (keyboardBroken) return false;
            try { return down ? Input.GetKeyDown(k) : Input.GetKey(k); }
            catch (System.InvalidOperationException) { keyboardBroken = true; return false; }
        }

        // ---------------- live plots ----------------
        private void OnGUI()
        {
            if (!showPlot) return;
            const float w = 340f;
            float x0 = Screen.width - w - 14f, y = 14f;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(x0 - 8, y - 8, w + 16, loadError != null ? 40 : 2 * 196 + 30), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (loadError != null) { GUI.Label(new Rect(x0, y, w, 24), loadError); return; }
            if (forearm == null) return;
            GUI.Label(new Rect(x0, y, w, 20), $"EMG DEMO  ({plotKey})    {forearmKey} = forearm flex,  {bicepsKey} = biceps flex"); y += 24;
            Panel(ref y, x0, w, forearm, forearmKey, new Color(0.3f, 0.6f, 1f));
            Panel(ref y, x0, w, biceps, bicepsKey, new Color(1f, 0.55f, 0.25f));
        }


        private static void Panel(ref float y, float x0, float w, EmgReplayChannel ch, KeyCode key, Color col)
        {
            const float rh = 50f, h = 80f;
            int L = EmgReplayChannel.PlotLen;
            float dx = w / L;
            GUI.Label(new Rect(x0, y, w, 20), $"<b>{ch.Name}</b>   events: {ch.Events}   {ch.Status}"); y += 20;

            // INPUT: raw signal fed to the filter (+-1 mV, min/max bars)
            float mid = y + rh / 2f, rs = (rh / 2f) / 1f;
            GUI.color = new Color(0.75f, 0.75f, 0.75f);
            for (int i = 0; i < L; i++)
            {
                int k = (ch.Head + 1 + i) % L;
                float hi = mid - Mathf.Clamp(ch.RawMax[k], -1f, 1f) * rs, lo = mid - Mathf.Clamp(ch.RawMin[k], -1f, 1f) * rs;
                GUI.DrawTexture(new Rect(x0 + i * dx, hi, 1.2f, Mathf.Max(1f, lo - hi)), Texture2D.whiteTexture);
            }
            y += rh + 4;

            // OUTPUT: envelope with ON/OFF lines; yellow = action sent to the game
            float on = (float)ch.Detector.OnThreshold, off = (float)ch.Detector.OffThreshold, sc = h / (on * 3f);
            for (int i = 0; i < L; i++)
            {
                int k = (ch.Head + 1 + i) % L;
                float px = x0 + i * dx;
                GUI.color = ch.Env[k] > on ? Color.green : col;
                GUI.DrawTexture(new Rect(px, y + h - Mathf.Min(h, ch.Env[k] * sc), 2, 2), Texture2D.whiteTexture);
                if (ch.Fired[k]) { GUI.color = Color.yellow; GUI.DrawTexture(new Rect(px, y, 2, h), Texture2D.whiteTexture); }
            }
            GUI.color = Color.white; GUI.DrawTexture(new Rect(x0, y + h - on * sc, w, 1), Texture2D.whiteTexture);
            GUI.Label(new Rect(x0 + w - 30, y + h - on * sc - 18, 40, 20), "ON");
            GUI.color = Color.gray; GUI.DrawTexture(new Rect(x0, y + h - off * sc, w, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;
            y += h + 18;
        }
    }
}
