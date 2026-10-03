using UnityEngine;
using System.IO.Ports;
using System;
using System.Threading;

public class ESP32InputController : MonoBehaviour
{
    [Header("Serial Settings")]
    public string portName = "COM5"; // Windows通常是COMx，Mac是/dev/tty...
    public int baudRate = 115200;

    private SerialPort stream;
    private Thread readThread;
    private bool isRunning = false;

    // 公共变量，供其他脚本读取
    [Header("Debug Values")]
    public Vector2 JoystickInput;
    public bool ButtonPressed;
    public bool HasReceivedData { get; private set; } = false;

    // 线程安全的数据缓存
    private string lastReceivedData = "";
    private object dataLock = new object();

    void Start()
    {
        OpenConnection();
    }

    void OpenConnection()
    {
        try
        {
            stream = new SerialPort(portName, baudRate);
            stream.ReadTimeout = 50;
            stream.Open();
            
            isRunning = true;
            // 开启一个子线程读取串口，避免卡死 Unity 主线程
            readThread = new Thread(ReadSerialLoop);
            readThread.Start();
            Debug.Log("串口已打开: " + portName);
        }
        catch (Exception e)
        {
            Debug.LogError("无法打开串口: " + e.Message);
        }
    }

    void ReadSerialLoop()
    {
        while (isRunning && stream != null && stream.IsOpen)
        {
            try
            {
                string data = stream.ReadLine(); // 读取一行
                if (!string.IsNullOrEmpty(data))
                {
                    lock (dataLock)
                    {
                        lastReceivedData = data;
                    }
                }
            }
            catch (TimeoutException) { } // 超时很正常，忽略
            catch (Exception e)
            {
                Debug.LogWarning("读取错误: " + e.Message);
            }
        }
    }

    void Update()
    {
        // 在主线程解析数据
        string dataToParse = "";
        lock (dataLock)
        {
            dataToParse = lastReceivedData;
        }

        if (!string.IsNullOrEmpty(dataToParse))
        {
            ParseData(dataToParse);
        }
    }

    void ParseData(string data)
    {
        try
        {
            string[] parts = data.Split(',');
            if (parts.Length == 3)
            {
                float x = float.Parse(parts[0]);
                float y = float.Parse(parts[1]);
                int btn = int.Parse(parts[2]);

                // 更新公共变量
                JoystickInput = new Vector2(x, y);
                ButtonPressed = (btn == 1);
                HasReceivedData = true;
            }
        }
        catch
        {
            // 解析失败忽略
        }
    }

    void OnDestroy()
    {
        isRunning = false;
        if (readThread != null && readThread.IsAlive)
            readThread.Join();

        if (stream != null && stream.IsOpen)
            stream.Close();
    }
}