using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package GU1 (D333), the ballistic leftovers, in a booted toy room with Bob as a player: the bow (an arrow nocked from the
/// off hand, the string drawn with its picture and the nocked arrow's overlay, loosed at a man; an undrawn bow lets the arrow fall,
/// a dropped bow too), an arrow dipped in a beaker, the divine bow's quiver; the Sakhno (its bolt worked in hand, a shot of .310
/// Strilka that leaves no casing, the surplus rifle's jam, the sawn-off Obrez), the pipe gun's junk shot with its sparks, the
/// doohickey's barrel screwed off and on and its wear, the Donksoft SMG's foam darts and the foam magazines' pictures, the toy
/// shotgun throwing away a spent dart, the stickman's flat gun slipping away, the healium rod healing the man it sticks in and the
/// supermatter bolt's dust; every type the package made live spawns live.
/// </summary>
public class BallisticGunsGU1Tests
{
    private const int LineY = 4;

    /// <summary>Every type the package made live (the ballistic guns, their ammunition, magazines, projectiles and the element).</summary>
    internal static readonly Type[] AllTypes =
    {
        typeof(obj_item_gun_ballistic_rifle_boltaction), typeof(obj_item_gun_ballistic_rifle_boltaction_presawn),
        typeof(obj_item_gun_ballistic_rifle_boltaction_surplus), typeof(obj_item_gun_ballistic_rifle_boltaction_prime),
        typeof(obj_item_gun_ballistic_rifle_boltaction_donkrifle), typeof(obj_item_gun_ballistic_rifle_boltaction_pipegun),
        typeof(obj_item_gun_ballistic_rifle_boltaction_pipegun_empty), typeof(obj_item_gun_ballistic_rifle_boltaction_pipegun_pistol),
        typeof(obj_item_gun_ballistic_rifle_boltaction_pipegun_pistol_empty), typeof(obj_item_gun_ballistic_rifle_boltaction_pipegun_pistol_prime),
        typeof(obj_item_gun_ballistic_rifle_boltaction_pipegun_prime), typeof(obj_item_gun_ballistic_rifle_boltaction_pipegun_prime_empty),
        typeof(obj_item_gun_ballistic_rifle_sks), typeof(obj_item_gun_ballistic_rifle_sks_chekhov), typeof(obj_item_gun_ballistic_rifle_sks_empty),
        typeof(obj_item_gun_ballistic_automatic_pistol_deagle_regal), typeof(obj_item_gun_ballistic_automatic_pistol_deagle_regal_no_mag),
        typeof(obj_item_gun_ballistic_automatic_pistol_doohickey), typeof(obj_item_gun_ballistic_automatic_pistol_riot),
        typeof(obj_item_gun_ballistic_revolver_russian_soul), typeof(obj_item_gun_ballistic_shotgun_hook),
        typeof(obj_item_gun_ballistic_shotgun_toy), typeof(obj_item_gun_ballistic_shotgun_toy_riot),
        typeof(obj_item_gun_ballistic_shotgun_toy_crossbow), typeof(obj_item_gun_ballistic_shotgun_toy_crossbow_riot),
        typeof(obj_item_gun_ballistic_bow), typeof(obj_item_gun_ballistic_bow_longbow), typeof(obj_item_gun_ballistic_bow_shortbow),
        typeof(obj_item_gun_ballistic_bow_divine), typeof(obj_item_gun_ballistic_bow_ashenbow),
        typeof(obj_item_ammo_casing_arrow), typeof(obj_item_ammo_casing_arrow_sticky), typeof(obj_item_ammo_casing_arrow_poison),
        typeof(obj_item_ammo_casing_arrow_holy), typeof(obj_item_ammo_casing_arrow_holy_blazing), typeof(obj_item_ammo_casing_arrow_plastic),
        typeof(obj_item_ammo_casing_arrow_ashen), typeof(obj_item_ammo_casing_strilka310), typeof(obj_item_ammo_casing_strilka310_surplus),
        typeof(obj_item_ammo_casing_strilka310_phasic), typeof(obj_item_ammo_casing_strilka310_enchanted),
        typeof(obj_item_gun_ballistic_automatic_pistol_stickman), typeof(obj_item_ammo_casing_harpoon), typeof(obj_item_ammo_casing_rebar_healium),
        typeof(obj_item_ammo_casing_shotgun_scatterlaser), typeof(obj_item_ammo_casing_shotgun_techshell), typeof(obj_item_ammo_casing_shotgun_dart),
        typeof(obj_item_ammo_casing_shotgun_dart_large), typeof(obj_item_ammo_box_magazine_toy_smg), typeof(obj_item_ammo_box_magazine_toy_smg_riot),
        typeof(obj_item_ammo_box_magazine_toy_smgm45), typeof(obj_item_ammo_box_magazine_toy_smgm45_riot), typeof(obj_item_ammo_box_magazine_toy_m762),
        typeof(obj_item_ammo_box_magazine_toy_m762_riot),
    };

    /// <summary>The projectiles the package made live (spawned on the floor only, never fired here).</summary>
    internal static readonly Type[] ProjectileTypes =
    {
        typeof(obj_projectile_bullet_arrow_poison), typeof(obj_projectile_bullet_arrow_holy), typeof(obj_projectile_bullet_arrow_blazing),
        typeof(obj_projectile_bullet_arrow_ashen), typeof(obj_projectile_bullet_rebar_hydrogen), typeof(obj_projectile_bullet_rebar_healium),
        typeof(obj_projectile_bullet_rebar_supermatter), typeof(obj_projectile_bullet_shotgun_frag12), typeof(obj_projectile_bullet_c38_trac),
        typeof(obj_projectile_bullet_c38_hotshot), typeof(obj_projectile_bullet_c38_iceblox), typeof(obj_projectile_bullet_pea),
        typeof(obj_projectile_bullet_junk), typeof(obj_projectile_bullet_junk_phasic), typeof(obj_projectile_bullet_junk_shock),
        typeof(obj_projectile_bullet_junk_hunter), typeof(obj_projectile_bullet_junk_ripper), typeof(obj_projectile_bullet_junk_reaper),
    };

    // tg's own stack_trace: a magazine's starting rounds add no materials, so each round that leaves it takes the magazine's materials
    // below zero (_box_magazine.dm:77, atom_materials.dm:46); the foam magazines and the guns that spawn with them show it
    private static bool MaterialsTrace(string text) => text.Contains("materials with non-positive values found in", StringComparison.Ordinal);

    private static void AssertNoOtherRuntimes(AudioRoom r) =>
        Assert.True(r.Room.RuntimeText.All(MaterialsTrace), string.Join("\n", r.Room.RuntimeText.Where(t => !MaterialsTrace(t))));

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(14, 7, seed: seed);
        if (TgClone.Root != null)
            TgStrings.Mount(DmWorldState.Of(r.World).Files, TgClone.Root);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Arm<T>(AudioRoom r, mob_living_carbon_human holder) where T : obj_item
    {
        T gun = r.In(() => Dm.@new<T>(holder.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(holder.put_in_active_hand(gun)));
        r.Step();
        r.In(() => Assert.Same(gun, holder.get_active_held_item()));
        return gun;
    }

    private static object Pull(AudioRoom r, mob_living_carbon_human shooter, obj_item_gun gun, atom target)
    {
        r.In(() => shooter.next_click = -1);
        r.In(() => shooter.next_move = -1);
        return r.In(() => Waiting.NoSleep(gun.try_fire_gun(target, shooter, "icon-x=16;icon-y=16")));
    }

    private static List<obj_projectile> Shots(AudioRoom r) => r.In(() => Dm.each<obj_projectile>().Where(p => Dm.truthy(p.fired)).ToList());

    private static void Fly(AudioRoom r)
    {
        for (int i = 0; i < 4 * Second && Shots(r).Count > 0; i++)
            r.Step();
    }

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    private static string Examine(AudioRoom r, atom a, mob who) => r.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(a.examine(who))).Select(o => Dm.str(o))));

    private static bool Heard(AudioRoom r, string file) => r.Sounds.Heard.Any(h => (h.File ?? "").Contains(file, StringComparison.Ordinal));

    private static string Said(Feed feed) => string.Join(" | ", feed.Chat.Select(c => c.Html));

    private static List<T> On<T>(AudioRoom r, int x, int y) where T : atom => r.In(() => Dm.each<T>(locate(x, y, 1).contents).ToList());

    [Fact]
    public void EveryTypeSpawnsLiveIsExaminedAndGoes()
    {
        var (r, _, bob) = Room(9651);
        var failures = new List<string>();
        foreach (Type type in AllTypes.Concat(ProjectileTypes))
        {
            if (InertSeam.IsInertType(type))
                failures.Add(type.Name + " is inert");
            atom thing = r.In(() => (atom)Dm.@new(type, locate(6, LineY, 1)));
            r.Step(2);
            if (type.IsSubclassOf(typeof(obj_projectile)))
            {
                r.In(() => { if (!QDELETED(thing)) qdel(thing); });
                r.Step(2);
                continue;
            }
            r.In(() => Waiting.NoSleep(thing.examine(bob)));
            r.In(() => { if (!QDELETED(thing)) qdel(thing); });
            r.Step(2);
        }
        Assert.False(InertSeam.IsInertType(typeof(datum_element_envenomable_casing)));
        Assert.False(InertSeam.IsInertType(typeof(datum_embedding_rebar_healium)));
        // left for antagonists (D333): the enchanted rifle, the harpoon gun, the cycler shotgun, the Ansem/SC
        Assert.True(InertSeam.IsInertType(typeof(obj_item_gun_ballistic_rifle_enchanted)));
        Assert.True(InertSeam.IsInertType(typeof(obj_item_gun_ballistic_shotgun_automatic_dual_tube)));
        Assert.True(failures.Count == 0, string.Join("\n", failures));

        // the hook shotgun carries its hook gun
        var hook = Arm<obj_item_gun_ballistic_shotgun_hook>(r, bob);
        Assert.IsType<obj_item_gun_magic_hook_bounty>(r.In(() => hook.hook));
        Assert.Contains("Right-click to shoot the hook.", Examine(r, hook, bob));
        r.Step(2 * Second);
        AssertNoOtherRuntimes(r);
    }

    [Fact]
    public void TheBowNocksAnArrowFromTheOffHandIsDrawnAndLoosesItAtAMan()
    {
        var (r, feed, bob) = Room(9652);
        mob_living_carbon_human rob = r.Dummy(7, LineY);
        var bow = r.In(() => Dm.@new<obj_item_gun_ballistic_bow>(bob.loc));
        var arrow = r.In(() => Dm.@new<obj_item_ammo_casing_arrow>(bob.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(bob.put_in_inactive_hand(bow)));
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(arrow)));
        r.Step();
        Assert.Equal("bow", r.In(() => bow.icon_state));

        // the bow held in the off hand takes the arrow (must_hold_to_load): nocked, its overlay drawn
        Assert.Equal((float)ITEM_INTERACT_SUCCESS, Dm.num(r.In(() => Waiting.NoSleep(bow.item_interaction(bob, arrow, Dm.list())))));
        Assert.Same(arrow, r.In(() => bow.chambered));
        Assert.Contains("arrow", Overlays(r, bow));

        // to the shooting hand, the string drawn: the drawn picture and the drawn arrow
        r.In(() => bob.swap_hand());
        r.Step();
        Assert.Same(bow, r.In(() => bob.get_active_held_item()));
        r.In(() => Waiting.NoSleep(bow.attack_self(bob)));
        r.Step();
        Assert.True(feed.Any("balloon: string drawn"), Said(feed));
        Assert.True(Heard(r, "gun/bow/bow_draw.ogg"), r.Sounds.Dump());
        Assert.Equal(1f, r.In(() => bow.drawn));
        Assert.Equal("bow_drawn", r.In(() => bow.icon_state));
        Assert.Contains("arrow_drawn", Overlays(r, bow));

        // loosed at Rob: the arrow flies, the bow is undrawn and empty again
        Assert.True(Dm.truthy(Pull(r, bob, bow, rob)), "the bow did not fire");
        Assert.IsAssignableFrom<obj_projectile_bullet_arrow>(Assert.Single(Shots(r)));
        Fly(r);
        Assert.True(Heard(r, "gun/bow/bow_fire.ogg"), r.Sounds.Dump());
        Assert.True(r.In(() => Dm.num(rob.get_brute_loss())) > 0, "the arrow did not hurt Rob");
        Assert.Null(r.In(() => bow.chambered));
        Assert.Equal(0f, r.In(() => bow.drawn));
        Assert.Equal("bow", r.In(() => bow.icon_state));
        r.Step(Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnUndrawnBowLetsItsArrowFallAndSoDoesADroppedBow()
    {
        var (r, feed, bob) = Room(9653);
        mob_living_carbon_human rob = r.Dummy(7, LineY);
        var bow = r.In(() => Dm.@new<obj_item_gun_ballistic_bow>(bob.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(bob.put_in_inactive_hand(bow)));

        void Nock()
        {
            var arrow = r.In(() => Dm.@new<obj_item_ammo_casing_arrow>(bob.loc));
            r.Step(2);
            r.In(() => Waiting.NoSleep(bob.put_in_active_hand(arrow)));
            Assert.Equal((float)ITEM_INTERACT_SUCCESS, Dm.num(r.In(() => Waiting.NoSleep(bow.item_interaction(bob, arrow, Dm.list())))));
            Assert.Same(arrow, r.In(() => bow.chambered));
        }

        Nock();
        r.In(() => bob.swap_hand());
        r.Step();
        // not drawn: the arrow falls at the archer's feet and nothing flies
        Assert.Equal(FALSE, Dm.num(Pull(r, bob, bow, rob)));
        Assert.Empty(Shots(r));
        Assert.True(feed.Any("Without drawing the bow, the arrow uselessly falls to the ground."), Said(feed));
        Assert.Null(r.In(() => bow.chambered));
        Assert.Single(On<obj_item_ammo_casing_arrow>(r, 3, LineY));

        // nocked and drawn, then dropped: a tenth of a second later the arrow springs off the string
        r.In(() => bob.swap_hand());
        Nock();
        r.In(() => Waiting.NoSleep(bow.attack_self(bob)));
        Assert.Equal(1f, r.In(() => bow.drawn));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(bow)));
        r.Step(4);
        Assert.Null(r.In(() => bow.chambered));
        Assert.Equal(0f, r.In(() => bow.drawn));
        Assert.Equal(2, On<obj_item_ammo_casing_arrow>(r, 3, LineY).Count);
        Assert.True(Heard(r, "gun/bow/bow_fire.ogg"), r.Sounds.Dump());
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnArrowIsDippedInABeakerAndTheDivineBowBringsItsQuiver()
    {
        var (r, _, bob) = Room(9654);
        var arrow = Arm<obj_item_ammo_casing_arrow>(r, bob);
        Assert.Contains("You can dip it in a chemical to deliver a poisonous kick.", Examine(r, arrow, bob));
        var beaker = r.In(() => Dm.@new<obj_item_reagent_containers_cup_beaker>(locate(4, LineY, 1)));
        r.In(() => Waiting.NoSleep(((datum_reagents)beaker.reagents).add_reagent(typeof(datum_reagent_water), 30)));
        // the item's interaction with the open beaker (COMSIG_ITEM_INTERACTING_WITH_ATOM): five units coat the arrow
        Assert.Equal((float)ITEM_INTERACT_SUCCESS, r.In(() => Dm.num(arrow._SendSignal(COMSIG_ITEM_INTERACTING_WITH_ATOM, Dm.list(arrow, bob, beaker, Dm.list())))));
        Assert.Equal(25f, r.In(() => ((datum_reagents)beaker.reagents).total_volume), 3);
        string examined = Examine(r, arrow, bob);
        Assert.Contains("It's coated in some kind of chemical...", examined);
        Assert.DoesNotContain("You can dip it", examined);

        // the chaplain picks the divine bow: a quiver of holy arrows appears where it is
        var bow = r.In(() => Dm.@new<obj_item_gun_ballistic_bow_divine>(locate(5, LineY, 1)));
        r.Step(2);
        r.In(() => bow._SendSignal(COMSIG_ITEM_SUBTYPE_PICKER_SELECTED, Dm.list(bow, null, bob)));
        Assert.Single(On<obj_item_storage_bag_quiver_holy>(r, 5, LineY));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSakhnosBoltIsWorkedInHandAndItsStrilkaLeavesNoCasing()
    {
        var (r, feed, bob) = Room(9655);
        mob_living_carbon_human rob = r.Dummy(7, LineY);
        var gun = Arm<obj_item_gun_ballistic_rifle_boltaction>(r, bob);
        Assert.IsType<obj_item_ammo_casing_strilka310>(r.In(() => gun.chambered));
        Assert.Contains("The bolt is closed.", Examine(r, gun, bob));
        Assert.Contains("look closer", Examine(r, gun, bob)); // the Sakhno's lore

        // the bolt opened: the live round comes out, the bolt stays back (tg's rifle rack)
        r.In(() => Waiting.NoSleep(gun.attack_self(bob)));
        r.Step();
        Assert.True(feed.Any("balloon: bolt opened"), Said(feed));
        Assert.True(Heard(r, "gun/rifle/bolt_out.ogg"), r.Sounds.Dump());
        Assert.Equal(1f, r.In(() => gun.bolt_locked));
        Assert.Null(r.In(() => gun.chambered));
        Assert.Single(On<obj_item_ammo_casing_strilka310>(r, 3, LineY));
        Assert.False(Dm.truthy(r.In(() => gun.can_shoot())));

        // and closed again on the next round
        r.Step(Second);
        r.In(() => Waiting.NoSleep(gun.attack_self(bob)));
        r.Step();
        Assert.True(Heard(r, "gun/rifle/bolt_in.ogg"), r.Sounds.Dump());
        Assert.Equal(0f, r.In(() => gun.bolt_locked));
        Assert.NotNull(r.In(() => gun.chambered));

        // the shot: a .310 Strilka bullet, and the round crumbles (caseless) instead of leaving a casing
        Assert.True(Dm.truthy(Pull(r, bob, gun, rob)), "the rifle did not fire");
        Assert.IsAssignableFrom<obj_projectile_bullet_strilka310>(Assert.Single(Shots(r)));
        Fly(r);
        Assert.True(Heard(r, "gun/rifle/shot_heavy.ogg"), r.Sounds.Dump());
        Assert.True(r.In(() => Dm.num(rob.get_brute_loss())) > 0, "the bullet did not hurt Rob");
        Assert.Single(On<obj_item_ammo_casing_strilka310>(r, 3, LineY)); // only the round worked out by hand
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSurplusRifleJamsAndItsBoltSticksUntilItGivesWay()
    {
        var (r, feed, bob) = Room(9656);
        var gun = Arm<obj_item_gun_ballistic_rifle_boltaction_surplus>(r, bob);
        Assert.Equal(1f, r.In(() => gun.can_jam));
        r.In(() => gun.jamming_chance = 100);
        Assert.True(Dm.truthy(Pull(r, bob, gun, r.In(() => locate(12, LineY, 1)))), "the rifle did not fire");
        Fly(r);
        Assert.Equal(1f, r.In(() => gun.jammed));
        Assert.Equal(100f, r.In(() => gun.jamming_chance)); // clamped at 100

        // jammed: the bolt does not move, and each try makes the next likelier
        r.In(() => gun.unjam_chance = 0);
        Assert.Equal(FALSE, Dm.num(r.In(() => Waiting.NoSleep(gun.attack_self(bob)))));
        Assert.True(feed.Any("balloon: jammed!"), Said(feed));
        Assert.True(Heard(r, "weapons/jammed.ogg"), r.Sounds.Dump());
        Assert.Equal(10f, r.In(() => gun.unjam_chance));
        Assert.Equal(0f, r.In(() => gun.bolt_locked));

        // it gives way: unjammed, the chance back to tg's 10, the bolt opens
        r.In(() => gun.unjam_chance = 100);
        r.Step(Second);
        r.In(() => Waiting.NoSleep(gun.attack_self(bob)));
        Assert.Equal(0f, r.In(() => gun.jammed));
        Assert.Equal(10f, r.In(() => gun.unjam_chance));
        Assert.Equal(1f, r.In(() => gun.bolt_locked));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASakhnoSawnOffWithASawIsAnObrez()
    {
        var (r, _, bob) = Room(9657);
        var gun = Arm<obj_item_gun_ballistic_rifle_boltaction>(r, bob);
        Assert.Equal(-8f, r.In(() => gun.pixel_x));
        r.In(() => Waiting.NoSleep(gun.attack_self(bob))); // the bolt open: the live round out, nothing left to go off
        var saw = r.In(() => Dm.@new<obj_item_circular_saw>(bob.loc));
        r.Step(2);
        r.In(() => Waiting.Detach(gun.item_interaction(bob, saw, Dm.list())));
        r.Step(4 * Second);
        Assert.Equal(1f, r.In(() => gun.sawn_off));
        Assert.StartsWith("sawn-off", r.In(() => gun.name));
        Assert.Equal(36f, r.In(() => gun.spread));
        Assert.Equal(0f, r.In(() => gun.pixel_x));
        Assert.Equal(0f, r.In(() => gun.base_pixel_x));
        Assert.Equal((float)WEIGHT_CLASS_NORMAL, r.In(() => gun.w_class));
        Assert.Equal("gun", r.In(() => gun.inhand_icon_state));

        // the Zhihao's Obrez Moderna and the Jezail's lost power
        var prime = r.In(() => Dm.@new<obj_item_gun_ballistic_rifle_boltaction_prime>(locate(6, LineY, 1)));
        var jezail = r.In(() => Dm.@new<obj_item_gun_ballistic_rifle_boltaction_donkrifle>(locate(6, LineY, 1)));
        r.Step(2);
        r.In(() => prime.do_sawoff());
        r.In(() => jezail.do_sawoff());
        Assert.Contains("Obrez Moderna", r.In(() => prime.name));
        Assert.Equal(0.75f, r.In(() => jezail.projectile_damage_multiplier));
        Assert.Equal(50f, r.In(() => jezail.spread));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePipeGunFiresItsJunkWithSparks()
    {
        var (r, _, bob) = Room(9658);
        mob_living_carbon_human rob = r.Dummy(7, LineY);
        var gun = Arm<obj_item_gun_ballistic_rifle_boltaction_pipegun>(r, bob);
        Assert.IsType<obj_item_ammo_casing_junk>(r.In(() => gun.chambered));
        Assert.True(Dm.truthy(Pull(r, bob, gun, rob)), "the pipe gun did not fire");
        obj_projectile shot = Assert.Single(Shots(r));
        Assert.IsType<obj_projectile_bullet_junk>(shot);
        Assert.Equal(40f * 1.75f, r.In(() => Dm.num(shot.damage)), 3); // tg: junk 40, the pipe gun's projectile_damage_multiplier 1.75
        Assert.True(r.In(() => Dm.each<obj_effect_particle_effect_sparks>().Any()), "no sparks off the pipe gun's chamber");
        Fly(r);
        Assert.True(r.In(() => Dm.num(rob.get_brute_loss())) > 0, "the junk did not hurt Rob");
        r.Step(2 * Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDoohickeysBarrelIsScrewedOffAndOnAndItsShotWearsItOut()
    {
        var (r, feed, bob) = Room(9659);
        var gun = Arm<obj_item_gun_ballistic_automatic_pistol_doohickey>(r, bob);
        Assert.Equal(1f, r.In(() => gun.get_ammo(FALSE))); // the single-shot barrel (the round stays in it, chambered)

        // in hand (no bolt): the barrel unscrewed over two seconds, the round out
        r.In(() => Waiting.Detach(gun.attack_self(bob)));
        r.Step(3 * Second);
        Assert.True(feed.Any("balloon: unscrewing the barrel..."), Said(feed));
        Assert.True(Heard(r, "tools/screwdriver_operating.ogg"), r.Sounds.Dump());
        Assert.Equal(0f, r.In(() => gun.get_ammo(FALSE)));
        obj_item_ammo_casing round = Assert.Single(On<obj_item_ammo_casing>(r, 3, LineY));

        // the round back in from the other hand: the barrel screwed on over two seconds
        r.In(() => Waiting.NoSleep(bob.put_in_inactive_hand(round)));
        r.In(() => Waiting.Detach(gun.item_interaction(bob, round, Dm.list())));
        r.Step(3 * Second);
        Assert.True(feed.Any("balloon: screwing the barrel on..."), Said(feed));
        Assert.Equal(1f, r.In(() => gun.get_ammo(FALSE)));
        Assert.NotNull(r.In(() => gun.chambered));

        // the shot wears the plastic: it takes 70 to 140 damage of its 100, or shatters
        Assert.True(Dm.truthy(Pull(r, bob, gun, r.In(() => locate(12, LineY, 1)))), "the doohickey did not fire");
        Fly(r);
        r.Step(Second);
        Assert.True(r.In(() => QDELETED(gun) || Dm.num(gun.get_integrity()) < 100), "the doohickey took no wear");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDonksoftSmgFiresFoamDartsAndTheFoamMagazinesShowTheirRounds()
    {
        var (r, _, bob) = Room(9660);
        mob_living_carbon_human rob = r.Dummy(8, LineY);

        // the foam force SMG's magazine: full, then empty
        var smg_mag = r.In(() => Dm.@new<obj_item_ammo_box_magazine_toy_smg>(locate(5, LineY, 1)));
        r.Step(2);
        Assert.Equal("smg9mm-full", r.In(() => smg_mag.icon_state));
        r.In(() => smg_mag.empty_magazine());
        r.In(() => smg_mag.update_appearance());
        Assert.Equal("smg9mm-empty", r.In(() => smg_mag.icon_state));
        // the Donksoft box magazine, by ten rounds
        var box = r.In(() => Dm.@new<obj_item_ammo_box_magazine_toy_m762>(locate(5, LineY, 1)));
        r.Step(2);
        Assert.Equal("a7mm-50", r.In(() => box.icon_state));
        // the Donksoft SMG's magazine: a dart tip drawn for each pair of rounds, in the darts' colour
        var c20r_mag = r.In(() => Dm.@new<obj_item_ammo_box_magazine_toy_smgm45_riot>(locate(5, LineY, 1)));
        r.Step(2);
        Assert.Equal("c20r45-riot-base", r.In(() => c20r_mag.icon_state));
        List<string> tips = Overlays(r, c20r_mag).Where(o => o.StartsWith("c20r45-foam-", StringComparison.Ordinal)).ToList();
        Assert.Equal(10, tips.Count);
        Assert.Contains("c20r45-foam-red-2", tips);
        Assert.Contains("c20r45-foam-red-20", tips);

        // the foam force SMG fires its three-dart burst
        var gun = Arm<obj_item_gun_ballistic_automatic_toy>(r, bob);
        float before = r.In(() => gun.get_ammo());
        float burst = r.In(() => gun.burst_size);
        Assert.True(Dm.truthy(Pull(r, bob, gun, rob)), "the SMG did not fire");
        for (int i = 0; i < 5 && Shots(r).Count == 0; i++) // a burst's shots are timers (process_burst), the first one at the next tick
            r.Step();
        Assert.NotEmpty(Shots(r));
        Assert.All(Shots(r), s => Assert.IsAssignableFrom<obj_projectile_bullet_foam_dart>(s));
        r.Step(Second);
        Fly(r);
        Assert.True(Heard(r, "items/syringeproj.ogg"), r.Sounds.Dump());
        Assert.Equal(before - burst, r.In(() => gun.get_ammo()));
        r.Step(Second);
        AssertNoOtherRuntimes(r);
    }

    [Fact]
    public void TheToyShotgunThrowsAwayASpentDartsCasing()
    {
        var (r, _, bob) = Room(9661);
        var gun = Arm<obj_item_gun_ballistic_shotgun_toy>(r, bob);
        obj_item_ammo_casing first = r.In(() => gun.chambered);
        Assert.IsAssignableFrom<obj_item_ammo_casing_foam_dart>(first);
        Assert.True(Dm.truthy(Pull(r, bob, gun, r.In(() => locate(12, LineY, 1)))), "the toy shotgun did not fire");
        Fly(r);
        // the spent casing is gone at once (toy.dm handle_chamber), not ejected by the next pump
        Assert.True(r.In(() => QDELETED(first)), "the spent dart's casing is still there");
        Assert.Null(r.In(() => gun.chambered));
        r.Step(Second);
        r.In(() => Waiting.NoSleep(gun.attack_self(bob)));
        Assert.NotNull(r.In(() => gun.chambered));
        Assert.Empty(On<obj_item_ammo_casing>(r, 3, LineY));
        AssertNoOtherRuntimes(r);
    }

    [Fact]
    public void TheStickmansFlatGunSlipsOutOfTheHand()
    {
        var (r, feed, bob) = Room(9662);
        var gun = r.In(() => Dm.@new<obj_item_gun_ballistic_automatic_pistol_stickman>(bob.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(gun)));
        r.Step();
        Assert.True(feed.Any("it slips out of your possession"), Said(feed));
        Assert.Null(r.In(() => bob.get_active_held_item()));
        bool vanished = r.In(() => QDELETED(gun));
        Assert.True(vanished ? feed.Any("vanishes from your vision") : feed.Any("falls into view"), Said(feed));
        if (!vanished)
            Assert.Same(r.In(() => bob.loc), r.In(() => gun.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHealiumRodHealsAndLullsItsManAndTheSupermatterBoltDustsAnother()
    {
        var (r, _, bob) = Room(9663);
        mob_living_carbon_human rob = r.Dummy(6, LineY);
        r.In(() => Waiting.NoSleep(rob.apply_damage(30, BRUTE, BODY_ZONE_CHEST)));
        float hurt = r.In(() => Dm.num(rob.get_brute_loss()));
        Assert.True(hurt > 0);

        // a healium rod stuck in him heals each second and makes him drowsy
        var rod = r.In(() => Dm.@new<obj_item_ammo_casing_rebar_healium>(rob.loc));
        r.Step(2);
        r.In(() => rod.set_embed(typeof(datum_embedding_rebar_healium)));
        Assert.True(Dm.truthy(r.In(() => Waiting.NoSleep(rod.force_embed(rob, BODY_ZONE_CHEST)))), "the rod did not embed");
        r.Step(3 * Second);
        Assert.True(r.In(() => Dm.num(rob.get_brute_loss())) < hurt, "the healium did not heal");
        Assert.NotNull(r.In(() => rob.has_status_effect(typeof(datum_status_effect_drowsiness))));
        Assert.True(r.In(() => rod.heals_left) < 60, "the rod's heals were not spent");

        // the supermatter bolt, shot east at Tom (Rob may lie asleep by now, and a bolt flies over a lying man): a sound, and dust
        mob_living_carbon_human tom = r.Dummy(11, LineY);
        r.In(() => Waiting.NoSleep(Dm.@new<obj_projectile_bullet_rebar_supermatter>(locate(9, LineY, 1)).fire(90)));
        Fly(r);
        Assert.True(Heard(r, "effects/supermatter.ogg"), r.Sounds.Dump());
        r.Step(3 * Second);
        Assert.True(r.In(() => QDELETED(tom)), "Tom was not dusted");
        r.AssertNoRuntimes();
    }
}
