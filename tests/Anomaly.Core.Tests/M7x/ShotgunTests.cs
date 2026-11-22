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
/// The M7x slice D (docs/ai/specs/weapons.md 3, Slice D): tg's shotguns and their shells in a booted toy room. The riot shotgun's pump
/// (ballistic.dm rack, shotgun.dm) and its shell by shell reload to the tube's six, the combat shotgun's own pump on every shot
/// (shotgun.dm automatic/shoot_live_shot), the double-barrel opening and sawing off, the pellet cloud of a buckshot or rubber shell
/// (pellet_cloud.dm: six projectiles of their own, each with its own spread cone and, aimed by a camera, its own crossing test), and the
/// station shells' numbers (shotgun.dm of the ammunition and the projectiles).
/// </summary>
public partial class ShotgunTests
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

    private static object Pull(AudioRoom r, mob_living_carbon_human shooter, obj_item_gun gun, atom target, string @params = "icon-x=16;icon-y=16")
    {
        r.In(() => shooter.next_click = -1);
        r.In(() => shooter.next_move = -1);
        return r.In(() => Waiting.NoSleep(gun.try_fire_gun(target, shooter, @params)));
    }

    private static T Arm<T>(AudioRoom r, mob_living_carbon_human holder, int x = 3) where T : obj_item_gun
    {
        T gun = r.In(() => Dm.@new<T>(locate(x, LineY, 1)));
        r.In(() => Waiting.NoSleep(holder.put_in_active_hand(gun)));
        r.Step();
        return gun;
    }

    private static List<obj_projectile> Shots(AudioRoom r) => r.In(() => Dm.each<obj_projectile>().Where(p => Dm.truthy(p.fired)).ToList());

    private static void Settle(AudioRoom r)
    {
        for (int i = 0; i < 4 * Second && Shots(r).Count > 0; i++)
            r.Step();
        Assert.Empty(Shots(r));
    }

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance i ? i.icon_state : Dm.str(o)).ToList());

    private static float Click(AudioRoom r, mob_living_carbon_human user, obj_item target, obj_item tool) =>
        Dm.num(r.In(() => Waiting.NoSleep(target.item_interaction(user, tool, Dm.list()))));

    // The camera's aim at a man 'tiles' east of the shooter: from the eye (1.3 m) along the line through the tile centres, level.
    private static string Aim(int fromX, double up = 20.8, double yawOffset = 0) =>
        "icon-x=16;icon-y=16;left=1;button=left;" + ClientSeam.AimParam(fromX * 32, LineY * 32, up, 90 + yawOffset, 0);

    [Fact]
    public void TheRiotShotgunNeedsAPumpBetweenShotsAndRacksItsNextShellFromTheTube()
    {
        AudioRoom r = Room(7961);
        BalloonChat chat = Chat(r);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(5, LineY);
        obj_item_gun_ballistic_shotgun_riot gun = Arm<obj_item_gun_ballistic_shotgun_riot>(r, bob);
        // tg: shotgun.dm riot: an internal tube of six rubber shot shells (internal/shotgun.dm riot), one of them in the chamber; no
        // bolt-fed magazine, a pump (bolt_wording), a shell not a cartridge
        Assert.Equal(6f, r.In(() => gun.magazine.max_ammo));
        Assert.Equal(6f, r.In(() => gun.get_ammo()));
        Assert.Equal(5f, r.In(() => gun.magazine.ammo_count()));
        Assert.IsType<obj_item_ammo_casing_shotgun_rubbershot>(r.In(() => gun.chambered));
        Assert.Equal("riotshotgun", r.In(() => gun.icon_state));
        Assert.Equal(WEAPON_HEAVY, r.In(() => gun.weapon_weight));
        Assert.Equal(8f, r.In(() => gun.fire_delay));

        // the first shot: six rubber pellets (shotgun.dm of the ammunition: pellets = 6)
        r.Sounds.Heard.Clear();
        Assert.True(Dm.truthy(Pull(r, bob, gun, rob)));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/weapons/gun/shotgun/shot.ogg");
        Assert.Equal(6, Shots(r).Count);
        Assert.All(Shots(r), p => Assert.IsType<obj_projectile_bullet_pellet_shotgun_rubbershot>(p));
        Settle(r);
        // the pump gun does not chamber its next shell: the spent casing stays until the pump (semi_auto = FALSE)
        Assert.NotNull(r.In(() => gun.chambered));
        Assert.Null(r.In(() => gun.chambered.loaded_projectile));
        Assert.Equal(5f, r.In(() => gun.magazine.ammo_count(FALSE))); // the tube's five live shells (the spent one in the chamber is not live)
        r.Step(Second);

        // pulling again only clicks (the dry fire sound), no shot
        r.Sounds.Heard.Clear();
        Pull(r, bob, gun, rob);
        Assert.Empty(Shots(r));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/weapons/gun/general/dry_fire.ogg");

        // the pump (use in hand): the casing flies out, the next shell is chambered, the rack sound plays
        r.Sounds.Heard.Clear();
        r.In(() => Waiting.NoSleep(gun.attack_self(bob)));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/weapons/gun/shotgun/rack.ogg");
        Assert.Contains("pump racked", chat.Balloons);
        Assert.NotNull(r.In(() => gun.chambered?.loaded_projectile));
        Assert.Equal(4f, r.In(() => gun.magazine.ammo_count()));
        Assert.Single(r.In(() => Dm.each<obj_item_ammo_casing>(locate(3, LineY, 1).contents).ToList())); // the spent shell on the floor
        Assert.True(Dm.truthy(Pull(r, bob, gun, rob)));
        Settle(r);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRiotShotgunTakesItsShellsOneByOneUpToTheTubesSix()
    {
        AudioRoom r = Room(7962);
        BalloonChat chat = Chat(r);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        obj_item_gun_ballistic_shotgun_riot gun = Arm<obj_item_gun_ballistic_shotgun_riot>(r, bob);
        // empty the tube: unload (attack_self on a gun with a chambered round racks first; take the shells out with unload_ammo)
        r.In(() => Waiting.NoSleep(gun.unload_ammo(bob, TRUE)));
        r.In(() => qdel(gun.chambered)); // a shotgun keeps the shell in its chamber
        Assert.Equal(0f, r.In(() => gun.get_ammo()));
        Assert.Null(r.In(() => gun.chambered));

        // each shell goes in by the click (item_interaction / load_gun: the insert sound, tg's balloon), the sixth fills the tube
        for (int i = 1; i <= 6; i++)
        {
            obj_item_ammo_casing_shotgun_buckshot shell = r.In(() => Dm.@new<obj_item_ammo_casing_shotgun_buckshot>(locate(3, LineY, 1)));
            r.Sounds.Heard.Clear();
            Assert.Equal(ITEM_INTERACT_SUCCESS, Click(r, bob, gun, shell));
            Assert.True(r.Sounds.Heard.Any(h => h.File == "sound/items/weapons/gun/shotgun/insert_shell.ogg"), r.Sounds.Dump());
            Assert.Equal((float)i, r.In(() => gun.get_ammo()));
        }
        // a seventh shell has no room
        obj_item_ammo_casing_shotgun_buckshot extra = r.In(() => Dm.@new<obj_item_ammo_casing_shotgun_buckshot>(locate(3, LineY, 1)));
        Assert.Equal(ITEM_INTERACT_FAILURE, Click(r, bob, gun, extra));
        Assert.Equal(6f, r.In(() => gun.get_ammo()));
        Assert.Contains("1 shells loaded", chat.Balloons);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCombatShotgunPumpsItselfAfterEveryShotAndItsCompactBrotherHoldsFive()
    {
        AudioRoom r = Room(7963);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(5, LineY);
        obj_item_gun_ballistic_shotgun_automatic_combat gun = Arm<obj_item_gun_ballistic_shotgun_automatic_combat>(r, bob);
        Assert.Equal(6f, r.In(() => gun.magazine.max_ammo));
        Assert.Equal(1.5f, r.In(() => gun.projectile_damage_multiplier));
        r.Sounds.Heard.Clear();
        Assert.True(Dm.truthy(Pull(r, bob, gun, rob)));
        Settle(r);
        // shoot_live_shot racks (shotgun.dm automatic): the next shell is chambered without the user's pump, and the rack sounded
        Assert.NotNull(r.In(() => gun.chambered?.loaded_projectile));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/weapons/gun/shotgun/rack.ogg");
        r.Step(Second);
        Assert.True(Dm.truthy(Pull(r, bob, gun, rob)));
        Settle(r);
        Assert.Equal(4f, r.In(() => gun.get_ammo()));
        Assert.Equal(5f, r.In(() => Dm.initial(typeof(obj_item_ammo_box_magazine_internal_shot_com_compact), (obj_item_ammo_box_magazine p) => p.max_ammo)));
        Assert.Equal(WEAPON_MEDIUM, r.In(() => Dm.initial(typeof(obj_item_gun_ballistic_shotgun_automatic_combat_compact), (obj_item_gun p) => p.weapon_weight)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDoubleBarrelBreaksOpenAndTakesTwoShellsAndSawsOffToAMediumGun()
    {
        AudioRoom r = Room(7964);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(5, LineY);
        obj_item_gun_ballistic_shotgun_doublebarrel gun = Arm<obj_item_gun_ballistic_shotgun_doublebarrel>(r, bob);
        Assert.Equal(2f, r.In(() => gun.magazine.max_ammo));
        Assert.Equal(2f, r.In(() => gun.get_ammo(FALSE))); // a gun without a bolt counts its barrels, not the chambered shell twice
        Assert.Equal(BOLT_TYPE_NO_BOLT, r.In(() => gun.bolt_type));
        // two barrels, two shots with no pump between them (semi_auto = TRUE, no bolt)
        for (int barrel = 0; barrel < 2; barrel++)
        {
            Pull(r, bob, gun, rob);
            Assert.True(Shots(r).Count > 0, $"barrel {barrel} fired: live {r.In(() => gun.magazine.ammo_count(FALSE))}");
            Settle(r);
            r.Step(Second);
        }
        Pull(r, bob, gun, rob);
        Assert.Empty(Shots(r)); // the third pull only clicks
        Assert.Equal(0f, r.In(() => gun.magazine.ammo_count(FALSE)));

        // opened (attack_self of a ballistic gun without a bolt drops the shells): reload both barrels by the click
        r.In(() => Waiting.NoSleep(gun.attack_self(bob)));
        for (int i = 0; i < 2; i++)
        {
            obj_item_ammo_casing_shotgun_buckshot shell = r.In(() => Dm.@new<obj_item_ammo_casing_shotgun_buckshot>(locate(3, LineY, 1)));
            Assert.Equal(ITEM_INTERACT_SUCCESS, Click(r, bob, gun, shell));
        }
        Assert.Equal(2f, r.In(() => gun.magazine.ammo_count(FALSE)));

        // tg: doublebarrel/do_sawoff: a sawn-off gun is a medium weapon (one hand), whatever the base shotgun says
        Assert.Equal(WEAPON_MEDIUM, r.In(() => gun.weapon_weight));
        r.In(() => gun.do_sawoff());
        Assert.Equal(WEAPON_MEDIUM, r.In(() => gun.weapon_weight));
        Assert.True(Dm.truthy(r.In(() => gun.sawn_off)));
        Assert.StartsWith("sawn-off", r.In(() => gun.name));
        r.AssertNoRuntimes();
    }

    // A buckshot shell fired by Bob at Rob, 'tiles' east, aimed by the camera: the brute Rob took, and how many pellets the cloud accounted
    // for (hit or ranged out; an adjacent volley is over in the trigger's own tick and its cloud gone: 6 as well).
    private static (int Terminated, float Brute) Volley(ulong seed, int tiles, bool aimed)
    {
        AudioRoom r = Room(seed);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(3 + tiles, LineY);
        r.In(() => rob.dir = WEST);
        obj_item_gun_ballistic_shotgun_riot gun = Arm<obj_item_gun_ballistic_shotgun_riot>(r, bob);
        r.In(() => gun.chambered = Dm.@new<obj_item_ammo_casing_shotgun_buckshot>(gun));
        obj_item_ammo_casing casing = r.In(() => gun.chambered);
        r.In(() => bob.set_combat_mode(TRUE));
        Assert.True(Dm.truthy(Pull(r, bob, gun, rob, aimed ? Aim(3) : "icon-x=16;icon-y=16")));
        // (the cloud allows duplicates, so the casing's component table holds it, or a list of them, under its type)
        object entry = r.In(() => casing._datum_components?[typeof(datum_component_pellet_cloud)]);
        datum_component_pellet_cloud cloud = (entry is DmList l ? l[1] : entry) as datum_component_pellet_cloud;
        int terminated = 6;
        if (cloud != null && !r.In(() => QDELETED(cloud)))
        {
            Assert.Equal(6f, r.In(() => Dm.num(cloud.num_pellets)));
            for (int i = 0; i < 6 * Second && !r.In(() => QDELETED(cloud)); i++)
                r.Step();
            Assert.True(r.In(() => QDELETED(cloud)), "the cloud is not resolved");
            terminated = r.In(() => (int)Dm.num(cloud.terminated));
        }
        else
            Settle(r);
        r.AssertNoRuntimes();
        return (terminated, r.In(() => Dm.num(rob.get_brute_loss())));
    }

    [Fact]
    public void ABuckshotShellThrowsSixPelletsThatEachFlyOnTheirOwnAndNoneFliesBackToTheGunsMan()
    {
        AudioRoom r = Room(7965);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(5, LineY);
        obj_item_gun_ballistic_shotgun_riot gun = Arm<obj_item_gun_ballistic_shotgun_riot>(r, bob);
        r.In(() => gun.chambered = Dm.@new<obj_item_ammo_casing_shotgun_buckshot>(gun));
        Assert.True(Dm.truthy(Pull(r, bob, gun, rob, Aim(3))));
        List<obj_projectile> pellets = Shots(r);
        // tg: buckshot pellets = 6, variance 15, randomspread: six projectiles, each its own aim (no two at the same angle) and its own hit list
        Assert.Equal(6, pellets.Count);
        Assert.All(pellets, p => Assert.IsType<obj_projectile_bullet_pellet_shotgun_buckshot>(p));
        Assert.All(pellets, p => Assert.True(r.In(() => p.dm_aimed), "each pellet is aimed by the camera (DV94)"));
        List<float> angles = pellets.Select(p => r.In(() => Dm.num(p.angle))).ToList();
        Assert.True(angles.Distinct().Count() >= 4, "the pellets spread: " + string.Join(", ", angles));
        Assert.All(angles, a => Assert.InRange(a, 90 - 7.6f, 90 + 7.6f)); // round((rand() - 0.5) * 15)
        Assert.Equal(6, pellets.Select(p => r.In(() => p.impacted)).Distinct().Count());
        Assert.All(pellets, p => Assert.Equal(5f, r.In(() => Dm.num(p.damage)))); // pellet damage 5 (bullets/shotgun.dm)
        Settle(r);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void SomeBuckshotPelletsOfAnAimedVolleyHitAtRangeAndSomeMissByTheCrossingTest()
    {
        // 8 tiles out, several seeds: the cone (7.5 degrees) is wider than the man there, so some pellets miss him (and strike what is
        // behind), and some hit: each pellet is five brute (the cloud counts every strike, a wall's too, so the man's own damage tells)
        float most = 0, least = float.MaxValue, total = 0;
        for (ulong seed = 7966; seed < 7972; seed++)
        {
            (int t, float brute) = Volley(seed, 8, aimed: true);
            Assert.Equal(6, t); // every pellet is accounted for: hit or ranged out (pellet_hit, pellet_range)
            most = Math.Max(most, brute);
            least = Math.Min(least, brute);
            total += brute;
        }
        Assert.True(total > 0, "some pellets hit");
        Assert.True(most < 30, $"no volley lands all six pellets at 8 tiles: {most}");
        Assert.True(least < most, "the volleys differ, each pellet has its own cone");
        // adjacent and unaimed: tg's 2D rule hits with the whole load
        (int closeTerminated, float closeBrute) = Volley(7980, 1, aimed: false);
        Assert.Equal(6, closeTerminated);
        Assert.True(closeBrute >= 25, "an adjacent volley lands, brute: " + closeBrute);
    }

    [Fact]
    public void TheStationShellsDoTgsNumbers()
    {
        // shotgun.dm of the projectiles and the ammunition: the slug 25 brute with 30 armour penetration, the beanbag 10 brute and 55 stamina,
        // the buckshot pellet 5, the rubber pellet 3 brute and 10 stamina
        AudioRoom r = Room(7981);
        Assert.Equal((25f, 30f), r.In(() => (Dm.initial(typeof(obj_projectile_bullet_shotgun_slug), (obj_projectile p) => Dm.num(p.damage)), Dm.initial(typeof(obj_projectile_bullet_shotgun_slug), (obj_projectile p) => Dm.num(p.armour_penetration)))));
        Assert.Equal((10f, 55f), r.In(() => (Dm.initial(typeof(obj_projectile_bullet_shotgun_beanbag), (obj_projectile p) => Dm.num(p.damage)), Dm.initial(typeof(obj_projectile_bullet_shotgun_beanbag), (obj_projectile p) => Dm.num(p.stamina)))));
        Assert.Equal(5f, r.In(() => Dm.initial(typeof(obj_projectile_bullet_pellet_shotgun_buckshot), (obj_projectile p) => Dm.num(p.damage))));
        Assert.Equal((3f, 10f), r.In(() => (Dm.initial(typeof(obj_projectile_bullet_pellet_shotgun_rubbershot), (obj_projectile p) => Dm.num(p.damage)), Dm.initial(typeof(obj_projectile_bullet_pellet_shotgun_rubbershot), (obj_projectile p) => Dm.num(p.stamina)))));
        Assert.Equal(6f, r.In(() => Dm.initial(typeof(obj_item_ammo_casing_shotgun_rubbershot), (obj_item_ammo_casing p) => p.pellets)));
        Assert.Equal(1f, r.In(() => Dm.initial(typeof(obj_item_ammo_casing_shotgun), (obj_item_ammo_casing p) => p.pellets)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASlugAndABeanbagAreSingleProjectilesAndTheBeanbagStunsMoreThanItHurts()
    {
        AudioRoom r = Room(7982);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(5, LineY);
        obj_item_gun_ballistic_shotgun_riot gun = Arm<obj_item_gun_ballistic_shotgun_riot>(r, bob);
        r.In(() => gun.chambered = Dm.@new<obj_item_ammo_casing_shotgun_beanbag>(gun));
        Assert.True(Dm.truthy(Pull(r, bob, gun, rob)));
        obj_projectile bag = Assert.Single(Shots(r));
        Assert.IsType<obj_projectile_bullet_shotgun_beanbag>(bag);
        Settle(r);
        float brute = r.In(() => Dm.num(rob.get_brute_loss()));
        float stamina = r.In(() => Dm.num(rob.get_stamina_loss()));
        Assert.True(stamina > brute, $"stamina {stamina} brute {brute}");
        Assert.True(stamina > 0);
        r.Step(Second);
        r.In(() => gun.chambered = Dm.@new<obj_item_ammo_casing_shotgun>(gun));
        float before = r.In(() => Dm.num(rob.get_brute_loss()));
        Assert.True(Dm.truthy(Pull(r, bob, gun, rob)));
        obj_projectile slug = Assert.Single(Shots(r));
        Assert.IsType<obj_projectile_bullet_shotgun_slug>(slug);
        Settle(r);
        Assert.True(r.In(() => Dm.num(rob.get_brute_loss())) - before > 10, "a slug hurts");
        r.AssertNoRuntimes();
    }
}
