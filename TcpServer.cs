using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using Newtonsoft.Json;

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
        // 关键：保存已连接的客户端（iPhone/模拟工具）
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
                    _connectedClients.Add(client); // 保存已连接的客户端
                    OnConnectionEstablished?.Invoke(); // 触发连接成功事件
                    Console,WriteLine("Client connected,Counting connected clients: " + _connectedClients.Count);
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
                    var byteCount = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (byteCount == 0) break; // 连接关闭

                    var messageJson = Encoding.UTF8.GetString(buffer, 0, byteCount);
                    var message = JsonConvert.DeserializeObject<Message>(messageJson);

                    if (message.Command == "ready")
                    {
                        OnReadyReceived?.Invoke(); // 触发ready事件
                    }
                    else if (message.Command == "color_analyzed")
                    {
                        string mode = message.Data.mode;
                        float[][] colorData = message.Data.color_data.ToObject<float[][]>();
                        OnColorAnalyzedReceived?.Invoke(mode, colorData); // 触发color_analyzed事件
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error handling client messages: " + ex.Message);
            }
            finally
            {
                client.Close();
                _connectedClients.Remove(client); // 移除断开的客户端
                Console.WriteLine("Client disconnected,Counting connected clients: " + _connectedClients.Count);
            }
        }
    }
}