// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/lavaland/geyser.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: gaps E1a (D327): the plunger and the reinforced plunger (plunging a clogged vent, the plumbing machines' plunger_act, the
// layer mode for the ducts, the throw that sticks it to a face); the geysers come with Lavaland. The var blocks are generated (D111).

///A wearable tool that lets you empty plumbing machinery and some other stuff
// tg: code/game/objects/structures/lavaland/geyser.dm:129
public partial class obj_item_plunger : obj_item
{
    // tg: code/game/objects/structures/lavaland/geyser.dm:152
    public override async DmTask<object> attack_atom(atom attacked_atom = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        var attacked_obj = attacked_atom as obj;
        if (Dm.truthy(layer_mode))
        {
            attacked_obj?.SEND_SIGNAL(COMSIG_MOVABLE_CHANGE_DUCT_LAYER, Dm.list(attacked_obj, attacked_obj, target_layer));
            return await base.attack_atom(attacked_atom, user, modifiers, attack_modifiers);
        }
        else
        {
            if (attacked_obj == null || !Dm.truthy(await attacked_obj.plunger_act(this, user, reinforced)))
                return await base.attack_atom(attacked_atom, user, modifiers, attack_modifiers);
        }
        return null;
    }

    // tg: code/game/objects/structures/lavaland/geyser.dm:160
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        if (!Dm.eq(throwingdatum?.target_zone, BODY_ZONE_HEAD))
            return dot;
        if (iscarbon(hit_atom))
        {
            var victim = (mob_living_carbon)hit_atom;
            if (Dm.truthy(Waiting.NoSleep(victim.equip_to_slot_if_possible(this, ITEM_SLOT_MASK, disable_warning: TRUE))))
                victim.visible_message(span_warning(I18n.T("The plunger slams into []'s face!", victim)), span_warning(I18n.T("The plunger suctions to your face!")));
        }
        return dot;
    }

    // tg: code/game/objects/structures/lavaland/geyser.dm:169
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);

        layer_mode = Dm.truthy(layer_mode) ? FALSE : TRUE;

        if (!Dm.truthy(layer_mode))
        {
            icon_state = Dm.initial(this, (obj_item_plunger p) => p.icon_state);
            to_chat(user, span_notice(I18n.T("You set the plunger to 'Plunger Mode'.")));
        }
        else
        {
            icon_state = layer_mode_sprite;
            to_chat(user, span_notice(I18n.T("You set the plunger to 'Layer Mode'.")));
        }

        playsound(this, "sound/machines/click.ogg", 10, TRUE);
        return dot;
    }

    // tg: code/game/objects/structures/lavaland/geyser.dm:183
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        object new_layer = await tgui_input_list(user, I18n.T("Select a layer"), I18n.T("Layer"), GLOB.plumbing_layers);
        if (new_layer == null || !Dm.truthy(await user.can_perform_action(this)))
            return CLICK_ACTION_BLOCKING;
        target_layer = Dm.num(GLOB.plumbing_layers[new_layer]);
        return CLICK_ACTION_SUCCESS;
    }
}
