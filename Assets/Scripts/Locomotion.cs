using System.Collections.Generic;
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

    [Header("Beam Balancing")]
    [SerializeField] private float beamDetectRadius = 0.6f;
    [SerializeField] private float beamDetectHalfHeight = 1.0f;
    [SerializeField] private float beamEntryMaxAngle = 45f;
    [SerializeField] private float beamExitOppositeMaxAngle = 40f;
    [SerializeField] private float beamEdgeEpsilon = 0.05f;

    [Header("Scanner")]
    [SerializeField] private Vector3 obstacleBoxHalfExtents = new(0.6f, 0.6f, 2.2f);
    [SerializeField] private float maxJumpOnObstacleHeight = 1f;
    [SerializeField] private float maxJumpDistance = 2.2f;
    [SerializeField] private float autoJumpTriggerDistance = 1.1f;
    [SerializeField] private float obstacleJumpMoveSpeed = 7f;
    [SerializeField] private LayerMask obstacleLayer;
    private const int maxObstaclesCount = 10;
    private readonly Collider[] obstacles = new Collider[maxObstaclesCount];
    private bool hasBestObstacleCandidate;
    private ObstacleCandidate bestObstacleCandidate;
    private bool isJumpingToObstacleTarget;
    private Vector3 obstacleJumpTarget;

    [SerializeField] private float gravityValue = 9.81f;

    private Animator animator;
    private CharacterController controller;
    private PlayerContext ctx;

    private float turnSmoothVel;
    private Transform cameraTransform;
    private Vector3 playerVelocity;

    private float airborneTimer = 0f;

    private LayerMask nodeLayer;
    private readonly Collider[] beamCollidersBuffer = new Collider[8];
    private ParkourNode currentBeamNode;

    private readonly struct ObstacleCandidate
    {
        public readonly Collider Collider;
        public readonly Vector3 TargetPoint;
        public readonly float DeltaY;
        public readonly float Distance;
        public readonly float Score;

        public ObstacleCandidate(Collider c, Vector3 targetPoint, float dY, float dist, float score)
        {
            Collider = c;
            TargetPoint = targetPoint;
            DeltaY = dY;
            Distance = dist;
            Score = score;
        }
    }

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        controller = GetComponent<CharacterController>();
        ctx = GetComponent<PlayerContext>();
        nodeLayer = LayerMask.GetMask(ParkourNode.LayerName);

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
            currentBeamNode = null;
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
        ScanObstacles();
        HandleJumping();
        HandlePhysics();
    }

    private void HandleMovement()
    {
        if (!controller.isGrounded || ctx.State == PlayerState.Jumping)
        {
            return;
        }

        Vector3 moveVec = GetCameraRelativeMoveInput();

        if (ctx.State == PlayerState.Balancing)
        {
            HandleBalancingMovement(moveVec);
            return;
        }

        TryEnterBeamBalancing(moveVec);
        if (ctx.State == PlayerState.Balancing)
        {
            HandleBalancingMovement(moveVec);
            return;
        }

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

    private void ScanObstacles()
    {
        if (ctx.State != PlayerState.Locomotion)
        {
            return;
        }

        Vector3 moveVec = GetCameraRelativeMoveInput();
        Vector3 boxCenter = transform.position + moveVec * obstacleBoxHalfExtents.z + Vector3.up * obstacleBoxHalfExtents.y;

        int count = Physics.OverlapBoxNonAlloc(boxCenter, obstacleBoxHalfExtents, obstacles, Quaternion.LookRotation(moveVec), obstacleLayer);
        if (count == 0)
        {
            return;
        }

        ObstacleCandidate bestCandidate = default;
        float bestScore = -float.MaxValue;
        bool found = false;

        for (int i = 0; i < count; ++i)
        {
            Collider c = obstacles[i];

            Vector3 rawPoint = c.ClosestPoint(transform.position);
            rawPoint += moveVec * 0.2f;
            rawPoint.y = c.bounds.max.y + 5f;
            Debug.DrawRay(rawPoint, Vector3.up * 2, Color.blue);

            Vector3 targetPoint = c.ClosestPoint(rawPoint);
            float dY = targetPoint.y - transform.position.y;
            if (dY <= maxStepHeight || dY > maxJumpOnObstacleHeight)
            {
                continue;
            }
            float heightScore = 1f - Mathf.Clamp01(dY / Mathf.Max(0.01f, maxJumpOnObstacleHeight));

            float dot = Vector3.Dot(new Vector3(targetPoint.x, 0, targetPoint.z).normalized, moveVec);
            if (dot <= 0.6f)
            {
                continue;
            }
            float inputScore = Mathf.InverseLerp(0.2f, 1f, dot);

            float distance = Vector3.Distance(targetPoint, transform.position);
            float distanceScore = 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, maxJumpDistance));

            float score = inputScore + distanceScore + heightScore;

            Debug.DrawRay(targetPoint, Vector3.up * 2, Color.rebeccaPurple, 5);
            ObstacleCandidate candidate = new(c, targetPoint, dY, distance, score);
            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = candidate;
                found = true;
            }
        }

        if (!found)
        {
            return;
        }

        hasBestObstacleCandidate = true;
        bestObstacleCandidate = bestCandidate;
        Debug.DrawRay(bestCandidate.TargetPoint, Vector3.up * 2.5f, Color.gold);

        if (Input.GetKey(highProfileCode) && bestCandidate.Distance <= autoJumpTriggerDistance)
        {
            StartObstacleJump(bestCandidate.TargetPoint);
        }
    }

    private void StartObstacleJump(Vector3 targetPoint)
    {
        hasBestObstacleCandidate = false;
        isJumpingToObstacleTarget = true;
        obstacleJumpTarget = targetPoint;
        airborneTimer = 0.15f;
        playerVelocity = Vector3.zero;
        ctx.State = PlayerState.Jumping;

        Vector3 lookDir = obstacleJumpTarget - transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(lookDir.normalized);
        }
    }

    private void HandleJumping()
    {
        if (isJumpingToObstacleTarget || !controller.isGrounded || ctx.State == PlayerState.Jumping)
        {
            return;
        }

        if (Input.GetKey(highProfileCode) && Input.GetKeyDown(jumpCode) && controller.isGrounded)
        {
            if (hasBestObstacleCandidate)
            {
                StartObstacleJump(bestObstacleCandidate.TargetPoint);
            }
            else
            {
                ChangeStateToJumping();
                HandleJump();
            }
        }
    }

    private void HandlePhysics()
    {
        if (isJumpingToObstacleTarget)
        {
            Vector3 nextPos = Vector3.MoveTowards(
                transform.position,
                obstacleJumpTarget,
                obstacleJumpMoveSpeed * Time.deltaTime
            );
            Vector3 displacement = nextPos - transform.position;
            playerVelocity = displacement / Time.deltaTime;
            controller.Move(displacement);

            if (transform.position == obstacleJumpTarget || airborneTimer <= 0f && controller.isGrounded)
            {
                isJumpingToObstacleTarget = false;
                playerVelocity = Vector3.zero;
                ctx.State = PlayerState.Locomotion;
            }
            return;
        }

        if (airborneTimer <= 0f && controller.isGrounded && playerVelocity.y < 0f)
        {
            playerVelocity.y = -2f;
        }

        playerVelocity.y -= gravityValue * Time.deltaTime;
        controller?.Move(playerVelocity * Time.deltaTime);
    }

    private void HandleJump()
    {
        // animator?.CrossFade("Jumping", 0.15f);
        ApplyJumpForce();
    }

    private Vector3 GetCameraRelativeMoveInput()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        Vector3 inputVec = new(moveX, 0f, moveZ);

        if (inputVec.sqrMagnitude < 0.001f || cameraTransform == null)
        {
            return Vector3.zero;
        }

        inputVec.Normalize();

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveVec = cameraForward * inputVec.z + cameraRight * inputVec.x;
        return moveVec.sqrMagnitude > 0.001f ? moveVec.normalized : Vector3.zero;
    }

    private void TryEnterBeamBalancing(Vector3 moveVec)
    {
        if (moveVec.sqrMagnitude < 0.001f)
        {
            return;
        }

        Vector3 boxCenter = transform.position + Vector3.up * beamDetectHalfHeight;
        Vector3 halfExtents = new(beamDetectRadius, beamDetectHalfHeight, beamDetectRadius);
        int count = Physics.OverlapBoxNonAlloc(
            boxCenter,
            halfExtents,
            beamCollidersBuffer,
            transform.rotation,
            nodeLayer
        );

        ParkourNode bestBeam = null;
        Vector3 bestProjPoint = Vector3.zero;
        float bestDistanceSqr = float.MaxValue;

        Vector3 flatMove = new Vector3(moveVec.x, 0f, moveVec.z).normalized;

        for (int i = 0; i < count; i++)
        {
            Collider col = beamCollidersBuffer[i];
            if (col == null || col is not BoxCollider)
            {
                continue;
            }

            if (!col.TryGetComponent<ParkourNode>(out var node) || node.NodeType != ParkourNodeType.Beam)
            {
                continue;
            }

            Vector3 projPoint = node.PointAtProject(transform.position);
            Vector3 toProjFlat = projPoint - transform.position;
            toProjFlat.y = 0f;

            bool angleValid;
            if (toProjFlat.sqrMagnitude <= 0.0001f)
            {
                angleValid = true;
            }
            else
            {
                float angleToProj = Vector3.Angle(flatMove, toProjFlat.normalized);
                angleValid = angleToProj <= beamEntryMaxAngle;
            }

            if (!angleValid)
            {
                continue;
            }

            float distSqr = (projPoint - transform.position).sqrMagnitude;
            if (distSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distSqr;
                bestBeam = node;
                bestProjPoint = projPoint;
            }
        }

        if (bestBeam == null)
        {
            return;
        }

        Vector3 targetMagnetPos = new(bestProjPoint.x, transform.position.y, bestProjPoint.z);
        controller.enabled = false;
        transform.position = targetMagnetPos;
        controller.enabled = true;

        currentBeamNode = bestBeam;
        ctx.State = PlayerState.Balancing;
    }

    private void HandleBalancingMovement(Vector3 moveVec)
    {
        if (currentBeamNode == null)
        {
            ctx.State = moveVec.sqrMagnitude > 0.001f ? PlayerState.Locomotion : PlayerState.Idle;
            return;
        }

        Vector3 beamAxis = currentBeamNode.GlobalRight;
        beamAxis.y = 0f;
        if (beamAxis.sqrMagnitude < 0.0001f)
        {
            beamAxis = currentBeamNode.GlobalRight.normalized;
        }
        else
        {
            beamAxis.Normalize();
        }

        float currentS = currentBeamNode.Project(transform.position);
        float maxSpeed = Input.GetKey(highProfileCode) ? runSpeed : walkSpeed;

        float alongDot = moveVec.sqrMagnitude > 0.001f ? Vector3.Dot(moveVec, beamAxis) : 0f;
        float alongSpeed = Mathf.Abs(alongDot) > 0.01f ? Mathf.Sign(alongDot) * maxSpeed : 0f;

        Vector3 oppositeMoveDir = Mathf.Abs(alongSpeed) > 0.001f
            ? beamAxis * Mathf.Sign(alongSpeed)
            : (Vector3.Dot(transform.forward, beamAxis) >= 0f ? beamAxis : -beamAxis);

        Debug.DrawRay(currentBeamNode.transform.position + Vector3.up * 0.5f, beamAxis, Color.black);

        bool atStart = currentS <= beamEdgeEpsilon;
        bool atEnd = currentS >= currentBeamNode.Length - beamEdgeEpsilon;

        if ((atStart || atEnd) && moveVec.sqrMagnitude > 0.001f)
        {
            Vector3 flatMove = new Vector3(moveVec.x, 0f, moveVec.z).normalized;
            Vector3 flatOpposite = new Vector3(oppositeMoveDir.x, 0f, oppositeMoveDir.z).normalized;
            float angleToOpposite = Vector3.Angle(flatMove, flatOpposite);
            Debug.DrawRay(transform.position, flatMove * 5, Color.blue);
            Debug.DrawRay(transform.position, flatOpposite * 5, Color.red);
            Debug.Log($"angle = {angleToOpposite}");

            if (angleToOpposite <= beamExitOppositeMaxAngle)
            {
                ExitBeamBalancing(moveVec);
                return;
            }
        }

        float nextS = Mathf.Clamp(currentS + alongSpeed * Time.deltaTime, 0f, currentBeamNode.Length);
        Vector3 targetLinePoint = currentBeamNode.PointAt(nextS);

        Vector3 desiredPos = new(targetLinePoint.x, transform.position.y, targetLinePoint.z);
        Vector3 horizontalDisplacement = desiredPos - transform.position;
        horizontalDisplacement.y = 0f;

        if (Mathf.Abs(alongSpeed) > 0.001f)
        {
            Vector3 faceDir = alongSpeed > 0f ? beamAxis : -beamAxis;
            float targetAngle = Mathf.Atan2(faceDir.x, faceDir.z) * Mathf.Rad2Deg;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVel, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }

        playerVelocity.x = Time.deltaTime > 0f ? horizontalDisplacement.x / Time.deltaTime : 0f;
        playerVelocity.z = Time.deltaTime > 0f ? horizontalDisplacement.z / Time.deltaTime : 0f;

        animator?.SetFloat("Speed", Mathf.Abs(alongSpeed), 0.1f, Time.deltaTime);
    }

    private void ExitBeamBalancing(Vector3 moveVec)
    {
        Debug.Log("Exit from beam balancing");
        currentBeamNode = null;
        float speed = moveVec.sqrMagnitude > 0.001f
            ? (Input.GetKey(highProfileCode) ? runSpeed : walkSpeed)
            : 0f;

        if (moveVec.sqrMagnitude > 0.001f)
        {
            playerVelocity.x = moveVec.x * speed;
            playerVelocity.z = moveVec.z * speed;
            ctx.State = PlayerState.Locomotion;

            controller.enabled = false;
            transform.position += playerVelocity * 0.05f;
            controller.enabled = true;
        }
        else
        {
            playerVelocity.x = 0f;
            playerVelocity.z = 0f;
            ctx.State = PlayerState.Idle;
        }

        animator?.SetFloat("Speed", speed, 0.1f, Time.deltaTime);
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