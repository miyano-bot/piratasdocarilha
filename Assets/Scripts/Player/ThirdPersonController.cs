using UnityEngine;

public class TopDownPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float sprintSpeed = 8f;
    public float rotationSpeed = 15f;
    public float acceleration = 10f;
    public float deceleration = 10f;

    [Header("Jump Settings")]
    public float jumpHeight = 2f;   // Altura do pulo
    public float gravityScale = 2f; // Controle da gravidade

    [Header("Control Settings")]
    public bool relativeToCamera = true;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    private CharacterController controller;
    private Vector3 movement;
    private Vector3 smoothMovement;
    private bool isSprinting;
    private float currentSpeed;
    private bool isGrounded;
    private Vector3 velocity;
    private Camera mainCamera;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
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
    }

    private void Update()
    {

        Debug.Log("Está no chão? " + isGrounded);

        // Ground check
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        // Se estiver no chão, resetamos a velocidade de queda
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // Pulo
        if (isGrounded && Input.GetKeyDown(KeyCode.Space))
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y);
        }

        // Aplicar gravidade personalizada
        velocity.y += Physics.gravity.y * gravityScale * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        // Entrada de movimento
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // Sprint
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            isSprinting = true;
            currentSpeed = sprintSpeed;
        }
        if (Input.GetKeyUp(KeyCode.LeftShift))
        {
            isSprinting = false;
            currentSpeed = moveSpeed;
        }

        // Calcular movimento
        Vector3 input = new Vector3(horizontal, 0f, vertical).normalized;

        if (input.magnitude >= 0.1f)
        {
            if (relativeToCamera)
            {
                Vector3 cameraForward = mainCamera.transform.forward;
                cameraForward.y = 0;
                cameraForward.Normalize();

                Vector3 cameraRight = mainCamera.transform.right;
                cameraRight.y = 0;
                cameraRight.Normalize();

                movement = cameraRight * input.x + cameraForward * input.z;
            }
            else
            {
                movement = new Vector3(input.x, 0, input.z);
            }

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

        // Aplicar movimento horizontal
        controller.Move(smoothMovement * Time.deltaTime);
    }
}
