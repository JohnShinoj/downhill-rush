# Downhill Rush — 3D Skiing Prototype (Unity 6, primitives only)

A complete, endless downhill skiing prototype built **only from Unity primitives and
code-generated meshes/materials** (no external assets). Steer down a winding slope, dodge
obstacles, jump logs and crevasses, launch off ramps, do backflips for **x2 coins**.

The game is built so that the next step — **EMG** (jump, backflip) and **Wii Balance Board
Centre-of-Pressure** (steering, speed) input — is a small, self-contained change.

---

> **Controls reference:** see [CONTROLS.md](CONTROLS.md) for steering math, all timing/delay values, and how EMG and the Wii Balance Board map onto the controls.

## 1. How to open, run and play

### Open
1. Open **Unity Hub** → **Add** → **Add project from disk** → choose the folder
   `D:\SSRP Game project`.
2. Open it with **Unity 6000.6.8f1** (Unity 6.6). Unity 2022.3 LTS also works.
3. On the first open the project **sets itself up automatically**: it creates
   `Assets/Settings/*.asset` and builds `Assets/Scenes/SkiGame.unity` with everything wired.
   (Console: `[SkiGame] Scene ready ... press Play.`)
4. If the scene is not open: **Tools → Ski Game → Open Game Scene**.
5. Press **Play**.

If anything gets broken: **Tools → Ski Game → Rebuild Scene** recreates the scene (your
tuning in `Assets/Settings` is kept).

### Controls (keyboard)
| Key | Action |
|---|---|
| **A / D** or **← / →** | Turn left / right gradually (hold to keep turning, let go to keep your current direction) |
| **W / S** or **↑ / ↓** | Speed up (tuck) / slow down (brake) |
| **Space** | Jump |
| **F** | Backflip — works any time you are airborne (normal jump or ramp) |
| **Enter** | Start (menu) / Restart (game over) |
| **Esc** or **P** | Pause / resume |
| **R** / **M** | Restart / main menu (from pause or game over) |
| **F1** | Toggle the input debug HUD |

### Rules
* **Red** things (trees with a red ring, rocks, red walls) → **steer around** them.
* **Orange** things (logs, crevasses with orange edges) → **jump over** them.
* **Blue** ramps → launch you for long airtime.
* **Gold** coins → 1 point each.
* **Backflip**: press **F** in the air. A full 360° flip takes 0.62 s; a normal jump lasts
  0.8 s, so press F early in the jump (or up to 130 ms *before* jumping - it is buffered). Land within ±55° of upright = success:
  **coins collected during that airtime ×2 + a landing bonus**, and a "**BACKFLIP x2!**"
  popup. Land mid-rotation = crash.
* Crash = speed loss, a short stun, **−1 life**, then a short invulnerability (the skier blinks).
* 3 lives. Difficulty (density, wall/long-log/crevasse frequency, max speed) rises with distance.
* Main menu button switches **Endless** ↔ **Course** (fixed length with a finish line).

### Test checklist
- [ ] Menu shows; Enter starts the run.
- [ ] A/D steer with smooth lean; W tucks and speeds up, S brakes; the HUD speed changes.
- [ ] The slope winds, keeps generating ahead and disappears behind; riding into the
      side banks slows you and pushes you back.
- [ ] Space jumps (~0.7 s); F in the air does a smooth backflip in a ball pose.
- [ ] Clean flip landing → "BACKFLIP x2!" popup and doubled air-coins; a late flip → crash.
- [ ] Ramps launch you higher/longer; coin arcs follow the jump/ramp trajectory.
- [ ] Hitting a red obstacle or a log without jumping → crash, −1 life, blink.
- [ ] 0 lives → Game Over screen; Restart and Main menu work; Esc pauses.
- [ ] F1 shows Steer / SpeedControl / JumpTriggered / BackflipTriggered live.

---

## 2. Folder and architecture overview

```
Assets/
  Scenes/SkiGame.unity             (auto-generated)
  Settings/*.asset                 (all tuning numbers - edit these in the Inspector)
  Scripts/
    Input/        IGameInput, GameInputBase, KeyboardInput
    Player/       PlayerController, SkierSettings
    Player/Visual ISkierVisual, PrimitiveSkierVisual, SkiSnowSpray
    World/        SlopeGenerator, SlopeChunk, SlopeSettings, ContentSpawner, SpawnTable,
                  ObstacleFactory, Obstacle, Ramp, Coin
    UI/           GameHUD, DebugInputHUD, HudDrawing
    FX/           GameEffects (snowfall, speed lines, powder, sparkles), ParticleFactory
    Core/         GameManager, GameEvents (+GameState, GameLayers), ScoreManager,
                  DifficultyManager, DifficultySettings, GameRulesSettings, SkiCamera,
                  MaterialFactory, MeshFactory, PrimitiveBuilder, SkiPostEffects,
                  GameAudio, ShaderLibrary
    World/        (+ LightingRig, VisualSettings)
    Editor/       SkiGameSceneBuilder (auto setup + Tools menu)
  Shaders/        SkiPostFX (god rays/bloom/tonemap/grade), SkiSnow (glitter snow), SkiSky,
                  SkiBackdrop (mountains), SkiCloud, SkiParticle + SkiParticleAdditive
```

### Scene layout
| GameObject | Components |
|---|---|
| **Player** | `PlayerController`, `KeyboardInput` |
| └ SkierVisual (Primitive) | `PrimitiveSkierVisual` (builds the model at runtime) |
| **World** | `SlopeGenerator`, `ContentSpawner` (chunks are created as children) |
| **Main Camera** | `Camera` (HDR), `SkiCamera`, `SkiPostEffects` |
| **Game** | `GameManager`, `ScoreManager`, `DifficultyManager`, `GameHUD`, `DebugInputHUD`, `ShaderLibrary`, `GameEffects`, `GameAudio` |
| **Lighting** (Sun + Fill Light) | `LightingRig` (sky, fog, ambient, shadows, distant mountains) |

### Data flow
```
 [Any IGameInput] --Steer/Speed/Jump/Flip--> PlayerController --ISkierVisual--> model
                                                  |  raises
                                                  v
                                             GameEvents  (static C# events)
                       /             |               |              \
               ScoreManager     GameManager       GameHUD         SkiCamera
              (score, coins)  (lives, states)  (popups, menus)   (crash shake)

 SlopeGenerator --ChunkSpawned--> ContentSpawner (reads SpawnTable + DifficultySettings)
```

### Key design decisions
* **Custom kinematic controller, no Rigidbody.** Jump airtime and flip timing must be exact
  and repeatable so a delayed EMG trigger always behaves the same; physics adds solver
  jitter, bounces and friction on a procedural mesh. Buffers, coyote time and a generous
  landing check are trivial when we own the vertical motion. Height is computed **relative
  to the slope plane**, so jump airtime = `2 · jumpVelocity / gravity` regardless of speed.
* **Ground** = raycast down against layer *Default* (slope chunks + ramps).
  **Obstacles/coins** = overlap capsule against layer *Ignore Raycast*. Only built-in
  layers are used → **no Tags/Layers/Physics setup is needed.**
* **Analytic slope.** Height and path centre are pure functions of (x, z). Mesh chunks,
  spawner, camera and player all query the same functions.
* **Old Input Manager.** Fine here because input is isolated behind `IGameInput`.
  The editor script switches *Active Input Handling* to **Both** if the project is
  set to "new only" (Tools → Ski Game → Fix Input Handling). For the EMG/COP edit you will
  not read Unity input at all, so this choice does not matter later.
* **IMGUI UI** (OnGUI): zero scene setup, no packages or fonts, scales to any resolution.
* **Built-in render pipeline**, Standard shader; materials/textures are created in code.
  (Unity 6.5+ shows a notice that the Built-in pipeline is deprecated - it is still fully
  supported through Unity 6.7 LTS, and needs no package installs.)

### Look & feel (lighting, effects, audio)
* **Lighting** (`LightingRig` + `VisualSettings`): warm low golden-hour sun with soft
  4-cascade shadows, cool blue fill light, procedural sky with a sun disc, blue trilight
  ambient (snow shadows turn blue), distance fog, and a ring of distant snow-capped
  low-poly mountains that always stays on the horizon.
* **Sky** (`SkiSky.shader`): art-directed golden-hour sky - deep blue zenith, horizon that
  turns warm peach toward a low sun, HDR sun disc + halo, high cirrus streaks - plus soft
  procedural clouds (`SkiCloud.shader`) that glow warm near the sun.
* **Mountains** (`SkiBackdrop.shader`): three ranges (near ridges, main range, far giants)
  for depth; ridged low-poly peaks with snow streaking down gullies, rocky cliffs, warm
  sunlit faces, blue shadow faces, pink alpenglow and distance haze. All colours live in
  `VisualSettings` (Sky / Distant mountains sections).
* **Scenery**: snow-laden 3-tier pines with shade variation, low-poly boulders with snow
  caps (obstacles and decoration), dense forest edge.
* **Post-processing** (`SkiPostEffects` + `SkiPostFX.shader`, no packages): god rays
  (sun shafts that are blocked by trees/terrain using the depth buffer), HDR bloom,
  ACES filmic tonemapping, warm-highlight / cool-shadow grading, saturation & contrast,
  vignette that tightens at top speed, fine film grain.
* **Snow** (`SkiSnow.shader`): PBR snow with ripple normal map, view-dependent glitter
  sparkles that twinkle toward the sun (bloom turns them into glints), and a cool
  subsurface tint at grazing angles.
* **UI**: Bahnschrift / Segoe UI system font (no font files), rounded panels, gold
  selected-state buttons, text that auto-shrinks to fit its button.
* **Particles** (`GameEffects`, `SkiSnowSpray`): snow spray from the skis, ambient
  snowfall, speed lines at high speed, powder puff on landing, snow explosion on crash,
  golden sparkles on coins, star burst on a clean backflip.
* **Trails**: ski tracks carved in the snow, glowing gold ribbons from the ski tips during a flip.
* **Glow**: coins and ramp lips are emissive (they bloom).
* **Camera**: cinematic orbit around the skier in the main menu, blends into the follow cam.
* **Audio** (`GameAudio`, fully synthesised - no audio files): wind that rises with speed,
  ski carve hiss, coin chime (pitch climbs on combos), jump/flip whooshes, trick fanfare,
  landing thud, crash.

### Where the numbers live (all Inspector-editable, nothing hard-coded)
| Asset | Contains |
|---|---|
| `SkierSettings` | speeds, acceleration, steering, banks, jump velocity, gravity, flip duration, landing tolerance, **input buffer, coyote time**, crash/stun/invulnerability |
| `SlopeSettings` | angle, path width, banks, winding, chunk size, endless vs. course length, colours, fog |
| `SpawnTable` | list of obstacles/ramps (kind, shape/prefab, weight, min difficulty, size, colour, coin arcs), coin lines, ramp size |
| `DifficultySettings` | distance to max difficulty, curve, row spacing, obstacles per row, speed bonus |
| `GameRulesSettings` | lives, coin value, backflip multiplier, landing bonus, points per metre |
| `VisualSettings` | sun / fill light, sky, ambient, fog, shadows, MSAA, mountains, snowfall |
| `SkiPostEffects` (camera component) | bloom, exposure, contrast, saturation, colour tints, vignette, grain |
| `GameAudio` (component) | master / effects / wind / carve volume |
| `KeyboardInput` (component) | steer/speed **response time**, dead zones, jump/backflip **cooldowns**, key bindings |

---

## 3. Next edit: EMG and COP integration

The next edit integrates:
* **EMG** — **JUMP** = flexor carpi ulnaris flex, **BACKFLIP** = biceps flex while airborne.
* **Wii Balance Board Centre of Pressure** — **STEERING** = sideways lean,
  **SPEED UP / DOWN** = forward / backward lean.

Signal acquisition and filtering are done by the student. The game only needs the final
values through one interface:

```csharp
public interface IGameInput
{
    float Steer { get; }            // -1 (full left lean) .. 0 .. +1 (full right lean)
    float SpeedControl { get; }     // -1 (full backward lean = brake) .. +1 (full forward lean = speed up)
    bool  JumpTriggered { get; }    // true for ONE frame when FCU flex is detected
    bool  BackflipTriggered { get; }// true for ONE frame when biceps flex is detected
}
```

| Property | Source | Range / meaning |
|---|---|---|
| `Steer` | COP medio-lateral (x) | float **−1..+1**, 0 = centred. Normalise by the subject's calibrated max lean. |
| `SpeedControl` | COP antero-posterior (y) | float **−1..+1**, + = forward lean (speed up), − = backward lean (slow down). |
| `JumpTriggered` | EMG flexor carpi ulnaris | **bool, true for exactly one frame** per flex (threshold crossing). |
| `BackflipTriggered` | EMG biceps | **bool, true for exactly one frame** per flex. The game itself only flips if airborne. |

### Recommended: derive from `GameInputBase`
`GameInputBase` already gives you **smoothing with adjustable response time, dead zones,
rising-edge detection and cooldowns**, so you only supply raw values. Your detector may
return a *level* ("muscle is active") — the base class converts it to a single trigger.

```csharp
// Assets/Scripts/Input/EmgCopInput.cs   (next edit)
using UnityEngine;
namespace SkiGame
{
    public class EmgCopInput : GameInputBase
    {
        protected override float ReadSteer()          { /* COP x, normalised to -1..1 */ return 0f; }
        protected override float ReadSpeedControl()   { /* COP y, normalised to -1..1 */ return 0f; }
        protected override bool  ReadJumpRequest()    { /* FCU EMG above threshold   */ return false; }
        protected override bool  ReadBackflipRequest(){ /* biceps EMG above threshold */ return false; }
    }
}
```
(You may also implement `IGameInput` directly if you already do your own smoothing.)

### How to switch the input source
1. Select **Player** in the Hierarchy.
2. **Add Component →** your new input class (e.g. `EmgCopInput`).
3. Drag that component into **PlayerController → Input Source**.
4. Disable (untick) or remove `KeyboardInput`.

At runtime you can also call `player.SetInputSource(myInput)`.
Nothing else changes — gameplay, the debug HUD (F1) and all tuning stay the same.

### Built-in tolerance for delayed / noisy signals (tune in `SkierSettings`)
* **Input buffer** (`inputBufferTime`, default 130 ms): a jump/flip trigger that arrives
  slightly too early is remembered and executed as soon as it becomes possible
  (jump on touchdown, flip right after take-off).
* **Coyote time** (`coyoteTime`, 120 ms): a jump still works shortly after leaving the ground.
* **Generous landing check** (`landingTolerance` height, `landingAngleTolerance` ±55°).
* **Response time / dead zone / cooldowns** on the input component.
* Use the **F1 debug HUD** to see the live values and raw targets while calibrating.

---

## 4. Replacing the character

Gameplay never touches the model — it only calls `ISkierVisual`:

```csharp
void SetLean(float lean);          // -1..1, every frame
void SetTuck(float tuck);          // 0..1, every frame
void SetSpeed01(float speed01);    // 0..1, every frame (spray, wind, etc.)
void SetOnGround(bool onGround);
void SetFlipProgress(float p01);   // 0..1 = 0..360°, 0 when not flipping - follow it exactly
void SetInvulnerable(bool on);     // blink after a crash
void PlayJump();  void PlayBackflip(float duration);
void PlayLand(bool afterTrick);  void PlayCrash(float duration);
```

Steps:
1. Import your model (FBX, rigged or not) and place it as a **child of Player**
   (feet at the Player origin, facing **+Z**).
2. Create a script implementing `ISkierVisual` on that child, e.g. map the calls to Animator
   parameters (`animator.SetFloat("Lean", lean)`, `animator.SetTrigger("Jump")`, …) and
   rotate a pivot by `-360 * p01` around X for the backflip.
3. Drag it into **PlayerController → Visual Component** (or just make sure it is the
   only `ISkierVisual` in Player's children).
4. Delete or disable the `SkierVisual (Primitive)` child.
5. Want the snow spray? Add `SkiSnowSpray` to a child at the ski tails and call
   `SetIntensity(0..1)` from your visual.

Colours of the primitive skier (jacket, helmet, goggles, skis, spray, …) are on the
`PrimitiveSkierVisual` component.

---

## 5. How to extend the game

* **New obstacle / ramp** — open `Assets/Settings/SpawnTable`, add an entry: choose a
  *kind* (Swerve / Jumpable / Ramp), a built-in *shape* or **CustomPrefab + prefab**,
  weight, min difficulty, size, colour, coin arc. Colliders and layers are set up
  automatically. A new procedural shape = one `case` in `ObstacleFactory.Create`.
* **New input device** — new `GameInputBase` subclass (see section 3).
* **New reaction to events** (sound, particles, achievements, a data logger for your
  EMG/COP study) — subscribe to `GameEvents` (`Jumped`, `BackflipStarted`, `Landed`,
  `Crashed`, `CoinCollected`, `PointsAwarded`, `BackflipLanded`, `StateChanged`) in
  `OnEnable` and unsubscribe in `OnDisable`. No existing script changes.
* **New trick** (e.g. a spin on another muscle) — add a property to your input class,
  handle it in `PlayerController.HandleBufferedActions` like the backflip, add a
  `Play…` method to `ISkierVisual`.
* **Difficulty** — `DifficultySettings` curve + `minDifficulty` per spawn entry.
* **Slope look / shape** — `SlopeSettings` (angle, width, winding, colours, fog).
* **Scoring** — `GameRulesSettings`, or listen to events in a new scoring component.
* **Course mode** — `SlopeSettings.useFixedCourse` + `courseLength` (or the menu button).

### Known limitations / ideas
* Very long endless runs (tens of km) slowly lose float precision; a floating-origin shift
  would fix it.
* Coin arcs assume ~20 m/s (`SpawnTable.coinArcSpeedEstimate`); at very different speeds
  the arc and the actual trajectory drift apart (coins have a generous pickup radius).
* Fixed: obstacles were briefly at the world origin when spawned (physics not yet synced),
  which could cost a life right at the start of a run ("Hit a pine tree").

---

## Changelog

### v2.0
* **Gradual steering:** the steer input sets the turn *rate* (`SkierSettings.steerTurnRate`,
  default 55 deg/s). Holding right keeps turning right; letting go keeps the current
  direction (no automatic track-following). The heading is limited to `maxHeadingAngle`
  (60 deg) from straight downhill, and the soft side banks still push you back.
* **Ramp jumps capped:** jumping on a ramp, or right after leaving one, adds only
  `rampJumpBonus` (3 m/s) on top of the ramp launch instead of a full extra jump.

### v1.x
* Ramps launch reliably at any frame rate (launch is frame-based; ground snap is capped).
* Performance: decoration in each slope chunk is merged into a few meshes, GPU instancing,
  2 shadow cascades, 2x MSAA. In the editor, set the Game view to 1920x1080 for best FPS.
  Tick `PlayerController > Log Airtime` to print every take-off / landing to the Console.
* Fixed: obstacles were briefly at the world origin when spawned, which could cost a life
  right at the start of a run.
