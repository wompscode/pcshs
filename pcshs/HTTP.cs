using System.Collections.Specialized;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Web;
using MimeTypes;

namespace pcshs;

public static class Http
{
    private static string? ReceiveData(Socket cs)
    {
        byte[] data = new byte[1024];
        string returnData = "";

        try
        {
            while (cs.Available > 0)
            {
                int received = cs.Receive(data);
                returnData += Encoding.ASCII.GetString(data, 0, received);

                if (received <= 0) break;
            }
        }
        catch (SocketException e)
        {
            Console.WriteLine($"something went wrong when reading data, safely closing socket: {e.Message}");
            cs.Shutdown(SocketShutdown.Both);
            cs.Close();
            return null!;
        }
        
        return returnData;
    }
    
    public static void ConnectionHandler(Socket cs)
    {
        string? data = ReceiveData(cs);
        if (data == null)
            return; // don't continue if this is null - there was an error along the way.
        
        int rtrn = ParseRequest(data, cs);
        if (rtrn == 1)
        {
            cs.Shutdown(SocketShutdown.Both);
            cs.Close();
        }
        else
        {
            Console.WriteLine($"something went wrong here, but I'm not sure what: rtrn value was {rtrn}, expected 1.");
            cs.Shutdown(SocketShutdown.Both);
            cs.Close();
        }
    }

    private static void SendData(Socket cs, byte[] data)
    {
        // wrapper to look pretty
        try
        {
            cs.Send(data);
        }
        catch (SocketException e)
        {
            Console.Write($"something went wrong when sending data, safely closing socket: {e.Message}");
            cs.Shutdown(SocketShutdown.Both);
            cs.Close();
        }
    }
    private static int ParseRequest(string? chunk, Socket cs)
    {
        // split big data chunk into small data chunks by using carriage return line break as a delimiter
        string[] data = chunk.Trim().Split("\r\n");
        Console.WriteLine($"req:{cs.RemoteEndPoint}:{data[0].Trim().Replace("\r\n", "")}");

        // make sure we have some data (Dangerous Assumption: data[0] will always be the request we are expecting - this will be fine for normal web browsers though.)
        if (!string.IsNullOrEmpty(data[0]))
        {
            // split by whitespace
            string[] request = data[0].Split(' ');

            // trim whitespace and see what the client wants
            switch (request[0].Trim())
            {
                case "GET":
                    DateTime now = DateTime.Now;
                    string req = request[1];
                    if (req.Contains("?")) // filenames can't contain ? and users would only be screwing over their experience by misplacing a ? so that's how I'm gonna detect querystrings lol
                        req = request[1].Split('?')[0]; 
                    
                    string path = Path.GetFullPath(Path.Join(Program.Config.DataDirectory, $"{HttpUtility.UrlDecode(req)}"));
                    // check if path is actually a directory, and if it is, append index.html to it. simplistic route but it works lmfao
                    if (Directory.Exists(path))
                    {
                        FileAttributes attr = File.GetAttributes(path);
                        if (attr.HasFlag(FileAttributes.Directory))
                        {
                            path = Path.GetFullPath(Path.Join(Program.Config.DataDirectory, $"{HttpUtility.UrlDecode(req)}/index.html"));
                        }
                    }

                    if (File.Exists(path))
                    {
                        if (!Directory.GetFiles(Path.GetFullPath(Program.Config.DataDirectory), "*.*",
                                SearchOption.AllDirectories).Contains(path))
                        {
                            // Last ditch effort to ensure no relative path traversal.
                            SendData(cs, "HTTP/1.1 403 Forbidden"u8.ToArray());
                            return 1;
                        }
                        
                        FileInfo fi = new FileInfo(path); // yea
                        string mimetype = MimeTypeMap.GetMimeType(fi.Extension); // get mimetype
                        byte[] content = File.ReadAllBytes(path); // read requested file
                        // construct response and send it
                        string response = $"HTTP/1.1 200 OK\r\nDate: {now.ToLongDateString()} {now.ToLongTimeString()}\r\nContent-Type: {mimetype}; charset=UTF-8\r\nContent-Length: {content.Length}\r\nServer: {Program.Config.ServerValue}\r\nConnection: close\r\n\r\n";
                        SendData(cs, Encoding.ASCII.GetBytes(response).Concat(content).ToArray());
                    }
                    else
                    {
                        // find NotFoundPage first
                        string s = Path.Join(Program.Config.DataDirectory, Program.Config.NotFoundPage);
                        if (File.Exists(s))
                        {
                            // send this if exists
                            FileInfo fi = new FileInfo(s);
                            string mimetype = MimeTypeMap.GetMimeType(fi.Extension);
                            byte[] content = File.ReadAllBytes(s);
                            string response = $"HTTP/1.1 404 Not Found\r\nDate: {now.ToLongDateString()} {now.ToLongTimeString()}\r\nContent-Type: {mimetype}; charset=UTF-8\r\nContent-Length: {content.Length}\r\nServer: {Program.Config.ServerValue}\r\nConnection: close\r\n\r\n";
                            SendData(cs, Encoding.ASCII.GetBytes(response).Concat(content).ToArray());
                        }
                        else
                        {
                            // double not found lol
                            SendData(cs, "HTTP/1.1 404 Not Found\r\n"u8.ToArray());
                        }
                    }
                    return 1;
                // all unimplemented request types will return a 501 (not implemented) with a reference to the Funniest Ironic Comic Ever
                default:
                    SendData(cs, "HTTP/1.1 501 i warned you about the stairs bro\r\n"u8.ToArray());
                    return 1;
            }
        }

        return 1;
    }
}