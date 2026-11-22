using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Silicons;

/// <summary>
/// Silicons SI6 (D326): the pAI. A ghost signs up through the ghost menu, a card's owner downloads the candidate and the ghost becomes
/// the pAI in the card; the pAI unfolds its holochassis and folds back, damage forces it in and the emitters must recover; software
/// costs RAM; the door jack's cable, plugged into an airlock by the card's holder, opens the door after the hack; the candidate's
/// personality is saved and loaded; the ghost menu's DNR and notification switches.
/// </summary>
public class PaiTests
{
    private const int Second = 20;

    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(3, 5, "Bob");
        r.In(() => (get_area(locate(5, 5, 1)) as area).requires_power = FALSE);
        r.Step(Second);
        return (r, bob);
    }

    private static mob_dead_observer Ghost(AudioRoom r, int x, int y, string key)
    {
        mob_living_carbon_human body = r.Join(x, y, key);
        client player = body.client;
        r.In(() => body.ghostize(TRUE));
        r.Step(2);
        return Assert.IsType<mob_dead_observer>(player.mob);
    }

    private static object Act(AudioRoom r, mob user, datum host, string window, string action, DmList @params) => r.In(() =>
    {
        World.Current.usr = user;
        return Waiting.NoSleep(host.ui_act(action, @params ?? Dm.list(), Dm.@new<datum_tgui>(user, host, window), null));
    });

    // a ghost signs up as Pip and Bob's card downloads it
    private static (obj_item_pai_card Card, mob_living_silicon_pai Pai) Download(AudioRoom r, mob_living_carbon_human bob, string key = "Ghosty")
    {
        mob_dead_observer ghost = Ghost(r, 7, 5, key);
        var card = r.In(() => Dm.@new<obj_item_pai_card>(bob.loc));
        r.In(() => Waiting.NoSleep(bob.put_in_hands(card)));
        Act(r, ghost, r.In(() => GLOB.ghost_menu), "GhostMenu", "signup_pai", null);
        Act(r, ghost, r.In(() => SSpai), "PaiSubmit", "submit", r.In(() => Dm.list(Dm.kv("name", "Pip"), Dm.kv("description", "Helpful"), Dm.kv("comments", "Hi"))));
        string ckey = r.In(() => Dm.str(ghost.ckey));
        Act(r, bob, card, "PaiCard", "download", r.In(() => Dm.list(Dm.kv("ckey", ckey))));
        r.Step(2);
        return (card, r.In(() => card.pai));
    }

    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(mob_living_silicon_pai), typeof(obj_item_pai_card), typeof(obj_item_pai_cable), typeof(datum_pai_candidate),
            typeof(datum_controller_subsystem_pai), typeof(datum_hud_pai), typeof(atom_movable_screen_pai_software), typeof(atom_movable_screen_pai_shell),
            typeof(obj_item_camera_siliconcam_pai_camera), typeof(datum_action_innate_pai_shell), typeof(datum_ghost_menu),
            typeof(atom_movable_screen_ghost_settings), typeof(atom_movable_screen_ghost_dnr),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void AGhostSignsUpAndTheCardDownloadsIt()
    {
        var (r, bob) = Room(9701);
        mob_dead_observer ghost = Ghost(r, 7, 5, "Ghosty");
        client player = r.In(() => ghost.client);
        var card = r.In(() => Dm.@new<obj_item_pai_card>(bob.loc));
        r.In(() => Waiting.NoSleep(bob.put_in_hands(card)));
        Assert.Contains(card, r.In(() => Dm.each(SSpai.pai_card_list).ToArray()));

        Act(r, ghost, r.In(() => GLOB.ghost_menu), "GhostMenu", "signup_pai", null);
        var candidate = r.In(() => SSpai.candidates[ghost.ckey] as datum_pai_candidate);
        Assert.NotNull(candidate);
        Assert.Empty(r.In(() => Dm.each(card.pool_candidates()).ToArray())); // not submitted yet

        Act(r, ghost, r.In(() => SSpai), "PaiSubmit", "submit", r.In(() => Dm.list(Dm.kv("name", "Pip"), Dm.kv("description", "Helpful"), Dm.kv("comments", "Hi"))));
        Assert.Equal(TRUE, r.In(() => candidate.ready));
        DmList pool = r.In(() => card.pool_candidates());
        Assert.Equal(1, r.In(() => Dm.length(pool)));
        Assert.Equal("Pip", r.In(() => Dm.str((pool[1] as DmList)["name"])));

        Act(r, bob, card, "PaiCard", "download", r.In(() => Dm.list(Dm.kv("ckey", ghost.ckey))));
        r.Step(2);
        var pai = r.In(() => card.pai);
        Assert.NotNull(pai);
        Assert.Same(card, r.In(() => pai.loc));
        Assert.Equal("Pip", r.In(() => pai.name));
        Assert.Same(pai, r.In(() => player.mob));
        Assert.Equal(typeof(datum_pai_screen_image_neutral), r.In(() => card.screen_image));
        Assert.Null(r.In(() => SSpai.candidates[ghost.ckey]));
        // the card's master is set by DNA: the holder's
        r.In(() => pai.set_dna(bob));
        Assert.Equal(r.In(() => bob.real_name), r.In(() => Dm.str(pai.master_name)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHolochassisUnfoldsFoldsAndDamageForcesItIn()
    {
        var (r, bob) = Room(9702);
        var (card, pai) = Download(r, bob);
        r.Step((int)(HOLOCHASSIS_INIT_TIME / 10 * Second) + Second); // the emitters warm up after the download (deciseconds)
        Assert.Equal(TRUE, r.In(() => Waiting.NoSleep(pai.fold_out())));
        Assert.Equal(TRUE, r.In(() => pai.holoform));
        Assert.Same(r.In(() => get_turf(card)), r.In(() => pai.loc));

        // a hit beyond the emitters' health folds the pAI in, and the emitters must recover before it unfolds again
        r.In(() => pai.take_holo_damage(30));
        r.Step(2);
        Assert.Equal(FALSE, r.In(() => pai.holoform));
        Assert.Same(card, r.In(() => pai.loc));
        Assert.True(r.In(() => pai.holochassis_health) < 0);
        Assert.Equal(FALSE, r.In(() => Waiting.NoSleep(pai.fold_out())));
        // the shell regenerates (process: HOLOCHASSIS_REGEN_PER_SECOND)
        r.In(() => pai.process(10));
        Assert.True(r.In(() => pai.holochassis_health) > 0);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDoorJackOpensAnAirlockItsHolderPlugsInto()
    {
        var (r, bob) = Room(9703);
        var (card, pai) = Download(r, bob);
        var door = r.In(() => Dm.@new<obj_machinery_door_airlock>(locate(4, 5, 1)));
        r.Step(2);
        float ram = r.In(() => pai.ram);
        Act(r, pai, pai, "PaiInterface", "buy", r.In(() => Dm.list(Dm.kv("selection", "Door Jack"))));
        Assert.Contains("Door Jack", r.In(() => Dm.each(pai.installed_software).Select(Dm.str).ToArray()));
        Assert.True(r.In(() => pai.ram) < ram);

        r.In(() => Waiting.NoSleep(pai.door_jack(PAI_DOOR_JACK_CABLE)));
        var cable = r.In(() => pai.hacking_cable);
        Assert.NotNull(cable);
        Assert.True(r.In(() => Dm.truthy(bob.is_holding(cable))));
        r.In(() =>
        {
            World.Current.usr = bob;
            Waiting.NoSleep(door.item_interaction(bob, cable, Dm.list()));
        });
        Assert.Same(door, r.In(() => cable.hacking_machine));
        Assert.Same(door, r.In(() => cable.loc));

        r.In(() => Waiting.Detach(pai.door_jack(PAI_DOOR_JACK_HACK)));
        r.Step(16 * Second);
        Assert.False(r.In(() => Dm.truthy(door.density)));
        Assert.Null(r.In(() => pai.hacking_cable));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePersonalityIsSavedAndLoaded()
    {
        var (r, bob) = Room(9704);
        mob_dead_observer ghost = Ghost(r, 7, 5, "Ghosty");
        Act(r, ghost, r.In(() => GLOB.ghost_menu), "GhostMenu", "signup_pai", null);
        Act(r, ghost, r.In(() => SSpai), "PaiSubmit", "save", r.In(() => Dm.list(Dm.kv("name", "Pip"), Dm.kv("description", "Helpful"), Dm.kv("comments", "Hi"))));
        var candidate = r.In(() => SSpai.candidates[ghost.ckey] as datum_pai_candidate);
        r.In(() => candidate.name = "Other");
        r.In(() => Waiting.NoSleep(candidate.savefile_load(ghost)));
        Assert.Equal("Pip", r.In(() => Dm.str(candidate.name)));
        Assert.Equal("Helpful", r.In(() => Dm.str(candidate.description)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGhostMenuSwitchesNotificationsAndDnr()
    {
        var (r, bob) = Room(9705);
        client player = r.In(() => bob.client);
        r.In(() => bob.ghostize(TRUE));
        r.Step(2);
        var ghost = Assert.IsType<mob_dead_observer>(r.In(() => player.mob));
        var menu = r.In(() => GLOB.ghost_menu);
        DmList data = r.In(() => Waiting.NoSleep(menu.ui_data(ghost)) as DmList);
        Assert.Equal(r.In(() => bob.real_name), r.In(() => Dm.str(data["body_name"])));
        Assert.True(r.In(() => Dm.length(data["notification_data"] as DmList)) > 0);

        Act(r, ghost, menu, "GhostMenu", "change_notification", r.In(() => Dm.list(Dm.kv("key", POLL_IGNORE_PAI))));
        Assert.True(r.In(() => Dm.@in(ghost.ckey, GLOB.poll_ignore[POLL_IGNORE_PAI] as DmList)));
        Act(r, ghost, menu, "GhostMenu", "turn_all_on", null);
        Assert.False(r.In(() => Dm.@in(ghost.ckey, GLOB.poll_ignore[POLL_IGNORE_PAI] as DmList)));

        Act(r, ghost, menu, "GhostMenu", "DNR", null);
        Assert.Equal(FALSE, r.In(() => ghost.can_reenter_corpse));
        Assert.Null(r.In(() => ghost.mind));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void MakePaiTurnsAMobIntoAPaiInACard()
    {
        var (r, bob) = Room(9706);
        client player = r.In(() => bob.client);
        string name = r.In(() => bob.name);
        r.In(() => Waiting.NoSleep(bob.make_pai(FALSE)));
        r.Step(2);
        var pai = Assert.IsType<mob_living_silicon_pai>(r.In(() => player.mob));
        Assert.IsType<obj_item_pai_card>(r.In(() => pai.loc));
        Assert.Equal(name, r.In(() => pai.name));
        r.AssertNoRuntimes();
    }
}
