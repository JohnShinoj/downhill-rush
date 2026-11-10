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

Usage (Windows PowerShell, in this folder):
    py ski_emg_bridge.py                       # live BioRadio (needs BioRadioSDK.dll, see --sdk)
    py ski_emg_bridge.py --sdk "C:\\path\\to\\Python_Bridge\\BioRadioSDK.dll"
    py ski_emg_bridge.py --replay ..\\..\\Assets\\StreamingAssets\\john_final.csv   # no hardware: stream a recording

Needs:  py -m pip install pythonnet matplotlib      (pythonnet only for live mode)
The SDK DLL is NOT included here (vendor licence) - it is found automatically in the kit folder.
"""
import argparse, json, math, os, socket, sys, time
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
def run_live(sdk_path, port):
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
          "lines": [], "msgs": 0, "t_rate": time.perf_counter()}

    bar = ttk.Frame(root, padding=12); bar.pack(fill="x")
    ttk.Label(bar, text="Available BioRadio devices:").pack(side="left")
    selection = ttk.Combobox(bar, width=47, state="readonly"); selection.pack(side="left", padx=8)
    status = tk.StringVar(value="Click Scan to find BioRadio devices.  Ch1 = forearm (JUMP), Ch2 = biceps (BACKFLIP).")
    ttk.Label(root, textvariable=status, padding=8).pack(anchor="w")
    fig = Figure(figsize=(10, 6), dpi=90)
    canvas = FigureCanvasTkAgg(fig, master=root); canvas.get_tk_widget().pack(fill="both", expand=True)

    def disconnect():
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
                    st["buffers"][i].extend(vals)
                    st["totals"][i] += len(vals)
                    send_channel(i + 1, vals, st["totals"][i], st["fs"], port)
                    st["msgs"] += 1
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
    root.protocol("WM_DELETE_WINDOW", close)
    root.mainloop()


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--sdk", help="path to BioRadioSDK.dll")
    ap.add_argument("--port", type=int, default=UDP_PORT)
    ap.add_argument("--replay", help="stream a recorded CSV (sample,time_s,ch1_mv,ch2_mv) instead of the BioRadio")
    a = ap.parse_args()
    if a.replay:
        try: run_replay(a.replay, a.port)
        except KeyboardInterrupt: pass
        sys.exit(0)
    sdk = a.sdk or next((p for p in DEFAULT_SDK_PATHS if os.path.exists(p)), None)
    if not sdk or not os.path.exists(sdk):
        sys.exit("BioRadioSDK.dll not found. Pass it with --sdk \"...\\Python_Bridge\\BioRadioSDK.dll\"")
    print("Using SDK:", sdk)
    run_live(sdk, a.port)
