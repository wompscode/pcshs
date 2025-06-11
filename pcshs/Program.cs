using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using static pcshs.Http;

namespace pcshs;

class Program
{
    // I've never written any sort of HTTP server before - I've KNOWN how HTTP works, but I've never implemented it myself!
    // This will never be anything other than a simple GET server though.
    private static Socket? _srv;
    public static Config Config;
    private static bool _killFlag;

    public static void Kill() => _killFlag = true;
    
    static void Main()
    {
        Console.WriteLine("Phoebe's C# HTTP Server");
        Console.WriteLine("pcshs:starting");

        if (File.Exists(@"./config.json"))
        {
            string json = File.ReadAllText(@"./config.json");
            Config = LoadConfig(json);
        }
        else
        {
            Console.WriteLine("fatal:no config file found.");
            Environment.Exit(1);
        }

        if (!Directory.Exists(Config.DataDirectory))
        {
            Console.WriteLine("fatal:no data directory found.");
            Environment.Exit(1);
        }
        
        var endpoint = new IPEndPoint(IPAddress.Any, Config.Port);
        _srv = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        
        _srv.Bind(endpoint);
        _srv.Listen(100);
        
        Console.WriteLine($"pcshs:listening on port {Config.Port}.");

        while (_killFlag == false)
        {
            Socket cs = _srv.Accept();
            if (cs.Connected)
            {
                Console.WriteLine($"conn:{cs.RemoteEndPoint}");
                Thread socketThread = new Thread(() => ConnectionHandler(cs));
                socketThread.Start();
            }
        }
    }

    private static Config LoadConfig(string data)
    {
        Config config = JsonSerializer.Deserialize<Config>(data);
        if(string.IsNullOrEmpty(config.ServerValue)) config.ServerValue = "pcshs/0.1 (Skaianet)";
        if(string.IsNullOrEmpty(config.DataDirectory)) config.DataDirectory = "./data/";
        if(config.Port == 0) config.Port = 8000;
        return config;
    }
}