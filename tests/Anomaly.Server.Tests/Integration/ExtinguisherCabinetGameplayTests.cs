using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// A regression check (the owner's report of 2026-10-19 that a fire extinguisher could not be put back did not reproduce). Through the real server on MetaStation a player
/// takes the extinguisher out of a mapped cabinet with an empty hand (attack_hand, the cabinet opens) and puts it back with a click of the held
/// extinguisher (item_interaction: transferItemToLoc, "extinguisher stored").
/// </summary>
public class ExtinguisherCabinetGameplayTests
{
    private const int Second = 20;
    private readonly ITestOutputHelper _output;

    public ExtinguisherCabinetGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    [MetaServerFact]
    public void A_mapped_cabinets_extinguisher_is_taken_out_and_put_back()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        int bootRuntimes = w.World.RuntimeErrorCount;
        int z = In(w, () => (int)me.z);
        var cabinet = In(w, () =>
        {
            for (int x = 1; x <= 255; x++)
                for (int y = 1; y <= 255; y++)
                {
                    if (locate(x, y, z) is not turf_open open)
                        continue;
                    var found = Dm.each_cast<atom>(open.contents).OfType<obj_structure_extinguisher_cabinet>().FirstOrDefault(c => c.stored_extinguisher != null);
                    if (found != null)
                        return found;
                }
            return null;
        });
        Assert.NotNull(cabinet);
        In(w, () => Waiting.NoSleep(me.forceMove(cabinet.loc)));
        w.Run(Second);
        _output.WriteLine($"the cabinet at {In(w, () => $"{cabinet.x},{cabinet.y} dir {cabinet.dir} opened {cabinet.opened}")}");
        obj_item_extinguisher stored = In(w, () => cabinet.stored_extinguisher);

        Assert.True(p.Session.Click(cabinet.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Same(stored, In(w, () => me.get_active_held_item()));
        Assert.Null(In(w, () => cabinet.stored_extinguisher));
        Assert.Equal(1f, In(w, () => cabinet.opened));

        Assert.True(p.Session.Click(cabinet.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Same(cabinet, In(w, () => stored.loc));
        Assert.Same(stored, In(w, () => cabinet.stored_extinguisher));
        Assert.Null(In(w, () => me.get_active_held_item()));

        // Again from the tile beside, with the safety off (Z, attack_self): the cabinet's item_interaction still comes before the spray.
        turf beside = In(w, () => Dm.each_cast<turf>(Builtins.orange(1, cabinet.loc)).OfType<turf_open>().First(t => !Dm.truthy(t.density) && Dm.each_cast<atom>(t.contents).All(a => !Dm.truthy(a.density))));
        In(w, () => Waiting.NoSleep(me.forceMove(beside)));
        w.Run(Second);
        Assert.True(p.Session.Click(cabinet.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Same(stored, In(w, () => me.get_active_held_item()));
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(5);
        Assert.Equal(0f, In(w, () => stored.safety));
        Assert.True(p.Session.Click(cabinet.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Same(stored, In(w, () => cabinet.stored_extinguisher));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
