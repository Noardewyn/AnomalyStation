using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Engineering;

/// <summary>
/// Engineering package E16 (machinery/big_manipulator) in a booted toy room: a manipulator built with a servo takes tg's tasks through
/// its window, picks an item up from the tile west of it and drops it on the tile east of it (its arm turning toward each in 45 degree
/// animate steps), waits for something to enter a task's tile when nothing can run, keeps the drop's overflow rule, throws, refuses to
/// run unanchored, writes and reads its tasks to and from a task disk, and an ID card locks its window.
/// </summary>
public class BigManipulatorTests
{
    private const int Second = 20;
    private const int X = 6, Y = 5;

    private static (AudioRoom r, mob_living_carbon_human bob, obj_machinery_big_manipulator m) Room(ulong seed = 16101)
    {
        var r = new AudioRoom(12, 10, seed: seed);
        mob_living_carbon_human bob = r.Join(X, Y + 2, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        var m = r.In(() => Dm.@new<obj_machinery_big_manipulator>(locate(X, Y, 1)));
        return (r, bob, m);
    }

    private static object Act(AudioRoom r, obj_machinery_big_manipulator m, mob user, string action, DmList @params = null)
        => r.In(() => Waiting.NoSleep(m.ui_act(action, @params ?? Dm.list(), Dm.@new<datum_tgui>(user, m, "BigManipulator"))));

    private static datum_manipulator_task Task(AudioRoom r, obj_machinery_big_manipulator m, mob user, string type, int dx, int dy)
        => r.In(() => (datum_manipulator_task)m.create_new_task(user, type, locate(X + dx, Y + dy, 1)));

    private static float ArmAngle(AudioRoom r, obj_machinery_big_manipulator m) => r.In(() => Dm.num(((m.manipulator_arm.transform as matrix) ?? matrix()).get_angle()));

    [Fact]
    public void ItPicksAnItemUpWestAndDropsItEast()
    {
        var (r, bob, m) = Room();
        r.In(() =>
        {
            // the servo's tier 1: tg's speed range and task limit, the yellow greyscale, the arm in its vis_contents
            Assert.NotNull(Dm.locate<datum_stock_part_servo>(m.component_parts));
            Assert.Equal((MIN_SPEED_MULTIPLIER_TIER_1, MAX_SPEED_MULTIPLIER_TIER_1, MAX_TASKS_TIER_1), (m.min_speed_multiplier, m.max_speed_multiplier, m.interaction_point_limit));
            Assert.Contains(m.manipulator_arm, Dm.each(m.vis_contents));
            Assert.Same(m, m.manipulator_arm.loc);
            Assert.IsType<datum_tasking_strategy_sequential>(m.master_tasking);
        });
        var pickup = Task(r, m, bob, TASK_TYPE_PICKUP, -1, 0);
        var drop = Task(r, m, bob, TASK_TYPE_DROP, 1, 0);
        r.In(() =>
        {
            Assert.Equal((-1f, 0f), (Dm.num(((datum_manipulator_task_cargo)pickup).offset_dx), Dm.num(((datum_manipulator_task_cargo)pickup).offset_dy)));
            var data = (DmList)Waiting.NoSleep(m.ui_data(bob));
            var tasks = (DmList)data["tasks_data"];
            Assert.Equal(2f, tasks.len);
            Assert.Equal(("pickup", "-1,0", "Wait For Suiting"), (Dm.str(((DmList)tasks[1])["task_type"]), Dm.str(((DmList)tasks[1])["turf"]), Dm.str(((DmList)tasks[1])["pickup_eagerness"])));
            DmList prios = (DmList)((DmList)tasks[1])["settings_list"];
            Assert.Equal(new[] { "PICK UP ITEMS", "PICK UP CLOSETS" }, Dm.each(prios).Select(p => Dm.str(((DmList)p)["name"])));
            Assert.Equal(("drop", "ALLOW"), (Dm.str(((DmList)tasks[2])["task_type"]), Dm.str(((DmList)tasks[2])["overflow_status"])));
            Assert.Equal(Dm.text("[],[]", X, Y), Dm.str(data["manipulator_position"]));
        });

        // nothing to pick up: it waits for its tiles
        Act(r, m, bob, "run_cycle");
        Assert.True(r.In(() => Dm.truthy(m.on)));
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(m.waiting_for_signal));
            Assert.Null(m.current_task);
            Assert.Equal(2f, m.signal_turfs.len);
        });

        // an item put on the pickup tile wakes it: the arm turns west (90 degrees from its north: two 45 degree steps) and picks it up
        var wrench = r.In(() => Dm.@new<obj_item_wrench>(locate(X - 1, Y + 2, 1)));
        r.In(() => Waiting.NoSleep(wrench.forceMove(locate(X - 1, Y, 1))));
        r.Step(Second);
        float pickedAngle = ArmAngle(r, m);
        Assert.Equal(r.In(() => Dm.num(dir2angle(WEST))), (pickedAngle + 360) % 360, 0.5f);
        r.Step(3 * Second);
        Assert.Equal((X + 1f, (float)Y), r.In(() => (((atom)wrench.loc).x, ((atom)wrench.loc).y)));
        Assert.Equal(r.In(() => Dm.num(dir2angle(EAST))), (ArmAngle(r, m) + 360) % 360, 0.5f);
        r.In(() =>
        {
            Assert.Null(m.held_object);
            Assert.Null(m.manipulator_arm.item_in_my_claw);
        });

        // stopping
        Act(r, m, bob, "run_cycle");
        r.Step(2 * Second);
        Assert.False(r.In(() => Dm.truthy(m.on) || Dm.truthy(m.stopping)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheClawShowsTheHeldItemAndTheWiresAndDropWork()
    {
        var (r, bob, m) = Room();
        var pickup = Task(r, m, bob, TASK_TYPE_PICKUP, 0, -1);
        r.In(() =>
        {
            ((datum_manipulator_task_cargo_pickup)pickup).pickup_eagerness = PICKUP_EAGER;
            Dm.@new<obj_item_crowbar>(locate(X, Y - 1, 1));
        });
        Act(r, m, bob, "run_cycle");
        r.Step(2 * Second); // south is four 45 degree steps from the arm's north, 0.3 s each
        r.In(() =>
        {
            var crowbar = m.held_object?.resolve() as obj_item_crowbar;
            Assert.NotNull(crowbar);
            Assert.Same(m, crowbar.loc);
            Assert.NotNull(m.manipulator_arm.icon_overlay);
            Assert.Equal(crowbar.icon_state, m.manipulator_arm.icon_overlay.icon_state);
            // the wires' status lights
            DmList status = m.wires.get_status();
            Assert.Contains("The small light bulb is glowing bright green.", Dm.each(status).Select(Dm.str));
            Assert.Contains("The big light bulb is glowing green.", Dm.each(status).Select(Dm.str));
        });
        Act(r, m, bob, "drop_held_atom");
        r.In(() =>
        {
            Assert.Null(m.held_object);
            Assert.Single(Dm.each<obj_item_crowbar>(locate(X, Y, 1).contents));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheWindowEditsTasksAndTheDiskKeepsThem()
    {
        var (r, bob, m) = Room();
        Act(r, m, bob, "create_task", Dm.list(Dm.kv("task_type", "throw")));
        Act(r, m, bob, "create_task", Dm.list(Dm.kv("task_type", "wait")));
        r.In(() =>
        {
            Assert.Equal(2f, m.tasks.len);
            var throwTask = (datum_manipulator_task_cargo_dropoff_base_throw)m.tasks[1];
            string id = Dm.str(REF(throwTask));
            Waiting.NoSleep(m.ui_act("adjust_task_param", Dm.list(Dm.kv("taskId", id), Dm.kv("param", "cycle_throw_range")), Dm.@new<datum_tgui>(bob, m, "BigManipulator")));
            Assert.Equal(2f, throwTask.throw_range);
            Waiting.NoSleep(m.ui_act("adjust_task_param", Dm.list(Dm.kv("taskId", id), Dm.kv("param", "move_to"), Dm.kv("value", Dm.list(Dm.kv("buttonNumber", 2)))), Dm.@new<datum_tgui>(bob, m, "BigManipulator")));
            Assert.Equal((0f, 1f), (Dm.num(throwTask.offset_dx), Dm.num(throwTask.offset_dy)));
            Assert.Same(locate(X, Y + 1, 1), throwTask.interaction_turf);
            Waiting.NoSleep(m.ui_act("adjust_task_param", Dm.list(Dm.kv("taskId", id), Dm.kv("param", "move_down")), Dm.@new<datum_tgui>(bob, m, "BigManipulator")));
            Assert.Same(throwTask, m.tasks[2]);
            var wait = (datum_manipulator_task_simple_wait)m.tasks[1];
            Waiting.NoSleep(m.ui_act("adjust_task_param", Dm.list(Dm.kv("taskId", REF(wait)), Dm.kv("param", "set_wait_time"), Dm.kv("value", 90)), Dm.@new<datum_tgui>(bob, m, "BigManipulator")));
            Assert.Equal(60f, wait.time_seconds);
            Waiting.NoSleep(m.ui_act("cycle_tasking_strategy", Dm.list(Dm.kv("new_strategy", TASKING_STRICT)), Dm.@new<datum_tgui>(bob, m, "BigManipulator")));
            Assert.IsType<datum_tasking_strategy_strict>(m.master_tasking);
            Waiting.NoSleep(m.ui_act("adjust_interaction_speed", Dm.list(Dm.kv("new_speed", 9)), Dm.@new<datum_tgui>(bob, m, "BigManipulator")));
            Assert.Equal(MAX_SPEED_MULTIPLIER_TIER_1, m.speed_multiplier);

            // the task disk: written, cleared from the machine, read back
            var disk = Dm.@new<obj_item_disk_manipulator>(m);
            m.task_disk = disk;
            Assert.Equal(TRUE, Dm.num(m.write_disk_tasks(bob)));
            Assert.Equal(2f, disk.tasks_data.len);
            QDEL_LIST(m.tasks);
            m.tasks = Dm.list();
            Assert.Equal(TRUE, Dm.num(m.read_disk_tasks(bob)));
            Assert.Equal(2f, m.tasks.len);
            var read = (datum_manipulator_task_cargo_dropoff_base_throw)m.tasks[2];
            Assert.Equal((2f, 0f, 1f), (read.throw_range, Dm.num(read.offset_dx), Dm.num(read.offset_dy)));
            Assert.Same(locate(X, Y + 1, 1), read.interaction_turf);
            Assert.Equal(60f, ((datum_manipulator_task_simple_wait)m.tasks[1]).time_seconds);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnIdCardLocksItAndItNeedsAnchoringToRun()
    {
        var (r, bob, m) = Room();
        r.In(() =>
        {
            var card = Dm.@new<obj_item_card_id>(bob.loc);
            Assert.Equal(ITEM_INTERACT_SUCCESS, Dm.num(Waiting.NoSleep(m.item_interaction(bob, card))));
            Assert.NotNull(m.id_lock);
            Waiting.NoSleep(m.ui_interact(bob));
            Assert.Null(SStgui.get_open_ui(bob, m));
            var other = Dm.@new<obj_item_card_id>(bob.loc);
            Assert.Equal(ITEM_INTERACT_BLOCKING, Dm.num(Waiting.NoSleep(m.item_interaction(bob, other))));
            Assert.Equal(ITEM_INTERACT_SUCCESS, Dm.num(Waiting.NoSleep(m.item_interaction(bob, card))));
            Assert.Null(m.id_lock);

            Waiting.NoSleep(m.set_anchored(FALSE));
            Waiting.NoSleep(m.try_press_on(bob));
            Assert.False(Dm.truthy(m.on));
        });
        r.AssertNoRuntimes();
    }
}
