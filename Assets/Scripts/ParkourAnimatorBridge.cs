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

    public void SetPositionsForHands(Vector3 left, Vector3 right)
    {
        leftHandIKWeight = 1f;
        rightHandIKWeight = 1f;
        leftHandTarget = left;
        rightHandTarget = right;
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

    public void ResetHandsIK()
    {
        leftHandIKWeight = 0f;
        rightHandIKWeight = 0f;
        leftHandTarget = Vector3.zero;
        rightHandTarget = Vector3.zero;
    }

    public void ResetAll()
    {
        ResetCurrentAnchorPoint();
        ResetHandsIK();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator)
        {
            switch (ctx.State)
            {
                case PlayerState.Hanging:
                    animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, leftHandIKWeight);
                    animator.SetIKPositionWeight(AvatarIKGoal.RightHand, rightHandIKWeight);
                    animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandTarget);
                    animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandTarget);
                    break;
            }
        }
    }

    private void OnAnimatorMove()
    {
        controller.enabled = false;
        transform.parent.position += animator.deltaPosition;
        controller.enabled = true;
    }
}