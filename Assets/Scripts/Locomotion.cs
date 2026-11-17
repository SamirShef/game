using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(PlayerContext))]
public class Locomotion : MonoBehaviour
{
    [Header("Moving")]
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float runSpeed = 5f;
    [SerializeField] private float turnSmoothTime = 0.12f;
    [SerializeField] private KeyCode highProfileCode = KeyCode.LeftShift;
    [SerializeField] private KeyCode jumpCode = KeyCode.Space;
    [SerializeField] private float jumpHeight = 1f;
    [SerializeField] private float edgeThreshold = 0.2f;
    [SerializeField] private float maxStepHeight = 0.35f;
    [SerializeField] private float fallThreshold = 0.8f;
    [SerializeField] private int rayCount = 5;
    [SerializeField] private float probeArcAngle = 80.0f;

    [SerializeField] private float gravityValue = 9.81f;

    private Animator animator;
    private CharacterController controller;
    private PlayerContext ctx;

    private float turnSmoothVel;
    private Transform cameraTransform;
    private Vector3 playerVelocity;

    private float airborneTimer = 0f;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        controller = GetComponent<CharacterController>();
        ctx = GetComponent<PlayerContext>();
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void Update()
    {
        if (ctx.State != PlayerState.Locomotion &&
            ctx.State != PlayerState.Idle &&
            ctx.State != PlayerState.Jumping &&
            ctx.State != PlayerState.Balancing)
        {
            playerVelocity = Vector3.zero;
            return;
        }

        if (airborneTimer > 0f)
        {
            airborneTimer -= Time.deltaTime;
        }

        if (ctx.State == PlayerState.Jumping && airborneTimer <= 0f && controller.isGrounded)
        {
            ctx.State = PlayerState.Idle;
            Debug.Log("Landed safely! Returning to Idle.");
        }

        HandleMovement();
        HandleJumping();
        HandlePhysics();
    }

    private void HandlePhysics()
    {
        if (airborneTimer <= 0f && controller.isGrounded && playerVelocity.y < 0f)
        {
            playerVelocity.y = -2f;
        }

        playerVelocity.y -= gravityValue * Time.deltaTime;
        controller?.Move(playerVelocity * Time.deltaTime);
    }

    private void HandleMovement()
    {
        if (!controller.isGrounded || ctx.State == PlayerState.Jumping)
        {
            return;
        }

        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        Vector3 inputVec = new Vector3(moveX, 0, moveZ).normalized;

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveVec = (cameraForward * inputVec.z + cameraRight * inputVec.x).normalized;

        bool needJumping = Input.GetKey(highProfileCode) && Input.GetKeyDown(jumpCode);
        if (controller.isGrounded && !needJumping && moveVec.sqrMagnitude > 0.001f)
        {
            if (TryGetFloorEdgeDropNormal(moveVec, out Vector3 edgeNormal))
            {
                Debug.DrawRay(transform.position, edgeNormal, Color.blue);
                float dot = Vector3.Dot(moveVec, edgeNormal);
                if (dot > 0)
                {
                    moveVec -= edgeNormal * dot;
                    moveVec.Normalize();
                }
            }
        }

        float speed = moveVec.magnitude * (Input.GetKey(highProfileCode) ? runSpeed : walkSpeed);

        if (moveVec.sqrMagnitude > 0.001f)
        {
            float targetAngle = Mathf.Atan2(moveVec.x, moveVec.z) * Mathf.Rad2Deg;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVel, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            playerVelocity.x = moveVec.x * speed;
            playerVelocity.z = moveVec.z * speed;
            ctx.State = PlayerState.Locomotion;
        }
        else
        {
            if (ctx.State == PlayerState.Locomotion)
            {
                Debug.Log($"from HandleMovement isGrounded = {controller.isGrounded}");
                ctx.State = PlayerState.Idle;
            }
            playerVelocity.x = 0;
            playerVelocity.z = 0;
        }

        animator?.SetFloat("Speed", speed, 0.1f, Time.deltaTime);
    }

    private void HandleJumping()
    {
        if (Input.GetKey(highProfileCode) && Input.GetKeyDown(jumpCode) && controller.isGrounded)
        {
            ChangeStateToJumping();
            HandleJump();
        }
    }

    private void HandleJump()
    {
        // animator?.CrossFade("Jumping", 0.15f);
        ApplyJumpForce();
    }

    private bool TryGetFloorEdgeDropNormal(Vector3 moveVec, out Vector3 edgeNormal)
    {
        edgeNormal = Vector3.zero;

        float probeDist = edgeThreshold;
        Vector3 origin = transform.position + Vector3.up * maxStepHeight;

        bool edgeDetected = false;
        Vector3 normal = Vector3.zero;

        float startAngle = -probeArcAngle * 0.5f;
        float deltaAngle = probeArcAngle / Mathf.Max(1, rayCount - 1);

        for (int i = 0; i < rayCount; i++)
        {
            float angle = startAngle + i * deltaAngle;
            Quaternion rotation = Quaternion.Euler(0, angle, 0);
            Vector3 probeDir = rotation * moveVec;
            Vector3 probeRayOrigin = origin + probeDir * probeDist;

            if (!Physics.Raycast(probeRayOrigin, Vector3.down, fallThreshold + maxStepHeight))
            {
                Debug.DrawRay(probeRayOrigin, Vector3.down * (fallThreshold + maxStepHeight), Color.red);
                edgeDetected = true;
                normal += probeDir;
            }
            else
            {
                Debug.DrawRay(probeRayOrigin, Vector3.down * (fallThreshold + maxStepHeight), Color.green);
            }
        }

        if (edgeDetected)
        {
            normal.Normalize();
            edgeNormal = normal;
            return true;
        }

        return false;
    }

    public void ApplyJumpForce()
    {
        float jumpVelocity = Mathf.Sqrt(jumpHeight * 2 * gravityValue);
        playerVelocity.y = jumpVelocity;
    }

    public void ForceJumpAway(Vector3 forceDir)
    {
        ctx.State = PlayerState.Jumping;
        playerVelocity = forceDir + Vector3.up * 3f;
        airborneTimer = 0.3f;
    }

    public void ChangeStateToJumping()
    {
        ctx.State = PlayerState.Jumping;
    }

    public void ChangeStateToIdle()
    {
        ctx.State = PlayerState.Idle;
    }
}