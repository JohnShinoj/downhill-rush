using System.Collections.Generic;
using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// All game screens: main menu, in-game HUD (score, coins, speed, distance, lives,
    /// multiplier, difficulty, course progress), popups ("BACKFLIP x2!"), pause and
    /// game over. Reads state from the managers and listens to GameEvents; it never
    /// changes gameplay directly - it only calls GameManager methods.
    ///
    /// Menu keys (UI only, not gameplay): Enter = start / restart, Esc or P = pause,
    /// R = restart, M = main menu.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameManager game;
        [SerializeField] private ScoreManager score;
        [SerializeField] private PlayerController player;
        [SerializeField] private DifficultyManager difficulty;
        [SerializeField] private SlopeGenerator slope;

        [Header("Look")]
        [SerializeField] private string gameTitle = "DOWNHILL RUSH";
        [SerializeField] private Color panelColor = new Color(0.03f, 0.06f, 0.13f, 0.68f);
        [SerializeField] private Color accentColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.32f, 0.3f);
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private float popupDuration = 1.5f;

        private class Popup { public string Text; public Color Color; public int Size; public float Age; public float Duration; }
        private readonly List<Popup> popups = new List<Popup>();

        private GUIStyle title, header, label, small, big, center, centerSmall, button, selectedButton, popupStyle;

        private void Awake()
        {
            if (game == null) game = FindAnyObjectByType<GameManager>();
            if (score == null) score = FindAnyObjectByType<ScoreManager>();
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (difficulty == null) difficulty = FindAnyObjectByType<DifficultyManager>();
            if (slope == null) slope = FindAnyObjectByType<SlopeGenerator>();
        }

        private void OnEnable()
        {
            GameEvents.BackflipLanded += OnBackflipLanded;
            GameEvents.Crashed += OnCrashed;
            GameEvents.PopupRequested += AddPopup;
        }

        private void OnDisable()
        {
            GameEvents.BackflipLanded -= OnBackflipLanded;
            GameEvents.Crashed -= OnCrashed;
            GameEvents.PopupRequested -= AddPopup;
        }

        private void OnBackflipLanded(int flips, int multiplier, int points)
        {
            string text = flips > 1 ? $"{flips}x BACKFLIP x{multiplier}!" : $"BACKFLIP x{multiplier}!";
            AddPopup(text, accentColor, 76);
            AddPopup($"+{points}", accentColor, 48);
        }

        private void OnCrashed(string reason)
        {
            AddPopup("CRASH!  -1 life", dangerColor, 64);
            AddPopup(reason, textColor, 36);
        }

        private void AddPopup(string text, Color color) => AddPopup(text, color, 52);

        private void AddPopup(string text, Color color, int size)
        {
            popups.Add(new Popup { Text = text, Color = color, Size = size, Duration = popupDuration });
            if (popups.Count > 6) popups.RemoveAt(0);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                popups[i].Age += dt;
                if (popups[i].Age >= popups[i].Duration) popups.RemoveAt(i);
            }
        }

        // ------------------------------------------------------------------ OnGUI
        private void OnGUI()
        {
            EnsureStyles();
            HandleKeys(Event.current);
            Matrix4x4 old = HudDrawing.BeginCanvas();

            GameState state = game != null ? game.State : GameState.Playing;
            switch (state)
            {
                case GameState.Menu: DrawMenu(); break;
                case GameState.Playing: DrawHud(); break;
                case GameState.Paused: DrawHud(); DrawPause(); break;
                case GameState.GameOver:
                case GameState.Finished: DrawEnd(state); break;
            }
            if (state == GameState.Playing || state == GameState.Paused) DrawPopups();

            GUI.matrix = old;
        }

        private void EnsureStyles()
        {
            if (label != null) return;
            title = HudDrawing.MakeLabel(92, accentColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            header = HudDrawing.MakeLabel(54, textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            label = HudDrawing.MakeLabel(28, textColor);
            small = HudDrawing.MakeLabel(21, new Color(1f, 1f, 1f, 0.75f));
            big = HudDrawing.MakeLabel(58, textColor, TextAnchor.UpperLeft, FontStyle.Bold);
            center = HudDrawing.MakeLabel(30, textColor, TextAnchor.MiddleCenter);
            centerSmall = HudDrawing.MakeLabel(22, new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleCenter);
            button = HudDrawing.MakeButton(32);
            selectedButton = HudDrawing.MakeSelectedButton(28);
            popupStyle = HudDrawing.MakeLabel(52, accentColor, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void HandleKeys(Event e)
        {
            if (e == null || e.type != EventType.KeyDown || game == null) return;
            KeyCode k = e.keyCode;
            switch (game.State)
            {
                case GameState.Menu:
                    if (k == KeyCode.Return || k == KeyCode.KeypadEnter) { game.StartGame(); e.Use(); }
                    break;
                case GameState.Playing:
                    if (k == KeyCode.Escape || k == KeyCode.P) { game.TogglePause(); e.Use(); }
                    break;
                case GameState.Paused:
                    if (k == KeyCode.Escape || k == KeyCode.P) { game.TogglePause(); e.Use(); }
                    else if (k == KeyCode.R) { game.Restart(); e.Use(); }
                    else if (k == KeyCode.M) { game.ReturnToMenu(); e.Use(); }
                    break;
                case GameState.GameOver:
                case GameState.Finished:
                    if (k == KeyCode.Return || k == KeyCode.KeypadEnter || k == KeyCode.R) { game.Restart(); e.Use(); }
                    else if (k == KeyCode.M) { game.ReturnToMenu(); e.Use(); }
                    break;
            }
        }

        // ------------------------------------------------------------------ screens
        private void DrawHud()
        {
            float vw = HudDrawing.VirtualWidth;

            // Left: score panel
            HudDrawing.Panel(new Rect(20, 20, 380, 262), panelColor);
            HudDrawing.Label(new Rect(42, 28, 300, 30), "SCORE", small);
            HudDrawing.Label(new Rect(40, 48, 340, 70), score != null ? score.Score.ToString("N0") : "0", big);
            HudDrawing.Label(new Rect(42, 124, 340, 36), $"Coins      <b>{(score != null ? score.Coins : 0)}</b>", label);
            HudDrawing.Label(new Rect(42, 160, 340, 36), $"Distance  <b>{(player != null ? player.DistanceTravelled : 0f):0} m</b>", label);
            HudDrawing.Label(new Rect(42, 196, 340, 36), $"Speed      <b>{(player != null ? player.Speed * 3.6f : 0f):0} km/h</b>", label);
            HudDrawing.Label(new Rect(42, 234, 120, 36), "Lives", label);
            int lives = game != null ? game.Lives : 0;
            int maxLives = game != null ? game.MaxLives : 3;
            for (int i = 0; i < maxLives; i++)
                HudDrawing.Panel(new Rect(150 + i * 36, 238, 28, 28), i < lives ? dangerColor : new Color(1f, 1f, 1f, 0.2f));

            // Centre top: multiplier
            if (player != null && !player.IsGrounded)
            {
                int mult = player.CurrentMultiplier;
                string m = player.IsFlipping ? "FLIPPING..." : (mult > 1 ? $"x{mult}" : "AIR");
                Color c = mult > 1 ? accentColor : textColor;
                HudDrawing.Panel(new Rect(vw * 0.5f - 160, 20, 320, 110), panelColor);
                var style = new GUIStyle(header) { fontSize = mult > 1 ? 64 : 44 };
                style.normal.textColor = c;
                HudDrawing.Label(new Rect(vw * 0.5f - 160, 20, 320, 76), m, style);
                HudDrawing.Label(new Rect(vw * 0.5f - 160, 88, 320, 34), $"multiplier x{mult}   air coins +{player.PendingAirPoints}", centerSmall);
            }

            // Right: difficulty + course progress
            float rx = vw - 320;
            HudDrawing.Panel(new Rect(rx, 20, 300, slope != null && slope.FixedCourseActive ? 130 : 80), panelColor);
            float diff = difficulty != null ? difficulty.Current : 0f;
            HudDrawing.Label(new Rect(rx + 18, 26, 280, 28), $"DIFFICULTY  {Mathf.RoundToInt(diff * 100)}%", small);
            HudDrawing.Bar(new Rect(rx + 18, 58, 264, 12), diff, Color.Lerp(new Color(0.3f, 0.9f, 0.4f), dangerColor, diff));
            if (slope != null && slope.FixedCourseActive && player != null)
            {
                float p = player.DistanceTravelled / Mathf.Max(1f, slope.CourseLength);
                HudDrawing.Label(new Rect(rx + 18, 80, 280, 28), $"COURSE  {player.DistanceTravelled:0} / {slope.CourseLength:0} m", small);
                HudDrawing.Bar(new Rect(rx + 18, 112, 264, 12), p, accentColor);
            }

            HudDrawing.Label(new Rect(0, HudDrawing.ReferenceHeight - 44, vw, 30), "Esc  pause     F1  input debug", centerSmall);
        }

        private void DrawMenu()
        {
            float vw = HudDrawing.VirtualWidth;
            float w = 860, h = 820;
            // Left side of the screen, so the orbiting camera shows the skier on the right.
            var r = new Rect(Mathf.Min(90f, vw * 0.04f), (HudDrawing.ReferenceHeight - h) * 0.5f, w, h);
            HudDrawing.Panel(r, panelColor);

            HudDrawing.Label(new Rect(r.x, r.y + 30, w, 110), gameTitle, title);
            HudDrawing.Label(new Rect(r.x, r.y + 130, w, 40), "An endless downhill ski run", center);

            float y = r.y + 200, x = r.x + 90;
            string[,] controls =
            {
                { "Lean left / right   (A / D)", "Steer" },
                { "Lean forward / back   (W / S)", "Speed up / slow down" },
                { "Squeeze forearm   (Space)", "Jump" },
                { "Curl biceps in the air   (F)", "Backflip - land it for x2 coins!" },
                { "F4  /  F3", "Calibrate EMG / centre" },
                { "Esc  ·  F2", "Pause  ·  sensor panel" },
            };
            for (int i = 0; i < controls.GetLength(0); i++)
            {
                HudDrawing.Label(new Rect(x - 40, y, 420, 36), $"<b>{controls[i, 0]}</b>", label);
                HudDrawing.Label(new Rect(x + 390, y, 420, 36), controls[i, 1], label);
                y += 40;
            }

            y += 16;
            DrawLegend(x, y, new Color(0.88f, 0.12f, 0.1f), "Red = avoid");
            DrawLegend(x + 180, y, new Color(1f, 0.6f, 0.05f), "Orange = jump");
            DrawLegend(x + 380, y, new Color(0.15f, 0.55f, 1f), "Blue = ramp");
            DrawLegend(x + 560, y, new Color(1f, 0.78f, 0.15f), "Gold = coin");

            y += 60;
            if (slope != null)
            {
                // Two-option mode selector; the active mode is gold. Text always fits its button.
                HudDrawing.Label(new Rect(x, y + 14, 120, 36), "<b>MODE</b>", label);
                float bw = (w - x + r.x - 90f - 150f - 16f) * 0.5f;
                var endlessRect = new Rect(x + 150f, y, bw, 60);
                var courseRect = new Rect(x + 150f + bw + 16f, y, bw, 60);
                bool course = slope.FixedCourseActive;
                if (HudDrawing.FitButton(endlessRect, "ENDLESS", course ? button : selectedButton)) slope.SetCourseMode(false);
                if (HudDrawing.FitButton(courseRect, $"COURSE  {slope.CourseLength:0} m", course ? selectedButton : button)) slope.SetCourseMode(true);
            }
            y += 84;
            if (HudDrawing.FitButton(new Rect(r.x + 160, y, w - 320, 84), "START   (Enter)", button) && game != null) game.StartGame();
            y += 104;
            HudDrawing.Label(new Rect(r.x, y, w, 36), $"Best score: {(score != null ? score.BestScore : 0)}", centerSmall);
        }

        private void DrawLegend(float x, float y, Color c, string text)
        {
            HudDrawing.Fill(new Rect(x, y + 6, 22, 22), c);
            HudDrawing.Label(new Rect(x + 30, y, 200, 34), text, small);
        }

        private void DrawPause()
        {
            float vw = HudDrawing.VirtualWidth;
            HudDrawing.Fill(new Rect(0, 0, vw, HudDrawing.ReferenceHeight), new Color(0f, 0f, 0f, 0.35f));
            float w = 520, h = 440;
            var r = new Rect((vw - w) * 0.5f, (HudDrawing.ReferenceHeight - h) * 0.5f, w, h);
            HudDrawing.Panel(r, panelColor);
            HudDrawing.Label(new Rect(r.x, r.y + 20, w, 80), "PAUSED", header);
            if (GUI.Button(new Rect(r.x + 80, r.y + 130, w - 160, 70), "Resume  (Esc)", button)) game.TogglePause();
            if (GUI.Button(new Rect(r.x + 80, r.y + 220, w - 160, 70), "Restart  (R)", button)) game.Restart();
            if (GUI.Button(new Rect(r.x + 80, r.y + 310, w - 160, 70), "Main menu  (M)", button)) game.ReturnToMenu();
        }

        private void DrawEnd(GameState state)
        {
            float vw = HudDrawing.VirtualWidth;
            float w = 680, h = 680;
            var r = new Rect((vw - w) * 0.5f, (HudDrawing.ReferenceHeight - h) * 0.5f, w, h);
            HudDrawing.Panel(r, panelColor);

            bool finished = state == GameState.Finished;
            var t = new GUIStyle(title) { fontSize = 80 };
            t.normal.textColor = finished ? accentColor : dangerColor;
            HudDrawing.Label(new Rect(r.x, r.y + 24, w, 100), finished ? "FINISH!" : "GAME OVER", t);
            HudDrawing.Label(new Rect(r.x, r.y + 120, w, 40), game != null ? game.EndReason : "", center);

            float y = r.y + 190;
            void Row(string a, string b)
            {
                HudDrawing.Label(new Rect(r.x + 150, y, 250, 40), a, label);
                HudDrawing.Label(new Rect(r.x + 400, y, 200, 40), $"<b>{b}</b>", label);
                y += 44;
            }
            Row("Score", score != null ? score.Score.ToString("N0") : "0");
            Row("Coins", score != null ? score.Coins.ToString() : "0");
            Row("Distance", player != null ? $"{player.DistanceTravelled:0} m" : "0 m");
            Row("Backflips", score != null ? score.Backflips.ToString() : "0");
            Row("Best", score != null ? score.BestScore.ToString("N0") + (score.IsNewBest ? "  NEW!" : "") : "0");

            if (GUI.Button(new Rect(r.x + 120, r.y + h - 190, w - 240, 72), "Restart  (Enter)", button)) game.Restart();
            if (GUI.Button(new Rect(r.x + 120, r.y + h - 100, w - 240, 64), "Main menu  (M)", button)) game.ReturnToMenu();
        }

        private void DrawPopups()
        {
            float vw = HudDrawing.VirtualWidth;
            float baseY = 300f;
            for (int i = 0; i < popups.Count; i++)
            {
                Popup p = popups[i];
                float t = p.Age / p.Duration;
                float alpha = t < 0.75f ? 1f : 1f - (t - 0.75f) / 0.25f;
                float pop = t < 0.12f ? Mathf.Lerp(1.35f, 1f, t / 0.12f) : 1f;
                popupStyle.fontSize = Mathf.RoundToInt(p.Size * pop);
                popupStyle.normal.textColor = new Color(p.Color.r, p.Color.g, p.Color.b, alpha);
                float y = baseY + i * 70f - t * 40f;
                HudDrawing.Label(new Rect(0, y, vw, 90), p.Text, popupStyle);
            }
        }
    }
}
