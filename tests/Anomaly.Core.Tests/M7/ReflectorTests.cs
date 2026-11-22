using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// The M7 supermatter slice: tg's reflectors (structures/reflector.dm), which turn MetaStation's emitter beams onto the crystal. A box
/// reflector sends every beam its own way (:254), a single one bounces a beam off its face and lets one from behind through (:201),
/// a double one bounces from both sides (:228); the window's rotate and calculate, refused when the rotation is locked (:325).
/// </summary>
public class ReflectorTests
{
    private const int Second = 20;
    private const int LineY = 3, From = 3, MirrorX = 7;

    private static (AudioRoom R, T Mirror, mob_living_carbon_human Victim) Rig<T>(ulong seed, float angle, int victimX, int victimY) where T : obj_structure_reflector
    {
        var r = new AudioRoom(13, 10, seed: seed);
        r.Step(Second);
        T mirror = r.In(() =>
        {
            var m = Dm.@new<T>(locate(MirrorX, LineY, 1));
            m.set_angle(angle);
            return m;
        });
        mob_living_carbon_human victim = r.Dummy(victimX, victimY);
        return (r, mirror, victim);
    }

    private static void Shoot(AudioRoom r, float angle = 90) => r.In(() =>
    {
        var p = Dm.@new<obj_projectile_beam_emitter_hitscan>(locate(From, LineY, 1));
        Waiting.NoSleep(p.fire(angle));
    });

    private static float Burn(AudioRoom r, mob_living m) => r.In(() => Dm.num(m.get_fire_loss()));

    [Fact]
    public void ABoxReflectorSendsTheBeamItsOwnWay()
    {
        var (r, box, victim) = Rig<obj_structure_reflector_box_anchored>(7901, 0, MirrorX, LineY + 3);
        Assert.Equal("reflector_base", r.In(() => box.icon_state));
        Shoot(r);
        Assert.True(Burn(r, victim) > 0, "the beam turned north at the box and struck the man three tiles up");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASingleReflectorBouncesOffItsFaceAndADoubleOffBoth()
    {
        // a single mirror at 315 (its face north-west): a beam going east (90) meets it at an incidence that sends it north
        var (r, single, north) = Rig<obj_structure_reflector_single_anchored>(7902, 315, MirrorX, LineY + 3);
        Shoot(r);
        Assert.True(Burn(r, north) > 0, "the single mirror turns the eastbound beam north");
        r.AssertNoRuntimes();

        // facing away (135), the eastbound beam hits its back and stops there (auto_reflect returns FALSE: bullet_act's ..())
        var (r2, back, north2) = Rig<obj_structure_reflector_single_anchored>(7903, 135, MirrorX, LineY + 3);
        Shoot(r2);
        Assert.Equal(0f, Burn(r2, north2));
        r2.AssertNoRuntimes();

        // a double mirror at 135 bounces it anyway: incidence (135 - 270) %% 360 = 225, the new angle 135 + 225 = 0 (north, :228)
        var (r3, dbl, north3) = Rig<obj_structure_reflector_double_anchored>(7904, 135, MirrorX, LineY + 3);
        Shoot(r3);
        Assert.True(Burn(r3, north3) > 0, "the double mirror bounces from its back side too");
        r3.AssertNoRuntimes();
    }

    [Fact]
    public void ItsWindowTurnsItUnlessItsRotationIsLocked()
    {
        var (r, single, bob) = Rig<obj_structure_reflector_single_anchored>(7905, 0, MirrorX - 1, LineY);
        r.In(() =>
        {
            var ui = Dm.@new<datum_tgui>(bob, single, "Reflector");
            Waiting.NoSleep(single.ui_act("rotate", Dm.list(Dm.kv("rotation_angle", 370)), ui, null));
            Assert.InRange(single.rotation_angle, 9.999f, 10.001f); // SIMPLIFY_DEGREES
            Waiting.NoSleep(single.ui_act("calculate", Dm.list(Dm.kv("rotation_angle", -15)), ui, null));
            // BYOND's %% keeps the dividend's sign (dm-conformance): SIMPLIFY_DEGREES(10 - 15) is -5, as in tg
            Assert.InRange(single.rotation_angle, -5.001f, -4.999f);
            single.can_rotate = FALSE;
            Waiting.NoSleep(single.ui_act("rotate", Dm.list(Dm.kv("rotation_angle", 90)), ui, null));
            Assert.InRange(single.rotation_angle, -5.001f, -4.999f);
        });
        r.AssertNoRuntimes();
    }
}
