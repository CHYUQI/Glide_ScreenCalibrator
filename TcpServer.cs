using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ScreenCalibrator
{
    public class Message
    {
        public string Command { get; set; }
        public string Data { get; set; }
    }
    public class TcpServer
    {
        
    }
}