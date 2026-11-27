using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(PlayerContext))]
public class ParkourManager : MonoBehaviour
{
    [SerializeField] private KeyCode highProfileKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode dropKey = KeyCode.E;
    [SerializeField] private Vector3 boxSize = new(1.5f, 2.0f, 2.0f);
    [SerializeField] private LayerMask obstacleLayerMask;
    [SerializeField] private Vector3 hangingOffset = new(0f, -1.8f, 0f);
    [SerializeField] private float handLength = 0.1f;
    [SerializeField] private float shoulderLength = 0.5f;
    private const float boxForwardDistance = 1.5f;
    private LayerMask nodeLayer;
    private const int maxNodesForScan = 15;
    private readonly Collider[] foundNodes = new Collider[maxNodesForScan];
    private int foundNodesCount = 0;
    private const float maxLedgeTransferDistance = 2.5f;

    private PlayerContext ctx;
    private CharacterController controller;
    private Animator animator;
    private ParkourAnimatorIKBridge ikBridge;

    private ParkourNode currentNode = null;
    private Vector3 currentAnchorPoint = Vector3.zero;
    private const float transferRotationSpeed = 180f;
    private const float transferMovingSpeed = 3f;
    private const float shimmySpeed = 2f;

    private struct ParkourEdgeCandidate
    {
        public ParkourEdge Edge;
        public float Score;

        public ParkourEdgeCandidate(ParkourEdge edge, float score)
        {
            Edge = edge;
            Score = score;
        }
    }

    private void Awake()
    {
        ctx = GetComponent<PlayerContext>();
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        ikBridge = GetComponentInChildren<ParkourAnimatorIKBridge>();
        nodeLayer = LayerMask.GetMask(ParkourNode.LayerName);
    }

    private void Update()
    {
        ScanDropDownShimmyLedge();
        ScanWallToLedges();
        ScanNextLedgeOnWall();
        Shimmy();
        DropFromHanging();
        TransferIfClimbing();

        if (ctx.State == PlayerState.Hanging)
        {
            var (left, right) = CalculatePointForArms(currentAnchorPoint - hangingOffset, currentNode);
            ikBridge.SetPositionsForHands(left, right);
        }
        else
        {
            ikBridge.ResetAll();
        }
    }

    private void ScanDropDownShimmyLedge()
    {
        if (ctx.State != PlayerState.Idle && ctx.State != PlayerState.Locomotion && ctx.State != PlayerState.Balancing)
        {
            return;
        }

        Vector3 halfExtent = new(controller.radius * 1.5f, 0.3f, controller.radius * 1.5f);
        Vector3 boxStart = transform.position + Vector3.up * halfExtent.y;

        foundNodesCount = Physics.OverlapBoxNonAlloc(boxStart, halfExtent, foundNodes, transform.rotation, nodeLayer);
        ParkourNode bestNode = null;
        float minimalDistance = float.MaxValue;

        for (int i = 0; i < foundNodesCount; i++)
        {
            if (foundNodes[i].TryGetComponent<ParkourNode>(out var node) && node.NodeType == ParkourNodeType.Ledge)
            {
                Vector3 dirToNode = node.transform.position - node.PointAtProject(transform.position);
                float distance = dirToNode.magnitude;
                dirToNode.Normalize();

                if (minimalDistance > distance)
                {
                    minimalDistance = distance;
                    bestNode = node;
                }
            }
        }

        if (bestNode == null)
        {
            return;
        }

        string nodeFullName = bestNode.name;
        Transform curParent = bestNode.transform.parent;
        while (curParent != null)
        {
            nodeFullName = $"{curParent.name}/{nodeFullName}";
            curParent = curParent.parent;
        }
        Debug.Log($"May be drop down ledge: {nodeFullName}");

        if (Input.GetKeyDown(dropKey))
        {
            DropIntoHanging(bestNode);
        }
    }

    private void ScanWallToLedges()
    {
        if (ctx.State != PlayerState.Idle && ctx.State != PlayerState.Locomotion)
        {
            return;
        }

        float inputX = Input.GetAxisRaw("Horizontal");
        float inputY = Input.GetAxisRaw("Vertical");
        Vector3 inputVec = new Vector3(inputX, 0, inputY).normalized;

        Transform cameraTransform = Camera.main.transform;
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveVec = (cameraRight * inputVec.x + cameraForward * inputVec.z).normalized;

        ParkourNode bestNode = FindBestEntryNode(transform.position, moveVec);
        if (bestNode == null)
        {
            return;
        }

        string nodeFullName = bestNode.name;
        Transform curParent = bestNode.transform.parent;
        while (curParent != null)
        {
            nodeFullName = $"{curParent.name}/{nodeFullName}";
            curParent = curParent.parent;
        }
        Debug.Log($"Found entry parkour node: {nodeFullName}");

        if (Input.GetKey(highProfileKey) && Input.GetKeyDown(jumpKey))
        {
            TransferToNextLedge(bestNode);
        }
    }

    private ParkourNode FindBestEntryNode(Vector3 playerPosition, Vector3 inputDirection)
    {
        if (inputDirection.sqrMagnitude < 0.01f)
        {
            inputDirection = transform.forward;
        }
        else
        {
            inputDirection.Normalize();
        }

        Vector3 boxCenter = playerPosition + 0.5f * boxForwardDistance * inputDirection + boxSize.y * Vector3.up;
        Quaternion boxRotation = Quaternion.LookRotation(inputDirection);
        Vector3 halfExtents = boxSize * 0.5f;

        foundNodesCount = Physics.OverlapBoxNonAlloc(boxCenter, halfExtents, foundNodes, boxRotation, nodeLayer);

        ParkourNode bestNode = null;
        float highestScore = -float.MaxValue;

        for (int i = 0; i < foundNodesCount; i++)
        {
            if (foundNodes[i].TryGetComponent<ParkourNode>(out var node))
            {
                Vector3 dirToNode = node.transform.position - playerPosition;
                float distance = dirToNode.magnitude;
                dirToNode.Normalize();

                Vector3 dirToNodeProjection = new Vector3(dirToNode.x, 0f, dirToNode.z).normalized;
                float inputScore = Vector3.Dot(inputDirection, dirToNodeProjection);

                if (inputScore < 0.3f)
                {
                    continue;
                }

                float finalScore = inputScore / (distance + 0.1f);

                if (finalScore > highestScore)
                {
                    highestScore = finalScore;
                    bestNode = node;
                }
            }
        }

        return bestNode;
    }

    private void ScanNextLedgeOnWall()
    {
        if (ctx.State != PlayerState.Hanging)
        {
            return;
        }

        ParkourEdge bestEdge = null;
        float bestScore = -float.MaxValue;

        float inputY = Input.GetAxisRaw("Vertical");
        float inputX = Input.GetAxisRaw("Horizontal");
        Vector3 moveVec = (currentNode.GlobalRight * inputX + currentNode.transform.up * inputY).normalized;

        foreach (var edge in currentNode.OutgoingEdges)
        {
            if (!edge.FromRange.IsInto(currentNode.Project(transform.position)))
            {
                continue;
            }

            Vector3 fromPos = currentNode.PointAt((edge.FromRange.From + edge.FromRange.To) / 2);
            Vector3 rawDir = edge.GetDirection(fromPos);
            Debug.DrawRay(fromPos, moveVec, Color.blue);
            float distance = rawDir.magnitude;
            rawDir.Normalize();

            Vector3 dir = rawDir - currentNode.GlobalNormal * Vector3.Dot(rawDir, currentNode.GlobalNormal);
            Debug.DrawRay(fromPos, dir, Color.magenta);
            dir.Normalize();

            float dot = Vector3.Dot(dir, moveVec);
            if (dot < 0.8f)
            {
                continue;
            }

            float score = dot + maxLedgeTransferDistance - distance;

            if (score > bestScore)
            {
                bestScore = score;
                bestEdge = edge;
            }
        }

        if (bestEdge == null)
        {
            if (Mathf.Approximately(inputY, 0))
            {
                if (currentNode.Length != 0)
                {
                    ctx.State = PlayerState.Shimmy;
                    return;
                }
            }
            else if (inputY > 0)
            {
                if (CanClimbUp(out var posToTransfer, out var ledgePointForArms))
                {
                    ClimbUp(posToTransfer, ledgePointForArms);
                }
            }
            return;
        }

        string nodeFullName = bestEdge.TargetNode.name;
        Transform curParent = bestEdge.TargetNode.transform.parent;
        while (curParent != null)
        {
            nodeFullName = $"{curParent.name}/{nodeFullName}";
            curParent = curParent.parent;
        }
        Debug.Log($"Found best edge for transfering: {nodeFullName}");

        TransferToNextLedge(bestEdge.TargetNode);
    }

    private bool CanClimbUp(out Vector3 posToTransfer, out Vector3 ledgePointForArms)
    {
        posToTransfer = Vector3.zero;
        ledgePointForArms = Vector3.zero;

        Vector3 rawLedgePointForArms = currentNode.PointAtProject(transform.position);

        Vector3 newPlayerPos = rawLedgePointForArms - currentNode.GlobalNormal * controller.radius;

        if (Physics.CheckBox(rawLedgePointForArms + (controller.height / 2 + 0.05f) * Vector3.up, new(controller.radius, controller.height / 2, controller.radius), transform.rotation, obstacleLayerMask))
        {
            return false;
        }

        posToTransfer = newPlayerPos;
        ledgePointForArms = rawLedgePointForArms;
        return true;
    }

    private void ClimbUp(Vector3 posToTransfer, Vector3 ledgePointForArms)
    {
        currentAnchorPoint = posToTransfer;
        ctx.State = PlayerState.Climbing;
        currentNode = null;
    }

    private void Shimmy()
    {
        if (ctx.State != PlayerState.Shimmy)
        {
            return;
        }

        float inputY = Input.GetAxisRaw("Vertical");
        float inputX = Input.GetAxisRaw("Horizontal");
        Vector3 moveVec = (currentNode.GlobalRight * inputX + currentNode.transform.up * inputY).normalized;

        if (moveVec.sqrMagnitude < 0.001f || !Mathf.Approximately(inputY, 0))
        {
            ctx.State = PlayerState.Hanging;
            return;
        }

        float project = currentNode.Project(transform.position);
        Debug.Log($"{project} == {currentNode.Project(currentNode.Start)} = {Mathf.Abs(currentNode.Project(currentNode.Start) - project) <= 0.01f}");
        Debug.Log($"{project} == {currentNode.Project(currentNode.End)} = {Mathf.Abs(currentNode.Project(currentNode.End) - project) <= 0.01f}");
        Debug.Log($"{Vector3.Dot(moveVec, currentNode.GlobalRight)}");
        if (Mathf.Abs(currentNode.Project(currentNode.End) - project) <= 0.01f && Vector3.Dot(moveVec, currentNode.GlobalRight) > 0
            || Mathf.Abs(currentNode.Project(currentNode.Start) - project) <= 0.01f && Vector3.Dot(moveVec, currentNode.GlobalRight) < 0)
        {
            ctx.State = PlayerState.Hanging;
            return;
        }

        Vector3 move = Mathf.Sign(Vector3.Dot(moveVec, currentNode.GlobalRight)) * shimmySpeed * Time.deltaTime * currentNode.GlobalRight;
        Vector3 newPos = transform.position + move;
        float newPosProj = currentNode.Project(newPos);
        newPos = currentNode.PointAt(newPosProj) + hangingOffset;
        currentAnchorPoint = newPos;

        controller.enabled = false;
        transform.position = newPos;
        controller.enabled = true;
    }

    private void TransferToNextLedge(ParkourNode bestNode)
    {
        if (bestNode == null)
        {
            currentNode = null;
            return;
        }

        currentNode = bestNode;
        currentAnchorPoint = currentNode.PointAtProject(transform.position) + hangingOffset;
        ctx.State = PlayerState.Climbing;
    }

    private void DropFromHanging()
    {
        if (ctx.State != PlayerState.Hanging)
        {
            return;
        }

        if (Input.GetKeyDown(dropKey))
        {
            ctx.State = PlayerState.Jumping;
        }
    }

    private void DropIntoHanging(ParkourNode targetNode)
    {
        TransferToNextLedge(targetNode);
    }

    private void TransferIfClimbing()
    {
        if (ctx.State != PlayerState.Climbing)
        {
            return;
        }

        Quaternion targetRot = currentNode != null ? Quaternion.LookRotation(-currentNode.GlobalNormal) : transform.rotation;
        Vector3 targetPos = currentAnchorPoint;

        if (transform.position == targetPos && transform.rotation == targetRot)
        {
            ctx.State = currentNode != null ? PlayerState.Hanging : PlayerState.Idle;

            if (ctx.State == PlayerState.Hanging)
            {
                if (animator != null)
                {
                    animator.CrossFade("Hanging Idle", 0.15f);
                }
            }

            return;
        }

        Quaternion newRot = Quaternion.RotateTowards(transform.rotation, targetRot, transferRotationSpeed * Time.deltaTime);
        Vector3 newPos = Vector3.MoveTowards(transform.position, targetPos, transferMovingSpeed * Time.deltaTime);

        controller.enabled = false;
        transform.SetPositionAndRotation(newPos, newRot);
        controller.enabled = true;
    }

    private (Vector3 left, Vector3 right) CalculatePointForArms(Vector3 currentLedgePointForArms, ParkourNode node)
    {
        float distance = handLength;
        if (node.Length != 0)
        {
            float rawDistance = node.Length / 2f;
            distance = Mathf.Clamp(rawDistance, 0, shoulderLength / 2f);
        }

        Vector3 left = currentLedgePointForArms - node.Right * distance;
        Vector3 right = currentLedgePointForArms + node.Right * distance;

        return (left, right);
    }

    private void OnDrawGizmos()
    {
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputY = Input.GetAxisRaw("Vertical");
        Vector3 inputVec = new Vector3(inputX, 0, inputY).normalized;

        Transform cameraTransform = Camera.main.transform;
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveVec = (cameraRight * inputVec.x + cameraForward * inputVec.z).normalized;

        if (Application.isPlaying && (ctx.State == PlayerState.Idle || ctx.State == PlayerState.Locomotion))
        {
            Vector3 boxCenter = transform.position + 0.5f * boxForwardDistance * moveVec + boxSize.y * Vector3.up;
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
            Gizmos.DrawWireCube(boxCenter, boxSize);

            Vector3 halfExtent = new(controller.radius * 1.5f, 0.3f, controller.radius * 1.5f);
            boxCenter = transform.position + Vector3.up * halfExtent.y;
            Gizmos.color = Color.rebeccaPurple;
            Gizmos.DrawWireCube(boxCenter, halfExtent * 2);
        }

        if (foundNodesCount != 0)
        {
            Gizmos.color = Color.red;
            for (int i = 0; i < foundNodesCount; ++i)
            {
                var t = foundNodes[i].transform;
                var startPos = t.position;
                var node = foundNodes[i].GetComponent<ParkourNode>();
                Gizmos.DrawWireSphere(startPos, 0.1f);
                Gizmos.DrawRay(startPos, t.TransformDirection(node.Normal));
            }
        }
    }
}