# Sensor control: EMG and Wii Balance Board

This document covers how the game is controlled by the body instead of the keyboard:

| Body signal | Sensor | Game control |
|---|---|---|
| Forearm flexor (FCU) contraction | EMG, BioRadio **Ch1** | **Jump** |
| Biceps contraction | EMG, BioRadio **Ch2** | **Backflip** (only while in the air) |
| Lean left / right | Wii Balance Board centre of pressure (COP), medio-lateral | **Steering** |
| Lean forward / back | Wii Balance Board COP, antero-posterior | **Speed** (forward = faster, back = brake) |

It covers which filters are used and why, the numbers from analysing the recordings, how replay and live mode work, and how to tune everything. All filters run **causally**: they only use past samples, exactly as in real time. The analysis therefore measures the delays the player will actually feel.

The figures are in `Docs/figures/`. The analysis script is `Tools/SignalAnalysis/sensor_analysis.py`. It uses the same filter maths as the game, so you can re-run it on new recordings.

---

## 1. The data we analysed

| File | What it is | Rate | Length |
|---|---|---|---|
| `john_final.csv` (+ `.json`) | BioRadio EMG, 2 channels, in mV | 1000 Hz | 131.8 s, 63 Ch1 contractions + 57 Ch2 contractions, alternating every ~1 s |
| `John14.txt`, `John15.txt`, `John16.txt` | Wii Balance Board Recorder export: Time (s), CpX, CpY (cm), 4 corner forces (N), Weight (kg), raw counts | 5 Hz | 111 s, 74 s, 63 s. Leans left/right, then forward/back |
| `WiiMote1_All.txt` (live, 886 MB) | Written continuously by the board software. We read only its last 5,000 lines into `Data/live_sample.txt`. | **≈ 85 lines/s** | 10 tab-separated columns |

Format of the live file: column 4 = COP X, column 5 = COP Y, column 6 = total weight (kg), columns 7–9 = clock time (hh mm ss). This matches your professor's script, which reads columns 4 and 5. The sample was taken with nobody on the board: weight was 2.6–2.8 kg, and the COP stayed fixed at 26.05 / 16.75.

---

## 2. EMG: jump and backflip

### 2.1 What the recording showed

| Measurement | Ch1 (jump) | Ch2 (backflip) | Consequence |
|---|---|---|---|
| Power below 20 Hz (movement artefact, drift) | **32 %** | **50 %** | A high-pass at 20 Hz is essential. Without it, every arm movement would look like a contraction. |
| Median frequency of the EMG band | 83 Hz | 57 Hz | Normal surface EMG, well inside 20–450 Hz |
| 50 / 100 / 150 Hz mains peaks (vs. neighbouring frequencies) | 1.7× / 1.2× / 1.5× | 1.5× / 1.1× / 1.6× | Very little mains hum in this lab. The 50 Hz notch is kept as cheap insurance for other rooms. |
| DC offset | −0.05 mV | −0.79 mV | The high-pass removes it. The filter is primed with the first sample so start-up doesn't fire a false jump. |
| Envelope at rest / during contraction | 0.0047 / 0.233 mV | 0.0037 / 0.588 mV | Large signal-to-noise ratio, so a threshold works well |
| Small activity before each Ch2 contraction | — | up to ~25 % of a full contraction | **Ch2 needs a higher ON threshold (0.40)** than Ch1 (0.30) |

### 2.2 The EMG filter chain, run on every sample at 1000 Hz

```
raw EMG (mV)
 1. High-pass  : 4th-order Butterworth, 20 Hz     (two biquads, Q = 0.5412 and 1.3066)
 2. Low-pass   : 4th-order Butterworth, 450 Hz    (two biquads, Q = 0.5412 and 1.3066)
 3. Notch      : 50 Hz, Q = 30                     (≈ 1.7 Hz wide)
 4. Rectify    : full-wave, |x|
 5. Envelope   : 2nd-order Butterworth low-pass, 8 Hz   (Q = 0.7071)
 6. Normalise  : n = (envelope − rest) / (contraction − rest)    rest = 10th, contraction = 99th percentile
 7. Detect     : hysteresis. ON when n > ON threshold for at least 30 ms; OFF when n < 0.15
 8. Game       : ON edge → one jump / backflip (cooldown 0.25 s / 0.30 s, 0.13 s input buffer, 0.12 s coyote time)
```

All filters are IIR biquads designed with the bilinear transform (RBJ cookbook formulas), so cascading them gives exact Butterworth responses. See `emg_filters.png`.

### 2.3 How the settings were chosen

The detector was run on the recording with every combination of:

- envelope cut-off: 4, 6, 8 and 10 Hz;
- ON threshold: 0.2, 0.3 and 0.4;
- OFF threshold: 0.10 and 0.15;
- minimum ON time: 30 and 50 ms.

Each run was scored against the contractions found offline: a zero-phase (non-causal) envelope, peaks with prominence above 40 % of the range.

- **8 Hz envelope** had the shortest delay with no errors. 4–6 Hz was 15–40 ms slower; 10 Hz was not more accurate but was noisier.
- **Ch1 ON = 0.30**:
  - At 0.20 there were 2–4 false triggers; at 0.40 detection was 40 ms slower.
  - At 0.30: **63 / 63 detected, 0 false**.
- **Ch2 ON = 0.40**:
  - At 0.30 there were 8–10 false triggers from the small activity before each contraction.
  - At 0.40: **57 / 57 detected, 2 false**. One false trigger comes 0.28 s after a real one, so the game's 0.30 s backflip cooldown absorbs it. The other remains: 1 extra in 132 s, and it does nothing while on the ground.
- **OFF = 0.15, minimum ON time = 30 ms**: this rejects single spikes and keeps one trigger per contraction.

As a realistic test, I calibrated on the **first 30 s only** and tested on the remaining 100 s:

| | Contractions | Detected | Missed | False | Delay after onset* | Fires before the peak |
|---|---|---|---|---|---|---|
| Jump (Ch1) | 49 | **49** | 0 | 0 | 90 ms | 107 ms before |
| Backflip (Ch2) | 42 | **42** | 0 | 2 | 159 ms | 111 ms before |

\* "Onset" is where the offline envelope reaches 30 % of that contraction's peak. In both cases the game reacts **before the contraction reaches its peak**.

`emg_detection.png` shows the envelope, the thresholds and the detected activity for 12 s of the recording.

### 2.4 Calibration

- **Replay:** before play starts, the first 30 s of the file are filtered and the 10th and 99th percentiles of each envelope are used as "rest" and "contraction".
- **Live:** for the first 30 s the player should rest, then make a few strong contractions of each muscle. The panel shows CALIBRATING %. Until it finishes, the values measured from `john_final.csv` are used. Press **F4** to recalibrate at any time.
- If no clear contraction is found (peak less than 3× rest), the old values are kept and the panel says so.

---

## 3. COP (Wii Balance Board): steering and speed

### 3.1 What the recordings showed

| Measurement | John14 | John15 | John16 | Consequence |
|---|---|---|---|---|
| Neutral stance (X / Y cm) | −1.0 / −1.6 | −0.8 / −1.1 | −1.7 / −1.4 | Not zero and different every time, so **measure neutral at the start** |
| Sway while standing still, SD (cm) | 0.8 / 0.6 | 0.1 / 0.3 | 0.15 / 0.3 | **Dead zone 0.8 cm** on steering |
| Sample-to-sample noise (cm) | 1.0 / 0.4 | 0.24 / 0.25 | 0.35 / 0.22 | Needs a low-pass |
| Typical full lean, 95th percentile (cm) | 12.7 / 7.2 | 13.3 / 8.0 | 14.3 / 7.0 | **Full steer at 10 cm**, full speed at 6 cm |
| Fastest lean change (cm/s) | 57 | 20 | 26 | Voluntary leans are slow (< ~2 Hz), so a 3 Hz low-pass keeps them |
| Cross-talk | Leaning sideways moves Y back by 1.5–2 cm. Leaning forward/back moves X by up to ±3–4 cm. | | | **Dead zone 1.8 cm on speed**, so sideways leans don't brake |

### 3.2 The COP filter chain, run on an internal 100 Hz clock

```
raw COP X, Y (cm), weight (kg)
 1. Resample    : onto a fixed 100 Hz clock (replay: linear interpolation of the 5 Hz file;
                  live: the ~85 Hz lines that arrived this frame are spread over this frame's ticks)
 2. Validity    : NaN or "-1.#IND00" rejected. Weight < 20 kg = nobody on the board → output glides to neutral.
                  Live file silent for > 0.5 s → neutral.
 3. Glitches    : a jump of > 6 cm in one sample is ignored (at most 3 in a row)
 4. Low-pass    : 2nd-order Butterworth, 3 Hz (Q = 0.7071), one per axis
 5. Neutral     : subtract the mean of the first 2 s with someone on the board (F3 re-measures it)
 6. Scale       : steer = X / 10 cm, speed = Y / 6 cm, clamped to −1..+1
 7. Game        : dead zone 0.08 (steer) / 0.30 (speed), ramp 0.05 s (steer) / 0.15 s (speed),
                  then steering turns the skier at up to 80 °/s (see CONTROLS.md)
```

`cop_processing.png` shows raw COP against the game input for all three recordings. `cop_filter.png` shows the filter's response.

### 3.3 Why these values

- **3 Hz low-pass, 2nd-order Butterworth:**
  - It passes the voluntary leans unchanged, removes sway jitter and the occasional noisy sample, and delays the signal by only **77 ms** (group delay at 0.5 Hz).
  - A lower cut-off (1–2 Hz) would feel laggy. A higher one passes more sway into the steering.
- **100 Hz internal clock:** the same filter then behaves the same on the 5 Hz recordings and the 85 Hz live file.
- **Neutral measured each session:** the stance centre varied by 1 cm between recordings.
- **10 cm full scale:** people reached 12–14 cm, so full steering doesn't require a maximum lean. Steering was at full lock 20–26 % of the time in these recordings.
- **Game-side ramp reduced from 0.12 s to 0.05 s:** the 3 Hz low-pass already smooths, so stacking both would add needless lag.

### 3.4 Live file: how it is read (read-only)

- `WiiMote1_All.txt` is opened with **read access only**, sharing read/write/delete with the program writing it. The game never writes, renames or locks it.
- Reading starts at the **end** of the file, so the 886 MB of history is skipped. Each frame only the newly added bytes are read, about 85 lines/s × ~75 bytes ≈ 6 KB/s.
- If the file is restarted or truncated, the reader starts again from the beginning. If the game falls more than 256 KB behind, it jumps close to the end.
- **Left/right sign:** your professor's script draws the ball at −X, so +X in the live file means **left**. `liveInvertX` is therefore on. This has not been checked with someone on the board yet; see section 6.

---

## 4. Total delay from the body to the game

| Stage | EMG jump / backflip | COP steering |
|---|---|---|
| Sensor / software / file | BioRadio + export (unknown) | ~12 ms (85 Hz file) |
| Filtering | band-pass ≈ 2–5 ms + envelope 29 ms | low-pass 77 ms |
| Detection | minimum ON time 30 ms + threshold crossing | dead zone + 0.05 s ramp (≈ 25 ms average) |
| Game frame | ≤ 17 ms | ≤ 17 ms |
| **Measured** | **90 ms (jump) / 159 ms (backflip) after the contraction starts**, but before its peak | **≈ 120–130 ms** to follow a lean |

The game's 0.12 s coyote time and 0.13 s input buffer cover most of the EMG delay: a jump triggered slightly after going over a ramp lip still counts.

---

## 5. How to use it in Unity

All settings are in **`Assets/Resources/SensorSettings.asset`**. Click it in the Project window and edit it in the Inspector.

| What you want | Set |
|---|---|
| Test with the recordings (now) | `Cop Source = Replay`, `Emg Source = Replay` (default) |
| Live balance board | `Cop Source = LiveFile`, `Cop Live File = C:\Users\praka\OneDrive\Desktop\Balance Board\Balance Board\WiiMote1_All.txt` |
| Live EMG | `Emg Source = LiveFile` and `Emg Live File` = a text/CSV file your EMG software keeps appending to (same columns as `john_final.csv`) |
| Keyboard only | untick `Use Sensors` |
| Another COP recording | `Cop Replay File = SensorData/COP/John14.txt` (paths are relative to the project folder) |

The recordings are in `D:\SSRP Game project\SensorData\`. They are **not** uploaded to GitHub, because the repository is public and these are personal physiological recordings.

**In game:**

| Key | Does |
|---|---|
| **F2** | Sensor panel: COP raw / filtered / neutral, weight, steer and speed bars, both EMG envelopes against their thresholds, a 6 s scrolling plot |
| **F3** | Re-measure the neutral stance (stand still for 2 s) |
| **F4** | Recalibrate EMG (30 s: rest, then a few strong contractions) |
| F1 | The general input panel (smoothed values, triggers, buffers) |

The keyboard still works alongside the sensors (`Keyboard Also Works`), which is handy for testing.

**About the replay:** the EMG recording alternates the two muscles about every 1 s, and a normal jump lasts 0.8 s. So in replay most biceps contractions arrive after the skier has already landed, and backflips only happen on ramp jumps, which last about 1.2 s. In real play the player does them in sequence, so this is expected.

---

## 6. Still to confirm (needs someone on the board or the EMG setup)

1. **Which EMG channel is which muscle.** I assumed Ch1 = FCU (jump) and Ch2 = biceps (backflip). Swap `Jump Column` and `Backflip Column` (2 = ch1, 3 = ch2) if it's the other way round.
2. **Live COP direction and scale.** Stand on the board with the live file running and open F2.
   - Lean right: Steer should go **+**. If it goes −, untick `Live Invert X`.
   - Lean forward: Speed should go **+**. If it goes −, tick `Live Invert Y`.
   - If a comfortable lean doesn't reach ±1, lower `Steer Full Scale Cm`. The live software may use a different cm scale from the Recorder.
3. **Live EMG.** Your professor only provided the COP file. If the EMG software can also write a growing text file, point `Emg Live File` at it and set the sample rate and columns.

---

## 7. Every parameter (defaults)

| Group | Parameter | Value |
|---|---|---|
| EMG | high-pass / low-pass / notch | 20 Hz BW4 / 450 Hz BW4 / 50 Hz Q30 |
| EMG | envelope | rectify + 8 Hz BW2 |
| EMG | jump ON / OFF | 0.30 / 0.15 |
| EMG | backflip ON / OFF | 0.40 / 0.15 |
| EMG | minimum ON time | 30 ms |
| EMG | calibration | first 30 s, 10th / 99th percentile |
| COP | processing rate | 100 Hz |
| COP | low-pass | 3 Hz BW2 |
| COP | glitch limit | 6 cm per sample (max 3 in a row) |
| COP | minimum weight | 20 kg |
| COP | stale timeout | 0.5 s |
| COP | neutral | mean of the first 2 s |
| COP | full scale | steer 10 cm, speed 6 cm |
| Game | steer dead zone / ramp / release | 0.08 / 0.05 s / 0.04 s |
| Game | speed dead zone / ramp | 0.30 / 0.15 s |

Re-run the analysis on new recordings with:

```
python Tools/SignalAnalysis/sensor_analysis.py --emg SensorData/EMG/<file>.csv --cop SensorData/COP/<file>.txt --out Docs/figures
```
