using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
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
/// M8 slice D through the real server with headless clients: Alice scans Bob with tg's health analyzer and only her client gets the
/// boxed readout with Bob's numbers (D40; Bob's and Carol's feeds hold none of it), which the readout panel opens whole (D184); she
/// sutures Bob's bleeding arm aimed at it (DV30) with tg's suture sounds and wraps it in gauze, whose bandage overlay Carol's client
/// sees; then, alone, she tears open a mesh pack and meshes her own burned chest through her health doll (D183): the click on the
/// doll's chest is a click on her own mob aimed at the chest.
/// </summary>
public class FirstAidGameplayTests(ITestOutputHelper output)
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

    // The health doll as the client knows it: the entry whose picture holds tg's doll zones (D107's HudReader).
    private static bool IsDoll(ReplicaWorld replica, long id)
    {
        if (!replica.Screen.TryGetValue(id, out ScreenEntry entry))
            return false;
        var zones = new List<FoundZone>();
        HudReader.Collect(replica, entry.Appearance, zones);
        return zones.Count > 0;
    }

    private sealed class Tables(ReplicaWorld replica) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }

    [Fact]
    public void The_scan_reaches_only_its_user_a_suture_closes_an_aimed_arm_and_a_doll_click_meshes_ones_own_chest()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 81, TimeoutSteps = 20000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        RealHeadlessWorld.Player carol = w.Join("Carol");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var carolMob = (mob_living_carbon_human)w.MobOf(carol);
        var texts = new Dictionary<string, List<string>> { ["Alice"] = new(), ["Bob"] = new(), ["Carol"] = new() };
        alice.Client.ServerTextReceived += t => texts["Alice"].Add(t.Html);
        bob.Client.ServerTextReceived += t => texts["Bob"].Add(t.Html);
        carol.Client.ServerTextReceived += t => texts["Carol"].Add(t.Html);
        var heard = new List<SoundEvent>();
        alice.Session.Replica.SoundsReceived += events => heard.AddRange(events);
        w.Run(10);
        int bootRuntimes = w.World.RuntimeErrorCount;
        datum_wound cut = In(w, () =>
        {
            Waiting.NoSleep(aliceMob.forceMove(locate(12, 12, 1)));
            Waiting.NoSleep(bobMob.forceMove(locate(13, 12, 1)));
            Waiting.NoSleep(carolMob.forceMove(locate(13, 13, 1)));
            Waiting.NoSleep(((obj_item_bodypart)bobMob.get_bodypart(BODY_ZONE_R_LEG)).receive_damage(17, 0, wound_bonus: CANT_WOUND));
            return (datum_wound)Waiting.NoSleep(bobMob.cause_wound_of_type_and_severity(WOUND_SLASH, (obj_item_bodypart)bobMob.get_bodypart(BODY_ZONE_L_ARM), WOUND_SEVERITY_MODERATE));
        });
        w.Run(Second);

        // 1. The scan: Alice's chat holds the blue box with Bob's numbers; nobody else's feed holds any of it (D40).
        Hold<obj_item_healthanalyzer>(w, aliceMob);
        string percent = In(w, () => Dm.str(round(Dm.num(Dm.div(bobMob.health, bobMob.maxHealth)), 0.01f) * 100));
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 18, AimZone.Chest), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        string scan = texts["Alice"].LastOrDefault(t => t.Contains("boxed_message blue_box", StringComparison.Ordinal));
        Assert.NotNull(scan);
        output.WriteLine(HtmlText.ToPlain(scan));
        Assert.Contains($"<b>{percent}% healthy</b>", scan);
        Assert.Contains("Analyzing results for", scan);
        foreach (string other in new[] { "Bob", "Carol" })
            Assert.DoesNotContain(texts[other], t => t.Contains("boxed_message", StringComparison.Ordinal) || t.Contains("% healthy", StringComparison.Ordinal) || t.Contains("Analyzing results", StringComparison.Ordinal));
        Assert.Contains(heard, h => h.File == "sound/items/healthanalyzer.ogg");
        var readout = new ReadoutModel();
        Assert.True(readout.Offer(scan));
        Assert.Contains("Overall status:", readout.Text);

        // 2. The suture on Bob's left arm, aimed at it (DV30): other_delay 1 s, stop_bleeding 0.5 off the cut's flow.
        Hold<obj_item_stack_medical_suture>(w, aliceMob);
        float flow = In(w, () => Dm.num(cut.blood_flow));
        heard.Clear();
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 22, 15, AimZone.LeftArm), ClickButton.Left, ClickModifiers.None));
        w.Run(Second + 4);
        Assert.Equal(BODY_ZONE_L_ARM, In(w, () => aliceMob.zone_selected));
        float sutured = In(w, () => Dm.num(cut.blood_flow));
        output.WriteLine($"the cut's flow {flow} -> {sutured}");
        Assert.True(sutured <= flow - 0.5f + 0.01f, "the suture takes its stop_bleeding off the cut");
        Assert.Contains(heard, h => (h.File ?? "").StartsWith("sound/items/suture/", StringComparison.Ordinal));
        Assert.Contains(texts["Alice"], t => t.Contains("You apply", StringComparison.Ordinal) && t.Contains("left arm", StringComparison.Ordinal));
        Do(w, () => Waiting.NoSleep(aliceMob.dropItemToGround(aliceMob.get_active_held_item() as obj_item)));
        w.Run(Second); // the suture's loop stops with it out of the hand

        // ... and gauze over it: Carol's client sees tg's bandage on Bob's arm.
        Hold<obj_item_stack_medical_wrap_gauze>(w, aliceMob);
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 22, 15, AimZone.LeftArm), ClickButton.Left, ClickModifiers.None));
        w.Run(4 * Second);
        Assert.True(carol.Session.Replica.TryGet(bobMob.dm_id, out ReplicaEntity bobSeen));
        List<(string Icon, string State, float Layer)> looks = CrewOutfitTests.Icons(carol.Session.Replica, bobSeen.State.Appearance);
        Assert.Contains(looks, l => l.Icon == "icons/mob/human/bandage.dmi" && l.State != null && l.State.StartsWith("l_arm", StringComparison.Ordinal));

        // 3. Alone: a burned chest, a sealed mesh pack torn open in the hand (Z), then a click on the doll's chest (D183).
        Do(w, () =>
        {
            Waiting.NoSleep(bobMob.forceMove(locate(20, 20, 1)));
            Waiting.NoSleep(carolMob.forceMove(locate(22, 20, 1)));
            Waiting.NoSleep(((obj_item_bodypart)aliceMob.get_bodypart(BODY_ZONE_CHEST)).receive_damage(0, 25, wound_bonus: CANT_WOUND));
            aliceMob.zone_selected = BODY_ZONE_HEAD;
        });
        var mesh = Hold<obj_item_stack_medical_mesh>(w, aliceMob);
        Assert.Equal(FALSE, In(w, () => mesh.is_open));
        heard.Clear();
        alice.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(5);
        Assert.Equal(TRUE, In(w, () => mesh.is_open));
        Assert.Contains(heard, h => h.File == "sound/items/poster/poster_ripped.ogg");
        w.Run(Second);

        ReplicaWorld replica = alice.Session.Replica;
        long doll = replica.Screen.Keys.Single(id => IsDoll(replica, id));
        (byte x, byte y) = (16, 16);
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root != null)
        {
            // the pixel of the doll's chest as the client finds it under the pointer (BodyZoneAim.DollZoneAt over tg's healthdoll states)
            var compositor = new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(root)));
            var tables = new Tables(replica);
            TableRef look = replica.Screen[doll].Appearance;
            (x, y) = Enumerable.Range(1, 32).SelectMany(px => Enumerable.Range(1, 32).Select(py => ((byte)px, (byte)py)))
                .First(p => BodyZoneAim.DollZoneAt(tables, compositor, look, p.Item1, p.Item2) == AimZone.Chest);
            Assert.Equal(AimZone.Head, Enumerable.Range(1, 32).SelectMany(px => Enumerable.Range(1, 32).Select(py => BodyZoneAim.DollZoneAt(tables, compositor, look, (byte)px, (byte)py))).First(z => z == AimZone.Head));
        }
        ClickTarget click = DragModel.SelfItemClickOnDoll(new ClickTarget(TargetKind.Screen, doll, 0, 0, 0, x, y), ClickModifiers.None, true, id => IsDoll(replica, id), replica.OwnEntity, AimZone.Chest);
        Assert.Equal(new ClickTarget(TargetKind.Entity, aliceMob.dm_id, 0, 0, 0, 0, 0, AimZone.Chest), click);
        heard.Clear();
        Assert.True(alice.Session.Click(click, ClickButton.Left, ClickModifiers.None));
        w.Run(3 * Second + 4); // self_delay 3 s
        Assert.Equal(BODY_ZONE_CHEST, In(w, () => aliceMob.zone_selected));
        Assert.Equal(15f, In(w, () => Dm.num(((obj_item_bodypart)aliceMob.get_bodypart(BODY_ZONE_CHEST)).burn_dam)), 3);
        Assert.Contains(heard, h => (h.File ?? "").StartsWith("sound/items/regenerative_mesh/", StringComparison.Ordinal));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
