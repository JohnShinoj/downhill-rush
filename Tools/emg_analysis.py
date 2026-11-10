"""
EMG analysis for the ski game  ->  thresholds for BiosignalInput (Unity).

Usage:   python emg_analysis.py path/to/recording.csv  [--cal 20]
CSV format: sample,time_s,ch1_mv,ch2_mv   (BioRadio export, 1000 Hz)

Pipeline (identical to EmgChannelDetector.cs, all CAUSAL = usable in real time):
  1. Band-pass 20-450 Hz, 4th-order Butterworth  (removes drift/motion artefact and HF noise)
  2. Full-wave rectification
  3. Low-pass 5 Hz, 2nd-order Butterworth        (linear envelope)
  4. Thresholds from the first --cal seconds:  rest = 20th pct, max = 95th pct
        ON  = rest + 0.30*(max-rest),  OFF = rest + 0.12*(max-rest),  refractory 0.6 s
It prints the thresholds to paste into Unity, the number of detected contractions,
the detection delay vs. an offline (zero-phase) reference, and saves a plot.
"""
import argparse, numpy as np, pandas as pd, matplotlib
matplotlib.use("Agg"); import matplotlib.pyplot as plt
from scipy import signal

FS = 1000
BP = signal.butter(4, [20, 450], "bandpass", fs=FS, output="sos")
LP = signal.butter(2, 5, fs=FS, output="sos")
ON_FRAC, OFF_FRAC, REFRACTORY = 0.30, 0.12, 0.6

def envelope(x, causal=True):
    f = signal.sosfilt if causal else signal.sosfiltfilt
    return f(LP, np.abs(f(BP, x)))

def detect(env, t, on, off):
    active, last, ev = False, -1e9, []
    for i, v in enumerate(env):
        if t[i] < 1.0: continue                      # filter warm-up
        if not active and v > on and t[i] - last > REFRACTORY:
            active, last = True, t[i]; ev.append(t[i])
        elif active and v < off:
            active = False
    return np.array(ev)

def main():
    ap = argparse.ArgumentParser(); ap.add_argument("csv"); ap.add_argument("--cal", type=float, default=20)
    a = ap.parse_args()
    d = pd.read_csv(a.csv); t = d.time_s.values
    fig, axes = plt.subplots(2, 1, figsize=(12, 6), sharex=True)
    for ax, (col, name) in zip(axes, [("ch1_mv", "jump (Ch1 forearm)"), ("ch2_mv", "backflip (Ch2 biceps)")]):
        x = d[col].values
        env = envelope(x)
        cal = env[(t > 1) & (t < a.cal)]
        rest, mx = np.percentile(cal, 20), np.percentile(cal, 95)
        on, off = rest + ON_FRAC * (mx - rest), rest + OFF_FRAC * (mx - rest)
        ev = detect(env, t, on, off)
        ref = detect(envelope(x, causal=False), t, on, off)
        lat = [ev[(ev >= r - 0.2) & (ev <= r + 0.5)][0] - r for r in ref if np.any((ev >= r - 0.2) & (ev <= r + 0.5))]
        print(f"{name}:  ON = {on:.4f} mV   OFF = {off:.4f} mV   (rest {rest:.4f}, max {mx:.4f})")
        print(f"   real-time detections: {len(ev)}   offline reference: {len(ref)}   "
              f"delay median {1000*np.median(lat):.0f} ms, 90th pct {1000*np.percentile(lat, 90):.0f} ms")
        ax.plot(t, env, lw=1, label="causal envelope")
        ax.axhline(on, ls="--", c="k", lw=1, label="ON"); ax.axhline(off, ls=":", c="gray", lw=1, label="OFF")
        ax.plot(ev, np.full(len(ev), env.max() * 1.05), "v", label="detections")
        ax.set_title(name, loc="left"); ax.set_ylabel("mV"); ax.legend(loc="upper right", frameon=False)
    axes[-1].set_xlabel("time (s)"); fig.tight_layout()
    out = a.csv.rsplit(".", 1)[0] + "_emg_analysis.png"; fig.savefig(out, dpi=120); print("plot saved:", out)

if __name__ == "__main__":
    main()
