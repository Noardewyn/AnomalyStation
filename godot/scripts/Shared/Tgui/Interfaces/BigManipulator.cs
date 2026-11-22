using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/BigManipulator/index.tsx and types.ts (engineering package
// E16). The window reads tg's ui_data of the big manipulator (big_manipulator.dm:445) and sends its acts: run_cycle,
// adjust_interaction_speed, drop_held_atom, unbuckle, the disk's disk_eject, disk_read, disk_write and disk_clear, create_task,
// cycle_tasking_strategy, reset_tasking_index and adjust_task_param (taskId, param, value). The task editor is tgui-core's Modal, kept
// open by the task's id (React's editingTask, refreshed from the live data as tg's useEffect does); the point grid's CSS grid of nine
// buttons is three rows of three, and BlockQuote a box.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class BigManipulator
{
    // tg: index.tsx:20
    private static readonly (string Key, string Label)[] TaskTypeLabels =
    {
        ("pickup", "Pick up..."),
        ("drop", "Drop..."),
        ("throw", "Throw..."),
        ("use", "Use held..."),
        ("interact", "Interact..."),
        ("move", "Move..."),
        ("wait", "Wait..."),
        ("stop", "Stop..."),
    };

    // tg: index.tsx:31
    private static readonly Dictionary<string, string> TaskTypeIcons = new()
    {
        ["pickup"] = "hand",
        ["drop"] = "box-open",
        ["throw"] = "paper-plane",
        ["use"] = "mouse-pointer",
        ["interact"] = "bolt",
        ["move"] = "arrows-alt",
        ["wait"] = "hourglass-half",
        ["stop"] = "stop-circle",
    };

    // tg: index.tsx:42
    private static readonly Dictionary<string, string> TaskingStrategyIcons = new()
    {
        ["Sequential"] = "list-ol",
        ["Strict order"] = "lock",
    };

    // tg: index.tsx:47
    private static readonly string[] ButtonNumberToIcon = { "", "", "arrow-up", "", "arrow-left", "arrows-to-dot", "arrow-right", "", "arrow-down", "" };

    private static string Label(string key) => TaskTypeLabels.FirstOrDefault(t => t.Key == key).Label;

    // tg: index.tsx:571  BigManipulator
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool active = data.Truthy("active");
        bool stopping = data.Truthy("stopping");
        bool diskInserted = data.Truthy("disk_inserted");
        bool diskReadOnly = data.Truthy("disk_read_only");

        // tg: index.tsx:576  <Window title="Manipulator Interface" width={420} height={560}>
        return c.Window(420, 560, "Manipulator Interface",
            new UiNode(UiKind.Section)
            {
                Key = "action",
                Title = "Action Panel",
                // tg: index.tsx:581
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "run_cycle",
                    Icon = !active ? "play" : stopping ? "hourglass-start" : "stop",
                    Color = !active ? "good" : stopping ? "blue" : "bad",
                    Text = !active ? "Run" : stopping ? "Stopping" : "Stop",
                    OnClick = () => c.Act("run_cycle"),
                },
            }.With(MasterControls(c, data)),
            // tg: index.tsx:594  the task disk's row
            new UiNode(UiKind.Section) { Key = "disk" }.With(
                new UiNode(UiKind.Stack).With(
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Button)
                    {
                        Key = "disk_eject",
                        Fluid = true,
                        Icon = diskInserted ? "eject" : "info",
                        Disabled = !diskInserted || active || stopping,
                        Color = diskInserted ? null : "none",
                        Text = diskInserted ? $"floppy drive (tasks: {(int)data.Num("disk_task_count")})" : "No drives inserted",
                        OnClick = () => c.Act("disk_eject"),
                    }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "disk_read",
                        Icon = "download",
                        Text = "Read",
                        Disabled = !diskInserted || active || stopping,
                        OnClick = () => c.Act("disk_read"),
                    }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "disk_write",
                        Icon = "upload",
                        Text = "Write",
                        Disabled = !diskInserted || diskReadOnly || active || stopping,
                        OnClick = () => c.Act("disk_write"),
                    }),
                    new UiNode(UiKind.StackItem).With(ConfirmWith(c, "disk_clear", new UiNode(UiKind.Button)
                    {
                        Icon = "trash",
                        Text = "Clear",
                        Disabled = !diskInserted || diskReadOnly || active || stopping,
                        OnClick = () => c.Act("disk_clear"),
                    }, "Clear?")))),
            TaskList(c, data));
    }

    // tg: index.tsx:59  MasterControls
    private static UiNode MasterControls(TguiContext c, UiValue data)
    {
        double speed = data.Num("speed_multiplier");
        double minSpeed = data.Num("min_speed_multiplier");
        double maxSpeed = data.Num("max_speed_multiplier");
        return new UiNode(UiKind.Stack) { Key = "master" }.With(
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
            {
                Key = "speed_min",
                Icon = "backward-step",
                OnClick = () => c.Act("adjust_interaction_speed", ("new_speed", minSpeed)),
            }),
            // tg: index.tsx:75
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Slider)
            {
                Key = "speed",
                Step = 0.1,
                Value = speed,
                MinValue = minSpeed,
                MaxValue = maxSpeed,
                Unit = "x",
                StepPixelSize = 20,
                OnChange = value => c.Act("adjust_interaction_speed", ("new_speed", value)),
            }),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
            {
                Key = "speed_max",
                Icon = "forward-step",
                OnClick = () => c.Act("adjust_interaction_speed", ("new_speed", maxSpeed)),
            }),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
            {
                Key = "drop",
                Icon = "eject",
                Tooltip = "Disengage the claws, dropping the held item",
                Text = "Drop",
                OnClick = () => c.Act("drop_held_atom"),
            }),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
            {
                Key = "unbuckle",
                Icon = "person-walking-arrow-right",
                Tooltip = "Unbuckle the worker",
                Text = "Unbuckle",
                OnClick = () => c.Act("unbuckle"),
            }));
    }

    // tg: index.tsx:136  ConfigRow
    private static UiNode ConfigRow(string key, string label, string content, Action onClick, string tooltip = "", bool selected = false) =>
        new UiNode(UiKind.TableRow) { Key = "row:" + key, Candystripe = true }.With(
            new UiNode(UiKind.TableCell).With(new UiNode(UiKind.Box) { Text = label }),
            new UiNode(UiKind.TableCell) { TextAlign = "right" }.With(new UiNode(UiKind.Button)
            {
                Key = key,
                Tooltip = string.IsNullOrEmpty(tooltip) ? null : tooltip,
                Selected = selected,
                Text = content,
                OnClick = onClick,
            }));

    // tg: index.tsx:161  getPointButtonNumber
    private static int? GetPointButtonNumber(string offset)
    {
        string[] parts = (offset ?? "").Split(',');
        if (parts.Length < 2 || !double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double dx)
            || !double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double dy))
            return null;
        if (dx < -1 || dx > 1 || dy < -1 || dy > 1)
            return null;
        if (dx == 0 && dy == 0)
            return null;
        double xIndex = dx + 1;
        double yIndex = 1 - dy;
        return (int)(yIndex * 3 + xIndex + 1);
    }

    // tg: index.tsx:177  TaskEditModal
    private static UiNode TaskEditModal(TguiContext c, UiValue task)
    {
        string taskId = task.Str("id");
        string taskType = task.Str("task_type");
        void Adjust(string param, object value = null) => c.Act("adjust_task_param", ("taskId", taskId), ("param", param), ("value", value));

        bool isCargo = !string.IsNullOrEmpty(task.Str("turf"));
        bool isPickup = taskType.Contains("pickup");
        bool isDropoff = taskType == "drop" || taskType == "throw" || taskType == "use";
        bool isInteract = taskType.Contains("interact");
        bool isMove = taskType == "move";
        int? currentButton = isCargo ? GetPointButtonNumber(task.Str("turf")) : null;

        // tg: index.tsx:197
        var edit = new UiNode(UiKind.Section)
        {
            Key = "edit",
            Title = $"Edit: {task.Str("name")}",
            Buttons = new UiNode(UiKind.Button) { Key = "close", Icon = "xmark", Color = "bad", OnClick = () => c.SetState("editingTask", (string)null) },
        };

        // tg: index.tsx:204  the wait time
        if (taskType.Contains("wait"))
        {
            edit.With(new UiNode(UiKind.Table) { Key = "wait" }.With(
                new UiNode(UiKind.TableRow) { Candystripe = true }.With(
                    new UiNode(UiKind.TableCell).With(new UiNode(UiKind.Box) { Text = "Wait Time" }),
                    new UiNode(UiKind.TableCell).With(new UiNode(UiKind.Slider)
                    {
                        Key = "wait_time",
                        Value = task.Field("time") is { Kind: UiValueKind.Number } t ? t.AsNum() : 1,
                        MinValue = 1,
                        MaxValue = 60,
                        Step = 1,
                        StepPixelSize = 4,
                        Unit = "s",
                        OnChange = value => Adjust("set_wait_time", value),
                    }))));
        }

        // tg: index.tsx:224  the point grid and the settings
        if (isCargo)
        {
            var grid = new UiNode(UiKind.Stack) { Key = "grid", Vertical = true };
            for (int row = 0; row < 3; row++)
            {
                var line = new UiNode(UiKind.Stack);
                for (int col = 1; col <= 3; col++)
                {
                    int n = row * 3 + col;
                    line.With(new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "point:" + n,
                        Width = "18px",
                        Height = "18px",
                        Disabled = n == 5,
                        Color = currentButton == n ? "good" : "default",
                        Icon = string.IsNullOrEmpty(ButtonNumberToIcon[n]) ? null : ButtonNumberToIcon[n],
                        OnClick = () =>
                        {
                            if (n != 5)
                                Adjust("move_to", UiValue.ObjectOf(("buttonNumber", UiValue.Of(n))));
                        },
                    }));
                }
                grid.With(new UiNode(UiKind.StackItem).With(line));
            }

            var settings = new UiNode(UiKind.Table) { Key = "settings" };
            if (!isMove)
                settings.With(ConfigRow("toggle_filter_skip", "Use Filters", task.Truthy("filters_status") ? "TRUE" : "FALSE", () => Adjust("toggle_filter_skip"), "Toggle filter usage"));
            if (isPickup)
                settings.With(ConfigRow("cycle_pickup_eagerness", "Eagerness", task.Field("pickup_eagerness") is { Kind: UiValueKind.String } e ? e.AsStr() : "—", () => Adjust("cycle_pickup_eagerness"), "Wait for dropoff slot or pick up immediately"));
            if (isDropoff)
            {
                if (taskType == "drop")
                    settings.With(ConfigRow("cycle_overflow_status", "Overflow", task.Field("overflow_status") is { Kind: UiValueKind.String } o ? o.AsStr() : "—", () => Adjust("cycle_overflow_status"), "Cycle overflow behaviour"));
                if (taskType == "throw")
                    settings.With(ConfigRow("cycle_throw_range", "Throw Range", $"{(int)task.Num("throw_range")} TILES", () => Adjust("cycle_throw_range"), "Cycle throwing range"));
            }
            if (taskType == "use" || isInteract)
            {
                settings.With(
                    ConfigRow("toggle_worker_rmb", "Alt Click", task.Truthy("worker_use_rmb") ? "TRUE" : "FALSE", () => Adjust("toggle_worker_rmb"), "Simulate RMB click"),
                    ConfigRow("toggle_worker_combat", "Combat Mode", task.Truthy("worker_combat_mode") ? "TRUE" : "FALSE", () => Adjust("toggle_worker_combat"), "Use combat mode during interaction"),
                    ConfigRow("toggle_skip_anchored", "Skip Anchored", task.Truthy("skip_anchored") ? "TRUE" : "FALSE", () => Adjust("toggle_skip_anchored"), "Skip anchored objects when looking for interaction targets"));
            }

            edit.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem).With(grid),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(settings)));
        }

        var modal = new UiNode(UiKind.Modal) { Key = "editModal", Width = "340px" }.With(edit);

        // tg: index.tsx:300  the filters and the priorities
        if (isCargo && !isMove)
        {
            var filters = new UiNode(UiKind.Stack) { Vertical = true };
            IReadOnlyList<UiValue> itemFilters = task.Arr("item_filters");
            for (int index = 0; index < itemFilters.Count; index++)
            {
                int i = index;
                filters.With(new UiNode(UiKind.Stack) { Key = "filter:" + i }.With(
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Box) { Text = itemFilters[i].AsStr() }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "delete_filter:" + i,
                        Color = "transparent",
                        Icon = "xmark",
                        OnClick = () => Adjust("delete_filter", i),
                    })));
            }
            modal.With(new UiNode(UiKind.Section)
            {
                Key = "filters",
                Title = "Item Filters",
                Buttons = new UiNode(UiKind.Fragment).With(
                    new UiNode(UiKind.Button) { Key = "add_atom_filter_from_held", Icon = "plus", Text = "Add held", OnClick = () => Adjust("add_atom_filter_from_held") },
                    ConfirmWith(c, "reset_atom_filters", new UiNode(UiKind.Button) { Icon = "trash", OnClick = () => Adjust("reset_atom_filters") }, "Reset?")),
            }.With(filters));

            IReadOnlyList<UiValue> settingsList = task.Arr("settings_list");
            if (settingsList.Count > 0)
            {
                var priorities = new UiNode(UiKind.Table) { Key = "priorities" };
                for (int index = 0; index < settingsList.Count; index++)
                {
                    int i = index;
                    UiValue setting = settingsList[i];
                    priorities.With(new UiNode(UiKind.TableRow) { Key = "priority:" + setting.Str("name"), Candystripe = true }.With(
                        new UiNode(UiKind.TableCell).With(new UiNode(UiKind.Icon) { Icon = "hashtag" }, new UiNode(UiKind.Box) { Text = " " + (i + 1) }),
                        new UiNode(UiKind.TableCell).With(new UiNode(UiKind.Button)
                        {
                            Key = "toggle_priority:" + i,
                            Checkbox = setting.Truthy("active"),
                            Fluid = true,
                            Text = setting.Str("name"),
                            OnClick = () => Adjust("toggle_priority", i),
                        }),
                        new UiNode(UiKind.TableCell) { Width = "1em" }.With(new UiNode(UiKind.Button)
                        {
                            Key = "priority_move_up:" + i,
                            Icon = "arrow-up",
                            Disabled = i == 0,
                            OnClick = () =>
                            {
                                if (i > 0)
                                    Adjust("priority_move_up", i);
                            },
                        })));
                }
                modal.With(new UiNode(UiKind.Section) { Key = "prioritiesSection", Title = "Interaction Priorities" }.With(priorities));
            }
        }

        return modal;
    }

    // tg: index.tsx:382  TaskList
    private static UiNode TaskList(TguiContext c, UiValue data)
    {
        IReadOnlyList<UiValue> tasks = data.Arr("tasks_data");
        string currentTask = data.Field("current_task") is { Kind: UiValueKind.String } ct ? ct.AsStr() : null;
        string taskingStrategy = data.Str("tasking_strategy");

        // tg: index.tsx:386-389  the editing task (its id: the modal reads the live task, tg's useEffect), the selected new task type
        string editingTask = c.UseState<string>("editingTask", null);
        string selectedType = c.UseState("selectedType", "pickup");

        void Adjust(string taskId, string param, object value = null) => c.Act("adjust_task_param", ("taskId", taskId), ("param", param), ("value", value));

        string strategyIcon = TaskingStrategyIcons.TryGetValue(taskingStrategy, out string icon) ? icon : "list-ol";

        var list = new UiNode(UiKind.Stack) { Vertical = true };
        for (int index = 0; index < tasks.Count; index++)
        {
            UiValue task = tasks[index];
            string id = task.Str("id");
            string taskType = task.Str("task_type");
            bool isActive = currentTask == id;
            string taskTypeKey = TaskTypeLabels.Select(t => t.Key).FirstOrDefault(k => taskType.Contains(k)) ?? "wait";

            // tg: index.tsx:449  the task's lines
            var lines = new UiNode(UiKind.Box) { Color = "label", FontSize = "11px" };
            IReadOnlyList<UiValue> itemFilters = task.Arr("item_filters");
            if (itemFilters.Count > 0)
            {
                string any = "...any of: " + string.Join(", ", itemFilters.Take(3).Select(f => f.AsStr()))
                    + (itemFilters.Count > 3 ? $" and {itemFilters.Count - 3} more" : "") + "...";
                lines.With(new UiNode(UiKind.Box) { Text = any });
            }
            if (!string.IsNullOrEmpty(task.Str("turf")))
                lines.With(new UiNode(UiKind.Box) { Text = $"...{(taskType == "move" ? "to" : "at")} [{task.Str("turf")}]..." });
            if (task.Field("time") is { Kind: UiValueKind.Number } time && time.AsNum() != 0)
                lines.With(new UiNode(UiKind.Box) { Text = $"...for {(int)time.AsNum()} second{(time.AsNum() > 1 ? "s" : "")}..." });
            if (!string.IsNullOrEmpty(task.Str("sub_name")))
                lines.With(new UiNode(UiKind.Box) { Text = "..." + task.Str("sub_name") });

            int i = index;
            // tg: index.tsx:425  the task's row, outlined while it runs
            list.With(new UiNode(UiKind.StackItem) { Key = "task:" + id, Candystripe = true, Background = isActive ? "rgba(151, 142, 95, 0.55)" : null }.With(
                new UiNode(UiKind.Stack) { Align = "center" }.With(
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "index", Width = "1.4em", TextAlign = "center", Color = "#888", Bold = true, Text = (i + 1).ToString() }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Icon) { Icon = TaskTypeIcons.TryGetValue(taskTypeKey, out string typeIcon) ? typeIcon : "circle" }),
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Box).With(
                        new UiNode(UiKind.Box) { Key = "type", Bold = true, Text = Label(taskTypeKey) ?? taskType },
                        lines)),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "edit:" + id, Icon = "gear", Color = "transparent", OnClick = () => c.SetState("editingTask", id) }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "remove:" + id, Icon = "trash", Color = "transparent", OnClick = () => Adjust(id, "remove_task") }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Stack).With(
                        new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "down:" + id, Icon = "arrow-down", Disabled = i == tasks.Count - 1, OnClick = () => Adjust(id, "move_down") }),
                        new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "up:" + id, Icon = "arrow-up", Disabled = i == 0, OnClick = () => Adjust(id, "move_up") }))))));
        }

        var fragment = new UiNode(UiKind.Fragment).With(
            // tg: index.tsx:405
            new UiNode(UiKind.Section)
            {
                Key = "tasks",
                Title = "Tasks",
                Buttons = new UiNode(UiKind.Fragment).With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "cycle_tasking_strategy",
                        Icon = strategyIcon,
                        Color = "transparent",
                        Tooltip = "Cycle tasking strategy",
                        Text = taskingStrategy,
                        OnClick = () => c.Act("cycle_tasking_strategy", ("new_strategy", taskingStrategy == "Sequential" ? "Strict order" : "Sequential")),
                    },
                    new UiNode(UiKind.Button)
                    {
                        Key = "reset_tasking_index",
                        Icon = "arrows-spin",
                        Color = "transparent",
                        Tooltip = "Reset tasking index",
                        Text = "Reset",
                        OnClick = () => c.Act("reset_tasking_index"),
                    }),
            }.With(list),
            // tg: index.tsx:528  the new task's type and button
            new UiNode(UiKind.Section) { Key = "new" }.With(
                new UiNode(UiKind.Stack).With(
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Dropdown)
                    {
                        Key = "task_type",
                        Width = "100%",
                        Options = TaskTypeLabels.Select(t => t.Label).ToArray(),
                        TextValue = Label(selectedType),
                        OnSelected = label => c.SetState("selectedType", TaskTypeLabels.FirstOrDefault(t => t.Label == label).Key ?? selectedType),
                    }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "create_task",
                        Icon = "plus",
                        Text = "New",
                        OnClick = () => c.Act("create_task", ("task_type", selectedType)),
                    }))));

        // tg: index.tsx:559  the modal of the editing task, kept in sync with the live data
        UiValue editing = editingTask != null ? tasks.FirstOrDefault(t => t.Str("id") == editingTask) : null;
        if (editing != null)
            fragment.With(TaskEditModal(c, editing));
        return fragment;
    }

    // tgui-core's Button.Confirm with its confirmContent
    private static UiNode ConfirmWith(TguiContext c, string key, UiNode button, string confirmContent)
    {
        UiNode confirm = c.ButtonConfirm(key, button);
        if (c.UseState("confirm:" + key, false))
            confirm.Text = confirmContent;
        return confirm;
    }
}
