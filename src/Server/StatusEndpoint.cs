using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Anomaly.Server;

/// <summary>
/// D335: the server's answer to the launcher and its hub, <c>GET /status</c> over TCP on the game's port number: the name, the
/// description, the mode, the map, the players, the round's time and the client build (the launcher's README has the JSON). The smallest HTTP there is:
/// one request per connection, read with a deadline and a size cap, answered and closed. The answer is a string the host's loop
/// refreshes; this class reads nothing of the world.
/// </summary>
public sealed class StatusEndpoint : IDisposable
{
    private const int MaxRequestBytes = 4096, MaxConnections = 32;
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(3);

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stop = new();
    private int _open;

    /// <summary>The JSON of the next answers; the host's loop sets it.</summary>
    public volatile string Json = "{}";

    public StatusEndpoint(string bindAddress, int port)
    {
        _listener = new TcpListener(string.IsNullOrEmpty(bindAddress) ? IPAddress.Any : IPAddress.Parse(bindAddress), port);
        _listener.Start(MaxConnections);
        _ = Accept();
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>The status JSON; <paramref name="buildJson"/> is the client build's file as it is (checked to be JSON when loaded), or null.</summary>
    public static string Compose(string name, string description, string mode, string map, int players, int maxPlayers, int? roundSeconds,
        string buildJson)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("name", name ?? "");
            json.WriteString("description", description ?? "");
            json.WriteString("mode", mode ?? "");
            json.WriteString("map", map ?? "");
            json.WriteNumber("players", players);
            json.WriteNumber("maxPlayers", maxPlayers);
            if (roundSeconds is int seconds) json.WriteNumber("roundSeconds", seconds);
            else json.WriteNull("roundSeconds");
            if (buildJson != null)
            {
                json.WritePropertyName("build");
                json.WriteRawValue(buildJson);
            }
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private async Task Accept()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
            }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(100).ConfigureAwait(false); // a caller that reset before the accept: not a reason to spin or to stop
                continue;
            }
            if (Interlocked.Increment(ref _open) > MaxConnections)
            {
                Interlocked.Decrement(ref _open); // a flood of callers is dropped unanswered
                client.Dispose();
                continue;
            }
            _ = Answer(client);
        }
    }

    private async Task Answer(TcpClient client)
    {
        try
        {
            using (client)
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
            {
                deadline.CancelAfter(Deadline);
                NetworkStream stream = client.GetStream();
                // The request up to the end of its headers, the cap or the deadline; only its first line is looked at.
                byte[] request = new byte[MaxRequestBytes];
                int length = 0;
                while (length < request.Length && request.AsSpan(0, length).IndexOf("\r\n\r\n"u8) < 0)
                {
                    int read = await stream.ReadAsync(request.AsMemory(length), deadline.Token).ConfigureAwait(false);
                    if (read == 0) break;
                    length += read;
                }
                string line = Encoding.ASCII.GetString(request, 0, length);
                bool status = line.StartsWith("GET /status ", StringComparison.Ordinal) || line.StartsWith("GET /status?", StringComparison.Ordinal);
                byte[] body = Encoding.UTF8.GetBytes(status ? Json : "");
                string head = (status
                        ? "HTTP/1.1 200 OK\r\nContent-Type: application/json; charset=utf-8\r\nCache-Control: no-store\r\n"
                        : "HTTP/1.1 404 Not Found\r\n")
                    + "Content-Length: " + body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(head), deadline.Token).ConfigureAwait(false);
                await stream.WriteAsync(body, deadline.Token).ConfigureAwait(false);
                client.Client.Shutdown(SocketShutdown.Send);
            }
        }
        catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
            // a caller that left or stalled
        }
        finally
        {
            Interlocked.Decrement(ref _open);
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
    }
}
