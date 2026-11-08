using UnityEngine;

namespace SkiGame
{
    /// <summary>Scoring and lives rules. Create via Assets > Create > Ski Game > Game Rules.</summary>
    [CreateAssetMenu(menuName = "Ski Game/Game Rules", fileName = "GameRulesSettings")]
    public class GameRulesSettings : ScriptableObject
    {
        [Header("Lives")]
        [Min(1)] public int startLives = 3;

        [Header("Coins")]
        [Tooltip("Points per coin (base value).")]
        [Min(0)] public int coinValue = 1;

        [Header("Backflip")]
        [Tooltip("Coins collected during an airtime with a completed backflip are multiplied by this.")]
        [Min(1)] public int backflipMultiplier = 2;
        [Tooltip("Bonus points per completed backflip on a clean landing (also multiplied).")]
        [Min(0)] public int backflipLandingBonus = 5;

        [Header("Distance")]
        [Tooltip("Optional points per metre travelled. 0 = score only from coins and tricks.")]
        [Min(0f)] public float pointsPerMeter = 0f;
    }
}
