"""
emg_methods.py - Streaming EMG processing methods for SSRP Project 7 (EMG Lift).
Methods:
  * IIR (Raima): 4th-order Butterworth bandpass (20-450 Hz) + 50 Hz notch filter
  * FIR (Kunal): Causal linear-phase FIR bandpass (20-450 Hz) + 50 Hz notch filter
  * FFT (Chandan): Block spectral filtering (zeroing <20 Hz, >450 Hz, and 50 Hz notch)
  * DIFF (Krishna): 1st-order difference high-pass + 50 Hz notch filter
"""
import numpy as np
from scipy import signal
from collections import deque

METHODS = ['IIR', 'FIR', 'FFT', 'DIFF']
OWNER = {'IIR': 'Raima', 'FIR': 'Kunal', 'FFT': 'Chandan', 'DIFF': 'Krishna'}

class Streaming:
    def __init__(self, method, fs=1000.0):
        self.method = method.upper()
        self.fs = float(fs)
        self.history = deque(maxlen=int(0.5 * self.fs))  # buffer of recent processed samples (uV)

        high = min(450.0, 0.45 * self.fs)

        # 1. IIR (Raima)
        self.iir_sos = signal.butter(4, (20.0, high), btype='bandpass', fs=self.fs, output='sos')
        self.notch_b, self.notch_a = signal.iirnotch(50.0, 30.0, fs=self.fs)
        self.zi_bp = None
        self.zi_notch = np.zeros(max(len(self.notch_a), len(self.notch_b)) - 1)

        # 2. FIR (Kunal)
        numtaps = 101
        if numtaps >= int(self.fs / 2):
            numtaps = 51
        self.fir_b = signal.firwin(numtaps, [20.0, high], pass_zero=False, fs=self.fs)
        self.fir_zi = np.zeros(len(self.fir_b) - 1)
        self.zi_fir_notch = np.zeros(max(len(self.notch_a), len(self.notch_b)) - 1)

        # 3. FFT (Chandan)
        self.fft_buf = deque(maxlen=256)

        # 4. DIFF (Krishna)
        self.diff_prev = None

    def process(self, raw_uv):
        x = np.asarray(raw_uv, dtype=float)
        if len(x) == 0:
            return np.array([])

        if self.method == 'IIR':
            if self.zi_bp is None:
                self.zi_bp = signal.sosfilt_zi(self.iir_sos) * x[0]
            y, self.zi_bp = signal.sosfilt(self.iir_sos, x, zi=self.zi_bp)
            y, self.zi_notch = signal.lfilter(self.notch_b, self.notch_a, y, zi=self.zi_notch)

        elif self.method == 'FIR':
            y, self.fir_zi = signal.lfilter(self.fir_b, 1.0, x, zi=self.fir_zi)
            y, self.zi_fir_notch = signal.lfilter(self.notch_b, self.notch_a, y, zi=self.zi_fir_notch)

        elif self.method == 'FFT':
            self.fft_buf.extend(x)
            buf = np.array(self.fft_buf)
            N = len(buf)
            if N >= 32:
                freqs = np.fft.rfftfreq(N, d=1.0 / self.fs)
                X = np.fft.rfft(buf)
                mask = (freqs >= 20.0) & (freqs <= 450.0) & (np.abs(freqs - 50.0) > 2.0)
                X_filt = X * mask
                y_full = np.fft.irfft(X_filt, n=N)
                y = y_full[-len(x):]
            else:
                y = x

        elif self.method == 'DIFF':
            if self.diff_prev is None:
                self.diff_prev = x[0]
            extended = np.insert(x, 0, self.diff_prev)
            y = np.diff(extended)
            self.diff_prev = x[-1]
            y, self.zi_notch = signal.lfilter(self.notch_b, self.notch_a, y, zi=self.zi_notch)

        else:
            if self.zi_bp is None:
                self.zi_bp = signal.sosfilt_zi(self.iir_sos) * x[0]
            y, self.zi_bp = signal.sosfilt(self.iir_sos, x, zi=self.zi_bp)
            y, self.zi_notch = signal.lfilter(self.notch_b, self.notch_a, y, zi=self.zi_notch)

        self.history.extend(y)
        return y

    def envelope(self, window_s=0.100):
        """Causal moving RMS over the most recent window_s seconds (uV)."""
        n = max(1, int(window_s * self.fs))
        h = np.fromiter(self.history, dtype=float)
        if h.size == 0:
            return 0.0
        return float(np.sqrt(np.mean(h[-n:] ** 2)))
