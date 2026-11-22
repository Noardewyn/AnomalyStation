// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/gas.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10x closing: the freon effect (:1-50; a person on a turf that freezing water vapor freezes, freeze_turf). Its watcher and lasting
// kinds are var blocks only (generated). Gaps RG3 (D339): the hypernoblium protection (:64-95, the hyper-noblium reagent's on a
// plasmaman). Not translated: the rest of the file (gases beyond the core version's five, ROADMAP section 4).

// tg: code/datums/status_effects/gas.dm:1
public partial class datum_status_effect_freon : datum_status_effect
{
    // tg: code/datums/status_effects/gas.dm:14
    [DmParams("new_owner", "set_duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object set_duration = Dm.arg(__extra, 0);
        if (isnum(set_duration))
            duration = Dm.num(set_duration);
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/datums/status_effects/gas.dm:19
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return dot;
        ADD_TRAIT(owner, TRAIT_IMMOBILIZED, TRAIT_STATUS_EFFECT(id));
        RegisterSignal(owner, COMSIG_LIVING_RESIST, nameof(owner_resist));
        if (!IS_UNCONSCIOUS_OR_CRIT(owner))
            to_chat(owner, span_userdanger(I18n.T("You become frozen in a cube!")));
        cube = Dm.icon(Dm.resource("icons/effects/freeze.dmi"), "ice_cube");
        owner.add_overlay(cube);
        return dot;
    }

    // tg: code/datums/status_effects/gas.dm:30
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (Dm.truthy(can_melt) && owner.bodytemperature >= Dm.num(owner.get_body_temp_normal()))
            qdel(this);
        return null;
    }

    // tg: code/datums/status_effects/gas.dm:34
    [SignalHandler]
    public virtual void owner_resist()
    {
        Waiting.Detach(do_resist());
    }

    // tg: code/datums/status_effects/gas.dm:38
    public virtual async DmTask do_resist()
    {
        to_chat(owner, span_notice(I18n.T("You start breaking out of the ice cube...")));
        if (Dm.truthy(await do_after(owner, 4 * SECONDS, target: owner)))
        {
            to_chat(owner, span_notice(I18n.T("You break out of the ice cube!")));
            qdel(this);
        }
    }

    // tg: code/datums/status_effects/gas.dm:44
    public override async DmTask<object> on_remove()
    {
        if (!IS_UNCONSCIOUS_OR_CRIT(owner))
            to_chat(owner, span_notice(I18n.T("The cube melts!")));
        owner.cut_overlay(cube);
        owner.adjust_bodytemperature(100);
        UnregisterSignal(owner, COMSIG_LIVING_RESIST);
        REMOVE_TRAIT(owner, TRAIT_IMMOBILIZED, TRAIT_STATUS_EFFECT(id));
        return await base.on_remove();
    }
}

// tg: code/datums/status_effects/gas.dm:64  (gaps RG3: the hyper-noblium reagent)
public partial class datum_status_effect_hypernob_protection : datum_status_effect
{
    // tg: code/datums/status_effects/gas.dm:69
    [DmParams("new_owner", "duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        duration ??= 10 * SECONDS;
        this.duration = Dm.num_or_null(duration);
        return await base.on_creation(new_owner, Dm.forward(__extra, duration));
    }

    // tg: code/datums/status_effects/gas.dm:78
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (!ishuman(owner))
            throw Dm.CRASH(Dm.text("[] status effect added to non-human owner: []", type, owner != null ? owner.type : "null owner"), "code/datums/status_effects/gas.dm", 80);
        var human_owner = (mob_living_carbon_human)owner;
        await human_owner.add_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_hypernoblium)); //small slowdown as a tradeoff
        ADD_TRAIT(human_owner, TRAIT_NOFIRE, TRAIT_STATUS_EFFECT(id));
        return TRUE;
    }

    // tg: code/datums/status_effects/gas.dm:86
    public override async DmTask<object> on_remove()
    {
        if (!ishuman(owner))
            stack_trace(Dm.text("[] status effect being removed from non-human owner: []", type, owner != null ? owner.type : "null owner"), "code/datums/status_effects/gas.dm", 88);
        var human_owner = owner as mob_living_carbon_human;
        human_owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_hypernoblium));
        REMOVE_TRAIT(human_owner, TRAIT_NOFIRE, TRAIT_STATUS_EFFECT(id));
        return null;
    }
}
