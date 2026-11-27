// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/damage_threshold.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: a living mob's force threshold (the banana glutton's fat; the blobbernaut and the juggernaut wait for their
// antagonists). The var block is generated (D111).

/// Applied to living mobs.
/// Adds a force threshold for which attacks will be blocked entirely.
/// IE, if they are hit with an attack that deals less than X damage, the attack does nothing.
// tg: code/datums/elements/damage_threshold.dm:4
public partial class datum_element_damage_threshold : datum_element
{
    // tg: code/datums/elements/damage_threshold.dm:10
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object threshold = Dm.arg(__extra, 0);
        object dot = await base.Attach(target, __extra);
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;
        if (!isnum(threshold) || Dm.num(threshold) <= 0)
            return ELEMENT_INCOMPATIBLE;

        RegisterSignal(target, COMSIG_LIVING_CHECK_BLOCK, nameof(check_block));
        force_threshold = Dm.num(threshold);
        return dot;
    }

    // tg: code/datums/elements/damage_threshold.dm:20
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_LIVING_CHECK_BLOCK);
        return dot;
    }

    // tg: code/datums/elements/damage_threshold.dm:24
    [SignalHandler]
    public virtual object check_block(mob_living source = null, atom hitby = null, object damage = null, object attack_text = null, object attack_type = null, object armour_penetration = null, object damage_type = null, object attack_flag = null)
    {
        if (Dm.num(damage) <= 0) // Already handled
            return NONE;

        if (Dm.num(damage) <= force_threshold)
        {
            obj_item item_hitting = hitby as obj_item;
            object tap_vol = item_hitting is obj_item ? item_hitting.get_clamped_volume() : 50;
            source.visible_message(
                span_warning(I18n.T("[] looks unharmed!", source)),
                span_warning(I18n.T("[] deals no damage to you!", attack_text)),
                span_hear(I18n.T("You hear a thud.")),
                COMBAT_MESSAGE_RANGE);
            playsound(source, "sound/items/weapons/tap.ogg", tap_vol, TRUE, -1);
            return SUCCESSFUL_BLOCK;
        }

        return NONE;
    }
}
