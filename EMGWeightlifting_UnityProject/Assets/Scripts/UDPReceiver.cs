using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

// Receives the EMG activation from the BioRadio tkinter app (bioradio_unity_bridge.py),
// fake_sender.py or replay_sender.py. Each UDP message is one JSON object, for example:
// {"channel": 1, "raw_mv": -2.9, "rms_mv": 2.6, "sample_count": 25762, "sample_rate_hz": 1000.0,
// "sender_time": 1793997405.432, "envelope_uv": 411.1, "activation": 0.4200, "filter": "B"}
public class UDPReceiver : MonoBehaviour
{
    // Safe unblocked UDP port (> 8080) for Windows localhost communication
    public const int Port = 9050;

    public float Activation { get; private set; }
    public string FilterTag { get; private set; } = "NA";
    public double LastSenderTime { get; private set; }
    public float EnvelopeUv { get; private set; }
    public float TimeSinceLastPacket => Time.realtimeSinceStartup - lastPacketTime;

    [Serializable]
    class Packet
    {
        public int channel = 1;
        public double sender_time;
        public float activation = -1f; // stays -1 if the message has no activation
        public float envelope_uv;
        public string filter = "NA";
    }

    UdpClient client;
    Thread listenThread;
    volatile bool running;
    readonly object lockObj = new object();

    string latestMessage;
    bool hasNewMessage;
    float lastPacketTime = -999f;

    void Awake()
    {
        // Keep receiving packets even when Unity window is not focused!
        Application.runInBackground = true;
    }

    void Start()
    {
        try
        {
            client = new UdpClient();
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            try
            {
                client.Client.Bind(new IPEndPoint(IPAddress.Any, Port));
            }
            catch (Exception)
            {
                // Fallback to loopback if 0.0.0.0 is restricted
                client.Client.Bind(new IPEndPoint(IPAddress.Loopback, Port));
            }
            running = true;
            listenThread = new Thread(Listen) { IsBackground = true };
            listenThread.Start();
            Debug.Log($"[UDPReceiver] Successfully listening on UDP port {Port}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UDPReceiver] Error binding to port {Port}: {ex.Message}");
        }
    }

    void Listen()
    {
        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
        while (running)
        {
            try
            {
                byte[] data = client.Receive(ref sender);
                string msg = Encoding.UTF8.GetString(data);
                lock (lockObj) { latestMessage = msg; hasNewMessage = true; }
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { break; }
        }
    }

    void Update()
    {
        string msg = null;
        lock (lockObj)
        {
            if (hasNewMessage) { msg = latestMessage; hasNewMessage = false; }
        }
        if (msg == null) return;
        msg = msg.Trim();
        if (msg.StartsWith("{")) ParseJson(msg);
        else ParseCsv(msg); // older "sender_time,activation,filter" format still works
    }

    void ParseJson(string msg)
    {
        Packet p;
        try { p = JsonUtility.FromJson<Packet>(msg); }
        catch (Exception) { return; }
        if (p == null || p.channel != 1 || p.activation < 0f) return; // only CH 1 drives the arm

        LastSenderTime = p.sender_time;
        Activation = Mathf.Clamp01(p.activation);
        EnvelopeUv = p.envelope_uv;
        if (!string.IsNullOrEmpty(p.filter)) FilterTag = p.filter;
        lastPacketTime = Time.realtimeSinceStartup;
    }

    void ParseCsv(string msg)
    {
        string[] parts = msg.Split(',');
        if (parts.Length < 2) return;
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double senderTime)) return;
        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float act)) return;
        LastSenderTime = senderTime;
        Activation = Mathf.Clamp01(act);
        if (parts.Length >= 3) FilterTag = parts[2];
        lastPacketTime = Time.realtimeSinceStartup;
    }

    void OnDestroy()
    {
        running = false;
        try { client?.Close(); } catch { }
    }
}
