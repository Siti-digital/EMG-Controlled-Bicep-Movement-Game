"""BioRadio (Bluetooth) -> live plot + recording + EMG game control over UDP to Unity.
Windows, Python with pythonnet, numpy, scipy, matplotlib. BioRadioSDK.dll must be next to this file.
 
Same app as before (Scan -> Connect -> live plot -> Record saves CH 1 to CSV), plus:
  * channel 1 is processed live with the selected member's method (emg_methods.py, same folder):
      IIR (Raima), FIR (Kunal), FFT (Chandan), DIFF (Krishna)
  * Two-point calibration for EACH load (No load / 6 kg), for all four methods at once:
      Cal REST: arm straight, holding that load      -> the 0 level
      Cal MAX : 3 full curls with that load          -> the 1 level
    activation = (envelope - rest) / (max - rest), with a small dead zone and optional smoothing
  * every update sends one JSON message to Unity (127.0.0.1:50557) with the activation 0-1
  * Game log saves raw, filtered, envelope and activation for the report
SDK scaled-value units must be checked against BioCapture before interpreting mV.
"""
import os
import csv
import json
import socket
import time
import tkinter as tk
from tkinter import ttk, messagebox, filedialog
from collections import deque
from datetime import datetime
 
import numpy as np
from emg_methods import METHODS, OWNER, Streaming
 
UDP_HOST = '127.0.0.1'
UDP_PORT = 9050           # must match UDPReceiver.Port in Unity (port 9050 is safe and unblocked)
UPDATE_MS = 20            # read, filter and send every 20 ms (about 50 messages per second)
PLOT_EVERY = 10           # redraw the plot every 10th update (every 200 ms)
WINDOW_SECONDS = 5
TO_MV = 1000.0            # Verify SDK output units using BioCapture.
REST_SECONDS = 5.0
MAX_SECONDS = 8.0
REST_PERCENTILE = 95      # rest level: 95th percentile, so normal resting wobble stays below it
MAX_PERCENTILE = 99       # max level: the person's strongest activity during the curls
DEAD_ZONE = 0.05          # activation below 5% of the range is sent as 0 (arm stays straight)
SMOOTHING = {'Off': 0.0, '100 ms': 0.100, '200 ms': 0.200}   # extra smoothing of the activation
LOADS = ['No load', '6 kg']
 
try:
    import clr
    dll_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'BioRadioSDK.dll')
    if os.path.exists(dll_path):
        clr.AddReference(dll_path)
        from GLNeuroTech.Devices.BioRadio import BioRadioDeviceManager
    else:
        BioRadioDeviceManager = None
    import matplotlib
    matplotlib.use('TkAgg')
    from matplotlib.figure import Figure
    from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
except Exception as exc:
    print('Warning: Hardware SDK or TkAgg failed to initialize:', exc)
    BioRadioDeviceManager = None
 
root = tk.Tk()
root.title('BioRadio EMG to Unity')
root.geometry('1100x800')
manager = BioRadioDeviceManager() if BioRadioDeviceManager is not None else None
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
found = []
device = None
signals = []
buffers = []
totals = []
fs = 1000.0
running = False
recording = False
record_data = []       # list of (time_s, ch1_mv) tuples
record_start_time = 0  # time.time() when recording started
update_count = 0
 
procs = {}             # method -> Streaming processor for channel 1 (all four run in parallel)
calib = {}             # (load, method) -> {'rest': value, 'max': value}, person-specific
cal_active = None      # None, 'rest' or 'max'
cal_load = LOADS[0]
cal_end = 0.0
cal_values = {k: [] for k in METHODS}
act_smooth = 0.0
act_buffer = deque(maxlen=int(WINDOW_SECONDS * 1000 / UPDATE_MS))
log_file = None
log_writer = None
 
bar = ttk.Frame(root, padding=12)
bar.pack(fill='x')
ttk.Label(bar, text='Available BioRadio devices:').pack(side='left')
selection = ttk.Combobox(bar, width=40, state='readonly')
selection.pack(side='left', padx=8)
 
bar2 = ttk.Frame(root, padding=(12, 0, 12, 6))
bar2.pack(fill='x')
ttk.Label(bar2, text='Subject:').pack(side='left')
subject_var = tk.StringVar(value='S1')
ttk.Entry(bar2, textvariable=subject_var, width=6).pack(side='left', padx=(4, 16))
ttk.Label(bar2, text='Method:').pack(side='left')
filter_var = tk.StringVar(value='IIR')
for k in METHODS:
    ttk.Radiobutton(bar2, text='%s (%s)' % (k, OWNER[k]), value=k,
                    variable=filter_var).pack(side='left', padx=4)
 
bar3 = ttk.Frame(root, padding=(12, 0, 12, 6))
bar3.pack(fill='x')
ttk.Label(bar3, text='Load:').pack(side='left')
load_var = tk.StringVar(value=LOADS[0])
for k in LOADS:
    ttk.Radiobutton(bar3, text=k, value=k, variable=load_var).pack(side='left', padx=4)
ttk.Label(bar3, text='     Smoothing:').pack(side='left')
smooth_var = tk.StringVar(value='100 ms')
for k in SMOOTHING:
    ttk.Radiobutton(bar3, text=k, value=k, variable=smooth_var).pack(side='left', padx=4)
 
status = tk.StringVar(value='Click Scan to find BioRadio devices.')
ttk.Label(root, textvariable=status, padding=8).pack(anchor='w')
live = tk.StringVar(value='')
ttk.Label(root, textvariable=live, padding=(8, 0), font=('Consolas', 11)).pack(anchor='w')
 
fig = Figure(figsize=(10, 6), dpi=90)
canvas = FigureCanvasTkAgg(fig, master=root)
canvas.get_tk_widget().pack(fill='both', expand=True)
 
 
def disconnect():
    global device, running, signals, buffers, totals, recording, procs
    if recording:              # save what was recorded instead of losing it
        recording = False
        record_btn.config(text='Record')
        save_recording()
    running = False
    stop_log()
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
    procs = {}
    status.set('Disconnected. Scan and select another device.')
 
 
def scan():
    if manager is None:
        messagebox.showerror('SDK Missing', 'BioRadioSDK.dll not loaded. Ensure it is in the same folder.')
        return
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
    global device, signals, buffers, totals, fs, running, procs
    if manager is None:
        messagebox.showerror('SDK Missing', 'BioRadioSDK.dll not loaded.')
        return
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
        procs = {m: Streaming(m, fs) for m in METHODS}   # channel 1 = biceps
        act_buffer.clear()
        setup_plot(n)
        candidate.StartAcquisition()
        device = candidate
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
 
 
# ------------------------------------------------------------ calibration
def start_calibration(kind):
    """kind = 'rest' or 'max'; calibrates the load that is selected now, for all four methods."""
    global cal_active, cal_end, cal_load
    if not running:
        messagebox.showwarning('Not connected', 'Connect to a BioRadio device first.')
        return
    if cal_active:
        messagebox.showinfo('Calibrating', 'Wait until the current calibration has finished.')
        return
    for k in cal_values:
        cal_values[k] = []
    cal_load = load_var.get()
    secs = REST_SECONDS if kind == 'rest' else MAX_SECONDS
    cal_end = time.time() + secs
    cal_active = kind
    if kind == 'rest':
        status.set('CALIBRATING REST (%s): arm straight and still, holding the load, %d s...' % (cal_load, secs))
    else:
        status.set('CALIBRATING MAX (%s): 3 full, strong curls with the load, %d s...' % (cal_load, secs))
 
 
def finish_calibration():
    global cal_active
    kind, cal_active = cal_active, None
    pct = REST_PERCENTILE if kind == 'rest' else MAX_PERCENTILE
    for k, vals in cal_values.items():
        if vals:
            calib.setdefault((cal_load, k), {})[kind] = float(np.percentile(vals, pct))
    c = calib.get((cal_load, filter_var.get()), {})
    if 'rest' in c and 'max' in c and c['max'] < 1.5 * c['rest']:
        messagebox.showwarning('Calibration', 'MAX is hardly above REST for %s. Redo MAX with strong '
                               'full curls, or check the electrodes.' % cal_load)
    status.set('Calibrated %s %s for %s: ' % (kind.upper(), cal_load, subject_var.get()) +
               ', '.join('%s %s' % (k, cal_text_for(cal_load, k)) for k in METHODS))
 
 
def cal_text_for(load, method):
    c = calib.get((load, method), {})
    return 'rest %s / max %s' % (('%.1f' % c['rest']) if 'rest' in c else '?',
                                 ('%.1f' % c['max']) if 'max' in c else '?')
 
 
def activation_from(env, load, method):
    """0 at the calibrated rest level of this load, 1 at its max level; dead zone; optional smoothing."""
    global act_smooth
    c = calib.get((load, method), {})
    rest = c.get('rest', 3.0)
    # If MAX has not been calibrated yet, use default 120.0 uV so user can test before Cal MAX
    max_val = c.get('max', 120.0)
    a = (env - rest) / max(max_val - rest, 1e-6)
    a = min(1.0, max(0.0, (a - DEAD_ZONE) / (1.0 - DEAD_ZONE)))
    tau = SMOOTHING[smooth_var.get()]
    if tau > 0:
        act_smooth += (1.0 - np.exp(-(UPDATE_MS / 1000.0) / tau)) * (a - act_smooth)
    else:
        act_smooth = a
    return float(act_smooth)
 
 
# ------------------------------------------------------------ game log
def toggle_log():
    global log_file, log_writer
    if log_file is not None:
        stop_log()
        return
    if not running:
        messagebox.showwarning('Not connected', 'Connect to a BioRadio device first.')
        return
    name = 'live_%s_%s.csv' % (subject_var.get(), datetime.now().strftime('%Y%m%d_%H%M%S'))
    log_file = open(name, 'w', newline='')
    log_writer = csv.writer(log_file)
    log_writer.writerow(['sample_index', 'time_s', 'raw_mV', 'processed_uV', 'envelope', 'activation',
                         'method', 'load', 'cal_rest', 'cal_max', 'smoothing', 'sent_unix_time'])
    log_btn.config(text='Stop game log')
    status.set('Game log: %s' % os.path.abspath(name))
 
 
def stop_log():
    global log_file, log_writer
    if log_file is not None:
        log_file.close()
        status.set('Game log saved.')
    log_file = None
    log_writer = None
    log_btn.config(text='Game log')
 
 
# ------------------------------------------------------------ main loop
def process_channel1(vals, first_index):
    """Process CH 1 with all four methods, update calibration, send the selected one to Unity, log it."""
    raw_uv = np.asarray(vals) * 1000.0                  # mV -> uV
    outs = {m: p.process(raw_uv) for m, p in procs.items()}
    envs = {m: p.envelope() for m, p in procs.items()}
    f = filter_var.get()
    env = envs[f]
    y = outs[f]
 
    if cal_active:
        for k in METHODS:
            cal_values[k].append(envs[k])
        if time.time() >= cal_end:
            finish_calibration()
 
    load = load_var.get()
    act = activation_from(env, load, f)
    c = calib.get((load, f), {})
    sent = time.time()
    rms_mv = float(np.sqrt(np.mean(np.square(vals))))
    payload = {'channel': 1, 'raw_mv': vals[-1], 'rms_mv': rms_mv,
               'sample_count': totals[0], 'sample_rate_hz': fs,
               'sender_time': round(sent, 3), 'envelope_uv': round(env, 3),
               'activation': round(act, 4), 'filter': f, 'load': load}
    try:
        sock.sendto(json.dumps(payload).encode('utf-8'), (UDP_HOST, UDP_PORT))
        if int(sent * 2) % 2 == 0 and not hasattr(process_channel1, '_last_print_sec') or getattr(process_channel1, '_last_print_sec', 0) != int(sent):
            process_channel1._last_print_sec = int(sent)
            print('[UDP 9050 -> Unity] Sent act=%.2f, env=%.1f uV, filter=%s' % (act, env, f))
    except Exception as exc:
        print('[UDP ERROR] Failed sending to %s:%d: %r' % (UDP_HOST, UDP_PORT, exc))
 
    if log_writer is not None:
        last = len(vals) - 1
        for k, (v, yy) in enumerate(zip(vals, y)):
            i = first_index + k
            row = [i, '%.3f' % (i / fs), '%.6f' % v, '%.3f' % yy]
            row += (['%.3f' % env, '%.4f' % act, f, load, '%.3f' % c.get('rest', float('nan')),
                     '%.3f' % c.get('max', float('nan')), smooth_var.get(), '%.3f' % sent]
                    if k == last else [''] * 8)
            log_writer.writerow(row)
 
    act_buffer.append(act)
    cal_text = cal_text_for(load, f) if 'max' in c else '%s (DEFAULT MAX: 120 uV - click Cal MAX)' % cal_text_for(load, f)
    live.set('%s | %s (%s) | envelope %7.1f | activation %.2f | %s' % (load, f, OWNER[f], env, act, cal_text))
 
 
def update():
    global update_count
    if not running or device is None:
        return
    try:
        for i, sig in enumerate(signals):
            batch = sig.GetScaledValueArray()
            vals = [float(x) * TO_MV for x in batch] if batch is not None else []
            if not vals:
                continue
            first_index = totals[i]
            buffers[i].extend(vals)
            totals[i] += len(vals)
            if i == 0:
                # ─── record CH 1 only (same CSV format as before) ───
                if recording:
                    elapsed = time.time() - record_start_time
                    sample_interval = 1.0 / fs
                    for k, v in enumerate(vals):
                        t = elapsed - (len(vals) - 1 - k) * sample_interval
                        record_data.append((round(t, 6), round(v, 6)))
                process_channel1(vals, first_index)
        update_count += 1
        if update_count % PLOT_EVERY == 0:
            draw()
    except Exception as exc:
        status.set('Read/plot error: %r' % exc)
        print('Read/plot error:', repr(exc))
    if running:
        root.after(UPDATE_MS, update)
 
 
plot_lines = []          # one line per channel + the activation line, made once in setup_plot()
 
 
def setup_plot(n):
    """Make the axes once; draw() then only updates the line data (much faster than redrawing)."""
    fig.clear()
    plot_lines.clear()
    rows = n + 1
    for i in range(n):
        ax = fig.add_subplot(rows, 1, i + 1)
        line, = ax.plot([], [], linewidth=0.7, color='black')
        ax.set_ylabel('CH %d (mV)' % (i + 1))
        ax.set_xlim(-WINDOW_SECONDS, 0)
        ax.grid(alpha=0.2)
        plot_lines.append((ax, line))
    ax = fig.add_subplot(rows, 1, rows)
    line, = ax.plot([], [], linewidth=1.5, color='#eb6834')
    ax.set_ylim(0, 1.05)
    ax.set_xlim(-WINDOW_SECONDS, 0)
    ax.set_ylabel('activation to Unity')
    ax.set_xlabel('Time relative to now (s)')
    ax.grid(alpha=0.2)
    plot_lines.append((ax, line))
    fig.tight_layout()
    canvas.draw_idle()
 
 
def draw():
    for i, (ax, line) in enumerate(plot_lines[:-1]):
        b = np.fromiter(buffers[i], dtype=float)[::4]       # every 4th sample is enough to draw
        if b.size:
            line.set_data(np.arange(-b.size + 1, 1) * 4 / fs, b)
            lo, hi = float(b.min()), float(b.max())
            pad = 0.05 * (hi - lo) + 1e-6
            ax.set_ylim(lo - pad, hi + pad)
    a = np.fromiter(act_buffer, dtype=float)
    if a.size:
        plot_lines[-1][1].set_data(np.arange(-a.size + 1, 1) * UPDATE_MS / 1000.0, a)
    canvas.draw_idle()
 
 
# ------------------------------------------------------------ recording (unchanged)
def toggle_record():
    global recording, record_data, record_start_time
    if not running or device is None:
        messagebox.showwarning('Not connected', 'Connect to a BioRadio device first.')
        return
    if not recording:
        record_data = []
        record_start_time = time.time()
        recording = True
        record_btn.config(text='Stop Recording')
        status.set(status.get() + '  |  RECORDING')
    else:
        recording = False
        record_btn.config(text='Record')
        save_recording()
 
 
def save_recording():
    if not record_data:
        messagebox.showinfo('No data', 'No data was recorded.')
        return
    default_name = 'bioradio_ch1_%s.csv' % datetime.now().strftime('%Y%m%d_%H%M%S')
    filepath = filedialog.asksaveasfilename(
        defaultextension='.csv',
        filetypes=[('CSV files', '*.csv')],
        initialfile=default_name,
        title='Save CH 1 recording'
    )
    if not filepath:
        return
    with open(filepath, 'w', newline='') as f:
        writer = csv.writer(f)
        writer.writerow(['time_seconds', 'ch1_mv'])
        writer.writerows(record_data)
    duration = record_data[-1][0] if record_data else 0
    messagebox.showinfo('Saved', 'Saved %d samples (%.1f seconds) to:\n%s'
                        % (len(record_data), duration, filepath))
    status.set('Recording saved. %d samples.' % len(record_data))
 
 
def close():
    disconnect()
    sock.close()
    root.destroy()
 
 
for label, callback in [('Scan', scan), ('Connect', connect), ('Disconnect', disconnect)]:
    ttk.Button(bar, text=label, command=callback).pack(side='left', padx=3)
record_btn = ttk.Button(bar, text='Record', command=toggle_record)
record_btn.pack(side='left', padx=10)
ttk.Button(bar, text='Cal REST (5 s)', command=lambda: start_calibration('rest')).pack(side='left', padx=3)
ttk.Button(bar, text='Cal MAX (8 s)', command=lambda: start_calibration('max')).pack(side='left', padx=3)
log_btn = ttk.Button(bar, text='Game log', command=toggle_log)
log_btn.pack(side='left', padx=3)
root.protocol('WM_DELETE_WINDOW', close)
root.mainloop()
