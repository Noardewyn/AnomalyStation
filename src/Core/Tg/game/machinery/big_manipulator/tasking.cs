// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/big_manipulator/tasking.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E16: the big manipulator's tasking strategies, whole: sequential (the next task that can run) and strict order (wait
// for the current task), each picking pickup candidates in turn. The var blocks are generated.

// tg: code/game/machinery/big_manipulator/tasking.dm:1
public partial class datum_tasking_strategy : datum
{
    /// Returns the next task to run, or null if nothing is available.
    // tg: code/game/machinery/big_manipulator/tasking.dm:5
    public virtual object get_next_task(DmList tasks = null, params object[] __extra)
    {
        return null;
    }

    /// Picks a next candidate from a list of eligible atoms.
    // tg: code/game/machinery/big_manipulator/tasking.dm:9
    public virtual object get_next_candidate(DmList candidates = null)
    {
        if (!Dm.truthy(Dm.length(candidates)))
            return null;
        return candidates[1];
    }
}

// Moves through the list, skipping tasks that can't run.
// tg: code/game/machinery/big_manipulator/tasking.dm:15
public partial class datum_tasking_strategy_sequential : datum_tasking_strategy
{
    // tg: code/game/machinery/big_manipulator/tasking.dm:20
    [DmParams("tasks", "manipulator")]
    public override object get_next_task(DmList tasks = null, params object[] __extra)
    {
        obj_machinery_big_manipulator manipulator = Dm.arg<obj_machinery_big_manipulator>(__extra, 0);
        if (!Dm.truthy(Dm.length(tasks)))
            return null;
        if (current_index < 1 || current_index > Dm.length(tasks))
            current_index = 1;
        float start = current_index;
        while (true)
        {
            datum_manipulator_task task = (datum_manipulator_task)tasks[current_index];
            current_index++;
            if (current_index > Dm.length(tasks))
                current_index = 1;
            if (Dm.truthy(task.can_run(manipulator)))
                return task;
            if (current_index == start)
                return null;
        }
    }

    // tg: code/game/machinery/big_manipulator/tasking.dm:35
    public override object get_next_candidate(DmList candidates = null)
    {
        if (!Dm.truthy(Dm.length(candidates)))
            return null;
        if (candidate_index < 1 || candidate_index > Dm.length(candidates))
            candidate_index = 1;
        object candidate = candidates[candidate_index];
        candidate_index++;
        if (candidate_index > Dm.length(candidates))
            candidate_index = 1;
        return candidate;
    }
}

// Stays on the current task until it can run.
// tg: code/game/machinery/big_manipulator/tasking.dm:47
public partial class datum_tasking_strategy_strict : datum_tasking_strategy
{
    // tg: code/game/machinery/big_manipulator/tasking.dm:52
    [DmParams("tasks", "manipulator")]
    public override object get_next_task(DmList tasks = null, params object[] __extra)
    {
        obj_machinery_big_manipulator manipulator = Dm.arg<obj_machinery_big_manipulator>(__extra, 0);
        if (!Dm.truthy(Dm.length(tasks)))
            return null;
        if (current_index < 1 || current_index > Dm.length(tasks))
            current_index = 1;
        datum_manipulator_task task = (datum_manipulator_task)tasks[current_index];
        if (!Dm.truthy(task.can_run(manipulator)))
            return null;
        current_index++;
        if (current_index > Dm.length(tasks))
            current_index = 1;
        return task;
    }

    // tg: code/game/machinery/big_manipulator/tasking.dm:63
    public override object get_next_candidate(DmList candidates = null)
    {
        if (!Dm.truthy(Dm.length(candidates)))
            return null;
        if (candidate_index < 1 || candidate_index > Dm.length(candidates))
            candidate_index = 1;
        object candidate = candidates[candidate_index];
        candidate_index++;
        if (candidate_index > Dm.length(candidates))
            candidate_index = 1;
        return candidate;
    }
}
