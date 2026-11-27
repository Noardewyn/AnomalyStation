// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/hooked.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: a mob snagged by a fishing rod's rescue or jawed hook, held by the fishing line until the line is cut or the
// hook is taken out (the alert's click); the jaws also slow it down. The var blocks are generated (D111).

///Status effect applied when casting a fishing rod at someone, provided the attached fishing hook allows it.
// tg: code/datums/status_effects/debuffs/hooked.dm:2
public partial class datum_status_effect_grouped_hooked : datum_status_effect_grouped
{
    // tg: code/datums/status_effects/debuffs/hooked.dm:9
    public virtual async DmTask<object> try_unhook()
    {
        return await do_after(owner, 2 * SECONDS, timed_action_flags: IGNORE_USER_LOC_CHANGE, extra_checks: Dm.@new<datum_callback>(this, nameof(still_exists)), interaction_key: DOAFTER_SOURCE_REMOVING_HOOK);
    }

    // tg: code/datums/status_effects/debuffs/hooked.dm:12
    public virtual object still_exists()
    {
        return !QDELETED(this);
    }

    // tg: code/datums/status_effects/debuffs/hooked.dm:15
    public override void source_added(object source = null, params object[] __extra)
    {
        RegisterSignal((datum)source, COMSIG_QDELETING, nameof(on_fishing_line_deleted));
    }

    // tg: code/datums/status_effects/debuffs/hooked.dm:18
    [SignalHandler]
    public virtual void on_fishing_line_deleted(datum source = null)
    {
        owner.remove_status_effect(type, source);
    }
}

// tg: code/datums/status_effects/debuffs/hooked.dm:22
public partial class atom_movable_screen_alert_status_effect_hooked : atom_movable_screen_alert_status_effect
{
    // tg: code/datums/status_effects/debuffs/hooked.dm:29
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (!Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(owner.can_resist()))
            return dot;
        owner.balloon_alert(owner, I18n.T("removing hook..."));
        datum_status_effect_grouped_hooked effect = owner.has_status_effect(attached_effect.type) as datum_status_effect_grouped_hooked;
        if (!Dm.truthy(await effect.try_unhook()))
            return dot;
        owner.balloon_alert(owner, I18n.T("hook removed"));
        datum_beam_held rand_source = pick(effect.sources) as datum_beam_held;
        qdel(rand_source);
        return dot;
    }
}

///Version used by the jawed fishing hook, which also applies slowdown
// tg: code/datums/status_effects/debuffs/hooked.dm:42
public partial class datum_status_effect_grouped_hooked_jaws : datum_status_effect_grouped_hooked
{
    // tg: code/datums/status_effects/debuffs/hooked.dm:46
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        await owner.add_movespeed_modifier(typeof(datum_movespeed_modifier_hook_jawed));
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/hooked.dm:50
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_hook_jawed));
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/hooked.dm:54
    public override async DmTask<object> try_unhook()
    {
        return await do_after(owner, 10 * SECONDS, extra_checks: Dm.@new<datum_callback>(this, nameof(still_exists)), interaction_key: DOAFTER_SOURCE_REMOVING_HOOK);
    }
}
