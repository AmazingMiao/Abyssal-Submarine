using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class SubmarineRadarGenerator : MonoBehaviour
{
    // --- Prefab 模板引用 (必须在 Inspector 中拖入) ---
    [Header("UI Templates (REQUIRED)")]
    public RectTransform segmentBarTemplate;      // Segment Bar 的 Prefab (应带 Image)
    public TextMeshProUGUI distanceTextTemplate; // 距离文本的 Prefab

    // --- 外部引用 ---
    [Header("References")]
    public Transform submarineTransform;          // 场景中潜艇的实际 Transform
    public GameObject centralIcon;               // 场景中的 Submarine_Icon (现在会随玩家旋转)

    // --- 配置参数 ---
    [Header("Radar Configuration")]
    public float maxDetectionRange = 1000f;
    public float textDistanceFromCenter = 150f;  // 文本显示在距中心多远的位置（用于文本）
    public float barDistanceFromCenter = 120f;   // 条（bar）显示在距中心多远的位置（独立可调）
    public float maxBarHeight = 180f;            // （保持兼容）不再用于缩放
    public float segmentBarLength = 60f;         // 固定的 segment bar 长度（像素）
    public float segmentBarWidth = 10f;          // 固定的 segment bar 宽度

    [Header("Player Size")]
    public float playerRadius = 1f; // 玩家半径（用于从玩家边缘开始计算距离）

    [Header("Debug")]
    public bool debugDrawRays = true;
    public LayerMask wallLayerMask = ~0; // 可在 Inspector 限制成只检测 Wall 的 Layer

    [Header("Bar Blink")]
    public bool enableBarBlink = true;           // 是否启用闪烁效果
    public float blinkMaxDistance = 200f;        // 最远距离时的参考（保留供调参）
    public float blinkMinDistance = 20f;         // 最近距离时的参考（保留供调参）
    public float blinkAlphaMin = 0.3f;           // 闪烁时的最小透明度
    public float blinkAlphaMax = 1f;             // 闪烁时的最大透明度

    // 新增：真正用于控制闪烁节奏（秒），更小值 = 更快闪烁
    [Tooltip("近处最快闪烁周期（秒），值越小闪烁越快")]
    public float blinkFastPeriod = 0.12f;
    [Tooltip("远处最慢闪烁周期（秒），值越大闪烁越慢")]
    public float blinkSlowPeriod = 1.2f;
    [Tooltip("闪烁速度曲线（0-1），小于1会放大近处差异，大于1会压缩近处差异")]
    [Range(0.2f, 2f)]
    public float blinkCurve = 0.5f;

    [Header("Audio Settings")]
    [Tooltip("后方（背对方向）的低通滤波截止频率，越低越闷")]
    public float minCutoffFreq = 800f;
    [Tooltip("前方（面对方向）的低通滤波截止频率，越高越清晰")]
    public float maxCutoffFreq = 22000f;

    // --- 运行时数据 ---
    public ObstacleDetector[] detectors; // 现在在 Inspector 可见（可删或在运行时生成）

    // --- 障碍物数据结构 ---
    [System.Serializable]
    public class ObstacleDetector
    {
        public string directionName;
        public Vector3 worldDirection;          // 世界坐标方向 (用于 Raycast，XY 平面)
        public float rotationAngle;             // 在 UI 上的旋转角度 (Z轴)
        public RectTransform segmentBar;        // 运行时生成的引用，现在可在 Inspector 查看
        public Image segmentBarImage;           // bar 的 Image（用于变色）
        public TextMeshProUGUI distanceText;    // 运行时生成的引用，现在可在 Inspector 查看

        // 以下用于调试，在 Inspector 中实时显示检测结果
        public bool wallDetected = false;
        public float detectedDistance = 0f;
        public Vector3 lastHitPoint = Vector3.zero;

        // 闪烁相位追踪 (0.0 ~ 1.0) - 替代 blinkStartTime
        public float blinkPhase = 0f;
        
        // 闪烁状态控制
        public bool blinkVisible = true; // true 显示，false 隐藏

        // 新增：在 Inspector 中显示当前计算得到的 blink period（秒）
        public float blinkPeriod = 0f;

        // 新增：用于音效播放的 AudioSource
        public AudioSource audioSource;
        // 新增：低通滤波器组件
        public AudioLowPassFilter lowPassFilter;
        
        // 新增：追踪上一帧的闪烁状态，用于检测状态变化
        public bool lastBlinkVisible = true;
    }

    private float subYRotation;

    // 新增：用于缓存模式状态
    private bool is2DMode = false;

    void Start()
    {
        // 初始检测一次模式
        CheckGameMode();
        InitializeDetectors(); // 初始化 8 个方向数据
        GenerateUI();          // 自动生成 UI 对象
    }

    // 辅助函数：检测是否为 2D 模式
    private void CheckGameMode()
    {
        var sample = GameObject.FindWithTag("Wall");
        if (sample != null && sample.GetComponent<Collider2D>() != null)
            is2DMode = true;
        else
            is2DMode = false;
    }

    // 初始化 8 个方向的数据结构（以 2D XY 平面为基础）
    private void InitializeDetectors()
    {
        // N 的角度改为 180，其他方向对应调整
        detectors = new ObstacleDetector[]
        {
            new ObstacleDetector { directionName = "N",  rotationAngle = 180f,  worldDirection = new Vector3(0f, -1f, 0f) },
            new ObstacleDetector { directionName = "NE", rotationAngle = 225f,  worldDirection = new Vector3(0.7071f, -0.7071f, 0f) },
            new ObstacleDetector { directionName = "E",  rotationAngle = 270f,  worldDirection = new Vector3(1f, 0f, 0f) },
            new ObstacleDetector { directionName = "SE", rotationAngle = 315f,  worldDirection = new Vector3(0.7071f, 0.7071f, 0f) },
            new ObstacleDetector { directionName = "S",  rotationAngle = 0f,    worldDirection = new Vector3(0f, 1f, 0f) },
            new ObstacleDetector { directionName = "SW", rotationAngle = 45f,   worldDirection = new Vector3(-0.7071f, 0.7071f, 0f) },
            new ObstacleDetector { directionName = "W",  rotationAngle = 90f,   worldDirection = new Vector3(-1f, 0f, 0f) },
            new ObstacleDetector { directionName = "NW", rotationAngle = 135f,  worldDirection = new Vector3(-0.7071f, -0.7071f, 0f) }
        };
    }

    // 自动生成 Segment Bar 和 Distance Text
    private void GenerateUI()
    {
        if (!segmentBarTemplate || !distanceTextTemplate)
        {
            Debug.LogError("Segment Bar and Distance Text Templates must be assigned in the Inspector!");
            return;
        }

        // 清理已存在的子项
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }

        foreach (var detector in detectors)
        {
            // 生成 Bar（以中心为父）
            RectTransform barRT = Instantiate(segmentBarTemplate, this.transform);
            barRT.gameObject.SetActive(true);
            barRT.pivot = new Vector2(0.5f, 0.5f);
            barRT.anchorMin = barRT.anchorMax = new Vector2(0.5f, 0.5f);
            barRT.localScale = Vector3.one;
            barRT.sizeDelta = new Vector2(segmentBarWidth, segmentBarLength);
            barRT.localRotation = Quaternion.Euler(0f, 0f, detector.rotationAngle);
            Vector2 dir = Quaternion.Euler(0, 0, detector.rotationAngle) * Vector2.up;
            barRT.anchoredPosition = dir * barDistanceFromCenter;

            // 生成文本
            TextMeshProUGUI text = Instantiate(distanceTextTemplate, this.transform);
            text.gameObject.SetActive(true);
            RectTransform textRT = text.rectTransform;
            textRT.anchorMin = textRT.anchorMax = new Vector2(0.5f, 0.5f);
            textRT.localScale = Vector3.one;
            text.alignment = TextAlignmentOptions.Center;
            text.text = detector.directionName;
            textRT.anchoredPosition = dir * (textDistanceFromCenter + Mathf.Max(segmentBarLength * 0.2f, 0f));

            // 获取 Image 以便后续变色（可能为 null）
            Image barImg = barRT.GetComponent<Image>();

            // 获取 AudioSource 组件（应在 segmentBarTemplate 中已添加）
            AudioSource audioSource = barRT.GetComponent<AudioSource>();
            
            // --- 新增：获取 AudioLowPassFilter 组件 ---
            AudioLowPassFilter lpf = barRT.GetComponent<AudioLowPassFilter>();
            if (lpf == null && audioSource != null)
            {
                // 如果预制体没加，尝试自动添加（可选，建议在预制体里加好）
                lpf = barRT.gameObject.AddComponent<AudioLowPassFilter>();
            }

            detector.segmentBar = barRT;
            detector.segmentBarImage = barImg;
            detector.distanceText = text;
            detector.audioSource = audioSource;
            detector.lowPassFilter = lpf; // 赋值给 detector
            detector.lastBlinkVisible = true;
        }
    }

    void Update()
    {
        if (submarineTransform == null || detectors == null) return;

        // 每帧检测模式（如果场景动态生成墙体）或者只在 Start 检测
        // 为了稳健性，这里保留每帧检测或使用缓存
        CheckGameMode(); 

        float subZRotation = submarineTransform.eulerAngles.z;
        float subYRotation = submarineTransform.eulerAngles.y;

        this.transform.localRotation = Quaternion.Euler(0, 0, -subYRotation);

        if (centralIcon != null)
        {
            centralIcon.transform.rotation = submarineTransform.rotation;
        }

        UpdateObstacleData();
        UpdateBarBlink(); 
    }

    // 每帧更新闪烁效果
    private void UpdateBarBlink()
    {
        if (!enableBarBlink || detectors == null) return;

        // 临时检测模式 (为了音频计算准确)
        bool use2D = false;
        var sample = GameObject.FindWithTag("Wall");
        if (sample != null && sample.GetComponent<Collider2D>() != null) use2D = true;

        // 找到离墙最近的探测点
        ObstacleDetector closestDetector = null;
        float closestDistance = float.MaxValue;

        foreach (var detector in detectors)
        {
            if (detector.wallDetected && detector.detectedDistance < closestDistance)
            {
                closestDistance = detector.detectedDistance;
                closestDetector = detector;
            }
        }

        foreach (var detector in detectors)
        {
            if (detector.segmentBarImage == null) continue;

            if (detector.wallDetected)
            {
                // normalized: 0 = very close, 1 = at/near max range
                float normalized = Mathf.Clamp01(detector.detectedDistance / maxDetectionRange);

                // 使用曲线放大近处差异，然后在最快和最慢周期间插值
                float t = Mathf.Pow(normalized, blinkCurve);
                float blinkPeriod = Mathf.Lerp(blinkFastPeriod, blinkSlowPeriod, t);

                // 写回以便 Inspector 调试
                detector.blinkPeriod = blinkPeriod;

                // 使用相位累加
                detector.blinkPhase += Time.deltaTime / blinkPeriod;
                if (detector.blinkPhase >= 1f) detector.blinkPhase -= 1f;

                // 前半周期显示，后半周期隐藏
                detector.blinkVisible = detector.blinkPhase < 0.5f;

                float blinkAlpha = detector.blinkVisible ? blinkAlphaMax : blinkAlphaMin;

                Color barColor = detector.segmentBarImage.color;
                barColor.a = blinkAlpha;
                detector.segmentBarImage.color = barColor;

                // 只有离墙最近的探测点才能播放音效
                if (detector == closestDetector && detector.blinkVisible && !detector.lastBlinkVisible)
                {
                    if (detector.audioSource != null && detector.audioSource.clip != null)
                    {
                        // --- 核心修改：计算立体声声像与滤波 ---
                        
                        // 1. 构建真实的射线世界方向向量
                        Vector3 realWorldDir;
                        if (use2D)
                        {
                            // 2D: XY 平面
                            realWorldDir = new Vector3(detector.worldDirection.x, detector.worldDirection.y, 0f);
                        }
                        else
                        {
                            // 3D: XZ 平面 (worldDirection.y 映射到 Z)
                            realWorldDir = new Vector3(detector.worldDirection.x, 0f, detector.worldDirection.y);
                        }

                        // 2. 转换为潜艇的本地方向 (InverseTransformDirection 会自动处理潜艇的旋转)
                        // 结果向量：x=右, y=上(2D前), z=前(3D前)
                        Vector3 localDir = submarineTransform.InverseTransformDirection(realWorldDir);

                        // 3. 设置 Pan (左右声道)
                        // 无论 2D/3D，本地 x 轴通常代表左右
                        float pan = Mathf.Clamp(localDir.x, -1f, 1f);
                        detector.audioSource.panStereo = pan;

                        // 4. 设置 Low Pass Filter (前后闷声效果)
                        if (detector.lowPassFilter != null)
                        {
                            // 获取前后分量：2D用y，3D用z
                            float forwardFactor = use2D ? localDir.y : localDir.z;
                            
                            // forwardFactor: 1.0 (正前) -> -1.0 (正后)
                            // 映射到 0~1 用于插值
                            float tFreq = (forwardFactor + 1f) * 0.5f; 
                            
                            // 插值计算截止频率：后方闷(min)，前方清(max)
                            float cutoff = Mathf.Lerp(minCutoffFreq, maxCutoffFreq, tFreq);
                            detector.lowPassFilter.cutoffFrequency = cutoff;
                        }

                        detector.audioSource.PlayOneShot(detector.audioSource.clip);
                    }
                }

                detector.lastBlinkVisible = detector.blinkVisible;
            }
            else
            {
                // 未检测到时恢复原始透明度并重置 blinkPhase
                detector.blinkVisible = true;
                detector.blinkPhase = 0f; 
                Color barColor = detector.segmentBarImage.color;
                barColor.a = blinkAlphaMax;
                detector.segmentBarImage.color = barColor;

                detector.lastBlinkVisible = true;
            }
        }
    }

    // 检测逻辑
    void UpdateObstacleData()
    {
        if (detectors == null || detectors.Length == 0 || submarineTransform == null) return;

        // 使用缓存的 is2DMode，不再重复 FindWithTag
        bool use2D = is2DMode;

        Vector3 origin3 = submarineTransform.position;
        float raycastRange = maxDetectionRange + playerRadius;

        for (int i = 0; i < detectors.Length; i++)
        {
            var detector = detectors[i];
            bool wasDetected = detector.wallDetected;
            detector.wallDetected = false;
            detector.detectedDistance = 0f;
            detector.lastHitPoint = Vector3.zero;

            if (use2D)
            {
                // 2D 逻辑...
                Vector2 dir2 = new Vector2(detector.worldDirection.x, detector.worldDirection.y).normalized;
                Vector2 origin2 = new Vector2(origin3.x, origin3.y);

                if (debugDrawRays) Debug.DrawLine(origin3, origin3 + new Vector3(dir2.x, dir2.y, 0f) * raycastRange, Color.cyan);

                RaycastHit2D[] hits2D = Physics2D.RaycastAll(origin2, dir2, raycastRange);
                RaycastHit2D? wallHit = null;
                if (hits2D != null && hits2D.Length > 0)
                {
                    System.Array.Sort(hits2D, (a, b) => a.distance.CompareTo(b.distance));
                    foreach (var h in hits2D)
                    {
                        if (h.collider != null && h.collider.CompareTag("Wall"))
                        {
                            wallHit = h;
                            break;
                        }
                    }
                }

                if (wallHit.HasValue)
                {
                    var hit = wallHit.Value;
                    float distanceFromEdge = Mathf.Max(0f, hit.distance - playerRadius);
                    detector.wallDetected = true;
                    detector.detectedDistance = distanceFromEdge;
                    detector.lastHitPoint = new Vector3(hit.point.x, hit.point.y, origin3.z);

                    if (debugDrawRays) Debug.DrawLine(origin3, detector.lastHitPoint, Color.red);
                    detector.distanceText.text = $"{distanceFromEdge:F2} M";
                    detector.distanceText.color = (distanceFromEdge < 50f) ? Color.red : new Color(255f/255f, 176f/255f, 0f/255f);
                    if (detector.segmentBarImage != null) detector.segmentBarImage.color = (distanceFromEdge < 50f) ? Color.red : Color.white;
                }
                else
                {
                    detector.distanceText.text = "CLEAR";
                    detector.distanceText.color = new Color(255f/255f, 176f/255f, 0f/255f);
                    if (detector.segmentBarImage != null) detector.segmentBarImage.color = Color.white * 0.6f;
                    if (debugDrawRays) Debug.DrawLine(origin3, origin3 + new Vector3(dir2.x, dir2.y, 0f) * raycastRange, Color.green);
                }
            }
            else
            {
                // 3D 逻辑...
                Vector3 worldDir3 = new Vector3(detector.worldDirection.x, 0f, detector.worldDirection.y).normalized;

                if (debugDrawRays) Debug.DrawRay(origin3, worldDir3 * raycastRange, Color.cyan);

                RaycastHit[] hits3D = Physics.RaycastAll(origin3, worldDir3, raycastRange);
                RaycastHit? wallHit = null;
                if (hits3D != null && hits3D.Length > 0)
                {
                    System.Array.Sort(hits3D, (a, b) => a.distance.CompareTo(b.distance));
                    foreach (var h in hits3D)
                    {
                        if (h.collider != null && h.collider.CompareTag("Wall"))
                        {
                            wallHit = h;
                            break;
                        }
                    }
                }

                if (wallHit.HasValue)
                {
                    var hit = wallHit.Value;
                    float distanceFromEdge = Mathf.Max(0f, hit.distance - playerRadius);
                    detector.wallDetected = true;
                    detector.detectedDistance = distanceFromEdge;
                    detector.lastHitPoint = hit.point;

                    if (debugDrawRays) Debug.DrawRay(origin3, worldDir3 * hit.distance, Color.red);
                    detector.distanceText.text = $"{distanceFromEdge:F2} M";
                    detector.distanceText.color = (distanceFromEdge < 50f) ? Color.red : new Color(255f/255f, 176f/255f, 0f/255f);
                    if (detector.segmentBarImage != null) detector.segmentBarImage.color = (distanceFromEdge < 50f) ? Color.red : Color.white;
                }
                else
                {
                    detector.distanceText.text = "CLEAR";
                    detector.distanceText.color = new Color(255f/255f, 176f/255f, 0f/255f);
                    if (detector.segmentBarImage != null) detector.segmentBarImage.color = Color.white * 0.6f;
                    if (debugDrawRays) Debug.DrawRay(origin3, worldDir3 * raycastRange, Color.green);
                }
            }

            if (detector.wallDetected && !wasDetected)
            {
                detector.blinkPhase = 0f;
                detector.blinkVisible = true;
                detector.lastBlinkVisible = true;
            }
        }
    }
}