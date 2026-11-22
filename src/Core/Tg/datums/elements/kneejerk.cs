// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/kneejerk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DE1 (D333): the whole file (the gavel's and the carpenter hammer's knee tap on a buckled human's leg with a right click: the
// leg kicks out sharply, late or weakly by the brain's damage).

/// An element which enables certain items to tap people on their knees to measure brain health
// tg: code/datums/elements/kneejerk.dm:2
public partial class datum_element_kneejerk : datum_element
{
    // tg: code/datums/elements/kneejerk.dm:4
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);

        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;

        RegisterSignal(target, COMSIG_ITEM_ATTACK, nameof(on_item_attack));
        return dot;
    }

    // tg: code/datums/elements/kneejerk.dm:12
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);

        UnregisterSignal(source, COMSIG_ITEM_ATTACK);
        return dot;
    }

    // tg: code/datums/elements/kneejerk.dm:17
    [SignalHandler]
    public virtual object on_item_attack(datum source = null, mob_living target = null, mob_living user = null, DmList modifiers = null)
    {
        if ((user.zone_selected == BODY_ZONE_L_LEG || user.zone_selected == BODY_ZONE_R_LEG) && Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)) && target.buckled != null)
        {
            Waiting.NoSleep(tap_knee(source as obj_item, target, user));

            return COMPONENT_SKIP_ATTACK;
        }
        return null;
    }

    // tg: code/datums/elements/kneejerk.dm:25
    public virtual async DmTask tap_knee(obj_item item = null, mob_living target = null, mob_living user = null)
    {
        string selected_zone = user.zone_selected;
        obj_item_bodypart_leg right = target.get_bodypart(BODY_ZONE_R_LEG) as obj_item_bodypart_leg;
        obj_item_bodypart_leg left = target.get_bodypart(BODY_ZONE_L_LEG) as obj_item_bodypart_leg;
        obj_item_organ_brain target_brain = target.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;

        if (!ishuman(target))
            return;

        if ((selected_zone == BODY_ZONE_R_LEG) && right == null)
            return;
        if ((selected_zone == BODY_ZONE_L_LEG) && left == null)
            return;

        await user.do_attack_animation(target);
        target.visible_message(span_warning(I18n.T("[] gently taps []'s knee with [].", user, target, item)),
            span_userdanger(I18n.T("[] taps your knee with [].", user, item)));

        if (target.stat == DEAD) //dead men have no reflexes!
            return;

        if (target_brain == null)
            return;

        float target_brain_damage = Dm.num(target_brain.damage);

        if (target_brain_damage < BRAIN_DAMAGE_MILD) //a healthy brain produces a normal reaction
        {
            playsound(target, Dm.resource("sound/items/weapons/punchmiss.ogg"), 25, TRUE, -1);
            target.visible_message(span_danger(I18n.T("[]'s leg kicks out sharply!", target)),
                span_danger(I18n.T("Your leg kicks out sharply!")));
        }

        else if (target_brain_damage < BRAIN_DAMAGE_SEVERE) //a mildly damaged brain produces a delayed reaction
        {
            playsound(target, Dm.resource("sound/items/weapons/punchmiss.ogg"), 15, TRUE, -1);
            target.visible_message(span_danger(I18n.T("After a moment, []'s leg kicks out sharply!", target)),
                span_danger(I18n.T("After a moment, your leg kicks out sharply!")));
        }

        else if (target_brain_damage < BRAIN_DAMAGE_DEATH) //a severely damaged brain produces a delayed + weaker reaction
        {
            playsound(target, Dm.resource("sound/items/weapons/punchmiss.ogg"), 5, TRUE, -1);
            target.visible_message(span_danger(I18n.T("After a moment, []'s leg kicks out weakly!", target)),
                span_danger(I18n.T("After a moment, your leg kicks out weakly!")));
        }

        return;
    }
}
