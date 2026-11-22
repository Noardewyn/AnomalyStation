// Translated from tgstation (snapshot 1d99bd7f): code/game/atom/atom_invisibility.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M3 P4: the undertile element hides the infrastructure under floor tiles through it, D115). The vars
// invisibility_sources and current_invisibility_priority are in the generated part (Generated/Types/atom.g.cs).

// tg: code/game/atom/atom_invisibility.dm:5
public partial class atom : datum
{
    // tg: code/game/atom/atom_invisibility.dm:2  #define INVISIBILITY_VALUE 1 (file-local, #undef at :71)
    private const int INVISIBILITY_VALUE = 1;
    // tg: code/game/atom/atom_invisibility.dm:3  #define INVISIBILITY_PRIORITY 2 (file-local, #undef at :72)
    private const int INVISIBILITY_PRIORITY = 2;

    // tg: code/game/atom/atom_invisibility.dm:9
    [DmPrivate]
    public virtual void RecalculateInvisibility()
    {
        if (!Dm.truthy(invisibility_sources))
        {
            current_invisibility_priority = -INFINITY;
            invisibility = Dm.num(Dm.initial(this, (atom p) => p.invisibility));
            return;
        }

        object highest_priority = null;
        DmList highest_priority_invisibility_data = null;
        foreach (object entry in Dm.each(invisibility_sources))
        {
            DmList priority_data = null;
            if (islist(entry))
                priority_data = entry as DmList;
            else
                priority_data = invisibility_sources[entry] as DmList;

            object priority = priority_data[INVISIBILITY_PRIORITY];
            if (Dm.gt(highest_priority, priority)) // In the case of equal priorities, we use the last thing in the list so that more recent changes apply first
                continue;

            highest_priority = priority;
            highest_priority_invisibility_data = priority_data;
        }

        current_invisibility_priority = Dm.num(highest_priority);
        invisibility = Dm.num(highest_priority_invisibility_data[INVISIBILITY_VALUE]);
    }

    /**
     * Sets invisibility according to priority.
     * If you want to be able to undo the value you set back to what it would be otherwise,
     * you should provide an id here and remove it using RemoveInvisibility(id)
     */
    // tg: code/game/atom/atom_invisibility.dm:41
    public virtual void SetInvisibility(object desired_value = null, object id = null, float? priority = null)
    {
        priority ??= 0;
        if (!Dm.truthy(invisibility_sources))
            invisibility_sources = Dm.list();

        if (Dm.truthy(id))
            invisibility_sources[id] = Dm.list(desired_value, priority);
        else
            Dm.append(ref invisibility_sources, Dm.list(Dm.list(desired_value, priority)));

        if (current_invisibility_priority > Dm.num(priority))
            return;

        RecalculateInvisibility();
    }

    /// Removes the specified invisibility source from the tracker
    // tg: code/game/atom/atom_invisibility.dm:56
    public virtual void RemoveInvisibility(object id = null)
    {
        if (!Dm.truthy(invisibility_sources?[id]))
            return;

        DmList priority_data = invisibility_sources[id] as DmList;
        Dm.remove(ref invisibility_sources, id);

        if (length(invisibility_sources) == 0)
            invisibility_sources = null;

        if (Dm.gt(current_invisibility_priority, priority_data[INVISIBILITY_PRIORITY]))
            return;

        RecalculateInvisibility();
    }
}
