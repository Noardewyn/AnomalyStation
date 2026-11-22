using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Protocol;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M10x slice G5 (D282, DV206): the Esc menu's bug report through the real server. A report with a screenshot is saved as
/// DATE/ROUND-1/ (report.txt with the key and the tile the host filled in, screenshot.webp) and posted to the webhook with both files;
/// the same key's next report within 2 minutes is refused with the wait at its first part, its other parts skipped without a strike; another player's report whose image is no WebP is kept
/// without it.
/// </summary>
public class BugReportGameplayTests
{
    [Fact]
    public async Task A_bug_report_is_saved_and_posted_once_per_two_minutes_and_a_bad_screenshot_is_dropped()
    {
        string folder = Path.Combine(Path.GetTempPath(), "anomaly-bug-reports-" + Guid.NewGuid().ToString("N"));
        using var listener = new HttpListener();
        listener.Prefixes.Add("http://127.0.0.1:7792/hook/");
        listener.Start();
        Task<string> received = Task.Run(async () =>
        {
            HttpListenerContext context = await listener.GetContextAsync();
            using var reader = new StreamReader(context.Request.InputStream, Encoding.Latin1);
            string body = await reader.ReadToEndAsync();
            string head = context.Request.HttpMethod + " " + context.Request.ContentType; // read before Close, which disposes the request
            context.Response.StatusCode = 204;
            context.Response.Close();
            return head + "\n" + body;
        });
        try
        {
            using var w = new RealHeadlessWorld(config: new ServerConfig
            {
                Seed = 7, RoomWidth = 24, RoomHeight = 24, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f,
                BugReportsFolder = folder, BugReportWebhook = "http://127.0.0.1:7792/hook/",
            });
            RealHeadlessWorld.Player alice = w.Join("Alice");
            var texts = new List<string>();
            alice.Client.ServerTextReceived += t => texts.Add(t.Html);
            w.Run(20);

            Assert.True(alice.Session.BugReport(new BugReportMessage("Door stuck", "The door did not open.", "1. Walk up\n2. Click", "OS: test", ProtocolV53Tests.TinyWebP)));
            w.Run(20);
            Assert.Contains(texts, t => t.Contains("Your bug report was sent."));
            string report = Assert.Single(Directory.GetDirectories(Assert.Single(Directory.GetDirectories(folder))));
            Assert.EndsWith("-1", report);
            string text = File.ReadAllText(Path.Combine(report, "report.txt"));
            Assert.Contains("Title: Door stuck", text);
            Assert.Contains("Key: Alice", text);
            Assert.Contains("Tile: ", text);
            Assert.DoesNotContain("Tile: none", text);
            Assert.Contains("OS: test", text);
            Assert.Contains("2. Click", text);
            Assert.Equal(ProtocolV53Tests.TinyWebP, File.ReadAllBytes(Path.Combine(report, "screenshot.webp")));
            await w.Host.LastBugReportPost.WaitAsync(TimeSpan.FromSeconds(20)); // the webhook post finished
            string posted = await received.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.StartsWith("POST multipart/form-data", posted);
            Assert.Contains("Bug report from Alice", posted);
            Assert.Contains("filename=report.txt", posted);
            Assert.Contains("filename=screenshot.webp", posted);

            Assert.True(alice.Session.BugReport(new BugReportMessage("Again", "Again.", null, null, new byte[10000]))); // 3 parts: refused at the first, the rest skipped
            w.Run(20);
            Assert.Contains(texts, t => t.Contains("You can send another bug report in"));
            Assert.Single(Directory.GetDirectories(Directory.GetDirectories(folder)[0]));
            Assert.All(w.Host.Sessions, s => Assert.Equal(0, s.Strikes));

            RealHeadlessWorld.Player bob = w.Join("Bob");
            Assert.True(bob.Session.BugReport(new BugReportMessage("Junk", "Not an image.", null, null, new byte[100])));
            w.Run(20);
            string bobs = Assert.Single(Directory.GetDirectories(Directory.GetDirectories(folder)[0]), d => d.EndsWith("-2"));
            Assert.Contains("Screenshot: dropped (not a WebP image)", File.ReadAllText(Path.Combine(bobs, "report.txt")));
            Assert.False(File.Exists(Path.Combine(bobs, "screenshot.webp")));
            Assert.Equal(0, w.World.RuntimeErrorCount);
        }
        finally
        {
            listener.Stop();
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }
}
