// Assets/Scripts/Debug/HrUdpReceiver.cs
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class HrUdpReceiver : MonoBehaviour
{
    [Header("UDP")]
    public int listenPort = 5055;

    UdpClient _udp;
    Thread _thread;
    volatile bool _running;
    public int lastBpm;

    void Start()
    {
        try
        {
            // ★ 0.0.0.0 로 바인딩: 모든 NIC에서 수신
            var any = new IPEndPoint(IPAddress.Any, listenPort);
            _udp = new UdpClient(AddressFamily.InterNetwork);
            _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udp.Client.Blocking = true;
            _udp.EnableBroadcast = true;
            _udp.Client.Bind(any);

            Debug.Log($"[HR] UDP listen {((_udp.Client.LocalEndPoint as IPEndPoint)?.ToString() ?? "unknown")}");
            NetUtils.LogAllLocalIPv4();
            Debug.Log("[HR] Suggest host IP = " + NetUtils.GetLikelyLanIPv4());

            _running = true;
            _thread = new Thread(ListenLoop) { IsBackground = true, Name = "HR-UDP-Listen" };
            _thread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError("[HR] UDP bind failed: " + e);
        }
    }

    void ListenLoop()
    {
        var remote = new IPEndPoint(IPAddress.Any, 0);
        while (_running)
        {
            try
            {
                byte[] data = _udp.Receive(ref remote); // blocking
                var json = Encoding.UTF8.GetString(data);

                // 심플 파싱 (선택): {"hr":72} 케이스만 bpm 업데이트
                if (json.Contains("\"hr\""))
                {
                    int i = json.IndexOf("\"hr\"");
                    int colon = json.IndexOf(':', i + 4);
                    if (colon > 0)
                    {
                        int end = json.IndexOfAny(new[] { ',', '}', ' ' }, colon + 1);
                        var num = (end > colon) ? json.Substring(colon + 1, end - colon - 1) : json.Substring(colon + 1);
                        if (int.TryParse(num.Trim(), out var bpm)) lastBpm = bpm;
                    }
                }

                Debug.Log($"[HR] {remote.Address} {lastBpm} bpm | {json}");
            }
            catch (SocketException se)
            {
                // 에디터 중지시 나는 WSACancelBlockingCall 무시
                if (_running) Debug.LogWarning("[HR] Socket: " + se.SocketErrorCode);
            }
            catch (Exception e)
            {
                if (_running) Debug.LogWarning("[HR] " + e.GetType().Name + ": " + e.Message);
            }
        }
    }

    void OnDestroy()
    {
        _running = false;
        try { _udp?.Close(); } catch { }
        try { _thread?.Join(100); } catch { }
    }
}
