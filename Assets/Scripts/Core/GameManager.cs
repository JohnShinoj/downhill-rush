using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkiGame
{
    /// <summary>
    /// Game flow: Main menu -> Playing <-> Paused -> Game over / Finished, plus Restart.
    /// Owns lives (a crash costs one life). Enables/disables player control per state.
    /// UI calls the public methods (StartGame, TogglePause, Restart, ReturnToMenu).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class GameManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameRulesSettings rules;
        [SerializeField] private PlayerController player;
        [SerializeField] private SlopeGenerator slope;
        [SerializeField] private ScoreManager score;

        [Header("Flow")]
        [Tooltip("Show the main menu when the scene starts. If off, the run starts immediately.")]
        [SerializeField] private bool showMainMenu = true;

        public static GameManager Instance { get; private set; }
        public GameState State { get; private set; } = GameState.Menu;
        public int Lives { get; private set; }
        public int MaxLives => rules.startLives;
        public string EndReason { get; private set; } = "";
        public GameRulesSettings Rules => rules;

        // Survives a scene reload so "Restart" skips the menu.
        private static bool skipMenuOnNextLoad;

        private void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            if (rules == null) rules = ScriptableObject.CreateInstance<GameRulesSettings>();
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (slope == null) slope = FindAnyObjectByType<SlopeGenerator>();
            if (score == null) score = FindAnyObjectByType<ScoreManager>();
            Lives = rules.startLives;
        }

        private void OnEnable() => GameEvents.Crashed += HandleCrash;

        private void OnDisable()
        {
            GameEvents.Crashed -= HandleCrash;
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            bool skipMenu = skipMenuOnNextLoad || !showMainMenu;
            skipMenuOnNextLoad = false;
            SetState(skipMenu ? GameState.Playing : GameState.Menu);
        }

        private void Update()
        {
            // Fixed-length course: reaching the finish line ends the run.
            if (State == GameState.Playing && slope != null && player != null &&
                slope.FixedCourseActive && player.DistanceTravelled >= slope.CourseLength)
            {
                EndRun(GameState.Finished, "Course complete!");
            }
        }

        private void HandleCrash(string reason)
        {
            if (State != GameState.Playing) return;
            Lives = Mathf.Max(0, Lives - 1);
            if (Lives <= 0) EndRun(GameState.GameOver, reason);
        }

        public void StartGame()
        {
            if (State == GameState.Menu) SetState(GameState.Playing);
        }

        public void TogglePause()
        {
            if (State == GameState.Playing) SetState(GameState.Paused);
            else if (State == GameState.Paused) SetState(GameState.Playing);
        }

        public void Restart()
        {
            skipMenuOnNextLoad = true;
            ReloadScene();
        }

        public void ReturnToMenu()
        {
            skipMenuOnNextLoad = false;
            ReloadScene();
        }

        private void EndRun(GameState endState, string reason)
        {
            EndReason = reason;
            if (score != null) score.CommitBestScore();
            SetState(endState);
        }

        private void SetState(GameState newState)
        {
            State = newState;
            Time.timeScale = newState == GameState.Paused ? 0f : 1f;
            if (player != null) player.ControlsEnabled = newState == GameState.Playing;
            GameEvents.RaiseStateChanged(newState);
        }

        private static void ReloadScene()
        {
            Time.timeScale = 1f;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0)
            {
                SceneManager.LoadScene(scene.buildIndex);
                return;
            }
#if UNITY_EDITOR
            // Scene not in the build list: still works inside the editor.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.name);
#endif
        }
    }
}
