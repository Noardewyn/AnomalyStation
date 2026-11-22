// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/stationstuck.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI7 (D326): the whole file (the derelict drones). The var block is generated (D111).

/*
This component is similar to stationloving in that it is meant to keep something on the z-level
The difference is that stationloving is for objects and stationstuck is for mobs.
It has a punishment variable that is what happens to the parent when they leave the z-level. See punish() documentation
*/
// tg: code/datums/components/stationstuck.dm:6
public partial class datum_component_stationstuck : datum_component
{
    // tg: code/datums/components/stationstuck.dm:12
    [DmParams("_punishment", "_message")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object _punishment = Dm.arg(__extra, 0) ?? PUNISHMENT_GIB;
        object _message = Dm.arg(__extra, 1) ?? "";
        await DmTask.CompletedTask;
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;
        var L = (mob_living)parent;
        RegisterSignals(L, Dm.list(COMSIG_MOVABLE_Z_CHANGED), nameof(punish));
        punishment = Dm.str(_punishment);
        message = Dm.str(_message);
        stuck_zlevel = L.z;
        return null;
    }

    // tg: code/datums/components/stationstuck.dm:21
    public override async DmTask InheritComponent(datum_component newc = null, object original = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        if (newc is datum_component_stationstuck stuck)
        {
            punishment = stuck.punishment;
            message = stuck.message;
        }
        else
        {
            punishment = Dm.str(Dm.arg(__extra, 0));
            message = Dm.str(Dm.arg(__extra, 1));
        }
    }

    /**
     * Called when parent leaves the zlevel this is set to (aka whichever zlevel it was on when it was added)
     * Sends a message, then does an effect depending on what the punishment was.
     */
    // tg: code/datums/components/stationstuck.dm:39
    public virtual void punish()
    {
        var escapee = (mob_living)parent;
        if (Dm.truthy(message))
        {
            string span = Dm.eq(punishment, PUNISHMENT_TELEPORT) ? "danger" : "userdanger";
            to_chat(escapee, Dm.text("<span class='[]'>[]</span>", span, message));
        }
        switch (punishment)
        {
            case PUNISHMENT_MURDER:
                if (escapee.stat != DEAD)
                    escapee.investigate_log("has been killed by stationstuck component.", INVESTIGATE_DEATHS);
                Waiting.Detach(escapee.death());
                break;
            case PUNISHMENT_GIB:
                escapee.investigate_log("has been gibbed by stationstuck component.", INVESTIGATE_DEATHS);
                Waiting.Detach(escapee.gib(DROP_ALL_REMAINS));
                break;
            case PUNISHMENT_TELEPORT:
            {
                object targetturf = find_safe_turf(stuck_zlevel);
                if (!Dm.truthy(targetturf))
                    targetturf = locate(world.maxx / 2, world.maxy / 2, Dm.num(stuck_zlevel));
                Waiting.NoSleep(escapee.forceMove(targetturf as atom));
                break;
            }
        }
    }
}
