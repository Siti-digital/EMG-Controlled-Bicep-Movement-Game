using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public enum Mode { Hold, Reps }

    [Header("Scene references")]
    public ArmController arm;
    public Transform ghostPivot;
    public Renderer ghostRenderer;
    public GameLogger logger;
    public WindowController windowController;

    [Header("UI references - Top Bar")]
    public TMP_Text modeChipText;
    public TMP_Text levelChipText;
    public TMP_Text inputChipText;
    public TMP_Text playerText;
    public Button modeChipBtn;
    public Button levelChipBtn;
    public Button inputChipBtn;
    public Button minBtn;
    public Button maxBtn;
    public Button closeBtn;

    [Header("UI references - Score Card")]
    public TMP_Text scoreText;
    public TMP_Text multiplierText;
    public TMP_Text streakText;
    public TMP_Text zoneText;

    [Header("UI references - Effort Meter & Timer")]
    public TMP_Text effortPercentText;
    public RectTransform effortFill;
    public RectTransform targetBand;
    public RectTransform targetMarkerLine;
    public Image timerRing;
    public TMP_Text timerText;
    public TMP_Text secLabel;

    [Header("UI references - Floating Badges & Hints")]
    public TMP_Text hintText;
    public TMP_Text inZoneText;
    public GameObject pausePanel;

    [Header("Settings")]
    public Mode mode = Mode.Hold;
    public bool heavy = true; // default 6 kg as shown in reference image
    public float lightRequired = 0.60f;
    public float heavyRequired = 0.60f;
    public float tolerance = 12f;
    public int totalRounds = 3;

    [Header("Hold challenge")]
    public float[] holdTargets = { 40f, 80f, 120f };
    public string[] holdNames = { "LOW (40°)", "MID (80°)", "HIGH (120°)" };
    public float holdTime = 10f;
    public float holdRest = 4f;

    [Header("Rep challenge")]
    public float repDuration = 60f;
    public float repHalfPeriod = 2f;
    public float repTopAngle = 120f;

    [Header("Fun score")]
    public float pointsPerSecondInZone = 50f;
    public int pointsPerRep = 200;
    public float holdStreakSeconds = 2f;
    public int streakPerMultiplier = 3;
    public int maxMultiplier = 4;

    [Header("3D Camera Views (Press V to toggle)")]
    public Camera cam;
    int currentCameraView = 0;
    readonly Vector3[] camPositions = new Vector3[]
    {
        new Vector3(6.0f, 2.30f, 0.60f),  // 0: Perspective Side View of the Arm
        new Vector3(4.9f, 2.35f, 0.70f),  // 1: Studio Side-Quarter Perspective
        new Vector3(3.2f, 2.25f, 1.35f)   // 2: Lifter & Hex Dumbbell Close-Up
    };
    readonly Vector3[] camRotations = new Vector3[]
    {
        new Vector3(0f, -90f, 0f),
        new Vector3(2.5f, -96f, 0f),
        new Vector3(6f, -112f, 0f)
    };

    static readonly Color GhostDim = new Color(0.15f, 1.00f, 0.35f, 0.35f);
    static readonly Color GhostLit = new Color(0.25f, 1.00f, 0.50f, 0.90f);

    bool running;
    int roundNumber = 2; // Matches reference image "Round 2/3"
    float t, inZoneTime, scoredTime, score = 2840f, zoneRun;
    int reps, repAttempts, lastPhase, streak = 6;
    bool repCounted;
    string endMessage = "";
    bool isPaused = false;

    // Filter selection (IIR = Raima, FIR = Kunal, FFT = Chandan, DIFF = Krishna)
    int currentFilterIndex = 0; // Default: IIR
    readonly string[] filterNames = { "IIR", "FIR", "FFT", "DIFF" };

    float TotalDuration => mode == Mode.Reps ? repDuration : holdTargets.Length * (holdRest + holdTime);
    int Multiplier => Mathf.Min(maxMultiplier, 1 + streak / streakPerMultiplier);

    void Start()
    {
        if (cam == null) cam = Camera.main;
        if (windowController == null) windowController = FindAnyObjectByType<WindowController>();

        // Wire button click listeners
        if (modeChipBtn != null) modeChipBtn.onClick.AddListener(ToggleMode);
        if (levelChipBtn != null) levelChipBtn.onClick.AddListener(ToggleWeight);
        if (inputChipBtn != null) inputChipBtn.onClick.AddListener(ToggleInput);

        if (minBtn != null && windowController != null) minBtn.onClick.AddListener(windowController.MinimizeWindow);
        if (maxBtn != null && windowController != null) maxBtn.onClick.AddListener(windowController.ToggleMaximize);
        if (closeBtn != null && windowController != null) closeBtn.onClick.AddListener(windowController.QuitGame);

        if (pausePanel != null) pausePanel.SetActive(false);
        SetGhost(false);
        if (inZoneText != null) inZoneText.gameObject.SetActive(false);

        ShowScore(74f);
        UpdateChips();
    }

    void Update()
    {
        // ESC key: toggle Pause/Quit menu
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePauseMenu();
        }

        // Input mode toggle: Press K anytime to switch between Keyboard and BioRadio EMG
        if (Input.GetKeyDown(KeyCode.K))
        {
            ToggleInput();
        }

        // Filter cycle: Press F anytime to cycle IIR -> FIR -> FFT -> DIFF
        if (Input.GetKeyDown(KeyCode.F))
        {
            CycleFilter();
        }

        // Camera view toggle with 'V'
        if (Input.GetKeyDown(KeyCode.V))
        {
            currentCameraView = (currentCameraView + 1) % camPositions.Length;
        }

        // Help info with 'H'
        if (Input.GetKeyDown(KeyCode.H))
        {
            if (hintText != null)
            {
                hintText.text = "KEYS: Space: Start | K: Input | F: Filter | M: Mode | L: Weight | ESC: Menu";
            }
        }

        if (cam != null)
        {
            cam.transform.position = Vector3.Lerp(cam.transform.position, camPositions[currentCameraView], Time.deltaTime * 6f);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.Euler(camRotations[currentCameraView]), Time.deltaTime * 6f);
        }

        arm.requiredActivation = heavy ? heavyRequired : lightRequired;
        UpdateChips();

        // Update live effort meter fill and percentage text
        float currentLiftRatio = Mathf.Clamp01(arm.CurrentAngle / arm.maxAngle);
        SetEffortFill(currentLiftRatio);

        if (!running)
        {
            if (Input.GetKeyDown(KeyCode.M)) ToggleMode();
            if (Input.GetKeyDown(KeyCode.L)) ToggleWeight();
            if (Input.GetKeyDown(KeyCode.Space) && !isPaused) { StartRound(); return; }

            if (ghostPivot != null) ghostPivot.localRotation = Quaternion.identity;
            SetTargetBand(0f);
            SetGhost(false);
            if (inZoneText != null) inZoneText.gameObject.SetActive(false);
            if (timerRing != null) timerRing.fillAmount = 1f;
            if (timerText != null) timerText.text = Mathf.CeilToInt(TotalDuration).ToString();
            if (secLabel != null) secLabel.text = "s";
            if (hintText != null && !isPaused)
            {
                hintText.text = string.IsNullOrEmpty(endMessage)
                    ? "Press SPACE to start | M: mode  L: weight  K: input  F: filter  H: help"
                    : endMessage + "Press SPACE to start again";
            }
            return;
        }

        float dt = Time.deltaTime;
        t += dt;
        float target = GetTarget(t, out bool scoring, out float nextTargetTime, out float nextTargetAngle);
        if (ghostPivot != null) ghostPivot.localRotation = Quaternion.Euler(-target, 0f, 0f);
        SetTargetBand(target);

        bool inZone = Mathf.Abs(arm.CurrentAngle - target) <= tolerance;
        bool showZone = scoring && inZone;
        SetGhost(showZone);

        if (inZoneText != null)
        {
            inZoneText.gameObject.SetActive(showZone);
            if (showZone)
            {
                inZoneText.transform.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(Time.time * 8f));
            }
        }

        if (scoring)
        {
            scoredTime += dt;
            if (inZone)
            {
                inZoneTime += dt;
                score += pointsPerSecondInZone * Multiplier * dt;
            }
        }

        if (mode == Mode.Hold)
        {
            if (scoring && inZone)
            {
                zoneRun += dt;
                if (zoneRun >= holdStreakSeconds) { streak++; zoneRun = 0f; }
            }
            else if (scoring)
            {
                streak = 0;
                zoneRun = 0f;
            }

            if (hintText != null)
            {
                if (scoring)
                {
                    hintText.text = $"Hold steady! Next target: {nextTargetAngle:F0}° in {Mathf.CeilToInt(nextTargetTime)} s";
                }
                else
                {
                    hintText.text = $"Get ready! Target {nextTargetAngle:F0}° in {Mathf.CeilToInt(nextTargetTime)} s";
                }
            }
        }
        else
        {
            int phase = Mathf.FloorToInt(t / repHalfPeriod);
            if (phase != lastPhase)
            {
                if (lastPhase >= 0 && lastPhase % 2 == 1 && !repCounted)
                    streak = 0; // missed the top
                lastPhase = phase;
                repCounted = false;
                if (phase % 2 == 1) repAttempts++;
            }
            if (phase % 2 == 1 && !repCounted && inZone)
            {
                reps++;
                repCounted = true;
                streak++;
                score += pointsPerRep * Multiplier;
            }
            if (hintText != null)
            {
                hintText.text = (phase % 2 == 1) ? "LIFT to the top! (120°)" : "Lower down smoothly...";
            }
        }

        if (logger != null) logger.Log(t, target, inZone, scoring);

        float pct = scoredTime > 0f ? 100f * inZoneTime / scoredTime : 0f;
        ShowScore(pct);

        float left = Mathf.Max(0f, TotalDuration - t);
        if (timerText != null) timerText.text = Mathf.CeilToInt(left).ToString();
        if (secLabel != null) secLabel.text = "s";
        if (timerRing != null) timerRing.fillAmount = left / TotalDuration;

        if (t >= TotalDuration)
        {
            running = false;
            if (logger != null)
            {
                logger.EndRound();
                logger.WriteSummary(mode.ToString(), heavy ? "HEAVY" : "LIGHT", pct, reps, repAttempts);
            }
            endMessage = $"Round over! Score {Mathf.RoundToInt(score):N0}, in zone {pct:F0}%. ";
            roundNumber = Mathf.Min(roundNumber + 1, totalRounds);
        }
    }

    public void ToggleMode()
    {
        if (running) return;
        mode = (mode == Mode.Hold) ? Mode.Reps : Mode.Hold;
        UpdateChips();
    }

    public void ToggleWeight()
    {
        if (running) return;
        heavy = !heavy;
        UpdateChips();
    }

    public void ToggleInput()
    {
        if (arm != null)
        {
            arm.useKeyboard = !arm.useKeyboard;
        }
        UpdateChips();
    }

    public void CycleFilter()
    {
        currentFilterIndex = (currentFilterIndex + 1) % filterNames.Length;
        UpdateChips();
        if (hintText != null && !running && !isPaused)
        {
            hintText.text = $"Filter switched to: Filter {filterNames[currentFilterIndex]}";
        }
    }

    public void TogglePauseMenu()
    {
        isPaused = !isPaused;
        if (pausePanel != null) pausePanel.SetActive(isPaused);
        Time.timeScale = isPaused ? 0f : 1f;
    }

    public void ResumeGame()
    {
        isPaused = false;
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void RestartRound()
    {
        ResumeGame();
        StartRound();
    }

    public void StartRound()
    {
        running = true;
        t = 0f; inZoneTime = 0f; scoredTime = 0f; zoneRun = 0f;
        reps = 0; repAttempts = 0; lastPhase = -1; repCounted = false;
        endMessage = "";
        if (logger != null) logger.BeginRound(mode.ToString(), heavy ? "HEAVY" : "LIGHT");
    }

    float GetTarget(float time, out bool scoring, out float nextTargetTime, out float nextTargetAngle)
    {
        if (mode == Mode.Reps)
        {
            scoring = true;
            nextTargetTime = repHalfPeriod - (time % repHalfPeriod);
            nextTargetAngle = repTopAngle;
            return (Mathf.FloorToInt(time / repHalfPeriod) % 2 == 1) ? repTopAngle : 0f;
        }

        float cycle = holdRest + holdTime;
        int i = Mathf.Min(Mathf.FloorToInt(time / cycle), holdTargets.Length - 1);
        float within = time - i * cycle;

        int nextIdx = Mathf.Min(i + 1, holdTargets.Length - 1);
        nextTargetAngle = holdTargets[nextIdx];
        nextTargetTime = Mathf.Max(0f, cycle - within);

        if (within < holdRest)
        {
            scoring = false;
            nextTargetAngle = holdTargets[i];
            nextTargetTime = holdRest - within;
            return 0f; // Rest before hold
        }

        scoring = true;
        return holdTargets[i];
    }

    void ShowScore(float pct)
    {
        if (scoreText != null) scoreText.text = Mathf.RoundToInt(score).ToString("N0");
        if (multiplierText != null) multiplierText.text = "x" + Multiplier;
        if (streakText != null) streakText.text = $"Streak: {streak} in a row";
        if (zoneText != null)
        {
            zoneText.text = mode == Mode.Reps
                ? $"Reps: {reps}/{repAttempts}  •  Time in zone: {pct:F0}%"
                : $"Time in zone: {pct:F0}%";
        }
    }

    void UpdateChips()
    {
        if (modeChipText != null) modeChipText.text = mode == Mode.Hold ? "HOLD" : "REPS";
        if (levelChipText != null) levelChipText.text = heavy ? "6 kg" : "NO LOAD";
        if (inputChipText != null && arm != null)
        {
            string tag = (arm.receiver != null && !string.IsNullOrEmpty(arm.receiver.FilterTag) && arm.receiver.FilterTag != "NA")
                ? arm.receiver.FilterTag
                : filterNames[currentFilterIndex];
            inputChipText.text = arm.useKeyboard ? "KEYBOARD" : $"EMG [:{UDPReceiver.Port}] a={arm.receiver.Activation:F2} req={arm.requiredActivation:F2} age={arm.receiver.TimeSinceLastPacket * 1000f:F0}ms";
        }
        if (playerText != null)
        {
            string subj = (logger != null && !string.IsNullOrEmpty(logger.subjectId)) ? logger.subjectId : "P1";
            playerText.text = $"{subj}  •  Round {roundNumber}/{totalRounds}";
        }
    }

    void SetEffortFill(float liftRatio)
    {
        if (effortFill != null)
        {
            effortFill.anchorMin = Vector2.zero;
            effortFill.anchorMax = new Vector2(1f, Mathf.Clamp01(liftRatio));
            effortFill.offsetMin = Vector2.zero;
            effortFill.offsetMax = Vector2.zero;
        }

        if (effortPercentText != null)
        {
            int pct = Mathf.RoundToInt(liftRatio * 100f);
            effortPercentText.text = pct + "%";
        }
    }

    void SetTargetBand(float targetAngle)
    {
        float c = targetAngle / arm.maxAngle;
        float h = tolerance / arm.maxAngle;

        if (targetBand != null)
        {
            targetBand.anchorMin = new Vector2(0f, Mathf.Clamp01(c - h));
            targetBand.anchorMax = new Vector2(1f, Mathf.Clamp01(c + h));
            targetBand.offsetMin = Vector2.zero;
            targetBand.offsetMax = Vector2.zero;
        }

        if (targetMarkerLine != null)
        {
            targetMarkerLine.anchorMin = new Vector2(0f, Mathf.Clamp01(c));
            targetMarkerLine.anchorMax = new Vector2(1f, Mathf.Clamp01(c));
            targetMarkerLine.anchoredPosition = Vector2.zero;
        }
    }

    void SetGhost(bool lit)
    {
        if (ghostRenderer == null) return;
        Material m = ghostRenderer.material;
        Color c = lit ? GhostLit : GhostDim;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        else m.color = c;

        if (m.HasProperty("_EmissionColor"))
        {
            Color emission = lit ? c * 2.2f : c * 0.8f;
            m.SetColor("_EmissionColor", emission);
            m.EnableKeyword("_EMISSION");
        }
    }
}
