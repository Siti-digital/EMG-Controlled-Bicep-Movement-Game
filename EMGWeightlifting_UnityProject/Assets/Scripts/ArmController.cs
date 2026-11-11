using UnityEngine;

public class ArmController : MonoBehaviour
{
    public Transform elbowPivot;
    public UDPReceiver receiver;
    public bool useKeyboard = true;

    public float maxAngle = 140f;
    public float restActivation = 0.03f; // below this the arm stays straight
    public float requiredActivation = 0.20f; // effort needed for a full curl (set by GameManager)
    public float signalTimeout = 0.3f; // seconds without data before the arm lowers

    [Header("3D Biceps Flexion Effect")]
    public Transform bicepMuscle;
    public Vector3 baseBicepScale = Vector3.zero;

    public float CurrentActivation { get; private set; }
    public float CurrentAngle { get; private set; }

    float keyEffort;

    void Start()
    {
        if (bicepMuscle != null && baseBicepScale == Vector3.zero)
        {
            baseBicepScale = bicepMuscle.localScale;
        }
    }

    void Update()
    {
        if (useKeyboard)
        {
            // Up arrow lifts, Down arrow lowers; releasing naturally returns arm to rest
            float keySpeed = 1.8f;
            if (Input.GetKey(KeyCode.UpArrow))
            {
                keyEffort += keySpeed * Time.deltaTime;
            }
            else if (Input.GetKey(KeyCode.DownArrow))
            {
                keyEffort -= keySpeed * 1.5f * Time.deltaTime;
            }
            else
            {
                // Smoothly lower if no key held
                keyEffort -= 1.2f * Time.deltaTime;
            }

            keyEffort = Mathf.Clamp01(keyEffort);
            CurrentActivation = Mathf.Lerp(restActivation, requiredActivation, keyEffort);
        }
        else if (receiver != null && receiver.TimeSinceLastPacket < signalTimeout)
        {
            CurrentActivation = receiver.Activation;
        }
        else
        {
            // Signal lost: lower the arm slowly (safe state)
            CurrentActivation = Mathf.MoveTowards(CurrentActivation, 0f, 0.5f * Time.deltaTime);
        }

        float lift = Mathf.InverseLerp(restActivation, requiredActivation, CurrentActivation);
        CurrentAngle = lift * maxAngle;

        if (elbowPivot != null)
        {
            elbowPivot.localRotation = Quaternion.Euler(-CurrentAngle, 0f, 0f);
        }

        // 3D Biceps dynamic contraction / swell biofeedback
        if (bicepMuscle != null)
        {
            if (baseBicepScale == Vector3.zero) baseBicepScale = bicepMuscle.localScale;
            float bulge = Mathf.Clamp01(CurrentActivation / Mathf.Max(0.01f, requiredActivation));
            bicepMuscle.localScale = new Vector3(
                baseBicepScale.x * (1f + 0.45f * bulge),
                baseBicepScale.y * (1f - 0.08f * bulge),
                baseBicepScale.z * (1f + 0.50f * bulge)
            );
        }
    }
}
