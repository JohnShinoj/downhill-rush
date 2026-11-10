# Downhill Rush: how the controls work

This is the reference for how player input becomes movement in the game, version v2.0. It covers:

- every timing value, delay and threshold in the pipeline;
- the steering, speed, jump and backflip math;
- what changes when the keyboard is replaced by **EMG** (jump and backflip) and the **Wii Balance Board centre of pressure (COP)** (steering and speed).

Every number here is the current default. You can change all of them in the Unity Inspector (section 9 shows where).

---

## 1. The four control channels

Every input device feeds the game through the same interface, `IGameInput`:

| Channel | Type | Range | Keyboard (now) | Planned sensor |
|---|---|---|---|---|
| `Steer` | analog | -1 (left) … +1 (right) | A / D, ← / → | Wii Board COP, medio-lateral (left/right lean) |
| `SpeedControl` | analog | -1 (brake) … +1 (boost) | S / W, ↓ / ↑ | Wii Board COP, antero-posterior (back/forward lean) |
| `JumpTriggered` | one-frame pulse | true/false | Space | EMG, forearm flexor (FCU) |
| `BackflipTriggered` | one-frame pulse | true/false | F | EMG, biceps |

The keyboard produces only -1, 0 or +1. A sensor can produce any value in between, which gives proportional control: lean halfway and you turn at half rate.

---

## 2. What happens each frame (execution order)

```
Frame N  (all in the same frame, so the code adds no extra frame of delay)
│
├─ 1. Input component       (execution order -100, runs first)
│     raw device value → clamp → dead zone → smoothing → Steer / SpeedControl
│     raw on/off       → rising edge + cooldown        → JumpTriggered / BackflipTriggered
│
└─ 2. PlayerController.Update
      ReadInput()            copies the values (all zeroed while stunned)
      UpdateSpeed()          boost / brake / carve drag / downhill acceleration
      UpdateHeadingAndMove() Steer → turn rate → heading → position; banks at the edges
      UpdateVertical()       ground snap, ramp lip launch, gravity, landing
      HandleBufferedActions()runs the jump / backflip if it is allowed now (buffer + coyote time)
      Overlaps + visuals     obstacles, coins, skier pose
```

At 60 FPS one frame is about **16.7 ms**. The game adds no frame of delay of its own. The delays that do exist are deliberate and listed in section 6, plus whatever delay the sensor pipeline adds before the game receives the signal.

---

## 3. Steering (v2: gradual turn that keeps its heading)

### 3.1 The input stage: `GameInputBase`

```
raw   = Clamp(device, -1, 1)
raw   = DeadZone(raw, 0.05)        // |x| ≤ 0.05 → 0, the rest is rescaled to 0..1
Steer = MoveTowards(Steer, raw, dt / responseTime)
        responseTime = 0.12 s  when moving away from 0 (pressing or leaning)
        responseTime = 0.04 s  when moving toward 0 or reversing (releasing)
```

- **Ramp-up**: it takes 0.12 s to go from 0 to full deflection, which is a linear ramp.
- **Release**: it takes 0.04 s to go from full back to 0. Release is deliberately faster so the turn stops almost as soon as you let go.
- **Dead zone**: 5 % of the range. It removes sensor noise and the small body sway you make when standing still.

### 3.2 The gameplay stage: `PlayerController`

The skier has a **heading** (yaw), measured in degrees from straight downhill.

```
authority = 1.0 on the ground, 0.861 in the air      (airSteerFactor)
yaw      += Steer × 80 °/s × authority × dt          (steerTurnRate)
yaw       = Clamp(yaw, -60°, +60°)                   (maxHeadingAngle)
position += (sin yaw, 0, cos yaw) × Speed × dt
```

Key points:

- **Steer is a turn rate, not an angle.** Holding right keeps turning right at up to 80 °/s.
- **The heading persists.** When steering returns to 0 the skier keeps the current heading and keeps drifting sideways. The game does not pull you back to the track. To go straight again, steer the opposite way.
- **Heading limit ±60°.** Going from straight to the limit at full steer takes about 60 / 80 ≈ **0.75 s** (plus the 0.12 s ramp-up).
- Sideways speed = `Speed × sin(yaw)`. At 20 m/s, a 10° heading gives about 3.5 m/s sideways and 30° gives 10 m/s.

| Hold at full steer | Heading reached* |
|---|---|
| 0.1 s tap | ≈ 5° |
| 0.25 s | ≈ 17° |
| 0.5 s | ≈ 37° |
| 0.75 s | ≈ 57° |
| ≥ 0.82 s | 60° (limit) |

\* This includes the 0.12 s ramp-up and the 0.04 s release.

### 3.3 Track edges (banks)

When the skier goes past the edge of the piste and onto the snow bank:

- speed drops by up to **8 m/s²** (`bankFriction`), scaled by how far up the bank you are;
- the heading is pushed back toward the piste at up to **110 °/s** (`bankPushTurnRate`), also scaled;
- a hard wall at **85 %** of the bank width (`hardBoundaryFraction`) stops you going any further.

### 3.4 After a crash (stun)

For **0.9 s** (`stunDuration`) all input is ignored, Steer and SpeedControl count as 0, and the heading turns back to the track direction at 170 °/s (`turnRate`). After that comes **1.8 s** of invulnerability, during which the skier blinks.

---

## 4. Speed

```
On the ground:
  accel = 1.6                               (downhillAcceleration)
        + SpeedControl × 8     if SpeedControl > 0   (boostAcceleration)
        + SpeedControl × 14    if SpeedControl < 0   (brakeDeceleration)
        - 1.5 × |Steer|                    (carveDrag: turning costs speed)
In the air:  accel = 1.6 × 0.3
Stunned:     accel = -7
Speed = Clamp(Speed + accel × dt, 8, 34 × difficulty)  // the max rises to about 1.2× over time
Start speed: 14 m/s
```

`SpeedControl` goes through the same smoothing as steering, but slower: **0.25 s** from 0 to full, with a 5 % dead zone. A forward/back lean is therefore filtered more heavily than a left/right lean, which is the right trade-off for a slow postural signal.

---

## 5. Jump and backflip

### 5.1 The trigger stage: `GameInputBase`

```
JumpTriggered = request now && !request last frame      // rising edge only
                && time ≥ nextAllowed                    // cooldown
if JumpTriggered: nextAllowed = time + 0.25 s           // jumpCooldown (backflip: 0.30 s)
```

- **One trigger per activation.** A held key or a long muscle contraction fires only once. The signal has to drop back to "off" before it can fire again.
- **Cooldown.** Jump 0.25 s and backflip 0.30 s. This stops a single noisy EMG burst from firing twice.
- The trigger is true for exactly **one frame**.

### 5.2 The gameplay stage: `PlayerController`

| Rule | Value | What it does |
|---|---|---|
| Input buffer | **0.13 s** (`inputBufferTime`) | A trigger is remembered for 0.13 s and runs as soon as it becomes possible, for example a jump pressed just before landing. |
| Coyote time | **0.12 s** (`coyoteTime`) | You can still jump for 0.12 s after leaving the ground, for example just past a ramp lip. |
| Backflip | airborne only | A backflip is accepted only while in the air and not already flipping. Several flips in one air time are allowed. |
| Landing tolerance | **0.18 m** height, **55°** rotation | The landing counts when the feet come within 0.18 m of the ground. A flip counts as clean when the skier is within 55° of upright. |

### 5.3 Air time numbers

Gravity is **25 m/s²** (arcade style; real gravity is 9.81).

| Situation | Launch speed | Air time (flat ground) | Peak |
|---|---|---|---|
| Normal jump | 10 m/s (`jumpVelocity`) | **0.80 s** | 2.0 m |
| Ramp, no jump | 12 m/s (ramp `rampLaunchVelocity`) | ≈ 1.0–1.1 s | ≈ 2.9 m |
| Jump on or just off a ramp | max(current, 12, 10) + 3 = 15 m/s (`rampJumpBonus`) | ≈ 1.2–1.3 s | ≈ 4.5 m |
| One backflip | | **0.62 s** (`flipDuration`) | |

A backflip fits inside a normal jump if it starts within about **0.18 s** of take-off (0.80 - 0.62), plus the landing tolerance. On a ramp there is plenty of time.

---

## 6. All the delays in one place

| Stage | Delay | Setting | Where |
|---|---|---|---|
| Frame time | ≈ 16.7 ms at 60 FPS | | |
| Steer ramp-up (0 → full) | **0.12 s** | `steerResponseTime` | Input component |
| Steer release (full → 0) | **0.04 s** | `steerReleaseTime` | Input component |
| Speed ramp (0 → full) | **0.25 s** | `speedResponseTime` | Input component |
| Steer / speed dead zone | 5 % | `steerDeadZone`, `speedDeadZone` | Input component |
| Heading 0 → 60° at full steer | ≈ 0.8 s | `steerTurnRate` 80, `maxHeadingAngle` 60 | SkierSettings |
| Jump cooldown | 0.25 s | `jumpCooldown` | Input component |
| Backflip cooldown | 0.30 s | `backflipCooldown` | Input component |
| Jump / flip buffer | **0.13 s** (forgiveness for early triggers) | `inputBufferTime` | SkierSettings |
| Coyote time | **0.12 s** (forgiveness for late jumps) | `coyoteTime` | SkierSettings |
| Backflip rotation | 0.62 s | `flipDuration` | SkierSettings |
| Stun after a crash | 0.9 s | `stunDuration` | SkierSettings |

The **buffer** and **coyote time** together give a ±0.12–0.13 s window around the ideal moment. This is what makes delayed sensor signals playable.

---

## 7. EMG: how it affects the controls

> The implemented filters and thresholds (measured from the recordings) are in [SENSORS.md](SENSORS.md). Where the two differ, SENSORS.md is current: the envelope is 8 Hz, the jump threshold 0.30 and the backflip threshold 0.40.

### 7.1 Mapping

- **FCU (forearm flexor) contraction → `JumpTriggered`**
- **Biceps contraction → `BackflipTriggered`**

The game only needs **a boolean per muscle**: "contracting now: yes or no". It handles edges, cooldown, buffer, coyote time and the airborne-only rule for backflips itself.

### 7.2 Recommended processing, done outside the game before sending

```
raw EMG (≥ 1 kHz)
 → band-pass 20–450 Hz          removes movement artefact and high-frequency noise
 → 50 Hz notch (+ harmonics)    removes mains interference (India: 50 Hz)
 → full-wave rectify
 → envelope: 6–10 Hz low-pass, or a 50–100 ms moving RMS
 → normalise to %MVC            (calibrate a maximum voluntary contraction per user, per session)
 → threshold with hysteresis:   ON above ~30 % MVC, OFF below ~15 % MVC
 → minimum ON duration 40–60 ms (rejects spikes)
 → send the boolean to the game at about 100 Hz (UDP)
```

### 7.3 How each game setting interacts with EMG

| Game setting | Effect on EMG |
|---|---|
| Rising-edge detection | A long, sustained contraction gives only **one** jump. The muscle must relax below the OFF threshold before the next one. |
| `jumpCooldown` 0.25 s / `backflipCooldown` 0.30 s | A burst that crosses the threshold twice (chatter) is ignored. If players can't jump fast enough in a row, lower it. If you see double jumps, raise it. |
| `inputBufferTime` 0.13 s | An EMG trigger that arrives up to 0.13 s **before** it is possible (for example just before landing) still works. |
| `coyoteTime` 0.12 s | An EMG trigger that arrives up to 0.12 s **after** leaving the ground (or the ramp lip) still gives a jump. This absorbs envelope and filter lag. |
| Backflip only in the air | A biceps contraction on the ground does nothing. There are no accidental flips. |
| Ramp context | An FCU contraction on a ramp or shortly after leaving one gives the larger ramp jump (15 m/s). |

### 7.4 Latency budget for EMG

| Source | Typical |
|---|---|
| Electromechanical delay (muscle) | already ahead of movement, so no cost |
| Envelope filter (6–10 Hz LP / 50–100 ms RMS) | 25–60 ms |
| Minimum-duration check | 40–60 ms |
| Transport (UDP, 100 Hz) | 5–15 ms |
| Game (same-frame read) | ≤ 17 ms |
| **Total** | **≈ 80–150 ms**. Keep it under 150 ms. The coyote and buffer windows cover about 120–130 ms of it. |

Watch for **crosstalk**: a hard grip can also activate the biceps, which would trigger an unintended flip. That only matters in the air. Watch for **fatigue** too: if %MVC drifts down over a session, recalibrate or lower the ON threshold.

---

## 8. Wii Balance Board (COP): how it affects steering and speed

> The implemented COP filter (3 Hz low-pass, 10 cm full scale, dead zones 0.08 / 0.30) is described in [SENSORS.md](SENSORS.md).

### 8.1 Mapping

- **Medio-lateral COP (left/right) → `Steer`**
- **Antero-posterior COP (forward/back) → `SpeedControl`**

### 8.2 Recommended processing before sending

```
4 load cells → COP x, y (mm)
 → subtract the neutral stance (calibrate: stand still for 3–5 s, take the mean)
 → divide by the comfortable lean range (calibrate: lean fully left/right and forward/back,
   use about 70–80 % of the maximum so full deflection needs no strain)
 → clamp to -1..1
 → optional light low-pass at 5–10 Hz (the game smooths as well)
 → send at 60–100 Hz
 → if the total weight is under about 20 kg (stepped off), send 0
```

### 8.3 What a lean does in v2

Steering is a **turn rate**, so:

- **Lean right and hold** → the skier keeps turning right, up to a 60° heading.
- **Return to centre** → the skier **keeps the heading reached** and keeps drifting in that direction. It does not straighten by itself.
- **Lean the other way** to straighten or turn back.
- **Half lean** (Steer = 0.5) → half the turn rate (40 °/s).

This suits balance training: the player has to make an active corrective weight shift, not just relax. If you want "lean = angle" instead (centre = straight ahead), that is a different steering model and would be a code change.

Forward lean = boost (up to +8 m/s²) and back lean = brake (up to -14 m/s²), both ramped over 0.25 s.

### 8.4 How each game setting interacts with COP

| Setting | Raise it → | Lower it → |
|---|---|---|
| `steerDeadZone` (0.05) | more stance sway ignored; small leans do nothing | more sensitive; may drift from sway |
| `steerResponseTime` (0.12 s) | smoother, laggier | snappier, passes more noise through |
| `steerReleaseTime` (0.04 s) | the turn continues a little after you re-centre | the turn stops almost instantly |
| `steerTurnRate` (80 °/s) | a lean turns faster (more sensitive) | gentler; needs a longer lean |
| `maxHeadingAngle` (60°) | allows sharper sideways travel | keeps the skier more downhill |
| `speedDeadZone` / `speedResponseTime` | same idea for forward/back | |

Steering from the board passes through the **0.12 s ramp** plus your own filter. Postural control is already slow (about 200–500 ms per weight shift), so this delay is small compared with the body.

---

## 9. Where to change the values

**Skier settings** (turn rate, heading limit, jump, gravity, buffer, coyote time, crash):
`Project` window → `Assets/Settings/SkierSettings` → Inspector.

**Input smoothing** (response times, dead zones, cooldowns):
`Hierarchy` → **Input** object (the `KeyboardInput` component; later `EmgCopInput`) → Inspector.

- Changes made **in Play mode are lost** when you stop. Changes to the **SkierSettings asset** are kept.
- Save the scene (Ctrl+S) after changing a scene object outside Play mode.

**Checking what is happening:**

- **F1** in game opens the debug HUD. It shows raw vs smoothed Steer and SpeedControl, trigger flashes, speed, heading and grounded state, live.
- **Log Airtime** checkbox on `Player` → `PlayerController` prints each take-off and landing with its air time to the Console.

---

## 10. Adding the sensor input (outline for the next step)

Create a component that inherits from `GameInputBase` and implement four methods:

```csharp
public class EmgCopInput : GameInputBase
{
    protected override float ReadSteer()          => latestCopX;      // -1..1
    protected override float ReadSpeedControl()   => latestCopY;      // -1..1
    protected override bool  ReadJumpRequest()    => latestFcuOn;     // EMG boolean
    protected override bool  ReadBackflipRequest()=> latestBicepsOn;  // EMG boolean
}
```

The values are filled from a background UDP receiver. Then disable `KeyboardInput` on the **Input** object and add `EmgCopInput`. Everything in sections 3–8 then applies to the sensor signals unchanged.
