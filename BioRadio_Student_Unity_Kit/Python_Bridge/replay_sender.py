"""
replay_sender.py - plays a recorded EMG file into the Unity game as if it were live.

Examples:
  python replay_sender.py S3_Kunal_6kg_raw.csv --filter B
  python replay_sender.py S3_Kunal_NoLoad_raw.csv --filter A --calib S3_Kunal_6kg_raw.csv --loop
"""
import argparse
import json
import socket
import time
import numpy as np
import pandas as pd
from scipy import signal

FS = 1000 # samples per second
CHUNK = 20 # send one value every 20 samples (20 ms)
WINDOWS = {"A": 0.025, "B": 0.100, "C": 0.200}
UNITY = ("127.0.0.1", 9050) # same port as the BioRadio app

def load_uv(path):
    df = pd.read_csv(path)
    if "ch1_mv" in df.columns:
        col = "ch1_mv"
    elif "emg_raw_mV" in df.columns:
        col = "emg_raw_mV"
    elif "raw_mV" in df.columns:
        col = "raw_mV"
    else:
        col = df.columns[1] if len(df.columns) > 1 else df.columns[0]
    return df[col].to_numpy() * 1000.0 # mV -> uV

def causal_envelope(raw_uv, window_s):
    """Same processing as the BioRadio app (bioradio_unity_bridge.py): causal band-pass + notch, then causal moving RMS."""
    sos = signal.butter(4, (20, 450), btype="bandpass", fs=FS, output="sos")
    b, a = signal.iirnotch(50, 30, fs=FS)
    y = signal.lfilter(b, a, signal.sosfilt(sos, raw_uv, zi=signal.sosfilt_zi(sos) * raw_uv[0])[0])
    n = int(window_s * FS)
    c = np.cumsum(np.insert(y ** 2, 0, 0.0))
    idx = np.arange(1, len(y) + 1)
    lo = np.maximum(0, idx - n)
    return np.sqrt((c[idx] - c[lo]) / (idx - lo))

def main():
    p = argparse.ArgumentParser()
    p.add_argument("file", help="recorded CSV (raw or labelled)")
    p.add_argument("--filter", choices=["A", "B", "C"], default="B")
    p.add_argument("--calib", help="file used for scaling to 0-1 (default: the same file); use the person's 6 kg file")
    p.add_argument("--loop", action="store_true", help="start again when the file ends")
    args = p.parse_args()

    window = WINDOWS[args.filter]
    env = causal_envelope(load_uv(args.file), window)
    calib_env = causal_envelope(load_uv(args.calib), window) if args.calib else env
    scale = np.percentile(calib_env, 99) # person-specific calibration: their strongest activity
    if scale <= 0:
        scale = 1.0
    activation = np.clip(env / scale, 0.0, 1.0)

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    print(f"Replaying {args.file} with filter {args.filter} ({int(window * 1000)} ms), "
          f"{len(activation) / FS:.0f} s, calibration {scale:.1f} uV")

    while True:
        start = time.perf_counter()
        for i in range(CHUNK, len(activation), CHUNK):
            # wait until this chunk's real time, so playback runs at the original speed
            delay = start + i / FS - time.perf_counter()
            if delay > 0:
                time.sleep(delay)
            msg = {
                "channel": 1,
                "sender_time": round(time.time(), 3),
                "activation": round(float(activation[i - 1]), 4),
                "filter": args.filter
            }
            sock.sendto(json.dumps(msg).encode(), UNITY)
            if i % 5000 == 0:
                print(f" {i / FS:5.0f} s activation {activation[i - 1]:.2f}")
        if not args.loop:
            break
    print("Finished.")

if __name__ == "__main__":
    main()
