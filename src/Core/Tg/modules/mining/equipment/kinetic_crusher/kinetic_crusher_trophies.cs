// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/kinetic_crusher/kinetic_crusher_trophies.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/*!
 * Contains the baseline of kinetic crusher trophies.
 */

// tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher_trophies.dm:5
public partial class obj_item_crusher_trophy : obj_item
{
    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher_trophies.dm:20
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("Causes [] when attached to a kinetic crusher.", effect_desc())));
        return dot;
    }

    /// Returns a string to get added to the examine
    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher_trophies.dm:25
    public virtual string effect_desc()
    {
        return I18n.T("errors");
    }

    /// Tries to add the trophy to our crusher
    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher_trophies.dm:30
    public virtual async DmTask<object> add_to(obj_item_kinetic_crusher crusher = null, mob_living user = null)
    {
        foreach (obj_item_crusher_trophy trophy in Dm.each<obj_item_crusher_trophy>(crusher.trophies))
        {
            if (istype(trophy, denied_type) || istype(this, trophy.denied_type))
            {
                to_chat(user, span_warning(I18n.T("You can't seem to attach [] to []. Maybe remove a few trophies?", this, crusher)));
                return FALSE;
            }
        }
        if (!Dm.truthy(await user.transferItemToLoc(this, crusher)))
            return FALSE;
        Dm.append(ref crusher.trophies, this);
        to_chat(user, span_notice(I18n.T("You attach [] to [].", this, crusher)));
        return TRUE;
    }

    /// Removes the trophy from our crusher
    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher_trophies.dm:42
    public virtual async DmTask<object> remove_from(obj_item_kinetic_crusher crusher = null, mob_living user = null)
    {
        await forceMove(get_turf(crusher));
        return TRUE;
    }

    /// Does an effect when you hit a mob with a crusher
    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher_trophies.dm:47
    public virtual async DmTask on_melee_hit(mob_living target = null, mob_living user = null) //the target and the user
    {
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher_trophies.dm:50
    public virtual void on_projectile_fire(obj_projectile_destabilizer marker = null, mob_living user = null) //the projectile fired and the user
    {
    }

    /// Does an effect when you hit a mob with the projectile
    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher_trophies.dm:54
    public virtual async DmTask on_projectile_hit_mob(mob_living target = null, mob_living user = null) //the target and the user
    {
    }

    /// Does an effect when you hit a mineral turf with the projectile
    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher_trophies.dm:58
    public virtual async DmTask on_projectile_hit_mineral(turf_closed_mineral mineral = null, mob_living user = null) //the target and the user
    {
    }

    /// Does an effect when a mark is applied
    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher_trophies.dm:62
    public virtual void on_mark_applied(mob_living target = null, mob_living user = null, datum_status_effect crusher_mark = null)
    {
    }

    /// Does an effect when you hit a mob that is marked via the projectile
    /// Returns additional damage for detonation
    // tg: code/modules/mining/equipment/kinetic_crusher/kinetic_crusher_trophies.dm:67
    [ShouldCallParent]
    public virtual async DmTask<object> on_mark_detonation(mob_living target = null, mob_living user = null, obj_item_kinetic_crusher pkc = null) //the target and the user
    {
        //if we dont have a set id, use the typepath as identifier
        // COMSIG_MOB_TROPHY_ACTIVATED(identifier) (__DEFINES/dcs/signals/signals_mob/signals_mob_main.dm:294), inline
        string signal = Dm.text("COMSIG_MOB_TROPHY_ACTIVATED[]", Dm.truthy(trophy_id) ? trophy_id : type);
        if (Dm.truthy(target._listen_lookup?[signal]))
            target._SendSignal(signal, Dm.list(target, this, user));
        return null;
    }
}
