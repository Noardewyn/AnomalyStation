using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M1Client.P1;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package CM1 (D333) on the converted MetaStation: the station's communications consoles boot live, the shuttle catalogue lists
/// every purchasable emergency shuttle the host mounted (DV188: tg's catalogue the converter writes, at least Meta's own), and a captain
/// at a console buys one: it is paid from the cargo budget, announced, and loaded in place of the emergency shuttle at its dock, with no
/// runtime from the console's or the templates' files.
/// </summary>
public class CommsCM1MetaTests
{
    private readonly ITestOutputHelper _output;

    public CommsCM1MetaTests(ITestOutputHelper output) => _output = output;

    private static readonly string[] Files =
    {
        "communications.cs", "datums/shuttles/emergency.cs", "_shuttle.cs", "subsystem/shuttle.cs", "subsystem/mapping.cs", "zlevel.cs",
        "pray.cs",
    };

    private static List<TguiOutput> Take(mob who)
    {
        var taken = who.client.dm_tgui_output.ToList();
        who.client.dm_tgui_output.Clear();
        return taken;
    }

    [MetaFact]
    public void A_captain_buys_a_shuttle_from_the_catalogue_at_a_Meta_console()
    {
        World world = MetaMaps.Boot(out TgTestWorld.Log log);
        int bootErrors = log.Runtimes.Count;
        using (world.Enter())
        {
            // the boot's state of the shuttles, for a failure's report
            _output.WriteLine($"boot: {bootErrors} runtimes; SSshuttle initialized {SSshuttle.initialized}, {SSshuttle.mobile_docking_ports?.len} mobile ports, {SSshuttle.stationary_docking_ports?.len} stationary; traits {string.Join(", ", Dm.each(SSstation.station_traits).Select(t => t.GetType().Name))}");
            foreach (Exception e in log.Runtimes.Take(6))
            {
                _output.WriteLine($"boot runtime: {e.GetType().Name}: {e.Message}");
                foreach (string frame in (e.StackTrace ?? "").Split('\n').Take(14))
                    _output.WriteLine($"boot   {frame.Trim()}");
            }
            foreach (string line in log.Lines.Where(l => l.Contains("shuttle", StringComparison.OrdinalIgnoreCase)).Take(20))
                _output.WriteLine($"boot log: {line}");
        }
        TgTestWorld.RunLevel(world, RUNLEVEL_GAME);
        var feed = new GapsHarness.Feed();
        var commands = new Queue<Action>();
        obj_machinery_computer_communications console;
        mob_living_carbon_human bob;
        DmList purchasable;
        using (world.Enter())
        {
            PresentationSinks.Of(world).Messages = feed;
            ClientSeam.SetHost(world, new RecordingHost());
            world.AddPhase("test player commands", _ =>
            {
                while (commands.Count > 0)
                    commands.Dequeue()();
            });

            // the consoles: live, the station's ones on the shuttle caller list
            var consoles = Dm.each<obj_machinery_computer_communications>(Dm.world).ToList();
            var station = consoles.Where(c => Dm.truthy(is_station_level(c.z))).ToList();
            _output.WriteLine($"Meta: {consoles.Count} communications consoles, {station.Count} on the station; inert: {consoles.Count(c => InertSeam.IsInert(c))}");
            Assert.NotEmpty(station);
            Assert.DoesNotContain(consoles, c => InertSeam.IsInert(c));
            Assert.All(station, c => Assert.True(Dm.@in(c, GLOB.shuttle_caller_list), "on the shuttle caller list"));

            // the mounted catalogue: the priced emergency shuttles a captain buys (at least Meta's own), and the purchase access
            purchasable = Dm.list();
            foreach (object id in Dm.each(SSmapping.shuttle_templates))
            {
                var template = (datum_map_template_shuttle)SSmapping.shuttle_templates[id];
                if (template is datum_map_template_shuttle_emergency && template.who_can_purchase != null && template.credit_cost != INFINITY && !Dm.truthy(template.emag_only) && Dm.@in(ACCESS_CAPTAIN, template.who_can_purchase))
                    Dm.append(ref purchasable, template);
            }
            _output.WriteLine($"Meta: {Dm.each(SSmapping.shuttle_templates).Count()} shuttle templates, {purchasable.len} emergency shuttles a captain buys; access {string.Join(", ", Dm.each(SSshuttle.has_purchase_shuttle_access).Select(a => Dm.str(a)))}");
            Assert.Contains(SSmapping.shuttle_templates["emergency_meta"], Dm.each(purchasable).ToList());
            Assert.True(Dm.@in(ACCESS_CAPTAIN, SSshuttle.has_purchase_shuttle_access), "the captain may buy shuttles");
            Assert.Equal(TRUE, SSmapping.current_map.allow_custom_shuttles);

            // Bob the captain beside the first station console
            console = station[0];
            turf spot = null;
            foreach (object dir in Dm.each(GLOB.cardinals))
            {
                if (get_step(console, Dm.num(dir)) is turf_open open && !Dm.truthy(open.density) && !Dm.each<atom>(open.contents).Any(a => Dm.truthy(a.density)))
                {
                    spot = open;
                    break;
                }
            }
            Assert.NotNull(spot);
            bob = Dm.@new<mob_living_carbon_human>(spot);
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(spot), ITEM_SLOT_ICLOTHING))));
            var card = Dm.@new<obj_item_card_id_advanced_gold>(spot); // the captain's gold card: a grey one refuses his wildcards
            Assert.Equal(TRUE, Waiting.NoSleep(SSid_access.apply_trim_to_card(card, typeof(datum_id_trim_job_captain))));
            card.registered_name = "Bob";
            Waiting.NoSleep(bob.equip_to_slot_or_del(card, ITEM_SLOT_ID));
            Assert.Same(card, bob.wear_id);
            SSeconomy.get_dep_account(ACCOUNT_CAR).adjust_money(30000);
        }
        mob_living_carbon_human player = bob;
        commands.Enqueue(() =>
        {
            ClientSeam.KeyMob(player, "Bob");
            ClientSeam.Connect("Bob");
        });
        world.Step(2);
        Assert.NotNull(bob.client);

        // the window and the login
        string window = null;
        JsonElement data = default;
        void Step(int ticks)
        {
            for (int i = 0; i < ticks; i++)
                world.Step();
        }
        JsonElement Latest()
        {
            Step(4);
            List<TguiOutput> outputs;
            using (world.Enter())
                outputs = Take(player);
            JsonElement payload = GapsHarness.LastUpdate(outputs, out string id);
            window ??= id;
            return payload.GetProperty("data");
        }
        void Act(string action, params (string Key, object Value)[] args)
        {
            commands.Enqueue(() =>
            {
                DmList payload = Dm.list();
                foreach ((string key, object value) in args)
                    payload[key] = value;
                ClientSeam.TguiMessage(player.client, window, "act/" + action, payload);
            });
        }
        commands.Enqueue(() => ClientSeam.Click(player.client, console, "icon-x=16;icon-y=16;left=1;button=left"));
        data = Latest();
        Act("toggleAuthentication");
        data = Latest();
        Assert.Equal(1, data.GetProperty("authenticated").GetInt32());
        Assert.Equal(1, data.GetProperty("canBuyShuttles").GetInt32());
        Act("setState", ("state", "buying_shuttle"));
        data = Latest();
        var listed = data.GetProperty("shuttles").EnumerateArray().Select(s => s.GetProperty("name").GetString()).OrderBy(n => n, StringComparer.Ordinal).ToList();
        List<string> expected;
        using (world.Enter())
            expected = Dm.each<datum_map_template_shuttle>(purchasable).Select(t => t.name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        _output.WriteLine("catalogue: " + string.Join("; ", listed));
        Assert.Equal(expected, listed);

        // the purchase: the Box shuttle when the catalogue is mounted, else Meta's own
        datum_map_template_shuttle bought;
        obj_docking_port_mobile_emergency old;
        object dock;
        float budget;
        float cost;
        string boughtRef;
        string buyer;
        using (world.Enter())
        {
            bought = (SSmapping.shuttle_templates["emergency_box"] ?? SSmapping.shuttle_templates["emergency_meta"]) as datum_map_template_shuttle;
            old = SSshuttle.emergency;
            dock = old.get_docked();
            Assert.NotNull(dock);
            budget = SSeconomy.get_dep_account(ACCOUNT_CAR).account_balance;
            cost = bought.credit_cost;
            boughtRef = Dm.str(REF(bought));
            buyer = Dm.str(bob.real_name);
        }
        int before = log.Runtimes.Count;
        Act("purchaseShuttle", ("shuttle", boughtRef));
        for (int i = 0; i < 400; i++)
        {
            using (world.Enter())
            {
                if (SSshuttle.emergency != old && SSshuttle.emergency != null)
                    break;
            }
            world.Step();
        }
        Step(2 * 20);
        using (world.Enter())
        {
            _output.WriteLine($"bought {bought.name} for {cost}");
            Assert.NotSame(old, SSshuttle.emergency);
            Assert.True(QDELETED(old), "the old shuttle is gone");
            Assert.Same(dock, SSshuttle.emergency.get_docked());
            Assert.Equal((float)SHUTTLEPURCHASE_PURCHASED, SSshuttle.shuttle_purchased);
            Assert.Equal(budget - cost, SSeconomy.get_dep_account(ACCOUNT_CAR).account_balance);
            Assert.True(feed.Any(buyer + " has purchased " + bought.name + " for " + Dm.str(cost) + " credits."), string.Join(" | ", feed.Chat.Select(c => c.Html ?? "").Where(h => h.Contains("purchased", StringComparison.Ordinal))));
            Assert.True(feed.Any("Shuttle Purchase"), "the announcement's title");
            Assert.Equal("main", console.state);
            Assert.Equal("A replacement shuttle has already been purchased.", Dm.str(console.can_buy_shuttles(player)));

            var errors = log.Runtimes.Skip(before).Select(e => e.ToString().Split('\n')[0]).ToList();
            var ours = log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine($"boot runtimes {bootErrors}; during the purchase {errors.Count}; from CM1's files {ours.Count}");
            foreach (var group in errors.GroupBy(e => e.Length > 160 ? e[..160] : e))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.Empty(ours);
            Assert.True(errors.All(e => e.Contains("not translated yet", StringComparison.Ordinal)), string.Join("\n", errors));
        }
    }
}
