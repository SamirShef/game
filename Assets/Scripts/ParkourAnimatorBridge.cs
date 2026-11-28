using UnityEngine;

[RequireComponent(typeof(Animator))]
class ParkourAnimatorBridge : MonoBehaviour
{
    private ParkourManager parkourManager;
    private CharacterController controller;
    private PlayerContext ctx;
    private Animator animator;

    private float leftHandIKWeight = 0f;
    private float rightHandIKWeight = 0f;
    private Vector3 leftHandTargetOld = Vector3.zero;
    private Vector3 rightHandTargetOld = Vector3.zero;
    private Vector3 leftHandTargetNext = Vector3.zero;
    private Vector3 rightHandTargetNext = Vector3.zero;
    private Vector3 leftHandTarget = Vector3.zero;
    private Vector3 rightHandTarget = Vector3.zero;

    private Vector3 currentAnchorPoint = Vector3.zero;

    private void Awake()
    {
        parkourManager = GetComponentInParent<ParkourManager>();
        controller = GetComponentInParent<CharacterController>();
        ctx = GetComponentInParent<PlayerContext>();
        animator = GetComponent<Animator>();
    }

    public void SetLeftHandPositionOld(Vector3 pos)
    {
        leftHandTargetOld = pos;
    }

    public void SetRightHandPositionOld(Vector3 pos)
    {
        rightHandTargetOld = pos;
    }

    public void SetPositionsForHandsOld(Vector3 left, Vector3 right)
    {
        SetLeftHandPositionOld(left);
        SetRightHandPositionOld(right);
    }

    public void SetLeftHandPositionNext(Vector3 pos)
    {
        leftHandTargetNext = pos;
    }

    public void SetRightHandPositionNext(Vector3 pos)
    {
        rightHandTargetNext = pos;
    }

    public void SetPositionsForHandsNext(Vector3 left, Vector3 right)
    {
        SetLeftHandPositionNext(left);
        SetRightHandPositionNext(right);
    }

    public void SetLeftHandPosition(Vector3 pos)
    {
        leftHandTarget = pos;
    }

    public void SetRightHandPosition(Vector3 pos)
    {
        rightHandTarget = pos;
    }

    public void SetPositionsForHands(Vector3 left, Vector3 right)
    {
        SetLeftHandPosition(left);
        SetRightHandPosition(right);
    }

    public void SetPositionsForHandsFromOld()
    {
        leftHandTarget = leftHandTargetOld;
        rightHandTarget = rightHandTargetOld;
    }

    public void SetPositionsForHandsFromNext()
    {
        leftHandTarget = leftHandTargetNext;
        rightHandTarget = rightHandTargetNext;
    }

    public void SetHandIK(float left, float right)
    {
        leftHandIKWeight = left;
        rightHandIKWeight = right;
    }

    public void ApplyHandIK()
    {
        SetHandIK(1f, 1f);
    }

    public void SetCurrentAnchorPoint(Vector3 anchorPoint)
    {
        currentAnchorPoint = anchorPoint;
    }

    public void ClimbUp()
    {
        controller.enabled = false;
        transform.parent.position = currentAnchorPoint;
        controller.enabled = true;
        ctx.State = PlayerState.Idle;
        ResetCurrentAnchorPoint();
    }

    public void ResetCurrentAnchorPoint()
    {
        currentAnchorPoint = Vector3.zero;
    }

    public void ResetHandIK()
    {
        leftHandIKWeight = 0f;
        rightHandIKWeight = 0f;
    }

    public void ResetHandPositions()
    {
        leftHandTargetOld = Vector3.zero;
        rightHandTargetOld = Vector3.zero;
        leftHandTargetNext = Vector3.zero;
        rightHandTargetNext = Vector3.zero;
        leftHandTarget = Vector3.zero;
        rightHandTarget = Vector3.zero;
        Debug.Log("ResetHandPositions");
    }

    public void ResetAll()
    {
        ResetCurrentAnchorPoint();
        ResetHandIK();
        ResetHandPositions();
    }

    public void RootMotionNotWorks()
    {
        parkourManager.RootMotionNotWorks();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, leftHandIKWeight);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, rightHandIKWeight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandTarget);
            animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandTarget);
        }
    }

    private void OnAnimatorMove()
    {
        controller.enabled = false;
        transform.parent.position += animator.deltaPosition;
        controller.enabled = true;
    }
}