using UnityEngine;

public class RotatableTopDownCamera : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;
    public float distance = 10f;
    public float height = 15f;
    public float smoothSpeed = 10f;
    
    [Header("Camera Angle")]
    public float pitch = 45f; // Downward angle in degrees
    public float minPitch = 20f; // Minimum tilt
    public float maxPitch = 80f; // Maximum tilt
    public float tiltSpeed = 50f; // Speed of tilting
    public bool allowCameraRotation = true;
    public float rotationSpeed = 100f;
    public KeyCode rotateLeftKey = KeyCode.Q;
    public KeyCode rotateRightKey = KeyCode.E;
    
    [Header("Zoom Settings")]
    public float minZoom = 5f;
    public float maxZoom = 20f;
    public float zoomSpeed = 4f;
    public float currentZoom = 10f;
    
    [Header("Camera Boundaries")]
    public bool useBoundaries = false;
    public float minX = -50f;
    public float maxX = 50f;
    public float minZ = -50f;
    public float maxZ = 50f;
    
    private float currentRotationAngle = 0f;
    private Vector3 desiredPosition;
    
    private void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                target = player.transform;
            else
                Debug.LogError("No target assigned to camera and no Player tag found!");
        }
        
        UpdateCameraRotation();
    }
    
    private void LateUpdate()
    {
        if (target == null)
            return;
            
        HandleZoom();
        
        if (allowCameraRotation)
        {
            HandleRotation();
        }
        
        UpdateCameraPosition();
    }
    
    private void HandleZoom()
    {
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        currentZoom -= scrollInput * zoomSpeed;
        currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
    }
    
    private void HandleRotation()
    {
        // Keyboard rotation
        if (Input.GetKey(rotateLeftKey))
        {
            currentRotationAngle -= rotationSpeed * Time.deltaTime;
        }
        else if (Input.GetKey(rotateRightKey))
        {
            currentRotationAngle += rotationSpeed * Time.deltaTime;
        }
        
        // Rotate with right mouse button
        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X");
            currentRotationAngle += mouseX * rotationSpeed * Time.deltaTime;
        }

        // **Tilt Camera with Mouse Wheel Button**
        if (Input.GetMouseButton(2)) // Middle mouse button
        {
            float mouseY = Input.GetAxis("Mouse Y");
            pitch -= mouseY * tiltSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        // Keep angle between 0-360
        currentRotationAngle = currentRotationAngle % 360;

        UpdateCameraRotation();
    }
    
    private void UpdateCameraRotation()
    {
        transform.rotation = Quaternion.Euler(pitch, currentRotationAngle, 0);
    }
    
    private void UpdateCameraPosition()
    {
        float adjustedDistance = distance * (currentZoom / 10f);
        float adjustedHeight = height * (currentZoom / 10f);
        
        float radian = currentRotationAngle * Mathf.Deg2Rad;
        float x = target.position.x - Mathf.Sin(radian) * adjustedDistance;
        float z = target.position.z - Mathf.Cos(radian) * adjustedDistance;
        
        desiredPosition = new Vector3(x, target.position.y + adjustedHeight, z);
        
        if (useBoundaries)
        {
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
            desiredPosition.z = Mathf.Clamp(desiredPosition.z, minZ, maxZ);
        }
        
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        
        transform.LookAt(target.position);

        Vector3 eulerAngles = transform.eulerAngles;
        eulerAngles.x = pitch;
        transform.eulerAngles = eulerAngles;
    }
}
