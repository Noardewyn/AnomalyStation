using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Anomaly.Core.Tests.Tg.M2Medical.T1;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4v;

/// <summary>
/// M4v slice 4 (D143): the animation sites of the ported mechanics that were left as deferred leaves, written from tg's DM: a mob
/// shaken up from the floor shakes sideways (carbon_defense.dm:404), a broken canister tips over (canister.dm:497), a hot
/// heat-exchanging pipe glows in its temperature's colour (he_pipes.dm:62), the resist button flicks (screen_objects.dm:582), and
/// BYOND's turn() of a matrix.
/// </summary>
public class AnimationSiteTests
{
    // The appearance sink's animate() steps and flicks (presentation-seam.md section 5).
    private sealed class Recorder : IAppearanceSink
    {
        public readonly List<(object Subject, DmList Arguments, DmList Before)> Steps = new();
        public readonly List<(object Target, object State)> Flicks = new();

        public void Changed(object atom, AppearanceAspect aspect) { }

        public void Animate(object target, DmList arguments) { }

        public void AnimateStep(object target, object subject, DmList arguments, DmList before) => Steps.Add((subject, arguments, before));

        public void Flick(object target, object state) => Flicks.Add((target, state));

        public void LightChanged(object atom) { }

        public void VisibilityChanged(object atom) { }
    }

    private static float Num(DmList list, string name) => Dm.num(list[name]);

    [Fact]
    public void AParalysedHumanShakenUpShakesSidewaysAndComesBackToItsPlace()
    {
        var room = new MedRoom(seed: 4601, size: 12);
        var helper = room.Human(4, 4);
        var target = room.Human(5, 4);
        var sink = new Recorder();
        PresentationSinks.Of(room.World).Appearance = sink;
        room.In(() =>
        {
            Waiting.NoSleep(target.Paralyze(10 * SECONDS));
            float before = target.pixel_w;
            sink.Steps.Clear();
            Waiting.NoSleep(target.attack_hand(helper, Dm.list()));
            var shake = sink.Steps.Where(s => ReferenceEquals(s.Subject, target) && Dm.@in("pixel_w", s.Arguments)).ToList();
            Assert.Equal(3, shake.Count);
            float direction = Num(shake[0].Arguments, "pixel_w") / 4f; // SHAKE_ANIMATION_OFFSET * direction, prob(50) ? -1 : 1
            Assert.Equal(1f, Math.Abs(direction));
            Assert.Equal((float)Dm.bitor(ANIMATION_PARALLEL, ANIMATION_RELATIVE), Num(shake[0].Arguments, "flags"));
            Assert.Equal((float)Dm.bitor(QUAD_EASING, EASE_OUT), Num(shake[0].Arguments, "easing"));
            Assert.Equal(-8f * direction, Num(shake[1].Arguments, "pixel_w"));
            Assert.Equal((float)ANIMATION_RELATIVE, Num(shake[1].Arguments, "flags"));
            Assert.Equal(4f * direction, Num(shake[2].Arguments, "pixel_w"));
            Assert.Equal((float)Dm.bitor(QUAD_EASING, EASE_IN), Num(shake[2].Arguments, "easing"));
            Assert.All(shake, s => Assert.Equal(0.1f * SECONDS, Num(s.Arguments, "time")));
            Assert.Equal(before, target.pixel_w); // the relative steps add up to nothing
        });
    }

    [Fact]
    public void AStandingHumanHelpedIsNotShaken()
    {
        var room = new MedRoom(seed: 4602, size: 12);
        var helper = room.Human(4, 4);
        var target = room.Human(5, 4);
        var sink = new Recorder();
        PresentationSinks.Of(room.World).Appearance = sink;
        room.In(() =>
        {
            Waiting.NoSleep(target.attack_hand(helper, Dm.list()));
            Assert.DoesNotContain(sink.Steps, s => ReferenceEquals(s.Subject, target) && Dm.@in("pixel_w", s.Arguments));
        });
    }

    [Fact]
    public void ABrokenCanisterTipsOverWithABounce()
    {
        var r = new T2Room(4603);
        var can = r.In(() => Dm.@new<obj_machinery_portable_atmospherics_canister_oxygen>(locate(6, 6, 1)));
        var sink = new Recorder();
        PresentationSinks.Of(r.World).Appearance = sink;
        r.In(() =>
        {
            // tg's damage path: atom_break (canister.dm:473) calls canister_break once; a direct canister_break call runs
            // atom_break -> canister_break again (canister.dm:486) and animates twice, as in tg
            Waiting.NoSleep(can.atom_break());
            Assert.False(Dm.truthy(can.density));
            var tip = Assert.Single(sink.Steps, s => ReferenceEquals(s.Subject, can) && Dm.@in("transform", s.Arguments));
            Assert.Equal(5f, Num(tip.Arguments, "time")); // 0.5 SECONDS, the second positional argument
            Assert.Equal((float)BOUNCE_EASING, Num(tip.Arguments, "easing"));
            var m = (matrix)can.transform;
            Assert.Equal(1f, m.a * m.a + m.b * m.b, 3); // turned, not scaled: turn(transform, rand(-179, 180))
            Assert.Equal(m.a, m.e, 4);
            Assert.Equal(-m.b, m.d, 4);
            Assert.Equal(0f, m.c);
            Assert.Equal(1f, ((matrix)tip.Before["transform"]).a); // it stood upright before
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHotHeatExchangingPipeGlowsInItsTemperaturesColour()
    {
        var r = new T2Room(4604);
        var pipes = new List<obj_machinery_atmospherics>();
        for (int y = 5; y <= 6; y++)
            pipes.Add(r.In(() => Dm.@new<obj_machinery_atmospherics_pipe_heat_exchanging_simple>(locate(5, y, 1))));
        r.In(() => SSair.setup_template_machinery(Dm.list(pipes.ToArray())));
        var pipe = (obj_machinery_atmospherics_pipe_heat_exchanging)pipes[0];
        var sink = new Recorder();
        PresentationSinks.Of(r.World).Appearance = sink;
        r.In(() =>
        {
            Assert.NotNull(pipe.parent);
            var air = pipe.parent.air;
            air.temperature = 400; // below the glow's 500 K: no colour
            pipe.process(1);
            Assert.Empty(sink.Steps);

            air.temperature = 1500;
            pipe.process(1);
            Assert.Equal(1500f, pipe.icon_temperature);
            // he_pipes.dm:62-75: heat2colour's channels, scaled from 64 toward them between 500 K and 2000 K
            float scale = (1500f - 500f) / 1500f;
            float red = 64 + (Dm.num(heat2colour_r(1500f)) - 64) * scale;
            float green = 64 + (Dm.num(heat2colour_g(1500f)) - 64) * scale;
            float blue = 64 + (Dm.num(heat2colour_b(1500f)) - 64) * scale;
            var glow = Assert.Single(sink.Steps);
            Assert.Same(pipe, glow.Subject);
            Assert.Equal(rgb(red, green, blue), glow.Arguments["color"]);
            Assert.Equal(rgb(red, green, blue), pipe.color);
            Assert.Equal(20f, Num(glow.Arguments, "time"));
            Assert.Equal((float)SINE_EASING, Num(glow.Arguments, "easing"));

            sink.Steps.Clear();
            air.temperature = 1505; // within 10 K of the shown temperature: no new glow
            pipe.process(1);
            Assert.Empty(sink.Steps);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheResistButtonFlicksItsOnState()
    {
        var r = new T2Room(4605);
        var sink = new Recorder();
        PresentationSinks.Of(r.World).Appearance = sink;
        r.In(() =>
        {
            var button = Dm.@new<atom_movable_screen_resist>();
            Waiting.NoSleep(button.Click()); // no living usr: the flick alone
            var flick = Assert.Single(sink.Flicks);
            Assert.Same(button, flick.Target);
            Assert.Equal("act_resist_on", flick.State);
        });
    }

    [Fact]
    public void TurnOfAMatrixIsATurnedCopy()
    {
        TgToyRoom room = TgToyRoom.Create();
        room.In(() =>
        {
            matrix m = Dm.matrix(2, 0, 3, 0, 2, 4);
            var turned = (matrix)turn(m, 90);
            Assert.NotSame(m, turned);
            Assert.Equal(2f, m.a); // the argument is not changed
            matrix expected = m.Copy().Turn(90);
            Assert.Equal(expected.a, turned.a);
            Assert.Equal(expected.b, turned.b);
            Assert.Equal(expected.c, turned.c);
            Assert.Equal(expected.d, turned.d);
            Assert.Equal(expected.e, turned.e);
            Assert.Equal(expected.f, turned.f);
            Assert.Equal(-2f, turned.d, 5); // clockwise: x goes to -y
        });
    }
}
