using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform cameraPivot;   // child of player, at head/shoulder height
    [SerializeField] private Transform cam;           // Main Camera transform

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float turnSmoothTime = 0.1f;

    [Header("Camera Orbit")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minPitch = -20f;
    [SerializeField] private float maxPitch = 60f;
    [SerializeField] private float rotationSmoothTime = 0.05f;

    [Header("Camera Zoom")]
    [SerializeField] private float defaultDistance = 4f;
    [SerializeField] private float minDistance = 1.5f;
    [SerializeField] private float maxDistance = 10f;
    [SerializeField] private float zoomSpeed = 4f;
    [SerializeField] private float zoomSmoothTime = 0.15f;

    [Header("Camera Follow")]
    [SerializeField] private float followSmoothTime = 0.1f;

    [Header("Camera Collision")]
    [SerializeField] private float cameraCollisionRadius = 0.2f;
    [SerializeField] private LayerMask cameraCollisionMask = ~0;
    [SerializeField] private float collisionPullInSpeed = 15f;
    [SerializeField] private float collisionPushOutSpeed = 5f;

    [Header("Orbit Lock")]
    [SerializeField] private bool orbitLocked;
    [SerializeField] private float lockedYaw = 0f;
    [SerializeField] private float lockedPitch = 25f;
    [SerializeField] private float lockedDistance = 8f;

    [Header("Jump & Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float groundedStick = -2f;

    private CharacterController controller;
    private Collider[] playerColliders;

    // camera state
    private float yaw;
    private float pitch;
    private float smoothYaw;
    private float smoothPitch;
    private float yawVel;
    private float pitchVel;

    private float targetDistance;
    private float smoothDistance;
    private float distanceVel;
    private float actualDistance;

    private Vector3 pivotFollowPos;
    private Vector3 pivotFollowVel;

    // character state
    private float turnVel;
    private Vector3 velocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerColliders = GetComponentsInChildren<Collider>();
        if (cam == null) cam = Camera.main.transform;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        yaw = cameraPivot.eulerAngles.y;
        pitch = cameraPivot.eulerAngles.x;
        smoothYaw = yaw;
        smoothPitch = pitch;

        targetDistance = defaultDistance;
        smoothDistance = defaultDistance;
        actualDistance = defaultDistance;

        pivotFollowPos = cameraPivot.position;
    }

    void Update()
    {
        HandleMoveAndJump();
    }

    void LateUpdate()
    {
        HandleCamera();
    }

    /// <summary>Call from scene triggers/scripts to lock or unlock the orbit.</summary>
    public void SetOrbitLocked(bool locked, float newYaw = 0f, float newPitch = 25f, float newDist = 8f)
    {
        orbitLocked = locked;
        if (locked)
        {
            lockedYaw = newYaw;
            lockedPitch = newPitch;
            lockedDistance = newDist;

            // snap immediately
            yaw = smoothYaw = newYaw;
            pitch = smoothPitch = newPitch;
            smoothDistance = actualDistance = newDist;
            targetDistance = newDist;
            yawVel = pitchVel = distanceVel = 0f;
            pivotFollowPos = cameraPivot.position;
            pivotFollowVel = Vector3.zero;
        }
    }

    void HandleCamera()
    {
        if (orbitLocked)
        {
            // --- locked: smooth toward fixed angles, no mouse, no collision ---
            yaw = lockedYaw;
            pitch = lockedPitch;
            smoothYaw = Mathf.SmoothDamp(smoothYaw, lockedYaw, ref yawVel, rotationSmoothTime);
            smoothPitch = Mathf.SmoothDamp(smoothPitch, lockedPitch, ref pitchVel, rotationSmoothTime);
            Quaternion orbitRot = Quaternion.Euler(smoothPitch, smoothYaw, 0f);

            smoothDistance = Mathf.SmoothDamp(smoothDistance, lockedDistance, ref distanceVel, zoomSmoothTime);
            actualDistance = smoothDistance;

            pivotFollowPos = Vector3.SmoothDamp(pivotFollowPos, cameraPivot.position, ref pivotFollowVel, followSmoothTime);

            Vector3 dir = -(orbitRot * Vector3.forward);
            cam.position = pivotFollowPos + dir * actualDistance;
            cam.rotation = orbitRot;
            return;
        }

        // --- mouse input ---
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        yaw += mx;
        pitch -= my;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // --- smooth rotation ---
        smoothYaw = Mathf.SmoothDamp(smoothYaw, yaw, ref yawVel, rotationSmoothTime);
        smoothPitch = Mathf.SmoothDamp(smoothPitch, pitch, ref pitchVel, rotationSmoothTime);
        Quaternion freeRot = Quaternion.Euler(smoothPitch, smoothYaw, 0f);

        // --- zoom ---
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        targetDistance -= scroll * zoomSpeed;
        targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        smoothDistance = Mathf.SmoothDamp(smoothDistance, targetDistance, ref distanceVel, zoomSmoothTime);

        // --- smooth follow pivot ---
        pivotFollowPos = Vector3.SmoothDamp(pivotFollowPos, cameraPivot.position, ref pivotFollowVel, followSmoothTime);

        // --- camera collision ---
        Vector3 dir2 = -(freeRot * Vector3.forward);
        float wantedDist = smoothDistance;
        float clippedDist = wantedDist;

        RaycastHit hit;
        if (Physics.Raycast(pivotFollowPos, dir2, out hit, wantedDist + cameraCollisionRadius,
                cameraCollisionMask, QueryTriggerInteraction.Ignore))
        {
            if (!IsPlayerCollider(hit.collider))
                clippedDist = Mathf.Min(clippedDist, hit.distance - cameraCollisionRadius);
        }

        if (Physics.SphereCast(pivotFollowPos, cameraCollisionRadius, dir2, out hit,
                wantedDist, cameraCollisionMask, QueryTriggerInteraction.Ignore))
        {
            if (!IsPlayerCollider(hit.collider))
                clippedDist = Mathf.Min(clippedDist, hit.distance);
        }

        clippedDist = Mathf.Max(clippedDist, 0.2f);

        float speed = clippedDist < actualDistance ? collisionPullInSpeed : collisionPushOutSpeed;
        actualDistance = Mathf.Lerp(actualDistance, clippedDist, Time.deltaTime * speed);

        Vector3 finalPos = pivotFollowPos + dir2 * actualDistance;

        cam.position = finalPos;
        cam.rotation = freeRot;
    }

    bool IsPlayerCollider(Collider col)
    {
        for (int i = 0; i < playerColliders.Length; i++)
            if (playerColliders[i] == col) return true;
        return false;
    }

    void HandleMoveAndJump()
    {
        // --- input ---
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 input = new Vector3(h, 0f, v).normalized;

        // --- move direction (camera-relative when free, fixed when locked) ---
        Vector3 camForward, camRight;
        if (orbitLocked)
        {
            // use the locked camera angle so WASD stays consistent
            Quaternion lockRot = Quaternion.Euler(0f, lockedYaw, 0f);
            camForward = lockRot * Vector3.forward;
            camRight   = lockRot * Vector3.right;
        }
        else
        {
            camForward = cam.forward; camForward.y = 0f; camForward.Normalize();
            camRight   = cam.right;   camRight.y   = 0f; camRight.Normalize();
        }
        Vector3 moveDir = camRight * input.x + camForward * input.z;

        // --- smooth character rotation toward move dir ---
        if (moveDir.sqrMagnitude > 0.001f)
        {
            float targetAngle = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
            float smoothAngle = Mathf.SmoothDampAngle(
                transform.eulerAngles.y, targetAngle, ref turnVel, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f);
        }

        // --- horizontal velocity ---
        float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : moveSpeed;
        Vector3 horizontal = moveDir * speed;

        // --- grounded / jump / gravity ---
        if (controller.isGrounded)
        {
            if (velocity.y < 0f) velocity.y = groundedStick;

            if (Input.GetButtonDown("Jump"))
                velocity.y = Mathf.Sqrt(2f * jumpHeight * -gravity);
        }

        velocity.y += gravity * Time.deltaTime;

        // --- move ---
        Vector3 finalMove = (horizontal + new Vector3(0f, velocity.y, 0f)) * Time.deltaTime;
        controller.Move(finalMove);
    }
}
