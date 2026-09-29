using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SandshipController : MonoBehaviour
{
    // =========================================================
    // Forward / Reverse Movement
    // =========================================================

    [Header("Forward Movement")]

    [Tooltip("最大前进速度")]
    [SerializeField]
    private float maxForwardSpeed = 20f;

    [Tooltip("最大倒车速度")]
    [SerializeField]
    private float maxReverseSpeed = 8f;

    [Tooltip("按 W 时的前进加速度")]
    [SerializeField]
    private float forwardAcceleration = 8f;

    [Tooltip("前进时按下S的制动加速度")]
    [SerializeField]
    private float brakingAcceleration = 20f;

    [Tooltip("按 S 时的倒车加速度")]
    [SerializeField]
    private float reverseAcceleration = 6f;

    [Tooltip("没有 W/S 输入时，自然减速的速度")]
    [SerializeField]
    private float coastDeceleration = 5f;


    // =========================================================
    // Steering
    // =========================================================

    [Header("Steering")]

    [Tooltip("正常航速下的最大转向角速度，单位：度/秒")]
    [SerializeField]
    private float maxTurnSpeed = 110f;

    [Tooltip("正常航速下的转向角加速度，单位：度/秒²")]
    [SerializeField]
    private float turnAcceleration = 220f;

    [Tooltip("松开 A/D 后，角速度衰减速度")]
    [SerializeField]
    private float turnDeceleration = 260f;

    [Tooltip("静止时保留多少转向能力。0.3 = 30%")]
    [Range(0f, 1f)]
    [SerializeField]
    private float stationaryTurnControl = 0.3f;

    [Tooltip(
        "达到最大前进速度的多少比例时获得100%转向能力。" +
        "0.4代表达到40%最大速度后即可获得完整转向性能。"
    )]
    [Range(0.05f, 1f)]
    [SerializeField]
    private float fullTurnControlSpeedRatio = 0.4f;

    // =========================================================
    // Ground FX
    // =========================================================

    [Header("Ground FX")]

    [SerializeField]
    private ParticleSystem dustParticleLeft;

    [SerializeField]
    private ParticleSystem dustParticleRight;

    [SerializeField]
    private TrailRenderer sandTrail;

    [Tooltip("低速时扬沙粒子生成量")]
    [SerializeField]
    private float minDustRate = 2f;

    [Tooltip("最高速度时扬沙粒子生成量")]
    [SerializeField]
    private float maxDustRate = 18f;

    [Tooltip("低于这个速度时关闭拖痕和新扬沙")]
    [SerializeField]
    private float fxStartSpeed = 0.3f;


    // =========================================================
    // Runtime State
    // =========================================================

    private Rigidbody rb;

    // W/S 输入
    // W = +1
    // S = -1
    private float throttleInput;

    // A/D 输入
    // D = +1
    // A = -1
    private float steerInput;

    // 带正负号的当前纵向速度
    // 正数 = 前进
    // 负数 = 倒车
    private float forwardSpeed;

    // 当前角速度
    // 正数 = 向右转
    // 负数 = 向左转
    private float angularSpeed;


    // =========================================================
    // Public Read-only Values
    // =========================================================

    /// <summary>
    /// 当前绝对速度。
    /// 不区分前进还是倒车。
    /// </summary>
    public float CurrentSpeed => Mathf.Abs(forwardSpeed);

    /// <summary>
    /// 当前带方向的速度。
    /// 正数前进，负数倒车。
    /// </summary>
    public float SignedSpeed => forwardSpeed;

    /// <summary>
    /// 当前速度占最大前进速度的比例。
    /// 0~1。
    /// </summary>
    public float SpeedNormalized =>
        maxForwardSpeed > 0f
            ? Mathf.Clamp01(CurrentSpeed / maxForwardSpeed)
            : 0f;

    /// <summary>
    /// 当前角速度。
    /// </summary>
    public float AngularSpeed => angularSpeed;

    /// <summary>
    /// 当前实际转向控制系数。
    /// 静止约0.3，正常航速达到1。
    /// </summary>
    public float TurnControlFactor => CalculateTurnControlFactor();


    // =========================================================
    // Unity Life Cycle
    // =========================================================

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        ReadInput();
        UpdateGroundFX();
    }

    private void FixedUpdate()
    {
        UpdateMovement();
        UpdateRotation();
    }


    // =========================================================
    // Input
    // =========================================================

    private void ReadInput()
    {
        // Vertical:
        // W = +1
        // S = -1
        throttleInput = Input.GetAxisRaw("Vertical");

        // Horizontal:
        // D = +1
        // A = -1
        steerInput = Input.GetAxisRaw("Horizontal");
    }


    // =========================================================
    // Movement
    // =========================================================

    private void UpdateMovement()
    {
        float deltaTime = Time.fixedDeltaTime;

        // -----------------------------------------------------
        // W：目标速度逐渐接近最大前进速度
        // -----------------------------------------------------

        if (throttleInput > 0.01f)
        {
            if(forwardSpeed >= 0f)
            {
                forwardSpeed = Mathf.MoveTowards(
                                forwardSpeed,
                                maxForwardSpeed,
                                forwardAcceleration * deltaTime
                );
            }
            else
            {
                forwardSpeed = Mathf.MoveTowards(
                    forwardSpeed,
                    0,
                    brakingAcceleration * deltaTime
                );
            }
        }

        // -----------------------------------------------------
        // S：目标速度逐渐接近最大倒车速度
        // -----------------------------------------------------

        else if (throttleInput < -0.01f)
        {
            if(forwardSpeed > 0.01)
            {
                 forwardSpeed = Mathf.MoveTowards(
                    forwardSpeed,
                    0,
                    brakingAcceleration * deltaTime
                 );
            }
            else
            {
                forwardSpeed = Mathf.MoveTowards(
                    forwardSpeed,
                    -maxReverseSpeed,
                    reverseAcceleration * deltaTime
                 );
            }
        }

        // -----------------------------------------------------
        // 没输入：自然滑行减速
        // -----------------------------------------------------

        else
        {
            forwardSpeed = Mathf.MoveTowards(
                forwardSpeed,
                0f,
                coastDeceleration * deltaTime
            );
        }

        // -----------------------------------------------------
        // 永远沿“船头方向”移动
        // -----------------------------------------------------

        Vector3 movement =
            transform.forward *
            forwardSpeed *
            deltaTime;

        rb.MovePosition(
            rb.position + movement
        );
    }


    // =========================================================
    // Steering
    // =========================================================

    private void UpdateRotation()
    {
        float deltaTime = Time.fixedDeltaTime;

        // 根据当前航速计算：
        // 静止 = 30%
        // 正常航速 = 100%
        float turnControlFactor =
            CalculateTurnControlFactor();

        // 当前允许的最大角速度
        float currentMaxTurnSpeed =
            maxTurnSpeed *
            turnControlFactor;

        // 当前允许的角加速度
        float currentTurnAcceleration =
            turnAcceleration *
            turnControlFactor;


        // -----------------------------------------------------
        // 有 A / D 输入
        // -----------------------------------------------------

        if (Mathf.Abs(steerInput) > 0.01f)
        {
            // 玩家希望达到的目标角速度
            float targetAngularSpeed =
                steerInput *
                currentMaxTurnSpeed;

            // 当前角速度逐渐接近目标角速度
            angularSpeed = Mathf.MoveTowards(
                angularSpeed,
                targetAngularSpeed,
                currentTurnAcceleration * deltaTime
            );
        }

        // -----------------------------------------------------
        // 松开 A / D
        // -----------------------------------------------------

        else
        {
            angularSpeed = Mathf.MoveTowards(
                angularSpeed,
                0f,
                turnDeceleration * deltaTime
            );
        }


        // -----------------------------------------------------
        // 应用船体旋转
        // -----------------------------------------------------

        Quaternion deltaRotation =
            Quaternion.Euler(
                0f,
                angularSpeed * deltaTime,
                0f
            );

        rb.MoveRotation(
            rb.rotation * deltaRotation
        );
    }


    // =========================================================
    // Turn Control
    // =========================================================

    private float CalculateTurnControlFactor()
    {
        // 达到多少速度后获得100%转向能力
        float fullControlSpeed =
            maxForwardSpeed *
            fullTurnControlSpeedRatio;

        // 防止数值异常
        if (fullControlSpeed <= 0.001f)
        {
            return 1f;
        }

        // CurrentSpeed:
        // 0 ---------------- fullControlSpeed
        //
        // speed01:
        // 0 ---------------- 1
        float speed01 = Mathf.InverseLerp(
            0f,
            fullControlSpeed,
            CurrentSpeed
        );

        // 静止时 stationaryTurnControl
        // 正常航速时 1
        return Mathf.Lerp(
            stationaryTurnControl,
            1f,
            speed01
        );
    }

    // =========================================================
    // Ground FX
    // =========================================================

    private void UpdateDustParticle(ParticleSystem dustParticle, bool isMoving)
    {
        // -------------------------
        // 扬沙
        // -------------------------
        if (dustParticle != null)
        {
            var emission = dustParticle.emission;

            // 完全关闭 Rate over Distance
            emission.rateOverDistanceMultiplier = 0f;

            if (!isMoving)
            {
                emission.rateOverTimeMultiplier = 0f;
            }
            else
            {
                float speed01 = SpeedNormalized;

                // 让低速也有明显扬沙，
                // 高速则增长得更明显
                float dustFactor = Mathf.Pow(speed01, 0.65f);

                emission.rateOverTimeMultiplier =
                    Mathf.Lerp(
                        minDustRate,
                        maxDustRate,
                        dustFactor
                    );
            }

            // 防止粒子系统因为之前停过而不再播放
            if (!dustParticle.isPlaying)
            {
                dustParticle.Play();
            }
        }
    }

    private void UpdateGroundFX()
    {
        bool isMoving =
            CurrentSpeed > fxStartSpeed;


        // -----------------------------------------------------
        // Sand Trail
        // -----------------------------------------------------

        if (sandTrail != null)
        {
            sandTrail.emitting = isMoving;
        }


        // -----------------------------------------------------
        // Dust Particle
        // -----------------------------------------------------
        UpdateDustParticle(dustParticleLeft, isMoving);
        UpdateDustParticle(dustParticleRight, isMoving);
    }


}