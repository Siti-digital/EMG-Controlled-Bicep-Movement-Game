# EMG Controlled Bicep Movement

Real-time **Surface Electromyography (sEMG) Controlled 3D Weightlifting Game** built using a **BioRadio** wearable biopotential sensor, a **Python** real-time DSP bridge, and **Unity 6 (`6000.6.7f1`)**.

Contracting your biceps brachii sends microvolt-level muscle electrical activity over Bluetooth to Python, which filters the signal, computes a calibrated moving-window RMS muscle activation (`0.0` to `1.0`), and streams it at **50 Hz over UDP (`127.0.0.1:9050`)** to drive a 3D avatar performing bicep curls in Unity.

---

## Key Features

- **Live BioRadio EMG Acquisition (`bioradio_unity_bridge.py`):**
  - Connects to the BioRadio device over Bluetooth via `BioRadioSDK.dll` (`pythonnet`).
  - Streams live plots for biopotential channels (`CH 1`, `CH 2`) and real-time `activation` sent to Unity.
  - Supports **Two-Point Per-Load Calibration** (`No load` & `6 kg`):
    - **Cal REST (5 s):** Captures resting baseline (95th percentile) — solves the *6 kg isometric hold paradox* so simply holding a dumbbell with your arm down does not lift the virtual arm.
    - **Cal MAX (8 s):** Captures peak bicep curls (99th percentile).
    - Normalizes activation with a **5% dead zone** and selectable exponential smoothing (`Off`, `100 ms`, `200 ms`).
  - Logs raw, filtered, RMS envelope, and activation telemetry to CSV.

- **Four Real-Time Causal DSP Filters (`emg_methods.py`):**
  - **IIR (Raima):** 4th-order Butterworth bandpass (`20–450 Hz`, Second-Order Sections `sosfilt`) + `50 Hz` IIR notch filter.
  - **FIR (Kunal):** 101-tap causal linear-phase Hamming-window FIR bandpass (`20–450 Hz`) + `50 Hz` notch filter.
  - **FFT (Chandan):** Block spectral masking over a 256-sample sliding window (`20–450 Hz`, notch around `50 ± 2 Hz`).
  - **DIFF (Krishna):** First-order discrete difference high-pass operator + `50 Hz` notch filter.
  - All four filters maintain causal streaming state (`zi`) and feed a **100 ms moving-window RMS** envelope detector.

- **3D Unity Weightlifting Game (`EMGWeightlifting_UnityProject` & `EMGWeightlifting_Build`):**
  - 3D studio gym environment with dynamic biceps flexion swelling, target ghost guide, effort meter, timer ring, streaks, multipliers, and CSV session logging (`GameLogger.cs`).
  - Two game modes: **HOLD** (match low/mid/high target angles) and **REPS** (timed curling repetitions), under **NO LOAD** or **6 kg** settings.
  - Built-in **Keyboard fallback mode** so you can test the game without hardware attached.
  - Includes both the clean Unity source project and the ready-to-run **Windows Standalone executable (`EMGWeightlifting.exe`)**.

- **Offline Replay & Simulation Tools:**
  - `replay_sender.py`: Replays recorded CSV EMG sessions (`LOADED/`, `UNLOADED/`) into Unity over UDP.
  - `fake_sender.py`: Generates a smooth synthetic activation wave for testing UDP connectivity.

---

## Repository Structure

```text
.
├── EMGWeightlifting_Build/          # Ready-to-run Windows Standalone game (EMGWeightlifting.exe)
├── EMGWeightlifting_UnityProject/   # Clean Unity 6 (6000.6.7f1) source project (Assets, Packages, ProjectSettings)
├── BioRadio_Student_Unity_Kit/
│   ├── Python_Bridge/
│   │   ├── bioradio_unity_bridge.py # Live BioRadio GUI, 2-point calibration & 50 Hz UDP sender (port 9050)
│   │   ├── emg_methods.py           # Streaming IIR, FIR, FFT, and DIFF EMG filters + RMS envelope
│   │   ├── replay_sender.py         # Replay recorded EMG CSV files to Unity
│   │   ├── fake_sender.py           # Synthetic test signal sender
│   │   ├── BioRadioSDK.dll          # BioRadio .NET assembly used by pythonnet
│   │   ├── LOADED/                  # Recorded 4-subject EMG datasets (loaded curls)
│   │   └── UNLOADED/                # Recorded 4-subject EMG datasets (unloaded curls)
│   └── Unity_Scripts/               # Minimal starter UDP script (BioRadioUdpMover.cs)
├── Launch_Game_With_BioRadio.bat    # One-click launcher: Standalone game + live BioRadio bridge
├── Launch_Game_With_Replay.bat      # One-click launcher: Standalone game + recorded CSV replay
├── EMG_Signal_Processing_DETAILED GUIDE.pdf  # 48-page mathematical & engineering guide
└── EMG_Signal_Processing_SUMMARY GUIDE.pdf   # 10-page A-to-Z project overview
```

---

## Quick Start (Windows)

### 1. Install Python Dependencies
```bash
pip install numpy scipy matplotlib pandas pythonnet
```

### 2. Run with Real BioRadio Hardware
1. Double-click **`Launch_Game_With_BioRadio.bat`** (or run `EMGWeightlifting.exe` and `python BioRadio_Student_Unity_Kit/Python_Bridge/bioradio_unity_bridge.py`).
2. In the Python GUI: click **Scan** $\rightarrow$ select your BioRadio $\rightarrow$ **Connect**.
3. Relax your arm straight $\rightarrow$ click **Cal REST (5 s)**.
4. Do 3 strong curls $\rightarrow$ click **Cal MAX (8 s)**.
5. In the game window, press **`K`** to switch from **KEYBOARD** to **EMG** mode.

### 3. Run Without Hardware (Dataset Replay or Keyboard)
- **Dataset Replay:** Double-click **`Launch_Game_With_Replay.bat`**, then press **`K`** inside the game to watch the arm follow recorded EMG data (`LOADED/KUNAL LOADED.csv`).
- **Keyboard Only:** Open `EMGWeightlifting_Build/EMGWeightlifting.exe` and hold **`↑` / `↓` Arrow** keys to curl.

---

## In-Game Controls

| Key | Action |
| :--- | :--- |
| **`Space`** | Start challenge round |
| **`K`** | Toggle input source: `KEYBOARD` $\leftrightarrow$ `EMG` (UDP Port `9050`) |
| **`↑` / `↓`** | Lift / lower arm (when in `KEYBOARD` mode) |
| **`F`** | Cycle filter label (`IIR` / `FIR` / `FFT` / `DIFF`) |
| **`M`** | Switch challenge mode (`HOLD` $\leftrightarrow$ `REPS`) |
| **`L`** | Switch weight condition (`NO LOAD` $\leftrightarrow$ `6 kg`) |
| **`V`** | Cycle 3D camera angles |
| **`H`** | Show controls hint |
| **`Esc`** | Pause / Quit menu |

---

## Documentation Included

- **`EMG_Signal_Processing_DETAILED GUIDE.pdf`** — Comprehensive guide covering sEMG electrophysiology, BioRadio differential acquisition, noise artifacts, LTI filter theory, IIR/FIR/FFT/DIFF mathematical design, RMS envelope detection, two-point calibration, and UDP localhost networking.
- **`EMG_Signal_Processing_SUMMARY GUIDE.pdf`** — Concise step-by-step A-to-Z walkthrough of the system.
