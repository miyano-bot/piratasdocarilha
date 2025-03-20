using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Animator")]
    private Animator animator;
    
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float sprintSpeed = 8f;
    public float rotationSpeed = 15f;
    public float acceleration = 10f;
    public float deceleration = 10f;

    [Header("Jump Settings")]
    public float jumpHeight = 2f;
    public float gravityScale = 2f;
    public float fallMultiplier = 2.5f;
    public float jumpBlendSpeed = 3f;

    [Header("Control Settings")]
    public bool relativeToCamera = true;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    [Header("Landing Detection")]
    public float landingDetectionDistance = 0.5f;
    public float landingBufferTime = 0.1f;

    // Constants
    private const float GroundedVerticalVelocity = -2f;
    private const float VerticalSpeedNormalizationFactor = 5f;

    // Component references
    private CharacterController controller;
    private Camera mainCamera;
    
    // Movement variables
    private Vector3 movement;
    private Vector3 smoothMovement;
    private float currentSpeed;
    private Vector3 velocity;
    
    // State tracking
    private enum MovementState { Grounded, Jumping, Falling, Landing }
    private MovementState currentState = MovementState.Grounded;
    private bool isGrounded;
    private float timeSinceLandingTriggered;
    
    // Animation blending
    private float currentBlendValue = 0f;
    private float currentVerticalBlend = 0f;
    
    // Parameter validation results
    private bool hasSpeedParam;
    private bool hasVerticalParam;
    private bool hasGroundedParam;
    private bool hasJumpingParam;
    private bool hasFallingParam;
    private bool hasLandingParam;
    
    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        mainCamera = Camera.main;

        if (groundCheck == null)
        {
            GameObject check = new GameObject("GroundCheck");
            check.transform.SetParent(transform);
            check.transform.localPosition = new Vector3(0, -0.9f, 0);
            groundCheck = check.transform;
        }
    }

    private void Start()
    {
        currentSpeed = moveSpeed;
        ValidateAnimatorParameters();
    }
    
    private void ValidateAnimatorParameters()
    {
        if (animator == null)
        {
            Debug.LogError("No Animator component found on " + gameObject.name);
            enabled = false;
            return;
        }
        
        hasSpeedParam = HasParameter("Speed");
        hasVerticalParam = HasParameter("VerticalSpeed");
        hasGroundedParam = HasParameter("IsGrounded");
        hasJumpingParam = HasParameter("IsJumping");
        hasFallingParam = HasParameter("IsFalling");
        hasLandingParam = HasParameter("IsLanding");
        
        if (!hasSpeedParam)
        {
            Debug.LogError("Blend parameter '" + "Speed" + "' not found. Disabling movement animations.");
            enabled = false;
        }
    }
    
    private bool HasParameter(string paramName)
    {
        if (animator == null) return false;
        
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == paramName)
                return true;
        }
        return false;
    }

    private void Update()
    {
        bool wasGrounded = isGrounded;
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (isGrounded && !wasGrounded)
        {
            currentState = (velocity.y < -4f) ? MovementState.Landing : MovementState.Grounded;
            timeSinceLandingTriggered = 0f;
        }

        ApplyGravity();
        ProcessMovementInput();
        controller.Move(smoothMovement * Time.deltaTime);
        controller.Move(velocity * Time.deltaTime);
        UpdateAnimator();
        
        if (currentState == MovementState.Landing)
        {
            timeSinceLandingTriggered += Time.deltaTime;
            if (timeSinceLandingTriggered >= landingBufferTime)
            {
                currentState = MovementState.Grounded;
            }
        }
    }
    
    private void ApplyGravity()
    {
        float gravityMultiplier = (velocity.y < 0) ? fallMultiplier : 1f;
        velocity.y += Physics.gravity.y * gravityScale * gravityMultiplier * Time.deltaTime;
        
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = GroundedVerticalVelocity;
        }
        
        if (isGrounded && Input.GetKeyDown(KeyCode.Space))
        {
            Jump();
        }
        
        if (!isGrounded && currentState != MovementState.Landing && velocity.y < -0.5f)
        {
            currentState = MovementState.Falling;
            PredictLanding();
        }
    }
    
    private void Jump()
    {
        currentState = MovementState.Jumping;
        velocity.y = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y);
    }
    
    private void PredictLanding()
    {
        RaycastHit hit;
        if (Physics.Raycast(groundCheck.position, Vector3.down, out hit, landingDetectionDistance, groundMask))
        {
            float timeToLand = hit.distance / Mathf.Abs(velocity.y);
            if (timeToLand <= landingBufferTime)
            {
                currentState = MovementState.Landing;
                timeSinceLandingTriggered = 0f;
            }
        }
    }
    
    private void ProcessMovementInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 input = new Vector3(horizontal, 0f, vertical).normalized;

        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            currentSpeed = sprintSpeed;
        }
        if (Input.GetKeyUp(KeyCode.LeftShift))
        {
            currentSpeed = moveSpeed;
        }

        if (input.magnitude >= 0.1f)
        {
            Transform cameraTransform = mainCamera.transform;
            Vector3 cameraForward = cameraTransform.forward;
            cameraForward.y = 0;
            cameraForward.Normalize();

            Vector3 cameraRight = cameraTransform.right;
            cameraRight.y = 0;
            cameraRight.Normalize();

            movement = cameraRight * input.x + cameraForward * input.z;
            movement.Normalize();

            smoothMovement = Vector3.Lerp(smoothMovement, movement * currentSpeed, acceleration * Time.deltaTime);

            if (movement != Vector3.zero)
            {
                Quaternion toRotation = Quaternion.LookRotation(movement, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, toRotation, rotationSpeed * Time.deltaTime);
            }
        }
        else
        {
            smoothMovement = Vector3.Lerp(smoothMovement, Vector3.zero, deceleration * Time.deltaTime);
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;
        
        float smoothMovementMagnitude = smoothMovement.magnitude;
        float moveSpeedPercent = Mathf.Clamp(smoothMovementMagnitude / currentSpeed, 0f, 2f);
        currentBlendValue = Mathf.Lerp(currentBlendValue, moveSpeedPercent, Time.deltaTime * 8f);
        
        if (hasSpeedParam)
        {
            animator.SetFloat("Speed", currentBlendValue);
        }
        
        if (hasVerticalParam)
        {
            float targetVerticalValue = isGrounded ? 0 : Mathf.Clamp(velocity.y / VerticalSpeedNormalizationFactor, -1f, 1f);
            if (Mathf.Abs(currentVerticalBlend - targetVerticalValue) > 0.01f)
            {
                currentVerticalBlend = Mathf.Lerp(currentVerticalBlend, targetVerticalValue, Time.deltaTime * jumpBlendSpeed);
            }
            animator.SetFloat("VerticalSpeed", currentVerticalBlend);
        }
        
        if (hasGroundedParam) animator.SetBool("IsGrounded", isGrounded);
        if (hasJumpingParam) animator.SetBool("IsJumping", currentState == MovementState.Jumping);
        if (hasFallingParam) animator.SetBool("IsFalling", currentState == MovementState.Falling);
        if (hasLandingParam) animator.SetBool("IsLanding", currentState == MovementState.Landing);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
            Gizmos.DrawLine(groundCheck.position, groundCheck.position + Vector3.down * landingDetectionDistance);
        }
    }
}