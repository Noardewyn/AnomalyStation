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
/// Syndicate stage X3a (D316): the martial arts' base and the Sleeping Carp. The scroll sells and, read, teaches its reader's mind the
/// art (the carp's traits, faction and the move list button) and goes blank; the carp's two shoves in combat mode make the Keelhaul
/// (a kick to the floor and stamina damage), and in combat mode it swats a bullet aside.
/// </summary>
public class SyndicateMartialArtsTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob, mob_living_carbon_human Alice) Room(ulong seed = 121)
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

    private static void ReadTheScroll(AudioRoom r, mob_living_carbon_human bob, out obj_item_book_granter_martial_carp scroll)
    {
        var item = r.In(() =>
        {
            var s = Dm.@new<obj_item_book_granter_martial_carp>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(s));
            return s;
        });
        r.In(() => Waiting.Detach(item.attack_self(bob)));
        r.Step(30 * Second); // the pages and the reading time
        scroll = item;
    }

    [Fact]
    public void TheCarpIsLiveAndSells()
    {
        foreach (Type type in new[]
        {
            typeof(datum_martial_art_the_sleeping_carp), typeof(obj_item_book_granter_martial_carp), typeof(datum_action_swap_arts),
            typeof(datum_component_mindbound_martial_arts), typeof(datum_component_martial_art_giver), typeof(datum_status_effect_silenced),
            typeof(obj_item_clothing_gloves_the_sleeping_carp), typeof(obj_item_staff_bostaff), typeof(atom_movable_screen_combo),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
        var r = new AudioRoom(5, 5, seed: 3);
        Assert.False(r.In(() => uplink_item_hidden((datum_uplink_item)Dm.@new(typeof(datum_uplink_item_stealthy_weapons_martialarts)))));
    }

    [Fact]
    public void TheScrollTeachesTheReadersMindTheCarp()
    {
        var (r, feed, bob, _) = Room(121);
        ReadTheScroll(r, bob, out var scroll);
        Assert.True(feed.Any("You start reading about sleeping carp..."));
        Assert.True(feed.Any("You have learned the ancient martial art of the Sleeping Carp!"));
        var art = r.In(() => LAZYACCESS(bob.martial_arts, 1) as datum_martial_art);
        Assert.IsType<datum_martial_art_the_sleeping_carp>(art);
        Assert.True(r.In(() => Dm.truthy(art.active)));
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_NOGUNS)));
        Assert.True(r.In(() => Dm.@in(FACTION_CARP, bob.faction)));
        Assert.NotNull(r.In(() => Dm.locate<datum_action_swap_arts>(bob.actions)));
        Assert.Same(r.In(() => bob.mind), r.In(() => (art.origin as datum_component_mindbound_martial_arts)?.parent)); // the mind owns it
        Assert.Equal("empty scroll", r.In(() => scroll.name));
        // the move list button: granted, with its button on the player's screen
        var swap = r.In(() => Dm.locate<datum_action_swap_arts>(bob.actions));
        var button = r.In(() => swap.viewers[bob.hud_used] as atom_movable_screen_movable_action_button);
        Assert.True(r.In(() => Dm.@in(button, bob.client.screen)), "the move list button is not on the screen");
        Assert.Equal("martial", r.In(() => button.icon_state));
        // the move list button prints the carp's teachings
        r.In(() => Waiting.NoSleep(Dm.locate<datum_action_swap_arts>(bob.actions).Trigger(bob)));
        Assert.True(feed.Any("recall the teachings of the Sleeping Carp"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TwoShovesInCombatModeAreTheKeelhaul()
    {
        var (r, feed, bob, alice) = Room(122);
        ReadTheScroll(r, bob, out _);
        r.In(() => bob.set_combat_mode(TRUE));
        float before = r.In(() => Dm.num(alice.get_stamina_loss()));
        DmList shove = r.In(() => { var m = Dm.list(); m[RIGHT_CLICK] = TRUE; return m; });
        r.In(() => Waiting.NoSleep(bob.UnarmedAttack(alice, TRUE, shove)));
        Assert.Equal("D", r.In(() => (LAZYACCESS(bob.martial_arts, 1) as datum_martial_art).streak));
        r.Step(Second); // CLICK_CD_MELEE
        r.In(() => Waiting.NoSleep(bob.UnarmedAttack(alice, TRUE, shove)));
        Assert.True(feed.Any("in the head"), "no Keelhaul");
        Assert.True(r.In(() => Dm.num(alice.get_stamina_loss())) >= before + 60, "too little stamina damage");
        Assert.False(r.In(() => Dm.eq(alice.body_position, STANDING_UP)), "Alice is still standing");
        Assert.Equal("", r.In(() => (LAZYACCESS(bob.martial_arts, 1) as datum_martial_art).streak));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void InCombatModeTheCarpSwatsABulletAside()
    {
        var (r, feed, bob, _) = Room(123);
        ReadTheScroll(r, bob, out _);
        var bullet = r.In(() => Dm.@new<obj_projectile_bullet>(locate(6, 4, 1)));
        Assert.Equal(0f, r.In(() => Dm.num(bob._SendSignal(COMSIG_ATOM_PRE_BULLET_ACT, Dm.list(bob, bullet, BODY_ZONE_CHEST)))));
        r.In(() => bob.set_combat_mode(TRUE));
        Assert.Equal((float)COMPONENT_BULLET_PIERCED, r.In(() => Dm.num(bob._SendSignal(COMSIG_ATOM_PRE_BULLET_ACT, Dm.list(bob, bullet, BODY_ZONE_CHEST)))));
        Assert.True(feed.Any("You deflect"));
        Assert.Same(bob, r.In(() => bullet.firer));
        r.AssertNoRuntimes();
    }
}
