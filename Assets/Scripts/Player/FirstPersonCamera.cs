using UnityEngine;

/// <summary>
/// First-Person Camera Controller for the player.
/// Supports mouse look, head bobbing, and smooth movement.
/// Toggle between this and third-person via CameraModeManager.
/// </summary>
public class FirstPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The transform to follow (player's head position)")]
    public Transform target;
    [Tooltip("Offset from target position (e.g., eye height)")]
    public Vector3 offset = new Vector3(0f, 1.6f, 0f);

    [Header("Mouse Look")]
    public float mouseSensitivity = 2f;
    public float smoothing = 2f;
    public float minPitch = -80f;
    public float maxPitch = 80f;
    public bool invertY = false;

    [Header("Head Bob")]
    public bool enableHeadBob = true;
    public float bobFrequency = 1.5f;
    public float bobAmplitudeY = 0.05f;
    public float bobAmplitudeX = 0.02f;

    [Header("Field of View")]
    public float baseFOV = 70f;
    public float sprintFOV = 85f;
    public float fovSmoothSpeed = 5f;

    // Internal state
    private Vector2 currentMouseDelta;
    private Vector2 smoothedMouseDelta;
    private float yaw;
    private float pitch;
    private float bobTimer;
    private Camera cam;
    private bool isActive = false;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
            cam = Camera.main;
    }

    private void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                target = player.transform;
        }

        // Initialize rotation from current camera angles
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
        if (pitch > 180f) pitch -= 360f;
    }

    private void OnEnable()
    {
        isActive = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Sync rotation with current state
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
        if (pitch > 180f) pitch -= 360f;
    }

    private void OnDisable()
    {
        isActive = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (!isActive || target == null) return;

        HandleMouseLook();
        HandleHeadBob();
        HandleFOV();
    }

    private void LateUpdate()
    {
        if (!isActive || target == null) return;

        UpdateCameraPosition();
    }

    private void HandleMouseLook()
    {
        // Raw mouse input
        currentMouseDelta.x = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        currentMouseDelta.y = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        if (invertY)
            currentMouseDelta.y *= -1f;

        // Smooth the input
        smoothedMouseDelta = Vector2.Lerp(smoothedMouseDelta, currentMouseDelta, 1f / smoothing);

        // Apply rotation
        yaw += smoothedMouseDelta.x;
        pitch -= smoothedMouseDelta.y;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        // Rotate the player body horizontally (optional — only yaw)
        if (target != null)
        {
            target.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }

    private void HandleHeadBob()
    {
        if (!enableHeadBob) return;

        // Check if player is moving (simple ground velocity check)
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        bool isMoving = Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f;

        if (isMoving)
        {
            bobTimer += Time.deltaTime * bobFrequency;
        }
        else
        {
            bobTimer = Mathf.Lerp(bobTimer, 0f, Time.deltaTime * 2f);
        }
    }

    private void HandleFOV()
    {
        if (cam == null) return;

        // Sprint detection (optional — uses Left Shift)
        bool isSprinting = Input.GetKey(KeyCode.LeftShift);
        float targetFOV = isSprinting ? sprintFOV : baseFOV;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, fovSmoothSpeed * Time.deltaTime);
    }

    private void UpdateCameraPosition()
    {
        Vector3 targetPos = target.position + offset;

        // Apply head bob offset
        if (enableHeadBob && bobTimer > 0f)
        {
            float bobX = Mathf.Sin(bobTimer * Mathf.PI * 2f) * bobAmplitudeX;
            float bobY = Mathf.Sin(bobTimer * Mathf.PI * 4f) * bobAmplitudeY;
            targetPos += transform.right * bobX + transform.up * bobY;
        }

        transform.position = targetPos;
    }

    /// <summary>
    /// Get the forward direction the camera is looking (useful for aiming).
    /// </summary>
    public Vector3 GetLookDirection()
    {
        return transform.forward;
    }
}
