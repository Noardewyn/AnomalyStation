// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/ignites_matches.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J3, D298). The var block is generated.

/// Ignites matches swiped over it.
// tg: code/datums/elements/ignites_matches.dm:4
public partial class datum_element_ignites_matches : datum_element
{
    // tg: code/datums/elements/ignites_matches.dm:6
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        RegisterSignal(target, COMSIG_ATOM_ITEM_INTERACTION, nameof(on_interact));
        return dot;
    }

    // tg: code/datums/elements/ignites_matches.dm:10
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, COMSIG_ATOM_ITEM_INTERACTION);
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/ignites_matches.dm:14
    [SignalHandler]
    public virtual object on_interact(atom source = null, mob_living user = null, obj_item_match match = null, params object[] __extra)
    {
        if (match == null || Dm.truthy(match.lit) || Dm.truthy(match.burnt) || Dm.truthy(match.broken))
            return NONE;
        if (HAS_TRAIT(source, TRAIT_COMBAT_MODE_SKIP_INTERACTION) && Dm.truthy(user.combat_mode)) // SHOULD_SKIP_INTERACTION (__DEFINES/tools.dm:62), inline
            return NONE;
        object over_what_tp = source.loc == user ? Dm.text("[] []", user.p_their(), source.name) : source;
        object over_what_fp = source.loc == user ? Dm.text("your []", source.name) : source;
        if (prob(10))
        {
            user.visible_message(
                span_warning(I18n.T("[] swipes [] over [], but nothing happens.", user, match, over_what_tp)),
                span_warning(I18n.T("You swipe [] over [], but it fails to ignite.", match, over_what_fp)));
            return ITEM_INTERACT_SUCCESS;
        }
        if (prob((HAS_TRAIT(user, TRAIT_CLUMSY) || HAS_TRAIT(user, TRAIT_HULK)) ? 33 : 2))
        {
            user.visible_message(
                span_warning(I18n.T("[] swipes [] over [], accidentally snapping it.", user, match, over_what_tp)),
                span_warning(I18n.T("You swipe [] over [] too fast, snapping it in half.", match, over_what_fp)));
            match.snap();
            return ITEM_INTERACT_SUCCESS;
        }

        user.visible_message(
            span_rose(I18n.T("[] swipes [] over [], igniting it.", user, match, over_what_tp)),
            span_rose(I18n.T("You swipe [] over [], igniting it.", match, over_what_fp)));
        match.matchignite();
        return ITEM_INTERACT_SUCCESS;
    }
}
