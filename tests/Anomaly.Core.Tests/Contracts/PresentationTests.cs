using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tests.Contracts;

/// <summary>
/// The presentation seam (docs/ai/specs/presentation-seam.md): world-owned, write-only sinks with no-op defaults, reached
/// through <c>Dm.output_to</c>, <c>animate</c>, <c>flick</c>; the <c>sound</c> datum (BYOND ref <c>/proc/sound</c>,
/// "vars (sound)").
/// </summary>
public class PresentationTests
{
    [Fact]
    public void EveryWorldStartsWithNullSinksAndHasItsOwnSet()
    {
        var w1 = new World();
        var w2 = new World();
        PresentationSinks s1 = PresentationSinks.Of(w1);
        Assert.Same(NullSinks.Messages, s1.Messages);
        Assert.Same(NullSinks.Sounds, s1.Sounds);
        Assert.Same(NullSinks.Appearance, s1.Appearance);
        Assert.Same(s1, DmWorldState.Of(w1).Sinks);
        Assert.NotSame(s1, PresentationSinks.Of(w2));
        var rec = RecordingSinks.Install(w1);
        Assert.Same(rec, s1.Messages);
        Assert.Same(NullSinks.Messages, PresentationSinks.Of(w2).Messages);
        s1.Messages = null;
        Assert.Same(NullSinks.Messages, s1.Messages);
    }

    [Fact]
    public void OutputRoutesTextSoundsAndTheWorldLog()
    {
        var w = new World();
        var rec = RecordingSinks.Install(w);
        var log = new List<string>();
        DmWorldState.Of(w).WorldLog = log.Add;
        var mob = new TestMob("bob").Register<TestMob>(w);
        w.Step(2);
        using (w.Enter())
        {
            Dm.output_to(mob, "hello");
            Dm.output_to(mob, Dm.sound("bang.ogg", volume: 40));
            Dm.output_to(Dm.world.log, "to the log");
            Dm.output_to(Dm.world, "everyone");
            Dm.output_to(null, "nobody");
            Dm.output_to(mob, null);
            Dm.output_to(new TestMob("unregistered"), "skipped"); // dm_id 0: presentation-seam.md section 1, rule 4
        }
        Assert.Equal(new[]
        {
            "tick 2 output TestMob#" + mob.dm_id + " hello",
            "tick 2 sound TestMob#" + mob.dm_id + " bang.ogg volume=40 channel=0",
            "tick 2 output world everyone",
        }, rec.Lines);
        Assert.Equal(new[] { "to the log" }, log);
    }

    [Fact]
    public void TheWorldLogWithoutAHookDiscards()
    {
        var w = new World();
        using (w.Enter())
            Dm.output_to(Dm.world.log, "nowhere");
    }

    [Fact]
    public void AnimateReportsTheTargetAndTheArgumentsAsGiven()
    {
        var w = new World();
        var rec = RecordingSinks.Install(w);
        var atom = new TestAtom("door").Register<TestAtom>(w);
        using (w.Enter())
        {
            Dm.animate(atom, Dm.named("alpha", 0f), Dm.named("time", 5f), Dm.EASE_IN);
            Dm.animate(Dm.named("alpha", 255f), Dm.named("time", 5f)); // continues the previous animation
            Dm.animate(Dm.arglist(Dm.list(atom, Dm.kv("pixel_x", 4f))));
            Dm.animate(Dm.arglist(Dm.alist(atom, Dm.kv("pixel_y", 2f))));      // an alist is read by position too
            Dm.animate(new TestAtom("prototype"), Dm.named("alpha", 1f)); // unregistered: nothing
            Assert.Throws<DmRuntimeException>(() => Dm.animate(Dm.arglist("not a list")));
        }
        string id = "TestAtom#" + atom.dm_id;
        Assert.Equal(new[]
        {
            "tick 0 animate " + id + " list(alpha=0, time=5, 64)",
            "tick 0 animate null list(alpha=255, time=5)",
            "tick 0 animate " + id + " list(pixel_x=4)",
            "tick 0 animate " + id + " list(pixel_y=2)",
        }, rec.Lines);
    }

    [Fact]
    public void FlickReportsTheStateOnTheObject()
    {
        var w = new World();
        var rec = RecordingSinks.Install(w);
        var atom = new TestAtom("door").Register<TestAtom>(w);
        using (w.Enter())
        {
            Dm.flick("opening", atom);
            Dm.flick("opening", null);
        }
        Assert.Equal(new[] { "tick 0 flick TestAtom#" + atom.dm_id + " opening" }, rec.Lines);
    }

    [Fact]
    public void SoundHasByondDefaultsAndNamedArguments()
    {
        sound s = Dm.sound("a.ogg");
        Assert.Equal("a.ogg", s.file);
        Assert.Equal(0f, s.repeat);
        Assert.Equal(0f, s.wait);
        Assert.Equal(0f, s.channel);
        Assert.Equal(100f, s.volume);   // BYOND ref /sound/var/volume: 100
        Assert.Equal(1f, s.falloff);    // /sound/var/falloff: 1
        Assert.Equal(-1f, s.environment); // /sound/var/environment: -1
        Assert.Equal(0f, s.frequency);
        Assert.Null(s.echo);
        sound t = Dm.sound("b.ogg", repeat: 1, channel: 1024, volume: null);
        Assert.Equal(1f, t.repeat);
        Assert.Equal(1024f, t.channel);
        Assert.Equal(100f, t.volume);   // a null argument keeps the default
    }

    [Fact]
    public void RecordingSinksDescribeChatMessagesAndAppearanceEvents()
    {
        var w = new World();
        var rec = RecordingSinks.Install(w);
        var mob = new TestMob("bob").Register<TestMob>(w);
        PresentationSinks sinks = PresentationSinks.Of(w);
        sinks.Messages.Chat(mob, Dm.list(Dm.kv("type", "notice"), Dm.kv("text", "You start prying...")));
        sinks.Messages.Balloon(mob, mob, "can't reach!");
        sinks.Appearance.Changed(mob, AppearanceAspect.IconState);
        sinks.Appearance.LightChanged(mob);
        sinks.Appearance.VisibilityChanged(mob);
        string id = "TestMob#" + mob.dm_id;
        Assert.Equal(new[]
        {
            "tick 0 chat " + id + " notice: You start prying...",
            "tick 0 balloon " + id + " " + id + " can't reach!",
            "tick 0 changed " + id + " IconState",
            "tick 0 light " + id,
            "tick 0 visibility " + id,
        }, rec.Lines);
    }

    [Fact]
    public void I18nReturnsAPlainTemplateAsIs()
    {
        const string template = "can't reach!";
        Assert.Same(template, I18n.T(template));
    }
}
