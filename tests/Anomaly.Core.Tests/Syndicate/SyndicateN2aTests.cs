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
/// Syndicate stage N2a (D316) in a booted toy room: the nuclear operatives' guns. The C-20r's syndicate firing pin lets only a holder with
/// the weapons authorization implant shoot; the L6 SAW will not fire or reload with its cover in the wrong state; the Bulldog swaps to its
/// secondary drum when the first runs dry, and on a right click; the M-90gl's right click fires its underbarrel grenade; the rocket
/// launcher's rocket explodes where it hits; the anti-materiel rifle's disruptor round puts its target to sleep; the cases hold their kits.
/// </summary>
public class SyndicateN2aTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed, int width = 13)
    {
        var r = new AudioRoom(width, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        foreach (obj_item held in Dm.each<obj_item>(who.held_items).ToList())
            Waiting.NoSleep(who.dropItemToGround(held));
        T item = Dm.@new<T>(who.loc);
        Waiting.NoSleep(who.put_in_active_hand(item));
        return item;
    });

    private static void Authorize(AudioRoom r, mob_living_carbon_human who)
        => r.In(() => Waiting.NoSleep(Dm.@new<obj_item_implant_weapons_auth>().implant(who, null, TRUE, TRUE)));

    private static object Fire(AudioRoom r, obj_item_gun gun, atom target, mob_living_carbon_human user, string @params = "icon-x=16;icon-y=16")
        => r.In(() => Waiting.NoSleep(gun.try_fire_gun(target, user, @params)));

    [Fact]
    public void TheArsenalIsLive()
    {
        foreach (Type type in new[]
        {
            typeof(obj_item_gun_ballistic_automatic_c20r), typeof(obj_item_gun_ballistic_automatic_m90), typeof(obj_item_gun_ballistic_automatic_l6_saw),
            typeof(obj_item_gun_ballistic_automatic_smartgun), typeof(obj_item_gun_ballistic_shotgun_bulldog), typeof(obj_item_gun_ballistic_rocketlauncher),
            typeof(obj_item_gun_ballistic_rifle_sniper_rifle_syndicate), typeof(obj_item_gun_ballistic_revolver_grenadelauncher_underbarrel),
            typeof(obj_item_gun_grenadelauncher), typeof(obj_item_firing_pin_implant_pindicate), typeof(obj_item_shield_energy),
            typeof(obj_item_storage_toolbox_guncase_m90gl), typeof(obj_item_storage_briefcase_sniper), typeof(obj_projectile_bullet_rocket),
            typeof(obj_projectile_bullet_p50_disruptor), typeof(obj_item_ammo_box_magazine_m12g), typeof(datum_element_backblast),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void TheC20rsSyndicatePinLetsOnlyAnAuthorizedOperativeShoot()
    {
        var (r, _, bob) = Room(1601);
        mob_living_carbon_human carol = r.Dummy(9, 4);
        var smg = Hold<obj_item_gun_ballistic_automatic_c20r>(r, bob);
        r.Step(2);
        float before = r.In(() => smg.get_ammo());
        Assert.True(before > 0, "loaded");

        // without the implant the pin refuses: no round leaves
        Fire(r, smg, carol, bob);
        r.Step(Second);
        Assert.Equal(before, r.In(() => smg.get_ammo()));

        // implanted, the three-round burst goes off
        Authorize(r, bob);
        r.Step(Second);
        Fire(r, smg, carol, bob);
        r.Step(2 * Second);
        Assert.True(r.In(() => smg.get_ammo()) < before, "the C-20r fired");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheL6RefusesWithItsCoverOpenAndTakesAMagazineOnlyThen()
    {
        var (r, _, bob) = Room(1602);
        Authorize(r, bob);
        mob_living_carbon_human carol = r.Dummy(9, 4);
        var saw = Hold<obj_item_gun_ballistic_automatic_l6_saw>(r, bob);
        r.Step(2);
        Assert.Equal("l6closedmag", r.In(() => { Waiting.NoSleep(saw.update_icon_state()); return saw.inhand_icon_state; }));

        // the cover opened, it will not fire
        r.In(() => Waiting.NoSleep(saw.click_alt(bob)));
        Assert.True(r.In(() => Dm.truthy(saw.cover_open)));
        float before = r.In(() => saw.get_ammo());
        Assert.False(Dm.truthy(Fire(r, saw, carol, bob)));
        Assert.Equal(before, r.In(() => saw.get_ammo()));
        Assert.Equal("l6openmag", r.In(() => saw.inhand_icon_state));

        // closed, a magazine is refused; closed and firing, it shoots
        r.In(() => Waiting.NoSleep(saw.click_alt(bob)));
        var spare = r.In(() => Dm.@new<obj_item_ammo_box_magazine_m7mm>(bob.loc));
        Assert.Equal(ITEM_INTERACT_BLOCKING, r.In(() => Waiting.NoSleep(saw.item_interaction(bob, spare, Dm.list()))));
        Fire(r, saw, carol, bob);
        r.Step(Second);
        Assert.True(r.In(() => saw.get_ammo()) < before, "the L6 fired");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBulldogSwapsToItsSecondaryDrum()
    {
        var (r, _, bob) = Room(1603);
        var bulldog = Hold<obj_item_gun_ballistic_shotgun_bulldog>(r, bob);
        r.Step(2);
        var first = r.In(() => bulldog.magazine);
        var second = r.In(() => bulldog.secondary_magazine);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);

        // a right click in hand swaps them
        Assert.Equal(SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN, r.In(() => Waiting.NoSleep(bulldog.attack_self_secondary(bob))));
        Assert.Same(second, r.In(() => bulldog.magazine));
        Assert.Same(first, r.In(() => bulldog.secondary_magazine));

        // a dry drum swaps by itself when the gun chambers its next round
        r.In(() =>
        {
            bulldog.magazine.stored_ammo.Cut(); // get_round only reads the top round; the drum is emptied outright
            Waiting.NoSleep(bulldog.handle_chamber());
        });
        Assert.Same(first, r.In(() => bulldog.magazine));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheM90sRightClickFiresItsUnderbarrelGrenade()
    {
        var (r, _, bob) = Room(1604, width: 16);
        Authorize(r, bob);
        mob_living_carbon_human carol = r.Dummy(12, 4);
        var carbine = Hold<obj_item_gun_ballistic_automatic_m90>(r, bob);
        r.Step(2);
        var underbarrel = r.In(() => carbine.underbarrel);
        Assert.NotNull(underbarrel);
        Assert.Same(r.In(() => carbine.pin), r.In(() => underbarrel.pin)); // the launcher takes the carbine's pin
        // the launcher starts empty: a 40mm shell loads into it from the carbine
        var shell = r.In(() => Dm.@new<obj_item_ammo_casing_a40mm>(bob.loc));
        r.In(() => Waiting.NoSleep(carbine.item_interaction(bob, shell, Dm.list())));
        Assert.Equal(1f, r.In(() => underbarrel.get_ammo()));
        float carbine_rounds = r.In(() => carbine.get_ammo());

        Fire(r, carbine, carol, bob, "icon-x=16;icon-y=16;right=1");
        r.Step(2 * Second);
        // the launcher, a revolver, keeps its spent shell: no live round is left in it
        Assert.Null(r.In(() => (underbarrel.chambered as obj_item_ammo_casing)?.loaded_projectile));
        Assert.Equal(0f, r.In(() => underbarrel.magazine.ammo_count(FALSE)));
        Assert.Equal(carbine_rounds, r.In(() => carbine.get_ammo())); // the carbine's own magazine is untouched
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRocketLauncherBlowsUpWhatItHits()
    {
        var (r, _, bob) = Room(1605, width: 20);
        Authorize(r, bob);
        mob_living_carbon_human carol = r.Dummy(16, 4);
        var launcher = Hold<obj_item_gun_ballistic_rocketlauncher>(r, bob);
        r.Step(2);
        Assert.Contains("rocketlauncher_loaded", r.In(() => Dm.each(Waiting.NoSleep(launcher.update_overlays()) as DmList).Select(Dm.str).ToList()));
        Assert.True(Dm.truthy(Fire(r, launcher, carol, bob)));
        r.Step(3 * Second);
        Assert.Equal(0f, r.In(() => launcher.get_ammo())); // the hack clears the tube
        Assert.True(r.In(() => QDELETED(carol) || Dm.num(carol.get_brute_loss()) + Dm.num(carol.get_fire_loss()) > 30), "the rocket hit");
    }

    // DV245 (the owner, 2026-11-20): the backblast is drawn behind the shooter, not on him nor in front of him
    [Fact]
    public void TheRocketLaunchersBackblastIsDrawnBehindTheShooter()
    {
        var (r, _, bob) = Room(1605, width: 20);
        Authorize(r, bob);
        mob_living_carbon_human carol = r.Dummy(16, 4);
        var launcher = Hold<obj_item_gun_ballistic_rocketlauncher>(r, bob);
        r.Step(2);
        var blasts = new HashSet<(float X, float Y)>();
        void See() => blasts.UnionWith(r.In(() => Dm.each<obj_effect_explosion>().Where(b => b.x < 10).Select(b => (b.x, b.y)).ToList()));
        Assert.True(Dm.truthy(Fire(r, launcher, carol, bob)));
        See();
        for (int t = 0; t < Second; t++)
        {
            r.Step();
            See();
        }
        Assert.Contains((2f, 4f), blasts); // the tile behind Bob (3, 4), firing east
        Assert.All(blasts, b => Assert.True(b.X < 3, $"a blast at {b} is not behind Bob"));
    }

    [Fact]
    public void TheDisruptorRoundPutsItsTargetToSleep()
    {
        var (r, _, bob) = Room(1606, width: 16);
        Authorize(r, bob);
        mob_living_carbon_human carol = r.Dummy(10, 4);
        var rifle = Hold<obj_item_gun_ballistic_rifle_sniper_rifle_syndicate>(r, bob);
        // the disruptor magazine in place of the standard one, and its round racked into the empty chamber
        r.In(() =>
        {
            rifle.chambered = null;
            Waiting.NoSleep(rifle.eject_magazine(bob, FALSE));
            var mag = Dm.@new<obj_item_ammo_box_magazine_sniper_rounds_disruptor>(bob.loc);
            Waiting.NoSleep(rifle.insert_magazine(bob, mag, FALSE));
            Waiting.NoSleep(rifle.rack(bob)); // a bolt action: the first rack opens the bolt, the second closes it on a round
            Waiting.NoSleep(rifle.rack(bob));
            // the ejected magazine went to the other hand: a heavy rifle fires with that hand free
            foreach (obj_item held in Dm.each<obj_item>(bob.held_items).Where(i => i != rifle).ToList())
                Waiting.NoSleep(bob.dropItemToGround(held));
        });
        r.Step(2 * Second);
        Assert.IsType<obj_item_ammo_casing_p50_disruptor>(r.In(() => rifle.chambered));
        Fire(r, rifle, carol, bob);
        r.Step(2 * Second);
        Assert.True(r.In(() => carol.IsSleeping() != null), "asleep");
    }

    [Fact]
    public void TheCasesHoldTheirKits()
    {
        var (r, _, bob) = Room(1607);
        List<Type> Inside<T>() where T : atom => r.In(() => Dm.each<atom>(Dm.@new<T>(bob.loc).contents).Select(a => a.GetType()).ToList());
        List<Type> m90 = Inside<obj_item_storage_toolbox_guncase_m90gl>();
        Assert.Contains(typeof(obj_item_gun_ballistic_automatic_m90), m90);
        Assert.Equal(2, m90.Count(t => t == typeof(obj_item_ammo_box_magazine_m223)));
        Assert.Contains(typeof(obj_item_ammo_box_a40mm_rubber), m90);
        List<Type> sniper = Inside<obj_item_storage_briefcase_sniper>();
        Assert.Contains(typeof(obj_item_gun_ballistic_rifle_sniper_rifle_syndicate), sniper);
        Assert.Contains(typeof(obj_item_ammo_box_magazine_sniper_rounds_disruptor), sniper);
        List<Type> board = Inside<obj_item_storage_toolbox_guncase_sword_and_board>();
        Assert.Contains(typeof(obj_item_shield_energy), board);
        Assert.Contains(typeof(obj_item_clothing_head_costume_knight), board);
        Assert.Contains(typeof(obj_item_gun_ballistic_rocketlauncher), Inside<obj_item_storage_toolbox_guncase_rocketlauncher>());
        Assert.Equal(12, r.In(() => Dm.each<obj_item_ammo_casing_a40mm>(Dm.@new<obj_item_storage_belt_bandolier_china_lake_extra>(bob.loc).contents).Count()));
        r.AssertNoRuntimes();
    }
}
