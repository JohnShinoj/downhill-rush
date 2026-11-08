using System.Collections.Generic;
using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// A skier built from Unity primitives, animated purely in code:
    /// crouched skiing pose, lean into turns, tuck at speed, extended legs in the air,
    /// tucked "ball" during a backflip, compression on landing, stumble on crash.
    ///
    /// Hierarchy (built at runtime under a "Rig" child):
    ///   Rig
    ///    ├─ FlipPivot (at hip height, rotates for the backflip)
    ///    │   └─ Body  (origin back at the feet, rolls for leaning)
    ///    │       ├─ FootL / FootR  (boot + binding + ski with upturned tip)
    ///    │       ├─ thighs, shins, pelvis, torso (jacket, stripe, bib, backpack)
    ///    │       ├─ head (face, helmet, goggles)
    ///    │       └─ arms, gloves, poles with baskets
    ///    └─ SnowSpray (particles at the ski tails)
    ///
    /// Limbs are placed each frame with a tiny two-bone IK, so feet stay on the skis.
    /// Implements ISkierVisual - replace this component with any other ISkierVisual.
    /// </summary>
    [DisallowMultipleComponent]
    public class PrimitiveSkierVisual : MonoBehaviour, ISkierVisual
    {
        [Header("Colours")]
        [SerializeField] private Color jacketColor = new Color(0.86f, 0.13f, 0.2f);
        [SerializeField] private Color jacketAccentColor = new Color(1f, 0.82f, 0.1f);
        [SerializeField] private Color pantsColor = new Color(0.12f, 0.14f, 0.24f);
        [SerializeField] private Color helmetColor = new Color(0.96f, 0.96f, 0.98f);
        [SerializeField] private Color gogglesFrameColor = new Color(0.08f, 0.08f, 0.1f);
        [SerializeField] private Color gogglesLensColor = new Color(1f, 0.55f, 0.1f);
        [SerializeField] private Color skinColor = new Color(0.95f, 0.76f, 0.62f);
        [SerializeField] private Color gloveColor = new Color(0.1f, 0.1f, 0.12f);
        [SerializeField] private Color bootColor = new Color(0.2f, 0.45f, 0.88f);
        [SerializeField] private Color skiColor = new Color(0.1f, 0.78f, 0.92f);
        [SerializeField] private Color poleColor = new Color(0.78f, 0.78f, 0.82f);
        [SerializeField] private Color snowSprayColor = new Color(1f, 1f, 1f, 0.85f);

        [Header("Proportions (m)")]
        [SerializeField] private float thighLength = 0.44f;
        [SerializeField] private float shinLength = 0.44f;
        [SerializeField] private float upperArmLength = 0.30f;
        [SerializeField] private float forearmLength = 0.29f;
        [SerializeField] private float skiLength = 1.7f;
        [SerializeField] private float footSpacing = 0.14f;

        [Header("Pose")]
        [Tooltip("Normal skiing crouch (0 = upright, 1 = deep).")]
        [SerializeField, Range(0f, 1f)] private float baseCrouch = 0.35f;
        [SerializeField] private float maxLeanAngle = 24f;
        [Tooltip("How fast the pose follows its targets.")]
        [SerializeField] private float poseSharpness = 12f;
        [Tooltip("Height of the backflip rotation centre.")]
        [SerializeField] private float flipPivotHeight = 0.7f;
        [SerializeField] private float landCompressionTime = 0.28f;

        [Header("Snow spray")]
        [SerializeField] private bool enableSnowSpray = true;
        [SerializeField] private float maxSprayRate = 230f;

        [Header("Ski tracks & trick trails")]
        [SerializeField] private bool enableSkiTracks = true;
        [SerializeField] private Color skiTrackColor = new Color(0.42f, 0.52f, 0.74f, 0.38f);
        [SerializeField] private float skiTrackTime = 1.8f;
        [SerializeField] private bool enableTrickTrails = true;
        [SerializeField] private Color trickTrailColor = new Color(1f, 0.72f, 0.25f, 1f);

        // ---- targets from the controller ----
        private float leanTarget, tuckTarget, speed01, flipProgress;
        private bool onGround = true, invulnerable;

        // ---- smoothed animation state ----
        private float lean, tuck, ball, extend, landTimer, crashTimer, crashDuration = 1f, blinkClock;
        private bool renderersVisible = true;

        // ---- rig ----
        private Transform rig, flipPivot, body, footL, footR, pelvis, torso, head, handL, handR, basketL, basketR;
        private Limb thighL, thighR, shinL, shinR, upperArmL, upperArmR, foreArmL, foreArmR, poleL, poleR;
        private SkiSnowSpray spray;
        private TrailRenderer trackL, trackR, trickL, trickR;
        private readonly List<Renderer> bodyRenderers = new List<Renderer>();
        private bool built;

        private void Awake() => Build();

        /// <summary>Builds the primitive model (once).</summary>
        public void Build()
        {
            if (built) return;
            built = true;

            Transform old = transform.Find("Rig");
            if (old != null) PrimitiveBuilder.SafeDestroy(old.gameObject);

            rig = PrimitiveBuilder.CreateEmpty("Rig", transform, Vector3.zero);
            flipPivot = PrimitiveBuilder.CreateEmpty("FlipPivot", rig, new Vector3(0f, flipPivotHeight, 0f));
            body = PrimitiveBuilder.CreateEmpty("Body", flipPivot, new Vector3(0f, -flipPivotHeight, 0f));

            Material jacket = MaterialFactory.Lit(jacketColor, 0.3f);
            Material accent = MaterialFactory.Lit(jacketAccentColor, 0.3f);
            Material pants = MaterialFactory.Lit(pantsColor, 0.2f);
            Material glove = MaterialFactory.Lit(gloveColor, 0.2f);
            Material pole = MaterialFactory.Lit(poleColor, 0.7f, 0.8f);
            Material white = MaterialFactory.Lit(Color.white, 0.2f);

            footL = BuildFoot("FootL", -1f);
            footR = BuildFoot("FootR", 1f);

            thighL = Limb.Create(PrimitiveType.Capsule, "ThighL", body, pants, 0.15f);
            thighR = Limb.Create(PrimitiveType.Capsule, "ThighR", body, pants, 0.15f);
            shinL = Limb.Create(PrimitiveType.Capsule, "ShinL", body, pants, 0.13f);
            shinR = Limb.Create(PrimitiveType.Capsule, "ShinR", body, pants, 0.13f);

            pelvis = PrimitiveBuilder.Create(PrimitiveType.Cube, "Pelvis", body, Vector3.zero, new Vector3(0.32f, 0.17f, 0.21f), pants).transform;

            torso = PrimitiveBuilder.CreateEmpty("Torso", body, Vector3.zero);
            PrimitiveBuilder.Create(PrimitiveType.Cube, "Jacket", torso, Vector3.zero, new Vector3(0.36f, 0.52f, 0.23f), jacket);
            PrimitiveBuilder.Create(PrimitiveType.Cube, "Stripe", torso, new Vector3(0f, 0.06f, 0f), new Vector3(0.372f, 0.07f, 0.242f), accent);
            PrimitiveBuilder.Create(PrimitiveType.Cube, "Bib", torso, new Vector3(0f, -0.08f, 0.118f), new Vector3(0.2f, 0.17f, 0.01f), white);
            PrimitiveBuilder.Create(PrimitiveType.Cylinder, "Collar", torso, new Vector3(0f, 0.27f, 0f), new Vector3(0.17f, 0.03f, 0.17f), accent);
            PrimitiveBuilder.Create(PrimitiveType.Cube, "Backpack", torso, new Vector3(0f, 0.02f, -0.15f), new Vector3(0.26f, 0.32f, 0.1f), MaterialFactory.Lit(pantsColor * 1.4f, 0.2f));

            head = PrimitiveBuilder.CreateEmpty("Head", body, Vector3.zero);
            PrimitiveBuilder.Create(PrimitiveType.Sphere, "Face", head, Vector3.zero, new Vector3(0.2f, 0.22f, 0.2f), MaterialFactory.Lit(skinColor, 0.1f));
            PrimitiveBuilder.Create(PrimitiveType.Sphere, "Helmet", head, new Vector3(0f, 0.045f, -0.025f), new Vector3(0.26f, 0.24f, 0.28f), MaterialFactory.Lit(helmetColor, 0.7f));
            Material frame = MaterialFactory.Lit(gogglesFrameColor, 0.4f);
            PrimitiveBuilder.Create(PrimitiveType.Cube, "GoggleStrap", head, new Vector3(0f, 0.02f, -0.02f), new Vector3(0.265f, 0.035f, 0.25f), frame);
            PrimitiveBuilder.Create(PrimitiveType.Cube, "GoggleFrame", head, new Vector3(0f, 0.02f, 0.105f), new Vector3(0.2f, 0.08f, 0.05f), frame);
            PrimitiveBuilder.Create(PrimitiveType.Cube, "GoggleLens", head, new Vector3(0f, 0.02f, 0.128f), new Vector3(0.175f, 0.058f, 0.02f), MaterialFactory.Lit(gogglesLensColor, 0.95f, 0.6f));

            upperArmL = Limb.Create(PrimitiveType.Capsule, "UpperArmL", body, jacket, 0.11f);
            upperArmR = Limb.Create(PrimitiveType.Capsule, "UpperArmR", body, jacket, 0.11f);
            foreArmL = Limb.Create(PrimitiveType.Capsule, "ForeArmL", body, jacket, 0.10f);
            foreArmR = Limb.Create(PrimitiveType.Capsule, "ForeArmR", body, jacket, 0.10f);
            handL = PrimitiveBuilder.Create(PrimitiveType.Sphere, "GloveL", body, Vector3.zero, Vector3.one * 0.1f, glove).transform;
            handR = PrimitiveBuilder.Create(PrimitiveType.Sphere, "GloveR", body, Vector3.zero, Vector3.one * 0.1f, glove).transform;

            poleL = Limb.Create(PrimitiveType.Cylinder, "PoleL", body, pole, 0.025f, 0f);
            poleR = Limb.Create(PrimitiveType.Cylinder, "PoleR", body, pole, 0.025f, 0f);
            basketL = PrimitiveBuilder.Create(PrimitiveType.Cylinder, "BasketL", body, Vector3.zero, new Vector3(0.1f, 0.006f, 0.1f), glove).transform;
            basketR = PrimitiveBuilder.Create(PrimitiveType.Cylinder, "BasketR", body, Vector3.zero, new Vector3(0.1f, 0.006f, 0.1f), glove).transform;

            bodyRenderers.Clear();
            bodyRenderers.AddRange(rig.GetComponentsInChildren<MeshRenderer>(true));

            if (enableSnowSpray)
            {
                var sprayGo = new GameObject("SnowSpray");
                sprayGo.transform.SetParent(rig, false);
                sprayGo.transform.localPosition = new Vector3(0f, 0.05f, -0.65f);
                spray = sprayGo.AddComponent<SkiSnowSpray>();
                spray.Configure(snowSprayColor, maxSprayRate);
            }

            if (enableSkiTracks)
            {
                Material trackMat = MaterialFactory.Trail(false);
                trackL = CreateTrail("SkiTrackL", rig, new Vector3(-footSpacing, 0.03f, -0.5f), Quaternion.Euler(90f, 0f, 0f),
                    0.1f, skiTrackTime, trackMat, skiTrackColor, LineAlignment.TransformZ);
                trackR = CreateTrail("SkiTrackR", rig, new Vector3(footSpacing, 0.03f, -0.5f), Quaternion.Euler(90f, 0f, 0f),
                    0.1f, skiTrackTime, trackMat, skiTrackColor, LineAlignment.TransformZ);
            }
            if (enableTrickTrails)
            {
                Material glow = MaterialFactory.Trail(true, 2.5f);
                trickL = CreateTrail("TrickTrailL", footL, new Vector3(0f, 0.05f, 0.95f), Quaternion.identity, 0.16f, 0.35f, glow, trickTrailColor, LineAlignment.View);
                trickR = CreateTrail("TrickTrailR", footR, new Vector3(0f, 0.05f, 0.95f), Quaternion.identity, 0.16f, 0.35f, glow, trickTrailColor, LineAlignment.View);
            }

            ApplyPose(0f, 0f);
        }

        private static TrailRenderer CreateTrail(string name, Transform parent, Vector3 localPos, Quaternion localRot, float width,
            float time, Material mat, Color color, LineAlignment alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            var tr = go.AddComponent<TrailRenderer>();
            tr.time = time;
            tr.widthMultiplier = width;
            tr.minVertexDistance = 0.35f;
            tr.sharedMaterial = mat;
            tr.alignment = alignment;
            tr.textureMode = LineTextureMode.Stretch;
            tr.numCapVertices = 2;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(color.a * 0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            tr.emitting = false;
            return tr;
        }

        private Transform BuildFoot(string name, float side)
        {
            Transform foot = PrimitiveBuilder.CreateEmpty(name, body, new Vector3(side * footSpacing, 0f, 0f));
            Material ski = MaterialFactory.Lit(skiColor, 0.6f);
            Material dark = MaterialFactory.Lit(new Color(0.15f, 0.15f, 0.17f), 0.3f);
            float skiCenterZ = 0.08f;
            // Long thin ski under the boot...
            PrimitiveBuilder.Create(PrimitiveType.Cube, "Ski", foot, new Vector3(0f, 0.015f, skiCenterZ), new Vector3(0.1f, 0.03f, skiLength), ski);
            // ...with an upturned tip attached to its front end.
            float frontZ = skiCenterZ + skiLength * 0.5f;
            PrimitiveBuilder.Create(PrimitiveType.Cube, "SkiTip", foot,
                new Vector3(0f, 0.015f + 0.05f, frontZ + 0.087f), Quaternion.Euler(-30f, 0f, 0f), new Vector3(0.1f, 0.03f, 0.2f), ski);
            PrimitiveBuilder.Create(PrimitiveType.Cube, "Binding", foot, new Vector3(0f, 0.04f, 0f), new Vector3(0.11f, 0.03f, 0.34f), dark);
            PrimitiveBuilder.Create(PrimitiveType.Cube, "Boot", foot, new Vector3(0f, 0.11f, 0.02f), new Vector3(0.13f, 0.16f, 0.3f), MaterialFactory.Lit(bootColor, 0.5f));
            PrimitiveBuilder.Create(PrimitiveType.Cube, "BootCuff", foot, new Vector3(0f, 0.2f, -0.02f), new Vector3(0.14f, 0.06f, 0.2f), dark);
            return foot;
        }

        // ---------------- ISkierVisual ----------------
        public void SetLean(float value) => leanTarget = Mathf.Clamp(value, -1f, 1f);
        public void SetTuck(float value) => tuckTarget = Mathf.Clamp01(value);
        public void SetSpeed01(float value) => speed01 = Mathf.Clamp01(value);
        public void SetOnGround(bool value) => onGround = value;
        public void SetFlipProgress(float value) => flipProgress = Mathf.Clamp01(value);
        public void SetInvulnerable(bool value) => invulnerable = value;
        public void PlayJump() => extend = Mathf.Max(extend, 0.6f);
        public void PlayBackflip(float duration) => ball = Mathf.Max(ball, 0.3f);
        public void PlayLand(bool afterTrick) => landTimer = landCompressionTime * (afterTrick ? 1.3f : 1f);
        public void PlayCrash(float duration)
        {
            crashDuration = Mathf.Max(0.1f, duration);
            crashTimer = crashDuration;
        }

        // ---------------- Animation ----------------
        private void LateUpdate()
        {
            if (!built) return;
            float dt = Time.deltaTime;
            float k = 1f - Mathf.Exp(-poseSharpness * dt);

            lean = Mathf.Lerp(lean, leanTarget, k);
            tuck = Mathf.Lerp(tuck, tuckTarget, k);
            ball = Mathf.MoveTowards(ball, flipProgress > 0f ? 1f : 0f, dt * 7f);
            extend = Mathf.MoveTowards(extend, (!onGround && flipProgress <= 0f) ? 1f : 0f, dt * 5f);
            if (landTimer > 0f) landTimer -= dt;
            if (crashTimer > 0f) crashTimer -= dt;

            float land = landTimer > 0f ? Mathf.Clamp01(landTimer / landCompressionTime) : 0f;
            float crash = crashTimer > 0f ? Mathf.Clamp01(crashTimer / crashDuration) : 0f;

            ApplyPose(land, crash);

            // Backflip: rotate backwards (head goes back first) around the pivot.
            flipPivot.localRotation = Quaternion.Euler(-360f * flipProgress, 0f, 0f);

            // Lean into turns (roll around the feet) + crash wobble.
            float wobble = crash > 0f ? Mathf.Sin(Time.time * 22f) * 18f * crash : 0f;
            body.localRotation = Quaternion.Euler(crash * 12f, 0f, -lean * maxLeanAngle + wobble);

            if (spray != null)
                spray.SetIntensity(onGround && crash < 0.05f ? speed01 * (0.35f + 0.65f * Mathf.Abs(lean)) : 0f);

            bool tracks = onGround && crash < 0.5f && speed01 > 0.01f;
            if (trackL != null) { trackL.emitting = tracks; trackR.emitting = tracks; }
            bool trick = flipProgress > 0f;
            if (trickL != null) { trickL.emitting = trick; trickR.emitting = trick; }

            // Blink while invulnerable.
            if (invulnerable)
            {
                blinkClock += dt;
                SetRenderersVisible(Mathf.Repeat(blinkClock, 0.16f) < 0.1f);
            }
            else if (!renderersVisible)
            {
                SetRenderersVisible(true);
            }
        }

        private void ApplyPose(float land, float crash)
        {
            // --- Hip height / torso angle from the blended pose parameters ---
            float crouch = Mathf.Clamp01(baseCrouch + 0.35f * land + 0.2f * crash);
            float hipY = Mathf.Lerp(0.93f, 0.72f, crouch);
            float hipZ = Mathf.Lerp(-0.02f, -0.1f, crouch);
            float torsoPitch = Mathf.Lerp(12f, 30f, crouch);

            hipY = Mathf.Lerp(hipY, 0.6f, tuck);          // racing tuck
            hipZ = Mathf.Lerp(hipZ, -0.14f, tuck);
            torsoPitch = Mathf.Lerp(torsoPitch, 64f, tuck);

            hipY = Mathf.Lerp(hipY, 0.95f, extend);       // legs extended in the air
            hipZ = Mathf.Lerp(hipZ, -0.04f, extend);
            torsoPitch = Mathf.Lerp(torsoPitch, 15f, extend);

            hipY = Mathf.Lerp(hipY, 0.7f, ball);          // tucked ball for the backflip
            hipZ = Mathf.Lerp(hipZ, -0.05f, ball);
            torsoPitch = Mathf.Lerp(torsoPitch, 58f, ball);
            float footLift = Mathf.Lerp(0f, 0.42f, ball);
            float footForward = Mathf.Lerp(0f, 0.12f, ball);

            // --- Feet + skis ---
            Vector3 fL = new Vector3(-footSpacing, footLift, footForward);
            Vector3 fR = new Vector3(footSpacing, footLift, footForward);
            Quaternion skiTilt = Quaternion.Euler(-6f * extend, 0f, 0f);
            footL.localPosition = fL; footL.localRotation = skiTilt;
            footR.localPosition = fR; footR.localRotation = skiTilt;
            Vector3 ankleL = fL + Vector3.up * 0.16f;
            Vector3 ankleR = fR + Vector3.up * 0.16f;

            // --- Legs (two-bone IK, knees bend forward) ---
            Vector3 hip = new Vector3(0f, hipY, hipZ);
            Vector3 hipL = hip + new Vector3(-0.1f, 0f, 0f);
            Vector3 hipR = hip + new Vector3(0.1f, 0f, 0f);
            Vector3 kneeL = SolveJoint(hipL, ankleL, thighLength, shinLength, new Vector3(-0.15f, 0f, 1f));
            Vector3 kneeR = SolveJoint(hipR, ankleR, thighLength, shinLength, new Vector3(0.15f, 0f, 1f));
            thighL.Place(hipL, kneeL); shinL.Place(kneeL, ankleL);
            thighR.Place(hipR, kneeR); shinR.Place(kneeR, ankleR);

            // --- Pelvis, torso, head ---
            pelvis.localPosition = hip + Vector3.up * 0.04f;
            pelvis.localRotation = Quaternion.Euler(torsoPitch * 0.4f, 0f, 0f);
            Quaternion tr = Quaternion.Euler(torsoPitch, 0f, 0f);
            Vector3 torsoBase = hip + new Vector3(0f, 0.08f, 0f);
            torso.localPosition = torsoBase + tr * new Vector3(0f, 0.26f, 0f);
            torso.localRotation = tr;
            Vector3 neck = torsoBase + tr * new Vector3(0f, 0.54f, 0f);
            head.localPosition = neck + tr * new Vector3(0f, 0.12f, 0f);
            head.localRotation = Quaternion.Euler(torsoPitch * 0.25f, 0f, 0f); // keep looking ahead

            // --- Arms + poles ---
            Vector3 chestFront = torsoBase + tr * new Vector3(0f, 0.4f, 0.15f);
            PoseArm(-1f, torsoBase + tr * new Vector3(-0.21f, 0.47f, 0f), chestFront, kneeL, crash, upperArmL, foreArmL, handL, poleL, basketL);
            PoseArm(1f, torsoBase + tr * new Vector3(0.21f, 0.47f, 0f), chestFront, kneeR, crash, upperArmR, foreArmR, handR, poleR, basketR);
        }

        private void PoseArm(float side, Vector3 shoulder, Vector3 chestFront, Vector3 knee, float crash,
            Limb upper, Limb fore, Transform hand, Limb pole, Transform basket)
        {
            // Hand target = blend of pose-specific targets.
            Vector3 neutral = shoulder + new Vector3(side * 0.12f, -0.36f, 0.3f);
            Vector3 tucked = chestFront + new Vector3(side * 0.1f, -0.05f, 0.2f);
            Vector3 armsOut = shoulder + new Vector3(side * 0.32f, -0.22f, 0.12f);
            Vector3 grabKnee = knee + new Vector3(side * 0.07f, 0.02f, 0.06f);
            Vector3 flail = shoulder + new Vector3(side * 0.42f, 0.25f, -0.05f);

            Vector3 h = Vector3.Lerp(neutral, tucked, tuck);
            h = Vector3.Lerp(h, armsOut, extend * 0.7f);
            h = Vector3.Lerp(h, grabKnee, ball);
            h = Vector3.Lerp(h, flail, crash);

            float reach = (upperArmLength + forearmLength) * 0.98f;
            Vector3 d = h - shoulder;
            if (d.magnitude > reach) h = shoulder + d.normalized * reach;

            Vector3 elbow = SolveJoint(shoulder, h, upperArmLength, forearmLength, new Vector3(side * 0.7f, -0.2f, -0.7f));
            upper.Place(shoulder, elbow);
            fore.Place(elbow, h);
            hand.localPosition = h;

            // Poles: down-and-back normally, horizontal under the arms in a tuck.
            Vector3 dir = new Vector3(side * 0.08f, -0.86f, -0.5f).normalized;
            dir = Vector3.Lerp(dir, new Vector3(side * 0.05f, 0.12f, -1f).normalized, tuck);
            dir = Vector3.Lerp(dir, new Vector3(side * 0.1f, 0.3f, -1f).normalized, ball);
            dir = Vector3.Lerp(dir, new Vector3(side * 0.7f, 0.6f, 0.2f).normalized, crash).normalized;
            pole.Place(h - dir * 0.08f, h + dir * 1.1f);
            basket.localPosition = h + dir * 0.98f;
            basket.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
        }

        /// <summary>Two-bone IK: returns the middle joint (knee / elbow) position.</summary>
        private static Vector3 SolveJoint(Vector3 root, Vector3 end, float l1, float l2, Vector3 bendHint)
        {
            Vector3 d = end - root;
            float dist = d.magnitude;
            if (dist < 1e-4f) return root + bendHint.normalized * l1;
            Vector3 dn = d / dist;
            dist = Mathf.Min(dist, l1 + l2 - 1e-3f);
            float a = (l1 * l1 - l2 * l2 + dist * dist) / (2f * dist);
            float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - a * a));
            Vector3 bend = Vector3.ProjectOnPlane(bendHint, dn);
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.Cross(dn, Vector3.right);
            return root + dn * a + bend.normalized * h;
        }

        private void SetRenderersVisible(bool visible)
        {
            renderersVisible = visible;
            for (int i = 0; i < bodyRenderers.Count; i++)
                if (bodyRenderers[i] != null) bodyRenderers[i].enabled = visible;
        }

        /// <summary>A capsule/cylinder stretched between two points.</summary>
        private class Limb
        {
            private Transform t;
            private float thickness;
            private float overlap;

            public static Limb Create(PrimitiveType type, string name, Transform parent, Material mat, float thickness, float overlap = 0.6f)
            {
                return new Limb
                {
                    t = PrimitiveBuilder.Create(type, name, parent, Vector3.zero, Vector3.one, mat).transform,
                    thickness = thickness,
                    overlap = overlap
                };
            }

            public void Place(Vector3 a, Vector3 b)
            {
                Vector3 d = b - a;
                float len = Mathf.Max(0.01f, d.magnitude);
                t.localPosition = (a + b) * 0.5f;
                t.localRotation = Quaternion.FromToRotation(Vector3.up, d / len);
                // Primitive height is 2 units; slightly longer so joints overlap without gaps.
                t.localScale = new Vector3(thickness, (len + thickness * overlap) * 0.5f, thickness);
            }
        }
    }
}
