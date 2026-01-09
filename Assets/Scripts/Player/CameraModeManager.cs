using UnityEngine;

/// <summary>
/// Manages switching between camera modes (First-Person / Third-Person).
/// Attach to the main camera or a dedicated manager object.
/// Press the toggle key (default: V) to switch modes.
/// </summary>
public class CameraModeManager : MonoBehaviour
{
    public enum CameraMode
    {
        ThirdPerson,
        FirstPerson
    }

    [Header("Camera Mode")]
    public CameraMode currentMode = CameraMode.ThirdPerson;
    public KeyCode toggleKey = KeyCode.V;

    [Header("Camera References")]
    [Tooltip("Third-person camera component (RotatableTopDownCamera)")]
    public RotatableTopDownCamera thirdPersonCamera;
    [Tooltip("First-person camera component")]
    public FirstPersonCamera firstPersonCamera;

    [Header("Character Hiding")]
    [Tooltip("Hides player mesh in first-person while keeping shadows.")]
    public HideFirstPersonModel hideFirstPerson;

    [Header("Transition")]
    public float transitionDuration = 0.3f;

    private bool isTransitioning = false;

    private void Start()
    {
        // Auto-find cameras if not assigned
        if (thirdPersonCamera == null)
            thirdPersonCamera = FindAnyObjectByType<RotatableTopDownCamera>();

        if (firstPersonCamera == null)
            firstPersonCamera = FindAnyObjectByType<FirstPersonCamera>();

        if (hideFirstPerson == null)
            hideFirstPerson = FindAnyObjectByType<HideFirstPersonModel>();

        // Apply initial mode
        ApplyCameraMode(currentMode, instant: true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey) && !isTransitioning)
        {
            ToggleCameraMode();
        }
    }

    /// <summary>
    /// Toggle between first and third person.
    /// </summary>
    public void ToggleCameraMode()
    {
        CameraMode newMode = (currentMode == CameraMode.ThirdPerson)
            ? CameraMode.FirstPerson
            : CameraMode.ThirdPerson;

        SetCameraMode(newMode);
    }

    /// <summary>
    /// Set a specific camera mode.
    /// </summary>
    public void SetCameraMode(CameraMode mode)
    {
        if (mode == currentMode) return;

        currentMode = mode;
        ApplyCameraMode(mode, instant: false);

        Debug.Log($"Camera Mode: {mode}");
    }

    private void ApplyCameraMode(CameraMode mode, bool instant)
    {
        switch (mode)
        {
            case CameraMode.ThirdPerson:
                if (firstPersonCamera != null)
                    firstPersonCamera.enabled = false;
                if (thirdPersonCamera != null)
                    thirdPersonCamera.enabled = true;

                if (hideFirstPerson != null)
                    hideFirstPerson.SetFirstPerson(false);

                // Unlock cursor for third-person
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                break;

            case CameraMode.FirstPerson:
                if (thirdPersonCamera != null)
                    thirdPersonCamera.enabled = false;
                if (firstPersonCamera != null)
                    firstPersonCamera.enabled = true;

                if (hideFirstPerson != null)
                    hideFirstPerson.SetFirstPerson(true);

                // Lock cursor for first-person
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                break;
        }
    }

    /// <summary>
    /// Check if currently in first-person mode.
    /// </summary>
    public bool IsFirstPerson => currentMode == CameraMode.FirstPerson;

    /// <summary>
    /// Check if currently in third-person mode.
    /// </summary>
    public bool IsThirdPerson => currentMode == CameraMode.ThirdPerson;
}
