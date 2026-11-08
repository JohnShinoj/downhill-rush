using UnityEngine;

namespace SkiGame
{
    /// <summary>High-level state of a run.</summary>
    public enum GameState { Menu, Playing, Paused, GameOver, Finished }

    /// <summary>
    /// Static event hub that keeps systems loosely coupled.
    /// The player raises gameplay events; scoring, lives, camera and UI listen.
    /// New systems (sound, achievements, analytics, a data logger for the EMG study...)
    /// can subscribe without touching the existing scripts.
    /// Subscribers must subscribe in OnEnable and unsubscribe in OnDisable.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>(coinValue, collectedWhileAirborne)</summary>
        public static event System.Action<int, bool> CoinCollected;
        /// <summary>World position of a coin that was just picked up (for sparkle effects / sound).</summary>
        public static event System.Action<Vector3> CoinPickedUpAt;
        /// <summary>Points that should be added to the score right now.</summary>
        public static event System.Action<int> PointsAwarded;
        /// <summary>(flipsCompleted, multiplierUsed, totalPointsAwardedForThatAirtime)</summary>
        public static event System.Action<int, int, int> BackflipLanded;
        /// <summary>Player crashed (reason text). The GameManager removes a life.</summary>
        public static event System.Action<string> Crashed;
        public static event System.Action Jumped;
        public static event System.Action BackflipStarted;
        /// <summary>Player landed. Argument = true when it was a clean trick landing.</summary>
        public static event System.Action<bool> Landed;
        public static event System.Action<GameState> StateChanged;
        /// <summary>Generic on-screen popup request (text, colour).</summary>
        public static event System.Action<string, Color> PopupRequested;

        public static void RaiseCoinCollected(int value, bool airborne) => CoinCollected?.Invoke(value, airborne);
        public static void RaiseCoinPickedUpAt(Vector3 position) => CoinPickedUpAt?.Invoke(position);
        public static void RaisePointsAwarded(int points) { if (points != 0) PointsAwarded?.Invoke(points); }
        public static void RaiseBackflipLanded(int flips, int multiplier, int points) => BackflipLanded?.Invoke(flips, multiplier, points);
        public static void RaiseCrashed(string reason) => Crashed?.Invoke(reason);
        public static void RaiseJumped() => Jumped?.Invoke();
        public static void RaiseBackflipStarted() => BackflipStarted?.Invoke();
        public static void RaiseLanded(bool cleanTrick) => Landed?.Invoke(cleanTrick);
        public static void RaiseStateChanged(GameState state) => StateChanged?.Invoke(state);
        public static void RaisePopup(string text, Color color) => PopupRequested?.Invoke(text, color);
    }

    /// <summary>
    /// Layer conventions. Uses only Unity's BUILT-IN layers, so no Tags/Layers setup is needed:
    ///  - Default (0)        = ground: slope chunks and ramps (hit by the ground raycast)
    ///  - Ignore Raycast (2) = interactables: obstacles and coins (found with overlap queries)
    /// </summary>
    public static class GameLayers
    {
        public const int Ground = 0;
        public const int Interactable = 2;
        public const int GroundMask = 1 << Ground;
        public const int InteractableMask = 1 << Interactable;
    }
}
