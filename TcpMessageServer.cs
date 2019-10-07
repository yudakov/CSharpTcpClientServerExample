using System;
using System.Diagnostics;
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

            Input = new TcpListener(IPAddress.Parse(Address), Port);
            try
            {
                Input.Start();
            }
            catch (Exception)
            {
                Stop();
                throw;
            }

            AcceptNextClient();
        }

        async void AcceptNextClient()
        {
            try
            {
                var client = await Input.AcceptTcpClientAsync();
                AcceptNextClient();

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
        
        async Task ProcessClient(TcpClient client)
        {
            var chunk = new byte[10000];
            using (var buffer = new MemoryStream())
            {
                while (true)
                {
                    int count;
                    try
                    {
                        count = await client.GetStream().ReadAsync(chunk, 0, chunk.Length);
                    }
                    catch (InvalidOperationException)
                    {
                        break; // Connection closed
                    }
                    if (count == 0)
                        break; // Connection closed

                    buffer.Write(chunk, 0, count);
                    
                    const int headerLength = 4;
                    if (buffer.Length > headerLength)
                    {
                        var bodyLength = BitConverter.ToInt32(buffer.GetBuffer(), 0);
                        if (buffer.Length >= headerLength + bodyLength)
                        {
                            var text = Encoding.UTF8.GetString(buffer.GetBuffer(), headerLength, bodyLength);
                            try
                            {
                                MessageReceived?.Invoke(client, text);
                            }
                            catch (Exception ex)
                            {
                                Trace.WriteLine(ex);
                            }

                            var remainder = buffer.Length - (headerLength + bodyLength);
                            if (remainder > 0)
                                Array.Copy(buffer.GetBuffer(), headerLength + bodyLength,
                                    buffer.GetBuffer(), 0, remainder);

                            buffer.SetLength(remainder);
                        }
                    }
                }
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
