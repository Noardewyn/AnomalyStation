using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tests.Tg.Wave3.Engine.EngineTestHelpers;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave3.Engine;

/// <summary>
/// Edits of <c>contents</c> through list operators (rules 4.3: <c>Dm.append(A.contents, x)</c>, <c>Dm.remove</c>,
/// <c>A.contents = L</c>, the list procs): in BYOND <c>contents += X</c> is <c>X.loc = src</c> (move-chain.md E1). The
/// port applies such an edit at the next engine access (Builtin/atom.builtin.cs: the list tells its owner atom of every
/// write, IDmListOwner), so each test pins what the next statement of a tg proc observes: loc, contents, x/y/z, Dm.each and `in`
/// over the old and the new container. The tg idioms come from tg's own uses (turf.dm:261, storage.dm:539-545,
/// pancakes.dm:137-147, cards.dm:42, shuttle.dm:751, reader.dm:935).
/// </summary>
public class ContentsEditTests
{
    [Fact]
    public void ContentsPlusEqualsMovesTheAtomBeforeTheNextStatementReadsAnything()
    {
        var t = new EngineWorld(3, 3, 1);
        using (t.World.Enter())
        {
            turf here = locate(1, 1, 1);
            turf there = locate(3, 2, 1);
            var box = Dm.@new<obj_test_engine_box>(here);
            var other = Dm.@new<obj_test_engine_box>(here);

            Dm.append(there.contents, box); // there.contents += box
            Assert.Same(there, box.loc);
            Assert.Equal(new[] { 3f, 2f, 1f }, new[] { box.x, box.y, box.z });

            Dm.append(here.contents, box); // and back: each read below is the first engine access after the edit
            Assert.Equal(new object[] { other, box }, Items(here));
            Dm.append(there.contents, box);
            Assert.False(Dm.@in(box, here));
            Dm.append(here.contents, box);
            Assert.Equal(1f, box.x);
            Dm.append(there.contents, box);
            Assert.Same(there, get_turf(box));
            Dm.append(here.contents, box);
            Assert.Equal(new object[] { here }, Items(box.locs));
            Dm.append(there.contents, box);
            Assert.Equal(1, here.contents.Count);
            Dm.append(here.contents, box);
            Assert.Equal(here.contents.Count, Dm.length(here.contents));
            Assert.Equal(2, here.contents.Count);
            Dm.append(there.contents, box);
            Assert.Equal(new object[] { box }, Items(there)); // for(var/obj/O in there)
            Dm.append(here.contents, box);
            Assert.Null(Dm.locate<obj_test_engine_box>(there)); // locate(/obj/...) in there
            Dm.append(there.contents, box);
            Assert.Same(box, Dm.locate<obj_test_engine_box>(there));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void KnownGap_AReadOutsideAnyWorldScopeSeesAContentsEditUnapplied()
    {
        var t = new EngineWorld(2, 1, 1);
        turf here;
        turf there;
        obj_test_engine_box box;
        using (t.World.Enter())
        {
            here = locate(1, 1, 1);
            there = locate(2, 1, 1);
            box = Dm.@new<obj_test_engine_box>(here);
            Dm.append(there.contents, box); // the last statement of the tick
        }
        // Port (open issue): the engine applies list edits only from inside a world scope, so a host-side read
        // (presentation, replication, the state hasher) sees the old loc until the world runs again; BYOND has no such
        // window.
        Assert.Same(here, box.loc);
        Assert.Equal(1f, box.x);
        using (t.World.Enter())
            Assert.Same(there, box.loc);
        Assert.Same(there, box.loc);
        Assert.Equal(2f, box.x);
    }

    [Fact]
    public void ContentsMinusEqualsSendsTheAtomToNullspace()
    {
        var t = new EngineWorld(2, 2, 1);
        using (t.World.Enter())
        {
            turf here = locate(2, 2, 1);
            var a = Dm.@new<obj_test_engine_box>(here);
            var b = Dm.@new<obj_test_engine_box>(here);
            var c = Dm.@new<obj_test_engine_box>(here);

            Dm.remove(here.contents, b);
            Assert.Null(b.loc);
            Assert.Equal(new[] { 0f, 0f, 0f }, new[] { b.x, b.y, b.z });
            Assert.Equal(new object[] { a, c }, Items(here));

            Dm.remove(here.contents, Dm.list(a, c)); // a list operand removes each item
            Assert.Null(a.loc);
            Assert.Null(c.loc);
            Assert.Empty(Items(here));

            Dm.remove(here.contents, b); // not inside: nothing happens
            Assert.Null(b.loc);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ContentsPlusEqualsAListMovesEveryItemInOrder()
    {
        var t = new EngineWorld(3, 1, 1);
        using (t.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var bag = Dm.@new<obj_test_engine_box>(locate(3, 1, 1));
            var a = Dm.@new<obj_test_engine_box>(here);
            var b = Dm.@new<obj_test_engine_box>();
            var c = Dm.@new<mob_test_engine_mob>(locate(2, 1, 1));

            // bitrunning/server/obj_generation.dm:74 `bag.contents += list(...)`; mail.dm:565 `shady_mail.contents += contents`.
            Dm.append(bag.contents, Dm.list(c, a, b));
            Assert.Equal(new object[] { c, a, b }, Items(bag));
            Assert.All(new atom[] { a, b, c }, x => Assert.Same(bag, x.loc));
            Assert.Empty(Items(here));
            Assert.Empty(Items(locate(2, 1, 1)));
        }
    }

    [Fact]
    public void TheContentsOfOneAtomAddedToAnotherAllMove()
    {
        var t = new EngineWorld(2, 1, 1);
        using (t.World.Enter())
        {
            var mail = Dm.@new<obj_test_engine_box>(locate(1, 1, 1));
            var shady = Dm.@new<obj_test_engine_box>(locate(2, 1, 1));
            var a = Dm.@new<obj_test_engine_box>(mail);
            var b = Dm.@new<obj_test_engine_box>(mail);
            var c = Dm.@new<obj_test_engine_box>(mail);

            // mail.dm:565 `shady_mail.contents += contents` and supplypod.dm:304 `contents |= holder.contents`: every
            // item moves, in order, although the right operand is the source's own contents list.
            Dm.append(shady.contents, mail.contents);
            Assert.Equal(new object[] { a, b, c }, Items(shady));
            Assert.Empty(Items(mail));

            Dm.combine(mail.contents, shady.contents);
            Assert.Equal(new object[] { a, b, c }, Items(mail));
            Assert.Empty(Items(shady));
            Assert.All(new atom[] { a, b, c }, x => Assert.Same(mail, x.loc));
        }
    }

    [Fact]
    public void AddingAnAtomThatIsAlreadyInsideKeepsItsPlace()
    {
        var t = new EngineWorld(1, 1, 1);
        using (t.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var a = Dm.@new<obj_test_engine_box>(here);
            var b = Dm.@new<obj_test_engine_box>(here);

            Dm.append(here.contents, a); // X.loc = its own loc: a no-op, so a is not moved to the end nor listed twice
            Assert.Equal(new object[] { a, b }, Items(here));
            Assert.Equal(2, here.contents.Count);
        }
    }

    [Fact]
    public void AnAreaGivenATurfItHoldsAmongNewOnesKeepsItsOrderAndTakesTheNewOnes()
    {
        // reader.dm:935 `area_instance.contents += crds` for every tile of a template: a template's space tiles over the reserved
        // level's space give the space area turfs it already holds (syndicate N1b: the base's load rebuilt a whole level's list for
        // each, a fast path drops the second copy instead), between turfs coming from another area
        var t = new EngineWorld(4, 1, 1);
        using (t.World.Enter())
        {
            area room = (area)locate(1, 1, 1).loc;
            var hall = Dm.@new<area_test_engine_hall>();
            hall.contents = Dm.list(locate(3, 1, 1), locate(4, 1, 1));
            Assert.Equal(new object[] { locate(1, 1, 1), locate(2, 1, 1) }, Items(room));

            Dm.append(room.contents, locate(1, 1, 1)); // already inside
            Dm.append(room.contents, locate(3, 1, 1)); // from the hall
            Dm.append(room.contents, locate(2, 1, 1)); // already inside
            Assert.Equal(new object[] { locate(1, 1, 1), locate(2, 1, 1), locate(3, 1, 1) }, Items(room));
            Assert.Equal(3, room.contents.Count);
            Assert.Same(room, locate(3, 1, 1).loc);
            Assert.Equal(new object[] { locate(4, 1, 1) }, Items(hall));
            Assert.Same(room, locate(1, 1, 1).loc);
        }
    }

    [Fact]
    public void TheContentsListProcsMoveAtomsToo()
    {
        var t = new EngineWorld(3, 1, 1);
        using (t.World.Enter())
        {
            var spawner = Dm.@new<obj_test_engine_box>(locate(1, 1, 1));
            var user = Dm.@new<mob_test_engine_mob>(locate(2, 1, 1));
            var stack = Dm.@new<obj_test_engine_box>(locate(3, 1, 1));
            var p1 = Dm.@new<obj_test_engine_box>(stack);
            var p2 = Dm.@new<obj_test_engine_box>(stack);

            spawner.contents.Add(user); // structures/spawner.dm:214 `contents.Add(user)`
            Assert.Same(spawner, user.loc);
            Assert.Empty(Items(locate(2, 1, 1)));

            stack.contents.Cut(); // pancakes.dm:147 `pancake.contents.Cut()`: everything left goes to nullspace
            Assert.Null(p1.loc);
            Assert.Null(p2.loc);

            var hall = Dm.@new<area_test_engine_hall>();
            hall.contents.Add(locate(3, 1, 1)); // modules/mapping/reader.dm:935 `area_instance.contents.Add(crds)`
            Assert.Same(hall, locate(3, 1, 1).loc);
            Assert.Equal(3f, hall.x);
        }
    }

    [Fact]
    public void AssigningContentsMakesTheListTheContents()
    {
        var t = new EngineWorld(2, 1, 1);
        using (t.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var a = Dm.@new<obj_test_engine_box>(here);
            var b = Dm.@new<obj_test_engine_box>(here);
            var c = Dm.@new<obj_test_engine_box>(here);
            var newcomer = Dm.@new<obj_test_engine_box>(locate(2, 1, 1));

            here.contents = Dm.list(c, newcomer, a); // b is not in the list: it leaves to nullspace
            Assert.Equal(new object[] { c, newcomer, a }, Items(here));
            Assert.Null(b.loc);
            Assert.Same(here, newcomer.loc);
            Assert.Empty(Items(locate(2, 1, 1)));

            var bot = Dm.@new<obj_test_engine_box>(locate(2, 1, 1));
            var part = Dm.@new<obj_test_engine_box>(bot);
            bot.contents = Dm.list(); // _bots.dm:550 `item_to_drop.contents = list()`
            Assert.Null(part.loc);
            Assert.Empty(Items(bot));
        }
    }

    [Fact]
    public void TgQuirk_ShufflingByAssigningContentsReordersIt()
    {
        var t = new EngineWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var deck = Dm.@new<obj_test_engine_box>(locate(1, 1, 1));
            var c1 = Dm.@new<obj_test_engine_box>(deck);
            var c2 = Dm.@new<obj_test_engine_box>(deck);
            var c3 = Dm.@new<obj_test_engine_box>(deck);

            // storage/subtypes/others/cards.dm:42 `real_location.contents = shuffle(real_location.contents)` and tcg.dm:302:
            // tg reorders a deck by assigning a permutation of its own contents. Here a fixed permutation.
            deck.contents = Dm.list(c3, c1, c2);
            Assert.Equal(new object[] { c3, c1, c2 }, Items(deck));
            Assert.All(new atom[] { c1, c2, c3 }, x => Assert.Same(deck, x.loc));
            c1.loc = locate(1, 1, 1);
            c1.loc = deck; // later arrivals go after the new order
            Assert.Equal(new object[] { c3, c2, c1 }, Items(deck));
        }
    }

    [Fact]
    public void TgQuirk_ChangeAreaMovesTheTurfWithAreaContentsPlusEquals()
    {
        var t = new EngineWorld(3, 2, 1);
        using (t.World.Enter())
        {
            turf src = locate(2, 1, 1);
            area old_area = (area)src.loc;
            var new_area = Dm.@new<area_test_engine_hall>();
            var box = Dm.@new<obj_test_engine_box>(src);

            // turf.dm:261 /turf/proc/change_area: `new_area.contents += src`, then signals whose handlers read src.loc and
            // the two areas' contents.
            Dm.append(new_area.contents, src);
            Assert.Same(new_area, src.loc);
            Assert.False(Dm.@in(src, old_area));
            Assert.Equal(new object[] { src }, Items(new_area));
            Assert.Equal(5, old_area.contents.Count);
            Assert.Equal(new[] { 2f, 1f, 1f }, new[] { src.x, src.y, src.z }); // the turf stays in its cell
            Assert.Same(src, locate(2, 1, 1));
            Assert.Same(new_area, get_area(box));
            Assert.Equal(new[] { 2f, 1f, 1f }, new[] { new_area.x, new_area.y, new_area.z });
            Assert.Equal(new object[] { box }, Items(src)); // the turf's own contents stay

            Dm.append(old_area.contents, src); // and back: appended at the end of the old area's turfs
            Assert.Same(old_area, src.loc);
            Assert.Same(src, old_area.contents[6f]);
            Assert.Empty(Items(new_area));
        }
    }

    [Fact]
    public void AssigningAnAreaAListOfTurfsMovesThemIntoIt()
    {
        var t = new EngineWorld(3, 1, 1);
        using (t.World.Enter())
        {
            area room = (area)locate(1, 1, 1).loc;
            var shuttle = Dm.@new<area_test_engine_hall>();

            // controllers/subsystem/shuttle.dm:751 `new_area.contents = proposal.reserved_turfs`
            shuttle.contents = Dm.list(locate(3, 1, 1), locate(2, 1, 1));
            Assert.Equal(new object[] { locate(3, 1, 1), locate(2, 1, 1) }, Items(shuttle));
            Assert.Same(shuttle, locate(2, 1, 1).loc);
            Assert.Equal(new object[] { locate(1, 1, 1) }, Items(room));
        }
    }

    [Fact]
    public void TgQuirk_StorageReordersAnItemThroughFourContentsEdits()
    {
        var t = new EngineWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var real_location = Dm.@new<obj_test_engine_box>(locate(1, 1, 1));
            var i1 = Dm.@new<obj_test_engine_box>(real_location);
            var i2 = Dm.@new<obj_test_engine_box>(real_location);
            var i3 = Dm.@new<obj_test_engine_box>(real_location);
            var i4 = Dm.@new<obj_test_engine_box>(real_location);
            obj target = i4;
            obj dropped_onto = i2;

            // datums/storage/storage.dm:539-545 (dropping target onto dropped_onto), statement by statement:
            float drop_index = real_location.contents.Find(dropped_onto);
            Dm.remove(real_location.contents, target);
            // Use an empty list if we're dropping onto the last item
            DmList to_move = real_location.contents.len >= drop_index ? real_location.contents.Copy(drop_index) : Dm.list();
            Assert.Equal(new object[] { i2, i3 }, Items(to_move)); // target already left the list it copies
            Dm.remove(real_location.contents, to_move);
            Dm.append(real_location.contents, target);
            Dm.append(real_location.contents, to_move);

            Assert.Equal(new object[] { i1, i4, i2, i3 }, Items(real_location));
            Assert.All(new atom[] { i1, i2, i3, i4 }, x => Assert.Same(real_location, x.loc));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TgQuirk_APancakeStackTakesTheNestedPancakesAndCutsTheEmptyList()
    {
        var t = new EngineWorld(2, 1, 1);
        using (t.World.Enter())
        {
            var src = Dm.@new<obj_test_engine_box>(locate(1, 1, 1));
            var tool = Dm.@new<obj_test_engine_box>(locate(2, 1, 1));
            var p1 = Dm.@new<obj_test_engine_box>(tool);
            var p2 = Dm.@new<obj_test_engine_box>(tool);

            // food/pancakes.dm:132-147 after transferItemToLoc(pancake, src):
            tool.loc = src;
            obj pancake = tool;
            Dm.append(src.contents, pancake); // already inside: no-op
            if (Dm.truthy(pancake.contents.len))
            {
                foreach (object pancake_content in Dm.each(pancake.contents))
                {
                    pancake = (obj)pancake_content;
                    Dm.append(src.contents, pancake);
                }
            }
            pancake = tool;
            pancake.contents.Cut();

            Assert.Equal(new object[] { tool, p1, p2 }, Items(src));
            Assert.Same(src, p1.loc);
            Assert.Same(src, p2.loc);
            Assert.Empty(Items(tool));
        }
    }

    [Fact]
    public void ForbiddenAdditionsAreDroppedAndLeaveTheAtomWhereItWas()
    {
        var t = new EngineWorld(2, 1, 1);
        using (t.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var box = Dm.@new<obj_test_engine_box>(here);
            var inner = Dm.@new<obj_test_engine_box>(box);

            Dm.append(box.contents, box); // itself
            Dm.append(inner.contents, box); // its own container: cyclic
            Dm.append(box.contents, here); // a turf into a movable
            Dm.append(here.contents, locate(2, 1, 1)); // a turf into a turf
            Dm.append(here.contents, here.loc); // an area into anything

            Assert.Same(here, box.loc);
            Assert.Same(box, inner.loc);
            Assert.Equal(new object[] { inner }, Items(box));
            Assert.Empty(Items(inner));
            Assert.Equal(new object[] { box }, Items(here));
            Assert.IsType<area_test_engine_room>(locate(2, 1, 1).loc);
            Assert.Null(here.loc.loc);
            Assert.Empty(t.Runtimes); // refused moves are silent, as a refused loc assignment is
        }
    }

    [Fact]
    public void ANonAtomInContentsIsARuntimeAndIsDropped()
    {
        var t = new EngineWorld(1, 1, 1);
        using (t.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var box = Dm.@new<obj_test_engine_box>(here);
            Dm.append(here.contents, "a string"); // BYOND: contents hold only atoms
            Assert.Equal(new object[] { box }, Items(here));
            Assert.Single(t.Runtimes);
        }
    }

    [Fact]
    public void AnEditThroughAKeptContentsReferenceIsSeenByTheNextEngineRead()
    {
        var t = new EngineWorld(2, 1, 1);
        using (t.World.Enter())
        {
            var bag = Dm.@new<obj_test_engine_box>(locate(1, 1, 1));
            var item = Dm.@new<obj_test_engine_box>(locate(2, 1, 1));

            DmList L = bag.contents; // var/list/L = bag.contents
            Dm.append(ref L, item); // L += item
            Assert.Same(bag, item.loc);
            Assert.Empty(Items(locate(2, 1, 1)));
        }
    }

    [Fact]
    public void AnEditThroughAContentsReferenceKeptAcrossAnEngineReadMovesTheAtom()
    {
        var t = new EngineWorld(2, 1, 1);
        using (t.World.Enter())
        {
            var bag = Dm.@new<obj_test_engine_box>(locate(1, 1, 1));
            var item = Dm.@new<obj_test_engine_box>(locate(2, 1, 1));

            DmList L = bag.contents; // var/list/L = bag.contents
            Assert.Same(locate(2, 1, 1), item.loc); // any engine access in between
            Dm.append(ref L, item); // L += item: in BYOND item.loc == bag from here on

            Assert.Same(bag, item.loc);
            Assert.Empty(Items(locate(2, 1, 1)));
            Assert.Equal(new object[] { item }, Items(bag));
        }
    }

    [Fact]
    public void ContentsPlusEqualsNewMovesTheNewAtomEvenWhenItsNewReadsTheEngine()
    {
        var t = new EngineWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var sec_briefcase = Dm.@new<obj_test_engine_box>(locate(1, 1, 1));

            // clothing/outfits/standard.dm:220-225 `sec_briefcase.contents += new /obj/item/...` (five times), and
            // bitrunning/server/obj_generation.dm:74 `bag.contents += list(new ..., new ..., new ...)`. In C# the left
            // operand (the contents getter) is evaluated before `new`, and the new atom's New reads the engine (here its
            // loc; tg's /atom/New -> Initialize always does) before the append runs. BYOND: each item is in the
            // briefcase after its statement; the list tells the engine of the append (IDmListOwner).
            obj_test_engine_box cash = null;
            Dm.append(sec_briefcase.contents, cash = Dm.@new<obj_test_engine_box>());
            Assert.Same(sec_briefcase, cash.loc);
            obj_test_engine_box x4 = null;
            Dm.append(sec_briefcase.contents, x4 = Dm.@new<obj_test_engine_box>());
            Assert.Same(sec_briefcase, x4.loc);
            Assert.Same(locate(1, 1, 1), get_turf(x4));
            Assert.Equal(new object[] { cash, x4 }, Items(sec_briefcase));
        }
    }

    [Fact]
    public void KnownGap_AddingAndRemovingThroughAKeptReferenceLeavesTheAtomWhereItWas()
    {
        var t = new EngineWorld(2, 1, 1);
        using (t.World.Enter())
        {
            var bag = Dm.@new<obj_test_engine_box>(locate(1, 1, 1));
            var item = Dm.@new<obj_test_engine_box>(locate(2, 1, 1));

            DmList L = bag.contents;
            Dm.append(ref L, item); // BYOND: item.loc = bag
            Dm.remove(ref L, item); // BYOND: item.loc = null
            // Port (open issue): the engine applies the net difference of the edits made between two engine accesses
            // (IDmListOwner marks, MapGrid.Sync applies), so the item never left its turf; BYOND leaves it in nullspace.
            Assert.Same(locate(2, 1, 1), item.loc);
        }
    }

    [Fact]
    public void KnownGap_AStateHashRightAfterAContentsEditHashesTheEditUnapplied()
    {
        static ulong Build(bool byContents, bool readAfter)
        {
            var t = new EngineWorld(2, 1, 1, seed: 21);
            using (t.World.Enter())
            {
                var box = Dm.@new<obj_test_engine_box>(locate(1, 1, 1));
                if (byContents)
                    Dm.append(locate(2, 1, 1).contents, box);
                else
                    box.loc = locate(2, 1, 1);
                if (readAfter)
                    Assert.Same(locate(2, 1, 1), box.loc);
            }
            return t.World.ComputeStateHash();
        }

        // The same world state reached by `box.loc = T` and by `T.contents += box` hashes alike once anything has read
        // the engine after the edit...
        Assert.Equal(Build(byContents: false, readAfter: true), Build(byContents: true, readAfter: true));
        // ...but a hash taken right after the edit (the hasher reads the backing fields, outside any world scope) sees
        // box still on its old turf and in both contents lists (open issue).
        Assert.NotEqual(Build(byContents: false, readAfter: false), Build(byContents: true, readAfter: false));
        Assert.Equal(Build(byContents: true, readAfter: false), Build(byContents: true, readAfter: false)); // still deterministic
    }
}
