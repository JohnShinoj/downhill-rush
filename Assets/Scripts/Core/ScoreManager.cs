using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// Keeps score, coin count, backflip count and best score.
    /// Listens to GameEvents only - it does not know how points were earned.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        private const string BestScoreKey = "SkiGame.BestScore";

        [SerializeField] private GameRulesSettings rules;
        [SerializeField] private PlayerController player;

        public int Score { get; private set; }
        public int Coins { get; private set; }
        public int Backflips { get; private set; }
        public int BestScore { get; private set; }
        public bool IsNewBest { get; private set; }

        private float distancePointCarry;
        private float lastDistance;

        private void Awake()
        {
            if (rules == null) rules = ScriptableObject.CreateInstance<GameRulesSettings>();
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            BestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
        }

        private void OnEnable()
        {
            GameEvents.PointsAwarded += HandlePoints;
            GameEvents.CoinCollected += HandleCoin;
            GameEvents.BackflipLanded += HandleBackflip;
        }

        private void OnDisable()
        {
            GameEvents.PointsAwarded -= HandlePoints;
            GameEvents.CoinCollected -= HandleCoin;
            GameEvents.BackflipLanded -= HandleBackflip;
        }

        private void HandlePoints(int points) => Score += points;
        private void HandleCoin(int value, bool airborne) => Coins++;
        private void HandleBackflip(int flips, int multiplier, int points) => Backflips += flips;

        private void Update()
        {
            if (player == null) return;
            float d = player.DistanceTravelled;
            bool playing = GameManager.Instance == null || GameManager.Instance.State == GameState.Playing;
            if (playing && rules.pointsPerMeter > 0f)
            {
                distancePointCarry += Mathf.Max(0f, d - lastDistance) * rules.pointsPerMeter;
                int whole = Mathf.FloorToInt(distancePointCarry);
                if (whole > 0) { Score += whole; distancePointCarry -= whole; }
            }
            lastDistance = d;
        }

        /// <summary>Stores the best score (called at game over / finish).</summary>
        public void CommitBestScore()
        {
            if (Score <= BestScore) return;
            BestScore = Score;
            IsNewBest = true;
            PlayerPrefs.SetInt(BestScoreKey, BestScore);
            PlayerPrefs.Save();
        }
    }
}
