using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// Shared IMGUI drawing helpers and styles for the HUD and the debug HUD.
    /// IMGUI was chosen so the UI needs zero scene setup, zero packages and no fonts:
    /// it works the moment the scene runs. Everything is drawn on a virtual 1080p canvas.
    /// </summary>
    public static class HudDrawing
    {
        public const float ReferenceHeight = 1080f;

        private static Texture2D white;
        public static Texture2D White
        {
            get
            {
                if (white == null)
                {
                    white = new Texture2D(1, 1) { name = "HudWhite" };
                    white.SetPixel(0, 0, Color.white);
                    white.Apply();
                }
                return white;
            }
        }

        /// <summary>Scale factor from virtual 1080p to the real screen.</summary>
        public static float Scale => Screen.height / ReferenceHeight;
        public static float VirtualWidth => Screen.width / Mathf.Max(0.01f, Scale);

        /// <summary>Applies the 1080p virtual canvas. Returns the previous matrix.</summary>
        public static Matrix4x4 BeginCanvas()
        {
            Matrix4x4 old = GUI.matrix;
            float s = Scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            return old;
        }

        public static void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, White);
            GUI.color = old;
        }

        /// <summary>Horizontal bar filling from the left, value 0..1.</summary>
        public static void Bar(Rect r, float value01, Color fill)
        {
            Fill(r, new Color(1f, 1f, 1f, 0.15f));
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(value01), r.height), fill);
        }

        /// <summary>Bar filling from the centre, value -1..1.</summary>
        public static void CenterBar(Rect r, float value, Color fill)
        {
            Fill(r, new Color(1f, 1f, 1f, 0.15f));
            float half = r.width * 0.5f;
            float w = half * Mathf.Clamp(value, -1f, 1f);
            if (w >= 0f) Fill(new Rect(r.x + half, r.y, w, r.height), fill);
            else Fill(new Rect(r.x + half + w, r.y, -w, r.height), fill);
            Fill(new Rect(r.x + half - 1f, r.y - 2f, 2f, r.height + 4f), Color.white);
        }

        /// <summary>Label with a soft drop shadow (readable over bright snow).</summary>
        public static void Label(Rect r, string text, GUIStyle style)
        {
            Color c = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.55f * c.a);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);
            style.normal.textColor = c;
            GUI.Label(r, text, style);
        }

        // ------------------------------------------------------------------ fonts
        private static Font uiFont;
        private static bool fontResolved, noOsFont;

        /// <summary>
        /// Modern UI font taken from the operating system (no font files needed):
        /// Bahnschrift (Windows 10/11) -> Segoe UI -> Arial / Helvetica. Null = Unity default.
        /// </summary>
        public static Font UIFont
        {
            get
            {
                // Re-resolve if the cached font was destroyed (e.g. after leaving Play mode in the editor).
                if (fontResolved && (uiFont != null || noOsFont)) return uiFont;
                fontResolved = true;
                noOsFont = true;
                string[] preferred = { "Bahnschrift", "Segoe UI", "Helvetica Neue", "Arial" };
                string[] installed = Font.GetOSInstalledFontNames();
                foreach (string name in preferred)
                {
                    if (System.Array.IndexOf(installed, name) < 0) continue;
                    uiFont = Font.CreateDynamicFontFromOSFont(name, 32);
                    if (uiFont != null) { noOsFont = false; break; }
                }
                return uiFont;
            }
        }

        /// <summary>Largest font size (<= maxSize) at which the text fits the width.</summary>
        public static int FitFontSize(GUIStyle style, string text, float width, int maxSize, int minSize = 14)
        {
            int old = style.fontSize;
            int size = maxSize;
            for (; size > minSize; size -= 2)
            {
                style.fontSize = size;
                if (style.CalcSize(new GUIContent(text)).x <= width) break;
            }
            style.fontSize = old;
            return size;
        }

        public static GUIStyle MakeLabel(int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft, FontStyle fontStyle = FontStyle.Normal)
        {
            var s = new GUIStyle(GUI.skin.label)
            {
                font = UIFont,
                fontSize = size,
                alignment = anchor,
                fontStyle = fontStyle,
                wordWrap = false,
                richText = true
            };
            s.normal.textColor = color;
            return s;
        }

        // ------------------------------------------------------------------ rounded shapes
        private static Texture2D roundedWhite;
        private static GUIStyle roundedStyle;
        private const int RoundedSize = 64, RoundedRadius = 18;

        /// <summary>Rounded-rectangle texture (9-sliced), filled with the given colour.</summary>
        public static Texture2D MakeRounded(Color fill, Color border, float borderWidth = 0f)
        {
            var tex = new Texture2D(RoundedSize, RoundedSize, TextureFormat.RGBA32, false)
                { name = "HudRounded", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[RoundedSize * RoundedSize];
            for (int y = 0; y < RoundedSize; y++)
                for (int x = 0; x < RoundedSize; x++)
                {
                    // Signed distance to a rounded rectangle.
                    float cx = Mathf.Clamp(x + 0.5f, RoundedRadius, RoundedSize - RoundedRadius);
                    float cy = Mathf.Clamp(y + 0.5f, RoundedRadius, RoundedSize - RoundedRadius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) - RoundedRadius;
                    float a = Mathf.Clamp01(0.5f - d);
                    Color c = fill;
                    if (borderWidth > 0f) c = Color.Lerp(fill, border, Mathf.Clamp01(d + borderWidth + 0.5f));
                    c.a *= a;
                    px[y * RoundedSize + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Rounded translucent panel.</summary>
        public static void Panel(Rect r, Color color)
        {
            if (roundedWhite == null) roundedWhite = MakeRounded(Color.white, Color.white);
            if (roundedStyle == null)
            {
                roundedStyle = new GUIStyle { border = new RectOffset(RoundedRadius, RoundedRadius, RoundedRadius, RoundedRadius) };
                roundedStyle.normal.background = roundedWhite;
            }
            if (Event.current.type != EventType.Repaint) return;
            Color old = GUI.color;
            GUI.color = color;
            roundedStyle.Draw(r, GUIContent.none, false, false, false, false);
            GUI.color = old;
        }

        /// <summary>Button style that looks permanently "selected" (gold) - for toggles.</summary>
        public static GUIStyle MakeSelectedButton(int size)
        {
            GUIStyle s = MakeButton(size);
            Texture2D gold = MakeRounded(new Color(1f, 0.76f, 0.2f, 1f), new Color(1f, 0.95f, 0.7f, 1f), 2f);
            s.normal.background = gold;
            s.normal.textColor = new Color(0.08f, 0.1f, 0.2f);
            s.hover.background = gold;
            s.focused.background = gold;
            s.focused.textColor = s.normal.textColor;
            return s;
        }

        /// <summary>Button whose text is shrunk to always fit inside it.</summary>
        public static bool FitButton(Rect r, string text, GUIStyle style)
        {
            int old = style.fontSize;
            style.fontSize = FitFontSize(style, text, r.width - 36f, old);
            bool clicked = GUI.Button(r, text, style);
            style.fontSize = old;
            return clicked;
        }

        /// <summary>Rounded button: dark navy, gold on hover.</summary>
        public static GUIStyle MakeButton(int size)
        {
            var s = new GUIStyle(GUI.skin.button)
            {
                font = UIFont,
                fontSize = size,
                fontStyle = FontStyle.Bold,
                border = new RectOffset(RoundedRadius, RoundedRadius, RoundedRadius, RoundedRadius),
                alignment = TextAnchor.MiddleCenter
            };
            Texture2D normal = MakeRounded(new Color(0.1f, 0.18f, 0.34f, 0.95f), new Color(1f, 1f, 1f, 0.35f), 2f);
            Texture2D hover = MakeRounded(new Color(1f, 0.76f, 0.2f, 1f), new Color(1f, 0.95f, 0.7f, 1f), 2f);
            Texture2D active = MakeRounded(new Color(0.9f, 0.6f, 0.1f, 1f), new Color(1f, 1f, 1f, 1f), 2f);
            s.normal.background = normal;
            s.normal.textColor = Color.white;
            s.hover.background = hover;
            s.hover.textColor = new Color(0.08f, 0.1f, 0.2f);
            s.active.background = active;
            s.active.textColor = new Color(0.08f, 0.1f, 0.2f);
            s.focused.background = normal;
            s.focused.textColor = Color.white;
            return s;
        }
    }
}
