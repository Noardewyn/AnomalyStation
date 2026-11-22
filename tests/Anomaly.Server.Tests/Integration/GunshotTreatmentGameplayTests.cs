using System.Text.RegularExpressions;
using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// docs/ai/specs/medicine.md section 3 (D177 and M7x meet M8) through the real server and three real clients: Carol shoots Bob's arm with the
/// detective's .38 and his chest with a laser gun, each shot aimed by her camera's ray at the drawn body part (D177), so the bullet wounds
/// the arm it crossed and the laser burns the chest. Alice finds both with the health analyzer (its limb table names the bleeding arm's
/// wound and the burned chest) and treats them aimed at those parts (DV30): a suture and gauze on the arm until it stops bleeding, a
/// regenerative mesh on the chest until the burn is gone.
/// </summary>
public class GunshotTreatmentGameplayTests(ITestOutputHelper output)
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

    private static T Hold<T>(RealHeadlessWorld w, mob_living_carbon_human who) where T : obj_item
        => In(w, () =>
        {
            Waiting.NoSleep(who.dropItemToGround(who.get_active_held_item() as obj_item));
            var item = Dm.@new<T>(who.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_active_hand(item))));
            return item;
        });

    // The shooter's camera ray from her eye to Bob's point `lateral` tiles north of his centre at `up` metres (Bob stands east of her).
    private static ShotAim AimAt(RealHeadlessWorld w, RealHeadlessWorld.Player p, mob bob, double up, double lateral = 0)
    {
        (double bx, double by) = In(w, () => LocomotionSeam.TryGetState(w.World, bob.dm_id, out Anomaly.Core.Motion.MotionState s, out _)
            ? (s.X / 256.0, s.Y / 256.0) : (Dm.num(bob.x), Dm.num(bob.y)));
        (float ox, float oy, int z) = p.Session.Prediction.RenderPosition();
        double eastE = (ox - 0.5) * 2, northE = (oy - 0.5) * 2, upE = WorldScale.DefaultEyeHeight;
        double dx = (bx - 0.5) * 2 - eastE, dy = (by + lateral - 0.5) * 2 - northE, dz = up - upE, length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        return PlaySession.AimOf(new PickRay(eastE, northE, upE, z, dx / length, dy / length, dz / length, 10));
    }

    [Fact]
    public void Carol_shoots_Bobs_arm_and_chest_and_Alice_finds_both_with_the_analyzer_and_stops_the_bleeding()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 177, TimeoutSteps = 20000 });
        Do(w, () => DevCommands.ProjectileSpeed(1f)); // DV211: tg's flight, so a bullet's per-tile falloff is tg's number
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        RealHeadlessWorld.Player carol = w.Join("Carol");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var carolMob = (mob_living_carbon_human)w.MobOf(carol);
        var texts = new List<string>();
        alice.Client.ServerTextReceived += t => texts.Add(t.Html);
        bob.Yaw = Math.PI / 2; // Bob faces west, toward Carol
        Do(w, () =>
        {
            Waiting.NoSleep(carolMob.forceMove(locate(8, 12, 1)));
            Waiting.NoSleep(bobMob.forceMove(locate(12, 12, 1)));
            Waiting.NoSleep(aliceMob.forceMove(locate(12, 14, 1))); // off the line of fire
        });
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        obj_item_bodypart Part(string zone) => In(w, () => (obj_item_bodypart)bobMob.get_bodypart(zone));
        void Fire(ShotAim aim, float seconds = 1)
        {
            Do(w, () => carolMob.next_click = -1);
            Assert.True(carol.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 18, AimZone.Chest), ClickButton.Left, ClickModifiers.None, aim));
            w.Run((int)(seconds * Second));
        }
        void Heal() { Do(w, () => Waiting.NoSleep(bobMob.fully_heal(HEAL_ALL))); w.Run(5); }

        // where Bob's arms are along Carol's aim: probed with slice A's test beam (the dev hook takes her clicks)
        carol.Session.DevCommand("shoot me /obj/projectile/beam/emitter/hitscan");
        w.Run(5);
        var probes = new List<(double Offset, string Zone)>();
        for (int step = -20; step <= 20; step++)
        {
            Fire(AimAt(w, carol, bobMob, 0.9, step * 0.02), 0.3f);
            probes.Add((step * 0.02, new[] { BODY_ZONE_L_ARM, BODY_ZONE_R_ARM }.FirstOrDefault(z => Dm.num(Part(z).burn_dam) > 0)));
            Do(w, () => Waiting.NoSleep(bobMob.fully_heal(HEAL_ALL)));
        }
        (double From, double To, string Zone) arm = (0, 0, null);
        for (int i = 0; i < probes.Count; i++)
        {
            if (probes[i].Zone == null)
                continue;
            int j = i;
            while (j + 1 < probes.Count && probes[j + 1].Zone == probes[i].Zone)
                j++;
            if (arm.Zone == null || probes[j].Offset - probes[i].Offset > arm.To - arm.From)
                arm = (probes[i].Offset, probes[j].Offset, probes[i].Zone);
            i = j;
        }
        Assert.NotNull(arm.Zone);
        carol.Session.DevCommand("shoot off");
        Heal();
        AimZone armAim = arm.Zone == BODY_ZONE_L_ARM ? AimZone.LeftArm : AimZone.RightArm;
        string armName = arm.Zone == BODY_ZONE_L_ARM ? "left arm" : "right arm";
        output.WriteLine($"the {armName} is at offsets {arm.From:0.00} to {arm.To:0.00}");

        // 1. The .38 at the arm until it wounds (a limb's damage stops at its max, so at most two hits)
        Hold<obj_item_gun_ballistic_revolver_c38_detective>(w, carolMob);
        w.Run(5);
        int shots = 0;
        for (; shots < 6 && In(w, () => Dm.each<datum_wound>(Part(arm.Zone).wounds).Count()) == 0; shots++)
            Fire(AimAt(w, carol, bobMob, 0.9, (arm.From + arm.To) / 2));
        List<datum_wound> armWounds = In(w, () => Dm.each<datum_wound>(Part(arm.Zone).wounds).ToList());
        output.WriteLine($"the {armName} wounded by shot {shots}: {In(w, () => string.Join(", ", armWounds.Select(x => $"{Dm.str(x.name)} ({x.GetType().Name}, flow {Dm.num(x.blood_flow):0.##})")))}");
        Assert.Contains(armWounds, x => x is datum_wound_pierce && Dm.num(x.blood_flow) > 0);
        foreach (string zone in new[] { BODY_ZONE_HEAD, BODY_ZONE_CHEST, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG })
            Assert.Equal(0f, In(w, () => Dm.num(Part(zone).brute_dam)));

        // 2. The laser at the chest
        Hold<obj_item_gun_energy_laser>(w, carolMob);
        w.Run(5);
        Fire(AimAt(w, carol, bobMob, 0.8));
        float chestBurn = In(w, () => Dm.num(Part(BODY_ZONE_CHEST).burn_dam));
        output.WriteLine($"the laser burned the chest {chestBurn}; chest wounds: {In(w, () => string.Join(", ", Dm.each<datum_wound>(Part(BODY_ZONE_CHEST).wounds).Select(x => Dm.str(x.name))))}");
        Assert.True(chestBurn > 0, "the laser burns the chest it was aimed at");
        Assert.Equal(0f, In(w, () => Dm.num(Part(arm.Zone).burn_dam)));
        Do(w, () => Waiting.NoSleep(carolMob.dropItemToGround(carolMob.get_active_held_item() as obj_item)));

        // 3. Alice scans him: the limb table names the arm's wound and the chest's burn
        Do(w, () => Waiting.NoSleep(aliceMob.forceMove(locate(12, 13, 1))));
        w.Run(Second);
        Hold<obj_item_healthanalyzer>(w, aliceMob);
        texts.Clear();
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 18, AimZone.Chest), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        string scan = HtmlText.ToPlain(texts.Last(t => t.Contains("boxed_message blue_box", StringComparison.Ordinal)));
        output.WriteLine(scan);
        string[] lines = scan.Split('\n');
        int armRow = Array.FindIndex(lines, l => l.StartsWith(char.ToUpperInvariant(armName[0]) + armName[1..] + ":", StringComparison.Ordinal));
        Assert.True(armRow >= 0, "the arm's row");
        Assert.Contains("Physical trauma", lines[armRow + 1]);
        Match chest = Regex.Match(scan, @"Chest: [0-9]+ ([0-9]+)");
        Assert.True(chest.Success);
        Assert.Equal(MathF.Ceiling(chestBurn), float.Parse(chest.Groups[1].Value), 1);

        // 4. The suture aimed at the arm (clicked again while it bleeds), then gauze over it
        Hold<obj_item_stack_medical_suture>(w, aliceMob);
        bool Bleeds() => In(w, () => Dm.each<datum_wound>(Part(arm.Zone).wounds).Any(x => Dm.num(x.blood_flow) > 0));
        int clicks = 0;
        for (int i = 0; i < 60 && Bleeds(); i++)
        {
            if (!In(w, () => Dm.each_cast<object>(aliceMob.do_afters).Any()))
            {
                Do(w, () => aliceMob.next_click = -1);
                Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 15, armAim), ClickButton.Left, ClickModifiers.None));
                clicks++;
            }
            w.Run(Second / 2);
        }
        output.WriteLine($"{clicks} suture clicks: {In(w, () => string.Join(", ", Dm.each<datum_wound>(Part(arm.Zone).wounds).Select(x => $"{Dm.str(x.name)} {Dm.num(x.blood_flow):0.##}")))}");
        Assert.False(Bleeds(), "the arm stops bleeding");
        Assert.Contains(texts, t => t.Contains("You apply", StringComparison.Ordinal) || t.Contains("You stitch", StringComparison.Ordinal) || t.Contains(armName, StringComparison.Ordinal));
        Hold<obj_item_stack_medical_wrap_gauze>(w, aliceMob);
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 15, armAim), ClickButton.Left, ClickModifiers.None));
        w.Run(3 * Second);
        Assert.NotNull(In(w, () => Part(arm.Zone).applied_items?[LIMB_ITEM_GAUZE]));

        // 5. The mesh on the chest, torn open first (Z)
        Hold<obj_item_stack_medical_mesh>(w, aliceMob);
        alice.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(5);
        for (int i = 0; i < 30 && In(w, () => Dm.num(Part(BODY_ZONE_CHEST).burn_dam)) > 0; i++)
        {
            if (!In(w, () => Dm.each_cast<object>(aliceMob.do_afters).Any()))
            {
                Do(w, () => aliceMob.next_click = -1);
                Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 18, AimZone.Chest), ClickButton.Left, ClickModifiers.None));
            }
            w.Run(Second / 2);
        }
        Assert.Equal(0f, In(w, () => Dm.num(Part(BODY_ZONE_CHEST).burn_dam)));

        // no bleeding left anywhere, and it stays so
        w.Run(10 * Second);
        Assert.All(In(w, () => Dm.each<datum_wound>(bobMob.all_wounds).ToList()), x => Assert.Equal(0f, In(w, () => Dm.num(x.blood_flow))));
        Assert.Equal(0f, In(w, () => Dm.num(bobMob.get_bleed_rate())));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
