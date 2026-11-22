// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/shoes/gunboots.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the whole file (the gunboots fire a round now and then as their wearer walks, and into a body lying under their
// kick). The var blocks are generated (D111).

// tg: code/modules/clothing/shoes/gunboots.dm:1
public partial class obj_item_clothing_shoes_gunboots : obj_item_clothing_shoes
{
    // tg: code/modules/clothing/shoes/gunboots.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_pockets_shoes)));

        RegisterSignal(this, COMSIG_SHOES_STEP_ACTION, nameof(check_step));
        return dot;
    }

    // tg: code/modules/clothing/shoes/gunboots.dm:18
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_FEET)))
            RegisterSignal(user, COMSIG_LIVING_UNARMED_ATTACK, nameof(check_kick));
        else
            UnregisterSignal(user, COMSIG_LIVING_UNARMED_ATTACK);
    }

    // tg: code/modules/clothing/shoes/gunboots.dm:25
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        if (user != null)
            UnregisterSignal(user, COMSIG_LIVING_UNARMED_ATTACK);
        return await base.dropped(user, silent, __extra);
    }

    /// After each step, check if we randomly fire a shot
    // tg: code/modules/clothing/shoes/gunboots.dm:31
    [SignalHandler]
    public virtual void check_step(mob user = null)
    {
        if (!prob(shot_prob))
            return;

        Waiting.Detach(fire_shot()); // INVOKE_ASYNC(src, PROC_REF(fire_shot))
    }

    /// Stomping on someone while wearing gunboots shoots them point blank
    // tg: code/modules/clothing/shoes/gunboots.dm:39
    [SignalHandler]
    public virtual object check_kick(mob_living_carbon_human kicking_person = null, atom attacked_atom = null, object proximity = null)
    {
        if (!Dm.truthy(proximity) || !isliving(attacked_atom))
            return NONE;
        mob_living attacked_living = (mob_living)attacked_atom;
        if (Dm.eq(attacked_living.body_position, LYING_DOWN))
            Waiting.Detach(fire_shot(attacked_living)); // INVOKE_ASYNC(src, PROC_REF(fire_shot), attacked_living)
        return null;
    }

    /// Actually fire a shot. If no target is provided, just fire off in a random direction
    // tg: code/modules/clothing/shoes/gunboots.dm:48
    public virtual async DmTask fire_shot(atom target = null)
    {
        if (!isliving(loc))
            return;

        mob_living wearer = (mob_living)loc;
        obj_projectile shot = Dm.@new(projectile_type, get_turf(wearer)) as obj_projectile;

        if (target == null)
            target = get_offset_target_turf(get_turf(wearer), rand(-3, 3), rand(-3, 3)) as atom;

        //Shooting Code:
        shot.original = target;
        shot.fired_from = this;
        shot.firer = wearer; // don't hit ourself that would be really annoying
        shot.impacted = Dm.list(Dm.kv(WEAKREF(wearer), TRUE));
        shot.def_zone = (string)pick(BODY_ZONE_L_LEG, BODY_ZONE_R_LEG); // they're fired from boots after all
        await shot.aim_projectile(target, wearer);
        if (!Dm.truthy(shot.suppressed))
            wearer.visible_message(span_danger(I18n.T("[]'s [] fires \\a []!", wearer, name, shot)), "", blind_message: span_hear(I18n.T("You hear a gunshot!")), vision_distance: COMBAT_MESSAGE_RANGE);
        await shot.fire();
    }
}
