using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The unsafe door crush (door.dm:666) through the real server on MetaStation with two headless clients: Alice stands in the
/// open Fitness glass airlock (138,182), its safety wire is cut (wires/airlock.dm:104; a cut wire clears <c>safe</c>), and
/// the airlock closes on her. tg's crush: "closes on you and crushes you!" for
/// Alice (the bystanders' "closes on Alice, crushing her!" waits for hearing, M9), the splatter on the floor, DOOR_CRUSH_DAMAGE of brute on the chest, Paralyze(10 SECONDS). Bob's client sees the blood
/// decal and the lying body; Alice's client gets the paralysis. (The scream is an emote: M9.)
/// </summary>
public class DoorCrushGameplayTests
{
    private const int Second = 20; // ticks per second
    private const int DoorX = 138, DoorY = 182;

    private readonly ITestOutputHelper _output;

    public DoorCrushGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static List<T> On<T>(RealHeadlessWorld w, int x, int y)
        => In(w, () => Dm.each_cast<atom>(Builtins.locate(x, y, 1).contents).OfType<T>().ToList());

    [MetaServerFact]
    public void An_airlock_with_its_safety_wire_cut_crushes_the_player_standing_in_it_and_the_other_client_sees_it()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 62, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), seed: 202), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var aliceTexts = new List<string>();
        alice.Client.ServerTextReceived += t => aliceTexts.Add(t.Html);
        var bobHeard = new List<SoundEvent>();
        bob.Session.Replica.SoundsReceived += events => bobHeard.AddRange(events);
        var airlock = Assert.Single(On<obj_machinery_door_airlock>(w, DoorX, DoorY));
        using (w.World.Enter())
        {
            Waiting.NoSleep(aliceMob.forceMove(Builtins.locate(DoorX, DoorY, 1)));
            Waiting.NoSleep(bobMob.forceMove(Builtins.locate(DoorX, DoorY + 1, 1)));
        }
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        // set-up: the airlock open (its access and power are the M4a/M7 checks'); Alice stands in it, Bob beside it
        if (In(w, () => Dm.truthy(airlock.density)))
        {
            using (w.World.Enter())
                Waiting.Detach(airlock.open());
            w.Run(3 * Second);
        }
        Assert.False(In(w, () => Dm.truthy(airlock.density)), "the airlock is open");
        Assert.Same(In(w, () => Builtins.locate(DoorX, DoorY, 1)), In(w, () => aliceMob.loc));
        float brute0 = In(w, () => Dm.num(aliceMob.get_brute_loss()));
        Assert.False(In(w, () => Dm.truthy(aliceMob.IsParalyzed())));

        // a safe airlock refuses to close on her (airlock.dm:1410); with the safety wire cut it closes anyway
        using (w.World.Enter())
            Waiting.Detach(airlock.close());
        w.Run(2 * Second);
        Assert.False(In(w, () => Dm.truthy(airlock.density)), "the safe airlock did not close on Alice");
        Assert.Equal(brute0, In(w, () => Dm.num(aliceMob.get_brute_loss())));
        using (w.World.Enter())
        {
            Waiting.NoSleep(airlock.wires.cut(WIRE_SAFETY));
            Assert.False(Dm.truthy(airlock.safe));
            Waiting.Detach(airlock.close());
        }
        w.Run(3 * Second);

        Assert.True(In(w, () => Dm.truthy(airlock.density)), "the airlock closed");
        float hurt = In(w, () => Dm.num(aliceMob.get_brute_loss())) - brute0;
        _output.WriteLine($"Alice took {hurt} brute; paralyzed {In(w, () => Dm.num(aliceMob.AmountParalyzed()))} ds");
        Assert.Equal(DOOR_CRUSH_DAMAGE, hurt);
        Assert.True(In(w, () => Dm.truthy(aliceMob.IsParalyzed())), "Alice is paralyzed");
        Assert.InRange(In(w, () => Dm.num(aliceMob.AmountParalyzed())), 60f, 100f); // Paralyze(10 SECONDS) less the 3 s since

        // the messages of door.dm:670: Alice's own line (the bystanders' line is visible_message's deferred leaf: hearing, M9)
        string aliceLine = aliceTexts.LastOrDefault(t => t.Contains("closes on you and crushes you!", StringComparison.Ordinal));
        Assert.NotNull(aliceLine);
        _output.WriteLine("Alice: " + HtmlText.ToPlain(aliceLine, new List<ChatLink>()));

        // the splatter on the airlock's floor (add_splatter_floor(loc)): Bob's client sees the blood decal and Alice lying down
        var blood = On<obj_effect_decal_cleanable_blood>(w, DoorX, DoorY);
        Assert.NotEmpty(blood);
        ReplicaWorld replica = bob.Session.Replica;
        foreach (var splat in blood)
        {
            ReplicaEntity seen = Assert.Single(replica.Entities, e => e.Id == splat.dm_id);
            Assert.True(replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look));
            _output.WriteLine($"Bob sees the decal {replica.ResolveResource(look.Icon)} {replica.ResolveString(look.IconState)}");
            Assert.Equal("icons/effects/blood.dmi", replica.ResolveResource(look.Icon));
        }
        _output.WriteLine("Bob heard: " + string.Join(", ", bobHeard.Select(h => h.File).Distinct()));
        Assert.Contains(replica.Entities, e => e.Id == aliceMob.dm_id);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount); // nothing the player did ran into a runtime
    }
}
