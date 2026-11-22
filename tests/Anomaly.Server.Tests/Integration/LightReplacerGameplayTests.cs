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
/// M7 power helpers slice (D164): tg's light replacer (lightreplacer.dm) through the real server on MetaStation. An engineer
/// stands under a lit fixture with a light replacer and a spare glass sheet: the replacer is refilled by a click on the sheet
/// (5 lights from a sheet, glass.dm:53), then the fixture is broken (tg's break_light_tube): his client sees it dark and drawn
/// broken. A click on it with the replacer fits a new light: his client sees the fixture lit again with its tube picture and
/// hears tg's click (sound/machines/click.ogg); one light is used and the broken tube is a fragment in the replacer. Four
/// fragments (shards fed to the replacer) make a bulb with tg's ding.
/// </summary>
public class LightReplacerGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    [MetaServerFact]
    public void A_light_replacer_fits_a_new_light_in_a_broken_fixture_and_the_client_sees_it_lit_and_hears_the_click()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 93, TimeoutSteps = 12000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Janitor");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);

        // a lit tube fixture on the station floor
        obj_machinery_light lamp = In(w, () => Dm.each<obj_machinery_light>(Dm.world).OrderBy(l => l.dm_id)
            .First(l => Dm.eq(l.z, 1) && l.light_type == typeof(obj_item_light_tube) && Dm.truthy(l.on) && Dm.eq(l.status, LIGHT_OK) && l.loc is turf_open));
        int x = In(w, () => (int)lamp.x), y = In(w, () => (int)lamp.y), z = In(w, () => (int)lamp.z);
        output.WriteLine($"the fixture at {x},{y}");
        In(w, () => Waiting.NoSleep(me.forceMove(locate(x, y, z))));
        var lr = In(w, () => Dm.@new<obj_item_lightreplacer>(me.loc));
        In(w, () => Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_active_hand(lr)))));
        var glass = In(w, () => (obj_item_stack_sheet_glass)Dm.@new(typeof(obj_item_stack_sheet_glass), me.loc, 2));
        w.Run(4 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        ReplicaEntity Seen() => replica.Entities.FirstOrDefault(e => e.Id == lamp.dm_id);
        string IconState()
        {
            ReplicaEntity seen = Seen();
            return seen != null && replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? replica.ResolveString(look.IconState) : null;
        }
        Assert.True(Seen().Light.On, "the fixture is lit");
        string litIcon = IconState();

        // the replacer takes a glass sheet from the floor: five lights from it, the sheet is used up
        In(w, () => lr.uses = 3);
        Assert.True(p.Session.Click(glass.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Equal(8f, In(w, () => lr.uses));
        Assert.Equal(1f, In(w, () => Dm.num(glass.amount)));

        // the fixture broken: dark, drawn broken
        In(w, () => Waiting.NoSleep(lamp.break_light_tube(TRUE)));
        w.Run(Second);
        Assert.False(Seen().Light.On, "the broken fixture is dark");
        Assert.NotEqual(litIcon, IconState());
        output.WriteLine($"lit {litIcon}, broken {IconState()}");
        heard.Clear();

        // the click: a new light, lit again, tg's click
        Assert.True(p.Session.Click(lamp.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        In(w, () =>
        {
            Assert.Equal(LIGHT_OK, Dm.num(lamp.status));
            Assert.Equal(7f, lr.uses);
            Assert.Equal(1f, lr.bulb_shards);
        });
        Assert.True(Seen().Light.On, "the client sees the fixture lit again");
        Assert.Equal(litIcon, IconState());
        Assert.Contains(heard, e => e.File == "sound/machines/click.ogg");

        // two more fragments (shards clicked with the replacer) and the third makes four: a bulb and tg's ding
        for (int i = 0; i < 2; i++)
        {
            var shard = In(w, () => Dm.@new<obj_item_shard>(me.loc));
            Assert.True(p.Session.Click(shard.dm_id, ClickButton.Left, ClickModifiers.None));
            w.Run(Second);
        }
        In(w, () => Assert.Equal((7f, 3f), (lr.uses, lr.bulb_shards)));
        Assert.DoesNotContain(heard, e => e.File == "sound/machines/ding.ogg");
        var last = In(w, () => Dm.@new<obj_item_shard>(me.loc));
        Assert.True(p.Session.Click(last.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        In(w, () => Assert.Equal((8f, 0f), (lr.uses, lr.bulb_shards)));
        Assert.Contains(heard, e => e.File == "sound/machines/ding.ogg");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
