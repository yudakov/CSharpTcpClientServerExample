using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TcpClientServer
{
    /// <summary>
    /// TCP-сервер для обмена текстовыми сообщениями.
    /// Сообщение состоит из:
    /// 1) заголовка (4 байта - длина тела сообщения)
    /// 2) тела - текста в UTF-8 длиной, указанной в заголовке
    /// </summary>
    public sealed class TcpMessageServer : IDisposable
    {
        /// <summary>
        /// IP-адрес, где слушать входящие сообщения
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// Порт, где слушать входящие сообщения
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Получено новое входящее сообщение:
        /// TcpClient - от кого сообщение
        /// string - текст сообщения
        /// </summary>
        public event Action<TcpClient, string> MessageReceived;

        /// <summary>
        /// Начать прослушивание входящих сообщений
        /// на локальном {Address}:{Port}
        /// </summary>
        public void Start()
        {
            if (Port == 0)
                throw new InvalidOperationException("Port is empty");

            if (String.IsNullOrEmpty(Address))
                throw new InvalidOperationException("Address is empty");

            try
            {
                Input = new TcpListener(IPAddress.Parse(Address), Port);
                Input.Start();
            }
            catch (Exception)
            {
                Stop();
                throw;
            }

            AcceptSocket();
        }

        [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
        async Task ProcessClient(TcpClient client)
        {
            var chunk = new byte[10000];
            using (var buffer = new MemoryStream())
            {
                async Task<bool> ProcessStep()
                {
                    NetworkStream stream;
                    try
                    {
                        stream = client.GetStream();
                    }
                    catch (ObjectDisposedException)
                    {
                        return false;
                    }
                    catch (InvalidOperationException)
                    {
                        return false;
                    }

                    int count;
                    try
                    {
                        count = await stream.ReadAsync(chunk, 0, chunk.Length);
                    }
                    catch (ObjectDisposedException)
                    {
                        return false; // Connection closed
                    }

                    if (count == 0)
                        return false; // Connection closed

                    buffer.Write(chunk, 0, count);
                    if (buffer.Length > 4) // 4 bytes header (body length) + message body
                    {
                        var length = BitConverter.ToInt32(buffer.GetBuffer(), 0);
                        if (buffer.Length >= 4 + length)
                        {
                            var text = Encoding.UTF8.GetString(buffer.GetBuffer(), 4, length);
                            try
                            {
                                MessageReceived?.Invoke(client, text);
                            }
                            catch (Exception ex)
                            {
                                Trace.WriteLine(ex);
                            }
                            
                            var remainder = buffer.Length - (4 + length);
                            if (remainder > 0)
                                Array.Copy(buffer.GetBuffer(), 4 + length, buffer.GetBuffer(), 0, remainder);

                            buffer.SetLength(remainder);
                        }
                    }

                    return true;
                }

                while (await ProcessStep())
                {
                }
            }
        }

        async void AcceptSocket()
        {
            try
            {
                var client = await Input.AcceptTcpClientAsync();
                AcceptSocket();

                await ProcessClient(client);
            }
            catch (InvalidOperationException)
            {
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        /// <summary>
        /// Прекращает прослушивание входящих сообщений
        /// </summary>
        public void Stop()
        {
            Input?.Stop();
            Input = null;
        }

        private TcpListener Input;

        void IDisposable.Dispose()
        {
            Stop();
        }
    }
}
