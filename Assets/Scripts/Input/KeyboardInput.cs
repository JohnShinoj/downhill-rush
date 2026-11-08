using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// Keyboard implementation of IGameInput (old Input Manager).
    ///   A/D or Left/Right  = steer
    ///   W/S or Up/Down     = speed up / slow down
    ///   Space              = jump
    ///   F                  = backflip (while airborne)
    /// Smoothing, dead zones and trigger cooldowns are handled by GameInputBase.
    /// </summary>
    [AddComponentMenu("Ski Game/Input/Keyboard Input")]
    public class KeyboardInput : GameInputBase
    {
        [Header("Key bindings")]
        [SerializeField] private KeyCode[] steerLeftKeys = { KeyCode.A, KeyCode.LeftArrow };
        [SerializeField] private KeyCode[] steerRightKeys = { KeyCode.D, KeyCode.RightArrow };
        [SerializeField] private KeyCode[] speedUpKeys = { KeyCode.W, KeyCode.UpArrow };
        [SerializeField] private KeyCode[] slowDownKeys = { KeyCode.S, KeyCode.DownArrow };
        [SerializeField] private KeyCode[] jumpKeys = { KeyCode.Space };
        [SerializeField] private KeyCode[] backflipKeys = { KeyCode.F };

        private bool inputManagerUnavailable;

        protected override float ReadSteer() => Axis(steerLeftKeys, steerRightKeys);
        protected override float ReadSpeedControl() => Axis(slowDownKeys, speedUpKeys);
        protected override bool ReadJumpRequest() => AnyHeld(jumpKeys);
        protected override bool ReadBackflipRequest() => AnyHeld(backflipKeys);

        private float Axis(KeyCode[] negative, KeyCode[] positive)
        {
            return (AnyHeld(positive) ? 1f : 0f) - (AnyHeld(negative) ? 1f : 0f);
        }

        private bool AnyHeld(KeyCode[] keys)
        {
            if (inputManagerUnavailable || keys == null) return false;
            try
            {
                for (int i = 0; i < keys.Length; i++)
                    if (Input.GetKey(keys[i])) return true;
            }
            catch (System.InvalidOperationException)
            {
                // Happens when Project Settings > Player > Active Input Handling = "Input System Package (New)".
                inputManagerUnavailable = true;
                Debug.LogError("[SkiGame] KeyboardInput needs the old Input Manager. Set Edit > Project Settings > Player > " +
                               "Active Input Handling to 'Input Manager (Old)' or 'Both' and restart Unity " +
                               "(or use Tools > Ski Game > Fix Input Handling).");
            }
            return false;
        }
    }
}
