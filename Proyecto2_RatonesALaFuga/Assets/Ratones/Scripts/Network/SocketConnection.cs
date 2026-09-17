using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Ratones.Network
{
    // Cada conexión tiene lectura y escritura fuera del hilo de Unity.
    public sealed class SocketConnection : IDisposable
    {
        readonly TcpClient client;
        readonly NetworkStream stream;
        readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> outgoing = new ConcurrentQueue<string>();
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        readonly object latestLock = new object();
        string latest;
        volatile bool alive = true;
        public bool Alive { get { return alive; } }
        public string Error { get; private set; }

        public SocketConnection(TcpClient tcp)
        {
            client = tcp; client.NoDelay = true;
            stream = tcp.GetStream(); stream.WriteTimeout = 2000;
            new Thread(ReadLoop) { IsBackground = true, Name = "Ratones RX" }.Start();
            new Thread(WriteLoop) { IsBackground = true, Name = "Ratones TX" }.Start();
        }
        public bool TryRead(out string line) { return incoming.TryDequeue(out line); }
        public void Send(string message, bool replacePending = false)
        {
            if (!alive) return;
            if (replacePending) { lock (latestLock) latest = message; }
            else
            {
                if (outgoing.Count >= 64) { Fail("Cola de salida saturada."); return; }
                outgoing.Enqueue(message);
            }
            wake.Set();
        }
        void ReadLoop()
        {
            try
            {
                byte[] buffer = new byte[8192];
                using (var message = new MemoryStream())
                {
                    while (alive)
                    {
                        int count = stream.Read(buffer, 0, buffer.Length);
                        if (count == 0) break;
                        for (int i = 0; i < count; i++)
                        {
                            if (buffer[i] == 10)
                            {
                                if (incoming.Count >= 128) throw new IOException("Demasiados mensajes pendientes.");
                                incoming.Enqueue(Encoding.UTF8.GetString(message.ToArray()));
                                message.SetLength(0); message.Position = 0;
                            }
                            else
                            {
                                if (message.Length >= 32768) throw new IOException("Mensaje demasiado largo.");
                                message.WriteByte(buffer[i]);
                            }
                        }
                    }
                }
                Fail("Se cerró la conexión.");
            }
            catch (Exception e) { if (alive) Fail(e.Message); }
        }
        void WriteLoop()
        {
            try
            {
                while (alive)
                {
                    wake.WaitOne(250);
                    string message;
                    while (alive && outgoing.TryDequeue(out message)) Write(message);
                    lock (latestLock) { message = latest; latest = null; }
                    if (alive && message != null) Write(message);
                }
            }
            catch (Exception e) { if (alive) Fail(e.Message); }
        }
        void Write(string message)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message + "\n");
            stream.Write(bytes, 0, bytes.Length);
        }
        void Fail(string message) { Error = message; Dispose(); }
        public void Dispose()
        {
            if (!alive) return;
            alive = false;
            try { client.Close(); } catch { }
            wake.Set();
        }
    }
}
