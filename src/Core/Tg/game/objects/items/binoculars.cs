// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/binoculars.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J4, D298).

// tg: code/game/objects/items/binoculars.dm:2
public partial class obj_item_binoculars : obj_item
{
    // tg: code/game/objects/items/binoculars.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed), Dm.named("force_unwielded", 8), Dm.named("force_wielded", 12), Dm.named("wield_callback", Dm.@new<datum_callback>(this, nameof(on_wield))), Dm.named("unwield_callback", Dm.@new<datum_callback>(this, nameof(on_unwield)))));
        Waiting.NoSleep(AddComponent(typeof(datum_component_scope), Dm.named("range_modifier", 4), Dm.named("zoom_method", ZOOM_METHOD_WIELD)));
        return dot;
    }

    // tg: code/game/objects/items/binoculars.dm:18
    public virtual async DmTask on_wield(obj_item source = null, mob user = null)
    {
        user.visible_message(span_notice(I18n.T("[] holds [] up to [] eyes.", user, this, user.p_their())), span_notice(I18n.T("You hold [] up to your eyes.", this)));
        inhand_icon_state = "binoculars_wielded";
        await user.regenerate_icons();
        //Have you ever tried running with binocs on? It takes some willpower not to stop as things appear way too close than they're.
        await user.add_movespeed_modifier(typeof(datum_movespeed_modifier_binocs_wielded));
    }

    // tg: code/game/objects/items/binoculars.dm:25
    public virtual async DmTask on_unwield(obj_item source = null, mob user = null)
    {
        user.visible_message(span_notice(I18n.T("[] lowers [].", user, this)), span_notice(I18n.T("You lower [].", this)));
        inhand_icon_state = "binoculars";
        await user.regenerate_icons();
        user.remove_movespeed_modifier(typeof(datum_movespeed_modifier_binocs_wielded));
    }
}
