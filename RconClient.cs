using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MC_Server_Manager_3
{
    public class RconClient : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _password;
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private int _requestId = 1;
        private readonly object _lock = new object();
        private bool _isAuthenticated = false;

        public bool IsConnected => _tcpClient != null && _tcpClient.Connected;
        public bool IsAuthenticated => _isAuthenticated;

        public RconClient(string host, int port, string password)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;
            _password = password ?? throw new ArgumentNullException(nameof(password));
        }

        public async Task<bool> ConnectAsync(int timeoutMs = 5000, CancellationToken cancellationToken = default)
        {
            try
            {
                lock (_lock)
                {
                    _tcpClient = new TcpClient();
                }

                var connectTask = _tcpClient.ConnectAsync(_host, _port);
                var timeoutTask = Task.Delay(timeoutMs, cancellationToken);

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    Disconnect();
                    return false;
                }

                await connectTask; // Ensure connection is actually established

                lock (_lock)
                {
                    _stream = _tcpClient.GetStream();
                }

                // Authenticate immediately after connection
                return await AuthenticateAsync(cancellationToken);
            }
            catch
            {
                Disconnect();
                return false;
            }
        }

        private async Task<bool> AuthenticateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await SendPacketAsync(PacketType.Auth, _password, cancellationToken);
                
                if (response != null && response.Type == PacketType.AuthResponse)
                {
                    _isAuthenticated = true;
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string?> SendCommandAsync(string command, CancellationToken cancellationToken = default)
        {
            if (!_isAuthenticated)
            {
                throw new InvalidOperationException("Not authenticated. Call ConnectAsync first.");
            }

            var response = await SendPacketAsync(PacketType.Command, command, cancellationToken);
            return response?.Payload;
        }

        private async Task<RconPacket?> SendPacketAsync(PacketType type, string payload, CancellationToken cancellationToken = default)
        {
            if (_stream == null)
                throw new InvalidOperationException("Not connected");

            int requestId = _requestId++;

            // Send packet
            var packetData = BuildPacket(requestId, type, payload);
            
            try
            {
                lock (_lock)
                {
                    _stream.Write(packetData, 0, packetData.Length);
                }

                // Read response
                var responsePacket = await ReadPacketAsync(cancellationToken);
                return responsePacket;
            }
            catch
            {
                Disconnect();
                throw;
            }
        }

        private byte[] BuildPacket(int requestId, PacketType type, string payload)
        {
            var payloadBytes = Encoding.UTF8.GetBytes(payload);
            int length = 4 + 4 + payloadBytes.Length + 4 + 1; // length + id + type + payload + null terminator + empty string null terminator

            var packet = new byte[length + 4]; // +4 for length field itself

            int offset = 0;
            
            // Length
            BitConverter.GetBytes(length).CopyTo(packet, offset);
            offset += 4;

            // Request ID
            BitConverter.GetBytes(requestId).CopyTo(packet, offset);
            offset += 4;

            // Type
            BitConverter.GetBytes((int)type).CopyTo(packet, offset);
            offset += 4;

            // Payload
            Buffer.BlockCopy(payloadBytes, 0, packet, offset, payloadBytes.Length);
            offset += payloadBytes.Length;

            // Null terminator for payload
            packet[offset++] = 0;

            // Empty string null terminator
            packet[offset++] = 0;

            return packet;
        }

        private async Task<RconPacket?> ReadPacketAsync(CancellationToken cancellationToken = default)
        {
            if (_stream == null)
                return null;

            try
            {
                // Read length (4 bytes)
                var lengthBytes = new byte[4];
                await ReadExactAsync(_stream, lengthBytes, 4, cancellationToken);
                int length = BitConverter.ToInt32(lengthBytes, 0);

                if (length < 10 || length > 4096) // Sanity check
                    return null;

                // Read the rest of the packet
                var packetBytes = new byte[length];
                await ReadExactAsync(_stream, packetBytes, length, cancellationToken);

                int offset = 0;

                // Request ID
                int requestId = BitConverter.ToInt32(packetBytes, offset);
                offset += 4;

                // Type
                int typeInt = BitConverter.ToInt32(packetBytes, offset);
                offset += 4;
                PacketType type = (PacketType)typeInt;

                // Payload (until first null terminator)
                int payloadLength = 0;
                while (offset + payloadLength < packetBytes.Length && packetBytes[offset + payloadLength] != 0)
                {
                    payloadLength++;
                }

                string payload = Encoding.UTF8.GetString(packetBytes, offset, payloadLength);

                return new RconPacket(requestId, type, payload);
            }
            catch
            {
                return null;
            }
        }

        private async Task ReadExactAsync(NetworkStream stream, byte[] buffer, int bytesToRead, CancellationToken cancellationToken)
        {
            int bytesRead = 0;
            while (bytesRead < bytesToRead)
            {
                int read = await stream.ReadAsync(buffer, bytesRead, bytesToRead - bytesRead, cancellationToken);
                if (read == 0)
                    throw new InvalidOperationException("Connection closed");
                bytesRead += read;
            }
        }

        public void Disconnect()
        {
            lock (_lock)
            {
                _isAuthenticated = false;
                _stream?.Close();
                _stream?.Dispose();
                _stream = null;
                _tcpClient?.Close();
                _tcpClient?.Dispose();
                _tcpClient = null;
            }
        }

        public void Dispose()
        {
            Disconnect();
        }

        private class RconPacket
        {
            public int RequestId { get; }
            public PacketType Type { get; }
            public string Payload { get; }

            public RconPacket(int requestId, PacketType type, string payload)
            {
                RequestId = requestId;
                Type = type;
                Payload = payload;
            }
        }

        private enum PacketType : int
        {
            Command = 2,
            Auth = 3,
            AuthResponse = 2,
            Response = 0,
            AuthFailure = -1
        }
    }
}