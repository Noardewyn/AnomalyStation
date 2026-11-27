using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Doors.P3;

// M3 P3's smoke tests of lighting_atom.dm: the light vars, set_light* and update_light as the presentation call
// LightChanged (presentation-seam.md section 5). T1 tests P3 independently later.

/// <summary>A lamp fixture (rules 15: a fixture path no tg path starts with): a live structure with tg's light vars set.</summary>
[DmPath("/obj/structure/test_p3_lamp")]
public class test_p3_lamp : obj_structure
{
    public test_p3_lamp()
    {
        light_range = 3;
        light_power = 0.8f;
        light_color = "#FFEEDD";
    }
}

public class LightingAtomTests
{
    private static (TgToyRoom room, RecordingSinks sinks) Room()
    {
        var room = TgToyRoom.Create();
        return (room, RecordingSinks.Install(room.World));
    }

    private static int LightLines(RecordingSinks sinks, atom a)
        => sinks.Lines.Count(l => l.EndsWith(" light " + RecordingSinks.Describe(a), StringComparison.Ordinal));

    [Fact]
    public void AComplexLightAtomReportsItsLightAtInit()
    {
        var (room, sinks) = Room();
        using (room.World.Enter())
        {
            var lamp = Dm.@new<test_p3_lamp>(ToyRoom.FloorBottomLeft());
            Assert.Empty(room.RuntimeText);
            Assert.Equal(1, LightLines(sinks, lamp)); // atoms_initializing_EXPENSIVE.dm:135-136, update_light
            Assert.NotNull(lamp.light); // M7 lumcount: the server keeps tg's light source (lighting_atom.dm:61) beside the presentation call
            Assert.True(Dm.eq(lamp.light_range, 3));
            Assert.True(Dm.eq(lamp.light_power, 0.8f));
            Assert.Equal("#FFEEDD", lamp.light_color);
        }
    }

    [Fact]
    public void SetLightAppliesTgsRulesAndReportsTheChange()
    {
        var (room, sinks) = Room();
        using (room.World.Enter())
        {
            var lamp = Dm.@new<test_p3_lamp>(ToyRoom.FloorBottomLeft());
            sinks.Lines.Clear();

            Waiting.NoSleep(lamp.set_light(l_range: 1, l_power: 2, l_color: "#FF0000"));
            Assert.True(Dm.eq(lamp.light_range, MINIMUM_USEFUL_LIGHT_RANGE)); // lighting_atom.dm:13-14: 1 is raised to 1.4
            Assert.True(Dm.eq(lamp.light_power, 2));
            Assert.Equal("#FF0000", lamp.light_color);
            Assert.Equal(1, LightLines(sinks, lamp));

            // no l_color: NONSENSICAL_VALUE, the colour stays (:25); l_dir alone still updates the light
            Waiting.NoSleep(lamp.set_light(l_dir: SOUTH));
            Assert.Equal("#FF0000", lamp.light_color);
            Assert.True(Dm.eq(lamp.light_dir, SOUTH));
            Assert.Equal(2, LightLines(sinks, lamp));

            // a range of 0 deletes the light source (:50-51): the client drops the light
            Waiting.NoSleep(lamp.set_light(l_range: 0));
            Assert.True(Dm.eq(lamp.light_range, 0));
            Assert.Equal(3, LightLines(sinks, lamp));
            Assert.Null(Waiting.NoSleep(lamp.set_light_on(TRUE))); // already on: the setter returns null (:144-145)
            Assert.True(Dm.eq(Waiting.NoSleep(lamp.set_light_on(FALSE)), TRUE)); // the old value
        }
    }

    [Fact]
    public void AFrozenLightOnlyTurns()
    {
        var (room, _) = Room();
        using (room.World.Enter())
        {
            var lamp = Dm.@new<test_p3_lamp>(ToyRoom.FloorBottomLeft());
            lamp.set_light_flags(LIGHT_FROZEN);
            Waiting.NoSleep(lamp.set_light(l_range: 6, l_power: 5, l_color: "#00FF00", l_dir: WEST, l_on: FALSE));
            Assert.True(Dm.eq(lamp.light_range, 3)); // lighting_atom.dm:5-11: everything but l_dir is nulled
            Assert.True(Dm.eq(lamp.light_power, 0.8f));
            Assert.Equal("#FFEEDD", lamp.light_color);
            Assert.True(Dm.eq(lamp.light_on, TRUE));
            Assert.True(Dm.eq(lamp.light_dir, WEST)); // :193: no frozen check for the direction
            Assert.Null(lamp.set_light_flags(Dm.bitor(LIGHT_FROZEN, LIGHT_ATTACHED))); // both frozen: refused (:155)
        }
    }

    [Fact]
    public void UpdateLightOfAnOverlayLightCrashes()
    {
        var (room, _) = Room();
        using (room.World.Enter())
        {
            var lamp = Dm.@new<test_p3_lamp>(ToyRoom.FloorBottomLeft());
            lamp.light_system = OVERLAY_LIGHT;
            Assert.ThrowsAny<Exception>(() => lamp.update_light()); // lighting_atom.dm:47-48, CRASH
        }
    }

    [Fact]
    public void SetOpacityReturnsTheOldValueAndReportsVisibility()
    {
        var (room, sinks) = Room();
        using (room.World.Enter())
        {
            var lamp = Dm.@new<test_p3_lamp>(ToyRoom.FloorBottomLeft());
            sinks.Lines.Clear();
            Assert.True(Dm.eq(Waiting.NoSleep(lamp.set_opacity(TRUE)), FALSE));
            Assert.True(Dm.eq(lamp.opacity, TRUE));
            Assert.Null(Waiting.NoSleep(lamp.set_opacity(TRUE))); // unchanged: null (:71-72)
            Assert.Contains(sinks.Lines, l => l.EndsWith(" visibility " + RecordingSinks.Describe(lamp), StringComparison.Ordinal));
            Assert.Empty(room.RuntimeText);
        }
    }

    [Fact]
    public void AnInertObjectWithTgsDefaultLightLightsThroughTheInertInit()
    {
        // scope 2g: inert types keep their default light through InertInitialize's update_light (types-converter.md 7). The
        // type is one whose nearest live ancestor is a base type with no light code of its own: the inert machines left with
        // tg's default light all have live ancestors that set their light again (a vending machine's power, _vending.dm:385-387;
        // a button's, a fire alarm's), and the last inert machine without one, the syndicate's supermatter shard, is live now.
        var bases = new[] { typeof(obj), typeof(obj_item), typeof(obj_structure), typeof(obj_machinery), typeof(obj_effect) };
        Type type = null;
        foreach (object t in Dm.each(typesof(typeof(obj))))
        {
            if (t is not Type candidate || !InertSeam.IsInertType(candidate) || !candidate.IsDefined(typeof(DmGeneratedOnlyAttribute), false))
                continue;
            Type live = candidate.BaseType;
            while (InertSeam.IsInertType(live))
                live = live.BaseType;
            if (!bases.Contains(live))
                continue;
            var proto = (atom)Activator.CreateInstance(candidate);
            if (proto.light_system == COMPLEX_LIGHT && proto.light_range > 0 && proto.light_power > 0 && proto.color == null && proto.greyscale_config == null)
            {
                type = candidate;
                break;
            }
        }
        Assert.NotNull(type);
        var (room, sinks) = Room();
        using (room.World.Enter())
        {
            var made = (atom)Dm.@new(type, ToyRoom.FloorBottomLeft());
            Assert.True(LightLines(sinks, made) == 1, type.Name + " sent " + LightLines(sinks, made) + " light lines");
            Assert.True(!room.RuntimeText.Any(), string.Join("\n", room.RuntimeText));
        }
    }
}
