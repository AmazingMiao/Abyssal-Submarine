using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class EscPosUtil
{
    private List<byte> _buffer = new List<byte>();
    private Encoding _encoding;

    public EscPosUtil()
    {
        // 注意：打印中文通常需要 GBK 编码
        // 如果在 Unity 中报错 "Encoding name 'gbk' not supported"，请看下文“常见问题”
        try {
            _encoding = Encoding.GetEncoding("gbk");
        } catch {
            _encoding = Encoding.ASCII; // 降级处理，只能打英文
        }
        
        // 初始化打印机
        _buffer.Add(0x1B); _buffer.Add(0x40);
    }

    // 添加文字并换行
    public void PrintLine(string text)
    {
        byte[] b = _encoding.GetBytes(text + "\n");
        _buffer.AddRange(b);
    }

    // 走纸（空几行）
    public void FeedLines(int lines)
    {
        _buffer.Add(0x1B); 
        _buffer.Add(0x64); 
        _buffer.Add((byte)lines);
    }

    // 设置加粗
    public void SetBold(bool bold)
    {
        _buffer.Add(0x1B);
        _buffer.Add(0x45);
        _buffer.Add((byte)(bold ? 1 : 0));
    }

    // 设置对齐 (0:左, 1:中, 2:右)
    public void SetAlign(int align)
    {
        _buffer.Add(0x1B);
        _buffer.Add(0x61);
        _buffer.Add((byte)align);
    }

    // 设置反白 (黑底白字)
    public void SetInverse(bool inverse)
    {
        _buffer.Add(0x1D);
        _buffer.Add(0x42);
        _buffer.Add((byte)(inverse ? 1 : 0));
    }

    // 切纸
    public void CutPaper()
    {
        _buffer.Add(0x1D); _buffer.Add(0x56); _buffer.Add(0x42); _buffer.Add(0x00);
    }

    // 打印图片 (Raster Bit Image 模式)
    // 注意：Texture2D 必须在 Import Settings 中开启 "Read/Write Enabled"
    // maxWidth: 打印机最大宽度（像素），58mm通常384，80mm通常576
    public void PrintImage(Texture2D texture, int maxWidth = 384)
    {
        if (texture == null) return;

        // 1. 处理缩放
        Texture2D textureToPrint = texture;
        bool needDestroy = false;

        if (texture.width > maxWidth)
        {
            float scale = (float)maxWidth / texture.width;
            int newWidth = maxWidth;
            int newHeight = Mathf.RoundToInt(texture.height * scale);
            
            // 创建缩放后的临时 Texture
            textureToPrint = ResizeTexture(texture, newWidth, newHeight);
            needDestroy = true;
        }

        int width = textureToPrint.width;
        int height = textureToPrint.height;
        
        // 宽度字节数 (xL + xH * 256)
        int widthBytes = (width + 7) / 8; 

        // GS v 0 m xL xH yL yH d1...dk
        _buffer.Add(0x1D);
        _buffer.Add(0x76);
        _buffer.Add(0x30);
        _buffer.Add(0x00); // m=0 (Normal)

        // xL, xH
        _buffer.Add((byte)(widthBytes % 256));
        _buffer.Add((byte)(widthBytes / 256));

        // yL, yH
        _buffer.Add((byte)(height % 256));
        _buffer.Add((byte)(height / 256));

        // Data
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < widthBytes; x++)
            {
                byte b = 0;
                for (int bit = 0; bit < 8; bit++)
                {
                    int pixelX = x * 8 + bit;
                    if (pixelX < width)
                    {
                        // 打印机是从上到下打印，Texture2D (0,0) 在左下角
                        // 所以 y 轴需要反转：height - 1 - y
                        Color c = textureToPrint.GetPixel(pixelX, height - 1 - y);
                        
                        // 简单的二值化：亮度 < 0.5 视为黑色(打印)
                        // 打印机逻辑：1=黑点，0=白点
                        if (c.grayscale < 0.5f)
                        {
                            b |= (byte)(1 << (7 - bit));
                        }
                    }
                }
                _buffer.Add(b);
            }
        }

        // 清理临时纹理
        if (needDestroy)
        {
            Object.DestroyImmediate(textureToPrint);
        }
    }

    // 简单的双线性插值缩放
    private Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight)
    {
        // 使用 RGBA32 格式以确保支持 SetPixel，避免压缩格式导致的错误
        Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
        float incX = (1.0f / (float)targetWidth);
        float incY = (1.0f / (float)targetHeight);
        
        for (int i = 0; i < result.height; ++i)
        {
            for (int j = 0; j < result.width; ++j)
            {
                Color newColor = source.GetPixelBilinear((float)j / (float)result.width, (float)i / (float)result.height);
                result.SetPixel(j, i, newColor);
            }
        }
        result.Apply();
        return result;
    }

    // 获取最终的字节数组
    public byte[] GetBytes()
    {
        return _buffer.ToArray();
    }
}