using System;
using System.Diagnostics;
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
                    throw new TimeoutException($"Connection to {_host}:{_port} timed out after {timeoutMs}ms");
                }

                await connectTask; // Ensure connection is actually established

                lock (_lock)
                {
                    _stream = _tcpClient.GetStream();
                }

                // Authenticate immediately after connection
                return await AuthenticateAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Disconnect();
                throw new InvalidOperationException($"Failed to connect to RCON server at {_host}:{_port}: {ex.Message}", ex);
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

                throw new InvalidOperationException($"Authentication failed: Received null or invalid response. Response type: {response?.Type.ToString() ?? "null"}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"RCON authentication failed: {ex.Message}", ex);
            }
        }

        public async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default)
        {
            if (!_isAuthenticated)
            {
                throw new InvalidOperationException("Not authenticated. Call ConnectAsync first.");
            }

            var response = await SendPacketAsync(PacketType.Command, command, cancellationToken);

            if (response == null)
            {
                Debug.WriteLine($"[RCON] SendPacketAsync returned null response for command: {command}");
                throw new InvalidOperationException($"Server returned null response for command: {command}");
            }

            var payload = response.Payload ?? string.Empty;
            Debug.WriteLine($"[RCON] Command '{command}' returned payload: '{payload}'");
            return payload;
        }

        private async Task<RconPacket?> SendPacketAsync(PacketType type, string payload, CancellationToken cancellationToken = default)
        {
            if (_stream == null)
                throw new InvalidOperationException("Not connected. NetworkStream is null. Call ConnectAsync first.");

            int requestId = _requestId++;

            // Send packet
            var packetData = BuildPacket(requestId, type, payload);

            try
            {
                // Log hex dump of packet for debugging
                Debug.WriteLine($"[RCON] Sending packet hex: {BitConverter.ToString(packetData)}");

                lock (_lock)
                {
                    _stream.Write(packetData, 0, packetData.Length);
                }

                Debug.WriteLine($"[RCON] ✅ Packet sent successfully ({packetData.Length} bytes)");

                // Read response
                var responsePacket = await ReadPacketAsync(cancellationToken);
                if (responsePacket == null)
                {
                    throw new InvalidOperationException($"Received null response packet after sending {type} request with ID {requestId}. Server may have closed the connection or sent invalid data.");
                }
                return responsePacket;
            }
            catch (Exception ex)
            {
                Disconnect();
                throw new InvalidOperationException($"Error sending {type} packet (ID: {requestId}, payload length: {payload?.Length ?? 0}): {ex.Message}", ex);
            }
        }

        private byte[] BuildPacket(int requestId, PacketType type, string payload)
        {
            var payloadBytes = Encoding.UTF8.GetBytes(payload);
            // According to RCON protocol:
            // Length = 4 (RequestId) + 4 (Type) + PayloadLength + 1 (String Null) + 1 (Packet Null)
            int length = 4 + 4 + payloadBytes.Length + 1 + 1;

            var packet = new byte[length + 4]; // +4 for length field itself

            int offset = 0;

            // Length (4 bytes) - Total length of remaining packet bytes
            BitConverter.GetBytes(length).CopyTo(packet, offset);
            offset += 4;

            // Request ID (4 bytes)
            BitConverter.GetBytes(requestId).CopyTo(packet, offset);
            offset += 4;

            // Type (4 bytes)
            BitConverter.GetBytes((int)type).CopyTo(packet, offset);
            offset += 4;

            // Payload (variable length bytes)
            Buffer.BlockCopy(payloadBytes, 0, packet, offset, payloadBytes.Length);
            offset += payloadBytes.Length;

            // String Null Terminator (1 byte) - Ends the UTF-8 string
            packet[offset++] = 0;

            // Packet Null Terminator (1 byte) - Ends the entire packet
            packet[offset++] = 0;

            Debug.WriteLine($"[RCON] Packet built: Length={length}, RequestId={requestId}, Type={type}, PayloadBytes={payloadBytes.Length}");
            Debug.WriteLine($"[RCON] Total packet size: {packet.Length} bytes (4 length + {length} payload)");

            return packet;
        }

        private async Task<RconPacket?> ReadPacketAsync(CancellationToken cancellationToken = default)
        {
            if (_stream == null)
                throw new InvalidOperationException("NetworkStream is null. Connection may have been closed.");

            try
            {
                // Read length (4 bytes)
                var lengthBytes = new byte[4];
                await ReadExactAsync(_stream, lengthBytes, 4, cancellationToken);
                int length = BitConverter.ToInt32(lengthBytes, 0);

                Debug.WriteLine($"[RCON] Received packet length field: {length} bytes");
                Debug.WriteLine($"[RCON] Length field hex: {BitConverter.ToString(lengthBytes)}");

                if (length < 10 || length > 4096) // Sanity check
                    throw new InvalidOperationException($"Received invalid packet length: {length} bytes. Expected between 10 and 4096 bytes.");

                // Read the rest of the packet
                var packetBytes = new byte[length];
                await ReadExactAsync(_stream, packetBytes, length, cancellationToken);

                Debug.WriteLine($"[RCON] Full packet hex: {BitConverter.ToString(packetBytes)}");

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

                // Debug logging: print raw payload bytes count
                Debug.WriteLine($"[RCON] Received payload bytes: {payloadLength}");

                // Safely extract payload and trim trailing null bytes
                byte[] payloadBytes = new byte[payloadLength];
                if (payloadLength > 0)
                {
                    Array.Copy(packetBytes, offset, payloadBytes, 0, payloadLength);
                }

                // Convert to string safely, handling empty payloads
                string payload = payloadLength > 0 
                    ? Encoding.UTF8.GetString(payloadBytes).TrimEnd('\0') 
                    : string.Empty;

                // If payload is empty after trimming, log it
                if (string.IsNullOrEmpty(payload))
                {
                    Debug.WriteLine($"[RCON] Empty payload received for packet type {type}, request ID {requestId}");
                    payload = string.Empty; // Ensure it's empty string, not null
                }

                Debug.WriteLine($"[RCON] Parsed payload: '{payload}' (Type: {type}, RequestId: {requestId})");

                return new RconPacket(requestId, type, payload);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to read or parse RCON packet: {ex.Message}", ex);
            }
        }

        private async Task ReadExactAsync(NetworkStream stream, byte[] buffer, int bytesToRead, CancellationToken cancellationToken)
        {
            int bytesRead = 0;
            while (bytesRead < bytesToRead)
            {
                int read = await stream.ReadAsync(buffer, bytesRead, bytesToRead - bytesRead, cancellationToken);
                if (read == 0)
                    throw new InvalidOperationException($"Connection closed by remote host. Expected to read {bytesToRead - bytesRead} more bytes, but got 0. Total bytes read so far: {bytesRead}/{bytesToRead}");
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