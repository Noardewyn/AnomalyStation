using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Syndicate;

/// <summary>
/// Syndicate stage X3b (D316): the other martial arts. CQC's manual teaches it and Shove Shove Punch is its Consecutive CQC; boxing
/// gloves make two boxers whose punches are stamina; the Kaza Ruk implant grants the stances and the Neck Chop silences; the Plasma
/// Fist's Tornado Sweep repulses and its Plasma Fist gibs and powers up the Apotheosis; the Spider's Bite gives the tackle and kicks a
/// staggered target; the violent psychosis teaches the Psychotic Brawling, the odd mushroom the Mushroom Punch, the chef's chip Close
/// Quarters Cooking (only in a kitchen). Wrestling stays out (D80: a Goonstation-derived file).
/// </summary>
public class SyndicateOtherMartialArtsTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob, mob_living_carbon_human Alice) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        mob_living_carbon_human alice = r.Join(4, 4, "Alice");
        r.In(() => Waiting.NoSleep(bob.equipOutfit(typeof(datum_outfit_job_assistant))));
        r.In(() => Waiting.NoSleep(alice.equipOutfit(typeof(datum_outfit_job_assistant))));
        r.Step(Second);
        return (r, feed, bob, alice);
    }

    private static T Read<T>(AudioRoom r, mob_living_carbon_human bob) where T : obj_item_book_granter
    {
        var book = r.In(() =>
        {
            var b = Dm.@new<T>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(b));
            return b;
        });
        r.In(() => Waiting.Detach(book.attack_self(bob)));
        r.Step(30 * Second); // the pages and the reading time
        return book;
    }

    private static datum_martial_art Art(AudioRoom r, mob_living who) => r.In(() => LAZYACCESS(who.martial_arts, 1) as datum_martial_art);

    private static void Hit(AudioRoom r, mob_living_carbon_human bob, mob_living alice, bool shove)
    {
        DmList modifiers = r.In(() =>
        {
            var m = Dm.list();
            if (shove)
                m[RIGHT_CLICK] = TRUE;
            return m;
        });
        r.In(() => Waiting.NoSleep(bob.UnarmedAttack(alice, TRUE, modifiers)));
        r.Step(Second); // CLICK_CD_MELEE
    }

    [Fact]
    public void TheOtherArtsAndTheirSourcesAreLiveButWrestling()
    {
        foreach (Type type in new[]
        {
            typeof(datum_martial_art_cqc), typeof(datum_martial_art_cqc_under_siege), typeof(datum_martial_art_boxing),
            typeof(datum_martial_art_boxing_evil), typeof(datum_martial_art_boxing_hunter), typeof(datum_martial_art_kaza_ruk),
            typeof(datum_martial_art_plasma_fist), typeof(datum_martial_art_plasma_fist_nobomb), typeof(datum_martial_art_spiders_bite),
            typeof(datum_martial_art_jungle_arts), typeof(datum_martial_art_mushpunch), typeof(datum_martial_art_psychotic_brawling),
            typeof(obj_item_book_granter_martial_cqc), typeof(obj_item_book_granter_martial_plasma_fist), typeof(obj_item_book_granter_martial_plasma_fist_nobomb),
            typeof(obj_item_book_granter_martial_spider_bite), typeof(obj_item_implant_kaza_ruk), typeof(obj_item_implanter_kaza_ruk), typeof(obj_item_implantcase_kaza_ruk),
            typeof(obj_item_clothing_gloves_boxing), typeof(obj_item_clothing_gloves_boxing_evil), typeof(obj_item_clothing_gloves_boxing_golden),
            typeof(obj_item_clothing_gloves_fingerless_punch_mitts), typeof(obj_item_clothing_gloves_kaza_ruk_sec), typeof(obj_item_clothing_gloves_kaza_ruk_combatglovesplus),
            typeof(obj_item_mushpunch), typeof(datum_action_neck_chop), typeof(datum_action_low_sweep), typeof(datum_action_lung_punch),
            typeof(datum_action_cooldown_spell_aoe_repulse), typeof(datum_action_cooldown_spell_aoe_repulse_wizard), typeof(datum_action_cooldown_spell_aoe_repulse_xeno),
            typeof(datum_brain_trauma_special_psychotic_brawling), typeof(datum_brain_trauma_special_psychotic_brawling_bath_salts),
            typeof(obj_effect_temp_visual_crit), typeof(obj_effect_temp_visual_plasma_soul), typeof(datum_status_effect_rebuked), typeof(obj_item_skillchip_job_chef),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
        Assert.True(Attribute.IsDefined(typeof(datum_martial_art_wrestling), typeof(DmInertAttribute), inherit: false), "wrestling.dm is Goon-derived (D80)");
    }

    [Fact]
    public void TheCqcManualTeachesCqcAndShoveShovePunchIsConsecutiveCqc()
    {
        var (r, feed, bob, alice) = Room(131);
        var manual = Read<obj_item_book_granter_martial_cqc>(r, bob);
        Assert.True(feed.Any("You've mastered the basics of CQC."));
        Assert.True(feed.Any("beeps ominously"));
        Assert.IsType<datum_martial_art_cqc>(Art(r, bob));
        r.In(() => bob.set_combat_mode(TRUE));
        float before = r.In(() => Dm.num(alice.get_stamina_loss()));
        Hit(r, bob, alice, shove: true);
        Hit(r, bob, alice, shove: true);
        Assert.Equal("DD", Art(r, bob).streak);
        Hit(r, bob, alice, shove: false);
        Assert.True(feed.Any($"You strike {r.In(() => alice.name)}'s abdomen, neck and back consecutively!"), "no Consecutive CQC");
        float after = r.In(() => Dm.num(alice.get_stamina_loss()));
        Assert.True(after >= before + 45, $"too little stamina damage: {before} -> {after}"); // 50, less a second of regeneration
        Assert.Equal("", Art(r, bob).streak);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void BoxingGlovesMakeBoxersWhosePunchesAreStamina()
    {
        var (r, feed, bob, alice) = Room(132);
        foreach (var who in new[] { bob, alice })
        {
            r.In(() => Waiting.NoSleep(who.dropItemToGround(who.gloves as obj_item)));
            r.In(() => Waiting.NoSleep(who.equip_to_slot_or_del(Dm.@new<obj_item_clothing_gloves_boxing>(who.loc), ITEM_SLOT_GLOVES)));
        }
        r.Step(Second);
        Assert.IsType<datum_martial_art_boxing>(Art(r, bob));
        Assert.True(r.In(() => HAS_TRAIT(alice, TRAIT_BOXING_READY)));
        r.In(() => bob.set_combat_mode(TRUE));
        float brute = r.In(() => Dm.num(alice.get_brute_loss()));
        float stamina = r.In(() => Dm.num(alice.get_stamina_loss()));
        Hit(r, bob, alice, shove: false);
        Assert.True(feed.Any($"You punched {r.In(() => alice.name)}!"), "no boxing punch");
        Assert.True(r.In(() => Dm.num(alice.get_stamina_loss())) > stamina, "the punch did no stamina damage");
        Assert.Equal(brute, r.In(() => Dm.num(alice.get_brute_loss())));
        Assert.Equal("H", Art(r, bob).streak);
        // off come the gloves: no more boxing
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(bob.gloves as obj_item)));
        r.Step(Second);
        Assert.Null(Art(r, bob));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheKazaRukImplantGrantsTheStancesAndTheNeckChopSilences()
    {
        var (r, feed, bob, alice) = Room(133);
        var implant = r.In(() => Dm.@new<obj_item_implant_kaza_ruk>());
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(implant.implant(bob, bob)))));
        r.In(() => Waiting.NoSleep(implant.activate()));
        Assert.IsType<datum_martial_art_kaza_ruk>(Art(r, bob));
        Assert.True(feed.Any("You know the arts of Kaza Ruk!"));
        var chop = r.In(() => Dm.locate<datum_action_neck_chop>(bob.actions));
        Assert.NotNull(chop);
        Assert.NotNull(r.In(() => Dm.locate<datum_action_low_sweep>(bob.actions)));
        Assert.NotNull(r.In(() => Dm.locate<datum_action_lung_punch>(bob.actions)));
        r.In(() => Waiting.NoSleep(chop.Trigger(bob)));
        Assert.Equal("neck_chop", Art(r, bob).streak);
        r.In(() => bob.set_combat_mode(TRUE));
        Hit(r, bob, alice, shove: false);
        Assert.True(feed.Any($"You karate chop {r.In(() => alice.name)}'s neck"), "no Neck Chop");
        Assert.True(r.In(() => Dm.truthy(alice.has_status_effect(typeof(datum_status_effect_silenced)))));
        // the implant's button again: the art is forgotten with its stances
        r.In(() => Waiting.NoSleep(implant.activate()));
        Assert.Null(Art(r, bob));
        Assert.Null(r.In(() => Dm.locate<datum_action_neck_chop>(bob.actions)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePlasmaFistsTornadoSweepRepulses()
    {
        var (r, feed, bob, alice) = Room(134);
        var scroll = Read<obj_item_book_granter_martial_plasma_fist>(r, bob);
        Assert.IsType<datum_martial_art_plasma_fist>(Art(r, bob));
        Assert.Equal("empty scroll", r.In(() => scroll.name));
        r.In(() => bob.set_combat_mode(TRUE));
        Hit(r, bob, alice, shove: false);
        Hit(r, bob, alice, shove: false);
        Hit(r, bob, alice, shove: true);
        Assert.True(feed.Any("TORNADO SWEEP!"), "no Tornado Sweep");
        Assert.True(feed.Any($"You're thrown back by {r.In(() => bob.name)}!"), "Alice was not repulsed");
        Assert.True(r.In(() => Dm.truthy(alice.IsParalyzed())));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePlasmaFistGibsAndPowersUpTheApotheosis()
    {
        var (r, feed, bob, alice) = Room(135);
        Read<obj_item_book_granter_martial_plasma_fist>(r, bob);
        r.In(() => bob.set_combat_mode(TRUE));
        foreach (bool shove in new[] { false, true, true, true, false })
            Hit(r, bob, alice, shove);
        Assert.True(feed.Any("THE PLASMA FIST TECHNIQUE"), "no Plasma Fist");
        Assert.True(r.In(() => QDELETED(alice)), "Alice was not gibbed");
        Assert.Equal(2f, ((datum_martial_art_plasma_fist)Art(r, bob)).plasma_power); // Alice had a client
        Assert.True(feed.Any("is now at power level 2"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSpidersBiteGivesTheTackleAndKicksAStaggeredTarget()
    {
        var (r, feed, bob, alice) = Room(136);
        Read<obj_item_book_granter_martial_spider_bite>(r, bob);
        var art = Art(r, bob);
        Assert.IsType<datum_martial_art_spiders_bite>(art);
        Assert.NotNull(r.In(() => bob.GetComponent(typeof(datum_component_tackler))));
        Assert.Null(r.In(() => art.get_attacking_limb(bob, alice)));
        r.In(() => Waiting.NoSleep(alice.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH, 10 * SECONDS)));
        var limb = r.In(() => art.get_attacking_limb(bob, alice));
        Assert.IsAssignableFrom<obj_item_bodypart_leg>(limb);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheViolentPsychosisTheMushroomAndTheChefsChipTeachTheirArts()
    {
        var (r, feed, bob, alice) = Room(137);
        // the violent psychosis: a locked Psychotic Brawling, gone with the trauma
        r.In(() => Waiting.NoSleep(bob.gain_trauma(typeof(datum_brain_trauma_special_psychotic_brawling), TRAUMA_RESILIENCE_ABSOLUTE)));
        var psycho = Art(r, bob);
        Assert.IsType<datum_martial_art_psychotic_brawling>(psycho);
        Assert.True(r.In(() => Dm.truthy(psycho.locked_to_use)));
        r.In(() => bob.cure_trauma_type(typeof(datum_brain_trauma_special_psychotic_brawling), TRAUMA_RESILIENCE_ABSOLUTE));
        r.Step(Second);
        Assert.Null(Art(r, bob));

        // the odd mushroom: eaten, it teaches the Mushroom Punch
        var mushroom = r.In(() =>
        {
            var m = Dm.@new<obj_item_mushpunch>(alice.loc);
            Waiting.NoSleep(alice.put_in_active_hand(m));
            return m;
        });
        r.In(() => Waiting.NoSleep(mushroom.attack_self(alice)));
        Assert.IsType<datum_martial_art_mushpunch>(Art(r, alice));
        Assert.True(r.In(() => QDELETED(mushroom)));
        Assert.True(feed.Any("You devour"));

        // the chef's chip: Close Quarters Cooking, which works only in a kitchen
        var chip = r.In(() => Dm.@new<obj_item_skillchip_job_chef>(bob.loc));
        Assert.IsType<datum_martial_art_cqc_under_siege>(r.In(() => chip.style));
        r.In(() => Waiting.NoSleep(chip.on_activate(bob, TRUE)));
        var cooking = Art(r, bob);
        Assert.IsType<datum_martial_art_cqc_under_siege>(cooking);
        Assert.False(r.In(() => Dm.truthy(cooking.can_use(bob))), "the test room is no kitchen");
        r.In(() => Waiting.NoSleep(chip.on_deactivate(bob, TRUE)));
        Assert.Null(Art(r, bob));
        r.AssertNoRuntimes();
    }
}
