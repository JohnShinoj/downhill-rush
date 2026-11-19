"""
BioRadio EMG -> Downhill Rush (Unity), over UDP 127.0.0.1:50557.

Based on the BioRadio Student Unity Kit's bioradio_unity_bridge.py (same SDK calls, same GUI:
Scan -> choose device -> Connect), with two changes for the game:

  1. Every message carries ALL samples since the previous one ("samples": [...]), not only the
     last sample + a batch RMS. The game runs the proper EMG filters on them (20-450 Hz band-pass,
     50 Hz notch, rectify, 5 Hz envelope), which needs the full 1000 Hz signal.
     The kit's fields (channel, raw_mv, rms_mv, sample_count, sample_rate_hz) are still sent,
     so the kit's BioRadioUdpMover.cs keeps working too.
  2. Data is sent every 20 ms (50 times/s) and the plot is redrawn separately every 200 ms,
     so drawing never delays the game.
  3. Start Recording / Stop Recording saves the EMG to CSV while it streams to the game
     (same format as the kit: sample,time_s,ch1_mv,ch2_mv + an _info.json), so ONE window
     both plays the game and records. Recordings go to the kit's recordings folder
     (or --rec-dir).

Usage (Windows PowerShell, in this folder):
    py ski_emg_bridge.py                       # live BioRadio (needs BioRadioSDK.dll, see --sdk)
    py ski_emg_bridge.py --sdk "C:\\path\\to\\Python_Bridge\\BioRadioSDK.dll"
    py ski_emg_bridge.py --rec-dir "D:\\my_recordings"  # save recordings somewhere else
    py ski_emg_bridge.py --replay ..\\..\\Assets\\StreamingAssets\\john_final.csv   # no hardware: stream a recording

Needs:  py -m pip install pythonnet matplotlib      (pythonnet only for live mode)
The SDK DLL is NOT included here (vendor licence) - it is found automatically in the kit folder.
"""
import argparse, csv, json, math, os, socket, sys, time
from datetime import datetime
from collections import deque

UDP_HOST, UDP_PORT = "127.0.0.1", 50557
SEND_MS = 20          # send period
PLOT_MS = 200         # plot redraw period
WINDOW_SECONDS = 5
TO_MV = 1000.0        # SDK scaled values are volts -> mV (as in the kit; verify against BioCapture)

DEFAULT_SDK_PATHS = [
    os.path.join(os.path.dirname(os.path.abspath(__file__)), "BioRadioSDK.dll"),
    os.path.expandvars(r"%USERPROFILE%\Downloads\BioRadio_Student_Unity_Kit\BioRadio_Student_Unity_Kit\Python_Bridge\BioRadioSDK.dll"),
]

# Recordings: the kit's recordings folder if it exists (where the earlier recordings are), else next to this script.
DEFAULT_REC_DIRS = [
    os.path.expandvars(r"%USERPROFILE%\Downloads\BioRadio_Student_Unity_Kit\BioRadio_Student_Unity_Kit\Python_Bridge\recordings"),
]

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
MAX_SAMPLES_PER_MSG = 200   # keeps every UDP datagram small (~2 kB)


def send_channel(ch, vals, total, fs, port):
    """One JSON message per channel; splits large batches so no datagram gets too big."""
    for start in range(0, len(vals), MAX_SAMPLES_PER_MSG):
        part = vals[start:start + MAX_SAMPLES_PER_MSG]
        rms = math.sqrt(sum(v * v for v in part) / len(part))
        payload = {"channel": ch, "raw_mv": part[-1], "rms_mv": rms,
                   "sample_count": total - len(vals) + start + len(part), "sample_rate_hz": fs,
                   "samples": [round(v, 6) for v in part]}
        sock.sendto(json.dumps(payload, separators=(",", ":")).encode("utf-8"), (UDP_HOST, port))


# ------------------------------------------------------------------ no-hardware test
def run_replay(path, port, speed=1.0):
    import csv
    ch1, ch2 = [], []
    with open(path, newline="") as f:
        for row in csv.reader(f):
            try:
                ch1.append(float(row[2])); ch2.append(float(row[3]))
            except (ValueError, IndexError):
                pass  # header
    fs = 1000.0
    print(f"Replaying {len(ch1)} samples ({len(ch1)/fs:.0f} s, loops) to udp://{UDP_HOST}:{port}.  Ctrl+C to stop.")
    t0, sent, n = time.perf_counter(), 0, len(ch1)
    while True:
        due = int((time.perf_counter() - t0) * fs * speed)
        if due > sent:
            idx = [(sent + i) % n for i in range(due - sent)]
            send_channel(1, [ch1[i] for i in idx], due, fs, port)
            send_channel(2, [ch2[i] for i in idx], due, fs, port)
            sent = due
        time.sleep(SEND_MS / 1000.0)


# ------------------------------------------------------------------ live BioRadio (kit GUI)
def run_live(sdk_path, port, rec_dir):
    import tkinter as tk
    from tkinter import ttk, messagebox
    try:
        import clr
        clr.AddReference(sdk_path)
        from GLNeuroTech.Devices.BioRadio import BioRadioDeviceManager
        import matplotlib
        matplotlib.use("TkAgg")
        from matplotlib.figure import Figure
        from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
    except Exception as exc:
        raise SystemExit("Install pythonnet and matplotlib (py -m pip install pythonnet matplotlib) and check the DLL.\nDetails: %r" % exc)

    root = tk.Tk()
    root.title("BioRadio EMG -> Downhill Rush")
    root.geometry("1000x740")
    manager = BioRadioDeviceManager()
    st = {"found": [], "device": None, "signals": [], "buffers": [], "totals": [], "fs": 1000.0, "running": False,
          "lines": [], "msgs": 0, "t_rate": time.perf_counter(), "device_id": ("", "")}
    # recording state (same logic as the kit's bioradio_unity_bridge.py)
    rec = {"file": None, "writer": None, "pending": [], "start": 0, "rows": 0, "path": "", "info": {}}

    bar = ttk.Frame(root, padding=12); bar.pack(fill="x")
    ttk.Label(bar, text="Available BioRadio devices:").pack(side="left")
    selection = ttk.Combobox(bar, width=47, state="readonly"); selection.pack(side="left", padx=8)
    status = tk.StringVar(value="Click Scan to find BioRadio devices.  Ch1 = forearm (JUMP), Ch2 = biceps (BACKFLIP).")
    ttk.Label(root, textvariable=status, padding=8).pack(anchor="w")
    rec_status = tk.StringVar(value="Not recording.  (Recordings are saved to %s)" % rec_dir)
    ttk.Label(root, textvariable=rec_status, padding=(8, 0)).pack(anchor="w")
    rec_text = tk.StringVar(value="Start Recording")
    fig = Figure(figsize=(10, 6), dpi=90)
    canvas = FigureCanvasTkAgg(fig, master=root); canvas.get_tk_widget().pack(fill="both", expand=True)

    # ---------------- recording (ported from the kit's bioradio_unity_bridge.py) ----------------
    def save_info():
        with open(rec["path"][:-4] + "_info.json", "w") as f:
            json.dump(rec["info"], f, indent=2)

    def start_recording():
        if st["device"] is None:
            messagebox.showwarning("Not connected", "Connect to a BioRadio before recording."); return
        now = datetime.now()
        try:
            os.makedirs(rec_dir, exist_ok=True)
            base = os.path.join(rec_dir, "bioradio_%s_%s" % (now.strftime("%Y%m%d_%H%M%S"), st["device_id"][1]))
            path, k = base + ".csv", 1
            while os.path.exists(path):          # never overwrite an earlier recording
                k += 1; path = "%s_%d.csv" % (base, k)
            f = open(path, "w", newline="")
        except OSError as exc:
            messagebox.showerror("Recording failed", str(exc)); return
        w = csv.writer(f)
        w.writerow(["sample", "time_s"] + ["ch%d_mv" % (i + 1) for i in range(len(st["signals"]))])
        rec.update(file=f, writer=w, path=path, rows=0,
                   pending=[deque() for _ in st["signals"]],
                   start=max(st["totals"]),       # channels can be a batch apart: start all on the same sample
                   info={"device": st["device_id"][0], "mac": st["device_id"][1], "sample_rate_hz": st["fs"],
                         "channel_names": [str(s.Name) for s in st["signals"]],
                         "sdk_units": [str(s.Units) for s in st["signals"]],
                         "to_mv_factor": TO_MV, "started": now.isoformat(timespec="seconds")})
        save_info()
        rec_text.set("Stop Recording")
        rec_status.set("Recording to %s" % path)

    def write_rows(final=False):
        """One CSV row per sample index that every channel has; on final, pad the rest."""
        lengths = [len(q) for q in rec["pending"]]
        for _ in range(max(lengths) if final else min(lengths)):
            values = ["%.6f" % q.popleft() if q else "" for q in rec["pending"]]
            rec["writer"].writerow([rec["rows"], "%.6f" % (rec["rows"] / st["fs"])] + values)
            rec["rows"] += 1
        rec["file"].flush()

    def stop_recording():
        if rec["file"] is None: return
        try: write_rows(final=True)
        except Exception as e: print("Recording flush:", repr(e))
        rec["file"].close()
        rec.update(file=None, writer=None)
        rec["info"].update(stopped=datetime.now().isoformat(timespec="seconds"),
                           samples=rec["rows"], duration_s=rec["rows"] / st["fs"])
        save_info()
        rec_text.set("Start Recording")
        rec_status.set("Saved %d samples (%.1f s) to %s" % (rec["rows"], rec["rows"] / st["fs"], rec["path"]))

    def toggle_recording():
        if rec["file"] is None: start_recording()
        else: stop_recording()

    def disconnect():
        stop_recording()                         # never lose a recording on disconnect
        st["running"] = False
        d = st["device"]
        if d is not None:
            for name, fn in (("Stop", d.StopAcquisition), ("Disconnect", d.Disconnect)):
                try: fn()
                except Exception as e: print(name + ":", repr(e))
        st.update(device=None, signals=[], buffers=[], totals=[], lines=[])
        status.set("Disconnected. Scan and select another device.")

    def scan():
        if st["device"] is not None:
            messagebox.showinfo("Disconnect first", "Disconnect before scanning again."); return
        try:
            results = manager.DiscoverBluetoothDevices()
            st["found"] = []
            for i in range(results.Length):
                d = results[i]
                mac = str(d.MacId).replace(":", "").replace("-", "").upper()
                st["found"].append((str(d.DeviceId), mac))
            selection["values"] = ["%s | %s" % x for x in st["found"]]
            if st["found"]: selection.current(0)
            status.set("%d device(s) discovered. Choose one and click Connect." % len(st["found"]))
        except Exception as exc:
            status.set("Discovery failed: %s" % exc)

    def connect():
        if st["device"] is not None:
            messagebox.showinfo("Already connected", "Disconnect before choosing another device."); return
        idx = selection.current()
        if idx < 0:
            messagebox.showwarning("Select device", "Click Scan and select a BioRadio."); return
        candidate = None
        try:
            name, mac = st["found"][idx]
            candidate = manager.GetBluetoothDevice(int(mac, 16))
            group = candidate.BioPotentialSignals
            n = int(group.Count)
            if n < 1: raise RuntimeError("No enabled biopotential channels. Configure in BioCapture.")
            fs = float(group.SamplesPerSecond)
            if fs <= 0: raise RuntimeError("Invalid sampling frequency")
            st["device_id"] = (name, mac)
            st.update(fs=fs, signals=[group[i] for i in range(n)],
                      buffers=[deque(maxlen=max(10, int(fs * WINDOW_SECONDS))) for _ in range(n)], totals=[0] * n)
            candidate.StartAcquisition()
            st["device"] = candidate
            st["running"] = True
            fig.clear(); st["lines"] = []
            for i in range(n):
                ax = fig.add_subplot(n, 1, i + 1)
                line, = ax.plot([], [], linewidth=0.7, color="black")
                ax.set_xlim(-WINDOW_SECONDS, 0); ax.set_ylim(-1, 1); ax.grid(alpha=0.2)
                ax.set_ylabel("CH %d (mV)%s" % (i + 1, "  JUMP" if i == 0 else "  BACKFLIP" if i == 1 else ""))
                st["lines"].append((ax, line))
            fig.axes[-1].set_xlabel("Time relative to now (s)"); fig.tight_layout()
            status.set("%s | %s | %d channels | %.0f Hz | sending to Unity on UDP %s:%d" % (name, mac, n, fs, UDP_HOST, port))
            root.after(SEND_MS, pump); root.after(PLOT_MS, redraw)
        except Exception as exc:
            if candidate is not None:
                try: candidate.Disconnect()
                except Exception: pass
            status.set("Connection failed: %r" % exc)

    def pump():
        """Read every new sample from the SDK and send it to the game (fast path, no drawing)."""
        if not st["running"] or st["device"] is None: return
        try:
            for i, sig in enumerate(st["signals"]):
                batch = sig.GetScaledValueArray()
                vals = [float(x) * TO_MV for x in batch] if batch is not None else []
                if vals:
                    if rec["file"] is not None:      # recording: keep only samples from the start index on
                        rec["pending"][i].extend(vals[max(0, rec["start"] - st["totals"][i]):])
                    st["buffers"][i].extend(vals)
                    st["totals"][i] += len(vals)
                    send_channel(i + 1, vals, st["totals"][i], st["fs"], port)
                    st["msgs"] += 1
            if rec["file"] is not None:
                write_rows()
                rec_status.set("Recording %.1f s -> %s" % (rec["rows"] / st["fs"], os.path.basename(rec["path"])))
        except Exception as exc:
            status.set("Read error: %r" % exc); print("Read error:", repr(exc))
        if st["running"]: root.after(SEND_MS, pump)

    def redraw():
        if not st["running"]: return
        try:
            fs = st["fs"]
            for (ax, line), buf in zip(st["lines"], st["buffers"]):
                b = list(buf)[::5]                       # 200 Hz is plenty for a 5 s plot
                if b:
                    x = [(j - len(b) + 1) * 5 / fs for j in range(len(b))]
                    line.set_data(x, b)
                    lo, hi = min(b), max(b)
                    pad = max(0.05, (hi - lo) * 0.1)
                    ax.set_ylim(lo - pad, hi + pad)
            canvas.draw_idle()
            now = time.perf_counter()
            if now - st["t_rate"] > 2:
                print("sent %.0f msg/s" % (st["msgs"] / (now - st["t_rate"]))); st["msgs"] = 0; st["t_rate"] = now
        except Exception as exc:
            print("Plot error:", repr(exc))
        if st["running"]: root.after(PLOT_MS, redraw)

    def close():
        disconnect(); sock.close(); root.destroy()

    for label, cb in [("Scan", scan), ("Connect", connect), ("Disconnect", disconnect)]:
        ttk.Button(bar, text=label, command=cb).pack(side="left", padx=3)
    ttk.Button(bar, textvariable=rec_text, command=toggle_recording).pack(side="left", padx=3)
    root.protocol("WM_DELETE_WINDOW", close)
    root.mainloop()


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--sdk", help="path to BioRadioSDK.dll")
    ap.add_argument("--port", type=int, default=UDP_PORT)
    ap.add_argument("--rec-dir", help="folder for recordings (default: the kit's recordings folder, else ./recordings)")
    ap.add_argument("--replay", help="stream a recorded CSV (sample,time_s,ch1_mv,ch2_mv) instead of the BioRadio")
    a = ap.parse_args()
    if a.replay:
        try: run_replay(a.replay, a.port)
        except KeyboardInterrupt: pass
        sys.exit(0)
    sdk = a.sdk or next((p for p in DEFAULT_SDK_PATHS if os.path.exists(p)), None)
    if not sdk or not os.path.exists(sdk):
        sys.exit("BioRadioSDK.dll not found. Pass it with --sdk \"...\\Python_Bridge\\BioRadioSDK.dll\"")
    rec_dir = a.rec_dir or next((d for d in DEFAULT_REC_DIRS if os.path.isdir(d)),
                                os.path.join(os.path.dirname(os.path.abspath(__file__)), "recordings"))
    print("Using SDK:", sdk)
    print("Recordings will be saved to:", rec_dir)
    run_live(sdk, a.port, rec_dir)
