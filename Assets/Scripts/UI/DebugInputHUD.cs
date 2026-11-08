using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// Toggleable (F1) overlay showing the LIVE values of the active IGameInput:
    /// Steer, SpeedControl, JumpTriggered, BackflipTriggered - plus the timing
    /// assists (buffers, coyote time) and player state. Essential when tuning the
    /// EMG / Wii Balance Board signals in the next edit.
    /// Single-frame triggers are "latched" for a short time so you can see them.
    /// </summary>
    public class DebugInputHUD : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private bool visible = false;
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;
        [Tooltip("How long a single-frame trigger stays lit on screen (s).")]
        [SerializeField] private float triggerDisplayTime = 0.35f;

        private float jumpLit, flipLit;
        private int jumpCount, flipCount;
        private GUIStyle label, header;

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            jumpLit = Mathf.Max(0f, jumpLit - dt);
            flipLit = Mathf.Max(0f, flipLit - dt);
            IGameInput input = player != null ? player.InputSource : null;
            if (input == null) return;
            if (input.JumpTriggered) { jumpLit = triggerDisplayTime; jumpCount++; }
            if (input.BackflipTriggered) { flipLit = triggerDisplayTime; flipCount++; }
        }

        private void OnGUI()
        {
            Event e = Event.current;
            if (e != null && e.type == EventType.KeyDown && e.keyCode == toggleKey) { visible = !visible; e.Use(); }
            if (!visible || player == null) return;

            if (label == null)
            {
                label = HudDrawing.MakeLabel(20, Color.white);
                header = HudDrawing.MakeLabel(22, new Color(1f, 0.8f, 0.2f), TextAnchor.UpperLeft, FontStyle.Bold);
            }

            Matrix4x4 old = HudDrawing.BeginCanvas();
            IGameInput input = player.InputSource;
            float w = 420, x = HudDrawing.VirtualWidth - w - 20, y = 170;
            HudDrawing.Panel(new Rect(x, y, w, 440), new Color(0.02f, 0.04f, 0.1f, 0.78f));
            float cx = x + 16, cy = y + 10;

            GUI.Label(new Rect(cx, cy, w, 28), $"INPUT DEBUG  ({toggleKey})", header); cy += 30;
            GUI.Label(new Rect(cx, cy, w, 26), "Source: " + (input != null ? input.GetType().Name : "none"), label); cy += 34;

            if (input != null)
            {
                Analog(ref cy, cx, "Steer", input.Steer, new Color(0.3f, 0.8f, 1f));
                Analog(ref cy, cx, "SpeedControl", input.SpeedControl, new Color(0.4f, 1f, 0.5f));
                if (input is GameInputBase b)
                {
                    GUI.Label(new Rect(cx, cy, w, 24), $"raw target: steer {b.RawSteer:+0.00;-0.00;0.00}   speed {b.RawSpeedControl:+0.00;-0.00;0.00}", label);
                    cy += 30;
                }
                Trigger(ref cy, cx, "JumpTriggered", input.JumpTriggered, jumpLit, jumpCount);
                Trigger(ref cy, cx, "BackflipTriggered", input.BackflipTriggered, flipLit, flipCount);
            }

            cy += 6;
            HudDrawing.Fill(new Rect(cx, cy, w - 32, 1), new Color(1f, 1f, 1f, 0.3f)); cy += 8;
            GUI.Label(new Rect(cx, cy, w, 24), $"Grounded {player.IsGrounded}   Coyote {player.InCoyoteTime}", label); cy += 26;
            GUI.Label(new Rect(cx, cy, w, 24), $"Flipping {player.IsFlipping} ({player.FlipProgress * 360f:0}°)   flips {player.FlipsThisAirtime}", label); cy += 26;
            GUI.Label(new Rect(cx, cy, w, 24), $"Buffer  jump {player.JumpBufferRemaining * 1000f:0} ms   flip {player.FlipBufferRemaining * 1000f:0} ms", label); cy += 26;
            GUI.Label(new Rect(cx, cy, w, 24), $"Speed {player.Speed:0.0} m/s   height {player.HeightAboveGround:0.00} m", label); cy += 26;
            GUI.Label(new Rect(cx, cy, w, 24), $"Stunned {player.IsStunned}   Invulnerable {player.IsInvulnerable}", label);

            GUI.matrix = old;
        }

        private void Analog(ref float cy, float cx, string name, float value, Color color)
        {
            GUI.Label(new Rect(cx, cy, 180, 24), name, label);
            GUI.Label(new Rect(cx + 300, cy, 90, 24), value.ToString("+0.00;-0.00;0.00"), label);
            HudDrawing.CenterBar(new Rect(cx + 150, cy + 6, 140, 12), value, color);
            cy += 30;
        }

        private void Trigger(ref float cy, float cx, string name, bool now, float lit, int count)
        {
            GUI.Label(new Rect(cx, cy, 200, 24), name, label);
            HudDrawing.Fill(new Rect(cx + 200, cy + 3, 18, 18), lit > 0f ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 1f, 1f, 0.2f));
            GUI.Label(new Rect(cx + 230, cy, 160, 24), $"{(now ? "TRUE" : "false")}  x{count}", label);
            cy += 30;
        }
    }
}
