using System.Collections.Frozen;
using System.Reflection;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.P4;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4aClick.P1;

/// <summary>
/// The inert guard (D132, DV27; interaction.md 7), the screentips through the seam (D125, DV28), the minimal examine (D128)
/// and tg's get_turf_pixel unit test (maptest_get_turf_pixel), M4a P1.
/// </summary>
public class GuardAndHoverTests
{
    // ---- the guard -------------------------------------------------------------------------------------------------

    [Fact]
    public void TheWalkFindsAnUndeclaredOverrideBelowTheDeclaringClassAndStopsAtADeclaration()
    {
        var room = TgToyRoom.Boot(seed: 421);
        // tg's abductor baton overrides attack_self (abductor/equipment/gear/abductor_items.dm:420), which no C# class below the baton
        // declares (the baseball bat was the example until gaps WP1 translated it; the abductors stay paused, D333).
        Assert.True(room.In(() => InertSeam.HasUndeclaredOverride(Dm.@new<obj_item_melee_baton_abductor>(locate(3, 3, 1)), "attack_self")));
        Assert.True(room.In(() => InertSeam.HasUndeclaredOverride(Dm.@new<obj_item_melee_baton_abductor_test_p1_plain>(locate(3, 3, 1)), "attack_self")));
        // A subtype that declares it: the C# method that runs is its own, so nothing below it is missing.
        Assert.False(room.In(() => InertSeam.HasUndeclaredOverride(Dm.@new<obj_item_melee_baton_abductor_test_p1_declares>(locate(3, 3, 1)), "attack_self")));
        // A plain base item: nothing of tg's own is missing for the chain's attack_self; and a name outside the list is false.
        Assert.False(room.In(() => InertSeam.HasUndeclaredOverride(Dm.@new<obj_item_test_p1_stub>(locate(3, 3, 1)), "attack_self")));
        Assert.False(room.In(() => InertSeam.HasUndeclaredOverride(Dm.@new<obj_item_melee_baton_abductor>(locate(3, 3, 1)), "Initialize")));
    }

    [Fact]
    public void AnInertItemsOwnAttackSelfIsRefusedWithoutItsParentsBehaviour()
    {
        var (room, a, sinks) = P1.Room(422);
        var baton = P1.Hold<obj_item_melee_baton_abductor>(room, a);
        int before = room.In(() => DmWorldState.Of(room.World).DeferredCount);
        P1.Click(room, a, baton, "left=1;button=left");
        Assert.Contains("inert guard: /obj/item/melee/baton/abductor attack_self", P1.Deferred(room));
        // /obj/item/attack_self's own line (interact, and its fingerprint) never ran.
        Assert.DoesNotContain("code/_onclick/other_mobs.dm:137", P1.Deferred(room));
        Assert.Contains(sinks.Lines, l => l.Contains("balloon") && l.EndsWith("not ported yet", StringComparison.Ordinal));
        Assert.True(room.In(() => DmWorldState.Of(room.World).DeferredCount) > before);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheGeneratedTableHoldsOnlyGuardedNamesAtClassesThatDoNotDeclareThem()
    {
        Type table = typeof(datum).Assembly.GetType("Anomaly.Core.Tg.UndeclaredOverrides", throwOnError: true);
        var rows = (FrozenSet<(Type Type, string Proc)>)table.GetField("Rows", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        Assert.True(rows.Count > 500, "the table covers Meta's untranslated item, structure and machine overrides"); // it shrinks as the gaps packages port
        Assert.Contains((typeof(obj_item_melee_baton_abductor), "attack_self"), (IEnumerable<(Type, string)>)rows);
        const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        foreach (var (type, proc) in rows)
        {
            Assert.Contains(proc, InertSeam.GuardedProcs);
            Assert.True(type.GetMember(proc, MemberTypes.Method, declared).Length == 0 && type.GetMember(proc + "__proc", MemberTypes.Method, declared).Length == 0,
                $"{type.Name} declares {proc} but has a row");
        }
        // A live type whose chain procs are declared has none (the airlock: attack_hand, attackby's family through M3's door).
        Assert.DoesNotContain((typeof(obj_machinery_door_airlock), "attack_hand"), (IEnumerable<(Type, string)>)rows);
    }

    [Fact]
    public void NoLivePartialStubHidesAnInheritedRoot()
    {
        // types-converter.md 5 (M4a P1): the live-partial files disable CS0114 so that a new root does not stop the build;
        // a stub still declared `virtual` under a declared root would hide it until the tool reruns.
        var stale = new List<string>();
        foreach (Type t in typeof(datum).Assembly.GetTypes())
        {
            foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!m.GetCustomAttributes(false).Any(a => a.GetType().Name == "DmGeneratedStubAttribute")) continue;
                if (!m.IsVirtual || (m.Attributes & MethodAttributes.VtableLayoutMask) != MethodAttributes.NewSlot) continue;
                Type[] sig = m.GetParameters().Select(p => p.ParameterType).ToArray();
                if (t.BaseType?.GetMethod(m.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, sig, null) is { IsVirtual: true })
                    stale.Add(t.Name + "." + m.Name);
            }
        }
        Assert.True(stale.Count == 0, "stale stubs (rerun tools/Anomaly.Tools.Types): " + string.Join(", ", stale));
    }

    // ---- screentips ------------------------------------------------------------------------------------------------

    private sealed class RecordingScreentips : IScreentipSink
    {
        public readonly List<(string Name, IReadOnlyList<ScreentipHint> Hints)> Calls = new();

        public void Screentip(client viewer, atom target, string name, IReadOnlyList<ScreentipHint> hints) => Calls.Add((name, hints));
    }

    [Fact]
    public void AHeldWelderOnAnAtomWithToolScreentipsPublishesTgsLabels()
    {
        // The airlock's own add_context (airlock.dm) is M4a P4's; tg's contextual_screentip_tools element is the same path.
        var (room, a, _) = P1.Room(423);
        var sink = new RecordingScreentips();
        ScreentipSeam.Install(room.World, sink);
        var machine = P1.At<obj_structure_test_p1_machine>(room, 6, 5);
        room.In(() => Waiting.NoSleep(machine.AddElement(typeof(datum_element_contextual_screentip_tools),
            Dm.list(Dm.kv(TOOL_WELDER, Dm.list(Dm.kv(SCREENTIP_CONTEXT_LMB, "Weld"), Dm.kv(SCREENTIP_CONTEXT_RMB, "Weld shut")))))));
        var welder = P1.Hold<obj_item_test_p1_stub>(room, a);
        room.In(() => welder.tool_behaviour = TOOL_WELDER);
        // The host's Hover (ClientSeam.Hover, P6) runs BYOND's MouseEntered; SSmouse_entered's fire publishes the screentip.
        room.Command(() => ClientSeam.Hover(a.client, machine));
        room.Step(2);
        Assert.Single(sink.Calls);
        Assert.Equal(room.In(() => machine.name), sink.Calls[0].Name);
        Assert.Equal(new[] { ("left", "Weld"), ("right", "Weld shut") }, sink.Calls[0].Hints.Select(h => (h.Button, h.Label)).ToArray());
        room.AssertNoRuntimes();
    }

    [Fact]
    public void WithoutContextTheScreentipIsTheNameAlone()
    {
        var (room, a, _) = P1.Room(424);
        var sink = new RecordingScreentips();
        ScreentipSeam.Install(room.World, sink);
        var machine = P1.At<obj_structure_test_p1_machine>(room, 6, 5);
        room.Command(() => ClientSeam.Hover(a.client, machine));
        room.Step(2);
        Assert.Single(sink.Calls);
        Assert.Empty(sink.Calls[0].Hints);
        room.AssertNoRuntimes();
    }

    // ---- examine -------------------------------------------------------------------------------------------------

    [Fact]
    public void TheExamineBaseGivesTgsLines()
    {
        var (room, a, _) = P1.Room(425);
        var item = P1.At<obj_item_test_p1_stub>(room, 6, 5);
        room.In(() =>
        {
            item.name = "stub";
            item.desc = "A test stub.";
            item.resistance_flags = Dm.num(FLAMMABLE);
        });
        DmList result = room.In(() => Waiting.NoSleep(item.examine(a)) as DmList);
        string[] lines = Enumerable.Range(1, (int)result.len).Select(i => Dm.str(result[i])).ToArray();
        Assert.Equal("<i>A test stub.</i>", lines[0]);
        Assert.StartsWith("It is a <span", lines[1]);
        // M4a P2 translated /obj/item/examine_tags and examine_descriptor (items.dm): an item is "an item", with its weight tag.
        Assert.EndsWith(">flammable</span> item.", lines[1]);
        Assert.Equal(2, lines.Length);
        Assert.Equal("a stub", room.In(() => Dm.str(item.get_examine_name(a))).Replace("<em>", "").Replace("</em>", ""));
        Assert.Equal("That's <em>a stub</em>", room.In(() => Dm.str(item.examine_title(a, TRUE))));
        room.AssertNoRuntimes();
    }

    // ---- get_turf_pixel (tg's maptest_get_turf_pixel) --------------------------------------------------------------

    [Fact]
    public void GetTurfPixelClampsAnAtomDrawnOverTheMapsTopEdge()
    {
        // tg's test puts the colossus (a 96x96 sprite) on the top row; the port reads every icon as 32x32 (DV18), so the
        // stand-in is an atom shifted two tiles up by its pixel offset.
        var room = TgToyRoom.Boot(seed: 426);
        room.In(() =>
        {
            var north = locate(1, world.maxy, 1) as turf;
            var tall = Dm.@new<obj_item_test_p1_stub>(north);
            tall.pixel_y = 64;
            Assert.IsAssignableFrom<turf>(get_turf_pixel(tall));
            Assert.Equal(world.maxy, ((turf)get_turf_pixel(tall)).y);
            var mid = Dm.@new<obj_item_test_p1_stub>(locate(5, 5, 1));
            mid.pixel_x = 40; // round(40, 32) = 32: one tile east
            Assert.Same(locate(6, 5, 1), get_turf_pixel(mid));
            mid.flags_1 = Dm.num(Dm.bitor(mid.flags_1, IGNORE_TURF_PIXEL_OFFSET_1));
            Assert.Same(locate(5, 5, 1), get_turf_pixel(mid));
        });
    }
}
