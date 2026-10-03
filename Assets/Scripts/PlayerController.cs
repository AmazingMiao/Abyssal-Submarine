using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events; // 新增：用于死亡事件

using UnityEngine.SceneManagement; // 引入 SceneManagement

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class PlayerController : MonoBehaviour
{
    public Rigidbody2D rb;
    public CircleCollider2D coll;

    [Header("Settings")]
    public float forwardSpeed = 5f;    // 前进速度（单位/秒）
    public float backwardSpeed = 3f;   // 后退速度（单位/秒）
    public float rotationSpeed = 180f; // 旋转速度（度/秒）

    [Header("Acceleration")]
    public float acceleration = 20f;      // 加速率（单位/秒^2），用于速度增大时
    public float deceleration = 30f;      // 减速率（单位/秒^2），用于速度减小时或空档减速
    public float strafeAcceleration = 25f;// 侧移（左右）加速率，可单独调节
    private Vector2 currentVelocity = Vector2.zero; // 当前平滑速度（世界坐标）

    [Header("Inertia")]
    [Range(0f, 1f)]
    public float inertia = 0.85f; // 惯性系数：越接近1保持越多旧速度，越接近0则几乎瞬时响应
    public float brakeDrag = 6f;  // 无输入时的阻尼强度（用于快速减速）
    public bool useSeparateStrafeInertia = false; // 是否对左右和前后使用分离惯性
    [Tooltip("分离惯性时，x轴（侧移）惯性系数")]
    [Range(0f,1f)]
    public float strafeInertia = 0.7f;

    // Health
    [Header("Health")]
    public int maxHealth = 5;
    public int currentHealth;
    public UnityEvent onDeath; // 可在 Inspector 指定死亡时的响应
    [Tooltip("死亡后加载的场景名称")]
    public string deathSceneName = "END";
    [Tooltip("死亡后延迟多少秒加载场景")]
    public float deathLoadDelay = 3f;

    // Knockback
    [Header("Knockback")]
    public float knockbackForce = 5f;
    public float knockbackDecay = 5f; // 越大衰减越快

    // 防重复受击
    [Header("Wall Hit")]
    public float wallHitInvulnerability = 0.5f; // 碰到 Wall 后无敌时间（秒）
    private float lastWallHitTime = -Mathf.Infinity;

    [Header("Audio FX")]
    public AudioSource engineAudioSource;
    public AudioLowPassFilter engineLowPassFilter;
    [Tooltip("最小音量（静止时）")]
    public float minVolume = 0.2f;
    [Tooltip("最大音量（全速时）")]
    public float maxVolume = 1.0f;
    [Tooltip("最小音调")]
    public float minPitch = 0.8f;
    [Tooltip("最大音调")]
    public float maxPitch = 1.2f;
    [Tooltip("低通滤波最小频率（后方/闷）")]
    public float minCutoffFreq = 800f;
    [Tooltip("低通滤波最大频率（前方/清脆）")]
    public float maxCutoffFreq = 22000f;
    [Tooltip("立体声平移强度")]
    public float panIntensity = 1.0f;

    [Header("Collision Audio")]
    public AudioSource collisionAudioSource; // 专门用于播放碰撞音效的 AudioSource
    public AudioLowPassFilter collisionLowPassFilter;
    public AudioClip[] collisionClips;       // 碰撞音效池
    [Tooltip("碰撞音效最小音量")]
    public float minCollisionVolume = 0.5f;
    [Tooltip("碰撞音效最大音量")]
    public float maxCollisionVolume = 1.0f;

    [Header("ESP32 Input")]
    public ESP32InputController esp32Controller;
    public bool enableESP32 = true;
    public Vector2 joystickCenter = new Vector2(2048, 2048);
    public float joystickRange = 2048f;
    public float esp32Deadzone = 0.1f;
    public bool invertESP32Y = false;
    [Tooltip("If true, the first received value will be used as the center point.")]
    public bool calibrateOnStart = false;
    // public bool waitForValidInput = true; // Removed as per request

    [Header("Debug Info")]
    public Vector2 currentESP32Raw;
    public Vector2 currentESP32Normalized;
    public Vector2 finalInput;

    private bool isCalibrated = false;
    // private bool hasValidInputStarted = false;

    // internal knockback velocity
    private Vector2 knockbackVelocity;

    // Input System
    private PlayerInputActions inputActions;
    private Vector2 moveInput;
    private float rotateInput;

    private void Awake()
    {
        // 初始化 Input Actions
        inputActions = new PlayerInputActions();

        // 初始化血量
        currentHealth = maxHealth;
    }

    private void OnEnable()
    {
        inputActions.Enable();

        // 订阅 Move 输入
        inputActions.Player.Move.performed += OnMove;
        inputActions.Player.Move.canceled += OnMove;

        // 订阅 Rotate 输入
        inputActions.Player.Rotate.performed += OnRotate;
        inputActions.Player.Rotate.canceled += OnRotate;
    }

    private void OnDisable()
    {
        inputActions.Player.Move.performed -= OnMove;
        inputActions.Player.Move.canceled -= OnMove;
        inputActions.Player.Rotate.performed -= OnRotate;
        inputActions.Player.Rotate.canceled -= OnRotate;
        inputActions.Disable();
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void OnRotate(InputAction.CallbackContext context)
    {
        rotateInput = context.ReadValue<float>();
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        coll = GetComponent<CircleCollider2D>();

        if (esp32Controller == null)
            esp32Controller = FindObjectOfType<ESP32InputController>();

        // 自动获取音频组件 (优先获取子物体上的组件，如果没有则获取自身的)
        if (engineAudioSource == null) engineAudioSource = GetComponentInChildren<AudioSource>();
        if (engineLowPassFilter == null) engineLowPassFilter = GetComponentInChildren<AudioLowPassFilter>();
        
        // 如果没有指定碰撞音效源，尝试查找名为 "CollisionAudio" 的子物体，或者复用 engineAudioSource (不推荐)
        if (collisionAudioSource == null)
        {
            Transform collisionAudioObj = transform.Find("CollisionAudio");
            if (collisionAudioObj != null)
            {
                collisionAudioSource = collisionAudioObj.GetComponent<AudioSource>();
                collisionLowPassFilter = collisionAudioObj.GetComponent<AudioLowPassFilter>();
            }
        }

        // 锁定物理旋转，防止碰撞产生旋转；仍可通过脚本（transform.Rotate / rb.MoveRotation）进行控制旋转
        rb.freezeRotation = true;
        rb.angularVelocity = 0f;
    }

    private void Update()
    {
        UpdateAudioFX();
    }

    private void UpdateAudioFX()
    {
        if (engineAudioSource == null) return;

        // 1. 获取当前速度（本地坐标系）
        // 将世界速度转换为本地速度：x为左右（侧移），y为前后
        Vector2 localVelocity = transform.InverseTransformDirection(rb.velocity);
        float speed = localVelocity.magnitude;
        float maxSpeedRef = forwardSpeed; // 参考最大速度

        // 2. 音量和音调随速度增加
        float speedRatio = Mathf.Clamp01(speed / maxSpeedRef);
        engineAudioSource.volume = Mathf.Lerp(minVolume, maxVolume, speedRatio);
        engineAudioSource.pitch = Mathf.Lerp(minPitch, maxPitch, speedRatio);

        // 3. 立体声平移 (Stereo Pan)
        // 目标：声音听起来像是在移动方向的反方向
        // 如果向右移动 (localVelocity.x > 0)，声音应在左边 (Pan < 0)
        // 如果向左移动 (localVelocity.x < 0)，声音应在右边 (Pan > 0)
        float targetPan = -Mathf.Clamp(localVelocity.x / (maxSpeedRef * 0.5f), -1f, 1f) * panIntensity;
        engineAudioSource.panStereo = targetPan;

        // 4. 低通滤波 (Low Pass Filter)
        // 目标：声音听起来像是在移动方向的反方向
        // 如果向前移动 (localVelocity.y > 0)，声音源(引擎/尾流)在后方 -> 闷 (Low Freq)
        // 如果向后移动 (localVelocity.y < 0)，声音源在前方 -> 清脆 (High Freq)
        if (engineLowPassFilter != null)
        {
            // 归一化 Y 轴速度 (-1 ~ 1)
            float yRatio = Mathf.Clamp(localVelocity.y / maxSpeedRef, -1f, 1f);
            
            // 向前移动时，声音被甩在后面，变闷 (Max -> Min)
            // 向后移动或静止时，声音正常 (Max)
            float targetFreq;
            if (yRatio > 0)
            {
                targetFreq = Mathf.Lerp(maxCutoffFreq, minCutoffFreq, yRatio);
            }
            else
            {
                targetFreq = maxCutoffFreq;
            }
            
            engineLowPassFilter.cutoffFrequency = targetFreq;
        }
    }

    private void FixedUpdate()
    {
        // 将输入限制在单位圆内（避免对角速度过快）
        Vector2 input = moveInput;
        // ESP32 Input Logic
        if (enableESP32 && esp32Controller != null)
        {
            // Check if we have data, otherwise default to 0,0 which maps to -1,-1. 
            // To prevent initial drift if no data, we can check HasReceivedData if available, 
            // but user asked for "raw" behavior.
            if (esp32Controller.HasReceivedData)
            {
                Vector2 raw = esp32Controller.JoystickInput;
                currentESP32Raw = raw;

                // Direct mapping 0-4095 -> -1..1
                // 0 -> -1
                // 2048 -> ~0
                // 4095 -> 1
                float x = raw.x;
                float y = raw.y;
                
                if (invertESP32Y) y = -y;

                Vector2 espInput = new Vector2(x, y);
                currentESP32Normalized = espInput;

                // 死区处理
                if (espInput.magnitude < esp32Deadzone)
                {
                    espInput = Vector2.zero;
                }

                // 如果有有效输入，覆盖键盘输入
                // Note: If joystick is centered (0,0 after mapping), we might want to allow keyboard?
                // But usually joystick overrides.
                input = espInput;
            }
        }

        if (input.sqrMagnitude > 1f) input = input.normalized;

        finalInput = input;

        // 根据前进/后退分别应用速度（x 沿右侧使用 forwardSpeed 作为 strafing 基准）
        float localX = input.x * forwardSpeed;
        float localY = input.y >= 0f ? input.y * forwardSpeed : input.y * backwardSpeed; // input.y 可能为负

        // 计算目标速度（本地 -> 世界）
        Vector3 targetWorld3 = transform.TransformDirection(new Vector3(localX, localY, 0f));
        Vector2 targetVelocity = new Vector2(targetWorld3.x, targetWorld3.y);

        // 惯性处理：
        // - 当有输入时，按加速/减速速率推动速度朝目标速度变化，同时保留历史速度的一部分（inertia）
        // - 当无输入时，应用阻尼（brakeDrag）快速衰减
        if (input.sqrMagnitude > 0.001f)
        {
            // 计算速率（扩展原有 acceleration/deceleration）
            float speedDelta = targetVelocity.magnitude - currentVelocity.magnitude;
            float rate = speedDelta > 0f ? acceleration : deceleration;

            if (useSeparateStrafeInertia)
            {
                // 将 currentVelocity 投影回本地空间以分离处理 X/Y 的惯性
                Vector2 localCurrent = transform.InverseTransformDirection(currentVelocity);
                Vector2 localTarget = new Vector2(localX, localY);

                // 对左右与前后分别平滑（结合 inertia）
                float lerpX = Mathf.Clamp01((1f - strafeInertia) * rate * Time.fixedDeltaTime);
                float lerpY = Mathf.Clamp01((1f - inertia) * rate * Time.fixedDeltaTime);
                localCurrent.x = Mathf.Lerp(localCurrent.x, localTarget.x, lerpX);
                localCurrent.y = Mathf.Lerp(localCurrent.y, localTarget.y, lerpY);

                currentVelocity = transform.TransformDirection(new Vector3(localCurrent.x, localCurrent.y, 0f));
            }
            else
            {
                // 普通模式：在 MoveTowards 基础上混合历史速度（inertia）
                Vector2 reached = Vector2.MoveTowards(currentVelocity, targetVelocity, rate * Time.fixedDeltaTime);
                // 保留上一帧速度的一部分（inertia），并混合到 reached
                currentVelocity = Vector2.Lerp(reached, currentVelocity, Mathf.Clamp01(inertia));
            }
        }
        else
        {
            // 无输入：按阻尼快速减速，同时保留少量惯性
            // 使用指数衰减
            float dragFactor = 1f / (1f + brakeDrag * Time.fixedDeltaTime);
            currentVelocity *= dragFactor;
            // 小阈值清零
            if (currentVelocity.sqrMagnitude < 0.01f) currentVelocity = Vector2.zero;
        }

        // 将当前速度与 knockback 相加并赋给刚体（单位/秒）
        rb.velocity = currentVelocity + knockbackVelocity;

        // 让 knockback 平滑衰减到 0
        knockbackVelocity = Vector2.Lerp(knockbackVelocity, Vector2.zero, knockbackDecay * Time.fixedDeltaTime);

        // 旋转（Q 为 negative，E 为 positive）- 方向反转
        if (rotateInput != 0f)
        {
            transform.Rotate(0f, 0f, -rotateInput * rotationSpeed * Time.fixedDeltaTime);
        }
    }

    // 受伤函数
    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0);
        Debug.Log($"Player took {amount} dmg. HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // 治疗函数
    public void Heal(int amount)
    {
        if (amount <= 0) return;
        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        Debug.Log($"Player healed {amount}. HP: {currentHealth}/{maxHealth}");
    }

    // 死亡处理
    private void Die()
    {
        Debug.Log("Player died.");
        // 触发 Inspector 中绑定的事件（如播放动画、重生等）
        onDeath?.Invoke();

        // 简单处理：禁用控制脚本
        enabled = false;
        rb.velocity = Vector2.zero;

        // 延迟加载结束场景
        StartCoroutine(LoadDeathSceneRoutine());
    }

    private System.Collections.IEnumerator LoadDeathSceneRoutine()
    {
        yield return new WaitForSeconds(deathLoadDelay);
        SceneManager.LoadScene(deathSceneName);
    }

    // 碰撞：与标签为 "Wall" 的对象碰撞造成伤害并往反方向弹回一点
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("Wall")) return;

        // 如果处于短时间无敌，忽略这次碰撞
        if (Time.time - lastWallHitTime < wallHitInvulnerability) return;

        // 记录最近一次碰撞时间，开始无敌计时
        lastWallHitTime = Time.time;

        // 造成 1 点伤害
        TakeDamage(1);

        // 清除角速度以防万一，并获取碰撞法线把一部分速度加入 knockbackVelocity（不直接覆盖 rb.velocity）
        rb.angularVelocity = 0f;

        if (collision.contacts != null && collision.contacts.Length > 0)
        {
            Vector2 contactPoint = collision.contacts[0].point;
            Vector2 contactNormal = collision.contacts[0].normal;
            
            // 播放碰撞音效
            PlayCollisionSound(contactPoint);

            // 将反向冲量加入 knockbackVelocity
            knockbackVelocity += contactNormal * knockbackForce;
        }
    }

    private void PlayCollisionSound(Vector2 contactPoint)
    {
        if (collisionAudioSource == null || collisionClips == null || collisionClips.Length == 0) return;

        // 1. 计算碰撞点相对于玩家的本地位置
        Vector2 localContactPoint = transform.InverseTransformPoint(contactPoint);

        // 2. 立体声平移 (Stereo Pan)
        // 如果碰撞点在右边 (x > 0)，声音应在右边 (Pan > 0)
        // 如果碰撞点在左边 (x < 0)，声音应在左边 (Pan < 0)
        // 注意：这与引擎声相反，引擎声是模拟"被甩在后面"，而碰撞声是"源头就在那里"
        float targetPan = Mathf.Clamp(localContactPoint.x / 2f, -1f, 1f) * panIntensity;
        collisionAudioSource.panStereo = targetPan;

        // 3. 低通滤波 (Low Pass Filter)
        // 如果碰撞点在前方 (y > 0)，声音清脆 (Max Freq)
        // 如果碰撞点在后方 (y < 0)，声音沉闷 (Min Freq)
        if (collisionLowPassFilter != null)
        {
            float yRatio = Mathf.Clamp(localContactPoint.y / 2f, -1f, 1f);
            float targetFreq;
            if (yRatio > 0)
            {
                // 前方：清脆
                targetFreq = Mathf.Lerp(minCutoffFreq, maxCutoffFreq, yRatio);
            }
            else
            {
                // 后方：沉闷
                targetFreq = Mathf.Lerp(minCutoffFreq, maxCutoffFreq, (yRatio + 1f) * 0.5f); // 简单映射
                // 或者直接用 minCutoffFreq
                targetFreq = minCutoffFreq;
            }
            collisionLowPassFilter.cutoffFrequency = targetFreq;
        }

        // 4. 随机音量和音调
        collisionAudioSource.volume = Random.Range(minCollisionVolume, maxCollisionVolume);
        collisionAudioSource.pitch = Random.Range(0.9f, 1.1f);

        // 5. 播放随机音效
        AudioClip clip = collisionClips[Random.Range(0, collisionClips.Length)];
        collisionAudioSource.PlayOneShot(clip);
    }

    // 可视化调试
    private void OnDrawGizmosSelected()
    {
        if (rb != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)(rb.velocity * 0.1f));
        }
    }
}