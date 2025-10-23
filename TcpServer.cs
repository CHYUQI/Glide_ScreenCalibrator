using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net.NetworkInformation; // 用于网络接口信息

namespace ScreenCalibrator
{
    public class Message
    {
        public string Command { get; set; }
        public dynamic Data { get; set; }
    }

    public class TcpServer
    {
        private TcpListener _server;
        private readonly int _port = 8888; // 固定端口
        public string LocalIp { get; private set; } // 本机局域网IP
        // 保存已连接的客户端（iPhone/模拟工具）
        private readonly List<TcpClient> _connectedClients = new List<TcpClient>();

        // 事件：供外部订阅（比如通知UI更新）
        public event Action OnConnectionEstablished; // 客户端连接成功
        public event Action OnReadyReceived; // 收到"ready"消息（iPhone准备好）
        public event Action<string, float[][]> OnColorAnalyzedReceived; // 收到"color_analyzed"消息

        public TcpServer()
        {
            LocalIp = GetLocalIP();
        }

        public async Task StartServer()
        {
            try
            {
                _server = new TcpListener(IPAddress.Any, _port);
                _server.Start();
                Console.WriteLine($"Server started on {LocalIp}:{_port}");

                while (true)
                {
                    var client = await _server.AcceptTcpClientAsync();
                    // 线程安全地添加客户端
                    lock (_connectedClients)
                    {
                        _connectedClients.Add(client);
                    }
                    OnConnectionEstablished?.Invoke(); // 触发连接成功事件
                    Console.WriteLine($"Client connected, connected clients: {_connectedClients.Count}");
                    // 异步处理客户端消息（不阻塞主线程）
                    _ = HandleClientMessages(client);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error starting server: " + ex.Message);
            }
        }

        private async Task HandleClientMessages(TcpClient client)
        {
            var buffer = new byte[4096];
            var stream = client.GetStream();

            try
            {
                while (true)
                {
                    // 读取客户端发送的数据
                    var byteCount = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (byteCount == 0) break; // 连接关闭

                    var messageJson = Encoding.UTF8.GetString(buffer, 0, byteCount);
                    var message = JsonConvert.DeserializeObject<Message>(messageJson);

                    // 根据命令触发不同事件
                    if (message.Command == "ready")
                    {
                        OnReadyReceived?.Invoke(); // 收到iPhone准备就绪信号
                    }
                    else if (message.Command == "color_analyzed")
                    {
                        // 解析颜色分析结果（假设Data包含mode和color_data）
                        string mode = message.Data.mode;
                        float[][] colorData = message.Data.color_data.ToObject<float[][]>();
                        OnColorAnalyzedReceived?.Invoke(mode, colorData); // 触发颜色分析完成事件
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error handling client messages: " + ex.Message);
            }
            finally
            {
                // 客户端断开后清理
                lock (_connectedClients)
                {
                    _connectedClients.Remove(client);
                    Console.WriteLine($"Client disconnected, connected clients: {_connectedClients.Count}");
                }
                client.Close(); // 释放客户端资源
            }
        }

        // 发送消息给所有已连接的客户端
        public async Task SendMessage(string command, dynamic data = null)
        {
            if (_connectedClients.Count == 0)
            {
                Console.WriteLine("No connected clients to send message");
                return;
            }

            // 构建消息并序列化为JSON
            var message = new Message
            {
                Command = command,
                Data = data
            };
            string messageJson = JsonConvert.SerializeObject(message);
            byte[] dataBytes = Encoding.UTF8.GetBytes(messageJson);

            // 遍历所有客户端发送（加锁保证线程安全）
            lock (_connectedClients)
            {
                // 用ToList()避免遍历中集合被修改导致异常
                foreach (var client in _connectedClients.ToList())
                {
                    try
                    {
                        if (client.Connected)
                        {
                            await client.GetStream().WriteAsync(dataBytes, 0, dataBytes.Length);
                            Console.WriteLine($"Sent message: {command}");
                        }
                        else
                        {
                            _connectedClients.Remove(client);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Send failed: {ex.Message}");
                        _connectedClients.Remove(client);
                    }
                }
            }
        }

        // 停止服务器
        public void StopServer()
        {
            try
            {
                _server?.Stop();
                Console.WriteLine("Server stopped");

                lock (_connectedClients)
                {
                    foreach (var client in _connectedClients)
                    {
                        client.Close();
                    }
                    _connectedClients.Clear();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error stopping server: " + ex.Message);
            }
        }

        // 获取本机局域网IPv4地址
        private string GetLocalIP()
        {
            foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                // 过滤掉禁用的接口和环回接口
                if (netInterface.OperationalStatus != OperationalStatus.Up || 
                    netInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                // 检查IPv4地址
                var addresses = netInterface.GetIPProperties().UnicastAddresses;
                foreach (var addr in addresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork && 
                        !IPAddress.IsLoopback(addr.Address))
                    {
                        return addr.Address.ToString();
                    }
                }
            }
            return "127.0.0.1"; //  fallback
        }
    }
}