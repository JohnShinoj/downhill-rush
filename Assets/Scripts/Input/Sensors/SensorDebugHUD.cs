using UnityEngine;

namespace SkiGame.Sensors
{
    /// <summary>
    /// F2 panel: what the sensors are doing right now.
    ///  - COP: raw / filtered position (cm), neutral, weight, steer and speed outputs
    ///  - EMG: envelope of each muscle against its ON / OFF thresholds, activation counts
    ///  - a 6-second scrolling plot of all four signals
    ///  F3 = re-measure neutral stance,  F4 = recalibrate EMG.
    /// </summary>
    public class SensorDebugHUD : MonoBehaviour
    {
        private SensorInput sensor;
        private bool visible = true;
        private GUIStyle label, small, header;

        private const int History = 360;           // samples kept for the plot
        private const float HistoryInterval = 1f / 60f;
        private readonly float[] hJump = new float[History], hFlip = new float[History], hSteer = new float[History], hSpeed = new float[History];
        private int head;
        private float sampleTimer;

        private void Awake() => sensor = GetComponent<SensorInput>();

        private void Update()
        {
            if (sensor == null) { sensor = GetComponent<SensorInput>(); if (sensor == null) return; }
            sampleTimer += Time.unscaledDeltaTime;
            while (sampleTimer >= HistoryInterval)
            {
                sampleTimer -= HistoryInterval;
                hJump[head] = (float)sensor.Emg.Jump.Normalized;
                hFlip[head] = (float)sensor.Emg.Backflip.Normalized;
                hSteer[head] = sensor.Cop.Steer;
                hSpeed[head] = sensor.Cop.Speed;
                head = (head + 1) % History;
            }
        }

        private void OnGUI()
        {
            if (sensor == null || sensor.Settings == null) return;
            Event e = Event.current;
            if (e != null && e.type == EventType.KeyDown && e.keyCode == sensor.Settings.debugPanelKey) { visible = !visible; e.Use(); }
            if (!visible) return;

            if (label == null)
            {
                label = HudDrawing.MakeLabel(17, Color.white);
                small = HudDrawing.MakeLabel(14, new Color(1f, 1f, 1f, 0.75f));
                header = HudDrawing.MakeLabel(19, new Color(0.45f, 0.9f, 1f), TextAnchor.UpperLeft, FontStyle.Bold);
            }

            Matrix4x4 old = HudDrawing.BeginCanvas();
            float w = 470, x = 20, y = 330;
            HudDrawing.Panel(new Rect(x, y, w, 560), new Color(0.02f, 0.04f, 0.1f, 0.8f));
            float cx = x + 14, cy = y + 8, iw = w - 28;

            var cop = sensor.Cop;
            var emg = sensor.Emg;
            var s = sensor.Settings;

            GUI.Label(new Rect(cx, cy, iw, 24), $"SENSORS  ({s.debugPanelKey} hide · {s.recalibrateCopKey} zero COP · {s.recalibrateEmgKey} cal EMG)", header); cy += 26;

            // ---- COP
            GUI.Label(new Rect(cx, cy, iw, 22), "COP: " + sensor.CopStatus, small); cy += 20;
            if (sensor.CopRunning)
            {
                string rate = sensor.CopReplayTime > 0 ? $"t {sensor.CopReplayTime:0.0} s" : $"{sensor.CopLiveRate:0} lines/s";
                GUI.Label(new Rect(cx, cy, iw, 22), $"raw {cop.RawX:+0.0;-0.0} / {cop.RawY:+0.0;-0.0} cm   filt {cop.FilteredX:+0.0;-0.0} / {cop.FilteredY:+0.0;-0.0}   {rate}", small); cy += 20;
                string neutral = cop.HasNeutral ? $"neutral {cop.NeutralX:+0.0;-0.0} / {cop.NeutralY:+0.0;-0.0} cm"
                                                : $"measuring neutral - STAND STILL {cop.NeutralProgress * 100f:0}%";
                GUI.Label(new Rect(cx, cy, iw, 22), $"{neutral}   weight {cop.Weight:0} kg {(cop.PersonOnBoard ? "" : "(no one)")}   glitches {cop.GlitchesRejected}", small); cy += 22;
                Analog(ref cy, cx, "Steer (L/R lean)", cop.Steer, new Color(0.3f, 0.8f, 1f));
                Analog(ref cy, cx, "Speed (F/B lean)", cop.Speed, new Color(0.4f, 1f, 0.5f));
            }
            cy += 4;

            // ---- EMG
            GUI.Label(new Rect(cx, cy, iw, 22), "EMG: " + sensor.EmgStatus, small); cy += 22;
            if (sensor.EmgRunning)
            {
                Muscle(ref cy, cx, emg.Jump, new Color(1f, 0.75f, 0.25f));
                Muscle(ref cy, cx, emg.Backflip, new Color(1f, 0.4f, 0.6f));
                if (!string.IsNullOrEmpty(emg.Jump.LastCalibrationMessage))
                { GUI.Label(new Rect(cx, cy, iw, 20), emg.Jump.LastCalibrationMessage, small); cy += 18; }
                if (!string.IsNullOrEmpty(emg.Backflip.LastCalibrationMessage))
                { GUI.Label(new Rect(cx, cy, iw, 20), emg.Backflip.LastCalibrationMessage, small); cy += 18; }
            }

            // ---- scrolling plot
            cy += 6;
            Rect plot = new Rect(cx, cy, iw, 130);
            HudDrawing.Fill(plot, new Color(0f, 0f, 0f, 0.35f));
            float mid = plot.y + plot.height * 0.5f;
            HudDrawing.Fill(new Rect(plot.x, mid, plot.width, 1), new Color(1f, 1f, 1f, 0.25f));
            // EMG threshold lines (EMG traces use the lower half: 0 at the bottom, 1 at the middle).
            ThresholdLine(plot, (float)emg.Jump.OnThreshold, new Color(1f, 0.75f, 0.25f, 0.5f));
            ThresholdLine(plot, (float)emg.Backflip.OnThreshold, new Color(1f, 0.4f, 0.6f, 0.5f));
            if (Event.current.type == EventType.Repaint)
            {
            Trace(plot, hSteer, new Color(0.3f, 0.8f, 1f), true);
            Trace(plot, hSpeed, new Color(0.4f, 1f, 0.5f, 0.8f), true);
            Trace(plot, hJump, new Color(1f, 0.75f, 0.25f), false);
            Trace(plot, hFlip, new Color(1f, 0.4f, 0.6f), false);
            }
            GUI.Label(new Rect(plot.x + 4, plot.yMax + 2, iw, 20), "last 6 s:  steer  speed  (centre = 0)    jump  backflip  (bottom = rest, dashes = ON)", small);

            GUI.matrix = old;
        }

        private void Analog(ref float cy, float cx, string name, float value, Color color)
        {
            GUI.Label(new Rect(cx, cy, 170, 22), name, label);
            HudDrawing.CenterBar(new Rect(cx + 170, cy + 5, 200, 12), value, color);
            GUI.Label(new Rect(cx + 380, cy, 70, 22), value.ToString("+0.00;-0.00;0.00"), label);
            cy += 24;
        }

        private void Muscle(ref float cy, float cx, EmgChannel ch, Color color)
        {
            GUI.Label(new Rect(cx, cy, 170, 22), ch.Name, label);
            Rect bar = new Rect(cx + 170, cy + 5, 200, 12);
            HudDrawing.Fill(bar, new Color(1f, 1f, 1f, 0.12f));
            float v = Mathf.Clamp01((float)ch.Normalized);
            HudDrawing.Fill(new Rect(bar.x, bar.y, bar.width * v, bar.height), ch.Active ? color : new Color(color.r, color.g, color.b, 0.45f));
            HudDrawing.Fill(new Rect(bar.x + bar.width * (float)ch.OnThreshold, bar.y - 3, 2, bar.height + 6), Color.white);
            HudDrawing.Fill(new Rect(bar.x + bar.width * (float)ch.OffThreshold, bar.y - 1, 1, bar.height + 2), new Color(1f, 1f, 1f, 0.5f));
            GUI.Label(new Rect(cx + 380, cy, 90, 22), $"{(ch.Active ? "ON" : "--")} x{ch.Activations}", label);
            cy += 24;
            if (ch.Calibrating)
            {
                GUI.Label(new Rect(cx + 170, cy - 4, 280, 20), $"calibrating {ch.CalibrationProgress * 100f:0}% - rest, then contract", small);
                cy += 16;
            }
        }

        private void ThresholdLine(Rect plot, float level, Color c)
        {
            float yy = plot.yMax - Mathf.Clamp01(level) * plot.height * 0.5f;
            for (float xx = plot.x; xx < plot.xMax; xx += 12f) HudDrawing.Fill(new Rect(xx, yy, 6, 1), c);
        }

        private void Trace(Rect plot, float[] data, Color c, bool centered)
        {
            float step = plot.width / History;
            for (int i = 0; i < History; i++)
            {
                float v = data[(head + i) % History];
                float yy = centered
                    ? plot.y + plot.height * 0.5f - Mathf.Clamp(v, -1f, 1f) * plot.height * 0.5f
                    : plot.yMax - Mathf.Clamp01(v) * plot.height * 0.5f;
                HudDrawing.Fill(new Rect(plot.x + i * step, yy - 1, Mathf.Max(1f, step), 2), c);
            }
        }
    }
}
