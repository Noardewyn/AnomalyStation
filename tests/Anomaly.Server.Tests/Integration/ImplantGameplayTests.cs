using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Syndicate package G3 (D316) through the real server: Bob, a traitor with a precharged uplink implant, clicks the implant's
/// action button and the Uplink window opens on his client with the implant's 10 TC; he buys the freedom implant, which lands in his
/// hand boxed; he takes the implanter out and clicks himself with it; cuffed, he clicks the freedom implant's button: the cuffs
/// fall, his client stops drawing them on his body, and the implant's last use leaves him one fewer charge.
/// </summary>
public class ImplantGameplayTests(ITestOutputHelper output)
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

    private static ClickTarget Screen(long id) => new(TargetKind.Screen, id, 0, 0, 0, 16, 16);

    private static UiWindowState Needed(RealHeadlessWorld w, RealHeadlessWorld.Player p, string @interface)
    {
        UiWindowState window = p.Session.Replica.UiWindows.Values.SingleOrDefault(v => v.Interface == @interface);
        Assert.True(window != null, "no " + @interface + ": " + string.Join(", ", p.Session.Replica.UiWindows.Values.Select(v => v.Interface)) + "\n" + w.Err);
        return window;
    }

    private static long ButtonOf(RealHeadlessWorld w, mob_living_carbon_human me, obj_item item)
        => In(w, () => ((atom)Assert.Single(Dm.each_cast<datum_action>(item.actions)).viewers[me.hud_used]).dm_id);

    private static bool DrawsCuffs(ReplicaWorld replica)
    {
        Assert.True(replica.TryGet(replica.OwnEntity, out ReplicaEntity own));
        return CrewOutfitTests.Icons(replica, own.State.Appearance).Any(i => i.State == "handcuff1");
    }

    [Fact]
    public void A_traitor_opens_his_implant_uplink_buys_the_freedom_implant_implants_it_and_slips_the_cuffs()
    {
        using var w = new RealHeadlessWorld(config: new ServerConfig
        {
            Seed = 13, RoomWidth = 16, RoomHeight = 16, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f, DevCommands = false,
        }, strict: false);
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var texts = new List<string>();
        bob.Client.ServerTextReceived += t => texts.Add(t.Html);
        ReplicaWorld replica = bob.Session.Replica;
        w.Run(4 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        var me = (mob_living_carbon_human)w.MobOf(bob);

        // a traitor, and a precharged uplink implant in him (tg's implanter, used on himself)
        obj_item_implant_uplink uplinkImplant = In(w, () =>
        {
            Waiting.NoSleep(me.mind.add_antag_datum(typeof(datum_antagonist_traitor)));
            var implanter = Dm.@new<obj_item_implanter_uplink_precharged>(me.loc);
            var imp = (obj_item_implant_uplink)implanter.imp;
            Waiting.NoSleep(imp.implant(me, me, Dm.TRUE));
            return imp;
        });
        w.Run(2 * Second);

        // ---- 1. The implant's button opens the Uplink window with its 10 TC.
        Assert.True(bob.Session.Click(Screen(ButtonOf(w, me, uplinkImplant)), ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second);
        UiWindowState uplink = Needed(w, bob, "Uplink");
        Assert.Equal(Defines.TELECRYSTALS_PRELOADED_IMPLANT, uplink.Merged.Num("telecrystals"));
        Assert.Contains(uplink.Merged.Get("uplink_json").Arr("items"), i => i.Str("id") == "/datum/uplink_item/implants/freedom");

        // ---- 2. He buys the freedom implant: its box lands in his hand.
        Assert.True(bob.Session.UiAct(uplink.Window, "buy", UiValue.ObjectOf(("path", UiValue.Of("/datum/uplink_item/implants/freedom")))));
        w.Run(2 * Second);
        Assert.Equal(Defines.TELECRYSTALS_PRELOADED_IMPLANT - 5, Needed(w, bob, "Uplink").Merged.Num("telecrystals"));
        var box = In(w, () => Dm.each<object>(me.held_items).OfType<obj_item_storage_box_syndie_kit_imp_freedom>().FirstOrDefault());
        Assert.NotNull(box);
        Assert.True(bob.Session.UiAct(Needed(w, bob, "Uplink").Window, "close"));

        // ---- 3. The implanter out of the box into his hand, and a click on himself implants it.
        obj_item_implanter_freedom implanter = In(w, () =>
        {
            var found = Dm.locate<obj_item_implanter_freedom>(box);
            Waiting.NoSleep(me.dropItemToGround(box));
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_active_hand(found))));
            return found;
        });
        w.Run(Second);
        Assert.True(bob.Session.Click(me.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second);
        var freedom = In(w, () => Dm.locate<obj_item_implant_freedom>(me.implants));
        Assert.True(freedom != null, "no freedom implant: " + string.Join(" | ", texts));
        Assert.Contains(texts, t => t.Contains("You implant yourself.", StringComparison.Ordinal));
        Assert.Equal("implanter0", In(w, () => implanter.icon_state as string));

        // ---- 4. Cuffed: his client draws the cuffs; the freedom implant's button breaks them.
        Do(w, () =>
        {
            var cuffs = Dm.@new<obj_item_restraints_handcuffs>(me.loc);
            Waiting.NoSleep(me.equip_to_slot(cuffs, Defines.ITEM_SLOT_HANDCUFFED));
        });
        w.Run(Second);
        Assert.True(DrawsCuffs(replica), "the cuffs are drawn on him");
        float uses = In(w, () => freedom.uses);
        Assert.True(bob.Session.Click(Screen(ButtonOf(w, me, freedom)), ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second);
        Assert.Null(In(w, () => me.handcuffed));
        Assert.False(DrawsCuffs(replica), "the cuffs are gone from his body");
        Assert.Equal(uses - 1, In(w, () => freedom.uses));
        output.WriteLine($"freedom implant charges left: {uses - 1}");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
