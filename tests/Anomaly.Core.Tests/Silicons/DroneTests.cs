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
/// Silicons SI7 (D326): maintenance drones. A drone is born with its laws, its ten built-in tools in its internal storage and its
/// traits; a ghost who clicks a drone shell and says yes becomes a named drone and the shell is used up; the dispenser makes shells
/// from iron and glass up to its idle limit; a screwdriver tightens a hurt drone, a dead one is reactivated by another drone, and a
/// hacked drone's laws turn and a wrench resets them; the drone's hat shows in its examine.
/// </summary>
public class DroneTests
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

    private static void Act(AudioRoom r, mob who, string iface, string action, string key, object value)
    {
        r.Step(2);
        var open = r.In(() => Dm.each_cast<datum_tgui>(who.tgui_open_uis).Select(u => Dm.str(u.@interface)).ToList());
        Assert.True(open.Contains(iface), "no " + iface + " open; open: " + string.Join(", ", open));
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(who.tgui_open_uis).Last(u => Dm.str(u.@interface) == iface).window.id));
        r.Command(() => ClientSeam.TguiMessage(who.client, window, "act/" + action, Dm.list(Dm.kv(key, value))));
        r.Step(4);
    }

    private static mob_dead_observer Ghost(AudioRoom r, int x, int y, string key)
    {
        mob_living_carbon_human body = r.Join(x, y, key);
        client player = body.client;
        r.In(() => body.ghostize(TRUE));
        r.Step(2);
        return Assert.IsType<mob_dead_observer>(player.mob);
    }

    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(mob_living_basic_drone), typeof(obj_effect_mob_spawn), typeof(obj_effect_mob_spawn_ghost_role), typeof(obj_effect_mob_spawn_ghost_role_drone),
            typeof(obj_machinery_drone_dispenser), typeof(obj_item_storage_drone_tools), typeof(datum_storage_drone), typeof(datum_component_holderloving),
            typeof(obj_machinery_camera_silicon), typeof(obj_item_mob_holder_drone), typeof(mob_living_basic_drone_syndrone), typeof(mob_living_basic_drone_polymorphed),
            typeof(mob_living_basic_drone_derelict), typeof(datum_component_stationstuck),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void ADroneIsBornWithItsLawsAndTools()
    {
        var (r, _) = Room(9901);
        var drone = r.In(() => Dm.@new<mob_living_basic_drone>(locate(5, 5, 1)));
        r.Step(2);
        r.In(() =>
        {
            Assert.Contains(drone, Dm.each(GLOB.drones_list));
            Assert.StartsWith("1. You may not involve yourself", drone.laws);
            var tools = Assert.IsType<obj_item_storage_drone_tools>(drone.internal_storage);
            Assert.Equal(10, Dm.length(tools.contents));
            Assert.Contains(Dm.each(tools.contents), o => o is obj_item_screwdriver_drone);
            Assert.True(HAS_TRAIT(drone, TRAIT_VENTCRAWLER_ALWAYS));
            Assert.True(HAS_TRAIT(drone, TRAIT_PACIFISM)); // shy
            Assert.Same(tools, drone.get_item_by_slot(ITEM_SLOT_DEX_STORAGE));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AGhostBecomesADroneFromAShell()
    {
        var (r, _) = Room(9902);
        mob_dead_observer ghost = Ghost(r, 7, 5, "Ghosty");
        client player = r.In(() => ghost.client);
        var shell = r.In(() => Dm.@new<obj_effect_mob_spawn_ghost_role_drone>(locate(6, 5, 1)));
        r.In(() =>
        {
            SSticker.current_state = GAME_STATE_PLAYING; // tg's ghost roles wait for the round's start
            Waiting.Detach(shell.attack_ghost(ghost));
        });
        Act(r, ghost, "AlertModal", "choose", "choice", "Yes");
        r.Step(Second);
        var drone = Assert.IsType<mob_living_basic_drone>(r.In(() => player.mob));
        Assert.StartsWith("Drone (", r.In(() => drone.name));
        Assert.True(r.In(() => QDELETED(shell)), "the shell is used up");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AGhostTakesAShellFromTheSpawnersMenu()
    {
        var (r, _) = Room(9905);
        mob_dead_observer ghost = Ghost(r, 7, 5, "Ghosty");
        client player = r.In(() => ghost.client);
        var shell = r.In(() => Dm.@new<obj_effect_mob_spawn_ghost_role_drone>(locate(4, 3, 1)));
        r.In(() => SSticker.current_state = GAME_STATE_PLAYING);
        // tg's ghost HUD has the Spawners Menu button (ghost.dm:7); the menu lists the shell's group
        Assert.Contains(r.In(() => Dm.each(ghost.hud_used.screen_objects).Select(k => ghost.hud_used.screen_objects[k]).ToList()), o => o is atom_movable_screen_ghost_spawners_menu);
        r.In(() => ghost.open_spawners_menu());
        r.Step(2);
        var data = r.In(() => Waiting.NoSleep(ghost.spawners_menu.ui_static_data(ghost)) as DmList);
        var group = r.In(() => Dm.each(data["spawners"] as DmList).Cast<DmList>().Single());
        Assert.Equal("drone shell", Dm.str(r.In(() => group["name"])));
        Assert.Equal(1f, r.In(() => Dm.num(group["amount_left"])));
        // Spawn: the ghost follows the shell and gets its prompt
        Act(r, ghost, "SpawnersMenu", "spawn", "name", "drone shell");
        Act(r, ghost, "AlertModal", "choose", "choice", "Yes");
        r.Step(Second);
        Assert.IsType<mob_living_basic_drone>(r.In(() => player.mob));
        Assert.True(r.In(() => QDELETED(shell)), "the shell is used up");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDispenserMakesShellsFromIronAndGlass()
    {
        var (r, _) = Room(9903);
        var dispenser = r.In(() =>
        {
            var d = Dm.@new<obj_machinery_drone_dispenser>(locate(6, 5, 1));
            Waiting.NoSleep(d.materials.insert_amount_mat(SHEET_MATERIAL_AMOUNT * 5, typeof(datum_material_iron)));
            Waiting.NoSleep(d.materials.insert_amount_mat(SHEET_MATERIAL_AMOUNT * 5, typeof(datum_material_glass)));
            return d;
        });
        r.In(() => dispenser.process(1)); // ready: it whirs to life
        Assert.Equal("production", r.In(() => dispenser.mode));
        r.Step(4 * Second);
        r.In(() => dispenser.process(1)); // production: a shell
        Assert.Equal(1, r.In(() => dispenser.count_shells()));
        Assert.Equal("recharging", r.In(() => dispenser.mode));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADroneIsRepairedReactivatedHackedAndReset()
    {
        var (r, bob) = Room(9904);
        var drone = r.In(() => Dm.@new<mob_living_basic_drone>(locate(4, 5, 1)));
        var other = r.In(() => Dm.@new<mob_living_basic_drone>(locate(5, 5, 1)));
        r.Step(2);
        // a screwdriver tightens a hurt drone's screws
        r.In(() => Waiting.NoSleep(drone.adjust_brute_loss(20)));
        var screwdriver = r.In(() => Dm.@new<obj_item_screwdriver>(bob.loc));
        r.In(() => Waiting.NoSleep(bob.put_in_hands(screwdriver)));
        r.In(() => Waiting.Detach(drone.screwdriver_act(bob, screwdriver)));
        r.Step(9 * Second);
        Assert.Equal(drone.maxHealth, r.In(() => drone.health));

        // hacked: the laws turn and it is no longer shy; a wrench resets it
        r.In(() =>
        {
            Waiting.NoSleep(drone.mind_initialize());
            Waiting.NoSleep(drone.update_drone_hack(TRUE));
            Assert.Equal(TRUE, drone.hacked);
            Assert.StartsWith("1. You must always involve yourself", drone.laws);
            Assert.False(HAS_TRAIT(drone, TRAIT_PACIFISM));
            Assert.Equal("drone_maint_red", drone.icon_living);
        });
        var wrench = r.In(() => Dm.@new<obj_item_wrench>(bob.loc));
        r.In(() =>
        {
            Waiting.NoSleep(bob.dropItemToGround(screwdriver));
            Waiting.NoSleep(bob.put_in_hands(wrench));
            Waiting.Detach(drone.wrench_act(bob, wrench));
        });
        r.Step(6 * Second);
        Assert.Equal(FALSE, r.In(() => drone.hacked));
        Assert.True(r.In(() => HAS_TRAIT(drone, TRAIT_PACIFISM)));

        // a hat shows in the examine
        r.In(() =>
        {
            var hat = Dm.@new<obj_item_clothing_head_soft>(drone.loc);
            Waiting.NoSleep(drone.equip_to_slot(hat, ITEM_SLOT_HEAD));
            Assert.Same(hat, drone.head);
            var lines = Waiting.NoSleep(drone.examine(bob)) as DmList;
            Assert.Contains(Dm.each(lines).Select(Dm.str), l => l.Contains("on its head"));
        });
        r.AssertNoRuntimes();
    }
}
