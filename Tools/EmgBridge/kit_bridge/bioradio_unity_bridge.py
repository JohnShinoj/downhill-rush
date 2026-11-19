"""[Downhill Rush copy of the BioRadio Student Unity Kit bridge, with our CSV recording added.
The only change for the repo: BioRadioSDK.dll is also searched in Tools/EmgBridge and in the kit in Downloads.
The game works with this bridge too (it then uses rms_mv as the EMG envelope), but
Tools/EmgBridge/run_bridge.bat (ski_emg_bridge.py) is the recommended one: it sends every sample.]

BioRadio -> live plot + UDP to Unity + CSV recording. Windows, Python with pythonnet.
Choose a discovered device in the GUI. Press Connect, then Start Recording to save samples.
Each recording is a CSV in the recordings folder next to this script, plus an _info.json
with device, sample rate, channel names and SDK units.
SDK scaled-value units must be checked against BioCapture before interpreting mV.
"""
import os
import csv
import math
import socket
import json
import tkinter as tk
from tkinter import ttk, messagebox
from collections import deque
from datetime import datetime

UDP_HOST = '127.0.0.1'
UDP_PORT = 50557
UPDATE_MS = 50
WINDOW_SECONDS = 5
TO_MV = 1000.0  # Verify SDK output units using BioCapture.
REC_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'recordings')

try:
    import clr
    # [Downhill Rush] look for the SDK next to this script, then in Tools/EmgBridge, then in the kit in Downloads
    _here = os.path.dirname(os.path.abspath(__file__))
    _sdk_candidates = [os.path.join(_here, 'BioRadioSDK.dll'),
                       os.path.join(_here, '..', 'BioRadioSDK.dll'),
                       os.path.expandvars(r'%USERPROFILE%\Downloads\BioRadio_Student_Unity_Kit\BioRadio_Student_Unity_Kit\Python_Bridge\BioRadioSDK.dll')]
    _sdk = next((c for c in _sdk_candidates if os.path.exists(c)), _sdk_candidates[0])
    print('Using SDK:', _sdk)
    clr.AddReference(_sdk)
    from GLNeuroTech.Devices.BioRadio import BioRadioDeviceManager
    import matplotlib
    matplotlib.use('TkAgg')
    from matplotlib.figure import Figure
    from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
except Exception as exc:
    raise SystemExit('Install pythonnet and matplotlib, check DLL. Details: %r' % exc)

root = tk.Tk()
root.title('BioRadio EMG to Unity')
root.geometry('1000x760')
manager = BioRadioDeviceManager()
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
found = []
device = None
device_id = ('', '')
signals = []
buffers = []
totals = []
fs = 1000.0
running = False

rec_file = None
rec_writer = None
rec_pending = []  # per-channel samples waiting until every channel has them
rec_start = 0     # sample index (since connect) of the first recorded row
rec_rows = 0
rec_path = ''
rec_info = {}

bar = ttk.Frame(root, padding=12)
bar.pack(fill='x')
ttk.Label(bar, text='Available BioRadio devices:').pack(side='left')
selection = ttk.Combobox(bar, width=47, state='readonly')
selection.pack(side='left', padx=8)
status = tk.StringVar(value='Click Scan to find BioRadio devices.')
ttk.Label(root, textvariable=status, padding=8).pack(anchor='w')
rec_status = tk.StringVar(value='Not recording.')
ttk.Label(root, textvariable=rec_status, padding=(8, 0)).pack(anchor='w')
rec_text = tk.StringVar(value='Start Recording')

fig = Figure(figsize=(10, 6), dpi=90)
canvas = FigureCanvasTkAgg(fig, master=root)
canvas.get_tk_widget().pack(fill='both', expand=True)


def save_info():
    with open(rec_path[:-4] + '_info.json', 'w') as f:
        json.dump(rec_info, f, indent=2)


def start_recording():
    global rec_file, rec_writer, rec_pending, rec_start, rec_rows, rec_path, rec_info
    if device is None:
        messagebox.showwarning('Not connected', 'Connect to a BioRadio before recording.')
        return
    now = datetime.now()
    try:
        os.makedirs(REC_DIR, exist_ok=True)
        base = os.path.join(REC_DIR, 'bioradio_%s_%s' % (now.strftime('%Y%m%d_%H%M%S'), device_id[1]))
        rec_path, k = base + '.csv', 1
        while os.path.exists(rec_path):  # never overwrite an earlier recording
            k += 1
            rec_path = '%s_%d.csv' % (base, k)
        rec_file = open(rec_path, 'w', newline='')
    except OSError as exc:
        messagebox.showerror('Recording failed', str(exc))
        return
    rec_writer = csv.writer(rec_file)
    rec_writer.writerow(['sample', 'time_s'] + ['ch%d_mv' % (i+1) for i in range(len(signals))])
    rec_pending = [deque() for _ in signals]
    rec_start = max(totals)  # channels can be a batch apart; start all on the same sample
    rec_rows = 0
    rec_info = {'device': device_id[0], 'mac': device_id[1], 'sample_rate_hz': fs,
                'channel_names': [str(s.Name) for s in signals],
                'sdk_units': [str(s.Units) for s in signals],
                'to_mv_factor': TO_MV, 'started': now.isoformat(timespec='seconds')}
    save_info()
    rec_text.set('Stop Recording')
    rec_status.set('Recording to %s' % rec_path)


def write_rows(final=False):
    """Write one CSV row per sample index that every channel has; on final, pad the rest."""
    global rec_rows
    lengths = [len(p) for p in rec_pending]
    for _ in range(max(lengths) if final else min(lengths)):
        values = ['%.6f' % p.popleft() if p else '' for p in rec_pending]
        rec_writer.writerow([rec_rows, '%.6f' % (rec_rows / fs)] + values)
        rec_rows += 1
    rec_file.flush()


def stop_recording():
    global rec_file, rec_writer
    if rec_file is None:
        return
    try:
        write_rows(final=True)
    except Exception as e:
        print('Recording flush:', repr(e))
    rec_file.close()
    rec_file = rec_writer = None
    rec_info.update(stopped=datetime.now().isoformat(timespec='seconds'),
                    samples=rec_rows, duration_s=rec_rows / fs)
    save_info()
    rec_text.set('Start Recording')
    rec_status.set('Saved %d samples (%.1f s) to %s' % (rec_rows, rec_rows / fs, rec_path))


def toggle_recording():
    if rec_file is None:
        start_recording()
    else:
        stop_recording()


def disconnect():
    global device, running, signals, buffers, totals
    stop_recording()
    running = False
    if device is not None:
        try:
            device.StopAcquisition()
        except Exception as e:
            print('Stop:', repr(e))
        try:
            device.Disconnect()
        except Exception as e:
            print('Disconnect:', repr(e))
    device = None
    signals = []
    buffers = []
    totals = []
    status.set('Disconnected. Scan and select another device.')


def scan():
    if device is not None:
        messagebox.showinfo('Disconnect first', 'Disconnect before scanning again.')
        return
    try:
        results = manager.DiscoverBluetoothDevices()
        found[:] = []
        for i in range(results.Length):
            d = results[i]
            mac = str(d.MacId).replace(':', '').replace('-', '').upper()
            found.append((str(d.DeviceId), mac))
        selection['values'] = ['%s | %s' % x for x in found]
        if found:
            selection.current(0)
        status.set('%d device(s) discovered. Choose one and click Connect.' % len(found))
    except Exception as exc:
        status.set('Discovery failed: %s' % exc)


def connect():
    global device, device_id, signals, buffers, totals, fs, running
    if device is not None:
        messagebox.showinfo('Already connected', 'Disconnect before choosing another device.')
        return
    idx = selection.current()
    if idx < 0:
        messagebox.showwarning('Select device', 'Click Scan and select a BioRadio.')
        return
    candidate = None
    try:
        name, mac = found[idx]
        candidate = manager.GetBluetoothDevice(int(mac, 16))
        group = candidate.BioPotentialSignals
        n = int(group.Count)
        if n < 1:
            raise RuntimeError('No enabled biopotential channels. Configure in BioCapture.')
        fs = float(group.SamplesPerSecond)
        if fs <= 0:
            raise RuntimeError('Invalid sampling frequency')
        signals = [group[i] for i in range(n)]  # SDK SignalGroup indexer
        buffers = [deque(maxlen=max(10, int(fs * WINDOW_SECONDS))) for _ in range(n)]
        totals = [0] * n
        candidate.StartAcquisition()
        device = candidate
        device_id = (name, mac)
        running = True
        status.set('%s | %s | %d channels | %.0f Hz | UDP %s:%d' %
                   (name, mac, n, fs, UDP_HOST, UDP_PORT))
        root.after(UPDATE_MS, update)
    except Exception as exc:
        if candidate is not None:
            try:
                candidate.Disconnect()
            except Exception:
                pass
        status.set('Connection failed: %r' % exc)


def update():
    if not running or device is None:
        return
    try:
        fig.clear()
        n = len(signals)
        for i, sig in enumerate(signals):
            batch = sig.GetScaledValueArray()
            vals = [float(x) * TO_MV for x in batch] if batch is not None else []
            if vals:
                if rec_file is not None:
                    rec_pending[i].extend(vals[max(0, rec_start - totals[i]):])
                buffers[i].extend(vals)
                totals[i] += len(vals)
                rms = math.sqrt(sum(v*v for v in vals) / len(vals))
                payload = {'channel': i+1, 'raw_mv': vals[-1], 'rms_mv': rms,
                           'sample_count': totals[i], 'sample_rate_hz': fs}
                sock.sendto(json.dumps(payload).encode('utf-8'), (UDP_HOST, UDP_PORT))
            ax = fig.add_subplot(n, 1, i+1)
            b = list(buffers[i])
            if b:
                x = [(j-len(b)+1)/fs for j in range(len(b))]
                ax.plot(x, b, linewidth=0.7, color='black')
            ax.set_ylabel('CH %d (mV)' % (i+1))
            ax.set_xlim(-WINDOW_SECONDS, 0)
            ax.grid(alpha=0.2)
        if rec_file is not None:
            write_rows()
            rec_status.set('Recording %.1f s -> %s' % (rec_rows / fs, os.path.basename(rec_path)))
        fig.axes[-1].set_xlabel('Time relative to now (s)')
        fig.tight_layout()
        canvas.draw_idle()
    except Exception as exc:
        status.set('Read/plot error: %r' % exc)
        print('Read/plot error:', repr(exc))
    if running:
        root.after(UPDATE_MS, update)


def close():
    disconnect()
    sock.close()
    root.destroy()

for label, callback in [('Scan', scan), ('Connect', connect), ('Disconnect', disconnect)]:
    ttk.Button(bar, text=label, command=callback).pack(side='left', padx=3)
ttk.Button(bar, textvariable=rec_text, command=toggle_recording).pack(side='left', padx=3)
root.protocol('WM_DELETE_WINDOW', close)
root.mainloop()
