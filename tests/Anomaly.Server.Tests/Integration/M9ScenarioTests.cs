using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// ROADMAP M9's done-when (social.md section 2) through the real server on MetaStation with three real clients: Alice speaks, Bob listens 3
/// tiles off, Carol stands 10 tiles off. Alice's said line reaches Bob and not Carol, her shout reaches Carol, her whisper reaches Bob only as
/// "whispers something"; a closed airlock between Alice and Bob stops her voice and a glass one does not. Carol, 40 tiles away, hears Alice's
/// headset line on Common and reads her PDA message. Back near them, Carol sees Alice's wave from the emote wheel, her hug, her high-five and
/// her crowbar handed over to Bob, Bob putting it into his backpack and Alice shoving him into the airlock. Masked, Alice is "Unknown" to Bob;
/// her flood of lines stops at tg's spam filter. The big chat window speaks as the compact bar does. With the telecomms room unpowered a
/// headset line reaches nobody (the PDA's "Server is not responding" is PdaGameplayTests').
/// </summary>
public class M9ScenarioTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    // An open floor with normal air (in low pressure tg's voice carries one tile).
    private static bool Clear(turf t) => t is turf_open_floor && !Dm.truthy(t.density) && !Dm.truthy(t.opacity)
        && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density) || Dm.truthy(a.opacity))
        && Waiting.NoSleep(t.return_air()) is datum_gas_mixture air && Dm.num(air.return_pressure()) > 90;

    // The first straight row of 15 clear tiles running east on the station level, from its west end.
    private static turf Row()
    {
        for (int y = 1; y <= Dm.num(world.maxy); y++)
        {
            int run = 0;
            for (int x = 1; x <= Dm.num(world.maxx); x++)
            {
                run = Clear(locate(x, y, 1) as turf) ? run + 1 : 0;
                if (run == 15)
                    return locate(x - 14, y, 1) as turf;
            }
        }
        return null;
    }

    private static turf East(turf from, int tiles) => locate(from.x + tiles, from.y, from.z) as turf;

    // The first clear floor at least `distance` tiles from `from` on the station level.
    private static turf Far(turf from, int distance)
    {
        for (int y = 1; y <= Dm.num(world.maxy); y++)
        {
            for (int x = 1; x <= Dm.num(world.maxx); x++)
            {
                if (locate(x, y, 1) is turf t && get_dist(from, t) >= distance && Clear(t))
                    return t;
            }
        }
        throw new InvalidOperationException("no far floor");
    }

    private static void Say(RealHeadlessWorld.Player p, string entry)
    {
        p.Session.Say(new SayOut(SayTypes.Open, SayChannels.Say));
        p.Session.Say(new SayOut(SayTypes.Entry, SayChannels.Say, entry));
        p.Session.Say(new SayOut(SayTypes.Close));
    }

    private static UiNode Shown(RealHeadlessWorld w, TguiWindows windows, RealHeadlessWorld.Player p, string ui)
    {
        w.Run(Second);
        windows.Sync(p.Session.Replica.UiWindows, 1280, 720, 1);
        return windows.Get(p.Session.Replica.UiWindows.Values.Single(v => v.Interface == ui).Window).Content;
    }

    [MetaServerFact]
    public void Alice_Bob_and_Carol_talk_listen_and_get_along_as_in_tg()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 97, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        RealHeadlessWorld.Player carol = w.Join("Carol");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var carolMob = (mob_living_carbon_human)w.MobOf(carol);
        var chat = new Dictionary<string, List<string>> { ["Alice"] = new(), ["Bob"] = new(), ["Carol"] = new() };
        foreach (RealHeadlessWorld.Player p in new[] { alice, bob, carol })
        {
            string name = p.Name;
            p.Client.ServerTextReceived += t => chat[name].Add(t.Html);
        }
        var input = new ChatInputModel(message => alice.Session.Say(message));
        alice.Client.SayModalReceived += m => input.OnServer(m);
        w.Run(4 * Second); // tgui_say.initialize sleeps 3 s, then sends the window's props
        bool Heard(string who, string text) => chat[who].Any(t => t.Contains(text, StringComparison.Ordinal));
        string Lines(string who) => string.Join(" | ", chat[who]);

        turf row = In(w, Row);
        Assert.NotNull(row);
        void Stand()
        {
            Do(w, () =>
            {
                Waiting.NoSleep(aliceMob.forceMove(row));
                Waiting.NoSleep(bobMob.forceMove(East(row, 3)));
                Waiting.NoSleep(carolMob.forceMove(East(row, 10)));
            });
            w.Run(Second);
        }
        Stand();
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        var runtimes = new List<string>();
        Action<Exception> previous = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.ToString()); previous?.Invoke(e); };
        string aliceName = In(w, () => Dm.str(aliceMob.real_name));
        string bobName = In(w, () => Dm.str(bobMob.real_name));
        string carolName = In(w, () => Dm.str(carolMob.real_name));

        // ---- 1. The ranges: said (Bob, not Carol), shouted (Carol, DV162), whispered (Bob only "something").
        Say(alice, "Hello there");
        w.Run(Second);
        Assert.True(Heard("Bob", "Hello there"), Lines("Bob"));
        Assert.False(Heard("Carol", "Hello there"));
        Say(alice, "Help me!!");
        w.Run(Second);
        Assert.True(Heard("Carol", "Help me!!"), Lines("Carol"));
        Say(alice, "#the code is 1234");
        w.Run(Second);
        Assert.False(Heard("Bob", "1234"));
        Assert.True(Heard("Bob", "something, but you are too far away to hear"), Lines("Bob"));

        // ---- 2. The big chat window speaks as the compact bar.
        input.SetChannel(SayChannels.Say);
        input.Focus();
        input.Input("From the big window", w.World.Tick / (double)Second);
        Assert.True(input.Enter());
        w.Run(Second);
        Assert.True(Heard("Bob", "From the big window"), Lines("Bob"));

        // ---- 3. Doors: a closed airlock between them stops her voice; a glass airlock does not.
        obj_machinery_door_airlock door = In(w, () => Dm.@new<obj_machinery_door_airlock>(East(row, 2)));
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(door.density) && Dm.truthy(door.opacity)));
        Say(alice, "Through the door");
        w.Run(Second);
        Assert.False(Heard("Bob", "Through the door"));
        Do(w, () => qdel(door));
        obj_machinery_door_airlock glass = In(w, () => Dm.@new<obj_machinery_door_airlock_glass>(East(row, 2)));
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(glass.density) && !Dm.truthy(glass.opacity)));
        Say(alice, "Through the glass");
        w.Run(Second);
        Assert.True(Heard("Bob", "Through the glass"), Lines("Bob"));
        Do(w, () => qdel(glass));

        // ---- 4. The radio: Carol 40 tiles off hears Alice's Common line through telecomms.
        Do(w, () => Waiting.NoSleep(carolMob.forceMove(Far(row, 40))));
        w.Run(Second);
        Say(alice, ";Can anyone hear me");
        w.Run(3 * Second);
        Assert.True(chat["Carol"].Any(t => t.Contains("Can anyone hear me", StringComparison.Ordinal) && t.Contains("[Common]", StringComparison.Ordinal)), Lines("Carol"));

        // ---- 5. A PDA message from Alice's messenger reaches Carol's PDA.
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters),
            SendClose = window => alice.Session.UiClose(window),
        };
        alice.Session.Keybinding(KeybindingCode.OpenMessenger);
        UiNode messenger = Shown(w, windows, alice, "NtosMessenger");
        messenger.Walk().Single(n => n.Key == "chat" && n.Text.Contains(carolName, StringComparison.Ordinal)).OnClick();
        messenger = Shown(w, windows, alice, "NtosMessenger");
        messenger.Walk().Single(n => n.Key == "chatInput").OnInput("Meet me at the bar");
        Shown(w, windows, alice, "NtosMessenger").Walk().Single(n => n.Key == "send").OnClick();
        w.Run(3 * Second);
        Assert.True(chat["Carol"].Any(t => t.Contains("Meet me at the bar", StringComparison.Ordinal) && t.Contains("PDA message from " + aliceName, StringComparison.Ordinal)), Lines("Carol"));
        alice.Session.UiClose(alice.Session.Replica.UiWindows.Values.Single(v => v.Interface == "NtosMessenger").Window);

        // ---- 6. Back 3 tiles off, Carol sees Alice and Bob: the wave from Alice's emote wheel, the hug, the high-five, the hand-over,
        // the backpack and the shove.
        Do(w, () =>
        {
            Waiting.NoSleep(aliceMob.forceMove(East(row, 2)));
            Waiting.NoSleep(carolMob.forceMove(East(row, 5))); // within COMBAT_MESSAGE_RANGE (3) of the shove
        });
        w.Run(Second);
        var wheel = new EmoteWheel();
        wheel.Press(0);
        wheel.Update(0.3);
        wheel.Model.Motion(0, -60); // up: the first, Wave
        RadialEntry? pick = wheel.Release(0.6);
        Assert.Equal("Wave", pick?.Label);
        Assert.True(alice.Session.Emote(EmoteWheel.KeyOf(pick.Value)));
        w.Run(Second);
        Assert.True(Heard("Carol", "waves."), Lines("Carol"));

        Assert.True(alice.Session.Click(bobMob.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.True(Heard("Carol", "hugs"), Lines("Carol"));

        void ClickAlert()
        {
            long alert = In(w, () => (bobMob.alerts[aliceName] as atom)?.dm_id ?? 0);
            Assert.True(alert != 0, "Bob has no alert from Alice: " + Lines("Bob"));
            Assert.True(bob.Session.Click(new ClickTarget(TargetKind.Screen, alert, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
            w.Run(Second);
        }
        Assert.True(alice.Session.Emote("slap"));
        w.Run(Second);
        alice.Session.Keybinding(KeybindingCode.GiveItem);
        w.Run(Second);
        ClickAlert();
        Assert.True(Heard("Carol", bobName + " high-fives " + aliceName), Lines("Carol"));
        alice.Session.Keybinding(KeybindingCode.DropItem); // the slapper is a DROPDEL hand item
        w.Run(5);

        obj_item crowbar = In(w, () =>
        {
            var item = Dm.@new<obj_item_crowbar>(get_turf(aliceMob));
            Assert.True(Dm.truthy(Waiting.NoSleep(aliceMob.put_in_hands(item))));
            return (obj_item)item;
        });
        alice.Session.Keybinding(KeybindingCode.GiveItem);
        w.Run(Second);
        ClickAlert();
        Assert.Same(bobMob, In(w, () => crowbar.loc));
        Assert.True(Heard("Carol", "takes"), Lines("Carol"));

        obj_item backpack = In(w, () => bobMob.back as obj_item);
        Assert.NotNull(backpack);
        if (In(w, () => bobMob.get_active_held_item()) != crowbar)
        {
            bob.Session.Keybinding(KeybindingCode.SwapHands);
            w.Run(5);
        }
        Assert.Same(crowbar, In(w, () => bobMob.get_active_held_item()));
        Do(w, () => bobMob.next_click = -1);
        Assert.True(bob.Session.Click(new ClickTarget(TargetKind.Screen, backpack.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None)); // in its back slot
        w.Run(Second);
        Assert.Same(backpack, In(w, () => crowbar.loc));
        Assert.True(chat["Alice"].Any(t => t.Contains("puts", StringComparison.Ordinal) && t.Contains("crowbar", StringComparison.Ordinal)), Lines("Alice")); // tg's storage line to the viewers in range

        // A closed glass airlock behind Bob (Carol sees through it): Alice's shove knocks him down against it.
        obj_machinery_door_airlock behind = In(w, () => Dm.@new<obj_machinery_door_airlock_glass>(East(row, 4)));
        w.Run(Second);
        alice.Session.ToggleCombatMode(); // tg's right-click shove is a combat mode attack
        w.Run(5);
        Do(w, () => aliceMob.next_click = -1);
        Assert.True(alice.Session.Click(bobMob.dm_id, ClickButton.Right, ClickModifiers.None));
        w.Run(Second);
        alice.Session.ToggleCombatMode();
        Assert.True(Heard("Carol", "shoves " + bobName + ", knocking"), Lines("Carol"));
        Assert.True(Heard("Alice", "You shove " + bobName), Lines("Alice"));
        Do(w, () => qdel(behind));
        w.Run(3 * Second);

        // ---- 7. Masked, without her ID and PDA, Alice is "Unknown" to Bob.
        Do(w, () =>
        {
            if (aliceMob.wear_mask != null)
                qdel(aliceMob.wear_mask);
            Assert.True(Dm.truthy(Waiting.NoSleep(aliceMob.equip_to_slot_or_del(Dm.@new<obj_item_clothing_mask_gas>(get_turf(aliceMob)), ITEM_SLOT_MASK))));
            if (aliceMob.wear_id != null)
                qdel(aliceMob.wear_id);
            if (aliceMob.belt != null)
                qdel(aliceMob.belt);
        });
        w.Run(Second);
        Say(alice, "Who am I?");
        w.Run(Second);
        string masked = chat["Bob"].LastOrDefault(t => t.Contains("Who am I?", StringComparison.Ordinal));
        Assert.True(masked != null, Lines("Bob"));
        output.WriteLine("masked: " + masked);
        Assert.Contains("Unknown", masked, StringComparison.Ordinal);
        Assert.DoesNotContain(aliceName, masked, StringComparison.Ordinal);

        // ---- 8. A flood of lines: tg's spam filter (SPAM_TRIGGER_AUTOMUTE) drops the ones past the limit.
        w.Run(6 * Second); // a fresh count window
        for (int i = 1; i <= 15; i++)
            alice.Session.Say(new SayOut(SayTypes.Entry, SayChannels.Say, $"Spam line {i}"));
        w.Run(2 * Second);
        int through = chat["Bob"].Count(t => t.Contains("Spam line ", StringComparison.Ordinal));
        output.WriteLine($"flood: {through} of 15 reached Bob");
        Assert.True(through is > 0 and <= 10, Lines("Bob")); // the say window's token bucket (Limits.SayRate, a burst of 20) passes all 15: the cut is tg's

        // ---- 9. The telecomms room's APCs off: Bob's headset line does not reach Carol.
        Do(w, () =>
        {
            foreach (obj_machinery_power_apc apc in Dm.each<obj_machinery_telecomms>(GLOB.telecomm_machines).Select(m => (get_area(m) as area)?.apc).Where(a => a != null).Distinct().ToList())
                Waiting.NoSleep(apc.toggle_breaker(null));
            Waiting.NoSleep(carolMob.forceMove(Far(row, 40)));
        });
        w.Run(6 * Second);
        Say(bob, ";Hello? Hello?");
        w.Run(5 * Second);
        Assert.False(Heard("Carol", "Hello? Hello?"), Lines("Carol"));

        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, string.Join("\n----\n", runtimes));
    }
}
