using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// Procedurally synthesised sound (no audio files):
    ///  - wind loop whose volume / brightness rise with speed
    ///  - ski carve hiss while on the ground
    ///  - one-shots for coin, jump, backflip start, clean trick, landing, crash
    /// Everything reacts to GameEvents, so it never touches gameplay code.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float effectsVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float windVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] private float carveVolume = 0.25f;

        private const int Rate = 44100;
        private AudioSource sfx, wind, carve;
        private AudioLowPassFilter windFilter;
        private AudioClip coin, jump, flipStart, trick, land, crash;
        private float coinPitchStep;
        private float lastCoinTime;

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;

            var windGo = new GameObject("WindAudio");
            windGo.transform.SetParent(transform, false);
            wind = windGo.AddComponent<AudioSource>();
            wind.clip = Noise("Wind", 4f, brown: true);
            wind.loop = true;
            wind.volume = 0f;
            wind.spatialBlend = 0f;
            windFilter = windGo.AddComponent<AudioLowPassFilter>();
            windFilter.cutoffFrequency = 800f;
            wind.Play();

            var carveGo = new GameObject("CarveAudio");
            carveGo.transform.SetParent(transform, false);
            carve = carveGo.AddComponent<AudioSource>();
            carve.clip = Noise("Carve", 3f, brown: false);
            carve.loop = true;
            carve.volume = 0f;
            carve.spatialBlend = 0f;
            var carveFilter = carveGo.AddComponent<AudioHighPassFilter>();
            carveFilter.cutoffFrequency = 2500f;
            carve.Play();

            coin = Tone("Coin", 0.22f, new[] { 1318.5f, 1975.5f }, 0.07f, 18f);
            jump = Whoosh("Jump", 0.25f, 400f, 1800f);
            flipStart = Whoosh("FlipStart", 0.35f, 900f, 3500f);
            trick = Tone("Trick", 0.6f, new[] { 659.3f, 880f, 1318.5f, 1760f }, 0.09f, 6f);
            land = Thud("Land", 0.22f, 85f, 0.6f);
            crash = Thud("Crash", 0.7f, 55f, 1f);
        }

        private void OnEnable()
        {
            GameEvents.CoinCollected += OnCoin;
            GameEvents.Jumped += OnJump;
            GameEvents.BackflipStarted += OnFlipStart;
            GameEvents.BackflipLanded += OnTrick;
            GameEvents.Landed += OnLand;
            GameEvents.Crashed += OnCrash;
        }

        private void OnDisable()
        {
            GameEvents.CoinCollected -= OnCoin;
            GameEvents.Jumped -= OnJump;
            GameEvents.BackflipStarted -= OnFlipStart;
            GameEvents.BackflipLanded -= OnTrick;
            GameEvents.Landed -= OnLand;
            GameEvents.Crashed -= OnCrash;
        }

        private void Update()
        {
            bool playing = player != null && player.ControlsEnabled;
            float s = playing ? player.Speed01 : 0f;
            float dt = Time.unscaledDeltaTime;
            float paused = Time.timeScale <= 0f ? 0f : 1f;

            float windTarget = (0.12f + 0.88f * s) * windVolume * masterVolume * paused * (playing ? 1f : 0.35f);
            wind.volume = Mathf.MoveTowards(wind.volume, windTarget, dt * 0.8f);
            wind.pitch = 0.8f + 0.5f * s;
            windFilter.cutoffFrequency = Mathf.Lerp(500f, 4000f, s);

            float carveTarget = playing && player.IsGrounded ? (0.25f + 0.75f * s) * carveVolume * masterVolume * paused : 0f;
            carve.volume = Mathf.MoveTowards(carve.volume, carveTarget, dt * 3f);
            carve.pitch = 0.85f + 0.4f * s;
        }

        private void Play(AudioClip clip, float volume, float pitch = 1f)
        {
            if (clip == null) return;
            sfx.pitch = pitch;
            sfx.PlayOneShot(clip, volume * effectsVolume * masterVolume);
        }

        private void OnCoin(int value, bool airborne)
        {
            // Rising pitch for coins picked up in quick succession.
            coinPitchStep = Time.time - lastCoinTime < 0.6f ? Mathf.Min(coinPitchStep + 1f, 8f) : 0f;
            lastCoinTime = Time.time;
            Play(coin, 0.55f, Mathf.Pow(1.0595f, coinPitchStep));
        }

        private void OnJump() => Play(jump, 0.5f, Random.Range(0.95f, 1.05f));
        private void OnFlipStart() => Play(flipStart, 0.6f);
        private void OnTrick(int flips, int multiplier, int points) => Play(trick, 0.7f);
        private void OnLand(bool clean) => Play(land, 0.7f, Random.Range(0.9f, 1.1f));
        private void OnCrash(string reason) => Play(crash, 1f);

        // ------------------------------------------------------------------ synthesis
        private static AudioClip Noise(string name, float seconds, bool brown)
        {
            int n = (int)(Rate * seconds);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float b = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                if (brown) { b = (b + 0.02f * white) / 1.02f; data[i] = b * 3.5f; }
                else data[i] = white * 0.5f;
            }
            // Cross-fade the ends so the loop is seamless.
            int fade = Rate / 4;
            for (int i = 0; i < fade; i++)
            {
                float t = i / (float)fade;
                data[i] = data[i] * t + data[n - fade + i] * (1f - t);
            }
            var trimmed = new float[n - fade];
            System.Array.Copy(data, trimmed, trimmed.Length);
            var clip = AudioClip.Create(name, trimmed.Length, 1, Rate, false);
            clip.SetData(trimmed, 0);
            return clip;
        }

        /// <summary>Sequence of bell-like sine notes.</summary>
        private static AudioClip Tone(string name, float seconds, float[] notes, float noteLength, float decay)
        {
            int n = (int)(Rate * seconds);
            var data = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = (int)(k * noteLength * Rate);
                for (int i = start; i < n; i++)
                {
                    float t = (i - start) / (float)Rate;
                    float env = Mathf.Exp(-t * decay) * Mathf.Clamp01(t * 400f);
                    float w = 2f * Mathf.PI * notes[k] * t;
                    data[i] += (Mathf.Sin(w) + 0.3f * Mathf.Sin(2f * w)) * env * 0.35f;
                }
            }
            return Make(name, data);
        }

        /// <summary>Filtered noise sweep (air rushing).</summary>
        private static AudioClip Whoosh(string name, float seconds, float fromHz, float toHz)
        {
            int n = (int)(Rate * seconds);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float cutoff = Mathf.Lerp(fromHz, toHz, t);
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
                y += a * ((float)(rng.NextDouble() * 2.0 - 1.0) - y);
                data[i] = y * Mathf.Sin(t * Mathf.PI) * 0.9f;
            }
            return Make(name, data);
        }

        /// <summary>Low thump + noise burst (landing / crash).</summary>
        private static AudioClip Thud(string name, float seconds, float hz, float noiseAmount)
        {
            int n = (int)(Rate * seconds);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = hz * (1f + 0.6f * Mathf.Exp(-t * 30f));
                float thump = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 14f);
                y += 0.15f * ((float)(rng.NextDouble() * 2.0 - 1.0) - y);
                float noise = y * Mathf.Exp(-t * (6f / seconds)) * noiseAmount * 1.6f;
                data[i] = (thump * 0.8f + noise) * Mathf.Clamp01(t * 300f);
            }
            return Make(name, data);
        }

        private static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
