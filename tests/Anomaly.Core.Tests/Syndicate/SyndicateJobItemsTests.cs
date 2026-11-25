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
/// Syndicate stage G4a (D316), the uplink's small job-locked items: the magic eightball answers after a shake and the haunted one
/// asks the ghosts, who vote in its window; the mail counterfeit device makes an armed envelope whose signaler goes off when opened,
/// a gun in traitor mail fires at its opener, a crew member's letter opens only for them, a full mail crate fills with letters;
/// the clumsiness injector and the DNA syringe gun make their target clumsy; the assistant's toolbox hits harder with telecrystals
/// inside; the hot potato sticks to its victim and blows up in their hands; the reverse bear trap locks on a head and snaps; the
/// reverse revolver shoots a non-clown in the face; the briefcase gun fires the gun inside and refuses a heavy one; the mime's
/// combat baguette, the throwing croissant and the anarchist's cookbook; a sweep spawns and deletes each newly live item.
/// </summary>
public class SyndicateJobItemsTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed = 47)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        r.In(() => Waiting.NoSleep(bob.equipOutfit(typeof(datum_outfit_job_assistant))));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static mob_living_carbon_human Alice(AudioRoom r, int x = 7, int y = 5)
    {
        mob_living_carbon_human alice = r.Join(x, y, "Alice");
        r.In(() => Waiting.NoSleep(alice.equipOutfit(typeof(datum_outfit_job_assistant))));
        r.Step(Second);
        return alice;
    }

    private static void Crew(AudioRoom r, mob who) => r.In(() => who.mind.set_assigned_role(SSjob.get_job_type(typeof(datum_job_assistant)) as datum_job));

    private static T Held<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        T item = Dm.@new<T>(who.loc);
        Waiting.NoSleep(who.put_in_active_hand(item));
        return item;
    });

    // an act in the newest window of that interface the player has open (tg's input windows: AlertModal, ListInputWindow, TextInputModal)
    private static void Act(AudioRoom r, mob who, string iface, string action, string key, object value)
    {
        r.Step(2);
        var open = r.In(() => Dm.each_cast<datum_tgui>(who.tgui_open_uis).Select(u => Dm.str(u.@interface)).ToList());
        Assert.True(open.Contains(iface), "no " + iface + " open; open: " + string.Join(", ", open));
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(who.tgui_open_uis).Last(u => Dm.str(u.@interface) == iface).window.id));
        r.Command(() => ClientSeam.TguiMessage(who.client, window, "act/" + action, Dm.list(Dm.kv(key, value))));
        r.Step(4);
    }

    /// <summary>A player who joins on (x, y) and ghosts: their client is on the observer.</summary>
    private static mob_dead_observer Ghost(AudioRoom r, int x, int y, string key)
    {
        mob_living_carbon_human body = r.Join(x, y, key);
        client player = body.client;
        r.In(() => body.ghostize(TRUE));
        r.Step(2);
        return Assert.IsType<mob_dead_observer>(player.mob);
    }

    [Fact]
    public void TheTypesAreLiveAndTheirEntriesSell()
    {
        foreach (Type type in new[]
        {
            typeof(obj_item_toy_eightball_haunted), typeof(obj_item_storage_mail_counterfeit_device), typeof(obj_item_mail_traitor), typeof(obj_item_mail),
            typeof(obj_item_dnainjector_clumsymut), typeof(obj_item_storage_toolbox_mechanical_old_clean), typeof(obj_item_hot_potato_syndicate),
            typeof(obj_item_reverse_bear_trap), typeof(obj_item_gun_syringe_dna), typeof(obj_item_gun_ballistic_revolver_reverse),
            typeof(obj_item_storage_box_hug_reverse_revolver), typeof(obj_item_storage_briefcase_gun), typeof(obj_item_storage_briefcase_gun_preloaded),
            typeof(obj_item_food_baguette_combat), typeof(obj_item_food_croissant_throwing), typeof(obj_item_book_granter_crafting_recipe_combat_baking),
            typeof(datum_element_item_scaling), typeof(obj_projectile_bullet_dnainjector), typeof(obj_item_ammo_casing_dnainjector),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
        var r = new AudioRoom(5, 5, seed: 3);
        foreach (Type entry in new[]
        {
            typeof(datum_uplink_item_role_restricted_haunted_magic_eightball), typeof(datum_uplink_item_role_restricted_mail_counterfeit_kit),
            typeof(datum_uplink_item_role_restricted_clumsinessinjector), typeof(datum_uplink_item_role_restricted_oldtoolboxclean),
            typeof(datum_uplink_item_role_restricted_explosive_hot_potato), typeof(datum_uplink_item_role_restricted_combat_baking),
            typeof(datum_uplink_item_role_restricted_reverse_bear_trap), typeof(datum_uplink_item_role_restricted_modified_syringe_gun),
            typeof(datum_uplink_item_role_restricted_reverse_revolver), typeof(datum_uplink_item_role_restricted_briefcase_gun),
            typeof(datum_uplink_item_role_restricted_briefcase_gun_with_gun),
        })
            Assert.False(r.In(() => uplink_item_hidden(Dm.@new(entry) as datum_uplink_item)), entry.Name + " is hidden");
        // the round events are not ported
        Assert.True(r.In(() => uplink_item_hidden(Dm.@new<datum_uplink_item_role_restricted_bureaucratic_error>())));
    }

    [Fact]
    public void TheEightballAnswersAndTheHauntedOneAsksTheGhosts()
    {
        var (r, feed, bob) = Room();
        var ball = Held<obj_item_toy_eightball_broken>(r, bob);
        string fixed_answer = r.In(() => Dm.str(ball.fixed_answer));
        r.In(() => Waiting.Detach(ball.attack_self(bob)));
        r.Step(2);
        Assert.True(feed.Any("You start shaking"));
        r.Step(6 * Second);
        Assert.True(feed.Any(fixed_answer), "the broken ball did not say " + fixed_answer);
        Assert.Equal(TRUE, r.In(() => ball.on_cooldown));
        r.In(() => Waiting.Detach(ball.attack_self(bob)));
        Assert.True(feed.Any("was shaken recently"));

        // the haunted ball: a ghost can vote only while it is shaken
        mob_dead_observer gina = Ghost(r, 3, 3, "Gina");
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(ball)));
        var haunted = Held<obj_item_toy_eightball_haunted>(r, bob);
        Assert.Same(bob, r.In(() => haunted.loc));
        Click(r, gina, haunted);
        Assert.True(feed.Any("is not currently being shaken"));
        r.In(() => Waiting.Detach(haunted.attack_self(bob)));
        Act(r, bob, "TextInputModal", "submit", "entry", "Will I win?");
        Assert.Equal("Will I win?", r.In(() => haunted.selected_message));
        Assert.Equal(TRUE, r.In(() => haunted.shaking));
        Click(r, gina, haunted);
        Act(r, gina, "EightBallVote", "vote", "answer", "No");
        Assert.Equal(1f, r.In(() => Dm.num(haunted.votes["No"])));
        Assert.Equal("No", r.In(() => Dm.str(haunted.voted[gina.ckey])));
        // the ghosts' answer after the 30 second shake, and the votes start over
        DmList no = r.In(() => obj_item_toy_eightball.possible_answers["No"] as DmList);
        r.Step(31 * Second);
        r.AssertNoRuntimes();
        Assert.True(r.In(() => Dm.each(no).Any(a => feed.Any(Dm.str(a)) || feed.Any(System.Net.WebUtility.HtmlEncode(Dm.str(a))))), "the haunted ball did not answer No: " + string.Join(" | ", feed.Chat.Select(c => c.Html).TakeLast(8)));
        Assert.Equal(0f, r.In(() => Dm.num(haunted.votes["No"])));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCounterfeitDeviceMakesAnArmedEnvelopeThatGoesOffWhenOpened()
    {
        var (r, feed, bob) = Room();
        mob_living_carbon_human alice = Alice(r, 6, 5);
        var device = Held<obj_item_storage_mail_counterfeit_device>(r, bob);
        var signaler = r.In(() => Dm.@new<obj_item_assembly_signaler>(device));
        r.In(() => Waiting.Detach(device.attack_self(bob)));
        Act(r, bob, "AlertModal", "choose", "choice", "Envelope");
        Act(r, bob, "AlertModal", "choose", "choice", "Yes");
        Act(r, bob, "ListInputWindow", "submit", "entry", "Anyone");
        Act(r, bob, "TextInputModal", "submit", "entry", "for you");
        Assert.True(r.In(() => QDELETED(device)));
        var envelope = r.In(() => Dm.each<obj_item_mail_traitor_envelope>(bob.contents).Single());
        Assert.Equal("for you", r.In(() => envelope.name));
        Assert.Equal(TRUE, r.In(() => envelope.armed));
        Assert.Same(envelope, r.In(() => signaler.loc));
        Assert.True(r.In(() => envelope.stamps.len >= 1));
        Assert.Equal("mail_large", r.In(() => envelope.icon_state));

        // Alice opens it: the signaler lands in her hands and its button is pressed
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(envelope)));
        r.In(() => Waiting.NoSleep(alice.put_in_active_hand(envelope)));
        r.In(() => Waiting.Detach(envelope.attack_self(alice)));
        r.Step(2 * Second);
        Assert.True(r.In(() => QDELETED(envelope)));
        Assert.True(feed.Any("you accidentally press a button on"));
        Assert.Same(alice, r.In(() => signaler.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AGunInTraitorMailFiresAtItsOpener()
    {
        // seed 48: the shot misses the eyes' rare puncture roll, whose wound is not ported (not-translated.md section 137)
        var (r, feed, bob) = Room(seed: 48);
        var mail = r.In(() => Dm.@new<obj_item_mail_traitor>(bob.loc));
        var pistol = r.In(() => Dm.@new<obj_item_gun_ballistic_automatic_pistol>(mail));
        r.In(() => mail.armed = TRUE);
        float before = r.In(() => pistol.get_ammo());
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(mail)));
        r.In(() => Waiting.Detach(mail.attack_self(bob)));
        r.Step(3 * Second);
        Assert.True(feed.Any("Its trigger is already pulled!"));
        Assert.Equal(before - 1, r.In(() => pistol.get_ammo()));
        Assert.True(r.In(() => Dm.num(bob.get_brute_loss()) > 0));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALetterOpensOnlyForItsRecipientAndACrateFillsWithMail()
    {
        var (r, feed, bob) = Room();
        mob_living_carbon_human alice = Alice(r, 6, 5);
        Crew(r, alice);
        Crew(r, bob);
        var letter = r.In(() => Dm.@new<obj_item_mail>(bob.loc));
        r.In(() => letter.initialize_for_recipient(alice.mind));
        Assert.Equal(r.In(() => Dm.text("mail for [] (Assistant)", alice.mind.name)), r.In(() => letter.name));
        Assert.Equal(1, r.In(() => letter.contents.len));
        Assert.Equal(0.75f, r.In(() => ((matrix)letter.transform).a), 3);

        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(letter)));
        Assert.Equal(1f, r.In(() => ((matrix)letter.transform).a), 3); // item_scaling: full size in hand
        r.In(() => Waiting.Detach(letter.attack_self(bob)));
        r.Step(2 * Second);
        Assert.True(feed.Any("You can't open somebody else's mail!"));
        Assert.False(r.In(() => QDELETED(letter)));

        r.In(() => Waiting.NoSleep(bob.dropItemToGround(letter)));
        r.In(() => Waiting.NoSleep(alice.put_in_active_hand(letter)));
        r.In(() => Waiting.Detach(letter.attack_self(alice)));
        r.Step(2 * Second);
        Assert.True(r.In(() => QDELETED(letter)));

        // a full crate holds a letter for each crew member and junk mail for the rest
        var crate = r.In(() => Dm.@new<obj_structure_closet_crate_mail_full>(locate(2, 2, 1)));
        var letters = r.In(() => Dm.each<obj_item_mail>(crate).ToList());
        Assert.Equal(r.In(() => (int)crate.storage_capacity), letters.Count);
        Assert.Equal(2, r.In(() => letters.Count(l => l.recipient_ref != null)));
        Assert.Equal("mailsealed", r.In(() => crate.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheClumsinessInjectorAndTheDnaSyringeGun()
    {
        var (r, feed, bob) = Room();
        mob_living_carbon_human alice = Alice(r, 7, 5);
        var injector = Held<obj_item_dnainjector_clumsymut>(r, bob);
        r.In(() => Waiting.NoSleep(injector.attack__proc(bob, bob)));
        r.Step(2);
        Assert.True(feed.Any("You inject yourself with"));
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_CLUMSY)));
        Assert.Equal(TRUE, r.In(() => injector.used));
        Assert.Equal("dnainjector0", r.In(() => injector.icon_state));
        r.In(() => Waiting.NoSleep(injector.attack__proc(alice, bob)));
        Assert.True(feed.Any("This injector is used up!"));

        // the DNA syringe gun loads an injector and fires it at Alice
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(injector)));
        var gun = Held<obj_item_gun_syringe_dna>(r, bob);
        var second = r.In(() => Dm.@new<obj_item_dnainjector_clumsymut>(bob.loc));
        r.In(() => Waiting.NoSleep(bob.put_in_inactive_hand(second)));
        r.In(() => Waiting.NoSleep(gun.item_interaction(bob, second)));
        Assert.True(feed.Any("loaded"));
        Assert.Equal(1, r.In(() => gun.syringes.len));
        r.In(() => Waiting.NoSleep(gun.process_fire(alice, bob)));
        r.Step(2 * Second);
        Assert.True(r.In(() => HAS_TRAIT(alice, TRAIT_CLUMSY)));
        Assert.Equal(0, r.In(() => gun.syringes.len));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAssistantsToolboxHitsHarderWithTelecrystalsInside()
    {
        var (r, _, bob) = Room();
        mob_living_carbon_human alice = Alice(r, 6, 5);
        var toolbox = Held<obj_item_storage_toolbox_mechanical_old_clean>(r, bob);
        Assert.Equal(7, r.In(() => toolbox.contents.len));
        r.In(() => Waiting.NoSleep(toolbox.pre_attack(alice, bob)));
        Assert.Equal(19f, r.In(() => Dm.num(toolbox.force)));
        r.In(() =>
        {
            var crystals = Dm.@new<obj_item_stack_telecrystal>(toolbox);
            crystals.amount = 5;
        });
        r.In(() => Waiting.NoSleep(toolbox.pre_attack(alice, bob)));
        Assert.Equal(24f, r.In(() => Dm.num(toolbox.force)));
        Assert.Equal(27f, r.In(() => Dm.num(toolbox.throwforce)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHotPotatoSticksToItsVictimAndBlowsUp()
    {
        var (r, feed, bob) = Room();
        mob_living_carbon_human alice = Alice(r, 6, 5);
        var potato = Held<obj_item_hot_potato>(r, bob);
        r.In(() => potato.timer = 3 * SECONDS);
        r.In(() => Waiting.NoSleep(potato.attack_self(bob)));
        Assert.Equal(TRUE, r.In(() => potato.active)); // tg redraws it (potato_active) only at its next update
        Assert.True(r.In(() => HAS_TRAIT(potato, TRAIT_NODROP)));
        Assert.StartsWith("primed", r.In(() => potato.name));

        r.In(() => Waiting.NoSleep(potato.attack__proc(alice, bob)));
        Assert.Same(alice, r.In(() => potato.loc));
        Assert.True(feed.Any("You force"));
        r.Step(Second);
        Assert.NotNull(r.In(() => alice.color)); // the holder flashes yellow and cyan
        r.Step(3 * Second);
        Assert.True(feed.Any("explodes!"));
        Assert.True(r.In(() => QDELETED(potato)));
        Assert.True(r.In(() => QDELETED(alice) || alice.stat == DEAD));
    }

    [Fact]
    public void TheReverseBearTrapLocksOnAHeadAndSnaps()
    {
        var (r, feed, bob) = Room();
        mob_living_carbon_human alice = Alice(r, 6, 5);
        r.In(() => Waiting.NoSleep(alice.dropItemToGround(alice.get_item_by_slot(ITEM_SLOT_HEAD) as obj_item)));
        var trap = Held<obj_item_reverse_bear_trap>(r, bob);
        r.In(() => Waiting.Detach(trap.attack__proc(alice, bob)));
        r.Step(4 * Second);
        Assert.Same(trap, r.In(() => alice.get_item_by_slot(ITEM_SLOT_HEAD)));
        Assert.Equal(TRUE, r.In(() => trap.ticking));
        Assert.True(r.In(() => HAS_TRAIT_FROM(trap, TRAIT_NODROP, REVERSE_BEAR_TRAP_TRAIT)));
        Assert.Contains("rbt_ticking", r.In(() => Dm.each(trap.overlays).Select(o => o is IDmAppearance i ? i.icon_state : Dm.str(o)).ToList()));

        // a fiddle at it does not budge the lock at first
        r.In(() => Waiting.Detach(trap.attack_hand(alice)));
        r.Step(3 * Second);
        Assert.True(feed.Any("It doesn't budge!"));
        Assert.Equal(1f, r.In(() => trap.escape_chance));

        r.In(() => trap.kill_countdown = Dm.num(world.time) + 5);
        r.Step(3 * Second);
        Assert.True(feed.Any("goes off!"));
        Assert.Equal(DEAD, r.In(() => alice.stat));
        Assert.Equal(FALSE, r.In(() => trap.ticking));
    }

    [Fact]
    public void TheReverseRevolverShootsANonClownInTheFace()
    {
        var (r, feed, bob) = Room();
        var revolver = Held<obj_item_gun_ballistic_revolver_reverse>(r, bob);
        Assert.NotNull(r.In(() => revolver.chambered));
        r.In(() => Waiting.Detach(revolver.can_trigger_gun(bob)));
        r.Step(Second);
        Assert.True(feed.Any("You somehow shoot yourself in the face!"));
        Assert.True(r.In(() => Dm.num(bob.get_brute_loss()) > 0 || bob.stat == DEAD));
    }

    [Fact]
    public void TheBriefcaseGunFiresTheGunInsideAndRefusesAHeavyOne()
    {
        var (r, feed, bob) = Room();
        var briefcase = Held<obj_item_storage_briefcase_gun_preloaded>(r, bob);
        var pistol = r.In(() => Dm.each<obj_item_gun_ballistic_automatic_pistol>(briefcase).Single());
        float before = r.In(() => pistol.get_ammo());
        r.In(() => Waiting.NoSleep(briefcase.ranged_interact_with_atom(locate(5, 8, 1), bob)));
        r.Step(Second);
        Assert.Equal(before - 1, r.In(() => pistol.get_ammo()));

        var shotgun = r.In(() => Dm.@new<obj_item_gun_ballistic_shotgun>(bob.loc));
        r.In(() =>
        {
            shotgun.w_class = WEIGHT_CLASS_SMALL; // only the weight keeps it out
            shotgun.weapon_weight = WEAPON_HEAVY;
        });
        Assert.False(r.In(() => Dm.truthy(briefcase.atom_storage.can_insert(shotgun, bob, TRUE))));
        Assert.True(feed.Any("balloon: too heavy!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCombatBakery()
    {
        var (r, feed, bob) = Room();
        r.In(() => ADD_TRAIT(bob.mind, TRAIT_MIMING, "test"));
        var baguette = Held<obj_item_food_baguette_combat>(r, bob);
        float idle = r.In(() => Dm.num(baguette.force));
        r.In(() => Waiting.NoSleep(baguette.attack_self(bob)));
        Assert.True(feed.Any("like a sword"));
        Assert.Equal(20f, r.In(() => Dm.num(baguette.force)));
        Assert.Equal(50f, r.In(() => Dm.num(baguette.block_chance)));
        Assert.True(r.In(() => HAS_TRAIT(baguette, TRAIT_CUSTOM_TAP_SOUND)));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(baguette)));
        Assert.Equal(idle, r.In(() => Dm.num(baguette.force)));
        Assert.Equal(FALSE, r.In(() => baguette.fake_swordplay));

        var croissant = r.In(() => Dm.@new<obj_item_food_croissant_throwing>(bob.loc));
        Assert.NotNull(r.In(() => croissant.GetComponent(typeof(datum_component_boomerang))));
        Assert.Equal(20f, r.In(() => Dm.num(croissant.throwforce)));

        var book = r.In(() => Dm.@new<obj_item_book_granter_crafting_recipe_combat_baking>(bob.loc));
        r.In(() => Waiting.NoSleep(book.on_reading_finished(bob)));
        Assert.True(feed.Any("You learned how to make"));
        r.In(() => Waiting.NoSleep(book.recoil(bob)));
        Assert.True(feed.Any("dissolves into burnt flour"));
        Assert.True(r.In(() => QDELETED(book)));
        Assert.Single(r.In(() => Dm.each<obj_effect_decal_cleanable_ash>(bob.loc).ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EveryNewItemSpawnsAndDeletesCleanly()
    {
        var (r, _, bob) = Room();
        IEnumerable<Type> Live(Type root) => root.Assembly.GetTypes()
            .Where(t => (t == root || t.IsSubclassOf(root)) && !Attribute.IsDefined(t, typeof(DmInertAttribute), inherit: false));
        var types = Live(typeof(obj_item_toy_eightball)).Concat(Live(typeof(obj_item_mail))).Concat(Live(typeof(obj_item_storage_mail_counterfeit_device)))
            .Concat(Live(typeof(obj_structure_closet_crate_mail))).Concat(Live(typeof(obj_item_dnainjector))).Concat(Live(typeof(obj_item_hot_potato)))
            .Concat(Live(typeof(obj_item_storage_box_hug))).Concat(Live(typeof(obj_item_storage_briefcase_gun))).Concat(Live(typeof(obj_item_food_baguette)))
            .Concat(Live(typeof(obj_item_book_granter_crafting_recipe))).Concat(Live(typeof(obj_item_gun_ballistic_revolver_reverse))).Concat(new[]
        {
            typeof(obj_item_paper_fluff_junkmail_redpill), typeof(obj_effect_decal_cleanable_ash), typeof(obj_item_storage_bag_mail),
            typeof(obj_item_storage_toolbox_mechanical_old_clean), typeof(obj_item_storage_toolbox_mechanical_old_heirloom), typeof(obj_item_gun_syringe_dna),
            typeof(obj_item_reverse_bear_trap), typeof(obj_item_food_croissant_throwing),
        });
        foreach (Type t in types)
        {
            var made = r.In(() => Dm.@new(t, locate(2, 2, 1)) as atom);
            r.Step(1);
            r.In(() => qdel(made));
            r.Step(1);
        }
        r.AssertNoRuntimes();
    }
}
