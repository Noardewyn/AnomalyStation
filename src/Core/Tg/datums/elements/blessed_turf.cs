// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/blessed_turf.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RG3 (D339): the whole file (holy water blesses the floor it is splashed on: the cult's teleports stop there, and those who
// see blessed tiles see its faint glow). The var block is generated (D111).

/// Attached to blessed turfs, prevents jaunting, revenant movement and cult teleportation
// tg: code/datums/elements/blessed_turf.dm:3
public partial class datum_element_blessed_turf : datum_element
{
    // tg: code/datums/elements/blessed_turf.dm:1  #define BLESSING_APPEARANCE_KEY "blessing"
    private const string BLESSING_APPEARANCE_KEY = "blessing";

    // tg: code/datums/elements/blessed_turf.dm:5
    [DmParams("target", "invisible")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object invisible = Dm.arg(__extra, 0) ?? FALSE;
        object dot = await base.Attach(target, __extra);
        if (!isturf(target))
            return ELEMENT_INCOMPATIBLE;

        var target_turf = (turf)target;
        ADD_TRAIT(target_turf, TRAIT_TURF_BLESSED, ELEMENT_TRAIT(type));
        RegisterSignal(target_turf, COMSIG_ATOM_INTERCEPT_TELEPORTING, nameof(block_cult_teleport));

        if (Dm.truthy(invisible))
            return dot;

        image blessing_icon = image(icon: Dm.resource("icons/effects/effects.dmi"), icon_state: "blessed", layer: ABOVE_NORMAL_TURF_LAYER, loc: target_turf);
        SET_PLANE_EXPLICIT(blessing_icon, GAME_PLANE, target_turf);
        blessing_icon.alpha = 64;
        blessing_icon.appearance_flags = Dm.bitor(Dm.bitor(RESET_ALPHA, RESET_COLOR), KEEP_APART);
        target_turf.add_alt_appearance(typeof(datum_atom_hud_alternate_appearance_basic_blessed_aware), BLESSING_APPEARANCE_KEY, blessing_icon);
        return dot;
    }

    // tg: code/datums/elements/blessed_turf.dm:23
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        var target_turf = (turf)source;
        REMOVE_TRAIT(target_turf, TRAIT_TURF_BLESSED, ELEMENT_TRAIT(type));
        UnregisterSignal(target_turf, COMSIG_ATOM_INTERCEPT_TELEPORTING);
        target_turf.remove_alt_appearance(BLESSING_APPEARANCE_KEY);
        return dot;
    }

    // tg: code/datums/elements/blessed_turf.dm:30
    [SignalHandler]
    public virtual object block_cult_teleport(datum source = null, object channel = null, turf origin = null)
    {
        if (Dm.eq(channel, TELEPORT_CHANNEL_CULT))
            return COMPONENT_INTERCEPT_TELEPORT;
        return null;
    }
}
