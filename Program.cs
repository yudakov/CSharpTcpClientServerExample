using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace TcpClientServer
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            WriteLine("TCP text message Client/Server");

            var arguments = new Dictionary<string, string>();
            foreach (var str in args)
            {
                var pair = str.Split('=');
                arguments.Add(pair[0], pair.Length == 1 ? String.Empty : pair[1]);
            }

            if (!arguments.ContainsKey("-local") ||
                !arguments.ContainsKey("-remote"))
            {
                WriteLine("Usage example:\r\n" +
                          "TcpClientServer.exe -local=127.0.0.1:1000 -remote=127.0.0.1:1001");
                return;
            }

            try
            {
                if (!arguments.TryGetValue("-remote", out var value))
                    throw new ArgumentNullException("-remote");

                var pair = value.Split(':');
                if (pair.Length != 2)
                    throw new ArgumentOutOfRangeException("Invalid remote address:port");

                using (var client = new TcpMessageClient()
                {
                    Host = pair[0],
                    Port = int.Parse(pair[1])
                })
                {
                    WriteLine($"Using remote party at {client.Host}:{client.Port}");
                    
                    if (!arguments.TryGetValue("-local", out value))
                        throw new ArgumentNullException("-local");

                    pair = value.Split(':');
                    if (pair.Length != 2)
                        throw new ArgumentOutOfRangeException("Invalid local address:port");

                    using (var server = new TcpMessageServer()
                    {
                        Address = pair[0],
                        Port = int.Parse(pair[1])
                    })
                    {
                        WriteLine($"Listening at {server.Address}:{server.Port}");
                        
                        server.MessageReceived += (tcpClient, text) => { WriteLine("< " + text); };
                        server.Start();

                        WriteLine("Write text and press Enter to send message. Send empty message to exit");
                        while (true)
                        {
                            var line = Console.ReadLine();
                            if (String.IsNullOrEmpty(line))
                            {
                                WriteLine("Exit...");
                                break;
                            }

                            try
                            {
                                client.SendMessage(line);
                            }
                            catch (Exception ex)
                            {
                                WriteLine(ex.ToString());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex.ToString());
            }
        }

        static void WriteLine(string text)
        {
            Trace.WriteLine(text);
            Console.WriteLine(text);
        }
    }
}