BIORADIO EMG -> UNITY STUDENT KIT
================================
Plain black-and-white instructions

WHAT IS INSIDE
1. SDK/Original_BioRadio_SDK.zip - original SDK supplied by the instructor.
2. Python_Bridge/BioRadioSDK.dll - SDK library for Python on Windows.
3. Python_Bridge/bioradio_unity_bridge.py - choose a BioRadio, view live EMG plots, send channel data to Unity.
4. Unity_Scripts/BioRadioUdpMover.cs - Unity script to move a Cube using EMG RMS.
5. Vizard8_Optional/ - optional Vizard 8 demo with device selection and plots.

YOU NEED
- Windows laptop with Bluetooth and the BioRadio hardware.
- BioCapture (manufacturer software) for initial device setup and channel configuration.
- Python 3 compatible with pythonnet and the SDK .NET runtime.
- Unity Editor with a 3D project.
- BioRadio SDK DLL is supplied here; no Android APK is needed for this Windows-to-Unity setup.
- Internet connection only for installing Python packages and Unity if not installed.

STEP 1: CHECK THE DEVICE
1. Turn on your BioRadio.
2. In BioCapture, confirm that the EMG channel shows a signal.
3. Check which channels are enabled, then CLOSE BioCapture before using the bridge.
4. If Windows has blocked BioRadioSDK.dll, right-click it -> Properties -> Unblock.

STEP 2: INSTALL PYTHON LIBRARIES
1. Open Windows PowerShell (not the Vizard Output/Command window).
2. Run: py -m pip install pythonnet matplotlib
3. If Python reports a .NET loading error, ask the instructor to check runtime and Python compatibility.

STEP 3: START LIVE EMG
1. Open PowerShell in the Python_Bridge folder.
2. Run: py bioradio_unity_bridge.py
3. Click Scan. Wait for the list of devices.
4. Select the desired BioRadio using the drop-down box.
5. Click Connect. Live plots should appear for all enabled biopotential channels.
6. When done, click Disconnect before changing devices.

STEP 4: CONNECT UNITY
1. Create a new Unity 3D project.
2. Create a Cube (GameObject -> 3D Object -> Cube).
3. Copy BioRadioUdpMover.cs into the project's Assets folder.
4. Attach BioRadioUdpMover to the Cube.
5. Press Play in Unity while the Python bridge is running.
6. In Inspector, choose channelToUse (1, 2, etc.).
7. Adjust activationThresholdMv and maxExpectedRmsMv for your muscle and recording.
8. Contract the muscle. The Cube should move to the right when RMS is above threshold.

DATA FLOW
BioRadio -> SDK -> Python live plot -> UDP 127.0.0.1:50557 -> Unity -> Cube

TWO KNOWN DEVICES (EXAMPLES ONLY; THE PROGRAM SCANS LIVE)
BioRadio AOP: ECFE7E1836AF
BioRadio AOO: ECFE7E1836BF

IMPORTANT NOTES
- This is a Windows desktop setup. It is not an Android app; there is no APK.
- Python and Unity must run on the same computer for the default localhost UDP address.
- Unity does NOT connect to BioRadio Bluetooth directly. Python receives the EMG first.
- The program sends one UDP JSON message per channel per update. Each message contains channel, raw_mv, rms_mv, sample_count, sample_rate_hz.
- RMS is calculated from the latest batch of samples, not a fixed-length clinical EMG window.
- Verify SDK voltage units against BioCapture before trusting mV values.
- Calibrate thresholds for each person and task; the values in Unity are placeholders.
- This is a teaching demonstration, not a clinical measurement or medical device.
- Only enabled channels are shown. Enable additional channels in BioCapture before closing it.
- If no device appears, check Bluetooth, power, proximity, and that BioCapture is closed.
- If a DLL fails to load, check Windows file blocking and .NET dependencies.
- The SDK is an older vendor release; compatibility with modern Python/Windows is not guaranteed.
- Keep SDK distribution within the institution unless vendor redistribution permission is confirmed.

INSTRUCTOR CHECK BEFORE CLASS
Test the supplied SDK and code on the lab Windows computer with a real BioRadio.
Confirm that live plots update, that Unity receives UDP, and that the Cube moves.
