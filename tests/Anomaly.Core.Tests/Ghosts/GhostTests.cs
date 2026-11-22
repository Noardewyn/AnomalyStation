using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Ghosts;

/// <summary>
/// The ghosts in the owner's minimal scope (2026-10-16, D151; modules/mob/dead/observer/observer.dm, dead.dm, datums/mind):
/// a dead player who moves becomes an observer (client/Move's ghostize) with the body's mind and the player's key; the ghost
/// flies through walls (abstract_move), is invisible to the living (INVISIBILITY_OBSERVER) and sees ghosts
/// (SEE_INVISIBLE_OBSERVER); the HUD's Re-enter Corpse button puts the player back into the body and the empty ghost is
/// deleted; gib ghostizes and blows the body apart (M6 slice 5); a revive grabs the ghost back.
/// </summary>
public class GhostTests
{
    private const int W = 20, H = 16;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(W, H, seed: seed);
        r.In(() =>
        {
            for (int x = 2; x <= W - 1; x++)
                for (int y = 2; y <= H - 1; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
        });
        r.Step(20);
        return r;
    }

    private static turf At(int x, int y) => locate(x, y, 1);

    private static void Kill(AudioRoom r, mob_living body) => r.In(() => Waiting.NoSleep(body.death()));

    // Holds a direction for some ticks, as the player's held keys do (keyDown, keyLoop, client/Move), then lets it go.
    private static void Hold(AudioRoom r, client player, int dir, int ticks)
    {
        r.Command(() => ClientSeam.SetHeldDirections(player, dir));
        r.Step(ticks);
        r.Command(() => ClientSeam.SetHeldDirections(player, 0));
        r.Step();
    }

    [Fact]
    public void APlayerHasAMindWhoseCurrentIsTheBody()
    {
        var r = Room(15101);
        mob_living_carbon_human body = r.Join(5, 5, "Alice");
        r.In(() =>
        {
            Assert.NotNull(body.mind);
            Assert.Same(body, body.mind.current);
            Assert.Equal("alice", Dm.ckey(body.mind.key));
            Assert.Equal(TRUE, body.mind.active);
            Assert.Equal(body.real_name, body.mind.name);
            Assert.Same(body.mind, body.last_mind);
            Assert.Contains(body.mind, Dm.each_cast<datum_mind>(SSticker.minds));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACorpseSeesNoTurfsAndGetsAnOptionalNightVisionActionTakenBackOnRevive()
    {
        var r = Room(15198);
        var body = r.Join(5, 5, "Alice");
        client player = body.client;
        Kill(r, body);
        r.Step(2);
        var action = r.In(() => Dm.each<datum_action>(body.actions).OfType<datum_action_item_action_toggle_nv>().Single());
        var button = r.In(() => action.viewers[body.hud_used] as atom_movable_screen_movable_action_button);
        Assert.NotNull(button);
        r.In(() =>
        {
            Assert.Equal(0, Dm.num(Dm.bitand(body.sight, SEE_TURFS)));
            Assert.Equal(0, body.lighting_cutoff);
            Assert.Empty(Dm.each(body.lighting_color_cutoffs));
        });

        r.Command(() => ClientSeam.Click(player, button, "left=1;button=left"));
        r.Step(3);
        r.In(() => Assert.Equal(new[] { 10f, 25f, 10f }, Dm.each(body.lighting_color_cutoffs).Select(v => Dm.num(v))));
        r.In(() => body.update_sight());
        r.In(() => Assert.Equal(new[] { 10f, 25f, 10f }, Dm.each(body.lighting_color_cutoffs).Select(v => Dm.num(v))));

        r.In(() => Waiting.NoSleep(body.revive(HEAL_ALL)));
        r.Step(2);
        r.In(() =>
        {
            Assert.DoesNotContain(action, Dm.each<datum_action>(body.actions));
            Assert.False(Dm.truthy(body.get_client_colour(action)));
            Assert.All(Dm.each(body.lighting_color_cutoffs), v => Assert.Equal(0f, Dm.num(v))); // the living eyes' own cutoffs again
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDevReviveBringsBackABodyWhoseBrainWasTakenOut()
    {
        // the owner (2026-10-26): revive failed on a body without its brain; tg's admin heal (ADMIN_HEAL_ALL) regrows the organs
        var r = Room(15197);
        var body = r.Join(5, 5, "Alice");
        Kill(r, body);
        r.In(() =>
        {
            var brain = (obj_item_organ_brain)body.get_organ_by_type(typeof(obj_item_organ_brain));
            Waiting.NoSleep(brain.Remove(body));
            Waiting.NoSleep(brain.forceMove(body.loc));
        });
        r.Step(2);
        Assert.Null(r.In(() => body.get_organ_by_type(typeof(obj_item_organ_brain))));
        r.In(() => DevCommands.Revive(body));
        r.Step(5);
        r.In(() =>
        {
            Assert.NotEqual(DEAD, Dm.num(body.stat));
            Assert.NotNull(body.get_organ_by_type(typeof(obj_item_organ_brain)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDevReviveOfAGibbedPlayersGhostGivesItANewBody()
    {
        var r = Room(15196);
        var body = r.Join(8, 8, "Alice");
        client player = body.client;
        string name = r.In(() => (string)body.real_name);
        r.In(() => Waiting.Detach(body.gib()));
        r.Step();
        var ghost = Assert.IsType<mob_dead_observer>(player.mob);
        r.In(() => DevCommands.ReviveGhost(ghost));
        r.Step(2);
        var human = Assert.IsType<mob_living_carbon_human>(player.mob);
        r.In(() =>
        {
            Assert.NotEqual(DEAD, Dm.num(human.stat));
            Assert.Equal(name, (string)human.real_name);
            Assert.Same(At(8, 8), human.loc);
            Assert.Same(human, human.mind.current);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCeilingOutletsUpwardWindMovesNobodyOffTheirLevel()
    {
        // DV179: a hole in the ceiling vents UP; the wind under it carried a player onto z 2
        var r = Room(15195);
        var body = r.Join(5, 5, "Alice");
        r.In(() =>
        {
            var floor = (turf_open)At(5, 5);
            floor.pressure_difference = 5000;
            floor.pressure_direction = UP;
            for (int i = 0; i < 20; i++)
            {
                body.last_high_pressure_movement_air_cycle = -1;
                floor.high_pressure_movements();
            }
            Assert.Same(floor, body.loc);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AGhostGetsTheActualNightVisionItemActionAndCanToggleItThroughItsHudIcon()
    {
        var r = Room(15199);
        var body = r.Join(5, 5, "Alice");
        client player = body.client;
        Kill(r, body);
        Hold(r, player, (int)NORTH, 1);
        var ghost = Assert.IsType<mob_dead_observer>(player.mob);
        var action = r.In(() => Dm.each<datum_action>(ghost.actions).OfType<datum_action_item_action_toggle_nv>().Single());
        var goggles = Assert.IsType<obj_item_clothing_glasses_night>(action.target);
        var button = r.In(() => action.viewers[ghost.hud_used] as atom_movable_screen_movable_action_button);
        Assert.NotNull(button);
        Assert.True(r.In(() => Dm.@in(button, player.screen)));
        Assert.Equal(0, ghost.lighting_cutoff);
        Assert.Empty(Dm.each(ghost.lighting_color_cutoffs));
        Assert.Equal("night_off", goggles.icon_state);

        r.Command(() => ClientSeam.Click(player, button, "left=1;button=left"));
        r.Step(3);
        r.In(() =>
        {
            Assert.Equal(new[] { 10f, 25f, 10f }, Dm.each(ghost.lighting_color_cutoffs).Select(v => Dm.num(v)));
            Assert.IsType<datum_client_colour_glass_colour_lightgreen>(ghost.get_client_colour(action));
            Assert.Equal("night", goggles.icon_state);
        });
        Assert.Contains(r.Sounds.Heard, sound => sound.File?.EndsWith("night_vision_on.ogg", StringComparison.Ordinal) == true);

        r.Command(() => ClientSeam.Click(player, button, "left=1;button=left"));
        r.Step(3);
        r.In(() =>
        {
            Assert.Empty(Dm.each(ghost.lighting_color_cutoffs));
            Assert.False(Dm.truthy(ghost.get_client_colour(action)));
            Assert.Equal("night_off", goggles.icon_state);
        });

        r.Command(() => ClientSeam.Click(player, button, "left=1;button=left"));
        r.Step(3);
        r.In(() => ghost.reenter_corpse());
        r.Step(2);
        Assert.Same(body, player.mob);
        r.In(() =>
        {
            Assert.DoesNotContain(action, Dm.each<datum_action>(body.actions));
            Assert.False(Dm.truthy(body.get_client_colour(action)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADeadPlayerWhoMovesBecomesAGhostWithTheKeyAndTheBodysMind()
    {
        var r = Room(15102);
        mob_living_carbon_human body = r.Join(5, 5, "Alice");
        client player = body.client;
        Kill(r, body);
        Hold(r, player, (int)NORTH, 1);
        mob_dead_observer ghost = player.mob as mob_dead_observer;
        Assert.NotNull(ghost);
        r.In(() =>
        {
            Assert.Null(body.client);
            Assert.Null(body.key);
            Assert.Equal("alice", ghost.ckey);
            Assert.Same(body.mind, ghost.mind); // the ghost keeps a reference to the body's mind; the body keeps it too
            Assert.Same(body, ghost.mind.current);
            Assert.Equal(TRUE, ghost.can_reenter_corpse);
            Assert.Equal(body.real_name, ghost.real_name);
            Assert.Equal(body.name, ghost.name);
            Assert.Same(At(5, 5), ghost.loc); // abstract_move to the body's turf
            Assert.Equal((float)INVISIBILITY_OBSERVER, ghost.invisibility);
            Assert.Equal((float)SEE_INVISIBLE_OBSERVER, ghost.see_invisible);
            Assert.Equal((float)DEAD, ghost.stat);
            Assert.Contains(ghost, Dm.each_cast<mob>(GLOB.dead_mob_list));
            Assert.Contains(ghost, Dm.each_cast<mob>(GLOB.player_list));
            Assert.Equal(body.timeofdeath, ghost.persistent_client.time_of_death);
            // the ghost HUD: tg's Re-enter Corpse button on the client's screen
            Assert.Contains(Dm.each(player.screen), o => o is atom_movable_screen_ghost_reenter_corpse);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGhostFliesThroughWallsATileEveryTwoTicks()
    {
        var r = Room(15103);
        r.In(() =>
        {
            for (int x = 2; x <= W - 1; x++)
                Waiting.NoSleep(locate(x, 8, 1).ChangeTurf(typeof(turf_closed_wall), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
        });
        mob_living_carbon_human body = r.Join(5, 5, "Alice");
        client player = body.client;
        Kill(r, body);
        Hold(r, player, (int)NORTH, 1); // the first step ghostizes
        mob_dead_observer ghost = (mob_dead_observer)player.mob;
        Hold(r, player, (int)NORTH, 12);
        r.In(() =>
        {
            Assert.True(ghost.y >= 10, "the ghost stopped at the wall: " + ghost.x + "," + ghost.y);
            Assert.True(ghost.y <= 12, "DV54: a tile every GhostMoveDelay (two ticks), not tg's every tick: " + ghost.y);
            Assert.Equal(5f, ghost.x);
            Assert.Equal(16f, ghost.glide_size); // DV54: a tile glided over the two ticks (tg's ghost glides 32, observer.dm:328)
            Assert.Same(At(5, 5), body.loc); // the body stays
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ReenterCorpseFromTheHudPutsThePlayerBackAndTheGhostIsDeleted()
    {
        var r = Room(15104);
        mob_living_carbon_human body = r.Join(5, 5, "Alice");
        client player = body.client;
        Kill(r, body);
        Hold(r, player, (int)EAST, 3);
        mob_dead_observer ghost = (mob_dead_observer)player.mob;
        r.In(() => Assert.NotSame(At(5, 5), ghost.loc));
        atom button = r.In(() => Dm.each(player.screen).OfType<atom_movable_screen_ghost_reenter_corpse>().Single());
        r.Command(() => ClientSeam.Click(player, button, "left=1"));
        r.Step(2);
        r.In(() =>
        {
            Assert.Same(body, player.mob);
            Assert.Equal("alice", body.ckey);
            Assert.True(QDELETED(ghost), "the empty ghost was not deleted (observer/logout.dm's spawn)");
            Assert.Same(body, body.mind.current);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AGhostCannotReenterABodyThatIsGoneOrTakesASecondTry()
    {
        var r = Room(15105);
        mob_living_carbon_human body = r.Join(5, 5, "Alice");
        client player = body.client;
        Kill(r, body);
        Hold(r, player, (int)WEST, 1);
        mob_dead_observer ghost = (mob_dead_observer)player.mob;
        r.Command(() =>
        {
            World.Current.usr = ghost;
            ghost.can_reenter_corpse = FALSE; // tg's DNR leaves it so (stay_dead, observer.dm:400)
            ghost.reenter_corpse();
        });
        r.Step();
        Assert.Same(ghost, player.mob);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void GibGhostizesAndTheBodyIsBlownApart()
    {
        var r = Room(15106);
        mob_living_carbon_human body = r.Join(8, 8, "Alice");
        client player = body.client;
        datum_mind mind = body.mind;
        r.In(() => Waiting.Detach(body.gib()));
        r.Step();
        mob_dead_observer ghost = player.mob as mob_dead_observer;
        Assert.NotNull(ghost);
        r.In(() =>
        {
            Assert.True(QDELETED(body)); // M6 slice 5: tg's gib deletes the body (D150's stub is gone)
            Assert.Same(At(8, 8), ghost.loc);
            Assert.Same(mind, ghost.mind);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AReviveGrabsTheGhostBackIntoTheBody()
    {
        var r = Room(15107);
        mob_living_carbon_human body = r.Join(5, 5, "Alice");
        client player = body.client;
        Kill(r, body);
        Hold(r, player, (int)SOUTH, 2);
        mob_dead_observer ghost = (mob_dead_observer)player.mob;
        r.In(() => Waiting.NoSleep(body.revive(HEAL_ALL)));
        r.Step(2);
        r.In(() =>
        {
            Assert.NotEqual((float)DEAD, body.stat);
            Assert.Same(body, player.mob);
            Assert.True(QDELETED(ghost));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TwoRunsOfTheSameGhostScenarioHashTheSame()
    {
        static ulong Run()
        {
            var r = Room(15108);
            mob_living_carbon_human body = r.Join(5, 5, "Alice");
            client player = body.client;
            Kill(r, body);
            Hold(r, player, (int)NORTH | (int)EAST, 4);
            mob_dead_observer ghost = (mob_dead_observer)player.mob;
            r.Command(() =>
            {
                World.Current.usr = ghost;
                ghost.reenter_corpse();
            });
            r.Step(3);
            r.AssertNoRuntimes();
            return r.World.ComputeStateHash();
        }
        Assert.Equal(Run(), Run());
    }
}
