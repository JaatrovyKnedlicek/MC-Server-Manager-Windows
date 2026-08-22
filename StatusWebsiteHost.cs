using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MC_Server_Manager_3
{
    public sealed class StatusWebsiteServerInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Motd { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string PublicAddress { get; set; } = string.Empty;
        public string LanAddress { get; set; } = string.Empty;
        public bool Online { get; set; }
    }

    public sealed class StatusWebsiteHost : IDisposable
    {
        private readonly Func<IReadOnlyList<StatusWebsiteServerInfo>> _getServers;
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _acceptLoop;

        public int Port { get; private set; }
        public bool IsRunning => _listener != null;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public StatusWebsiteHost(Func<IReadOnlyList<StatusWebsiteServerInfo>> getServers)
        {
            _getServers = getServers ?? throw new ArgumentNullException(nameof(getServers));
        }

        public void Start(int port)
        {
            Stop();
            Port = port;
            _cts = new CancellationTokenSource();
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            _listener = listener;
            var token = _cts.Token;
            _acceptLoop = Task.Run(() => AcceptLoopAsync(listener, token), token);
        }

        public void Stop()
        {
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
            _listener = null;
            _cts?.Dispose();
            _cts = null;
            _acceptLoop = null;
        }

        public void Dispose() => Stop();

        private async Task AcceptLoopAsync(TcpListener listener, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient? client = null;
                try
                {
                    client = await listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                    _ = Task.Run(() => HandleClientAsync(client, token), token);
                }
                catch (OperationCanceledException)
                {
                    client?.Dispose();
                    break;
                }
                catch (ObjectDisposedException)
                {
                    client?.Dispose();
                    break;
                }
                catch
                {
                    client?.Dispose();
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            using (client)
            {
                try
                {
                    using var stream = client.GetStream();
                    stream.ReadTimeout = 5000;
                    stream.WriteTimeout = 5000;

                    var request = await ReadRequestAsync(stream, token).ConfigureAwait(false);
                    if (request == null)
                        return;

                    var (status, contentType, body) = BuildResponse(request);
                    var header =
                        $"HTTP/1.1 {status}\r\n" +
                        $"Content-Type: {contentType}\r\n" +
                        $"Content-Length: {body.Length}\r\n" +
                        "Cache-Control: no-store\r\n" +
                        "Connection: close\r\n\r\n";
                    var headerBytes = Encoding.UTF8.GetBytes(header);
                    await stream.WriteAsync(headerBytes, token).ConfigureAwait(false);
                    await stream.WriteAsync(body, token).ConfigureAwait(false);
                    await stream.FlushAsync(token).ConfigureAwait(false);
                }
                catch
                {
                    // ignore per-connection failures
                }
            }
        }

        private static async Task<string?> ReadRequestAsync(NetworkStream stream, CancellationToken token)
        {
            var buffer = new byte[4096];
            var received = 0;
            while (received < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(received, buffer.Length - received), token).ConfigureAwait(false);
                if (read <= 0)
                    break;
                received += read;
                var text = Encoding.ASCII.GetString(buffer, 0, received);
                var lineEnd = text.IndexOf("\r\n", StringComparison.Ordinal);
                if (lineEnd >= 0)
                    return text[..lineEnd];
            }

            return received == 0 ? null : Encoding.ASCII.GetString(buffer, 0, received);
        }

        private (string Status, string ContentType, byte[] Body) BuildResponse(string requestLine)
        {
            var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var method = parts.Length > 0 ? parts[0].ToUpperInvariant() : "GET";
            var path = parts.Length > 1 ? parts[1] : "/";
            var q = path.IndexOf('?', StringComparison.Ordinal);
            if (q >= 0)
                path = path[..q];

            if (method is not ("GET" or "HEAD"))
                return ("405 Method Not Allowed", "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Method Not Allowed"));

            IReadOnlyList<StatusWebsiteServerInfo> servers;
            try
            {
                servers = _getServers() ?? Array.Empty<StatusWebsiteServerInfo>();
            }
            catch
            {
                servers = Array.Empty<StatusWebsiteServerInfo>();
            }

            byte[] body;
            string contentType;
            if (path.Equals("/api/status", StringComparison.OrdinalIgnoreCase))
            {
                body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { servers }, JsonOptions));
                contentType = "application/json; charset=utf-8";
            }
            else if (path is "/" or "/index.html")
            {
                body = Encoding.UTF8.GetBytes(BuildHtml(servers));
                contentType = "text/html; charset=utf-8";
            }
            else
            {
                return ("404 Not Found", "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Not Found"));
            }

            if (method == "HEAD")
                body = Array.Empty<byte>();

            return ("200 OK", contentType, body);
        }

        private static string BuildHtml(IReadOnlyList<StatusWebsiteServerInfo> servers)
        {
            var cards = new StringBuilder();
            if (servers.Count == 0)
            {
                cards.Append("<div class=\"empty\">No servers are configured in MC Server Manager.</div>");
            }
            else
            {
                foreach (var s in servers)
                {
                    var onlineClass = s.Online ? "online" : "offline";
                    var onlineText = s.Online ? "Online" : "Offline";
                    cards.Append($"""
                        <article class="card">
                          <div class="card-top">
                            <div>
                              <h2>{Html(s.Name)}</h2>
                              <p class="motd">{Html(s.Motd)}</p>
                            </div>
                            <span class="badge {onlineClass}">{onlineText}</span>
                          </div>
                          <dl class="meta">
                            <div><dt>VERSION</dt><dd>{Html(s.Version)}</dd></div>
                            <div><dt>PUBLIC</dt><dd>{Html(s.PublicAddress)}</dd></div>
                            <div><dt>LAN</dt><dd>{Html(s.LanAddress)}</dd></div>
                          </dl>
                        </article>
                        """);
                }
            }

            return $$"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                  <meta charset="utf-8">
                  <meta name="viewport" content="width=device-width, initial-scale=1">
                  <title>Minecraft Server Status</title>
                  <style>
                    :root { color-scheme: dark; }
                    * { box-sizing: border-box; }
                    body {
                      margin: 0;
                      font-family: Segoe UI, system-ui, sans-serif;
                      background: #121212;
                      color: #f5f5f5;
                    }
                    header { text-align: center; padding: 48px 16px 28px; }
                    h1 { margin: 0; font-size: 2.1rem; font-weight: 700; }
                    .subtitle { margin: 10px 0 0; color: #b0b0b0; font-size: 0.98rem; }
                    main { max-width: 920px; margin: 0 auto; padding: 0 20px 48px; display: grid; gap: 16px; }
                    .card {
                      background: #1e1e1e;
                      border: 1px solid #2c2c2c;
                      border-radius: 14px;
                      padding: 22px 24px;
                    }
                    .card-top { display: flex; justify-content: space-between; gap: 16px; align-items: flex-start; }
                    h2 { margin: 0; font-size: 1.25rem; }
                    .motd { margin: 6px 0 0; color: #ececec; }
                    .badge {
                      border-radius: 999px;
                      padding: 4px 12px;
                      font-size: 0.85rem;
                      font-weight: 600;
                      white-space: nowrap;
                    }
                    .badge.online { background: #2e7d32; color: #fff; }
                    .badge.offline { background: #5c5c5c; color: #fff; }
                    .meta {
                      display: grid;
                      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
                      gap: 16px;
                      margin: 22px 0 0;
                    }
                    dt { color: #9e9e9e; font-size: 0.72rem; letter-spacing: .08em; }
                    dd { margin: 4px 0 0; }
                    .empty { text-align: center; color: #b0b0b0; padding: 32px; }
                  </style>
                </head>
                <body>
                  <header>
                    <h1>Minecraft Server Status</h1>
                    <p class="subtitle">Live overview of servers managed by MC Server Manager</p>
                  </header>
                  <main id="servers">{{cards}}</main>
                  <script>
                    async function refresh() {
                      try {
                        const res = await fetch('/api/status', { cache: 'no-store' });
                        if (!res.ok) return;
                        const data = await res.json();
                        const root = document.getElementById('servers');
                        const list = data.servers || [];
                        if (!list.length) {
                          root.innerHTML = '<div class="empty">No servers are configured in MC Server Manager.</div>';
                          return;
                        }
                        root.innerHTML = list.map(s => `
                          <article class="card">
                            <div class="card-top">
                              <div>
                                <h2>${esc(s.name)}</h2>
                                <p class="motd">${esc(s.motd)}</p>
                              </div>
                              <span class="badge ${s.online ? 'online' : 'offline'}">${s.online ? 'Online' : 'Offline'}</span>
                            </div>
                            <dl class="meta">
                              <div><dt>VERSION</dt><dd>${esc(s.version)}</dd></div>
                              <div><dt>PUBLIC</dt><dd>${esc(s.publicAddress)}</dd></div>
                              <div><dt>LAN</dt><dd>${esc(s.lanAddress)}</dd></div>
                            </dl>
                          </article>`).join('');
                      } catch (e) {}
                    }
                    function esc(v) {
                      return String(v ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
                    }
                    setInterval(refresh, 3000);
                  </script>
                </body>
                </html>
                """;
        }

        private static string Html(string? value)
        {
            return (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }
    }
}
