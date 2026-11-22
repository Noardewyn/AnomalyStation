// Translated from tgstation (snapshot 1d99bd7f): code/datums/quirks/negative_quirks/prosopagnosia.dm, the status effect (M9 slice G:
// the knocked out mob sees no names). The quirk itself (/datum/quirk/prosopagnosia) comes with the quirks (M10).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Conceals the names of other mobs
// tg: code/datums/quirks/negative_quirks/prosopagnosia.dm:22
public partial class datum_status_effect_grouped_see_no_names : datum_status_effect_grouped
{
    // tg: code/datums/quirks/negative_quirks/prosopagnosia.dm:28
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        RegisterSignal(owner, COMSIG_MOB_REQUESTING_SCREENTIP_NAME_FROM_USER, nameof(screentip_name_override));
        RegisterSignal(owner, COMSIG_LIVING_PERCEIVE_EXAMINE_NAME, nameof(examine_name_override));
        Dm.combine(ref owner.mob_flags, MOB_HAS_SCREENTIPS_NAME_OVERRIDE);
        return dot;
    }

    // tg: code/datums/quirks/negative_quirks/prosopagnosia.dm:34
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        UnregisterSignal(owner, Dm.list(COMSIG_MOB_REQUESTING_SCREENTIP_NAME_FROM_USER, COMSIG_LIVING_PERCEIVE_EXAMINE_NAME));
        return dot;
    }

    // tg: code/datums/quirks/negative_quirks/prosopagnosia.dm:38
    [SignalHandler]
    public virtual object examine_name_override(datum source = null, mob_living examined = null, object visible_name = null, DmList name_override = null)
    {
        if (!ishuman(examined) || source == examined)
            return NONE;

        object id_name = Dm.truthy(see_ids) ? ((mob_living_carbon_human)examined).get_id_name("", honorifics: TRUE) : see_ids;
        name_override[1] = Dm.truthy(id_name) ? Dm.text("[]?", id_name) : I18n.T("Unknown");
        return COMPONENT_EXAMINE_NAME_OVERRIDEN;
    }

    // tg: code/datums/quirks/negative_quirks/prosopagnosia.dm:48
    [SignalHandler]
    public virtual object screentip_name_override(datum source = null, DmList returned_name = null, obj_item held_item = null, atom hovered = null)
    {
        if (!ishuman(hovered) || source == hovered)
            return NONE;

        object id_name = Dm.truthy(see_ids) ? ((mob_living_carbon_human)hovered).get_id_name("", honorifics: TRUE) : see_ids;
        returned_name[1] = Dm.truthy(id_name) ? Dm.text("[]?", id_name) : I18n.T("Unknown");
        return SCREENTIP_NAME_SET;
    }
}
