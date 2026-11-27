using System.IO;
using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package BR1 (D339, D341): bitrunning's machinery in a booted toy room with a den (a quantum server, its console, a netpod and
/// a byteforge) and Bob as a player: the console's domains and the test domain loaded from its converted map into a reservation with
/// its safehouse; Bob in the netpod arriving as an avatar with his skills and coming back with the avatar's; the forced disconnection's
/// static, flash and brain damage against the ladder's safe one; the cache carried to the safehouse's send turf paying the server's
/// points and the occupant's bitrunning points, and the byteforge printing the reward; the order console paid in bitrunning points;
/// the decrypted curiosity; and every live bitrunning type created and deleted.
/// </summary>
public class BitrunningBR1Tests
{
    private const int W = 48, H = 48;
    private const int ServerX = 6, ServerY = 6;

    private sealed record Den(AudioRoom R, Feed Feed, mob_living_carbon_human Bob, obj_machinery_quantum_server Server,
        obj_machinery_computer_quantum_console Console, obj_machinery_netpod Pod, obj_machinery_byteforge Forge);

    /// The domain maps and safehouses the host mounts from the converted maps (MapSeam), the safehouse rooms' toml and the hacker
    /// aliases (TgStrings), before the boot.
    private static void Mount(World w)
    {
        IDmFileSystem files = DmWorldState.Of(w).Files;
        foreach (string folder in new[] { "virtual_domains", "safehouses" })
            foreach (string converted in Directory.GetFiles(Path.Combine(MapFixtures.RepoRoot(), "maps", folder), "*" + MapText.Extension))
                files.WriteText("_maps/" + folder + "/" + Path.GetFileNameWithoutExtension(converted) + ".dmm", File.ReadAllText(converted));
        TgStrings.Mount(files, TgClone.Root, new[] { "strings/modular_maps/safehouse.toml", "strings/names/hackers.txt", "strings/names/cyberauth.txt" });
    }

    private static string Why(Den d) => " | " + string.Join(" | ", d.Feed.Chat.Select(c => c.Html)) + "\n"
        + string.Join("\n---\n", d.R.Room.RuntimeText.Select(t => t.Length > 1500 ? t[..1500] : t));

    private static Den Room(ulong seed)
    {
        var r = new AudioRoom(W, H, seed: seed, configure: Mount);
        Feed feed = Listen(r);
        r.In(() => (get_area(locate(ServerX, ServerY, 1)) as area).requires_power = FALSE); // the toy room has no APC
        // the toy room's level as a station level (its SSmapping keeps no z_list): the domain's reservation adds a level after it
        r.In(() =>
        {
            SSmapping.z_list = Dm.list();
            SSmapping.z_level_to_plane_offset ??= Dm.list();
            SSmapping.z_level_to_lowest_plane_offset ??= Dm.list();
            SSmapping.gravity_by_z_level ??= Dm.list();
            SSmapping.z_level_to_stack ??= Dm.list();
            SSmapping.manage_z_level(Dm.@new<datum_space_level>(1, "station", Dm.list(Dm.kv(ZTRAIT_STATION, TRUE))), FALSE, contain_turfs: FALSE);
        });
        var server = r.In(() => Dm.@new<obj_machinery_quantum_server>(locate(ServerX, ServerY, 1)));
        r.Step(2);
        var console = r.In(() => Dm.@new<obj_machinery_computer_quantum_console>(locate(ServerX, ServerY + 1, 1)));
        var pod = r.In(() => Dm.@new<obj_machinery_netpod>(locate(ServerX + 2, ServerY, 1)));
        var forge = r.In(() => Dm.@new<obj_machinery_byteforge>(locate(ServerX, ServerY - 2, 1)));
        mob_living_carbon_human bob = r.Join(ServerX + 1, ServerY + 1, "Bob"); // beside the console and the pod
        r.Step(Second);
        return new Den(r, feed, bob, server, console, pod, forge);
    }

    /// The console's window open for Bob, its window id out.
    private static JsonElement OpenConsole(Den d, out string windowId)
    {
        d.R.In(() => Take(d.Bob));
        d.R.In(() => Waiting.Detach(d.Console.ui_interact(d.Bob)));
        d.R.Step(4);
        // the window's first update carries its static data (the domains); an open window's update has none
        windowId = null;
        JsonElement found = default;
        foreach (TguiOutput o in Take(d.Bob))
        {
            if (o.Kind != TguiOutputKind.Message)
                continue;
            using JsonDocument doc = JsonDocument.Parse(o.Json);
            if (doc.RootElement.GetProperty("type").GetString() != "update")
                continue;
            if (found.ValueKind == JsonValueKind.Object && found.TryGetProperty("static_data", out _) && !doc.RootElement.GetProperty("payload").TryGetProperty("static_data", out _))
                continue;
            found = doc.RootElement.GetProperty("payload").Clone();
            windowId = o.WindowId;
        }
        Assert.True(found.ValueKind == JsonValueKind.Object, "no update");
        return found;
    }

    /// The test domain deployed through the console's window, the map loaded into its reservation.
    private static void Deploy(Den d, string key)
    {
        OpenConsole(d, out string window);
        Act(d.R, d.Bob, window, "set_domain", ("id", key));
        d.R.Step(2 * Second);
        Assert.Equal(key, d.R.In(() => d.Server.generated_domain?.key));
        Assert.True(d.R.In(() => Dm.truthy(d.Server.is_ready)), "the server is not ready after the load: " + string.Join(" | ", d.Feed.Chat.Select(c => c.Html))
            + "\n" + string.Join("\n---\n", d.R.Room.RuntimeText.Select(t => t.Length > 1500 ? t[..1500] : t)));
    }

    /// Bob put into the netpod (tg's mouse drop of himself), connected after the do_after.
    private static mob_living_carbon_human Connect(Den d)
    {
        d.R.In(() => Waiting.Detach(d.Pod.mouse_drop_receive(d.Bob, d.Bob)));
        d.R.Step(2 * Second + 10); // the do_after's two seconds
        var avatar = d.R.In(() => d.Pod.avatar_ref?.resolve() as mob_living_carbon_human);
        Assert.True(avatar != null, "no avatar: " + d.R.In(() => $"bob client {d.Bob.client != null} key {d.Bob.key} pod occupant {d.Pod.occupant} connected {d.Pod.connected} state_open {d.Pod.state_open}")
            + " | " + string.Join(" | ", d.Feed.Chat.Select(c => c.Html)) + "\n" + string.Join("\n---\n", d.R.Room.RuntimeText.Select(t => t.Length > 1500 ? t[..1500] : t)));
        Assert.NotSame(d.Bob, avatar);
        // Bob's player now plays the avatar
        Assert.Null(d.R.In(() => d.Bob.client));
        Assert.Equal("bob", d.R.In(() => Dm.str(avatar.client?.ckey)));
        return avatar;
    }

    [Fact]
    public void TheMachineryIsLive()
    {
        foreach (Type type in new[]
        {
            typeof(obj_machinery_quantum_server), typeof(obj_machinery_netpod), typeof(obj_machinery_computer_quantum_console),
            typeof(obj_machinery_byteforge), typeof(obj_machinery_computer_order_console_bitrunning), typeof(datum_component_avatar_connection),
            typeof(datum_component_netpod_healing), typeof(datum_component_virtual_entity), typeof(datum_component_npc_friendly),
            typeof(datum_lazy_template_virtual_domain), typeof(datum_lazy_template_virtual_domain_test_only),
            typeof(datum_lazy_template_virtual_domain_gondola_asteroid), typeof(datum_lazy_template_virtual_domain_grasslands_hunt),
            typeof(obj_modular_map_root_safehouse), typeof(datum_map_template_map_module), typeof(obj_structure_hololadder),
            typeof(obj_structure_closet_crate_secure_bitrunning_encrypted), typeof(obj_structure_closet_crate_secure_bitrunning_decrypted),
            typeof(obj_structure_closet_crate_secure_bitrunning_encrypted_gondola), typeof(obj_item_storage_lockbox_bitrunning_decrypted),
            typeof(obj_effect_landmark_bitrunning_mob_segment), typeof(datum_modular_mob_segment_deer), typeof(obj_effect_baseturf_helper_virtual_domain),
            typeof(turf_open_floor_holofloor_basalt), typeof(turf_closed_indestructible_rock), typeof(datum_status_effect_static_vision),
            typeof(obj_effect_mob_spawn_ghost_role_human_virtual_domain), typeof(datum_outfit_beachbum_combat),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void TheConsoleListsTheDomainsAndLoadsTheTestDomainWithItsSafehouse()
    {
        Den d = Room(10501);
        JsonElement payload = OpenConsole(d, out _);
        JsonElement data = payload.GetProperty("data");
        Assert.Equal(1, data.GetProperty("connected").GetInt32());
        Assert.Equal(1, data.GetProperty("ready").GetInt32());
        var ids = payload.GetProperty("static_data").GetProperty("available_domains").EnumerateArray().Select(e => e.GetProperty("id").GetString()).ToList();
        Assert.Contains("gondola_asteroid", ids);
        Assert.Contains("grasslands_hunt", ids);
        Assert.DoesNotContain("test_only", ids); // DOMAIN_TEST_ONLY is skipped in the UI
        Assert.Equal("qserver", d.R.In(() => d.Server.icon_state));

        Deploy(d, "test_only");
        // the domain's template is reserved and loaded, its safehouse loaded on the modular root: three hololadder spawns and the goal
        Assert.Equal(3, d.R.In(() => length(d.Server.exit_turfs)));
        var exit = d.R.In(() => d.Server.exit_turfs[1] as turf);
        Assert.True(d.R.In(() => is_area_virtual(get_area(exit))), "the exit is not in a virtual area");
        Assert.True(d.R.In(() => Dm.each<obj_machinery_quantum_server>(Dm.list(d.Server)).Any()));
        Assert.Equal("qserver_on", d.R.In(() => d.Server.icon_state));
        Assert.Contains(d.R.Sounds.Heard, s => (s.File ?? "").Contains("terminal_insert_disc"));
        d.R.AssertNoRuntimes();
    }

    [Fact]
    public void BobEntersTheNetpodAndArrivesAsAnAvatarWithHisSkillsWhichComeBackWithHim()
    {
        Den d = Room(10502);
        Deploy(d, "test_only");
        // Bob's mining experience goes with him (avatar_connection.dm:84) and what the avatar learnt comes back (:322)
        d.R.In(() => d.Bob.mind.set_experience(typeof(datum_skill_mining), 1000, silent: TRUE));
        // DV639: the radial offers Climb in on the open pod beside him (the drag of himself onto it, which Connect sends)
        Assert.Contains((RadialExtra.Enter, "Climb in"), d.R.In(() => RadialExtras.Of(d.Bob, d.Pod)));
        var avatar = Connect(d);

        Assert.Same(d.Bob, d.R.In(() => d.Pod.occupant));
        Assert.True(d.R.In(() => Dm.truthy(d.Pod.connected)));
        Assert.Equal("netpod_closed_active", d.R.In(() => d.Pod.icon_state));
        // a closed pod is not offered to the one outside it (mouse_drop_receive's state_open)
        var carl = d.R.Join(ServerX + 3, ServerY + 1, "Carl");
        Assert.DoesNotContain(d.R.In(() => RadialExtras.Of(carl, d.Pod)), e => e.Kind == RadialExtra.Enter);
        Assert.Contains(d.R.Sounds.Heard, s => (s.File ?? "").Contains("tramclose"));
        Assert.True(d.R.In(() => is_area_virtual(get_area(avatar))), "the avatar is not in the domain");
        Assert.NotNull(d.R.In(() => avatar.GetComponent(typeof(datum_component_avatar_connection))));
        Assert.NotNull(d.R.In(() => d.Bob.GetComponent(typeof(datum_component_netpod_healing))));
        Assert.Equal(1, d.R.In(() => length(d.Server.avatar_connection_refs)));
        Assert.Equal(1000f, d.R.In(() => Dm.num(avatar.mind.get_skill_exp(typeof(datum_skill_mining)))));
        Assert.NotSame(d.R.In(() => d.Bob.mind), d.R.In(() => avatar.mind));
        // the avatar's arrival: the static on the screen
        Assert.NotNull(d.R.In(() => avatar.has_status_effect(typeof(datum_status_effect_static_vision))));

        // the avatar learns, then leaves by the ladder: a safe disconnection, no brain damage
        d.R.In(() => avatar.mind.set_experience(typeof(datum_skill_mining), 1500, silent: TRUE));
        // the avatar arrived on the hololadder the server put on its exit (server/util.dm:get_avatar_destination)
        var ladder = d.R.In(() => Dm.locate<obj_structure_hololadder>(get_turf(avatar)));
        Assert.NotNull(ladder);
        d.R.In(() => Waiting.Detach(ladder.disconnect(avatar)));
        d.R.Step(4 * Second);
        Assert.NotNull(d.R.In(() => d.Bob.client));
        Assert.Equal(1500f, d.R.In(() => Dm.num(d.Bob.mind.get_skill_exp(typeof(datum_skill_mining)))));
        Assert.Equal(0f, d.R.In(() => Dm.num(d.Bob.get_organ_loss(ORGAN_SLOT_BRAIN))));
        Assert.False(d.R.In(() => Dm.truthy(d.Pod.connected)));
        d.R.AssertNoRuntimes();
    }

    [Fact]
    public void AForcedDisconnectionScramblesBobsBrain()
    {
        Den d = Room(10503);
        Deploy(d, "test_only");
        var avatar = Connect(d);
        // the console's stop with Bob still connected: the server notifies, depowers after the do_after and severs the connection
        d.R.In(() => d.Server.reset());
        d.R.Step(10);
        Assert.True(d.R.In(() => d.Bob.client != null), "Bob is not back" + Why(d));
        Assert.Equal(40f, d.R.In(() => Dm.num(d.Bob.get_organ_loss(ORGAN_SLOT_BRAIN)))); // BASE_DISCONNECT_DAMAGE, netpod/_netpod.dm:1
        Assert.NotNull(d.R.In(() => d.Bob.has_status_effect(typeof(datum_status_effect_static_vision))));
        Assert.True(d.Feed.Any("forcefully disconnected"), "no disconnection message");
        d.R.Step(2 * Second);
        // the pod drains after the healing time and opens
        d.R.Step(30 * Second);
        Assert.True(d.R.In(() => Dm.truthy(d.Pod.state_open)), "the pod did not open");
        Assert.Contains(d.R.Sounds.Heard, s => (s.File ?? "").Contains("tramopen"));
        // the domain is scrubbed 15 seconds after the reset, the avatar with it
        Assert.Null(d.R.In(() => d.Server.generated_domain));
        Assert.True(d.R.In(() => QDELETED(avatar)), "the avatar outlived the domain");
    }

    [Fact]
    public void TheCacheOnTheSendTurfPaysThePointsAndTheByteforgePrintsTheReward()
    {
        Den d = Room(10504);
        // Bob's ID with his account (an ID needs a jumpsuit under it)
        var account = d.R.In(() =>
        {
            var suit = Dm.@new<obj_item_clothing_under_color_grey>(d.Bob.loc);
            Waiting.NoSleep(d.Bob.equip_to_slot_if_possible(suit, ITEM_SLOT_ICLOTHING));
            var card = Dm.@new<obj_item_card_id_advanced>(d.Bob.loc);
            var a = Dm.@new<datum_bank_account>("Bob", null, 0);
            card.set_account(a);
            Waiting.NoSleep(d.Bob.equip_to_slot_if_possible(card, ITEM_SLOT_ID));
            return a;
        });
        Assert.Same(account, d.R.In(() => d.Bob.get_bank_account()));
        Deploy(d, "test_only");
        var avatar = Connect(d);
        // the goal turf of the safehouse (test_only_safehouse.dmm's cache_goal_turf)
        // the safehouse's bottom row: the three ladder spawns, then the send turf (test_only_safehouse.dmm)
        turf goal = d.R.In(() =>
        {
            turf last = Dm.each<turf>(d.Server.exit_turfs).OrderBy(t => Dm.num(t.x)).Last();
            return locate(Dm.num(last.x) + 1, Dm.num(last.y), Dm.num(last.z));
        });
        // the cache carried onto the send turf (a creation is no entry: it is moved there)
        var crate = d.R.In(() => Dm.@new<obj_structure_closet_crate_secure_bitrunning_encrypted>(get_step(goal, NORTH)));
        d.R.Step(2);
        d.R.In(() => Waiting.NoSleep(crate.forceMove(goal)));
        d.R.Step(Second);
        Assert.True(d.R.In(() => Dm.truthy(d.Server.domain_complete)), "the delivery did not complete the domain" + Why(d) + d.R.In(() => $" goal {goal?.x},{goal?.y} {goal?.GetType().Name} crate at {crate.loc}"));
        Assert.True(d.R.In(() => QDELETED(crate)), "the cache stayed");
        Assert.True(d.R.In(() => d.Server.points) >= 1, "the server got no points");
        // the byteforge prints the decrypted cache after its windup (byteforge.dm:76)
        d.R.Step(10 * Second);
        var reward = d.R.In(() => Dm.locate<obj_structure_closet_crate_secure_bitrunning_decrypted>(get_turf(d.Forge)));
        Assert.NotNull(reward);
        Assert.True(d.R.In(() => Dm.each<obj_item_stack_ore>(reward.contents).Any()), "the reward holds no ore");
        Assert.True(d.R.In(() => account.bitrunning_points) > 0, "Bob got no bitrunning points"); // the reward points times 100 (netpod/signals.dm:34)
        Assert.Contains(d.R.Sounds.Heard, s => (s.File ?? "").Contains("blink"));
    }

    [Fact]
    public void TheOrderConsoleSpendsBitrunningPoints()
    {
        var r = new AudioRoom(13, 9, seed: 10505);
        Feed feed = Listen(r);
        r.In(() => (get_area(locate(4, 4, 1)) as area).requires_power = FALSE);
        var console = r.In(() => Dm.@new<obj_machinery_computer_order_console_bitrunning>(locate(5, 5, 1)));
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        r.Step(Second);
        var card = r.In(() =>
        {
            var c = Dm.@new<obj_item_card_id_advanced>(bob.loc);
            var account = Dm.@new<datum_bank_account>("Bob", null, 0);
            c.set_account(account);
            account.bitrunning_points = 1000;
            Waiting.NoSleep(bob.put_in_active_hand(c));
            return c;
        });
        Assert.Equal(1000f, r.In(() => console.retrieve_points(card)));
        Assert.Equal(TRUE, r.In(() => console.subtract_points(300, card)));
        Assert.Equal(700f, r.In(() => card.registered_account.bitrunning_points));
        Assert.Equal(FALSE, r.In(() => console.subtract_points(800, card)));
        // an order of cornchips (orders/flair.dm) goes on the next cargo shuttle
        var cornchips = r.In(() => Dm.each<datum_orderable_item>(GLOB.order_console_products).First(i => i.purchase_path == typeof(obj_item_food_cornchips)));
        r.In(() => Waiting.Detach(console.order_groceries(bob, card, Dm.list(Dm.kv(cornchips, 2)))));
        r.Step(Second);
        var order = r.In(() => Dm.each<datum_supply_order>(SSshuttle.shopping_list).Last());
        Assert.IsType<datum_supply_pack_bitrunning>(r.In(() => order.pack));
        Assert.Equal(2f, r.In(() => Dm.num(order.pack.contains[typeof(obj_item_food_cornchips)])));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADecryptedCuriosityNeedsItsDomainAndHoldsItsSecondaryLoot()
    {
        var r = new AudioRoom(9, 9, seed: 10506);
        var domain = r.In(() => Dm.each<datum_lazy_template_virtual_domain>(SSbitrunning.all_domains).First(v => Dm.str(v.key) == "gondola_asteroid"));
        r.In(() => domain.secondary_loot = Dm.list(Dm.kv(typeof(obj_item_food_cornchips), 1)));
        var box = r.In(() => Dm.@new<obj_item_storage_lockbox_bitrunning_decrypted>(locate(4, 4, 1), domain));
        r.Step(2);
        Assert.False(r.In(() => QDELETED(box)));
        Assert.Same(domain, r.In(() => box.source_domain));
        Assert.NotNull(r.In(() => Dm.locate<obj_item_food_cornchips>(box)));
        // without a domain it is a runtime and gone
        var lost = r.In(() => Dm.@new<obj_item_storage_lockbox_bitrunning_decrypted>(locate(4, 4, 1)));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(lost)));
    }

    [Fact]
    public void EveryLiveBitrunningTypeIsCreatedAndDeleted()
    {
        var r = new AudioRoom(13, 13, seed: 10507, configure: Mount);
        r.In(() => (get_area(locate(6, 6, 1)) as area).requires_power = FALSE);
        var types = r.In(() => typeof(atom).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(atom_movable).IsAssignableFrom(t) && !Attribute.IsDefined(t, typeof(DmInertAttribute), inherit: false))
            .Where(t => t.GetCustomAttributes(typeof(DmPathAttribute), false).OfType<DmPathAttribute>().Any(p => p.Path.Contains("bitrunning") || p.Path.Contains("quantum") || p.Path.Contains("netpod") || p.Path.Contains("byteforge") || p.Path.Contains("hololadder") || p.Path.Contains("virtual_domain")))
            .Where(t => !typeof(mob).IsAssignableFrom(t))
            // tg returns before its search under UNIT_TESTS (landmarks.dm:44): without a cache in its area it is a runtime
            .Where(t => t != typeof(obj_effect_landmark_bitrunning_crate_replacer))
            .ToList());
        Assert.True(types.Count >= 20, "only " + types.Count + " bitrunning types");
        foreach (Type type in types)
        {
            var thing = r.In(() => Dm.@new(type, locate(6, 6, 1)) as datum);
            r.Step(2);
            r.In(() => qdel(thing));
            r.Step(2);
        }
        Assert.True(r.Room.RuntimeText.All(t => t.Contains("no source domain")), string.Join("\n---\n", r.Room.RuntimeText));
    }
}
