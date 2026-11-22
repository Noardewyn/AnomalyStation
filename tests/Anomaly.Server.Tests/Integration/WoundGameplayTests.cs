using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
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
/// M6 slice 3 (the wound types) through the real server on MetaStation with a headless client, in the corridor east of the
/// wall at (42,155): a person's arm is torn open (a critical slash) and the client sees tg's bleed overlay on that arm, hears
/// the wound's sound and the floor around the person gets the blood; the player picks a fire extinguisher up and right-clicks
/// the person, winding it up for tg's head bash (attack_secondary), which lands on the head; then the player's own arm suffers a
/// compound fracture and drops the extinguisher, which the client sees on the floor.
/// </summary>
public class WoundGameplayTests
{
    private const int Second = 20;
    private const int X = 43, Y = 155;

    private readonly ITestOutputHelper _output;

    public WoundGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static turf At(int x, int y) => Builtins.locate(x, y, 1);

    private static List<(string Icon, string State, float Layer)> Looks(ReplicaWorld replica, long id)
        => replica.TryGet(id, out ReplicaEntity e) ? CrewOutfitTests.Icons(replica, e.State.Appearance) : new();

    private static obj_item_bodypart Part(mob_living_carbon_human h, string zone) => (obj_item_bodypart)h.get_bodypart(zone);

    [MetaServerFact]
    public void A_torn_arm_bleeds_a_heavy_extinguisher_bashes_a_head_and_a_broken_arm_drops_its_item()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 63, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        int bootRuntimes = w.World.RuntimeErrorCount;
        Assert.All(Enumerable.Range(X, 4), x => Assert.True(In(w, () => At(x, Y) is turf_open && !Dm.truthy(At(x, Y).density)), $"({x},{Y}) is open floor"));

        mob_living_carbon_human bob = In(w, () =>
        {
            Waiting.NoSleep(me.forceMove(At(X, Y)));
            return Dm.@new<mob_living_carbon_human>(At(X + 1, Y));
        });
        w.Run(Second);

        // A weeping avulsion on Bob's left arm: tg's bleed overlay on the arm, the wound's sound, blood on the floor.
        datum_wound cut = In(w, () => (datum_wound)Waiting.NoSleep(bob.cause_wound_of_type_and_severity(WOUND_SLASH, Part(bob, BODY_ZONE_L_ARM), WOUND_SEVERITY_CRITICAL)));
        w.Run(Second);
        Assert.IsType<datum_wound_slash_flesh_critical>(cut);
        List<(string Icon, string State, float Layer)> bobLook = Looks(replica, bob.dm_id);
        _output.WriteLine("Bob's wound overlays: " + string.Join(", ", bobLook.Where(l => l.Icon == null || l.Icon == "icons/mob/effects/bleed_overlays.dmi").Select(l => $"{l.Icon}:{l.State}")));
        // tg's wound overlay: a "blank" of bleed_overlays.dmi holding the bleeding limbs' states as text overlays, which have no
        // icon of their own and are drawn from their parent's (the client's compositor, as BYOND)
        Assert.Contains(bobLook, l => l.Icon == "icons/mob/effects/bleed_overlays.dmi" && l.State == "blank");
        Assert.Contains(bobLook, l => l.Icon == null && l.State != null && l.State.StartsWith("l_arm_", StringComparison.Ordinal));
        Assert.Contains(heard, h => h.File == "sound/effects/wounds/blood3.ogg");
        w.Run(10 * Second);
        int blood = In(w, () => Enumerable.Range(X - 1, 4).SelectMany(x => Enumerable.Range(Y - 1, 3).Select(y => (x, y)))
            .Sum(c => Dm.each_cast<atom>(At(c.x, c.y).contents).OfType<obj_effect_decal_cleanable_blood>().Count()));
        _output.WriteLine($"Bob's blood volume {In(w, () => bob.blood_volume)}; {blood} blood decals around him; the cut's flow {In(w, () => Dm.num(cut.blood_flow))}");
        Assert.True(In(w, () => bob.blood_volume) < BLOOD_VOLUME_NORMAL, "Bob bleeds");
        Assert.True(blood > 0, "the floor gets the blood");

        // Alice picks a fire extinguisher up and right-clicks Bob: the wind-up (2 s) and the bash on his head.
        obj_item_extinguisher extinguisher = In(w, () => Dm.@new<obj_item_extinguisher>(me.loc));
        w.Run(Second);
        Assert.True(p.Session.Click(extinguisher.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.True(In(w, () => extinguisher.loc == me), "the extinguisher is in hand");
        float headBefore = In(w, () => Dm.num(Part(bob, BODY_ZONE_HEAD).brute_dam));
        Assert.True(p.Session.Click(bob.dm_id, ClickButton.Right, ClickModifiers.None));
        w.Run(4 * Second);
        float headAfter = In(w, () => Dm.num(Part(bob, BODY_ZONE_HEAD).brute_dam));
        _output.WriteLine($"Bob's head brute {headBefore} -> {headAfter}; his head's wounds: " + In(w, () => string.Join(", ", Dm.each_cast<datum_wound>(Part(bob, BODY_ZONE_HEAD).wounds).Select(x => DmTypes.PathOf(x.GetType())))));
        Assert.True(headAfter > headBefore, "the bash lands on the head");
        Assert.Contains(heard, h => h.File == "sound/items/weapons/smash.ogg");

        // A compound fracture of the arm that holds the extinguisher: the arm gives out and drops it.
        string arm = In(w, () => IS_RIGHT_INDEX(Dm.num(me.get_held_index_of_item(extinguisher))) ? BODY_ZONE_R_ARM : BODY_ZONE_L_ARM);
        datum_wound fracture = In(w, () => (datum_wound)Waiting.NoSleep(me.cause_wound_of_type_and_severity(WOUND_BLUNT, Part(me, arm), WOUND_SEVERITY_CRITICAL)));
        w.Run(Second);
        Assert.IsType<datum_wound_blunt_bone_critical>(fracture);
        Assert.True(In(w, () => extinguisher.loc == me.loc), "the broken arm dropped the extinguisher");
        Assert.True(replica.TryGet(extinguisher.dm_id, out ReplicaEntity dropped), "the client sees the dropped extinguisher");
        _output.WriteLine($"the dropped extinguisher at ({dropped.State.X},{dropped.State.Y})");
        Assert.Contains(heard, h => h.File == "sound/effects/wounds/crack2.ogg");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
