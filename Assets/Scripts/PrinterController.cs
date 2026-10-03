using UnityEngine;

public class PrinterController : MonoBehaviour
{
    [Header("打印机设置")]
    [Tooltip("必须与 Windows 控制面板中的打印机名称完全一致")]
    public string printerName = "XP-80C"; 
    [Tooltip("打印机最大宽度（像素），58mm通常384，80mm通常576")]
    public int maxPrinterWidth = 384;

    [Header("测试内容")]
    public string title = "测试小票";
    [TextArea(3, 10)]
    public string content = "Hello Unity USB Print!";
    public Texture2D testImage; // 拖入要打印的图片 (需开启 Read/Write)
    
    [Header("日期设置")]
    [Tooltip("是否使用自定义日期字符串")]
    public bool useCustomDate = false;
    [Tooltip("自定义日期内容")]
    public string customDate = "2025-01-01";
    [Tooltip("自动获取当前时间的格式，例如: yyyy-MM-dd HH:mm:ss")]
    public string dateFormat = "yyyy-MM-dd HH:mm:ss";

    // 这个方法绑定到 UI 按钮上
    public void DoPrint()
    {
        if (string.IsNullOrEmpty(printerName))
        {
            Debug.LogError("请设置打印机名称！");
            return;
        }

        // 1. 构建打印数据
        EscPosUtil builder = new EscPosUtil();
        
        builder.PrintLine("--------------------------------");
        builder.PrintLine(title);
        builder.PrintLine("--------------------------------");
        
        // 解析并打印内容
        string[] lines = content.Split('\n');
        foreach (string line in lines)
        {
            string textToPrint = line.Trim(); // 去除首尾空白符（包括 \r）

            // 检查特殊指令
            if (textToPrint.Contains("[Feed lines & Cut]"))
            {
                builder.FeedLines(4);
                builder.CutPaper();
                continue; // 跳过打印这行文字
            }

            bool isBold = false;
            bool isCenter = false;
            bool isInverse = false;

            // 检查样式标签
            if (textToPrint.Contains("[BOLD]"))
            {
                isBold = true;
                textToPrint = textToPrint.Replace("[BOLD]", "");
            }
            if (textToPrint.Contains("[CENTER]"))
            {
                isCenter = true;
                textToPrint = textToPrint.Replace("[CENTER]", "");
            }
            if (textToPrint.Contains("[INVERSE]"))
            {
                isInverse = true;
                textToPrint = textToPrint.Replace("[INVERSE]", "");
            }

            // 应用样式
            if (isBold) builder.SetBold(true);
            if (isCenter) builder.SetAlign(1);
            if (isInverse) builder.SetInverse(true);

            // 打印处理后的文本
            builder.PrintLine(textToPrint);

            // 恢复默认样式
            if (isBold) builder.SetBold(false);
            if (isCenter) builder.SetAlign(0); // 恢复左对齐
            if (isInverse) builder.SetInverse(false);
        }
        
        // 如果有图片，打印图片
        if (testImage != null)
        {
            builder.FeedLines(1);
            builder.PrintImage(testImage, maxPrinterWidth);
            builder.FeedLines(1);
        }

        string dateStr = useCustomDate ? customDate : System.DateTime.Now.ToString(dateFormat);
        builder.PrintLine("Time: " + dateStr);
        
        builder.FeedLines(4); // 走纸以便撕下来
        builder.CutPaper();   // 切纸指令

        byte[] data = builder.GetBytes();

        // 2. 发送给 Windows 驱动
        Debug.Log($"正在发送 {data.Length} 字节到打印机: {printerName}...");
        
        bool result = RawPrinterHelper.SendBytesToPrinter(printerName, data);

        if (result)
        {
            Debug.Log("✅ 打印指令发送成功！");
        }
        else
        {
            Debug.LogError("❌ 发送失败。请检查：\n1. 打印机是否已开机并连接 USB。\n2. printerName 是否与控制面板中的名称完全一致（包括空格）。");
        }
    }

    // 专门用于打印照片的方法
    public void PrintPhoto(Texture2D photo)
    {
        if (string.IsNullOrEmpty(printerName))
        {
            Debug.LogError("请设置打印机名称！");
            return;
        }

        EscPosUtil builder = new EscPosUtil();

        if (photo != null)
        {
            builder.PrintImage(photo, maxPrinterWidth);
            builder.FeedLines(1);
        }

        string dateStr = useCustomDate ? customDate : System.DateTime.Now.ToString(dateFormat);
        builder.PrintLine("Captured: " + dateStr);
        
        builder.FeedLines(4);
        builder.CutPaper();

        byte[] data = builder.GetBytes();
        RawPrinterHelper.SendBytesToPrinter(printerName, data);
    }
}