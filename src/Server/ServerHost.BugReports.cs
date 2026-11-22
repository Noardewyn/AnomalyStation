using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server;

// M10x slice G5 (D282, DV206): the Esc menu's bug reports (protocol 53, MessageId.BugReport on the State channel). Not tg's: its
// snapshot has no bug report form. The host adds what it knows (the key, the round, the map, the mob, its area and tile), saves the
// report as BugReportsFolder/DATE/ROUND-N/report.txt with screenshot.webp, and posts both to the BugReportWebhook (Discord) off the
// host thread. One report per key every 2 minutes; a screenshot that is no WebP image is dropped and the report kept.
public sealed partial class ServerHost
{
    private static readonly HttpClient WebhookHttp = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly Dictionary<string, long> _lastBugReportTick = new(StringComparer.Ordinal);

    /// <summary>The newest webhook post of a bug report (the tests wait for it); null before the first.</summary>
    public Task LastBugReportPost { get; private set; }

    // A part of a report (Codec.EncodeBugReportParts): the first one is where the limits are checked, so a refused report's bytes
    // are only skipped; the body grows with the bytes that came, never by the total a client claims.
    private void ReceiveBugReport(Session s, ReadOnlySpan<byte> message)
    {
        if (s.State != SessionState.Joined) { Strike(s, "a command before Welcome"); return; }
        if (!Codec.TryDecode(message, out BugReportPart part, out ProtocolError error)) { Strike(s, "a malformed BugReport (" + error + ")"); return; }
        using (World.Enter())
        {
            if (s.Client == null)
                return;
            if (part.Offset == 0)
            {
                s.BugReportBody = null;
                s.BugReportRefused = !MayReport(s);
                if (s.BugReportRefused)
                    return;
                _lastBugReportTick[s.Ckey] = World.Tick;
                s.BugReportBody = new MemoryStream();
                s.BugReportTotal = part.Total;
            }
            else if (s.BugReportRefused)
                return;
            if (s.BugReportBody == null || part.Total != s.BugReportTotal || part.Offset != s.BugReportBody.Length) { Strike(s, "a BugReport part out of order"); return; }
            s.BugReportBody.Write(part.Bytes);
            if (s.BugReportBody.Length < s.BugReportTotal)
                return;
            byte[] body = s.BugReportBody.ToArray();
            s.BugReportBody = null;
            if (!Codec.TryDecodeBugReportBody(body, out BugReportMessage report, out error)) { Strike(s, "a malformed BugReport (" + error + ")"); return; }
            TakeBugReport(s, report);
        }
    }

    private bool MayReport(Session s)
    {
        if (_config.BugReportsFolder == null && _config.BugReportWebhook == null)
        {
            to_chat(s.Client, span_warning("This server does not collect bug reports."));
            return false;
        }
        long tick = World.Tick;
        if (_lastBugReportTick.TryGetValue(s.Ckey, out long last) && tick - last < Limits.BugReportCooldownTicks)
        {
            long seconds = (Limits.BugReportCooldownTicks - (tick - last) + 19) / 20;
            to_chat(s.Client, span_warning(string.Create(CultureInfo.InvariantCulture, $"You can send another bug report in {seconds} seconds.")));
            return false;
        }
        return true;
    }

    private void TakeBugReport(Session s, BugReportMessage report)
    {
        byte[] image = report.Image != null && IsWebP(report.Image) ? report.Image : null;
        string text = BugReportText(s, report, image != null ? "screenshot.webp" : report.Image != null ? "dropped (not a WebP image)" : "none");
        string round = RoundId ?? "0";
        if (_config.BugReportsFolder != null && !SaveBugReport(round, text, image))
        {
            to_chat(s.Client, span_warning("Your bug report could not be saved; please try again later."));
            return;
        }
        if (_config.BugReportWebhook != null)
            LastBugReportPost = PostBugReport(_config.BugReportWebhook, Dm.text("Bug report from [] (round [], []): []", s.Key, round, SSmapping.current_map?.map_name ?? "no map", report.Title), text, image);
        to_chat(s.Client, span_notice("Your bug report was sent. Thank you!"));
    }

    private string BugReportText(Session s, BugReportMessage report, string screenshot)
    {
        var text = new StringBuilder();
        void Line(string name, object value) => text.Append(name).Append(": ").Append(value).Append('\n');
        Line("Title", report.Title);
        Line("Key", s.Key);
        Line("Time", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture));
        Line("Server", BuildInfo.Display + ", protocol " + ProtocolVersion.Current.ToString(CultureInfo.InvariantCulture));
        Line("Round", RoundId ?? "none");
        Line("Map", SSmapping.current_map?.map_name ?? "none");
        mob mob = s.Client.mob;
        Line("Mob", mob == null ? "none" : Dm.text("[] ([])", DmTypes.PathOf(mob.GetType()), mob.name));
        turf turf = get_turf(mob);
        Line("Area", get_area(mob) is area a ? a.name : "none");
        Line("Tile", turf == null ? "none" : Dm.text("[], [], []", turf.x, turf.y, turf.z));
        Line("Screenshot", screenshot);
        text.Append("\nClient:\n").Append(report.Client ?? "not given").Append('\n');
        text.Append("\nWhat happened:\n").Append(report.WhatHappened).Append('\n');
        text.Append("\nSteps:\n").Append(report.Steps ?? "not given").Append('\n');
        return text.ToString();
    }

    // DATE/ROUND-N/: the first N whose folder is not there yet.
    private bool SaveBugReport(string round, string text, byte[] image)
    {
        try
        {
            string day = Path.Combine(_config.BugReportsFolder, DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            string folder;
            int n = 1;
            while (Directory.Exists(folder = Path.Combine(day, round + "-" + n.ToString(CultureInfo.InvariantCulture))))
                n++;
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "report.txt"), text);
            if (image != null)
                File.WriteAllBytes(Path.Combine(folder, "screenshot.webp"), image);
            _out.WriteLine("bug report saved: " + folder);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _err.WriteLine("bug report not saved: " + e.Message);
            return false;
        }
    }

    // M10x slice G6: rust-g's fire-and-forget HTTP (tg's /datum/http_request) for the admin help's Discord webhook, sent off the host
    // thread; a failure is the server log's line, as rust-g's is the server's
    private sealed class WebhookRequests(ServerHost host) : IHttpRequests
    {
        public void FireAndForget(string method, string url, string body, string headers)
        {
            TextWriter err = host._err;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var request = new HttpRequestMessage(new HttpMethod(method.ToUpperInvariant()), url)
                    {
                        Content = new StringContent(body ?? "", Encoding.UTF8, "application/json"),
                    };
                    using HttpResponseMessage response = await WebhookHttp.SendAsync(request).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                        err.WriteLine("admin help webhook: HTTP " + (int)response.StatusCode);
                }
                catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException or UriFormatException)
                {
                    err.WriteLine("admin help webhook: " + e.Message);
                }
            });
        }
    }

    // Discord's execute webhook: payload_json and the files as multipart (no mentions, whatever the title says).
    private Task PostBugReport(string url, string content, string text, byte[] image)
    {
        TextWriter err = _err;
        return Task.Run(async () =>
        {
            try
            {
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(JsonSerializer.Serialize(new { content, allowed_mentions = new { parse = Array.Empty<string>() } }), Encoding.UTF8, "application/json"), "payload_json");
                form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(text)), "files[0]", "report.txt");
                if (image != null)
                    form.Add(new ByteArrayContent(image), "files[1]", "screenshot.webp");
                using HttpResponseMessage response = await WebhookHttp.PostAsync(url, form).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    err.WriteLine("bug report webhook: HTTP " + (int)response.StatusCode);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                err.WriteLine("bug report webhook: " + e.Message);
            }
        });
    }

    /// <summary>
    /// A WebP file: the RIFF container whose size is the file's, and a first chunk of a lossy (VP8), lossless (VP8L) or extended (VP8X)
    /// image whose header gives a size of 1 to 16384 pixels a side.
    /// shortcut: checks the container and the frame header, not the pixel data; take a real decoder if broken files get through.
    /// </summary>
    public static bool IsWebP(byte[] b)
    {
        if (b.Length < 30 || Encoding.ASCII.GetString(b, 0, 4) != "RIFF" || Encoding.ASCII.GetString(b, 8, 4) != "WEBP")
            return false;
        if (BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(4)) != (uint)(b.Length - 8))
            return false;
        int width, height;
        switch (Encoding.ASCII.GetString(b, 12, 4))
        {
            case "VP8 ": // the key frame's start code, then 14-bit width and height
                if (b[23] != 0x9d || b[24] != 0x01 || b[25] != 0x2a)
                    return false;
                width = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(26)) & 0x3fff;
                height = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(28)) & 0x3fff;
                break;
            case "VP8L": // the signature byte, then 14 bits each of width - 1 and height - 1
                if (b[20] != 0x2f)
                    return false;
                uint bits = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(21));
                width = (int)(bits & 0x3fff) + 1;
                height = (int)((bits >> 14) & 0x3fff) + 1;
                break;
            case "VP8X": // 24-bit canvas width - 1 and height - 1
                width = (b[24] | b[25] << 8 | b[26] << 16) + 1;
                height = (b[27] | b[28] << 8 | b[29] << 16) + 1;
                break;
            default:
                return false;
        }
        return width is >= 1 and <= 16384 && height is >= 1 and <= 16384;
    }
}
