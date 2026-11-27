using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Through the real server: a joined chaplain strikes the altar of the gods with his bible, picks the maintenance god in the
/// ReligiousTool window and raises its totem with a rite from the same window; a joined chaplain heals a wounded player by
/// striking her with his bible.
/// </summary>
public class ChapelGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action f)
    {
        using (w.World.Enter())
            f();
    }

    private static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    private static mob_living_carbon_human MakeChaplain(RealHeadlessWorld w, RealHeadlessWorld.Player p)
    {
        var me = (mob_living_carbon_human)w.MobOf(p);
        Do(w, () => Waiting.Detach(SSjob.equip_rank(me, SSjob.get_job_type(typeof(datum_job_chaplain)) as datum_job, me.client)));
        w.Run(Second / 2);
        Assert.Equal(HOLY_ROLE_HIGHPRIEST, In(w, () => Dm.num(me.mind.holy_role)));
        return me;
    }

    private static obj_item_book_bible BibleInHand(RealHeadlessWorld w, mob_living_carbon_human me)
    {
        var bible = In(w, () => Dm.each<obj_item_book_bible>((me.get_all_contents() as DmList)).First());
        Do(w, () =>
        {
            Waiting.NoSleep(me.drop_all_held_items());
            Waiting.NoSleep(me.put_in_active_hand(bible));
        });
        w.Run(5);
        Assert.Same(bible, In(w, () => me.get_active_held_item()));
        return bible;
    }

    [Fact]
    public void A_chaplain_picks_a_sect_at_the_altar_and_performs_a_rite()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 14051, TimeoutSteps = 12000 });
        RealHeadlessWorld.Player p = w.Join("Chaplain");
        ReplicaWorld replica = p.Session.Replica;
        w.Run(Second);
        var me = MakeChaplain(w, p);
        int bootRuntimes = w.World.RuntimeErrorCount;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        BibleInHand(w, me);
        var altar = In(w, () => Dm.@new<obj_structure_altar_of_gods>(get_step(me, EAST)));
        w.Run(Second);

        // the bible on the altar opens the altar's window: the sects to pick from
        Assert.True(p.Session.Click(On(altar), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("ReligiousTool", window.Interface);
        UiNode content = Shown(windows, replica, window.Window);
        Assert.NotNull(content);
        output.WriteLine(content.AllText());
        Assert.Contains("Sect Select", content.AllText());
        UiNode pick = content.Walk().First(n => n.Key == "sect:/datum/religion_sect/maintenance").Walk().First(n => n.Key == "select");
        pick.OnClick();
        w.Run(Second);
        Assert.IsType<datum_religion_sect_maintenance>(In(w, () => GLOB.religious_sect));
        Assert.Equal(In(w, () => Dm.str(GLOB.religious_sect.altar_icon_state)), In(w, () => altar.icon_state));

        // the rites tab, and the totem's rite with the favour to pay for it
        Do(w, () =>
        {
            GLOB.religious_sect.set_favor(1000);
            Dm.@new<obj_item_stack_sheet_mineral_wood>(altar.loc, 5f); // the totem is carved from a plank on the altar
        });
        w.Run(Second);
        content = Shown(windows, replica, window.Window);
        content.Walk().First(n => n.Key == "tab:rites").OnClick();
        content = Shown(windows, replica, window.Window);
        output.WriteLine(content.AllText());
        UiNode rite = content.Walk().First(n => n.Key == "rite:/datum/religion_rites/ritual_totem").Walk().First(n => n.Kind == UiKind.Section);
        Assert.NotNull(rite.Buttons);
        rite.Buttons.OnClick();
        w.Run(Second);
        float length = In(w, () => Dm.initial(typeof(datum_religion_rites_ritual_totem), (datum_religion_rites r) => r.ritual_length));
        w.Run((int)(length * 2) + 2 * Second);
        var totem = In(w, () => Dm.each<obj_item_ritual_totem>(altar.loc).FirstOrDefault());
        Assert.NotNull(totem);
        Assert.True(In(w, () => GLOB.religious_sect.favor) < 1000, "the rite took its favour");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void A_chaplain_blesses_a_wounded_player_with_the_bible()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 14052, TimeoutSteps = 8000 });
        RealHeadlessWorld.Player chaplain = w.Join("Chaplain");
        RealHeadlessWorld.Player patient = w.Join("Patient");
        w.Run(Second);
        var me = MakeChaplain(w, chaplain);
        var her = (mob_living_carbon_human)w.MobOf(patient);
        int bootRuntimes = w.World.RuntimeErrorCount;
        BibleInHand(w, me);
        Do(w, () =>
        {
            Waiting.NoSleep(her.forceMove(get_step(me, EAST)));
            Waiting.NoSleep(her.apply_damage(30, BRUTE, BODY_ZONE_CHEST));
        });
        w.Run(Second);
        float before = In(w, () => Dm.num(her.get_brute_loss()));
        Assert.True(before > 0);

        // a blessing heals unless the god's smack chance (60%) hits her head instead: he strikes until one heals
        for (int strike = 0; strike < 12 && In(w, () => Dm.num(her.get_brute_loss())) >= before; strike++)
        {
            Assert.True(chaplain.Session.Click(On(her), ClickButton.Left, ClickModifiers.None));
            w.Run(Second);
        }
        Assert.True(In(w, () => Dm.num(her.get_brute_loss())) < before, "the blessing healed her");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
