using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform cameraPivot;   // assign CameraPivot (child of player)
    [SerializeField] private Transform cam;           // assign Main Camera transform (optional)

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float turnDegreesPerSecond = 720f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 70f;

    [Header("Jump & Gravity")]
    [SerializeField] private float gravity = -20f;     // negative
    [SerializeField] private float jumpHeight = 1.2f;  // meters
    [SerializeField] private float groundedStick = -2f; // keeps you stuck to ground

    private CharacterController controller;

    private float yaw;
    private float pitch;

    // This is our "velocity" (m/s). We'll add move + vertical then Move() once per frame.
    private Vector3 velocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (cam == null) cam = Camera.main.transform;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        yaw = cameraPivot.eulerAngles.y;
        pitch = cameraPivot.eulerAngles.x;
    }

    void Update()
    {
        HandleLook();
        HandleMoveAndJump();
    }

    void HandleLook()
    {
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        yaw += mx;
        pitch -= my;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        cameraPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    void HandleMoveAndJump()
    {
        // ---- INPUT ----
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 input = new Vector3(h, 0f, v).normalized;

        // ---- CAMERA-RELATIVE MOVE DIR ----
        Vector3 camForward = cam.forward; camForward.y = 0f; camForward.Normalize();
        Vector3 camRight = cam.right; camRight.y = 0f; camRight.Normalize();
        Vector3 moveDir = camRight * input.x + camForward * input.z;

        // ---- ROTATE TOWARD MOVE DIR ----
        if (moveDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                turnDegreesPerSecond * Time.deltaTime
            );
        }

        // ---- HORIZONTAL VELOCITY ----
        Vector3 horizontal = moveDir * moveSpeed;

        // ---- GROUNDED / JUMP / GRAVITY ----
        if (controller.isGrounded)
        {
            // keep a small downward velocity so the controller stays grounded on slopes
            if (velocity.y < 0f) velocity.y = groundedStick;

            // Jump (Space)
            if (Input.GetButtonDown("Jump"))
            {
                // v = sqrt(2 * jumpHeight * -gravity)
                velocity.y = Mathf.Sqrt(2f * jumpHeight * -gravity);
            }
        }

        velocity.y += gravity * Time.deltaTime;

        // ---- MOVE ONCE (reduces jitter) ----
        Vector3 finalMove = (horizontal + new Vector3(0f, velocity.y, 0f)) * Time.deltaTime;
        controller.Move(finalMove);
    }
}
