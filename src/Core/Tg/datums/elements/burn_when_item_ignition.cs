// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/burn_when_item_ignition.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * This item will be set on fire if a mob clicks on it with something that can ignite objects,
 * OR if a mob clicks it on something else that can ignite objects.
 */
// tg: code/datums/elements/burn_when_item_ignition.dm:5
public partial class datum_element_burn_on_item_ignition : datum_element
{
    // tg: code/datums/elements/burn_when_item_ignition.dm:11
    [DmParams("target", "bypass_clumsy")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object bypass_clumsy = Dm.arg(__extra, 0) ?? FALSE;
        object dot = await base.Attach(target, __extra);
        this.bypass_clumsy = Dm.num(bypass_clumsy);
        RegisterSignal(target, COMSIG_ATOM_ITEM_INTERACTION, nameof(try_burn));
        RegisterSignal(target, COMSIG_ITEM_INTERACTING_WITH_ATOM, nameof(try_hold_burn));
        return dot;
    }

    // tg: code/datums/elements/burn_when_item_ignition.dm:17
    public override object Detach(datum target = null, params object[] __extra)
    {
        object dot = base.Detach(target, __extra);
        UnregisterSignal(target, COMSIG_ATOM_ITEM_INTERACTION);
        UnregisterSignal(target, COMSIG_ITEM_INTERACTING_WITH_ATOM);
        return dot;
    }

    // tg: code/datums/elements/burn_when_item_ignition.dm:22
    [SignalHandler]
    public virtual object try_burn(atom source = null, mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        //can't be put on fire!
        if (Dm.truthy(Dm.bitand(source.resistance_flags, FIRE_PROOF)) || !Dm.truthy(Dm.bitand(source.resistance_flags, FLAMMABLE)))
            return NONE;
        //already on fire!
        if (Dm.truthy(Dm.bitand(source.resistance_flags, ON_FIRE)))
            return NONE;

        object ignition_message = Waiting.NoSleep(tool.ignition_effect(source, user));
        if (!Dm.truthy(ignition_message))
            return NONE;
        if (!Dm.truthy(bypass_clumsy) && HAS_TRAIT(user, TRAIT_CLUMSY) && prob(10) && Dm.truthy(Waiting.NoSleep(source.Adjacent(user))))
        {
            if (Dm.truthy(user.is_holding(tool))) //checking if they're holding it in case TK is involved
                Waiting.NoSleep(user.dropItemToGround(tool));
            float firestacks = Dm.num(tool.get_temperature()) / 200;
            Waiting.NoSleep(user.adjust_fire_stacks(Dm.clamp(firestacks, 1, 10)));
            Waiting.NoSleep(user.apply_damage(Dm.clamp(firestacks, 4, 12), BURN, user.get_active_hand(), attacking_item: tool));
            user.ignite_mob();
            if (Dm.truthy(user.on_fire))
                user.visible_message(
                    span_warning(I18n.T("[] accidentally ignites []self!", user, user.p_them())),
                    span_userdanger(I18n.T("You miss [] and accidentally light yourself on fire!", this)),
                    visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
            else
                user.visible_message(
                    span_warning(I18n.T("[] accidentally burns []self!", user, user.p_them())),
                    span_userdanger(I18n.T("You miss [] and accidentally burn yourself!", this)),
                    visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(user.is_holding(source as obj_item))) //no TK shit here.
        {
            Waiting.NoSleep(user.dropItemToGround(source as obj_item));
            source.add_fingerprint(user);
        }
        user.visible_message(ignition_message);
        Waiting.NoSleep(source.fire_act(tool.get_temperature()));
        if (Dm.truthy(source?._listen_lookup?[COMSIG_ATOM_IGNITED_BY_ITEM]))
            source._SendSignal(COMSIG_ATOM_IGNITED_BY_ITEM, Dm.list(source, user, tool));
        return ITEM_INTERACT_SUCCESS;
    }

    // Represents holding a paper over a cigarette like a badass
    // tg: code/datums/elements/burn_when_item_ignition.dm:65
    [SignalHandler]
    public virtual object try_hold_burn(obj_item source = null, mob_living user = null, atom interacting_with = null, DmList modifiers = null)
    {
        return try_burn(interacting_with, user, source, modifiers);
    }
}
