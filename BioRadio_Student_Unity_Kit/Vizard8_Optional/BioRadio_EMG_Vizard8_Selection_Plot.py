"""Vizard 8: select a discovered BioRadio using on-screen buttons, then plot all enabled channels.
Place the original BioRadioSDK.dll alongside this file. Close BioCapture before starting.
"""
import os
import math
from collections import deque
import viz
import vizact

UPDATE_SEC = 0.05
WINDOW_SEC = 5.0
TO_MV = 1000.0  # Confirm SDK voltage units against BioCapture.
MAX_DISPLAY_CHANNELS = 8
MIN_HALF_RANGE_MV = 0.05

viz.go()
status = viz.addText('BioRadio: initializing...', parent=viz.SCREEN)
status.setPosition(0.04, 0.94)
status.fontSize(20)
message = viz.addText('Select a discovered BioRadio device:', parent=viz.SCREEN)
message.setPosition(0.04, 0.87)
message.fontSize(16)

manager = None
device = None
found = []
buttons = []
channel_rows = []
signals = []
fs = 1000.0


def normalize_mac(value):
    return str(value).replace(':', '').replace('-', '').replace(' ', '').upper()


def clear_buttons():
    for b in buttons:
        try:
            b.remove()
        except Exception:
            pass
    buttons[:] = []


def clear_channels():
    for row in channel_rows:
        if row['line'] is not None:
            row['line'].remove()
        row['label'].remove()
    channel_rows[:] = []
    signals[:] = []


def disconnect():
    global device
    if device is not None:
        try:
            device.StopAcquisition()
        except Exception as e:
            print('StopAcquisition:', repr(e))
        try:
            device.Disconnect()
        except Exception as e:
            print('Disconnect:', repr(e))
    device = None
    clear_channels()


def connect_selected(mac):
    global device, fs
    if device is not None:
        status.message('Disconnect first to select another device.')
        return
    if not any(item['mac'] == mac for item in found):
        status.message('Selected device is not in latest scan. Rescan.')
        return
    candidate = None
    try:
        status.message('Connecting to %s...' % mac)
        candidate = manager.GetBluetoothDevice(int(mac, 16))
        group = candidate.BioPotentialSignals
        count = int(group.Count)
        if count < 1:
            raise RuntimeError('No enabled biopotential channels. Configure in BioCapture.')
        fs = float(group.SamplesPerSecond)
        if fs <= 0:
            raise RuntimeError('Invalid sampling rate: %s' % fs)
        capacity = max(10, int(WINDOW_SEC * fs))
        clear_channels()
        for i in range(count):
            signals.append(group[i])  # Confirmed working for this SDK, unlike group.Item(i)
            if i < MAX_DISPLAY_CHANNELS:
                label = viz.addText('CH %d: waiting for data' % (i + 1), parent=viz.SCREEN)
                label.setPosition(0.04, 0.78 - i * 0.085)
                label.fontSize(14)
                channel_rows.append({'label': label, 'line': None,
                                     'values': deque(maxlen=capacity), 'total': 0})
        candidate.StartAcquisition()
        device = candidate
        clear_buttons()
        message.message('Live EMG: last %.0f s | Press D to disconnect, R to rescan' % WINDOW_SEC)
        status.message('CONNECTED: %s | %d channel(s) | %.0f Hz' % (mac, count, fs))
        print('Acquisition started for %s, %d channels, %.0f Hz' % (mac, count, fs))
        if count > MAX_DISPLAY_CHANNELS:
            print('Only first %d channels plotted.' % MAX_DISPLAY_CHANNELS)
    except Exception as e:
        print('Connection error:', repr(e))
        status.message('Connection error - see Vizard Output')
        if candidate is not None:
            try:
                candidate.Disconnect()
            except Exception:
                pass
        device = None
        clear_channels()


def scan():
    global manager
    if device is not None:
        status.message('Press D to disconnect before rescanning.')
        return
    clear_buttons()
    found[:] = []
    status.message('Scanning for BioRadio devices...')
    try:
        if manager is None:
            import clr
            dll = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'BioRadioSDK.dll')
            if not os.path.isfile(dll):
                raise RuntimeError('Place BioRadioSDK.dll in the same folder as this script')
            clr.AddReference(dll)
            from GLNeuroTech.Devices.BioRadio import BioRadioDeviceManager
            manager = BioRadioDeviceManager()
        devices = manager.DiscoverBluetoothDevices()
        for i in range(devices.Length):
            item = devices[i]
            mac = normalize_mac(item.MacId)
            if not any(d['mac'] == mac for d in found):
                found.append({'name': str(item.DeviceId), 'mac': mac})
                print('Discovered: %s | MAC %s' % (item.DeviceId, mac))
        if not found:
            message.message('No BioRadio detected. Check power/Bluetooth; press R to rescan.')
            status.message('No devices found')
            return
        message.message('Available devices: click the one you want to connect')
        for i, item in enumerate(found):
            # Native Vizard GUI button, click with mouse.
            b = viz.addButtonLabel('%s | %s' % (item['name'], item['mac']))
            b.setPosition(0.32, 0.78 - i * 0.09)
            b.setScale(1.8, 1.3)
            vizact.onbuttonup(b, connect_selected, item['mac'])
            buttons.append(b)
        status.message('%d BioRadio device(s) available' % len(found))
    except Exception as e:
        print('Discovery error:', repr(e))
        status.message('Discovery error - see Vizard Output')


def draw_waveform(row, channel_index):
    if row['line'] is not None:
        row['line'].remove()
        row['line'] = None
    values = row['values']
    if len(values) < 2:
        return
    left, right = 0.04, 0.95
    center_y = 0.73 - channel_index * 0.085
    half_height = 0.027
    half_range = max(MIN_HALF_RANGE_MV, max(abs(v) for v in values) * 1.15)
    n = len(values)
    stride = max(1, int(math.ceil(n / 500.0)))
    viz.startLayer(viz.LINE_STRIP)
    viz.lineWidth(2)
    viz.vertexColor(0.0, 0.85, 0.3)
    for j in range(0, n, stride):
        x = left + (right - left) * j / float(n - 1)
        y = center_y + half_height * values[j] / half_range
        viz.vertex(x, y, 0)
    row['line'] = viz.endLayer(parent=viz.SCREEN)


def update():
    if device is None:
        return
    for i, sig in enumerate(signals):
        try:
            batch = sig.GetScaledValueArray()
            if batch is None:
                continue
            values = [float(v) * TO_MV for v in batch]
            if not values or i >= len(channel_rows):
                continue
            row = channel_rows[i]
            row['values'].extend(values)
            row['total'] += len(values)
            rms = math.sqrt(sum(v * v for v in values) / len(values))
            row['label'].message('CH %d | Raw %.3f mV | Batch RMS %.3f mV | N=%d' %
                                 (i + 1, values[-1], rms, row['total']))
            draw_waveform(row, i)
        except Exception as e:
            print('CH %d read/plot error: %s' % (i + 1, repr(e)))
            status.message('Read/plot error - see Output')


def on_disconnect():
    disconnect()
    status.message('Disconnected. Press R to discover and select again.')
    message.message('Press R to search for available BioRadio devices.')


vizact.onkeydown('r', scan)
vizact.onkeydown('d', on_disconnect)
vizact.ontimer(UPDATE_SEC, update)
viz.callback(viz.EXIT_EVENT, disconnect)
scan()
