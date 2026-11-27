using UnityEngine;

[RequireComponent(typeof(Animator))]
class ParkourAnimatorIKBridge : MonoBehaviour
{
    private ParkourManager parkourManager;
    private PlayerContext ctx;
    private Animator animator;

    private float leftHandIKWeight = 0f;
    private float rightHandIKWeight = 0f;
    private Vector3 leftHandTarget = Vector3.zero;
    private Vector3 rightHandTarget = Vector3.zero;

    private void Awake()
    {
        parkourManager = GetComponentInParent<ParkourManager>();
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

    public void ResetAll()
    {
        leftHandIKWeight = 0f;
        rightHandIKWeight = 0f;
        leftHandTarget = Vector3.zero;
        rightHandTarget = Vector3.zero;
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
}