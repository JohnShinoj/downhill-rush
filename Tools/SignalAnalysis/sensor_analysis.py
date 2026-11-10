"""
Offline analysis of the EMG and Wii Balance Board (COP) recordings used to choose the
real-time filters in the game. Every filter here is the SAME causal filter the game runs
(RBJ biquads = Butterworth sections), so the plots show exactly what the game sees.

Usage:
    python sensor_analysis.py --emg SensorData/EMG/john_final.csv --cop SensorData/COP/John16.txt --out Docs/figures

Needs: numpy, scipy, pandas, matplotlib
"""
import argparse, json, os
import numpy as np, pandas as pd
from scipy import signal
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

# ---------------------------------------------------------------- the game's filters
def rbj(kind, fs, f0, q):
    w = 2 * np.pi * f0 / fs; c = np.cos(w); al = np.sin(w) / (2 * q)
    b = {"lp": [(1 - c) / 2, 1 - c, (1 - c) / 2], "hp": [(1 + c) / 2, -(1 + c), (1 + c) / 2], "notch": [1, -2 * c, 1]}[kind]
    a = [1 + al, -2 * c, 1 - al]
    return np.array(b) / a[0], np.array(a) / a[0]

BW4 = (0.54119610, 1.30656296)
BW2 = 0.70710678

def emg_envelope(x, fs, hp=20, lp=450, notch=50, notch_q=30, env=8):
    """Causal: BW4 high-pass -> BW4 low-pass -> notch -> |x| -> BW2 low-pass."""
    sos = [rbj("hp", fs, hp, q) for q in BW4] + [rbj("lp", fs, lp, q) for q in BW4]
    if notch: sos.append(rbj("notch", fs, notch, notch_q))
    y = x - x[0]                       # same as priming the high-pass with the first sample
    for b, a in sos: y = signal.lfilter(b, a, y)
    return signal.lfilter(*rbj("lp", fs, env, BW2), np.abs(y)), y

def hysteresis(n, on, off, min_on):
    active = np.zeros(len(n), bool); st = False; cnt = 0; events = []
    for i, v in enumerate(n):
        if not st:
            cnt = cnt + 1 if v > on else 0
            if cnt >= min_on: st = True; events.append(i)
        elif v < off: st = False; cnt = 0
        active[i] = st
    return active, np.array(events)

def reference_bursts(x, fs):
    """Zero-phase (offline, non-causal) envelope used only as 'ground truth' contractions."""
    b, a = signal.butter(4, [20, 450], "bandpass", fs=fs)
    e = signal.filtfilt(*signal.butter(2, 6, fs=fs), np.abs(signal.filtfilt(b, a, x)))
    lo, hi = np.percentile(e, 10), np.percentile(e, 99)
    pk, _ = signal.find_peaks(e, prominence=0.4 * (hi - lo), distance=int(0.7 * fs))
    onsets = []
    for p in pk:
        lvl = lo + 0.3 * (e[p] - lo); i = p
        while i > 0 and e[i] > lvl: i -= 1
        onsets.append(i)
    return pk, np.array(onsets), e

def score(events, peaks, onsets, fs):
    used = set(); tp = 0; lat = []; lead = []
    for p, o in zip(peaks, onsets):
        m = [e for e in events if o - 0.3 * fs <= e <= p + 0.3 * fs and e not in used]
        if m: tp += 1; used.add(m[0]); lat.append((m[0] - o) / fs * 1000); lead.append((p - m[0]) / fs * 1000)
    return dict(true=len(peaks), detected=tp, missed=len(peaks) - tp, false=len(events) - len(used),
                latency_after_onset_ms=float(np.mean(lat)) if lat else None,
                before_peak_ms=float(np.mean(lead)) if lead else None)

def group_delay_ms(b, a, fs, f=1.0):
    w, gd = signal.group_delay((b, a), w=[f], fs=fs)
    return float(gd[0] / fs * 1000)

# ---------------------------------------------------------------- EMG
def analyse_emg(path, out, fs=1000.0, cal_s=30.0):
    d = pd.read_csv(path)
    chans = {"Jump (ch1, FCU)": (d["ch1_mv"].values, 0.30), "Backflip (ch2, biceps)": (d["ch2_mv"].values, 0.40)}
    report = {}
    fig, ax = plt.subplots(len(chans) + 1, 1, figsize=(14, 10))
    for k, (name, (x, on)) in enumerate(chans.items()):
        f, P = signal.welch(x - x.mean(), fs, nperseg=4096)
        tot = P.sum()
        bands = {"<20 Hz (artefact)": P[f < 20].sum() / tot, "20-450 Hz (EMG)": P[(f >= 20) & (f <= 450)].sum() / tot}
        f2, P2 = signal.welch(x - x.mean(), fs, nperseg=16384)
        mains = {}
        for c in (50, 100, 150):
            m = (f2 > c - 0.5) & (f2 < c + 0.5); nb = ((f2 > c - 6) & (f2 < c - 2)) | ((f2 > c + 2) & (f2 < c + 6))
            mains[f"{c} Hz"] = float(P2[m].max() / np.median(P2[nb]))
        env, band = emg_envelope(x, fs)
        n_cal = int(cal_s * fs)
        lo, hi = np.percentile(env[:n_cal], 10), np.percentile(env[:n_cal], 99)
        nrm = (env - lo) / (hi - lo)
        active, ev = hysteresis(nrm, on, 0.15, int(0.030 * fs))
        pk, ons, ref = reference_bursts(x, fs)
        sel = pk > n_cal
        sc = score(ev[ev > n_cal], pk[sel], ons[sel], fs)
        sc_all = score(ev, pk, ons, fs)
        m = (f >= 20) & (f <= 450)
        mf = f[m][np.searchsorted(np.cumsum(P[m]), P[m].sum() / 2)]   # median frequency of the EMG band
        report[name] = dict(power_fraction={k2: round(float(v) * 100, 1) for k2, v in bands.items()},
                            mains_line_to_neighbour_ratio={k2: round(v, 2) for k2, v in mains.items()},
                            median_frequency_hz=float(mf), calibration_rest_mv=float(lo), calibration_max_mv=float(hi),
                            on_threshold=on, off_threshold=0.15,
                            test_after_calibration=sc, whole_file=sc_all)
        t = np.arange(len(x)) / fs
        s = (t > 40) & (t < 52)
        a = ax[k]
        a.plot(t[s], nrm[s], lw=1, label="causal envelope (normalised)")
        a.axhline(on, ls="--", c="k", lw=0.8, label=f"ON {on}"); a.axhline(0.15, ls=":", c="k", lw=0.8, label="OFF 0.15")
        a.fill_between(t[s], 0, active[s] * 1.05, color="orange", alpha=0.25, label="active")
        for p in pk[(pk > 40 * fs) & (pk < 52 * fs)]: a.axvline(p / fs, c="g", lw=0.6)
        a.set_title(f"{name}: envelope vs thresholds (green = contraction peaks)"); a.set_ylim(-0.1, 1.6); a.legend(loc="upper right", fontsize=8)
        ax[-1].semilogy(f, P, lw=0.8, label=name)
    ax[-1].axvspan(20, 450, color="g", alpha=0.08, label="pass band 20-450 Hz")
    ax[-1].set_xlim(0, 500); ax[-1].set_xlabel("Hz"); ax[-1].set_title("Power spectrum (raw)"); ax[-1].legend(fontsize=8)
    plt.tight_layout(); plt.savefig(os.path.join(out, "emg_detection.png"), dpi=90); plt.close()

    # filter responses + delays
    sos = [rbj("hp", fs, 20, q) for q in BW4] + [rbj("lp", fs, 450, q) for q in BW4] + [rbj("notch", fs, 50, 30)]
    w = np.linspace(1, 499, 2000); H = np.ones_like(w, complex)
    for b, a in sos: H *= signal.freqz(b, a, worN=w, fs=fs)[1]
    be, ae = rbj("lp", fs, 8, BW2)
    He = signal.freqz(be, ae, worN=w, fs=fs)[1]
    fig, ax = plt.subplots(1, 2, figsize=(13, 4))
    ax[0].semilogx(w, 20 * np.log10(np.abs(H) + 1e-12)); ax[0].set_title("EMG band-pass 20-450 Hz + 50 Hz notch"); ax[0].set_ylim(-60, 5); ax[0].grid(True, which="both")
    ax[1].semilogx(w, 20 * np.log10(np.abs(He) + 1e-12)); ax[1].set_title("EMG envelope low-pass 8 Hz"); ax[1].set_ylim(-60, 5); ax[1].grid(True, which="both")
    for a in ax: a.set_xlabel("Hz"); a.set_ylabel("dB")
    plt.tight_layout(); plt.savefig(os.path.join(out, "emg_filters.png"), dpi=90); plt.close()
    report["envelope_group_delay_ms_at_1Hz"] = group_delay_ms(be, ae, fs)
    return report

# ---------------------------------------------------------------- COP
def analyse_cop(paths, out, rate=100.0, fc=3.0, full=10.0, speed_full=6.0):
    report = {}
    fig, ax = plt.subplots(len(paths), 1, figsize=(14, 3.6 * len(paths)), squeeze=False)
    b, a = rbj("lp", rate, fc, BW2)
    for k, p in enumerate(paths):
        d = np.loadtxt(p, skiprows=1); t, x, y = d[:, 0], d[:, 1], d[:, 2]
        q = t < 1.5
        tt = np.arange(0, t[-1], 1 / rate)
        xi, yi = np.interp(tt, t, x), np.interp(tt, t, y)        # the game interpolates replays onto 100 Hz
        zi = signal.lfilter_zi(b, a)
        xf = signal.lfilter(b, a, xi, zi=zi * xi[0])[0]; yf = signal.lfilter(b, a, yi, zi=zi * yi[0])[0]
        nx, ny = xf[tt < 2].mean(), yf[tt < 2].mean()
        steer = np.clip((xf - nx) / full, -1, 1); speed = np.clip((yf - ny) / speed_full, -1, 1)
        report[os.path.basename(p)] = dict(
            sample_rate_hz=float(1 / np.median(np.diff(t))), duration_s=float(t[-1]),
            stance_mean_cm=[round(float(x[q].mean()), 2), round(float(y[q].mean()), 2)],
            stance_sd_cm=[round(float(x[q].std()), 2), round(float(y[q].std()), 2)],
            noise_sd_cm=[round(float(np.std(np.diff(x, 2)) / np.sqrt(6)), 2), round(float(np.std(np.diff(y, 2)) / np.sqrt(6)), 2)],
            lean_p95_cm=[round(float(np.percentile(np.abs(x - x[q].mean()), 95)), 1), round(float(np.percentile(np.abs(y - y[q].mean()), 95)), 1)],
            max_lean_speed_cm_s=round(float(np.abs(np.diff(x)).max() / np.median(np.diff(t))), 1),
            time_at_full_steer_pct=round(float((np.abs(steer) >= 0.999).mean() * 100), 1))
        A = ax[k, 0]
        A.plot(t, x - nx, ".", ms=3, c="tab:blue", alpha=0.5, label="raw L/R (cm, minus neutral)")
        A.plot(tt, steer * full, c="tab:blue", label=f"steer x {full:g} cm (filtered, clipped)")
        A.plot(t, y - ny, ".", ms=3, c="tab:orange", alpha=0.5, label="raw F/B (cm)")
        A.plot(tt, speed * speed_full, c="tab:orange", label=f"speed x {speed_full:g} cm")
        A.axhspan(-0.08 * full, 0.08 * full, color="tab:blue", alpha=0.08)
        A.axhspan(-0.3 * speed_full, 0.3 * speed_full, color="tab:orange", alpha=0.06)
        A.set_title(os.path.basename(p) + " : raw COP vs game input (shaded = dead zones)"); A.legend(fontsize=8, loc="upper right"); A.grid(True)
    plt.tight_layout(); plt.savefig(os.path.join(out, "cop_processing.png"), dpi=90); plt.close()
    report["lowpass_group_delay_ms_at_0.5Hz"] = group_delay_ms(b, a, rate, 0.5)
    w = np.linspace(0.05, 49, 2000); H = signal.freqz(b, a, worN=w, fs=rate)[1]
    plt.figure(figsize=(6, 3.5)); plt.semilogx(w, 20 * np.log10(np.abs(H))); plt.ylim(-40, 3); plt.grid(True, which="both")
    plt.title(f"COP low-pass: 2nd-order Butterworth {fc:g} Hz @ {rate:g} Hz"); plt.xlabel("Hz"); plt.ylabel("dB")
    plt.tight_layout(); plt.savefig(os.path.join(out, "cop_filter.png"), dpi=90); plt.close()
    return report

def analyse_live(path):
    rows = [l.split() for l in open(path, errors="replace") if l.strip()]
    A = np.array([[float(v) for v in r] for r in rows if len(r) == 10])
    hms = A[:, 7] * 3600 + A[:, 8] * 60 + A[:, 9]
    _, c = np.unique(hms, return_counts=True)
    return dict(lines=len(rows), columns=10, lines_per_second_median=float(np.median(c[1:-1])) if len(c) > 2 else None,
                col4_copx_range=[float(A[:, 4].min()), float(A[:, 4].max())], col5_copy_range=[float(A[:, 5].min()), float(A[:, 5].max())],
                col6_weight_range=[float(A[:, 6].min()), float(A[:, 6].max())])

if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--emg"); ap.add_argument("--cop", nargs="*", default=[]); ap.add_argument("--live"); ap.add_argument("--out", default="figures")
    a = ap.parse_args(); os.makedirs(a.out, exist_ok=True)
    rep = {}
    if a.emg: rep["emg"] = analyse_emg(a.emg, a.out)
    if a.cop: rep["cop"] = analyse_cop(a.cop, a.out)
    if a.live: rep["live_file"] = analyse_live(a.live)
    with open(os.path.join(a.out, "analysis_report.json"), "w") as f: json.dump(rep, f, indent=2)
    print(json.dumps(rep, indent=2))
