// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/aggro_emote.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

/**
 * Attached to a mob with an AI controller, performs an emote when the AI acquires a new target.
 */
// tg: code/datums/components/aggro_emote.dm:4
public partial class datum_component_aggro_emote : datum_component
{
    // tg: code/datums/components/aggro_emote.dm:18
    [DmParams("target_key", "living_only", "emote_list", "emote_chance", "minimum_chance", "subtract_chance")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object dot = await base.Initialize(__extra);
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;

        atom atom_parent = parent as atom;
        if (atom_parent.ai_controller == null)
            return COMPONENT_INCOMPATIBLE;

        this.target_key = Dm.arg(__extra, 0) ?? BB_CURRENT_TARGET;
        this.emote_list = Dm.arg(__extra, 2) as DmList;
        this.emote_chance = Dm.arg(__extra, 3) ?? 30f;
        this.minimum_chance = Dm.arg(__extra, 4) ?? 2f;
        this.subtract_chance = Dm.arg(__extra, 5) ?? 7f;
        return dot;
    }

    // tg: code/datums/components/aggro_emote.dm:38
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_AI_BLACKBOARD_KEY_SET(target_key), nameof(on_target_changed));
    }

    // tg: code/datums/components/aggro_emote.dm:42
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_AI_BLACKBOARD_KEY_SET(target_key));
        await base.UnregisterFromParent(__extra);
    }

    /// When we get a new target, see if we want to bark at it
    // tg: code/datums/components/aggro_emote.dm:47
    [SignalHandler]
    public virtual void on_target_changed(atom source = null)
    {
        atom new_target = (source.ai_controller as datum_ai_controller).blackboard[target_key] as atom;
        if (new_target == null || !prob(Dm.num(emote_chance)))
            return;
        if (Dm.truthy(living_only) && !isliving(new_target))
            return; // If we don't want to bark at food items or chairs or windows
        emote_chance = Dm.max(Dm.num(emote_chance) - Dm.num(subtract_chance), Dm.num(minimum_chance));
        (source as mob)?.manual_emote(Dm.text("[] at [].", pick(emote_list), new_target));
    }
}
