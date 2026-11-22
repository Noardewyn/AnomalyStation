using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M8;

/// <summary>
/// M8 slice F: tg's atom HUDs (datums/hud.dm, game/data_huds.dm) in a running world with real clients. HUD glasses give their wearer the
/// HUD trait and with it the HUD's images in his client.images (and nobody else's); the health bar follows the patient's health; the
/// toggle takes the images away and brings them back; an EMP scrambles the display; a mindshield implant shows its icon to a security
/// HUD; a skillchip grants its traits when activated and takes them away when cut out of the brain.
/// </summary>
public class HudTests
{
    private const int Second = 20;

    private static List<string> StatesIn(client c) => Dm.each<image>(c.images).Select(i => i.icon_state).ToList();

    private static image Bar(mob_living who, string category) => who.hud_list[category] as image;

    private static (AudioRoom R, mob_living_carbon_human Alice, mob_living_carbon_human Bob, mob_living_carbon_human Carol) Rig(ulong seed)
    {
        var r = new AudioRoom(13, 10, seed: seed);
        var alice = r.Join(4, 5, "Alice");
        var bob = r.Join(6, 5, "Bob");
        var carol = r.Join(8, 5, "Carol");
        r.Step(Second);
        return (r, alice, bob, carol);
    }

    private static void Wear(AudioRoom r, mob_living_carbon_human who, obj_item item, float slot)
    {
        r.In(() => Waiting.NoSleep(who.equip_to_slot(item, slot)));
        r.Step(2);
    }

    [Fact]
    public void MedicalHudGlassesShowTheHealthBarAndStatusOfEveryoneToTheirWearerAlone()
    {
        var (r, alice, bob, carol) = Rig(8101);
        r.In(() =>
        {
            Assert.DoesNotContain(StatesIn(alice.client), s => s.StartsWith("hud", StringComparison.Ordinal) && s != "hudno_id");
            Assert.Equal("hudhealthy", Bar(bob, STATUS_HUD).icon_state);
            Assert.Equal("health100", Bar(bob, HEALTH_HUD).icon_state[3..]); // "hud" + RoundHealth
        });
        var glasses = r.In(() => Dm.@new<obj_item_clothing_glasses_hud_health>(alice.loc));
        Wear(r, alice, glasses, ITEM_SLOT_EYES);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(alice, TRAIT_MEDICAL_HUD));
            DmList seen = alice.client.images;
            Assert.Contains(Bar(bob, HEALTH_HUD), Dm.each(seen));
            Assert.Contains(Bar(bob, STATUS_HUD), Dm.each(seen));
            Assert.Contains(Bar(carol, HEALTH_HUD), Dm.each(seen));
            // not for the one without the glasses (D40: client.images is per client)
            Assert.DoesNotContain(Bar(alice, HEALTH_HUD), Dm.each(bob.client.images));
            Assert.DoesNotContain(Bar(bob, HEALTH_HUD), Dm.each(carol.client.images));
        });

        // the bar drops as the patient is hurt, and the status shows a dead one
        r.In(() => Waiting.NoSleep(bob.adjust_brute_loss(60)));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(bob.health < 100);
            Assert.Equal("hud" + RoundHealth(bob), Bar(bob, HEALTH_HUD).icon_state);
            Assert.NotEqual("hudhealth100", Bar(bob, HEALTH_HUD).icon_state);
            Assert.Equal("hudhealthy", Bar(bob, STATUS_HUD).icon_state);
        });
        r.In(() => Waiting.NoSleep(bob.death()));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal("health-100", Bar(bob, HEALTH_HUD).icon_state[3..]);
            Assert.Equal("huddefib", Bar(bob, STATUS_HUD).icon_state); // dead with the player still in the body: tg's defib icon (carbon med_hud_set_status)
        });
        Assert.Equal(0, r.World.RuntimeErrorCount);
    }

    [Fact]
    public void TheGlassesToggleHidesTheHudAndAnEmpScramblesTheDisplay()
    {
        var (r, alice, bob, _) = Rig(8102);
        var glasses = r.In(() => Dm.@new<obj_item_clothing_glasses_hud_health>(alice.loc));
        Wear(r, alice, glasses, ITEM_SLOT_EYES);
        r.In(() =>
        {
            Assert.True(Dm.truthy(glasses.display_active));
            Assert.Contains(Bar(bob, HEALTH_HUD), Dm.each(alice.client.images));
            glasses.toggle_hud_display(alice);
            Assert.False(Dm.truthy(glasses.display_active));
            Assert.False(HAS_TRAIT(alice, TRAIT_MEDICAL_HUD));
            Assert.DoesNotContain(Bar(bob, HEALTH_HUD), Dm.each(alice.client.images));
            glasses.toggle_hud_display(alice);
            Assert.True(HAS_TRAIT(alice, TRAIT_MEDICAL_HUD));
            Assert.Contains(Bar(bob, HEALTH_HUD), Dm.each(alice.client.images));
        });
        // a second source of the trait keeps the HUD (the count of sources: hud_users_all_z_levels)
        r.In(() =>
        {
            Assert.False(Dm.truthy(glasses.obj_flags is float f && ((int)f & (int)EMAGGED) != 0));
            Waiting.NoSleep(glasses.emp_act(EMP_HEAVY));
            Assert.True(((int)Dm.num(glasses.obj_flags) & (int)EMAGGED) != 0);
            Assert.Contains("flickering", (string)glasses.desc);
        });

        // taking the glasses off takes the HUD away
        r.In(() => Waiting.NoSleep(alice.dropItemToGround(glasses)));
        r.Step(2);
        r.In(() =>
        {
            Assert.False(HAS_TRAIT(alice, TRAIT_MEDICAL_HUD));
            Assert.DoesNotContain(Bar(bob, HEALTH_HUD), Dm.each(alice.client.images));
        });
    }

    [Fact]
    public void AMindshieldShowsItsIconToASecurityHudAndTheJobIconFollowsTheId()
    {
        var (r, alice, bob, _) = Rig(8103);
        var glasses = r.In(() => Dm.@new<obj_item_clothing_glasses_hud_security>(alice.loc));
        Wear(r, alice, glasses, ITEM_SLOT_EYES);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(alice, TRAIT_SECURITY_HUD));
            bob.update_ID_card(); // what equipping or dropping an ID card runs (a new mob shows nothing until then)
            Assert.Equal("hudno_id", Bar(bob, ID_HUD).icon_state); // no ID card worn
            Assert.Contains(Bar(bob, ID_HUD), Dm.each(alice.client.images));
            Assert.DoesNotContain(IMPLOYAL_HUD, Dm.each(bob.active_hud_list));
        });
        var implant = r.In(() => Dm.@new<obj_item_implant_mindshield>(bob.loc));
        object implanted = r.In(() => Waiting.NoSleep(implant.implant(bob, alice, TRUE)));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(Dm.truthy(implanted));
            Assert.True(HAS_TRAIT(bob, TRAIT_MINDSHIELD));
            Assert.Same(bob, implant.imp_in);
            Assert.Contains(implant, Dm.each(bob.implants));
            Assert.Equal("hud_imp_loyal", Bar(bob, IMPLOYAL_HUD).icon_state);
            Assert.Contains(IMPLOYAL_HUD, Dm.each(bob.active_hud_list));
            Assert.Contains(Bar(bob, IMPLOYAL_HUD), Dm.each(alice.client.images));
            Assert.Contains(implant, Dm.each(GLOB.tracked_implants));
        });
        // cut out again: the icon goes and so does the trait
        r.In(() => Waiting.NoSleep(implant.removed(bob, TRUE)));
        r.Step(2);
        r.In(() =>
        {
            Assert.False(HAS_TRAIT(bob, TRAIT_MINDSHIELD));
            Assert.DoesNotContain(IMPLOYAL_HUD, Dm.each(bob.active_hud_list));
            Assert.DoesNotContain(Bar(bob, IMPLOYAL_HUD), Dm.each(alice.client.images));
        });
        Assert.Equal(0, r.World.RuntimeErrorCount);
    }

    [Fact]
    public void ASkillchipGrantsItsTraitsWhenActiveAndTheBrainsLimitsHold()
    {
        var (r, alice, bob, _) = Rig(8104);
        r.In(() =>
        {
            var chip = Dm.@new<obj_item_skillchip_job_engineer>(alice.loc);
            object message = Waiting.NoSleep(alice.implant_skillchip(chip));
            Assert.Null(message);
            var brain = (obj_item_organ_brain)alice.get_organ_slot(ORGAN_SLOT_BRAIN);
            Assert.Contains(chip, Dm.each(brain.skillchips));
            Assert.Same(brain, chip.holding_brain);
            Assert.False(HAS_TRAIT(alice, TRAIT_KNOW_ENGI_WIRES)); // implanted, not yet active

            Assert.Null(Waiting.NoSleep(chip.try_activate_skillchip(TRUE, TRUE)));
            Assert.True(HAS_TRAIT(alice, TRAIT_KNOW_ENGI_WIRES));
            Assert.True(Dm.truthy(chip.is_active()));
            Assert.Equal(1, Dm.num(brain.get_used_skillchip_complexity()));
            Assert.Equal(2, Dm.num(brain.get_used_skillchip_slots()));

            // a second chip of the job category is refused (tg's incompatibility lists), and a duplicate too
            var other = Dm.@new<obj_item_skillchip_job_roboticist>(alice.loc);
            Assert.Contains("Incompatible with implanted job chip", (string)Waiting.NoSleep(alice.implant_skillchip(other)));
            var again = Dm.@new<obj_item_skillchip_job_engineer>(alice.loc);
            Assert.Contains("Duplicate chip", (string)Waiting.NoSleep(alice.implant_skillchip(again)));

            // cut out: the trait goes with the chip
            Assert.True(Dm.truthy(Waiting.NoSleep(alice.remove_skillchip(chip, TRUE))));
            Assert.False(HAS_TRAIT(alice, TRAIT_KNOW_ENGI_WIRES));
            Assert.Null(chip.holding_brain);
            Assert.Empty(Dm.each(brain.skillchips));

            // a chip too complex for the brain: tg's complexity modifier lowers the room and deactivates what no longer fits
            Assert.Null(Waiting.NoSleep(alice.implant_skillchip(chip)));
            Assert.Null(Waiting.NoSleep(chip.try_activate_skillchip(TRUE, TRUE)));
            Waiting.NoSleep(alice.adjust_skillchip_complexity_modifier(-3));
            Assert.False(Dm.truthy(chip.is_active()));
            Assert.False(HAS_TRAIT(alice, TRAIT_KNOW_ENGI_WIRES));
            Assert.Contains("too complex", (string)chip.has_activate_incompatibility(brain));
        });
        Assert.Equal(0, r.World.RuntimeErrorCount);
    }
}
