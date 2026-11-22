using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.T2;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.MultiZ;

/// <summary>
/// D325: tg's disposal system (code/modules/recycling/disposal) on the multi-z fixture (two linked levels, a 3 x 3 room on each from
/// (2, 2) to (4, 4)): a bin flushes what is in it through its trunk and the pipes to an outlet, which throws it out; a multi-z trunk
/// carries the holder to the trunk on the level above.
/// </summary>
public class DisposalTests
{
    private static World Boot(out TgTestWorld.Log log) => T2Maps.Boot("multiz", out log);

    // A trunk at (x, y, z) pointing dir, then the machine on it.
    private static T Trunked<T>(Type trunk, int x, int y, int z, float dir) where T : atom
    {
        var pipe = (obj_structure_disposalpipe)Dm.@new(trunk, locate(x, y, z));
        Waiting.NoSleep(pipe.setDir(dir));
        pipe.dpdir = dir; // a trunk's one direction is its dir (pipe.dm Initialize reads dir before setDir here)
        return Dm.@new<T>(locate(x, y, z));
    }

    private static void Segment(int x, int y, int z, float dir)
    {
        var pipe = Dm.@new<obj_structure_disposalpipe_segment>(locate(x, y, z));
        Waiting.NoSleep(pipe.setDir(dir));
        pipe.dpdir = Dm.num(Dm.bitor(dir, REVERSE_DIR(dir)));
    }

    [Fact]
    public void ABinFlushesItsContentsThroughThePipesAndTheOutletThrowsThemOut()
    {
        World world = Boot(out TgTestWorld.Log log);
        obj_machinery_disposal_bin bin;
        obj_structure_disposaloutlet outlet;
        obj_item_wrench wrench;
        using (world.Enter())
        {
            bin = Trunked<obj_machinery_disposal_bin>(typeof(obj_structure_disposalpipe_trunk), 2, 2, 1, EAST);
            Segment(3, 2, 1, EAST);
            outlet = Trunked<obj_structure_disposaloutlet>(typeof(obj_structure_disposalpipe_trunk), 4, 2, 1, WEST);
            Assert.NotNull(bin.trunk);
            Assert.NotNull(outlet.trunk);
            wrench = Dm.@new<obj_item_wrench>(bin);
        }
        world.Step(2);
        using (world.Enter())
            Waiting.Detach(bin.flush__proc());
        bool inTransit = false;
        for (int i = 0; i < 140; i++) // the flush's 1.5 s, the pipes, then the outlet's 2 s before it opens
        {
            world.Step(1);
            using (world.Enter())
                inTransit |= wrench.loc is obj_structure_disposalholder { loc: obj_structure_disposalpipe };
        }
        Assert.True(inTransit, "the wrench rode a holder through the pipes");
        using (world.Enter())
        {
            Assert.IsAssignableFrom<turf>(wrench.loc);
            Assert.Equal(1f, wrench.z);
            Assert.NotSame(bin, wrench.loc);
            Assert.Empty(log.Runtimes);
        }
    }

    [Fact]
    public void AMultiZTrunkCarriesTheHolderToTheTrunkOnTheLevelAbove()
    {
        World world = Boot(out TgTestWorld.Log log);
        obj_machinery_disposal_bin bin;
        obj_item_wrench wrench;
        using (world.Enter())
        {
            bin = Trunked<obj_machinery_disposal_bin>(typeof(obj_structure_disposalpipe_trunk), 2, 4, 1, EAST);
            Segment(3, 4, 1, EAST);
            var up = Dm.@new<obj_structure_disposalpipe_trunk_multiz>(locate(4, 4, 1));
            Waiting.NoSleep(up.setDir(WEST));
            up.dpdir = WEST;
            var down = Dm.@new<obj_structure_disposalpipe_trunk_multiz_down>(locate(4, 4, 2));
            Waiting.NoSleep(down.setDir(WEST));
            down.dpdir = WEST;
            Segment(3, 4, 2, EAST);
            Trunked<obj_structure_disposaloutlet>(typeof(obj_structure_disposalpipe_trunk), 2, 4, 2, EAST);
            wrench = Dm.@new<obj_item_wrench>(bin);
        }
        world.Step(2);
        using (world.Enter())
            Waiting.Detach(bin.flush__proc());
        world.Step(140);
        using (world.Enter())
        {
            Assert.IsAssignableFrom<turf>(wrench.loc);
            Assert.Equal(2f, wrench.z); // out of the outlet on the level above
            Assert.Empty(log.Runtimes);
        }
    }

    [Fact]
    public void AWrappedAndTaggedParcelIsSortedAtTheMailJunctionAndAnUntaggedThingGoesStraightOn()
    {
        World world = Boot(out TgTestWorld.Log log);
        obj_machinery_disposal_bin bin;
        obj_structure_disposalpipe_trunk sortedTrunk, straightTrunk;
        obj_item_delivery parcel;
        obj_item_screwdriver loose;
        using (world.Enter())
        {
            // the bin at (2, 3) feeds a mail junction at (3, 3) flowing east; its sorting side is south, to a trunk at (3, 2)
            bin = Trunked<obj_machinery_disposal_bin>(typeof(obj_structure_disposalpipe_trunk), 2, 3, 1, EAST);
            var junction = Dm.@new<obj_structure_disposalpipe_sorting_mail>(locate(3, 3, 1));
            Waiting.NoSleep(junction.setDir(EAST));
            junction.dpdir = Dm.num(Dm.bitor(Dm.bitor(EAST, SOUTH), WEST));
            junction.sortTypes = Dm.list(5f);
            Trunked<obj_structure_disposaloutlet>(typeof(obj_structure_disposalpipe_trunk), 3, 2, 1, NORTH);
            Trunked<obj_structure_disposaloutlet>(typeof(obj_structure_disposalpipe_trunk), 4, 3, 1, WEST);
            sortedTrunk = Dm.locate<obj_structure_disposalpipe_trunk>(locate(3, 2, 1));
            straightTrunk = Dm.locate<obj_structure_disposalpipe_trunk>(locate(4, 3, 1));

            // a clerk wraps a wrench with the package wrapper and tags the parcel for destination 5
            mob_living_carbon_human clerk = Dm.@new<mob_living_carbon_human>(locate(2, 4, 1));
            var wrench = Dm.@new<obj_item_wrench>(locate(2, 4, 1));
            var wrapper = Dm.@new<obj_item_stack_package_wrap>(locate(2, 4, 1));
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(wrapper.interact_with_atom(wrench, clerk, null)));
            parcel = wrench.loc as obj_item_delivery_small;
            Assert.NotNull(parcel);
            // wrap.dm:208: the wrapping goes into the clerk's attack log (mob.dm:14's individual log)
            var attackLog = clerk.logging[num2text(LOG_ATTACK, MAX_BITFLAG_DIGITS)] as DmList;
            Assert.NotNull(attackLog);
            Assert.Contains("has used " + wrapper.name + " on", Dm.text("[]", attackLog[attackLog[1]]));
            var tagger = Dm.@new<obj_item_dest_tagger>(locate(2, 4, 1));
            tagger.currTag = 5;
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(parcel.item_interaction(clerk, tagger, null)));
            Assert.Equal(5f, parcel.sort_tag);
            Waiting.NoSleep(parcel.forceMove(bin));
            loose = Dm.@new<obj_item_screwdriver>(locate(2, 2, 1)); // flushed alone afterwards
        }
        world.Step(2);
        using (world.Enter())
            Waiting.Detach(bin.flush__proc());
        // the holder carries the parcel's tag (COMSIG_MOVABLE_DISPOSING), so the junction sends everything in it south
        bool sorted = false;
        var trace = new System.Text.StringBuilder();
        for (int i = 0; i < 40; i++)
        {
            world.Step(1);
            using (world.Enter())
            {
                sorted |= parcel.loc is obj_structure_disposalholder { loc: var at } && at == sortedTrunk;
                trace.Append(i + ":" + parcel.loc?.GetType().Name + "/" + parcel.loc?.loc?.GetType().Name + "@" + parcel.x + "," + parcel.y
                    + (parcel.loc is obj_structure_disposalholder h ? " tag " + h.destinationTag + " dir " + h.dir : "") + "; ");
            }
        }
        Assert.True(sorted, "the tagged parcel went down the sorting side: " + trace);

        // an untagged thing alone goes straight on east
        using (world.Enter())
        {
            Waiting.NoSleep(loose.forceMove(bin));
            Waiting.Detach(bin.flush__proc());
        }
        bool straight = false;
        for (int i = 0; i < 40; i++)
        {
            world.Step(1);
            using (world.Enter())
                straight |= loose.loc is obj_structure_disposalholder { loc: var at } && at == straightTrunk;
        }
        Assert.True(straight, "the untagged thing went straight on");
        using (world.Enter())
            Assert.Empty(log.Runtimes);
    }
}
