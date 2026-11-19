# Downhill Rush — 3D Skiing Prototype (Unity 6, primitives only)

A complete, endless downhill skiing prototype built **only from Unity primitives and
code-generated meshes/materials** (no external assets). Steer down a winding slope, dodge
obstacles, jump logs and crevasses, launch off ramps, do backflips for **x2 coins**.

**Played with the body.** Squeeze the forearm to jump and curl the biceps to backflip
(BioRadio **EMG**). Lean on a **Wii Balance Board** to steer and change speed (centre of pressure).
The keyboard still works for testing.

> ### ⬇ Download the game from the `main` branch
> **The complete, final game is on `main`, the default branch. You don't need any other branch.**
> * **Easiest:** on the GitHub page click **Code → Download ZIP** and unzip it. This downloads `main`.
> * **With Git:** `git clone https://github.com/JohnShinoj/downhill-rush.git` (this also gets `main`).
>
> Any other branches in this repository are old work-in-progress copies kept only for history.
> Ignore them. Everything from them is already inside `main`.

## ▶ Running the final version (start here)

This `main` branch is the **final version**. Everything needed to open the game, play it with the
keyboard, replay the recordings, and play it **live** with the BioRadio EMG and the Wii Balance
Board is in this repository. The only exception is the BioRadio SDK (`BioRadioSDK.dll`), which is
vendor-licensed. See step C2.

### What you need on the laptop

| Need | For | Where to get it |
|---|---|---|
| **Unity 6000.6.8f1** (install via Unity Hub) | the game | Unity Hub → Installs → Install Editor → Archive → 6000.6.8f1 |
| **Git** | getting the code | https://git-scm.com |
| **Python 3** (the `py` launcher) | the EMG bridge | https://python.org. Tick "Add to PATH" when installing |
| **BioRadio + BioRadio Student Unity Kit** | live EMG | from the lab (the kit contains `BioRadioSDK.dll`) |
| **Wii Balance Board + Bluetooth** | live steering | the board tools are already in `Tools/WiiBoard/` |

### A. Get the project and open it

```bat
git clone https://github.com/JohnShinoj/downhill-rush.git
cd downhill-rush
```
`git clone <url>` downloads the repository into a new `downhill-rush` folder (the `main` branch).
`cd downhill-rush` moves into that folder. (Or use **Code → Download ZIP** on GitHub and unzip it.)

1. **Unity Hub → Add → Add project from disk** → choose the `downhill-rush` folder.
2. Open it with **Unity 6000.6.8f1**. The first import takes a few minutes, because Unity rebuilds `Library/`.
3. If the game scene isn't open: **Tools → Ski Game → Open Game Scene** (`Assets/Scenes/SkiGame.unity`).
4. Check that the **Console** shows no red errors.

### B. Play without hardware (keyboard or recordings)

- Press **Play**, then **Enter**. Keyboard controls: **A/D** steer, **W/S** speed, **Space** jump, **F** backflip (in the air).
- Press **F6** on the menu to switch to **REPLAY**. The game is then driven by the recorded data
  (`Assets/StreamingAssets/john_final.csv` for EMG, `John14.txt` for COP). Use this for a demo without sensors.

### C. Play LIVE with the sensors

Do these steps in order. Start each program **before** pressing Play.

**C1 – Wii Balance Board → `Tools/WiiBoard/`**
1. **Pair the board (first time on a laptop only):** open `Tools/WiiBoard/WiiBalanceWalker.exe` →
   **Add/Remove bluetooth Wii device**, and press the red **SYNC** button under the board's battery cover.
   When it says connected, **close WiiBalanceWalker**. Only one program can use the board at a time.
   Also close the Wii Balance Board Recorder if it is open.
2. Double-click **`Tools/WiiBoard/start_board.bat`**. It runs `wiiuse.exe` in that folder, and wiiuse writes
   `Tools/WiiBoard/WiiMote1_All.txt` (about 85 lines/s). The game reads that file live and never changes it.
   - At the start it offers to delete the old log. Say **Y**, because the file only grows (about 1 GB after a few hours).
   - Keep the window open while playing.
   - Already running `wiiuse.exe` from your own `Desktop\Balance Board\Balance Board` folder? That also works.
     The game reads whichever `WiiMote1_All.txt` was written most recently (`SensorSettings` → `Cop Live File`).

**C2 – BioRadio EMG → `Tools/EmgBridge/`**
1. Turn on the BioRadio. Electrodes: **Ch1 = forearm flexor (jump)**, **Ch2 = biceps (backflip)**. Close BioCapture.
2. `BioRadioSDK.dll` is found automatically if the kit is in
   `Downloads\BioRadio_Student_Unity_Kit\BioRadio_Student_Unity_Kit\Python_Bridge\`.
   Otherwise, copy `BioRadioSDK.dll` into `Tools/EmgBridge/`. It is git-ignored, so it is never uploaded.
3. Double-click **`Tools/EmgBridge/run_bridge.bat`**. The first run installs `pythonnet` and `matplotlib`.
   Click **Scan**, choose your BioRadio, then **Connect**. Keep the window open.
   - **Start Recording / Stop Recording** saves the raw EMG as a CSV (plus an `_info.json`) to the kit's `recordings` folder.
   - **Alternative:** `Tools/EmgBridge/kit_bridge/run_kit_bridge.bat` runs the BioRadio kit's own bridge
     (`bioradio_unity_bridge.py`, our copy with CSV recording added). The game accepts it too, but it only sends
     the RMS of each 50 ms batch, so detection is less clean. **Run only one bridge at a time**, because both use UDP port 50557.

**C3 – In Unity**
1. Press **Play**. The bottom-left status bar should show **LIVE · EMG ● bridge · COP ● board**. Green means data is arriving.
   (If it says REPLAY, press **F6**.)
2. Step on the board and **stand still for 2 s**. This measures your centre. Press **F3** to redo it.
3. Press **F4** and follow the prompts (relax / squeeze forearm / curl biceps, about 16 s). This is the EMG calibration. It is saved per laptop. Do it again for each new player.
4. Press **Enter** to start: **lean left/right** to steer, **lean forward/back** for speed,
   **squeeze your forearm** to jump, **curl your biceps** in the air to backflip.
5. **F2** opens the sensor panel: live COP, EMG envelopes and thresholds, and a scrolling plot.

### E. Play it as a normal PC game (`.exe`, no Unity needed)

Unity is only needed once, to **build** the game. After that anyone can play by double-clicking an `.exe`,
including live with the sensors.

**E1 – Build the `.exe` (once, or again after any change)**
1. Open the project in Unity (step A).
2. **File → Build Profiles** → select **Windows** on the left. If it is greyed out, click
   **Install with Unity Hub** to add *Windows Build Support*.
3. Check that **Scene List** shows `Assets/Scenes/SkiGame.unity` with a tick.
4. Click **Build**. Make a new empty folder, e.g. `downhill-rush\Build`, and select it. This takes 1–3 minutes.
5. The folder now contains **`SSRP Game project.exe`**, `SSRP Game project_Data\`, `UnityPlayer.dll` and more.
   **Keep all of these together.** The `.exe` won't run on its own. To give the game to someone, zip the whole folder.
6. **For live board input:** copy the whole `Tools\WiiBoard` folder into the build folder, so you get
   `Build\Tools\WiiBoard\start_board.bat`. The `.exe` reads the board file from `Tools\WiiBoard` next to itself.
   (Or run `wiiuse.exe` from `Desktop\Balance Board\Balance Board`, because the game also looks there.)

`Build/` is git-ignored, so the build is never uploaded. Build again on each laptop, or share the zip.

**E2 – Play the `.exe` with keyboard / recordings**
- Double-click **`SSRP Game project.exe`**. It opens full-screen. Press **Alt+Enter** to switch to windowed mode.
- Same keys as in Unity: **Enter** start, **A/D** steer, **W/S** speed, **Space** jump, **F** backflip,
  **Esc/P** pause, **R** restart, **M** menu. Close with **Alt+F4**.
- **F6** switches to **REPLAY** (the recordings are built into the `.exe`).

**E3 – Play the `.exe` LIVE with the sensors**. This is the same order as in Unity:
1. Board: `Build\Tools\WiiBoard\start_board.bat` (pair first with `WiiBalanceWalker.exe` if this laptop is new, see C1).
2. EMG: `Tools\EmgBridge\run_bridge.bat` from the repo → **Scan → Connect**.
3. Start **`SSRP Game project.exe`**. If Windows Firewall asks about it, click **Allow** (the EMG arrives over a local network port).
4. Press **Alt+Enter** for windowed mode if you want to watch the bridge window too. The game keeps running
   when you click another window (*Run In Background* is on).
5. The bottom-left status bar must show **LIVE · EMG ● bridge · COP ● board**, both green.
6. Stand still on the board for **2 s** (centre). Press **F4** and follow the prompts (EMG calibration, about 16 s).
   The `.exe` stores its own calibration, separate from the Unity editor, so do F4 once in the `.exe` too.
7. Press **Enter** and play. **F2** shows the live sensor panel.

---

### F. Every key and how live control works

| Key | In the menu / game |
|---|---|
| **Enter** | start the run (menu) / restart (game over) |
| **A / D**, **← / →** | steer (also works in LIVE mode as a backup) |
| **W / S**, **↑ / ↓** | speed up / slow down (backup in LIVE) |
| **Space** | jump (backup in LIVE) |
| **F** | backflip while airborne (backup in LIVE) |
| **Esc / P** | pause / resume |
| **R / M** | restart / main menu (from pause or game over) |
| **F1** | input debug HUD (Steer, Speed, Jump, Backflip values) |
| **F2** | sensor panel: live COP dot, EMG envelopes with ON/OFF thresholds, scrolling plot |
| **F3** | re-measure your standing centre on the board (stand still for 2 s) |
| **F4** | EMG calibration: relax → squeeze forearm → curl biceps, following the on-screen prompts |
| **F6** | switch **LIVE ↔ REPLAY** |
| **Alt+Enter** | full-screen ↔ window (`.exe` only) |

**What your body does in LIVE mode**

| Body | Game | Signal path |
|---|---|---|
| Lean **left / right** on the board | steer | COP x → 4 Hz low-pass → dead zone 1 cm → full turn at 12 cm |
| Lean **forward / back** | faster / slower | COP y, same filter |
| **Squeeze forearm** (Ch1) | jump | EMG 20–450 Hz band-pass → rectify → 5 Hz envelope → threshold, 0.6 s refractory |
| **Curl biceps** (Ch2) while in the air | backflip | same EMG chain on Ch2 |

Tips: keep the arm relaxed between flexes, because one clear squeeze gives one jump. Step off and on again, then press **F3**,
if steering drifts. The detection delay is about 50–70 ms, so flex slightly before an obstacle.

### G. If something doesn't work

| Symptom | Fix |
|---|---|
| `COP ●` stays red / "File not found" | `start_board.bat` isn't running, or the board isn't connected. Check that `Tools/WiiBoard/WiiMote1_All.txt` is growing. |
| wiiuse: "No wiimotes found" | Pair again with WiiBalanceWalker (C1.1), close it, press SYNC, then run `start_board.bat` again. |
| Steering is reversed | `SensorSettings` → tick **Live Invert X** (forward/back: **Live Invert Y**). |
| Steering is too weak or too strong | `SensorSettings` → **Steer Full Scale Cm** (smaller = more sensitive) / **Steer Dead Zone Cm**. |
| Steering style | `Assets/Settings/SkierSettings` → **Steer Mode**: `TurnRate` (lean = turning speed) or `LeanSetsHeading` (lean = direction, centre = straight). |
| `EMG ●` red | The bridge window is closed or not connected. Or `BioRadioSDK.dll` wasn't found: the bridge prints where it looked. |
| Jumps fire on their own / never fire | Press **F4** to calibrate again, and check the electrode placement. |
| Test the live paths without hardware | `Tools/EmgBridge/run_bridge_replay_test.bat` streams the recorded EMG through UDP. `SensorSettings` → **Cop Live Self Test** does the same for the board. |
| `.exe`: COP red | `Tools\WiiBoard` wasn't copied next to the `.exe` (E1.6), or `start_board.bat` was run from somewhere else. |
| `.exe`: EMG red | The bridge isn't connected, or Windows Firewall blocked the game. Allow it in *Windows Security → Firewall → Allow an app*. |
| `.exe` won't start / "missing UnityPlayer.dll" | It was copied without its folder. Keep the whole build folder together. |
| `.exe` game freezes when another window is clicked | Old build: build again (Run In Background is now on). |
| Speed limit | `Assets/Settings/SkierSettings` → **Max Speed** in m/s (km/h = m/s × 3.6). |

### Exactly what we used in the last live session

| Part | What ran | In the repo |
|---|---|---|
| Wii Balance Board pairing | `WiiBalanceWalker.exe` v0.5 (Add/Remove bluetooth Wii device) | `Tools/WiiBoard/WiiBalanceWalker.exe` + DLLs |
| Board → file | `wiiuse.exe` writing `WiiMote1_All.txt` (columns: 4 = COP X, 5 = COP Y, 6 = weight kg) | `Tools/WiiBoard/wiiuse.exe`, `wiiuse.dll`, `msvcr100d.dll`, `start_board.bat` |
| BioRadio → game | `ski_emg_bridge.py` via `run_bridge.bat` (every sample, UDP 127.0.0.1:50557) | `Tools/EmgBridge/` |
| BioRadio → game (kit version) | `bioradio_unity_bridge.py` from the BioRadio Student Unity Kit, with our CSV recording | `Tools/EmgBridge/kit_bridge/` |
| BioRadio SDK | `BioRadioSDK.dll` from the kit's `Python_Bridge` folder | **not included** (vendor licence). See C2. |
| Python packages | `pythonnet`, `matplotlib` | installed automatically by the `.bat` files |
| Game settings | sensor settings in code defaults (`SensorSettings.cs`) + `Assets/Resources/SensorSettings.asset`; skier tuning in `Assets/Settings/SkierSettings.asset` | yes, committed exactly as used |

**Settings used live** (change them in the Inspector; the asset files in the repo are what we played with):

| Setting | Value | File |
|---|---|---|
| COP processing rate / low-pass | 200 Hz / 4 Hz 2nd-order Butterworth | `SensorSettings.cs` |
| Steer dead zone / full scale / curve | 1.0 cm / 12 cm / 1.0 (linear) | `SensorSettings.cs` |
| Live COP file | `Tools/WiiBoard/WiiMote1_All.txt` (or the newest one in `Desktop\Balance Board\Balance Board`) | `SensorSettings.cs` |
| EMG band-pass / envelope | 20–450 Hz 4th-order Butterworth, rectify, 5 Hz low-pass | `SensorSettings.cs` (applied in `EmgProcessor.cs`), see `SENSORS.md` |
| EMG channels | Ch1 = forearm → jump, Ch2 = biceps → backflip | `SensorSettings` → Bridge Jump / Backflip Channel |
| Steer mode | `TurnRate` (turn rate 65°/s). `LeanSetsHeading` is the alternative. | `Assets/Settings/SkierSettings.asset` |
| Speeds | start 14 m/s, min 8 m/s, max 22.22 m/s (80 km/h) | `Assets/Settings/SkierSettings.asset` |

The EMG calibration from **F4** is stored per laptop in Unity PlayerPrefs, not in the repo. Every new laptop or player needs one F4 run (16 s).

### Recordings

| File | What |
|---|---|
| `Recordings/EMG/<name>/*.csv` | raw BioRadio EMG of each member (`sample,time_s,ch1_mv,ch2_mv`, 1000 Hz). `john`, `aman`, `vignesh` = calibration recordings. `prakash` = live session recording. The `.json` next to each file holds device, rate and units. |
| `Recordings/COP/John14.txt` | Wii Balance Board Recorder file (5 Hz) used for the COP filter analysis |
| `Assets/StreamingAssets/john_final.csv`, `John14.txt` | the copies the game replays with **F6** |

To replay another member's EMG, use one of these:
- In the game: `SensorSettings` → **Emg Source = Replay**, **Emg Replay File = `Recordings/EMG/aman/aman_final.csv`**.
- Through the real UDP path:
  ```bat
  cd Tools\EmgBridge
  py ski_emg_bridge.py --replay "..\..\Recordings\EMG\aman\aman_final.csv"
  ```
  `cd` changes into the bridge folder. `py` runs Python. `--replay <file>` streams that CSV at real speed to UDP port 50557, instead of reading a BioRadio.

### What is where

| Path | What |
|---|---|
| `Assets/Scenes/SkiGame.unity` | the game scene |
| `Assets/Scripts/Input/Sensors/` | live sensor input: COP and EMG filters, UDP bridge receiver, file tailer, debug HUD |
| `Assets/Scripts/Input/Biosignal/` | EMG filter blocks (biquads, detector) and recording replay |
| `Assets/Resources/SensorSettings.asset` | all sensor settings (sources, files, filters, thresholds) |
| `Assets/Settings/*.asset` | gameplay tuning (skier, slope, difficulty, spawns, visuals) |
| `Assets/StreamingAssets/` | recordings used by REPLAY (`john_final.csv` EMG, `John14.txt` COP) |
| `Recordings/` | all team EMG recordings + the COP recording |
| `Tools/WiiBoard/` | board tools: `start_board.bat`, `wiiuse.exe` (+ DLLs), `WiiBalanceWalker.exe` for pairing |
| `Tools/EmgBridge/` | BioRadio → game bridge: `run_bridge.bat`, `ski_emg_bridge.py`, replay test, `kit_bridge/` (the kit's bridge) |
| `Tools/SignalAnalysis/`, `Tools/emg_analysis.py` | offline filter analysis scripts used for the report |
| `Tools/save_to_github.bat` | add + commit + push the current branch in one double-click |
| `Build/` (you create it, git-ignored) | the `.exe` build from step E |
| `SENSORS.md` / `CONTROLS.md` | filter design and signal analysis / control mapping and timing |

---

> **Sensor control (EMG + Wii Balance Board):** see [SENSORS.md](SENSORS.md) for the filters, the signal analysis and how to switch between replay and live input.
>
> **Controls reference:** see [CONTROLS.md](CONTROLS.md) for steering math, all timing/delay values, and how EMG and the Wii Balance Board map onto the controls.

## 1. How to open, run and play

### Open
1. Open **Unity Hub** → **Add** → **Add project from disk** → choose the folder
   `downhill-rush` folder you cloned.
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

## Development history

What was added, newest first. **All of it is in `main`.**

### Final version: sensor tools, recordings and PC instructions
* **Live board tools in the repo:** `Tools/WiiBoard/` holds `wiiuse.exe` (+ DLLs), `WiiBalanceWalker.exe` for pairing, and `start_board.bat`.
  The game reads `Tools/WiiBoard/WiiMote1_All.txt` by default and falls back to the newest `WiiMote1_All.txt` in the usual `Balance Board` desktop folders.
* **Steering:** new `Steer Mode` in `SkierSettings`: `LeanSetsHeading` (lean = direction, centre = straight) or `TurnRate` (lean = turning speed).
  COP steering is more responsive: 200 Hz processing, 4 Hz low-pass, 1 cm dead zone, 12 cm full scale, linear response.
* **EMG bridge:** Start/Stop Recording button (CSV + info JSON). `run_bridge.bat` installs `pythonnet` and `matplotlib` on the first run.
* `Tools/save_to_github.bat` (add, commit and push in one click). README: complete setup and live-play instructions.
* README section E: build and play as a normal Windows `.exe` (keyboard, replay and live). Section F: every key and the body → game mapping. *Run In Background* turned on so the game keeps running while you use the bridge window.
* Added everything else used live: the kit's EMG bridge (`Tools/EmgBridge/kit_bridge/`) and all team EMG recordings plus the COP recording (`Recordings/`).

### Live EMG + balance-board control
* Includes John's EMG demo input, the recordings in `StreamingAssets`, and the 80 km/h speed cap.
* **Live EMG** from the BioRadio through `Tools/EmgBridge/ski_emg_bridge.py`, over UDP 50557.
  * It is based on the kit's bridge, but streams every sample.
  * The kit's original bridge also works.
* **EMG filters:**
  * 20–450 Hz Butterworth band-pass, 50 Hz notch, rectify, then a 5 Hz envelope (John's envelope cut-off).
  * Per-muscle thresholds: jump 0.15 / 0.08, backflip 0.40 / 0.10.
  * On the recording: jump 63/63 detected with 0 false, 29 ms after the contraction starts; backflip 57/57 with 0 false.
* **COP filters** (before this, steering was keyboard only):
  * weight gate and glitch rejection;
  * 3 Hz Butterworth low-pass;
  * neutral calibration and dead zones.
  * The live `WiiMote1_All.txt` is followed read-only.
* Guided EMG calibration (**F4**), saved per computer. "Stand still" centring for the board.
* Always-on status bar. **F6** switches LIVE ↔ REPLAY. Self-test modes check the live paths without hardware.

### Steering tuning and EMG demo
* Steering tuned to 80 °/s, with 0.861 air steer. Added `CONTROLS.md`.
* John: EMG demo input (keys trigger real recorded contractions through the filters) and an 80 km/h cap.

### Gradual steering and ramp fix
* **Gradual steering:** the steer input sets the turn *rate* (`SkierSettings.steerTurnRate`,
  default 55 deg/s). Holding right keeps turning right; letting go keeps the current
  direction (no automatic track-following). The heading is limited to `maxHeadingAngle`
  (60 deg) from straight downhill, and the soft side banks still push you back.
* **Ramp jumps capped:** jumping on a ramp, or right after leaving one, adds only
  `rampJumpBonus` (3 m/s) on top of the ramp launch instead of a full extra jump.

### First prototype fixes
* Ramps launch reliably at any frame rate (launch is frame-based; ground snap is capped).
* Performance: decoration in each slope chunk is merged into a few meshes, GPU instancing,
  2 shadow cascades, 2x MSAA. In the editor, set the Game view to 1920x1080 for best FPS.
  Tick `PlayerController > Log Airtime` to print every take-off / landing to the Console.
* Fixed: obstacles were briefly at the world origin when spawned, which could cost a life
  right at the start of a run.
