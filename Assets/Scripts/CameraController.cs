using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class CameraController : MonoBehaviour
{
    [Header("UI References")]
    public Image targetImage;           // 用于显示照片的 UI Image
    public GameObject photoUIContainer; // 可选：包含 Image 的父物体，用于控制显隐
    public PrinterController printerController; // 打印机控制器引用
    public AudioSource shutterAudioSource; // 快门音效源 (请拖入 Player 子对象的 AudioSource)
    [Header("Trigger Audio")]
    public AudioSource triggerAudioSource;
    public AudioClip triggerEnterClip;

    [Header("Photo Settings")]
    public List<Sprite> photoPool;      // 可指定大小的图片列表
    public List<Sprite> defaultPhotoPool; // 默认图片池（不在区域内时随机显示）
    public Sprite defaultSprite;        // 默认图片（可在 Inspector 指定）
    public float autoRevertTime = 10f;  // 自动关闭照片的时间

    [Header("End Sequence")]
    [Tooltip("需要绑定照片的 Trigger 数量，达到后开始倒计时")] 
    public int requiredTriggerCount = 5;
    [Tooltip("完成全部照片后等待的秒数")] 
    public float allPhotosDelay = 5f;
    [Tooltip("完成后加载的场景名")] 
    public string endSceneName = "END";

    // 内部状态
    private Sprite originalSprite;      // Image 组件原本的图片
    private bool isShowingPhoto = false;
    private bool isTakingPhoto = false; // 是否正在进行拍照流程（播放音效中）
    private float timer = 0f;
    private Coroutine endSequenceCoroutine;
    private bool hasTriggeredEnd = false;
    private HashSet<Collider2D> visitedTriggers = new HashSet<Collider2D>();

    // 触发器逻辑
    private Collider2D currentTrigger;  // 当前所在的 Trigger
    // 字典：记录每个 Trigger (Key) 绑定的照片 (Value)
    private Dictionary<Collider2D, Sprite> triggerPhotoMap = new Dictionary<Collider2D, Sprite>();

    void Start()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        if (targetImage != null)
        {
            originalSprite = targetImage.sprite; // 记录初始图片
        }
    }

    void Update()
    {
        // 检测 G 键按下
        if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
        {
            HandleInput();
        }

        // 倒计时自动关闭
        if (isShowingPhoto)
        {
            timer += Time.deltaTime;
            if (timer >= autoRevertTime)
            {
                ClosePhoto();
            }
        }
    }

    private void HandleInput()
    {
        // 如果正在拍照过程中（播放音效），忽略输入
        if (isTakingPhoto) return;

        // 如果照片正在显示，再次按下 G 键则关闭
        if (isShowingPhoto)
        {
            ClosePhoto();
            return;
        }

        // 开始拍照流程
        StartCoroutine(CaptureRoutine());
    }

    private IEnumerator CaptureRoutine()
    {
        isTakingPhoto = true;

        // 1. 播放音效并等待
        if (shutterAudioSource != null)
        {
            shutterAudioSource.Play();
            if (shutterAudioSource.clip != null)
            {
                yield return new WaitForSeconds(shutterAudioSource.clip.length);
            }
        }

        // 2. 执行拍照逻辑
        if (currentTrigger != null)
        {
            TakePhoto();
        }
        else
        {
            // 不在拍照区域内，尝试从默认图片池随机选取，或显示单一默认图片
            Sprite photoToUse = null;

            if (defaultPhotoPool != null && defaultPhotoPool.Count > 0)
            {
                int randomIndex = Random.Range(0, defaultPhotoPool.Count);
                photoToUse = defaultPhotoPool[randomIndex];
            }
            else if (defaultSprite != null)
            {
                photoToUse = defaultSprite;
            }

            if (photoToUse != null)
            {
                ShowPhoto(photoToUse);
                Debug.Log("不在拍照区域内，显示随机/默认图片");
            }
            else
            {
                Debug.Log("不在拍照区域内，且无默认图片资源");
            }
        }

        isTakingPhoto = false;
    }

    private void TakePhoto()
    {
        if (targetImage == null || photoPool == null || photoPool.Count == 0) return;

        Sprite photoToShow = null;

        // 1. 检查该 Trigger 是否已经绑定过照片
        if (triggerPhotoMap.ContainsKey(currentTrigger))
        {
            photoToShow = triggerPhotoMap[currentTrigger];
        }
        else
        {
            // 2. 如果没绑定，从剩余可用的图片中随机选一张
            List<Sprite> availablePhotos = new List<Sprite>();
            foreach (var sprite in photoPool)
            {
                // 检查该图片是否已被其他 Trigger 使用
                if (!triggerPhotoMap.ContainsValue(sprite))
                {
                    availablePhotos.Add(sprite);
                }
            }

            if (availablePhotos.Count == 0)
            {
                Debug.LogError("没有剩余的可用图片！(No available photos left in pool)");
                return;
            }

            int randomIndex = Random.Range(0, availablePhotos.Count);
            photoToShow = availablePhotos[randomIndex];
            triggerPhotoMap.Add(currentTrigger, photoToShow);
            Debug.Log($"Trigger {currentTrigger.name} 绑定了图片: {photoToShow.name}");
        }

        // 3. 显示照片
        TryStartEndSequence();
        ShowPhoto(photoToShow);
    }

    private void ShowPhoto(Sprite sprite)
    {
        targetImage.sprite = sprite;
        targetImage.gameObject.SetActive(true); // 确保图片可见
        if (photoUIContainer != null) photoUIContainer.SetActive(true);
        
        isShowingPhoto = true;
        timer = 0f;

        // 尝试打印
        if (printerController != null && sprite != null)
        {
            // 将 Sprite 转换为 Texture2D
            // 注意：Sprite 的 texture 属性可能包含整个图集，需要裁剪
            // 这里简单假设 Sprite 占用整个 Texture，或者您需要实现裁剪逻辑
            // 为了安全起见，我们尝试获取 sprite.texture
            // 如果需要精确裁剪，需要创建一个新的 Texture2D 并 SetPixels
            
            Texture2D texToPrint = GetTextureFromSprite(sprite);
            if (texToPrint != null)
            {
                printerController.PrintPhoto(texToPrint);
                
                // 如果创建了新的 Texture2D (裁剪版)，用完后销毁以防内存泄漏
                if (texToPrint != sprite.texture)
                {
                    Destroy(texToPrint);
                }
            }
        }
    }

    // 辅助方法：从 Sprite 提取 Texture2D
    private Texture2D GetTextureFromSprite(Sprite sprite)
    {
        if (sprite.rect.width != sprite.texture.width)
        {
            Texture2D newText = new Texture2D((int)sprite.rect.width, (int)sprite.rect.height);
            Color[] newColors = sprite.texture.GetPixels((int)sprite.textureRect.x, 
                                                         (int)sprite.textureRect.y, 
                                                         (int)sprite.textureRect.width, 
                                                         (int)sprite.textureRect.height);
            newText.SetPixels(newColors);
            newText.Apply();
            return newText;
        }
        else
            return sprite.texture;
    }

    private void TryStartEndSequence()
    {
        if (hasTriggeredEnd) return;

        if (triggerPhotoMap.Count >= requiredTriggerCount)
        {
            if (endSequenceCoroutine == null)
            {
                endSequenceCoroutine = StartCoroutine(AllPhotosCountdown());
            }
        }
        else if (endSequenceCoroutine != null)
        {
            StopCoroutine(endSequenceCoroutine);
            endSequenceCoroutine = null;
        }
    }

    private IEnumerator AllPhotosCountdown()
    {
        float duration = Mathf.Max(0f, allPhotosDelay);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (triggerPhotoMap.Count < requiredTriggerCount)
            {
                endSequenceCoroutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        hasTriggeredEnd = true;
        SceneManager.LoadScene(endSceneName);
    }

    private void ClosePhoto()
    {
        // 恢复原有图片
        if (targetImage != null)
        {
            targetImage.sprite = originalSprite;
        }
        
        // 如果希望关闭时完全隐藏 Image：
        // targetImage.gameObject.SetActive(false);
        // if (photoUIContainer != null) photoUIContainer.SetActive(false);

        isShowingPhoto = false;
        timer = 0f;
    }

    // --- 触发器检测 ---

    // 玩家进入 Trigger
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("PhotoTrigger"))
        {
            currentTrigger = other;
            if (!visitedTriggers.Contains(other))
            {
                visitedTriggers.Add(other);
                Debug.Log("进入拍照区域");
                if (triggerAudioSource != null && triggerEnterClip != null)
                {
                    triggerAudioSource.PlayOneShot(triggerEnterClip);
                }
            }
        }
    }

    // 玩家离开 Trigger
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("PhotoTrigger") && currentTrigger == other)
        {
            currentTrigger = null;
            Debug.Log("离开拍照区域");
            
            // 如果离开区域时照片还开着，是否要自动关闭？
            // ClosePhoto(); 
        }
    }
}
