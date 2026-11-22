// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/status_procs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

//Here are the procs used to modify status effects of a mob.

// tg: code/modules/mob/status_procs.dm:4
public partial class mob : atom_movable
{
    ///Adjust the disgust level of a mob
    // tg: code/modules/mob/status_procs.dm:4
    public virtual void adjust_disgust(object amount = null, params object[] __extra)
    {
        return;
    }

    ///Set the disgust level of a mob
    // tg: code/modules/mob/status_procs.dm:8
    public virtual void set_disgust(object amount = null)
    {
        return;
    }

    ///Adjust the body temperature of a mob, with min/max settings
    // tg: code/modules/mob/status_procs.dm:12
    public virtual object adjust_bodytemperature(object amount = null, float? min_temp = null, float? max_temp = null, params object[] __extra)
    {
        min_temp ??= 0;
        max_temp ??= INFINITY;
        if (bodytemperature >= Dm.num(min_temp) && bodytemperature <= Dm.num(max_temp))
        {
            bodytemperature = clamp(bodytemperature + Dm.num(amount), Dm.num(min_temp), Dm.num(max_temp));
            return TRUE;
        }
        return null;
    }

    /// Sight here is the mob.sight var, which tells byond what to actually show to our client
    /// See [code\__DEFINES\sight.dm] for more details
    // tg: code/modules/mob/status_procs.dm:19
    [ShouldCallParent]
    public virtual void set_sight(object new_value = null)
    {
        if (Dm.eq(sight, new_value))
            return;
        float old_sight = sight;
        sight = Dm.num(new_value);

        if (Dm.truthy(_listen_lookup?[COMSIG_MOB_SIGHT_CHANGE]))
            _SendSignal(COMSIG_MOB_SIGHT_CHANGE, Dm.list(this, new_value, old_sight));
    }

    // tg: code/modules/mob/status_procs.dm:28
    public virtual void add_sight(object new_value = null)
    {
        set_sight(Dm.bitor(sight, new_value));
    }

    // tg: code/modules/mob/status_procs.dm:31
    public virtual void clear_sight(object new_value = null)
    {
        set_sight(Dm.bitand(sight, Dm.bitnot(new_value)));
    }

    /// see invisibility is the mob's capability to see things that ought to be hidden from it
    /// Can think of it as a primitive version of changing the alpha of planes
    /// We mostly use it to hide ghosts, no real reason why
    // tg: code/modules/mob/status_procs.dm:37
    [ShouldCallParent]
    public virtual void set_invis_see(object new_sight = null)
    {
        if (Dm.eq(new_sight, see_invisible))
            return;
        float old_invis = see_invisible;
        if (!Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_MOB_SEE_INVIS_CHANGE]) ? NONE : _SendSignal(COMSIG_MOB_SEE_INVIS_CHANGE, Dm.list(this, new_sight, old_invis))), COMPONENT_BLOCK_INVIS_CHANGE)))
            see_invisible = Dm.num(new_sight);
    }
}
