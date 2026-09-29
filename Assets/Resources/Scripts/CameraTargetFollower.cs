using UnityEngine;

public class CameraTargetFollower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform ship;
    [SerializeField] private SandshipController shipController;

    [Header("Look Ahead")]
    [Tooltip("满速时相机前视目标距离")]
    [SerializeField] private float maxLookAheadDistance = 7f;

    [Tooltip("前视方向与距离变化的平滑时间")]
    [SerializeField] private float lookAheadSmoothTime = 0.3f;

    [Tooltip("低于该速度时开始视为静止")]
    [SerializeField] private float minLookAheadSpeed = 0.1f;

    private Vector3 currentLookAheadOffset;
    private Vector3 lookAheadVelocity;

    private void Start()
    {
        if (ship != null)
        {
            transform.position = ship.position;
        }
    }

    private void Update()
    {
        if (ship == null || shipController == null)
            return;

        UpdateTargetPosition();
    }

    private void UpdateTargetPosition()
    {
        // 1. 根据当前速度决定前视距离
        float speed01 = shipController.SpeedNormalized;

        Vector3 targetOffset = Vector3.zero;

        if (shipController.CurrentSpeed > minLookAheadSpeed)
        {
            // 前进看船头，倒车看船尾
            Vector3 travelDirection =
                shipController.SignedSpeed >= 0f
                    ? ship.forward
                    : -ship.forward;

            targetOffset =
                travelDirection *
                maxLookAheadDistance *
                speed01;
        }

        // 2. 只平滑“前视偏移”
        currentLookAheadOffset =
            Vector3.SmoothDamp(
                currentLookAheadOffset,
                targetOffset,
                ref lookAheadVelocity,
                lookAheadSmoothTime
            );

        // 3. 船本体位置完全直接跟随，不再 SmoothDamp
        transform.position =
            ship.position +
            currentLookAheadOffset;
    }
}