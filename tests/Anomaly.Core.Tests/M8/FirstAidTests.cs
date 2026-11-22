using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M8;

/// <summary>
/// M8 slice D: tg's healing items in a running world, each reached by a medic's click as tg's (stacks/medical.dm, tape.dm,
/// tourniquet.dm, the wounds' treatments, human.dm's CPR, the stethoscope, the penlight and the health analyzer). Every number
/// is tg's: each stack heals its heal_brute/heal_burn on the aimed limb after its other_delay (self_delay on oneself), a suture
/// takes stop_bleeding off a slash's flow (0.7 of it on oneself), ointment and mesh add their flesh_regeneration and
/// sanitization to a burn wound, a tourniquet keeps a tenth of a limb's bleeding, a CPR pump takes 7 oxygen damage off.
/// </summary>
public class FirstAidTests
{
    private const int Second = 20;

    private sealed class Feed : IMessageSink
    {
        public List<(object Target, string Html)> Chat { get; } = new();

        public List<(object Viewer, string Text)> Balloons { get; } = new();

        void IMessageSink.Chat(object target, DmList message) => Chat.Add((target, Dm.str(message?["html"])));

        public void Output(object target, string text) { }

        public void Balloon(object viewer, object atom, string text) => Balloons.Add((viewer, text));

        public List<string> To(object who) => Chat.Where(c => ReferenceEquals(c.Target, who) || (c.Target is client cl && ReferenceEquals(cl.mob, who))).Select(c => c.Html).ToList();
    }

    private sealed class FlickRecorder : IAppearanceSink
    {
        public readonly List<(object Target, object State)> Flicks = new();

        public void Changed(object atom, AppearanceAspect aspect) { }

        public void Animate(object target, DmList arguments) { }

        public void AnimateStep(object target, object subject, DmList arguments, DmList before) { }

        public void Flick(object target, object state) => Flicks.Add((target, state));

        public void LightChanged(object atom) { }

        public void VisibilityChanged(object atom) { }
    }

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        // tg's strings/ files (the scars' texts), as the server mounts them
        if (TgClone.Root != null)
            TgStrings.Mount(DmWorldState.Of(r.World).Files, TgClone.Root);
        r.In(() =>
        {
            for (int x = 2; x <= 11; x++)
            {
                for (int y = 2; y <= 8; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
            }
        });
        r.Step(Second);
        return r;
    }

    private static obj_item_bodypart Part(mob_living_carbon_human h, string zone) => (obj_item_bodypart)h.get_bodypart(zone);

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who, string zone) where T : obj_item
        => r.In(() =>
        {
            var item = Dm.@new<T>(who.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_active_hand(item))));
            who.zone_selected = zone;
            return item;
        });

    private static void Click(AudioRoom r, mob_living_carbon_human who, atom target, string button = "left")
        => r.Command(() => ClientSeam.Click(who.client, target, button == "left" ? "left=1;button=left" : "right=1;button=right"));

    public static IEnumerable<object[]> Stacks() => new[]
    {
        // type, brute healed, burn healed (of 20 each on the arm: an arm takes 50 in all), other_delay in seconds
        new object[] { typeof(obj_item_stack_medical_bruise_pack), 20f, 0f, 2 },
        new object[] { typeof(obj_item_stack_medical_suture), 10f, 0f, 1 },
        new object[] { typeof(obj_item_stack_medical_suture_medicated), 15f, 0f, 1 },
        new object[] { typeof(obj_item_stack_medical_ointment), 0f, 5f, 2 },
        new object[] { typeof(obj_item_stack_medical_mesh), 0f, 10f, 1 },
        new object[] { typeof(obj_item_stack_medical_mesh_advanced), 0f, 15f, 1 },
        new object[] { typeof(obj_item_stack_medical_aloe), 3f, 3f, 1 },
        new object[] { typeof(obj_item_stack_medical_poultice), 10f, 10f, 1 },
        new object[] { typeof(obj_item_stack_medical_bandage), 20f, 0f, 1 },
    };

    [Theory]
    [MemberData(nameof(Stacks))]
    public void EachStackHealsTgsAmountOnTheAimedLimbAfterItsDelay(Type stackType, float brute, float burn, int delay)
    {
        var r = Room(7101);
        mob_living_carbon_human medic = r.Join(5, 5, "Medic");
        mob_living_carbon_human victim = r.Dummy(6, 5);
        obj_item_stack_medical stack = r.In(() =>
        {
            Waiting.NoSleep(Part(victim, BODY_ZONE_L_ARM).receive_damage(20, 20, wound_bonus: CANT_WOUND));
            var s = (obj_item_stack_medical)Dm.@new(stackType, medic.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(medic.put_in_active_hand(s))));
            if (s is obj_item_stack_medical_mesh mesh)
                Waiting.NoSleep(mesh.attack_self(medic)); // a full pack is sealed: tear it open first
            medic.zone_selected = BODY_ZONE_L_ARM;
            return s;
        });
        float amountBefore = r.In(() => Dm.num(stack.amount));
        float oxyBefore = r.In(() => Dm.num(victim.get_oxy_loss()));
        r.Sounds.Heard.Clear();

        Click(r, medic, victim);
        r.Step(delay * Second - 1);
        r.In(() =>
        {
            Assert.Equal(20f, Dm.num(Part(victim, BODY_ZONE_L_ARM).brute_dam), 3);
            Assert.Equal(20f, Dm.num(Part(victim, BODY_ZONE_L_ARM).burn_dam), 3);
        });
        r.Step(2);
        r.In(() =>
        {
            obj_item_bodypart arm = Part(victim, BODY_ZONE_L_ARM);
            Assert.Equal(20f - brute, Dm.num(arm.brute_dam), 3);
            Assert.Equal(20f - burn, Dm.num(arm.burn_dam), 3);
            if (!QDELETED(stack))
                Assert.Equal(amountBefore - 1, Dm.num(stack.amount));
            if (stack is obj_item_stack_medical_poultice) // post_heal_effects: the poultice's shortness of breath, as much as it healed
                Assert.Equal(oxyBefore + brute + burn, Dm.num(victim.get_oxy_loss()), 3);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABruisePackOnOneselfTakesTheSelfDelay()
    {
        var r = Room(7102);
        mob_living_carbon_human medic = r.Join(5, 5, "Medic");
        r.In(() => Waiting.NoSleep(Part(medic, BODY_ZONE_R_LEG).receive_damage(20, 0, wound_bonus: CANT_WOUND)));
        Hold<obj_item_stack_medical_bruise_pack>(r, medic, BODY_ZONE_R_LEG);

        Click(r, medic, medic);
        r.Step(4 * Second - 1); // self_delay 4 s
        r.In(() => Assert.Equal(20f, Dm.num(Part(medic, BODY_ZONE_R_LEG).brute_dam), 3));
        r.Step(2);
        r.In(() => Assert.Equal(0f, Dm.num(Part(medic, BODY_ZONE_R_LEG).brute_dam), 3));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASutureTakesItsStopBleedingOffASlashsFlowWithTgsSounds()
    {
        var r = Room(7103);
        mob_living_carbon_human medic = r.Join(5, 5, "Medic");
        mob_living_carbon_human victim = r.Dummy(6, 5);
        (datum_wound treated, datum_wound control) = r.In(() =>
        {
            var left = (datum_wound)Waiting.NoSleep(victim.cause_wound_of_type_and_severity(WOUND_SLASH, Part(victim, BODY_ZONE_L_ARM), WOUND_SEVERITY_MODERATE));
            var right = (datum_wound)Waiting.NoSleep(victim.cause_wound_of_type_and_severity(WOUND_SLASH, Part(victim, BODY_ZONE_R_ARM), WOUND_SEVERITY_MODERATE));
            return (left, right);
        });
        Hold<obj_item_stack_medical_suture>(r, medic, BODY_ZONE_L_ARM);
        r.Sounds.Heard.Clear();
        (float t0, float c0) = r.In(() => (Dm.num(treated.blood_flow), Dm.num(control.blood_flow)));

        Click(r, medic, victim);
        r.Step(Second + 1); // other_delay 1 s
        r.In(() =>
        {
            // the untreated arm's flow changed by its own clotting only; the sutured arm by that and stop_bleeding 0.5
            float treatedDrop = t0 - Dm.num(treated.blood_flow);
            float controlDrop = c0 - Dm.num(control.blood_flow);
            Assert.Equal(0.5f, treatedDrop - controlDrop, 3);
        });
        Assert.Contains(r.Sounds.Heard, h => h.File.StartsWith("sound/items/suture/", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void OintmentDressesABurnWoundAndASealedMeshMustBeTornOpenFirst()
    {
        var r = Room(7104);
        mob_living_carbon_human medic = r.Join(5, 5, "Medic");
        mob_living_carbon_human victim = r.Dummy(6, 5);
        var feed = new Feed();
        PresentationSinks.Of(r.World).Messages = feed;
        (datum_wound_burn_flesh treated, datum_wound_burn_flesh control) = r.In(() =>
        {
            var left = (datum_wound_burn_flesh)Waiting.NoSleep(victim.cause_wound_of_type_and_severity(WOUND_BURN, Part(victim, BODY_ZONE_L_ARM), WOUND_SEVERITY_MODERATE));
            var right = (datum_wound_burn_flesh)Waiting.NoSleep(victim.cause_wound_of_type_and_severity(WOUND_BURN, Part(victim, BODY_ZONE_R_ARM), WOUND_SEVERITY_MODERATE));
            return (left, right);
        });

        // a full mesh pack is sealed: it refuses to heal until used in the hand, which tears it open with tg's sound
        var mesh = Hold<obj_item_stack_medical_mesh>(r, medic, BODY_ZONE_L_ARM);
        r.In(() =>
        {
            Assert.Equal(FALSE, mesh.is_open);
            Assert.Equal("regen_mesh_closed", Dm.str(mesh.icon_state));
        });
        Click(r, medic, victim);
        r.Step(2 * Second);
        Assert.Contains(feed.Balloons, b => b.Text == "open it first!");
        r.In(() => Assert.Equal(15f, Dm.num(mesh.amount)));
        r.Sounds.Heard.Clear();
        r.Command(() => ClientSeam.Keybinding(medic.client, 4, true)); // activate_inhand (Z): attack_self
        r.Step();
        r.In(() =>
        {
            Assert.Equal(TRUE, mesh.is_open);
            Assert.NotEqual("regen_mesh_closed", Dm.str(mesh.icon_state));
            Waiting.NoSleep(medic.dropItemToGround(mesh));
        });
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/poster/poster_ripped.ogg");

        // ointment: flesh_regeneration 2.5 and sanitization 0.25 onto the burn (the other arm's burn processes alike, untreated)
        var ointment = Hold<obj_item_stack_medical_ointment>(r, medic, BODY_ZONE_L_ARM);
        Click(r, medic, victim);
        float t_heal = 0, t_san = 0, c_heal = 0, c_san = 0;
        bool applied = false;
        for (int i = 0; i < 4 * Second && !applied; i++)
        {
            (t_heal, t_san, c_heal, c_san) = r.In(() => (Dm.num(treated.flesh_healing), Dm.num(treated.sanitization), Dm.num(control.flesh_healing), Dm.num(control.sanitization)));
            r.Step();
            applied = r.In(() => Dm.num(ointment.amount) < 8);
        }
        Assert.True(applied, "the ointment was applied");
        r.In(() =>
        {
            Assert.Equal(2.5f, (Dm.num(treated.flesh_healing) - t_heal) - (Dm.num(control.flesh_healing) - c_heal), 3);
            Assert.Equal(0.25f, (Dm.num(treated.sanitization) - t_san) - (Dm.num(control.sanitization) - c_san), 3);
        });
        r.AssertNoRuntimes();
    }


    [Fact]
    public void BoneGelThenSurgicalTapeSetABrokenBone()
    {
        var r = Room(7105);
        mob_living_carbon_human medic = r.Join(5, 5, "Medic");
        mob_living_carbon_human victim = r.Dummy(6, 5);
        var bone = (datum_wound_blunt_bone)r.In(() => Waiting.NoSleep(victim.cause_wound_of_type_and_severity(WOUND_BLUNT, Part(victim, BODY_ZONE_L_ARM), WOUND_SEVERITY_SEVERE)));
        var gel = Hold<obj_item_stack_medical_bone_gel>(r, medic, BODY_ZONE_L_ARM);
        // the wound's determination heals a wounded limb by 0.167 brute a second on each life tick (other_reagents.dm:3159), so the
        // gel's 25 would be read short whenever a tick falls between the gel and the check (gaps train 12: SSdisease moved the phase)
        r.In(() => Waiting.NoSleep(((datum_reagents)victim.reagents).remove_reagent(typeof(datum_reagent_determination), 100)));
        float bruteBefore = r.In(() => Dm.num(Part(victim, BODY_ZONE_L_ARM).brute_dam));

        Click(r, medic, victim);
        r.Step(r.In(() => (int)(Dm.num(bone.base_treat_time) * 1.5f / 10 * Second)) + 2); // base_treat_time * 1.5 on another
        r.In(() =>
        {
            Assert.Equal(TRUE, bone.gelled);
            Assert.Equal(4f, Dm.num(gel.amount));
            Assert.Equal(bruteBefore + 25, Dm.num(Part(victim, BODY_ZONE_L_ARM).brute_dam), 3);
            Waiting.NoSleep(medic.dropItemToGround(gel));
        });

        var tape = Hold<obj_item_stack_medical_wrap_sticky_tape_surgical>(r, medic, BODY_ZONE_L_ARM);
        Click(r, medic, victim);
        r.Step(r.In(() => (int)(Dm.num(bone.base_treat_time) / 10 * Second)) + 2);
        r.In(() =>
        {
            Assert.Equal(TRUE, bone.taped);
            Assert.Equal(TRUE, bone.processes);
            Assert.Equal(4f, Dm.num(tape.amount));
        });
        float ticksBefore = r.In(() => Dm.num(bone.regen_ticks_current));
        r.Step(6 * Second);
        r.In(() => Assert.True(Dm.num(bone.regen_ticks_current) > ticksBefore, "the gelled, taped bone reforms"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ATourniquetKeepsATenthOfAnArmsBleedingAndSlowsTheArm()
    {
        var r = Room(7106);
        mob_living_carbon_human medic = r.Join(5, 5, "Medic");
        mob_living_carbon_human victim = r.Join(6, 5, "Victim");
        r.In(() => Waiting.NoSleep(victim.cause_wound_of_type_and_severity(WOUND_SLASH, Part(victim, BODY_ZONE_L_ARM), WOUND_SEVERITY_SEVERE)));
        var tourniquet = Hold<obj_item_tourniquet>(r, medic, BODY_ZONE_L_ARM);
        float bleedBefore = r.In(() => Dm.num(Part(victim, BODY_ZONE_L_ARM).refresh_bleed_rate()));
        Assert.True(bleedBefore > 0);

        Click(r, medic, victim);
        r.Step(5 * Second / 2 + 2); // 5 s * 1 (a bleeding wound) * 0.5 (on another)
        r.In(() =>
        {
            obj_item_bodypart_arm arm = (obj_item_bodypart_arm)Part(victim, BODY_ZONE_L_ARM);
            Assert.Same(tourniquet, LAZYACCESS(arm.applied_items, LIMB_ITEM_TOURNIQUET));
            Assert.Equal(Dm.num(arm.refresh_bleed_rate()), Dm.num(arm.cached_bleed_rate), 4);
            Assert.True(Dm.num(arm.cached_bleed_rate) <= bleedBefore * 0.1f + 0.001f, "a tourniquet keeps a tenth of the bleeding");
            Assert.Equal(0.5f, arm.interaction_modifier);
            Assert.Equal(1.5f, arm.click_cd_modifier);
            Assert.True(Dm.truthy(victim.has_status_effect(typeof(datum_status_effect_arm_speed_penalty))));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void CprPumpsTakeSevenOxygenDamageOffUntilThePatientIsOutOfCrit()
    {
        var r = Room(7107);
        mob_living_carbon_human medic = r.Join(5, 5, "Medic");
        mob_living_carbon_human patient = r.Dummy(6, 5);
        var feed = new Feed();
        PresentationSinks.Of(r.World).Messages = feed;
        r.In(() =>
        {
            Waiting.NoSleep(patient.set_oxy_loss(110));
            Assert.True(Dm.num(patient.health) <= Dm.num(patient.crit_threshold));
            Assert.Equal(LYING_DOWN, patient.body_position);
            medic.zone_selected = BODY_ZONE_CHEST;
        });

        Click(r, medic, patient);
        r.Step(3 * Second + 2); // the first pump after 3 s
        Assert.Contains(feed.To(medic), h => h.Contains("You perform CPR on", StringComparison.Ordinal));
        Assert.Contains(feed.To(medic), h => h.Contains("still isn't up! You try harder!", StringComparison.Ordinal));
        r.Step(10 * Second); // the panicked pumps (CPR_PANIC_SPEED) until the patient is out of crit
        r.In(() =>
        {
            Assert.True(Dm.num(patient.health) > Dm.num(patient.crit_threshold), "CPR brings the patient out of crit");
            Assert.True(Dm.num(patient.get_oxy_loss()) <= 100);
        });
        Assert.True(feed.To(medic).Count(h => h.Contains("You perform CPR on", StringComparison.Ordinal)) >= 2);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheStethoscopeHearsNoBeatOnAStoppedHeartAndAHealthyOne()
    {
        var r = Room(7108);
        mob_living_carbon_human medic = r.Join(5, 5, "Medic");
        mob_living_carbon_human patient = r.Dummy(6, 5);
        var feed = new Feed();
        PresentationSinks.Of(r.World).Messages = feed;
        Hold<obj_item_clothing_neck_stethoscope>(r, medic, BODY_ZONE_CHEST);

        Click(r, medic, patient);
        r.Step(Second); // the click cooldown (CLICK_CD_MELEE) passes before the next
        string healthy = feed.To(medic).Last(h => h.Contains("boxed_message", StringComparison.Ordinal));
        Assert.Contains("breathing normally", healthy);
        Assert.Contains("lungs emit", healthy);
        Assert.Contains("You hear a healthy heartbeat.", healthy);
        Assert.Contains("heart produces a rhythmic thumping", healthy);

        r.In(() => ((obj_item_organ_heart)patient.get_organ_slot(ORGAN_SLOT_HEART)).Stop());
        Click(r, medic, patient);
        r.Step(Second); // the click cooldown (CLICK_CD_MELEE) passes before the next
        string stopped = feed.To(medic).Last(h => h.Contains("boxed_message", StringComparison.Ordinal));
        Assert.Contains("You don't hear a heartbeat!", stopped);
        Assert.DoesNotContain("heart produces", stopped);

        r.In(() => medic.zone_selected = BODY_ZONE_L_ARM);
        Click(r, medic, patient);
        r.Step(Second); // the click cooldown (CLICK_CD_MELEE) passes before the next
        Assert.Contains("You can't find a pulse!", feed.To(medic).Last(h => h.Contains("boxed_message", StringComparison.Ordinal)));
        Assert.DoesNotContain(feed.Chat, c => ReferenceEquals(c.Target, patient) && c.Html.Contains("boxed_message", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHealthAnalyzerScansWithItsFlickAndSoundAndOnlyItsUserGetsTheReadout()
    {
        var r = Room(7109);
        mob_living_carbon_human medic = r.Join(5, 5, "Medic");
        mob_living_carbon_human patient = r.Join(6, 5, "Patient");
        var feed = new Feed();
        PresentationSinks.Of(r.World).Messages = feed;
        var flicks = new FlickRecorder();
        PresentationSinks.Of(r.World).Appearance = flicks;
        r.In(() =>
        {
            Waiting.NoSleep(Part(patient, BODY_ZONE_L_ARM).receive_damage(12, 0, wound_bonus: CANT_WOUND));
            Waiting.NoSleep(patient.cause_wound_of_type_and_severity(WOUND_SLASH, Part(patient, BODY_ZONE_R_LEG), WOUND_SEVERITY_MODERATE));
            Waiting.NoSleep(((datum_reagents)patient.reagents).add_reagent(typeof(datum_reagent_water), 10));
            Waiting.NoSleep(((obj_item_organ)patient.get_organ_slot(ORGAN_SLOT_LIVER)).set_organ_damage(20)); // over its low threshold
        });
        var analyzer = Hold<obj_item_healthanalyzer>(r, medic, BODY_ZONE_CHEST);
        r.Sounds.Heard.Clear();

        Click(r, medic, patient);
        r.Step(Second); // the click cooldown (CLICK_CD_MELEE) passes before the next
        Assert.Contains(flicks.Flicks, f => ReferenceEquals(f.Target, analyzer) && Dm.eq(f.State, "health-scan"));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/healthanalyzer.ogg");
        string scan = feed.To(medic).Last(h => h.Contains("boxed_message blue_box", StringComparison.Ordinal));
        Assert.Contains("Analyzing results for", scan);
        Assert.Contains("Organ status:", scan); // the table shows the organs with a status
        Assert.Contains("Mildly Damaged", scan);
        Assert.DoesNotContain(feed.Chat, c => !ReferenceEquals(c.Target, medic) && !(c.Target is client cl && ReferenceEquals(cl.mob, medic)) && c.Html.Contains("blue_box", StringComparison.Ordinal));

        // the wound mode (attack_self) and the chemical scan (the right click)
        r.Command(() => ClientSeam.Keybinding(medic.client, 4, true)); // activate_inhand (Z): attack_self
        r.Step();
        Assert.Contains(feed.To(medic), h => h.Contains("report extra info on wounds", StringComparison.Ordinal));
        Click(r, medic, patient);
        r.Step(Second); // the click cooldown (CLICK_CD_MELEE) passes before the next
        Assert.Contains("Physical trauma detected in", feed.To(medic).Last(h => h.Contains("blue_box", StringComparison.Ordinal)));
        Click(r, medic, patient, "right");
        r.Step(2);
        string chems = feed.To(medic).Last(h => h.Contains("blue_box", StringComparison.Ordinal));
        Assert.Contains("units of Water", chems);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheWoundAnalyzerPingsAtAWoundlessPersonAndMarksScannedWounds()
    {
        var r = Room(7110);
        mob_living_carbon_human medic = r.Join(5, 5, "Medic");
        mob_living_carbon_human patient = r.Dummy(6, 5);
        var feed = new Feed();
        PresentationSinks.Of(r.World).Messages = feed;
        var flicks = new FlickRecorder();
        PresentationSinks.Of(r.World).Appearance = flicks;
        var analyzer = Hold<obj_item_healthanalyzer_simple>(r, medic, BODY_ZONE_CHEST);
        r.Sounds.Heard.Clear();

        Click(r, medic, patient);
        r.Step(Second); // the click cooldown (CLICK_CD_MELEE) passes before the next
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/ping.ogg");
        Assert.Contains(feed.To(medic), h => h.Contains("has no wounds!", StringComparison.Ordinal));
        Assert.Contains(flicks.Flicks, f => ReferenceEquals(f.Target, analyzer) && Dm.eq(f.State, "first_aid_pinprick"));
        r.In(() => Assert.Equal("happy", analyzer.emotion));
        r.Step(3 * Second);
        r.In(() => Assert.Equal("neutral", analyzer.emotion)); // back after 2 s

        datum_wound cut = r.In(() => (datum_wound)Waiting.NoSleep(patient.cause_wound_of_type_and_severity(WOUND_SLASH, Part(patient, BODY_ZONE_L_ARM), WOUND_SEVERITY_MODERATE)));
        Click(r, medic, patient);
        r.Step(Second); // the click cooldown (CLICK_CD_MELEE) passes before the next
        Assert.Contains(feed.To(medic), h => h.Contains("bright holo-images", StringComparison.Ordinal));
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(cut, TRAIT_WOUND_SCANNED));
            Assert.Equal("cautious", analyzer.emotion);
        });
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/beep/twobeep.ogg");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALitPenlightExaminesTheEyesAndTheMouth()
    {
        var r = Room(7111);
        mob_living_carbon_human medic = r.Join(5, 5, "Medic");
        mob_living_carbon_human patient = r.Dummy(6, 5);
        var feed = new Feed();
        PresentationSinks.Of(r.World).Messages = feed;
        var pen = Hold<obj_item_flashlight_pen>(r, medic, BODY_ZONE_PRECISE_EYES);
        r.Command(() => ClientSeam.Keybinding(medic.client, 4, true)); // activate_inhand (Z): attack_self // turn it on
        r.Step();
        r.In(() => Assert.Equal(TRUE, pen.light_on));

        Click(r, medic, patient);
        r.Step(Second); // the click cooldown (CLICK_CD_MELEE) passes before the next
        Assert.Contains("narrow.", feed.To(medic).Last(h => h.Contains("boxed_message", StringComparison.Ordinal)));

        r.In(() =>
        {
            Waiting.NoSleep(patient.set_oxy_loss(30));
            medic.zone_selected = BODY_ZONE_PRECISE_MOUTH;
        });
        Click(r, medic, patient);
        r.Step(Second); // the click cooldown (CLICK_CD_MELEE) passes before the next
        string mouth = feed.To(medic).Last(h => h.Contains("boxed_message", StringComparison.Ordinal));
        Assert.Contains("lips appear blue!", mouth);
        Assert.Contains("Color returns quickly.", mouth);
        r.AssertNoRuntimes();
    }
}
