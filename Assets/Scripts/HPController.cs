using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HPController : MonoBehaviour
{
    [Header("References")]
    public PlayerController playerController; // 引用玩家控制器以获取血量
    public List<Image> hpIcons;               // 直接在 Inspector 中拖入 5 个 Image
    public List<AudioSource> hpAudioSources;  // 对应每个血量图标的音效源 (Audio Pool)

    [Header("Settings")]
    public float blinkDuration = 0.2f;        // 每次闪烁持续时间
    public int blinkCount = 2;                // 闪烁次数

    private int lastHealth = -1;

    void Start()
    {
        if (playerController == null)
        {
            // 尝试自动查找
            playerController = FindObjectOfType<PlayerController>();
        }

        if (playerController != null)
        {
            // 初始化：确保所有图标状态正确
            lastHealth = playerController.currentHealth;
            RefreshIconsImmediate();
        }
        else
        {
            Debug.LogError("HPController: 未找到 PlayerController！");
        }
    }

    void Update()
    {
        if (playerController == null) return;

        // 只有当血量发生变化时才更新 UI
        if (playerController.currentHealth != lastHealth)
        {
            HandleHealthChange(lastHealth, playerController.currentHealth);
            lastHealth = playerController.currentHealth;
        }
    }

    // 立即刷新所有图标（不闪烁），用于初始化
    private void RefreshIconsImmediate()
    {
        int currentHP = playerController.currentHealth;
        for (int i = 0; i < hpIcons.Count; i++)
        {
            // 确保 GameObject 是激活的（为了 AudioSource 能播放），只控制 Image 组件的开关
            if (hpIcons[i] != null)
            {
                hpIcons[i].gameObject.SetActive(true);
                hpIcons[i].enabled = (i < currentHP);
            }
        }
    }

    // 处理血量变化
    private void HandleHealthChange(int oldVal, int newVal)
    {
        // 如果是扣血
        if (newVal < oldVal)
        {
            // 找出所有需要关闭的图标索引 (从 newVal 到 oldVal-1)
            // 例如：从 5 掉到 3，需要处理索引 3 和 4
            for (int i = newVal; i < oldVal; i++)
            {
                if (i >= 0 && i < hpIcons.Count)
                {
                    StartCoroutine(BlinkAndHide(i));
                }
            }
        }
        // 如果是加血
        else
        {
            // 直接显示增加的图标
            for (int i = oldVal; i < newVal; i++)
            {
                if (i >= 0 && i < hpIcons.Count)
                {
                    hpIcons[i].gameObject.SetActive(true);
                    hpIcons[i].enabled = true;
                }
            }
        }
    }

    // 闪烁协程
    IEnumerator BlinkAndHide(int index)
    {
        Image targetImage = hpIcons[index];

        // 确保开始时是显示的
        targetImage.gameObject.SetActive(true);
        targetImage.enabled = true;

        for (int i = 0; i < blinkCount; i++)
        {
            // 隐藏
            targetImage.enabled = false;
            yield return new WaitForSeconds(blinkDuration);
            
            // 显示
            targetImage.enabled = true;
            yield return new WaitForSeconds(blinkDuration);
        }

        // 最后只关闭 Image 组件，保持 GameObject 激活以便播放音效
        targetImage.enabled = false;

        // 播放对应的音效
        if (hpAudioSources != null && index < hpAudioSources.Count && hpAudioSources[index] != null)
        {
            hpAudioSources[index].Play();
        }
    }
}
