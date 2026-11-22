using Anomaly.Client.Shared;
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
/// Gaps package C4 on MetaStation through the real server: Tom, Pete and the xenobiology slime are on the map, live and on their trees;
/// they move with no runtime and the client's replica sees them; the minor mapping subsystem has dropped its vermin on exposed wires.
/// </summary>
public class CreaturesGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    [MetaServerFact]
    public void Tom_Pete_and_the_vermin_live_on_Meta_and_the_client_sees_them()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 98, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Crewman");
        ReplicaWorld replica = p.Session.Replica;
        w.Run(Second);
        var errors = new List<Exception>();
        Action<Exception> sink = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e =>
        {
            sink?.Invoke(e);
            // the rig's table of sound lengths (RigSoundLengths) lacks some of the rooms' ambience: not a creature's
            if (!e.Message.Contains("rustg_sound_length", StringComparison.Ordinal) && !e.Message.Contains("play_ambience", StringComparison.Ordinal))
                errors.Add(e);
        };

        var tom = In(w, () => Dm.each<mob_living_basic_mouse_brown_tom>(world).First());
        var pete = In(w, () => Dm.each<mob_living_basic_goat_pete>(world).First());
        var slime = In(w, () => Dm.each<mob_living_basic_slime>(world).First());
        Assert.False(In(w, () => InertSeam.IsInert(tom)));
        Assert.False(In(w, () => InertSeam.IsInert(pete)));
        Assert.False(In(w, () => InertSeam.IsInert(slime)));
        Assert.NotNull(tom.ai_controller);
        Assert.NotNull(pete.ai_controller);
        Assert.NotNull(slime.ai_controller);
        Assert.Equal("mouse_brown", In(w, () => tom.icon_state));
        Assert.Equal("goat", In(w, () => pete.icon_state));
        Assert.Equal("grey-baby", In(w, () => slime.icon_state));

        int vermin = In(w, () => Dm.each<mob_living_basic_mouse>(world).Count() + Dm.each<mob_living_basic_snail>(world).Count() + Dm.each<mob_living_basic_stoat>(world).Count() + Dm.each<mob_living_basic_regal_rat>(world).Count());
        output.WriteLine($"{vermin} vermin on the map (Tom included)");
        Assert.True(vermin >= 2, "minor mapping dropped its vermin on exposed wires");

        var startTom = In(w, () => (tom.x, tom.y));
        var startPete = In(w, () => (pete.x, pete.y));
        bool tomMoved = false, peteMoved = false;
        for (int i = 0; i < 90 && !(tomMoved && peteMoved); i++)
        {
            w.Run(Second);
            tomMoved |= In(w, () => (tom.x, tom.y)) != startTom;
            peteMoved |= In(w, () => (pete.x, pete.y)) != startPete;
        }
        Assert.True(tomMoved, "Tom wandered");
        Assert.True(peteMoved, "Pete wandered");

        // the client sees Pete: walk the crewman next to him and read the replica
        var me = (mob_living_carbon_human)w.MobOf(p);
        In(w, () => { Waiting.NoSleep(me.forceMove(locate(pete.x + 1, pete.y, pete.z))); return true; });
        w.Run(Second);
        ReplicaEntity seen = replica.Entities.Single(e => e.Id == pete.dm_id);
        Assert.True(replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look));
        Assert.Equal("goat", replica.ResolveString(look.IconState));
        Assert.Empty(errors);
    }
}
