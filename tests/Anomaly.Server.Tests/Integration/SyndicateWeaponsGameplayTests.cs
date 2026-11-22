using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The syndicate campaign G2 (D316) through the real server on MetaStation with headless clients. Alice turns an energy sword on
/// (Z, tg's activate_inhand), her client draws it lit, and she cuts Bob with it. Then she clicks a syndicate bomb beside her:
/// tg's settings ask the timer in the NumberInputModal (she keeps tg's 90 seconds) and whether to start in the AlertModal (Yes);
/// the bomb lights up on her client (<c>syndicate-bomb-active</c>), her shift-click reads its display and its seconds go down;
/// she walks away and it blows: the bomb is gone from her client and tg's explosion sound reached her.
/// </summary>
public class SyndicateWeaponsGameplayTests(ITestOutputHelper output)
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

    private static string IconStateOf(ReplicaWorld replica, long id)
    {
        ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == id);
        return seen != null && replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? replica.ResolveString(look.IconState) : null;
    }

    [MetaServerFact]
    public void Alice_cuts_Bob_with_an_energy_sword_then_arms_a_syndicate_bomb_watches_it_count_down_and_blow()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 316, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var her = (mob_living_carbon_human)w.MobOf(alice);
        var him = (mob_living_carbon_human)w.MobOf(bob);
        ReplicaWorld replica = alice.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        var chat = new List<ServerText>();
        alice.Client.ServerTextReceived += chat.Add;
        w.Run(2 * Second);
        var runtimes = new List<string>();
        Action<Exception> previous = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.ToString()); previous?.Invoke(e); };

        // ---- 1. The energy sword: Z turns it on, a click cuts Bob.
        obj_item_melee_energy_sword_saber_red sword = null;
        Do(w, () =>
        {
            turf open = Builtins.get_step(him, Dm.WEST) as turf;
            Waiting.NoSleep(her.forceMove(open));
            sword = Dm.@new<obj_item_melee_energy_sword_saber_red>(her.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(her.put_in_hands(sword))));
            her.set_combat_mode(Dm.TRUE);
        });
        w.Run(Second);
        alice.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        Assert.True(In(w, () => HAS_TRAIT(sword, Defines.TRAIT_TRANSFORM_ACTIVE)), "the sword is on");
        // her body's appearance carries the lit blade in hand (the inhand overlay, tg's inhand_icon_state) and the sword's light
        Assert.True(replica.TryGet(replica.OwnEntity, out ReplicaEntity own));
        Assert.Contains("e_sword_on_red", CrewOutfitTests.Icons(replica, own.State.Appearance).Select(i => i.State ?? ""));
        Assert.True(own.Light.On, "the lit sword glows");
        Assert.Contains(heard, e => e.File != null && e.File.EndsWith("saberon.ogg", StringComparison.Ordinal));

        float brute = In(w, () => Dm.num(him.get_brute_loss()));
        Assert.True(alice.Session.Click(him.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        float after = In(w, () => Dm.num(him.get_brute_loss()));
        output.WriteLine($"Bob's brute: {brute} -> {after}");
        Assert.True(after > brute, "the lit sword cuts");
        // ---- 2. The bomb, beside her on her own tile's east (Bob goes far away first).
        Do(w, () =>
        {
            qdel(sword); // an empty hand and no combat mode: her click on the bomb is tg's interact, not an attack
            her.set_combat_mode(Dm.FALSE);
        });
        Do(w, () => Waiting.NoSleep(him.forceMove(Builtins.locate(him.x + 15, him.y, him.z) is turf_open away ? away : him.loc as atom)));
        obj_machinery_syndicatebomb bomb = In(w, () =>
        {
            turf east = Builtins.get_step(her, Dm.EAST);
            return Dm.@new<obj_machinery_syndicatebomb>(east);
        });
        w.Run(Second);
        Assert.Equal("syndicate-bomb-inactive", IconStateOf(replica, bomb.dm_id));

        Assert.True(alice.Session.Click(bomb.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState timer = Assert.Single(replica.UiWindows.Values, v => v.Interface == "NumberInputModal");
        Assert.Equal("Countdown", timer.Merged.Str("title"));
        Assert.True(alice.Session.UiAct(timer.Window, "submit", UiValue.ObjectOf(("entry", UiValue.Of(90)))));
        w.Run(Second);
        UiWindowState start = Assert.Single(replica.UiWindows.Values, v => v.Interface == "AlertModal");
        Assert.True(alice.Session.UiAct(start.Window, "choose", UiValue.ObjectOf(("choice", UiValue.Of("Yes")))));
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(bomb.active)), "the countdown started");
        Assert.Equal("syndicate-bomb-active", IconStateOf(replica, bomb.dm_id));
        Assert.Contains(chat, t => t.Html.Contains("seconds until detonation, please clear the area", StringComparison.Ordinal));

        // her shift-click reads the display, and the seconds go down
        chat.Clear();
        Assert.True(alice.Session.Click(bomb.dm_id, ClickButton.Left, ClickModifiers.Shift));
        w.Run(Second);
        float first = In(w, () => Dm.num(bomb.seconds_remaining()));
        Assert.Contains(chat, t => t.Html.Contains("A digital display on it reads", StringComparison.Ordinal));
        w.Run(3 * Second);
        Assert.True(In(w, () => Dm.num(bomb.seconds_remaining())) < first, "the timer runs");
        Assert.Contains(heard, e => e.File != null && e.File.EndsWith("timer.ogg", StringComparison.Ordinal));

        // she walks well away; the last seconds are skipped
        Do(w, () =>
        {
            Waiting.NoSleep(her.forceMove(Builtins.locate(her.x - 12, her.y, her.z) is turf_open far ? far : her.loc as atom));
            bomb.detonation_timer = Dm.world.time + 2 * Second;
        });
        for (int i = 0; i < 30 * Second && !In(w, () => QDELETED(bomb)); i += Second)
            w.Run(Second);
        Assert.True(In(w, () => QDELETED(bomb)), "the payload took the bomb with it: active " + In(w, () => Dm.str(bomb.active)) + ", timer " + In(w, () => Dm.str(bomb.detonation_timer)) + " at " + In(w, () => Dm.world.time) + "\n" + string.Join("\n", runtimes));
        w.Run(2 * Second);
        Assert.DoesNotContain(replica.Entities, e => e.Id == bomb.dm_id); // the bomb is gone from her client
        Assert.Contains(heard, e => e.File != null && e.File.StartsWith("sound/effects/explosion/", StringComparison.Ordinal));

        Assert.True(runtimes.Count == 0, string.Join("\n", runtimes));
    }
}
