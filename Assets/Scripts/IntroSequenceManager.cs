using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // 引入 InputSystem

public class IntroSequenceManager : MonoBehaviour
{
    [Header("UI 组件")]
    public TextMeshProUGUI terminalText; // 拖入你的 TextMeshPro 对象
    public UnityEngine.UI.ScrollRect scrollRect; // 可选：如果有 ScrollRect，优先使用它

    [Header("打字设置")]
    [Tooltip("每个字符打字的间隔时间（秒）")]
    public float typingSpeed = 0.05f; 
    [Tooltip("每行文字显示完后的停留时间")]
    public float linePause = 2.0f;
    [Tooltip("两段文字之间的黑屏间隔")]
    public float clearScreenPause = 0.5f;

    [Header("音频设置")]
    public AudioSource audioSource;
    public AudioClip[] typingSounds; // 键盘敲击声池
    [Tooltip("使用 [SFX=index] 播放此列表中的音效")]
    public System.Collections.Generic.List<AudioClip> specialSounds; // 特殊音效列表
    [Range(0f, 0.5f)]
    public float pitchRandomness = 0.1f; // 音调随机化，让声音听起来不那么机械

    [Header("内容与跳转")]
    public bool playOnStart = true; // 是否在 Start 时自动播放
    [Tooltip("开始播放前的等待时间")]
    public float startDelay = 0f;
    [Tooltip("播放结束后的等待时间（在执行跳转或下一个序列之前）")]
    public float finishDelay = 0f;
    [Tooltip("播放结束后是否隐藏此对象")]
    public bool hideOnFinish = false;
    [Tooltip("是否等待玩家输入（G键或Arduino按钮）才结束")]
    public bool waitForInputToFinish = false;
    [Tooltip("等待输入时的提示文本（可选）")]
    public string waitForInputPrompt = "\n[PRESS BUTTON TO CONTINUE]";

    public enum FinishAction { LoadScene, PlayNextSequence, None }
    public FinishAction onFinish = FinishAction.LoadScene;
    
    [Tooltip("当 onFinish 为 LoadScene 时使用")]
    public string nextSceneName = "GameScene";
    [Tooltip("当 onFinish 为 PlayNextSequence 时使用")]
    public IntroSequenceManager nextSequence;

    public string cursorChar = "_";
    [TextArea(3, 10)]
    public string[] storyLines;

    [Header("Arduino 输入引用 (可选)")]
    public ESP32InputController esp32Controller;

    [Header("打印机引用 (可选)")]
    public PrinterController printerController;

    private Vector2 initialPos; // 记录初始位置用于手动滚动

    private void Start()
    {
        terminalText.text = ""; // 确保开始是空的
        if (terminalText != null)
        {
            initialPos = terminalText.rectTransform.anchoredPosition;
        }

        if (playOnStart)
        {
            Play();
        }
        else
        {
            // 如果不自动播放，可以选择隐藏整个对象，或者只是清空文本
            // 这里保持清空文本的状态
        }
    }

    // 公开方法，供外部调用开始播放
    public void Play()
    {
        gameObject.SetActive(true); // 确保自己是激活的
        StopAllCoroutines();
        StartCoroutine(PlayTerminalSequence());
    }

    IEnumerator PlayTerminalSequence()
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        foreach (string line in storyLines)
        {
            // 获取当前已有的文本，如果非空则换行
            string baseText = terminalText.text;
            if (!string.IsNullOrEmpty(baseText))
            {
                baseText += "\n";
            }
            
            // 2. 逐字打印 (打字机效果)
            yield return StartCoroutine(TypeWriterEffect(baseText, line));

            // 3. 停留阅读
            yield return new WaitForSeconds(linePause);
        }

        // 等待玩家输入
        if (waitForInputToFinish)
        {
            // 显示提示文本
            if (!string.IsNullOrEmpty(waitForInputPrompt))
            {
                terminalText.text += waitForInputPrompt;
                CheckScroll();
            }

            // 等待输入
            bool inputReceived = false;
            while (!inputReceived)
            {
                // 检测键盘 G 键
                if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
                {
                    inputReceived = true;
                }

                // 检测 Arduino 按钮
                if (esp32Controller != null && esp32Controller.ButtonPressed)
                {
                    inputReceived = true;
                }

                yield return null;
            }
        }

        if (finishDelay > 0f) yield return new WaitForSeconds(finishDelay);

        // 播放完毕，根据设置执行后续操作
        switch (onFinish)
        {
            case FinishAction.LoadScene:
                if (!string.IsNullOrEmpty(nextSceneName))
                {
                    SceneManager.LoadScene(nextSceneName);
                }
                break;
            case FinishAction.PlayNextSequence:
                if (nextSequence != null)
                {
                    nextSequence.Play();
                }
                break;
            case FinishAction.None:
            default:
                break;
        }

        if (hideOnFinish) gameObject.SetActive(false);
    }

    IEnumerator TypeWriterEffect(string baseText, string line)
    {
        string currentText = "";
        int i = 0;
        while (i < line.Length)
        {
            // --- 标签解析 ---
            if (line[i] == '[')
            {
                int closeIndex = line.IndexOf(']', i);
                if (closeIndex > -1)
                {
                    string tagContent = line.Substring(i + 1, closeIndex - i - 1);
                    bool isTag = false;

                    // [WAIT=seconds] : 等待指定时间
                    if (tagContent.StartsWith("WAIT="))
                    {
                        if (float.TryParse(tagContent.Substring(5), out float duration))
                        {
                            yield return new WaitForSeconds(duration);
                            isTag = true;
                        }
                    }
                    // [SFX=index] : 播放特殊音效
                    else if (tagContent.StartsWith("SFX="))
                    {
                        if (int.TryParse(tagContent.Substring(4), out int index))
                        {
                            if (specialSounds != null && index >= 0 && index < specialSounds.Count)
                            {
                                audioSource.PlayOneShot(specialSounds[index]);
                            }
                            isTag = true;
                        }
                    }
                    // [NUM=start,end,duration] : 数字滚动效果
                    else if (tagContent.StartsWith("NUM="))
                    {
                        string[] parts = tagContent.Substring(4).Split(',');
                        if (parts.Length == 3 && 
                            int.TryParse(parts[0], out int start) &&
                            int.TryParse(parts[1], out int end) &&
                            float.TryParse(parts[2], out float duration))
                        {
                            float timer = 0;
                            while (timer < duration)
                            {
                                timer += Time.deltaTime;
                                float t = Mathf.Clamp01(timer / duration);
                                int currentVal = (int)Mathf.Lerp(start, end, t);
                                
                                terminalText.text = baseText + currentText + currentVal + cursorChar;
                                CheckScroll();
                                yield return null;
                            }
                            currentText += end.ToString();
                            isTag = true;
                        }
                    }
                    // [PRINT] : 调用打印机打印
                    else if (tagContent == "PRINT")
                    {
                        if (printerController != null)
                        {
                            printerController.DoPrint();
                        }
                        else
                        {
                            Debug.LogWarning("IntroSequenceManager: 尝试打印但未绑定 PrinterController");
                        }
                        isTag = true;
                    }

                    if (isTag)
                    {
                        i = closeIndex + 1;
                        continue;
                    }
                }
            }
            // ----------------

            char letter = line[i];
            currentText += letter;
            
            // 显示文本 + 光标
            terminalText.text = baseText + currentText + cursorChar;

            // 检查并执行滚动
            CheckScroll();

            // 播放打字音效 (空格和换行通常不播)
            if (!char.IsWhiteSpace(letter) && audioSource != null && typingSounds != null && typingSounds.Length > 0)
            {
                audioSource.pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
                // 随机选择一个音效
                AudioClip clipToPlay = typingSounds[Random.Range(0, typingSounds.Length)];
                if (clipToPlay != null)
                {
                    audioSource.PlayOneShot(clipToPlay);
                }
            }

            // 如果遇到换行符，等待 linePause 时间，否则等待 typingSpeed
            if (letter == '\n')
            {
                yield return new WaitForSeconds(linePause);
            }
            else
            {
                yield return new WaitForSeconds(typingSpeed);
            }

            i++;
        }
        
        // 打字结束后，保持文字显示（去掉光标，或者让光标在等待时继续闪烁）
        terminalText.text = baseText + currentText; 
    }

    // 允许玩家点击跳过当前行的打字过程，直接显示全句
    void Update()
    {
        if (Input.GetMouseButtonDown(0) || Input.anyKeyDown)
        {
            // 这里可以添加加速逻辑，或者直接跳过整个开场
            // 为了简单演示，这里不做复杂处理，你可以根据需求添加
        }
    }

    private void CheckScroll()
    {
        if (terminalText == null) return;

        // 1. 如果有 ScrollRect，使用标准滚动
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
            return;
        }

        // 2. 如果没有 ScrollRect，尝试手动移动 RectTransform
        terminalText.ForceMeshUpdate();
        float textHeight = terminalText.preferredHeight;
        float boxHeight = terminalText.rectTransform.rect.height;

        if (textHeight > boxHeight)
        {
            // 计算溢出高度，将文本框向上移动，使底部内容可见
            float offset = textHeight - boxHeight;
            terminalText.rectTransform.anchoredPosition = initialPos + new Vector2(0, offset);
        }
    }
}