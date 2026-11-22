using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package SC1 on the converted MetaStation: its request consoles boot live, named and registered, RETA's tables are filled
/// by the job subsystem, and a message from the bridge's console reaches medbay's over Meta's own telecomms (the relays, the hub,
/// the message server), with no runtime from the files.
/// </summary>
public class RequestsConsoleSC1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public RequestsConsoleSC1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    [MetaFact]
    public void Metas_request_consoles_boot_registered_and_a_message_crosses_its_telecomms()
    {
        using (_meta.World.Enter())
        {
            var consoles = Dm.each<obj_machinery_requests_console>(GLOB.req_console_all).ToList();
            int inert = consoles.Count(c => InertSeam.IsInert(c));
            int unknown = consoles.Count(c => Dm.str(c.department) == "Unknown");
            int registered = (int)Dm.each(GLOB.reta_consoles_by_origin).Sum(dept => Dm.length(GLOB.reta_consoles_by_origin[dept] as DmList));
            _output.WriteLine($"Meta: {consoles.Count} request consoles ({unknown} unknown, {inert} inert), {registered} RETA-registered; departments: "
                + string.Join(", ", consoles.Select(c => Dm.str(c.department)).OrderBy(d => d, StringComparer.Ordinal)));
            Assert.True(consoles.Count >= 35, "too few request consoles");
            Assert.Equal(0, inert);
            Assert.Equal(0, unknown);
            Assert.True(registered > 0, "no console registered with RETA");
            Assert.Equal(8, Dm.length(GLOB.reta_dept_grants));
            foreach (string dept in new[] { "Medical", "Security", "Engineering", "Science", "Service", "Cargo" })
                Assert.True(Dm.length(GLOB.reta_job_trims[dept] as DmList) > 0, dept + " has no job trims");

            // the bridge writes to medbay over the station's telecomms
            var bridge = consoles.FirstOrDefault(c => Dm.str(ckey(c.department)) == "bridge") ?? consoles[0];
            var medbay = consoles.FirstOrDefault(c => Dm.str(ckey(c.department)) == "medbay")
                ?? consoles.First(c => Dm.str(ckey(c.department)) != Dm.str(ckey(bridge.department)));
            int before = (int)Dm.length(medbay.messages);
            bridge.send_message(medbay.department, "Meta test: is anyone there?", REQ_NORMAL_MESSAGE_PRIORITY, ASSISTANCE_REQUEST);
            _meta.World.Step(4);
            _output.WriteLine($"send error {bridge.has_mail_send_error}; medbay messages {before} -> {Dm.length(medbay.messages)}");
            Assert.Equal(0f, bridge.has_mail_send_error);
            Assert.Equal(before + 1, Dm.length(medbay.messages));
            var message = (datum_request_message)medbay.messages[1];
            Assert.Equal("Meta test: is anyone there?", Dm.str(message.content));
            Assert.Equal(Dm.str(bridge.department), Dm.str(message.sender_department));

            var runtimes = _meta.Log.Runtimes.Where(e =>
            {
                string text = e.ToString() ?? "";
                return text.Contains("requests_console", StringComparison.Ordinal) || text.Contains("reta", StringComparison.Ordinal)
                    || text.Contains("request_message", StringComparison.Ordinal);
            }).ToList();
            Assert.Empty(runtimes);
        }
    }
}
