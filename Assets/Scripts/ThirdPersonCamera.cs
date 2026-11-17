using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target & Offset")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Distance & Collision")]
    [SerializeField] private float defaultDistance = 3.5f;
    [SerializeField] private float minDistance = 0.6f;
    [SerializeField] private LayerMask collisionLayers;
    [SerializeField] private float cameraRadius = 0.2f;

    [Header("Sensitivity & Limits")]
    [SerializeField] private float mouseSensitivity = 2.5f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 60f;

    [Header("Effects")]
    [SerializeField] private float smoothTime = 0.08f;

    private float curYaw;
    private float curPitch;
    private Vector3 curVel;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Vector3 angles = transform.eulerAngles;
        curYaw = angles.y;
        curPitch = angles.x;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        curYaw += Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        curPitch -= Input.GetAxisRaw("Mouse Y") * mouseSensitivity;
        curPitch = Mathf.Clamp(curPitch, minPitch, maxPitch);

        Quaternion targetRotation = Quaternion.Euler(curPitch, curYaw, 0f);
        Vector3 focusePoint = target.position + targetOffset;

        Vector3 desiredPos = focusePoint - (targetRotation * Vector3.forward * defaultDistance);
        float targetDistance = defaultDistance;
        Vector3 rayDir = (desiredPos - focusePoint).normalized;
        if (Physics.SphereCast(focusePoint, cameraRadius, rayDir, out RaycastHit hit, defaultDistance, collisionLayers))
            targetDistance = Mathf.Clamp(hit.distance, minDistance, defaultDistance);
        Vector3 targetPos = focusePoint + rayDir * targetDistance;
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref curVel, smoothTime);
        transform.rotation = targetRotation;
    }
}