using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Integration;

/// <summary>
/// The shim gaps the M2 packages reported, closed at the integration (dm-conformance.md "M2 integration"): animate sets
/// the final vars (and a step without an object continues the last one's), client.screen holds an object once, locate(T)
/// in C with a typepath value, an overlay snapshot is part of the world state hash, and an untransformed atom reads the
/// identity matrix.
/// </summary>
public class ShimGapTests
{
    [Fact]
    public void AnimateSetsTheFinalVarsAtOnceRelativeOnesAddAndAChainContinuesTheLastObject()
    {
        TgToyRoom room = TgToyRoom.Create();
        room.In(() =>
        {
            var thing = Dm.@new<obj>(TgToyRoom.T(2, 2));
            animate(thing, Dm.named("pixel_z", 10), Dm.named("alpha", 128), Dm.named("time", 5));
            Assert.Equal((10f, 128f), (thing.pixel_z, thing.alpha)); // BYOND: the vars change now, the client shows the change
            animate(thing, Dm.named("pixel_z", 2), Dm.named("time", 10), Dm.named("loop", -1), Dm.named("flags", ANIMATION_RELATIVE));
            Assert.Equal(12f, thing.pixel_z);
            animate(Dm.named("pixel_z", -2), Dm.named("time", 10), Dm.named("flags", ANIMATION_RELATIVE)); // the next step of the same object
            Assert.Equal(10f, thing.pixel_z);
            animate(thing, Dm.named("transform", Dm.matrix().Turn(90)), Dm.named("time", 2));
            Assert.Equal(1f, ((matrix)thing.transform).b, 5);
            var img = image(Dm.resource("icons/effects/progressbar.dmi"), thing, "prog_bar_0");
            animate(img, Dm.named("pixel_z", 32), Dm.named("alpha", 255), Dm.named("time", 5), Dm.named("easing", SINE_EASING));
            Assert.Equal((32f, 255f), (img.pixel_z, img.alpha));
            animate(thing, Dm.named("no_such_var", 3), Dm.named("time", 1)); // not a var of the object: nothing assigned, no runtime
        });
    }

    // M4v slice 1 (protocol version 9): the sink's AnimateStep gets the step's subject and its vars before the shim applied them.
    private sealed class StepSink : IAppearanceSink
    {
        public readonly List<(object Target, object Subject, DmList Arguments, DmList Before)> Steps = new();

        public void Changed(object atom, AppearanceAspect aspect) { }

        public void Animate(object target, DmList arguments) => throw new InvalidOperationException("Dm.animate calls AnimateStep");

        public void AnimateStep(object target, object subject, DmList arguments, DmList before) => Steps.Add((target, subject, arguments, before));

        public void Flick(object target, object state) { }

        public void LightChanged(object atom) { }

        public void VisibilityChanged(object atom) { }
    }

    [Fact]
    public void AnimateStepGetsTheSubjectAndTheValuesBeforeEachStep()
    {
        TgToyRoom room = TgToyRoom.Create();
        var sink = new StepSink();
        PresentationSinks.Of(room.World).Appearance = sink;
        room.In(() =>
        {
            var thing = Dm.@new<obj>(TgToyRoom.T(2, 2));
            thing.pixel_z = 4;
            animate(thing, Dm.named("pixel_z", 2), Dm.named("transform", Dm.matrix().Turn(90)), Dm.named("time", 10), Dm.named("flags", ANIMATION_RELATIVE));
            animate(Dm.named("pixel_z", -2), Dm.named("alpha", 0), Dm.named("time", 10), Dm.named("flags", ANIMATION_RELATIVE));
            Assert.Equal(2, sink.Steps.Count);
            Assert.Same(thing, sink.Steps[0].Subject);
            Assert.Equal(4f, sink.Steps[0].Before["pixel_z"]);
            Assert.Equal(1f, ((matrix)sink.Steps[0].Before["transform"]).a); // the identity before, a copy
            Assert.False(Dm.@in("time", sink.Steps[0].Before));
            Assert.Null(sink.Steps[1].Target);
            Assert.Same(thing, sink.Steps[1].Subject); // the chain's object
            Assert.Equal(6f, sink.Steps[1].Before["pixel_z"]); // after the first step
            Assert.Equal(255f, sink.Steps[1].Before["alpha"]);
            Assert.Equal(4f, thing.pixel_z);
        });
    }

    [Fact]
    public void AClientsScreenHoldsAnObjectOnce()
    {
        TgToyRoom room = TgToyRoom.Create();
        room.In(() =>
        {
            var player = Dm.@new<client_test_m1_client>();
            var hudThing = Dm.@new<atom_movable_screen>();
            Dm.append(player.screen, hudThing);
            Dm.append(player.screen, hudThing);
            player.screen.Add(hudThing);
            Assert.Equal(1, player.screen.len);
            Dm.remove(player.screen, hudThing); // removed once, gone
            Assert.Equal(0, player.screen.len);
            DmList ordinary = Dm.list();
            Dm.append(ordinary, hudThing);
            Dm.append(ordinary, hudThing);
            Assert.Equal(2, ordinary.len); // an ordinary list keeps both
        });
    }

    [Fact]
    public void LocateWithATypepathValueFindsTheFirstItemOfThatType()
    {
        TgToyRoom room = TgToyRoom.Create();
        room.In(() =>
        {
            var a = Dm.@new<obj>(TgToyRoom.T(2, 2));
            var b = Dm.@new<obj_item>(TgToyRoom.T(2, 2));
            Type wanted = typeof(obj_item);
            Assert.Same(b, Dm.locate_type(wanted, TgToyRoom.T(2, 2)));
            Assert.Same(a, Dm.locate_type(typeof(obj), Dm.list(a, b)));
            Assert.Null(Dm.locate_type(typeof(mob), Dm.list(a, b)));
            Assert.Null(Dm.locate_type(null, Dm.list(a, b)));
            Assert.Null(Dm.locate_type(wanted, null));
        });
    }

    [Fact]
    public void AnOverlaySnapshotIsPartOfTheWorldStateHash()
    {
        ulong Run(string state)
        {
            TgToyRoom room = TgToyRoom.Create(seed: 3);
            room.In(() =>
            {
                var thing = Dm.@new<obj>(TgToyRoom.T(2, 2));
                Dm.append(thing.overlays, mutable_appearance(Dm.resource("icons/mob/effects/dam_mob.dmi"), state));
            });
            return room.World.ComputeStateHash();
        }

        Assert.Equal(Run("human_chest_20"), Run("human_chest_20"));
        Assert.NotEqual(Run("human_chest_20"), Run("human_chest_30"));
    }

    [Fact]
    public void AnAtomWithoutATransformationReadsTheIdentityMatrix()
    {
        TgToyRoom room = TgToyRoom.Create();
        room.In(() =>
        {
            var thing = Dm.@new<obj>(TgToyRoom.T(2, 2));
            var m = (matrix)thing.transform;
            Assert.Equal((1f, 0f, 0f, 0f, 1f, 0f), (m.a, m.b, m.c, m.d, m.e, m.f));
            thing.transform = ((matrix)thing.transform).Scale(2); // tg's idiom on a fresh atom (brain_item.dm:56)
            Assert.Equal(2f, ((matrix)thing.transform).a);
        });
    }
}
