using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M8 slice F (D185, D186) through the real server, a strict world, with headless clients. Alice puts on medical HUD glasses and her replica
/// holds an image of Bob's health bar and status hanging on Bob's body (tg's client.images, the private block of her session alone: Carol,
/// without glasses, gets none, D40); the bar drops when Bob is hurt; the glasses' toggle takes the images away and brings them back.
/// Carol puts on a security HUD; Bob is dressed by the dev verb `outfit` with tg's security officer outfit, which in a strict world now
/// equips whole: the mindshield is in his body, Carol's replica shows its icon and the officer's job icon above him, and Alice's (a medical HUD
/// only) shows neither. The second test dresses every station job's outfit on one body.
/// </summary>
public class MedHudGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    // The icon states of the private images hanging on one body, as the replica of a session holds them.
    private static List<string> StatesOn(ReplicaWorld replica, long body)
    {
        var states = new List<string>();
        foreach (ImageEntry image in replica.Images.Values)
        {
            if (image.AttachedTo != body || !replica.TryGetAppearance(image.Appearance, out AppearanceEntry entry))
                continue;
            states.Add(replica.ResolveString(entry.IconState));
        }
        return states;
    }

    private static T Wear<T>(RealHeadlessWorld w, mob_living_carbon_human who) where T : obj_item
        => In(w, () =>
        {
            var item = Dm.@new<T>(who.loc);
            Waiting.NoSleep(who.equip_to_slot(item, ITEM_SLOT_EYES));
            return item;
        });

    [Fact]
    public void Medical_hud_glasses_put_the_health_bar_on_the_patient_for_their_wearer_alone_and_a_mindshield_shows_to_a_security_hud()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 91, TimeoutSteps = 20000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        RealHeadlessWorld.Player carol = w.Join("Carol");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var carolMob = (mob_living_carbon_human)w.MobOf(carol);
        w.Run(10);
        int bootRuntimes = w.World.RuntimeErrorCount;
        Do(w, () =>
        {
            Waiting.NoSleep(aliceMob.forceMove(locate(12, 12, 1)));
            Waiting.NoSleep(bobMob.forceMove(locate(13, 12, 1)));
            Waiting.NoSleep(carolMob.forceMove(locate(13, 13, 1)));
        });
        w.Run(Second);
        ReplicaWorld aliceSees = alice.Session.Replica, carolSees = carol.Session.Replica, bobSees = bob.Session.Replica;
        long bobId = bobMob.dm_id;

        // before the glasses nobody holds a HUD image of Bob (the doll's own pictures are screen objects, not images)
        Assert.DoesNotContain(StatesOn(aliceSees, bobId), s => s != null && s.StartsWith("hud", StringComparison.Ordinal));
        Assert.DoesNotContain(StatesOn(carolSees, bobId), s => s != null && s.StartsWith("hud", StringComparison.Ordinal));

        // 1. Alice puts on a health scanner HUD: Bob's health bar and status arrive in her replica, hung on Bob
        var glasses = Wear<obj_item_clothing_glasses_hud_health>(w, aliceMob);
        w.Run(Second);
        List<string> aliceSeesOnBob = StatesOn(aliceSees, bobId);
        output.WriteLine("Alice sees on Bob: " + string.Join(", ", aliceSeesOnBob));
        Assert.Contains("hudhealth100", aliceSeesOnBob);
        Assert.Contains("hudhealthy", aliceSeesOnBob);
        // not for Carol (no glasses), and not for Bob: nobody sees a bar of anyone without a HUD (D40)
        Assert.DoesNotContain(StatesOn(carolSees, bobId), s => s != null && s.StartsWith("hudhealth", StringComparison.Ordinal));
        Assert.DoesNotContain(bobSees.Images.Values, i => bobSees.TryGetAppearance(i.Appearance, out AppearanceEntry e) && (bobSees.ResolveString(e.IconState) ?? "").StartsWith("hudhealth", StringComparison.Ordinal));

        // 2. Bob is hurt: the bar drops in Alice's replica (the image changed in place, not a new one)
        long barId = aliceSees.Images.Values.First(i => i.AttachedTo == bobId && (aliceSees.ResolveString(aliceSees.TryGetAppearance(i.Appearance, out AppearanceEntry e) ? e.IconState : default) ?? "").StartsWith("hudhealth", StringComparison.Ordinal)).Id;
        Do(w, () => Waiting.NoSleep(bobMob.adjust_brute_loss(60)));
        w.Run(Second);
        string expected = "hud" + In(w, () => (string)RoundHealth(bobMob));
        List<string> after = StatesOn(aliceSees, bobId);
        output.WriteLine("after the beating (health " + In(w, () => bobMob.health) + "): " + string.Join(", ", after));
        Assert.Contains(expected, after);
        Assert.NotEqual("hudhealth100", expected);
        Assert.DoesNotContain("hudhealth100", after);
        Assert.True(aliceSees.Images.ContainsKey(barId), "the same image carries the changed state");
        Assert.DoesNotContain(StatesOn(carolSees, bobId), s => s != null && s.StartsWith("hudhealth", StringComparison.Ordinal));

        // 3. The toggle takes the images away and brings them back
        Do(w, () => glasses.toggle_hud_display(aliceMob));
        w.Run(Second);
        Assert.DoesNotContain(StatesOn(aliceSees, bobId), s => s != null && s.StartsWith("hudhealth", StringComparison.Ordinal));
        Do(w, () => glasses.toggle_hud_display(aliceMob));
        w.Run(Second);
        Assert.Contains(expected, StatesOn(aliceSees, bobId));

        // 4. Carol puts on a security HUD; Bob is made a security officer by tg's outfit (the strict world: the mindshield equips)
        var secHud = Wear<obj_item_clothing_glasses_hud_security>(w, carolMob);
        w.Host.EnqueueConsoleLine("outfit Bob /datum/outfit/job/security");
        w.Run(Second * 2);
        Assert.True(In(w, () => HAS_TRAIT(bobMob, TRAIT_MINDSHIELD)));
        Assert.Single(In(w, () => Dm.each<obj_item_implant_mindshield>(bobMob.implants).ToList()));
        List<string> carolSeesOnBob = StatesOn(carolSees, bobId);
        output.WriteLine("Carol sees on Bob: " + string.Join(", ", carolSeesOnBob));
        Assert.Contains("hud_imp_loyal", carolSeesOnBob);
        Assert.Contains(SECHUD_SECURITY_OFFICER, carolSeesOnBob);
        // DV231: composed as the client draws it, the job icon and the mindshield's frame on it move over the head, the rest stays
        if (Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot() is string iconRoot)
        {
            var compositor = new Anomaly.Client.Shared.Icons.AppearanceCompositor(new Anomaly.Client.Shared.Icons.IconLibrary(new Anomaly.Client.Shared.Icons.DirectoryIconFiles(iconRoot)));
            foreach (ImageEntry image in carolSees.Images.Values.Where(i => i.AttachedTo == bobId))
            {
                Assert.True(carolSees.TryGetAppearance(image.Appearance, out AppearanceEntry look));
                Assert.True(compositor.TryCompose(new HairGameplayTests.Tables(carolSees), image.Appearance, 2, 0, out Anomaly.Client.Shared.Icons.ComposedRaster picture));
                bool job = carolSees.ResolveString(look.IconState) is SECHUD_SECURITY_OFFICER or "hud_imp_loyal";
                Assert.Equal(job ? (Anomaly.Client.Shared.DataHudPlacement.JobShiftRight, Anomaly.Client.Shared.DataHudPlacement.JobShiftUp) : (0, 0),
                    Anomaly.Client.Shared.DataHudPlacement.Shift(picture));
            }
        }
        // Alice's glasses are a medical HUD: no security images for her; Bob (no glasses) sees none of his own
        List<string> aliceNow = StatesOn(aliceSees, bobId);
        Assert.DoesNotContain("hud_imp_loyal", aliceNow);
        Assert.DoesNotContain(SECHUD_SECURITY_OFFICER, aliceNow);
        Assert.DoesNotContain(StatesOn(bobSees, bobId), s => s == "hud_imp_loyal" || s == SECHUD_SECURITY_OFFICER);
        // the medical bar is still Alice's
        Assert.Contains(expected, aliceNow);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    // The outfits of the station jobs (tg's /datum/outfit/job), the MODsuit outfits included since gaps MO1: not the gimmick assistants
    // (large worn sprites) nor the bitrunning domain's (the same set as the ported outfit_sanity, docs/ai/specs/not-translated.md section 55).
    private static bool StationJobOutfit(Type t, datum_outfit outfit)
        => !t.Name.Contains("assistant_gimmick", StringComparison.Ordinal) && !t.Name.Contains("virtual_domain", StringComparison.Ordinal);

    [Fact]
    public void Every_station_job_outfit_equips_whole_in_a_strict_world_with_its_implants_and_active_skillchips()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 92, TimeoutSteps = 20000 });
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        w.Run(10);
        int bootRuntimes = w.World.RuntimeErrorCount;
        int dressed = 0, implanted = 0, chipped = 0;
        List<Type> outfits = In(w, () => Dm.each_cast<Type>(subtypesof(typeof(datum_outfit_job))).ToList());
        Assert.True(outfits.Count > 40, "station job outfits: " + outfits.Count);
        foreach (Type outfit_type in outfits)
        {
            datum_outfit outfit = In(w, () => Dm.@new(outfit_type) as datum_outfit);
            if (!StationJobOutfit(outfit_type, outfit))
                continue;
            Do(w, () =>
            {
                foreach (obj_item i in Dm.each<obj_item>(bobMob.get_equipped_items(INCLUDE_POCKETS | INCLUDE_ABSTRACT)))
                    qdel(i);
                Waiting.NoSleep(bobMob.destroy_all_skillchips());
                foreach (obj_item_implant implant in Dm.each<obj_item_implant>(bobMob.implants).ToList())
                    qdel(implant);
                Assert.True(Dm.truthy(Waiting.NoSleep(outfit.equip(bobMob, FALSE))), outfit.name + " did not equip");
                foreach (Type implant_type in Dm.each_cast<Type>(outfit.implants))
                {
                    Assert.True(Dm.each<obj_item_implant>(bobMob.implants).Any(i => i.GetType() == implant_type), outfit.name + " did not implant a " + implant_type);
                    implanted++;
                }
                var brain = (obj_item_organ_brain)bobMob.get_organ_slot(ORGAN_SLOT_BRAIN);
                foreach (Type chip_type in Dm.each_cast<Type>(outfit.skillchips))
                {
                    Assert.True(Dm.each<obj_item_skillchip>(brain.skillchips).Any(c => c.GetType() == chip_type && Dm.truthy(c.active)), outfit.name + " did not activate a " + chip_type);
                    chipped++;
                }
            });
            dressed++;
            w.Run(1);
        }
        output.WriteLine($"{dressed} station job outfits dressed Bob; {implanted} implants and {chipped} skillchips went in");
        Assert.True(dressed > 40);
        Assert.True(implanted >= 6, "the security officer, warden, HoS, detective, captain and the clown's implants: " + implanted);
        Assert.True(chipped >= 12, "the chips of the jobs: " + chipped);
        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, w.Err.ToString());
    }
}
