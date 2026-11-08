namespace SkiGame
{
    /// <summary>
    /// Contract between gameplay (PlayerController) and the character's looks.
    /// The controller only ever talks to this interface, so the primitive skier can be
    /// replaced by a rigged / animated character without touching gameplay code.
    ///
    /// Put a MonoBehaviour that implements this on a child of the Player object.
    /// The controller moves and orients the Player root; the visual only animates locally.
    /// </summary>
    public interface ISkierVisual
    {
        /// <summary>Lean into a turn: -1 = full left, +1 = full right. Called every frame.</summary>
        void SetLean(float lean);

        /// <summary>Tuck / racing crouch amount 0..1. Called every frame.</summary>
        void SetTuck(float tuck);

        /// <summary>Normalised speed 0..1 (drives snow spray etc.). Called every frame.</summary>
        void SetSpeed01(float speed01);

        /// <summary>Called when the skier touches down or leaves the ground.</summary>
        void SetOnGround(bool onGround);

        /// <summary>
        /// Backflip rotation progress, 0..1 = 0..360°. 0 = not flipping.
        /// Called every frame. The visual MUST follow this exactly so what you see matches
        /// the landing check.
        /// </summary>
        void SetFlipProgress(float progress01);

        /// <summary>True while the skier is invulnerable after a crash (e.g. blink).</summary>
        void SetInvulnerable(bool invulnerable);

        /// <summary>A jump just started.</summary>
        void PlayJump();

        /// <summary>A backflip just started; it will take 'duration' seconds.</summary>
        void PlayBackflip(float duration);

        /// <summary>Landed. afterTrick = true for a clean backflip landing.</summary>
        void PlayLand(bool afterTrick);

        /// <summary>Crashed; stumble for 'duration' seconds.</summary>
        void PlayCrash(float duration);
    }
}
