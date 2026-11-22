using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M7;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7x;

/// <summary>
/// The M7x slice C (docs/ai/specs/weapons.md 3): tg's ballistic guns in a booted toy room. The detective's revolver with lead and with
/// rubber .38 (bullets/revolver.dm numbers, run_armor_check), a bullet's wound through the security vest (M6 wounds), the speed
/// loader filling the cylinder (_box_magazine.dm try_load, _cylinder.dm give_round), the Russian revolver's spin and roulette
/// (revolver.dm), the Makarov's magazine eject and insert, its bolt and its casings with their sounds (ballistic.dm), the ricochet's
/// auto aim (projectile.dm on_ricochet), shrapnel (embedding.dm), collecting casings into a box and the recharger's power pack.
/// </summary>
public class BallisticTests
{
    private const int Second = 20;
    private const int Width = 14, Height = 7, LineY = 4;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(Width, Height, seed: seed);
        if (TgClone.Root != null)
            TgStrings.Mount(DmWorldState.Of(r.World).Files, TgClone.Root);
        r.Step(Second);
        return r;
    }

    private static BalloonChat Chat(AudioRoom r)
    {
        var chat = new BalloonChat();
        PresentationSinks.Of(r.World).Messages = chat;
        return chat;
    }

    private static object Pull(AudioRoom r, mob_living_carbon_human shooter, obj_item_gun gun, atom target) =>
        r.In(() => Waiting.NoSleep(gun.try_fire_gun(target, shooter, "icon-x=16;icon-y=16")));

    private static T Arm<T>(AudioRoom r, mob_living_carbon_human holder, int x = 3) where T : obj_item_gun
    {
        T gun = r.In(() => Dm.@new<T>(locate(x, LineY, 1)));
        r.In(() => Waiting.NoSleep(holder.put_in_active_hand(gun)));
        r.Step();
        return gun;
    }

    private static List<obj_projectile> Shots(AudioRoom r) => r.In(() => Dm.each<obj_projectile>().Where(p => Dm.truthy(p.fired)).ToList());

    // Fire, then step until the bullet is gone (hit, or out of range).
    private static obj_projectile Shoot(AudioRoom r, mob_living_carbon_human shooter, obj_item_gun gun, atom target)
    {
        r.In(() => shooter.next_click = -1);
        r.In(() => shooter.next_move = -1);
        Assert.True(Dm.truthy(Pull(r, shooter, gun, target)), "the gun did not fire");
        obj_projectile shot = Assert.Single(Shots(r));
        for (int i = 0; i < 4 * Second && !r.In(() => QDELETED(shot)); i++)
            r.Step();
        Assert.True(r.In(() => QDELETED(shot)), "the bullet is still flying");
        return shot;
    }

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance i ? i.icon_state : Dm.str(o)).ToList());

    private static float Click(AudioRoom r, mob_living_carbon_human user, obj_item target, obj_item tool) =>
        Dm.num(r.In(() => Waiting.NoSleep(target.item_interaction(user, tool, Dm.list()))));

    // DV401: the number on a held revolver is its live rounds (its get_ammo counts the spent casings left in the cylinder too)
    [Fact]
    public void AHeldRevolversNumberGoesDownWithEachShot()
    {
        AudioRoom r = Room(7931);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        obj_item_gun_ballistic_revolver_c38 gun = Arm<obj_item_gun_ballistic_revolver_c38>(r, bob);
        turf far = r.In(() => locate(12, LineY, 1));
        Assert.Contains(">6/6<", r.In(() => Dm.str(gun.maptext)));
        Assert.True(Dm.truthy(Pull(r, bob, gun, far)), "the revolver did not fire");
        r.Step(2 * Second);
        Assert.Contains(">5/6<", r.In(() => Dm.str(gun.maptext)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALeadPointThirtyEightDoesTwentyFiveBruteAndARubberOneTenBruteAndThirtyStamina()
    {
        AudioRoom r = Room(7951);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(4, LineY);
        obj_item_gun_ballistic_revolver_c38_detective gun = Arm<obj_item_gun_ballistic_revolver_c38_detective>(r, bob);
        Assert.Equal(6f, r.In(() => gun.magazine.ammo_count()));
        Assert.Equal(".38 bullet casing", r.In(() => gun.chambered.name));

        // tg: bullets/revolver.dm c38 damage 25, and the armour check of the zone it hit
        obj_projectile lead = Shoot(r, bob, gun, rob);
        Assert.IsType<obj_projectile_bullet_c38>(lead);
        Assert.Equal(BRUTE, r.In(() => lead.damage_type));
        string zone = r.In(() => lead.def_zone);
        float armour = r.In(() => Dm.num(rob.run_armor_check(zone, BULLET, "", "", 0, "", TRUE)));
        Assert.Equal(25 * (1 - Math.Min(ARMOR_MAX_BLOCK, armour) / 100), r.In(() => Dm.num(rob.get_brute_loss())), 3);
        // a revolver keeps its spent casing in the cylinder (casing_ejector = FALSE): five live rounds are left
        Assert.Equal(5f, r.In(() => gun.get_ammo(FALSE, FALSE)));
        Assert.Equal(6f, r.In(() => gun.get_ammo(FALSE, TRUE)));
        Assert.Empty(r.In(() => Dm.each<obj_item_ammo_casing>(locate(3, LineY, 1).contents).ToList()));

        // the rubber loader: the old rounds out (attack_self dumps the cylinder), the rubber ones in by the item interaction
        r.In(() => Waiting.NoSleep(rob.revive(HEAL_ALL)));
        r.In(() => Waiting.NoSleep(gun.attack_self(bob)));
        Assert.Equal(0f, r.In(() => gun.get_ammo(FALSE, TRUE)));
        obj_item_ammo_box_speedloader_c38_match_bouncy loader = r.In(() => Dm.@new<obj_item_ammo_box_speedloader_c38_match_bouncy>(locate(3, LineY, 1)));
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(gun.item_interaction(bob, loader, Dm.list()))));
        Assert.Equal(6f, r.In(() => gun.get_ammo(FALSE, FALSE)));
        r.Step(2 * Second);
        r.In(() => Waiting.NoSleep(rob.set_stamina_loss(0)));
        float brute0 = r.In(() => Dm.num(rob.get_brute_loss()));
        obj_projectile rubber = Shoot(r, bob, gun, rob);
        Assert.IsType<obj_projectile_bullet_c38_match_bouncy>(rubber);
        zone = r.In(() => rubber.def_zone);
        armour = r.In(() => Dm.num(rob.run_armor_check(zone, BULLET, "", "", 0, "", TRUE)));
        float block = 1 - Math.Min(ARMOR_MAX_BLOCK, armour) / 100;
        Assert.Equal(10 * block, r.In(() => Dm.num(rob.get_brute_loss())) - brute0, 3);
        Assert.Equal(30 * block, r.In(() => Dm.num(rob.get_stamina_loss())), 3);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABulletTakenByTheSecurityVestIsAblatedAndStillWoundsWhenItsBonusIsHigh()
    {
        AudioRoom r = Room(7952);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(4, LineY);
        obj_item_clothing_suit_armor_vest vest = r.In(() => Dm.@new<obj_item_clothing_suit_armor_vest>(locate(4, LineY, 1)));
        r.In(() => Waiting.NoSleep(rob.equip_to_slot_or_del(vest, ITEM_SLOT_OCLOTHING)));
        Assert.Same(vest, r.In(() => rob.wear_suit));
        float bulletArmour = r.In(() => Dm.num(rob.run_armor_check(BODY_ZONE_CHEST, BULLET, "", "", 0, "", TRUE)));
        Assert.True(bulletArmour > 0, "the vest has bullet armour on the chest");

        // a 9mm (bullets/pistol.dm c9mm: 30 brute, pierce) with a wound bonus tg's way (the Russian revolver sets 100): the vest's wound
        // armour cuts the roll (bodyparts check_woundings_mods) and the bullet armour the damage, and the chest is still pierced
        obj_projectile bullet = r.In(() => Dm.@new<obj_projectile_bullet_c9mm>(locate(3, LineY, 1)));
        r.In(() => bullet.wound_bonus = 100);
        r.In(() => bullet.firer = bob);
        r.In(() => bullet.fired_from = null);
        r.In(() => Waiting.NoSleep(rob.projectile_hit(bullet, BODY_ZONE_CHEST, 0))); // what the hit loop calls: the armour check, bullet_act, on_hit
        Assert.Equal(30 * (1 - Math.Min(ARMOR_MAX_BLOCK, bulletArmour) / 100), r.In(() => Dm.num(rob.get_brute_loss())), 3);
        obj_item_bodypart chest = r.In(() => rob.get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart);
        datum_wound pierce = Assert.Single(r.In(() => Dm.each<datum_wound>(chest.wounds).ToList()));
        Assert.IsAssignableFrom<datum_wound_pierce_bleed>(pierce);
        // the armour rating the roll was cut by is the vest's wound armour
        Assert.True(r.In(() => Dm.num(vest.get_armor_rating(WOUND))) > 0);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASpeedLoaderFillsTheEmptyCylinderAndShowsItsRoundsAndBand()
    {
        AudioRoom r = Room(7953);
        BalloonChat chat = Chat(r);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        obj_item_gun_ballistic_revolver_c38_detective gun = Arm<obj_item_gun_ballistic_revolver_c38_detective>(r, bob);
        obj_item_ammo_box_speedloader_c38 loader = r.In(() => Dm.@new<obj_item_ammo_box_speedloader_c38>(locate(3, LineY, 1)));
        Assert.Equal(6, r.In(() => loader.stored_ammo.Count));
        Assert.Equal("38-base", r.In(() => loader.icon_state));
        Assert.Equal(new[] { "38-lead-1", "38-lead-2", "38-lead-3", "38-lead-4", "38-lead-5", "38-lead-6" }, Overlays(r, loader));

        // a rubber loader wears its coloured band over the base (ammo_band_color, update_ammo_band)
        obj_item_ammo_box_speedloader_c38_match_bouncy rubber = r.In(() => Dm.@new<obj_item_ammo_box_speedloader_c38_match_bouncy>(locate(3, LineY, 1)));
        Assert.Contains("+38_ammo_band", Overlays(r, rubber));
        Assert.Equal(7, Overlays(r, rubber).Count);
        Assert.Equal(COLOR_AMMO_RUBBER, r.In(() => rubber.ammo_band_color));

        // dump the cylinder (attack_self: the casings fall at the feet), then the loader refills it by the click
        r.In(() => Waiting.NoSleep(gun.attack_self(bob)));
        Assert.Contains("6 bullets unloaded", chat.Balloons);
        Assert.Equal(6, r.In(() => Dm.each<obj_item_ammo_casing>(locate(3, LineY, 1).contents).Count()));
        Assert.Equal(0f, r.In(() => gun.get_ammo(TRUE, TRUE)));
        r.Sounds.Heard.Clear();
        Assert.Equal(ITEM_INTERACT_SUCCESS, Click(r, bob, gun, loader));
        Assert.Equal(6f, r.In(() => gun.get_ammo(FALSE, FALSE)));
        Assert.Contains("6 bullets loaded", chat.Balloons);
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/weapons/gun/revolver/load_bullet.ogg");
        // the loader gave its six (a speed loader multiloads out into a gun) and shows no rounds
        Assert.Equal(0, r.In(() => loader.stored_ammo.Count));
        Assert.Empty(Overlays(r, loader));
        Assert.Equal("38-base", r.In(() => loader.icon_state));
        // the rounds are .38 casings in the cylinder's chambers, drawn from the cylinder by get_round on the next shot
        Assert.All(r.In(() => Dm.each(gun.magazine.stored_ammo).ToList()), c => Assert.IsType<obj_item_ammo_casing_c38>(c));
        // a seventh round has no chamber: the full cylinder refuses it
        obj_item_ammo_casing_c38 extra = r.In(() => Dm.@new<obj_item_ammo_casing_c38>(locate(3, LineY, 1)));
        Assert.Equal(ITEM_INTERACT_FAILURE, r.In(() => Waiting.NoSleep(gun.item_interaction(bob, extra, Dm.list()))));
        Assert.Equal(6f, r.In(() => gun.get_ammo(FALSE, FALSE)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRussianRevolverNeedsASpinAndTheRouletteHitsOrClicks()
    {
        AudioRoom r = Room(7954);
        BalloonChat chat = Chat(r);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        obj_item_gun_ballistic_revolver_russian gun = Arm<obj_item_gun_ballistic_revolver_russian>(r, bob);
        r.In(() => gun.aim_time = 0); // the aim pause is a do_after of four seconds; the pause itself is tg's
        // five spent casings and one live round in a six chamber cylinder (internal/revolver.dm rus357)
        Assert.Equal(6, r.In(() => gun.magazine.stored_ammo.Count));
        Assert.Equal(1f, r.In(() => gun.get_ammo(FALSE, FALSE)));
        Assert.Equal(6f, r.In(() => gun.get_ammo(FALSE, TRUE)));
        Assert.False(r.In(() => Dm.truthy(gun.spun)));

        // firing unspun is refused; the gun tells to spin it (try_fire_gun)
        r.In(() => bob.set_combat_mode(FALSE));
        Assert.True(Dm.truthy(Pull(r, bob, gun, bob)));
        Assert.Contains(chat.Html, h => h.Contains("You need to spin", StringComparison.Ordinal));
        Assert.Empty(Shots(r));
        // used in the hand (attack_self) it spins first: every spin turns the cylinder rand(0, 12) times
        r.Sounds.Heard.Clear();
        r.In(() => World.Current.usr = bob); // the spin's sound is played at usr, as the host's click sets it
        r.In(() => Waiting.NoSleep(gun.attack_self(bob)));
        Assert.True(r.In(() => Dm.truthy(gun.spun)));
        Assert.Contains("chamber spun", chat.Balloons);
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.StartsWith("sound/items/weapons/gun/revolver/spin", StringComparison.Ordinal));

        // the roulette on a spent chamber: nothing happens, the gun needs a new spin (fire_gun)
        r.In(() => gun.magazine.stored_ammo.Cut());
        r.In(() =>
        {
            for (int i = 0; i < 5; i++)
                gun.magazine.stored_ammo.Add(Dm.@new<obj_item_ammo_casing_c357_spent>(gun.magazine));
            gun.magazine.stored_ammo.Add(Dm.@new<obj_item_ammo_casing_c357>(gun.magazine));
            gun.chambered = null;
            gun.chamber_round(spin_cylinder: FALSE); // the first chamber: spent
            gun.spun = TRUE;
        });
        Assert.NotNull(r.In(() => gun.chambered));
        Assert.Null(r.In(() => gun.chambered.loaded_projectile));
        chat.Html.Clear();
        r.In(() => bob.next_click = -1);
        Assert.True(Dm.truthy(Pull(r, bob, gun, bob)));
        Assert.Contains(chat.Html, h => h.Contains("pull the trigger, and... nothing happens!", StringComparison.Ordinal));
        Assert.False(r.In(() => Dm.truthy(gun.spun)));
        Assert.Equal(0f, r.In(() => Dm.num(bob.get_brute_loss())));
        r.Step(Second);

        // and on the live one: the body part the trigger is pointed at takes tg's 80 (the head 300, wound bonus 100)
        r.In(() =>
        {
            gun.magazine.stored_ammo.Cut();
            gun.magazine.stored_ammo.Add(Dm.@new<obj_item_ammo_casing_c357>(gun.magazine));
            for (int i = 0; i < 5; i++)
                gun.magazine.stored_ammo.Add(Dm.@new<obj_item_ammo_casing_c357_spent>(gun.magazine));
            gun.chambered = null;
            gun.chamber_round(spin_cylinder: FALSE);
            gun.spun = TRUE;
            bob.zone_selected = BODY_ZONE_R_LEG;
            bob.next_click = -1;
        });
        Assert.NotNull(r.In(() => gun.chambered.loaded_projectile));
        Assert.True(Dm.truthy(Pull(r, bob, gun, bob)));
        for (int i = 0; i < 2 * Second; i++)
            r.Step();
        Assert.Contains(chat.Html, h => h.Contains("as it goes off", StringComparison.Ordinal));
        Assert.True(r.In(() => Dm.num(bob.get_brute_loss())) > 0, "the shot hit its own man");
        Assert.False(r.In(() => Dm.truthy(gun.spun)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMakarovsMagazineEjectsAndInsertsAndTheSlideLocksBackWhenEmpty()
    {
        AudioRoom r = Room(7955);
        BalloonChat chat = Chat(r);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(9, LineY);
        obj_item_gun_ballistic_automatic_pistol gun = Arm<obj_item_gun_ballistic_automatic_pistol>(r, bob);
        // a locking bolt pistol starts with a round in the chamber and a full magazine (ballistic.dm Initialize: chamber_round(replace_new_round))
        Assert.NotNull(r.In(() => gun.chambered));
        Assert.Equal(12f, r.In(() => gun.magazine.ammo_count()));
        Assert.Equal(13f, r.In(() => gun.get_ammo()));
        Assert.Contains("pistol_mag", Overlays(r, gun));
        Assert.DoesNotContain("pistol_bolt_locked", Overlays(r, gun));

        // attack_hand on the held gun takes the magazine out into the hand
        obj_item_ammo_box_magazine_m9mm mag = r.In(() => gun.magazine as obj_item_ammo_box_magazine_m9mm);
        Assert.NotNull(mag);
        r.Sounds.Heard.Clear();
        r.In(() => Waiting.NoSleep(gun.attack_hand(bob, Dm.list())));
        Assert.Null(r.In(() => gun.magazine));
        Assert.Contains("magazine unloaded", chat.Balloons);
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/weapons/gun/pistol/mag_release.ogg");
        Assert.Same(mag, r.In(() => bob.get_inactive_held_item()));
        Assert.DoesNotContain("pistol_mag", Overlays(r, gun));
        Assert.Equal(1f, r.In(() => gun.get_ammo())); // the chambered round stays

        // the same magazine goes back in by the click (item_interaction: no magazine yet)
        r.Sounds.Heard.Clear();
        Assert.Equal(ITEM_INTERACT_SUCCESS, Click(r, bob, gun, mag));
        Assert.Same(mag, r.In(() => gun.magazine));
        Assert.Contains("magazine loaded", chat.Balloons);
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/weapons/gun/pistol/mag_insert.ogg");
        Assert.Equal(13f, r.In(() => gun.get_ammo()));

        // a loaded magazine on a loaded gun swaps them (tac_reloads)
        obj_item_ammo_box_magazine_m9mm fresh = r.In(() => Dm.@new<obj_item_ammo_box_magazine_m9mm>(locate(3, LineY, 1)));
        Assert.Equal(ITEM_INTERACT_SUCCESS, Click(r, bob, gun, fresh));
        Assert.Same(fresh, r.In(() => gun.magazine));
        Assert.Contains("magazine swapped", chat.Balloons);
        Assert.Contains(mag, r.In(() => Dm.each(bob.held_items).ToList()));

        // fire the pistol dry: every shot ejects its casing (the sound of it landing follows), the slide locks back on the last
        r.In(() => gun.magazine.stored_ammo.Cut(3)); // leaves two rounds
        r.Sounds.Heard.Clear();
        Shoot(r, bob, gun, rob);
        Assert.Single(r.In(() => Dm.each<obj_item_ammo_casing>(locate(3, LineY, 1).contents).ToList()));
        r.Step(Second);
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/weapons/gun/pistol/shot.ogg");
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/weapons/gun/general/mag_bullet_remove.ogg"); // the turf's bullet_bounce_sound
        Shoot(r, bob, gun, rob);
        Assert.False(r.In(() => Dm.truthy(gun.bolt_locked)));
        Shoot(r, bob, gun, rob);
        Assert.Equal(0f, r.In(() => gun.get_ammo()));
        Assert.True(r.In(() => Dm.truthy(gun.bolt_locked)));
        Assert.Contains("pistol_bolt_locked", Overlays(r, gun));
        Assert.Equal(3, r.In(() => Dm.each<obj_item_ammo_casing>(locate(3, LineY, 1).contents).Count()));
        // spent casings look spent: no live cap in the picture
        Assert.All(r.In(() => Dm.each<obj_item_ammo_casing>(locate(3, LineY, 1).contents).ToList()), c => Assert.DoesNotContain("-live", c.icon_state));

        // the trigger on an empty gun is the pistol's own click; dropping the bolt (attack_self) after a new magazine chambers a round
        r.Sounds.Heard.Clear();
        r.In(() => bob.next_click = -1);
        Pull(r, bob, gun, rob);
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/weapons/gun/pistol/dry_fire.ogg");
        r.In(() => Waiting.NoSleep(gun.attack_self(bob))); // an empty magazine in a gun is ejected first
        Assert.Null(r.In(() => gun.magazine));
        obj_item_ammo_box_magazine_m9mm third = r.In(() => Dm.@new<obj_item_ammo_box_magazine_m9mm>(locate(3, LineY, 1)));
        Click(r, bob, gun, third);
        r.Sounds.Heard.Clear();
        r.In(() => Waiting.NoSleep(gun.attack_self(bob)));
        Assert.False(r.In(() => Dm.truthy(gun.bolt_locked)));
        Assert.NotNull(r.In(() => gun.chambered));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/weapons/gun/pistol/drop_small.ogg");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARackedRoundEjectsTheOldCasingAndAShotCasingBouncesWithItsSounds()
    {
        AudioRoom r = Room(7956);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        obj_item_gun_ballistic_automatic_pistol gun = Arm<obj_item_gun_ballistic_automatic_pistol>(r, bob);
        obj_item_ammo_casing first = r.In(() => gun.chambered);
        r.Sounds.Heard.Clear();
        r.In(() => Waiting.NoSleep(gun.attack_self(bob))); // rack: the chambered live round is ejected, the next one chambered
        Assert.NotSame(first, r.In(() => gun.chambered));
        Assert.Equal(locate_turf(r, 3, LineY), r.In(() => first.loc));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/weapons/gun/pistol/rack_small.ogg");
        r.Step(Second);
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/weapons/gun/general/mag_bullet_remove.ogg");
        Assert.EndsWith("-live", r.In(() => first.icon_state)); // the casing keeps the picture of a live round (it was racked out unfired)
        Assert.Equal(12f, r.In(() => gun.get_ammo())); // the chamber took one of the magazine's twelve (replace_new_round is for the spawn only)
        r.AssertNoRuntimes();
    }

    private static object locate_turf(AudioRoom r, int x, int y) => r.In(() => locate(x, y, 1));

    [Fact]
    public void ARicochetingTrueStrikeBulletTurnsTowardTheManInSightAndNotTheOneBehindAWall()
    {
        AudioRoom r = Room(7957);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(7, LineY + 2);
        // a .38 True Strike (bullets/revolver.dm: 100 degrees, 4 tiles) fired by Bob flying north at x=6: Rob is up and to the east
        obj_projectile_bullet_c38_match_true bullet = r.In(() => Dm.@new<obj_projectile_bullet_c38_match_true>(locate(6, LineY, 1)));
        r.In(() => bullet.firer = bob);
        r.In(() => bullet.set_angle(0)); // north
        r.In(() => bullet.on_ricochet(null));
        Assert.Equal(1f, r.In(() => bullet.ricochets));
        float angle = r.In(() => Dm.num(bullet.angle));
        Assert.Equal(r.In(() => Dm.num(get_angle(bullet, rob))), angle, 0.01); // aimed at Rob's tile from the bullet's
        Assert.Same(rob, r.In(() => bullet.original));

        // a wall between them blocks the sight (inLineOfSight): the bullet keeps its way
        r.In(() => Waiting.NoSleep(locate(6, LineY + 1, 1).ChangeTurf(typeof(turf_closed_wall))));
        r.In(() => Waiting.NoSleep(locate(7, LineY + 1, 1).ChangeTurf(typeof(turf_closed_wall))));
        r.In(() => bullet.original = null);
        r.In(() => bullet.set_angle(0));
        r.In(() => bullet.on_ricochet(null));
        Assert.Equal(0f, r.In(() => Dm.num(bullet.angle)));
        Assert.Null(r.In(() => bullet.original));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABulletThatEmbedsLeavesItsShrapnelInTheLimbUnderTheBulletsName()
    {
        AudioRoom r = Room(7958);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(4, LineY);
        obj_projectile_bullet_c38 bullet = r.In(() => Dm.@new<obj_projectile_bullet_c38>(locate(3, LineY, 1)));
        r.In(() => bullet.firer = bob);
        datum_embedding embed = r.In(() => bullet.get_embed() as datum_embedding);
        Assert.NotNull(embed);
        r.In(() => embed.embed_chance = 100);
        r.In(() => Waiting.NoSleep(rob.projectile_hit(bullet, BODY_ZONE_CHEST, 0)));
        obj_item_bodypart chest = r.In(() => rob.get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart);
        obj_item_shrapnel_bullet shrapnel = Assert.IsType<obj_item_shrapnel_bullet>(Assert.Single(r.In(() => Dm.each<obj_item>(chest.embedded_objects).ToList())));
        Assert.Equal(".38 bullet", r.In(() => shrapnel.name));
        Assert.Same(embed, r.In(() => shrapnel.get_embed()));
        Assert.Null(r.In(() => bullet.embed_data)); // the payload took the embedding datum from the bullet
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACasingOnTheFloorIsCollectedIntoAMagazineByTheClick()
    {
        AudioRoom r = Room(7959);
        BalloonChat chat = Chat(r);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        obj_item_ammo_box_magazine_m9mm mag = r.In(() => Dm.@new<obj_item_ammo_box_magazine_m9mm>(locate(3, LineY, 1)));
        r.In(() => mag.empty_magazine());
        Assert.Equal(0, r.In(() => mag.stored_ammo.Count));
        List<obj_item_ammo_casing> casings = r.In(() => Dm.each<obj_item_ammo_casing>(locate(3, LineY, 1).contents).ToList());
        Assert.Equal(12, casings.Count);
        r.In(() => casings[0].loaded_projectile = null); // a spent one stays
        r.In(() => casings[0].update_appearance());
        // the click of the box on a casing of the tile, or the casing on the box: collect_into_box
        Assert.Equal(ITEM_INTERACT_SUCCESS, Dm.num(r.In(() => Waiting.NoSleep(casings[1].item_interaction(bob, mag, Dm.list())))));
        Assert.Equal(11, r.In(() => mag.stored_ammo.Count));
        Assert.Equal(1, r.In(() => Dm.each<obj_item_ammo_casing>(locate(3, LineY, 1).contents).Count()));
        Assert.Contains(chat.Html, h => h.Contains("You collect 11 rounds", StringComparison.Ordinal));
        // nothing live is left: the second click fails to collect
        Assert.Equal(ITEM_INTERACT_BLOCKING, Dm.num(r.In(() => Waiting.NoSleep(casings[0].item_interaction(bob, mag, Dm.list())))));
        Assert.Contains(chat.Html, h => h.Contains("You fail to collect anything!", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARechargerRefillsAPowerPackOneShotPerCycle()
    {
        AudioRoom r = Room(7960);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        r.In(() => (get_area(locate(5, LineY, 1)) as area).requires_power = FALSE);
        obj_machinery_recharger charger = r.In(() => Dm.@new<obj_machinery_recharger>(locate(5, LineY, 1)));
        r.Step(Second);
        obj_item_ammo_box_magazine_recharge pack = r.In(() => Dm.@new<obj_item_ammo_box_magazine_recharge>(locate(3, LineY, 1)));
        r.In(() => pack.stored_ammo.Cut(6)); // five shots
        Assert.Equal(5, r.In(() => pack.stored_ammo.Count));
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(charger.item_interaction(bob, pack, Dm.list()))));
        r.Step(Second);
        for (int i = 0; i < 10 * Second && r.In(() => pack.stored_ammo.Count) == 5; i++)
            r.Step();
        Assert.True(r.In(() => pack.stored_ammo.Count) > 5, "the first cycle added a shot");
        float coeff = r.In(() => charger.recharge_coeff);
        int max = (int)r.In(() => pack.max_ammo);
        for (int i = 0; i < 60 * Second && r.In(() => pack.stored_ammo.Count) < max; i++)
            r.Step();
        Assert.Equal(max, r.In(() => pack.stored_ammo.Count));
        Assert.True(coeff >= 1);
        r.Step(2 * Second);
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/ping.ogg");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDetectivesHolsterHoldsTwoSpeedLoadersAndTheRevolverAndGivesTheGunFlipWhileWorn()
    {
        AudioRoom r = Room(7961);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        obj_item_storage_belt_holster_detective_full holster = r.In(() => Dm.@new<obj_item_storage_belt_holster_detective_full>(locate(3, LineY, 1)));
        List<obj_item> inside = r.In(() => Dm.each<obj_item>(holster.contents).ToList());
        Assert.Equal(2, inside.OfType<obj_item_ammo_box_speedloader_c38>().Count());
        Assert.Single(inside.OfType<obj_item_gun_ballistic_revolver_c38_detective>());
        Assert.Equal(3, inside.Count);
        // its storage takes handguns and their ammo (storage/subtypes/holsters.dm detective), not a rifle laser; three slots, all in use
        obj_item_gun_ballistic_automatic_pistol pistol = r.In(() => Dm.@new<obj_item_gun_ballistic_automatic_pistol>(locate(3, LineY, 1)));
        obj_item_gun_energy_laser laser = r.In(() => Dm.@new<obj_item_gun_energy_laser>(locate(3, LineY, 1)));
        r.In(() => Waiting.NoSleep(inside[0].forceMove(locate(3, LineY, 1))));
        Assert.True(Dm.truthy(r.In(() => holster.atom_storage.can_insert(pistol, bob, TRUE))));
        Assert.False(Dm.truthy(r.In(() => holster.atom_storage.can_insert(laser, bob, TRUE))));

        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_GUNFLIP)));
        // a belt needs a jumpsuit to hang on
        obj_item_clothing_under_color_grey suit = r.In(() => Dm.@new<obj_item_clothing_under_color_grey>(locate(3, LineY, 1)));
        r.In(() => Waiting.NoSleep(bob.equip_to_slot_or_del(suit, ITEM_SLOT_ICLOTHING)));
        r.In(() => Waiting.NoSleep(bob.equip_to_slot_or_del(holster, ITEM_SLOT_BELT)));
        r.Step();
        Assert.Same(holster, r.In(() => bob.belt));
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_GUNFLIP)));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(holster)));
        r.Step();
        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_GUNFLIP)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASuppressedPistolFiresWithTheSuppressedSoundAndShowsItsSuppressor()
    {
        AudioRoom r = Room(7962);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(9, LineY);
        obj_item_gun_ballistic_automatic_pistol_suppressed gun = Arm<obj_item_gun_ballistic_automatic_pistol_suppressed>(r, bob);
        Assert.NotNull(r.In(() => gun.suppressor));
        Assert.Equal(SUPPRESSED_QUIET, r.In(() => gun.suppressed));
        Assert.Contains("pistol_suppressor", Overlays(r, gun));
        r.Sounds.Heard.Clear();
        Shoot(r, bob, gun, rob);
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/weapons/gun/pistol/shot_suppressed.ogg");
        Assert.DoesNotContain(r.Sounds.Heard, h => h.File == "sound/items/weapons/gun/pistol/shot.ogg");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheContrabandPistolsAndTheMatebaComeFreeWithTheirParents()
    {
        AudioRoom r = Room(7963);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(9, LineY);
        // the contraband Makarov and Desert Eagle: an unremovable pin, a clown pin in one of ten (pistol.dm contraband Initialize)
        int clown = 0;
        for (int i = 0; i < 60; i++)
        {
            obj_item_gun_ballistic_automatic_pistol_contraband gun = r.In(() => Dm.@new<obj_item_gun_ballistic_automatic_pistol_contraband>(locate(3, LineY, 1)));
            obj_item_firing_pin pin = r.In(() => gun.pin as obj_item_firing_pin);
            Assert.NotNull(pin);
            Assert.False(r.In(() => Dm.truthy(pin.pin_removable)));
            if (pin is obj_item_firing_pin_clown)
                clown++;
            obj_item_gun_ballistic_automatic_pistol_deagle_contraband eagle = r.In(() => Dm.@new<obj_item_gun_ballistic_automatic_pistol_deagle_contraband>(locate(3, LineY, 1)));
            Assert.False(r.In(() => Dm.truthy((eagle.pin as obj_item_firing_pin).pin_removable)));
        }
        Assert.InRange(clown, 1, 20);

        // the Unica 6 auto-revolver: a seven chamber .357 cylinder and the revolver's loud shot, fired point blank
        obj_item_gun_ballistic_revolver_mateba mateba = Arm<obj_item_gun_ballistic_revolver_mateba>(r, bob);
        Assert.Equal(7f, r.In(() => mateba.magazine.ammo_count()));
        Assert.Equal(".357", r.In(() => Dm.str(mateba.magazine.caliber)));
        r.Sounds.Heard.Clear();
        obj_projectile shot = Shoot(r, bob, mateba, rob);
        Assert.IsType<obj_projectile_bullet_c357>(shot);
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/weapons/gun/revolver/shot_alt.ogg");
        r.AssertNoRuntimes();
    }
}
