using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;

using static System.Console;
using System.Collections.Generic;

namespace HttpServer
{
    class Client
    {
        static void Main(string[] arguments)
        {
            //UI Start
            WriteLine("Setting up server on port 80");
            //End
            HttpServer server = new HttpServer(80);
            server.Start();
        }
    }

    class HttpServer
    {
        public const string VERSION = "HTTP/1.1";
        public const string SERVERNAME = "Standard Simple HTTP Server";
        public const string MESSAGEDIRECTORY = "\\msg";
        public const string WEBDIRECTORY = "\\web";
        static List<FileSystemItem> fileSystem = new List<FileSystemItem>();
        static string workingDirectory;

        private bool isRunning;
        private TcpListener listener;

        public HttpServer(int port)
        {
            isRunning = false;
            listener = new TcpListener(IPAddress.Any, port);
        }

        public void Start()
        {
            Task server = Task.Factory.StartNew(() => ListenAndConnect());
            while (!server.IsCompleted) ;
        }

        private void ListenAndConnect()
        {
            //UI Start
            WriteLine("Waiting for an incoming connection");
            //End
            isRunning = true;
            listener.Start();
            workingDirectory = @"D:\\";
            UpdateFileSystem(workingDirectory);
            while (isRunning)
            {
                ServeClient(listener.AcceptTcpClient());
                WriteLine("Client connected");
            }
            listener.Stop();
        }

        private void ServeClient(TcpClient client)
        {
            //UI Start
            WriteLine("Handling incoming connection");
            //End
            StreamReader reader = new StreamReader(client.GetStream());
            BinaryWriter writer = new BinaryWriter(client.GetStream());
            HttpRequest(reader, writer);
            reader.Close();
            writer.Close();
            client.Close();
        }

        public static void HttpRequest(StreamReader reader, BinaryWriter writer)
        {
            StringBuilder requestString = new StringBuilder();

            while (reader.Peek() != -1)
            {
                requestString.AppendLine(reader.ReadLine());
            }
            //UI Start
            Console.WriteLine("Request =>\n" + requestString);
            //End
            string[] requestTokens = requestString.ToString().Split('\n', ' ');
            if (requestTokens.Length < 4)
            {
                Post(writer, "400 Bad Request", "text/html", new FileInfo(Environment.CurrentDirectory + HttpServer.MESSAGEDIRECTORY + "\\400.html"));
                return;
            }
            string[] request = requestString.ToString().Split(' ', '\n');
            string type = request[0];
            string url = request[1];
            string host = request[4];
            string referer = null;
            for (int count = 0; count < request.Length; count++)
            {
                if (request[count].Equals("Referer:"))
                {
                    referer = request[count + 1];
                    break;
                }
            }
            HttpResponse(request, type, url, host, referer, writer);
        }

        public static void HttpResponse(string[] request, string type, string url, string host, string referrer, BinaryWriter writer)
        {
            if (type.Equals("GET"))
            {
                Match match = Regex.Match(url, @"\b\w+(?=.script?)\b");
                if (match.Success)
                {
                    switch(match.Value)
                    {
                        case ("populate"):
                            match = Regex.Match(url, @"(?<=\/populate\.script\?directory=)\S+");
                            if (match.Success)
                            {
                            	UpdateFileSystem(workingDirectory + match.ToString().Split("%2F")[0]);
                                List(writer);
                            }
                            break;
                        default:
                            WriteLine("not moo");
                            break;
                    }
                    return;
                }
                FileInfo file = new FileInfo(Environment.CurrentDirectory + HttpServer.WEBDIRECTORY + url);
                DirectoryInfo directory = new DirectoryInfo(file + url);
                WriteLine(Environment.CurrentDirectory + HttpServer.WEBDIRECTORY + url);
                if (file.Exists)
                {
                    string extension = file.Extension.Split('.')[1];
                    string mime = "text/html";
                    if (extension.Equals("html") || extension.Equals("htm")) mime = "text/html";
                    else if (extension.Equals("css")) mime = "text/css";
                    else if (extension.Equals("js")) mime = "text/javascript";
                    else if (extension.Equals("jpg") || extension.Equals("jpe") || extension.Equals("jpeg")) mime = "image/jpeg";
                    else if (extension.Equals("gif")) mime = "image/gif";
                    else if (extension.Equals("ico")) mime = "image/ico";
                    else if (extension.Equals("mp3")) mime = "audio/mpeg";
                    else if (extension.Equals("wav")) mime = "audio/x-wav";
                    else if (extension.Equals("bmp")) mime = "image/bmp";
                    else if (extension.Equals("tif") || extension.Equals("tiff")) mime = "image/tiff";
                    Post(writer, "200 OK", mime, file);
                }
                else if (directory.Exists)
                {
                    foreach (FileInfo subfile in directory.GetFiles())
                    {
                        if (subfile.Name.Equals("index.html") || subfile.Name.Equals("index.htm") || subfile.Name.Equals("default.html") || subfile.Name.Equals("default.htm"))
                        {
                            Post(writer, "200 OK", "text/html", subfile);
                            return;
                        }
                    }
                }
                Post(writer, "404 Page Not Found", "text/html", new FileInfo(Environment.CurrentDirectory + HttpServer.MESSAGEDIRECTORY + "\\404.html"));
            }
            Post(writer, "404 Method Not Allowed", "text/html", new FileInfo(Environment.CurrentDirectory + HttpServer.MESSAGEDIRECTORY + "\\405.html"));
        }

        public static void Post(BinaryWriter writer, string status, string mime, FileInfo file)
        {
            WriteLine("POST {0}", file.Length);
            long fileSize = file.Length;
            byte[] buffer = new byte[1024];
            FileStream readFile = file.OpenRead();
            try
            {
                writer.Write(string.Format("{0} {1}\r\nServer: {2}\r\nContent-Type: {3}\r\nAccept-Ranges: bytes\r\nContent-Length: {4}\r\n\n", HttpServer.VERSION, status, HttpServer.SERVERNAME, mime, fileSize));
                for (long packetcount = 0; packetcount < fileSize / 1024; packetcount++)
                {
                    readFile.Read(buffer, 0, 1024);
                    writer.Write(buffer, 0, 1024);
                }
                readFile.Read(buffer, 0, (int)fileSize % 1024);
                writer.Write(buffer, 0, (int)fileSize % 1024);
            }
            catch (IOException)
            {
                writer.Close();
            }
            readFile.Close();
        }
        
        public static void List(BinaryWriter writer)
        {
            StringBuilder folderContents = new StringBuilder("");
            foreach (FileSystemItem item in fileSystem)
            {
                folderContents.Append(item.MachineList());
            }
            folderContents.Remove(folderContents.Length - 1, 1);
            folderContents.Append("\r\n");
            Write(folderContents);
            WriteLine("POST {0}", folderContents.Length);
            byte[] buffer = new byte[1024];
            try
            {
                writer.Write(string.Format("{0} {1}\r\nServer: {2}\r\nContent-Type: {3}\r\nAccept-Ranges: bytes\r\nContent-Length: {4}\r\n\n", HttpServer.VERSION, "200 OK", HttpServer.SERVERNAME, "text/plain; charset=iso-8859-1", folderContents.Length));
                writer.Write(folderContents.ToString());
            }
            catch (IOException)
            {
                writer.Close();
            }
        }

        public static void UpdateFileSystem(string workingDirectory)
        {
            fileSystem.Clear();
            DirectoryInfo root = new DirectoryInfo(workingDirectory);
            DirectoryInfo[] directories = root.GetDirectories();
            FileInfo[] files = root.GetFiles();
            foreach (DirectoryInfo directory in directories)
            {
                try { fileSystem.Add(new FileSystemItem(directory)); }
                catch { }
            }
            foreach (FileInfo file in files)
            {
                try { fileSystem.Add(new FileSystemItem(file)); }
                catch { }
            }
        }
    }

    class FileSystemItem
    {
        public string name;
        public long size;
        public string modificationTime;
        public string modifiedDate;
        public static int numberOfLinks = 1;
        public static int currentYear = DateTime.Now.Year;
        public bool isDirectory;
        public FileSystemItem(FileInfo file)
        {
            name = file.Name;
            size = file.Length;
            modificationTime = file.LastWriteTimeUtc.Year.ToString("0000") + file.LastWriteTimeUtc.Month.ToString("00") + file.LastWriteTimeUtc.Day.ToString("00") + file.LastWriteTimeUtc.Hour.ToString("00") + file.LastWriteTimeUtc.Minute.ToString("00") + file.LastWriteTimeUtc.Second.ToString("00");
            modifiedDate = file.LastWriteTimeUtc.Year < currentYear ? file.LastWriteTime.ToString("MMM dd  yyyy") : file.LastWriteTime.ToString("MMM dd HH:mm");
            isDirectory = false;
        }
        public FileSystemItem(DirectoryInfo directory)
        {
            name = directory.Name;
            size = 0;
            modificationTime = directory.LastWriteTimeUtc.Year.ToString("0000") + directory.LastWriteTimeUtc.Month.ToString("00") + directory.LastWriteTimeUtc.Day.ToString("00") + directory.LastWriteTimeUtc.Hour.ToString("00") + directory.LastWriteTimeUtc.Minute.ToString("00") + directory.LastWriteTimeUtc.Second.ToString("00");
            modifiedDate = directory.LastWriteTimeUtc.Year < currentYear ? directory.LastWriteTime.ToString("MMM dd  yyyy") : directory.LastWriteTime.ToString("MMM dd HH:mm");
            isDirectory = true;
        }
        public string MachineList()
        {
            return "Type=" + ((isDirectory) ? "dir" : "file") + ",Size=" + size.ToString() + ",Modify=" + modificationTime.ToString() + ",Name=" + name + ";";
        }
    }
}