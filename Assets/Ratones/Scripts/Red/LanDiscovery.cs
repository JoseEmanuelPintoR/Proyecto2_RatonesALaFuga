using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Ratones.Basic
{
    // UDP se usa solo para encontrar la sala; la partida funciona por TCP.
    public sealed class LanBeacon : IDisposable
    {
        readonly UdpClient udp;
        readonly string code, instance = Guid.NewGuid().ToString("N");
        readonly int gamePort;
        volatile bool alive = true;
        public int BoundPort { get { return ((IPEndPoint)udp.Client.LocalEndPoint).Port; } }
        public LanBeacon(string roomCode, int tcpPort, int discoveryPort = Rules.DiscoveryPort)
        {
            code = roomCode; gamePort = tcpPort;
            udp = new UdpClient(AddressFamily.InterNetwork);
            try
            {
                udp.ExclusiveAddressUse = false;
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryPort));
                udp.Client.ReceiveTimeout = 500;
                new Thread(Loop) { IsBackground = true, Name = "Ratones descubrimiento" }.Start();
            }
            catch { udp.Close(); throw; }
        }
        void Loop()
        {
            while (alive)
            {
                try
                {
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] bytes = udp.Receive(ref sender);
                    if (bytes.Length > 128) continue;
                    string[] parts = Encoding.UTF8.GetString(bytes).Split('|');
                    if (parts.Length != 3 || parts[0] != "RF2_FIND" || parts[1] != code || parts[2].Length != 32) continue;
                    byte[] reply = Encoding.UTF8.GetBytes("RF2_ROOM|" + code + "|" + parts[2] + "|" + gamePort + "|" + instance);
                    udp.Send(reply, reply.Length, sender);
                }
                catch (SocketException) { if (!alive) return; }
                catch (ObjectDisposedException) { return; }
            }
        }
        public void Dispose() { alive = false; udp.Close(); }
    }
    public static class LanDiscovery
    {
        public static string NewCode()
        {
            byte[] bytes = new byte[4];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return (100000 + BitConverter.ToUInt32(bytes, 0) % 900000).ToString();
        }
        public static bool ValidCode(string value)
        { return value != null && value.Length == 6 && value.All(c => c >= '0' && c <= '9'); }

        public static IPEndPoint Find(string code, CancellationToken cancel,
            int discoveryPort = Rules.DiscoveryPort, int timeoutMs = 5500, IPAddress[] testTargets = null)
        {
            if (!ValidCode(code)) throw new ArgumentException("Escribe los 6 números del código de sala.");
            string nonce = Guid.NewGuid().ToString("N");
            byte[] query = Encoding.UTF8.GetBytes("RF2_FIND|" + code + "|" + nonce);
            var targets = testTargets ?? Targets();
            var found = new Dictionary<string, IPEndPoint>();
            using (var udp = new UdpClient(0))
            {
                udp.EnableBroadcast = true; udp.Client.ReceiveTimeout = 100;
                var watch = Stopwatch.StartNew(); long nextSend = 0, firstResult = -1;
                while (watch.ElapsedMilliseconds < timeoutMs)
                {
                    cancel.ThrowIfCancellationRequested();
                    if (watch.ElapsedMilliseconds >= nextSend)
                    {
                        foreach (IPAddress address in targets)
                            try { udp.Send(query, query.Length, new IPEndPoint(address, discoveryPort)); } catch (SocketException) { }
                        nextSend = watch.ElapsedMilliseconds + 650;
                    }
                    try
                    {
                        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                        byte[] data = udp.Receive(ref remote);
                        if (data.Length > 256) continue;
                        string[] p = Encoding.UTF8.GetString(data).Split('|'); int port;
                        if (p.Length != 5 || p[0] != "RF2_ROOM" || p[1] != code || p[2] != nonce
                            || !int.TryParse(p[3], out port) || port < 1 || port > 65535 || p[4].Length != 32) continue;
                        found[p[4]] = new IPEndPoint(remote.Address, port);
                        if (firstResult < 0) firstResult = watch.ElapsedMilliseconds;
                    }
                    catch (SocketException e)
                    { if (e.SocketErrorCode != SocketError.TimedOut && e.SocketErrorCode != SocketError.WouldBlock) throw; }
                    if (found.Count > 1) throw new InvalidOperationException("Dos salas tienen el mismo código. El anfitrión debe crear una sala nueva.");
                    if (firstResult >= 0 && watch.ElapsedMilliseconds - firstResult >= 300) return found.Values.First();
                }
            }
            if (found.Count == 1) return found.Values.First();
            throw new TimeoutException("No se encontró la sala. Revisa el código y que todos estén en la misma Wi-Fi.");
        }
        static IPAddress[] Targets()
        {
            var list = new List<IPAddress> { IPAddress.Broadcast, IPAddress.Loopback };
            try
            {
                foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (var entry in adapter.GetIPProperties().UnicastAddresses)
                    {
                        if (entry.Address.AddressFamily != AddressFamily.InterNetwork || entry.IPv4Mask == null) continue;
                        byte[] ip = entry.Address.GetAddressBytes(), mask = entry.IPv4Mask.GetAddressBytes();
                        for (int i = 0; i < 4; i++) ip[i] = (byte)(ip[i] | ~mask[i]);
                        list.Add(new IPAddress(ip));
                    }
                }
            }
            catch (Exception) { /* Algunas versiones móviles no enumeran interfaces. */ }
            return list.Distinct().ToArray();
        }
    }
}
