// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/weaponry/melee/supermatter_sword.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps WP1 (D333): the file whole. The supermatter sword (an admin's): a supermatter crystal in its blade (its countdown removed)
// dusts whatever it hits, whatever it is thrown at, the bullets that hit it, blasts and acid; balanced on its hilt until first
// picked up, it then eats its way down through whatever holds it or the floor it lies on (not space). The var blocks are generated
// (D111).

// tg: code/game/objects/items/weaponry/melee/supermatter_sword.dm:1
public partial class obj_item_melee_supermatter_sword : obj_item_melee
{
    // tg: code/game/objects/items/weaponry/melee/supermatter_sword.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        shard = Dm.@new<obj_machinery_power_supermatter_crystal>(this);
        qdel(shard.countdown);
        shard.countdown = null;
        START_PROCESSING(SSobj, this);
        visible_message(span_warning(I18n.T("[] appears, balanced ever so perfectly on its hilt. This isn't ominous at all.", this)));
        RegisterSignal(this, COMSIG_ATOM_PRE_BULLET_ACT, nameof(eat_bullets));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/supermatter_sword.dm:29
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(balanced) || throwing != null || ismob(loc) || loc == null)
            return null;
        if (!isturf(loc))
        {
            atom target = loc as atom;
            Waiting.NoSleep(forceMove(target.loc));
            Waiting.NoSleep(consume_everything(target));
        }
        else
        {
            turf turf = get_turf(this);
            if (!isspaceturf(turf))
                Waiting.NoSleep(consume_turf(turf));
        }
        return null;
    }

    // tg: code/game/objects/items/weaponry/melee/supermatter_sword.dm:41
    public override async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.pre_attack(target, user, modifiers, attack_modifiers);
        if (Dm.truthy(dot))
            return dot;

        if (target == user)
            await user.dropItemToGround(this, TRUE);
        else
            await user.do_attack_animation(target);
        await consume_everything(target);
        return TRUE;
    }

    // tg: code/game/objects/items/weaponry/melee/supermatter_sword.dm:53
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        base.throw_impact(hit_atom, throwingdatum, __extra);
        if (hit_atom is mob mob)
        {
            if (loc == mob)
                Waiting.NoSleep(mob.dropItemToGround(this, TRUE));
        }
        Waiting.NoSleep(consume_everything(hit_atom));
        return null;
    }

    // tg: code/game/objects/items/weaponry/melee/supermatter_sword.dm:61
    public override async DmTask<object> pickup(mob user = null)
    {
        await base.pickup(user);
        balanced = 0;
        icon_state = "supermatter_sword";
        icon_angle = -45;
        return null;
    }

    // tg: code/game/objects/items/weaponry/melee/supermatter_sword.dm:67
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        visible_message(
            span_danger(I18n.T("The blast wave smacks into [] and rapidly flashes to ash.", this)),
            span_hear(I18n.T("You hear a loud crack as you are washed with a wave of heat.")));
        Waiting.NoSleep(consume_everything());
        return TRUE;
    }

    // tg: code/game/objects/items/weaponry/melee/supermatter_sword.dm:75
    public override async DmTask<object> acid_act(object acidpwr = null, object acid_volume = null, params object[] __extra)
    {
        visible_message(span_danger(I18n.T("The acid smacks into [] and rapidly flashes to ash.", this)),
            span_hear(I18n.T("You hear a loud crack as you are washed with a wave of heat.")));
        await consume_everything();
        return TRUE;
    }

    // tg: code/game/objects/items/weaponry/melee/supermatter_sword.dm:81
    [SignalHandler]
    public virtual object eat_bullets(datum source = null, obj_projectile hitting_projectile = null)
    {
        visible_message(
            span_danger(I18n.T("[] smacks into [] and rapidly flashes to ash.", hitting_projectile, source)),
            null,
            span_hear(I18n.T("You hear a loud crack as you are washed with a wave of heat.")));
        Waiting.NoSleep(consume_everything(hitting_projectile));
        return COMPONENT_BULLET_BLOCKED;
    }

    // tg: code/game/objects/items/weaponry/melee/supermatter_sword.dm:92
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] touches []'s blade. It looks like [] tired of waiting for the radiation to kill []!", user, this, user.p_theyre(), user.p_them())));
        await user.dropItemToGround(this, TRUE);
        shard.Bumped(user);
        return null;
    }

    // tg: code/game/objects/items/weaponry/melee/supermatter_sword.dm:97
    public virtual async DmTask consume_everything(atom target = null)
    {
        if (target == null)
            await shard.Bump(target);
        else if (!isturf(target))
            shard.Bumped(target as atom_movable);
        else
            await consume_turf((turf)target);
    }

    // tg: code/game/objects/items/weaponry/melee/supermatter_sword.dm:105
    public virtual async DmTask consume_turf(turf turf = null)
    {
        Type oldtype = turf.type;
        turf newT = await turf.ScrapeAway(flags: CHANGETURF_INHERIT_AIR) as turf;
        if (newT.type == oldtype)
            return;
        playsound(turf, Dm.resource("sound/effects/supermatter.ogg"), 50, TRUE);
        turf.visible_message(
            span_danger(I18n.T("[] smacks into [] and rapidly flashes to ash.", turf, this)),
            span_hear(I18n.T("You hear a loud crack as you are washed with a wave of heat.")));
        await shard.Bump(turf);
    }
}
