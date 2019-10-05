using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TcpClientServer
{
    /// <summary>
    /// TCP-клиент для обмена текстовыми сообщениями.
    /// Сообщение состоит из:
    /// 1) заголовка (4 байта - длина тела сообщения)
    /// 2) тела - текста в UTF-8 длиной, указанной в заголовке
    /// </summary>
    public sealed class TcpMessageClient : IDisposable
    {
        /// <summary>
        /// Доменное имя, либо IP-адрес, куда отправлять сообщения
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Порт, куда отправлять сообщения
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Отправить текстовое сообщение
        /// </summary>
        /// <param name="text">Текст сообщения</param>
        public void SendMessage(string text)
        {
            if (String.IsNullOrEmpty(text))
                throw new ArgumentNullException(nameof(text));

            OutputLock.Wait();
            try
            {
                if (Output == null)
                {
                    if (Port == 0)
                        throw new InvalidOperationException("Port is empty");

                    if (String.IsNullOrEmpty(Host))
                        throw new InvalidOperationException("Host is empty");

                    Output = new TcpClient(Host, Port);
                }

                if (!Output.Connected)
                    Output.Connect(Host, Port);

                var body = Encoding.UTF8.GetBytes(text);
                var header = BitConverter.GetBytes(body.Length);
                try
                {
                    Output.GetStream().Write(header, 0, header.Length);
                    Output.GetStream().Write(body, 0, body.Length);
                }
                catch (IOException)
                {
                    Output.Dispose();
                    Output = null;
                    throw;
                }
            }
            finally
            {
                OutputLock.Release();
            }
        }

        private TcpClient Output;

        private readonly SemaphoreSlim OutputLock = new SemaphoreSlim(1);

        /// <summary>
        /// Очищает используемые ресурсы
        /// </summary>
        public void Dispose()
        {
            Output?.Dispose();
            OutputLock.Dispose();
        }
    }
}
