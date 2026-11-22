// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tourniquet.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice D: the whole file. The tourniquet is applied to a limb by the limb_applicable component (M6); the bodypart reads
// it in refresh_bleed_rate (_bodyparts.dm) and the carbon's grasp (carbon.dm), already translated.

// tg: code/game/objects/items/tourniquet.dm:1
public partial class obj_item_tourniquet : obj_item
{
    // tg: code/game/objects/items/tourniquet.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        DmList valid_zones = GLOB.limb_zones.Copy();
        Dm.append(ref valid_zones, BODY_ZONE_HEAD);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_limb_applicable),
            Dm.named("valid_zones", valid_zones),
            Dm.named("apply_category", LIMB_ITEM_TOURNIQUET),
            Dm.named("do_apply", Dm.@new<datum_callback>(this, nameof(do_apply_tourniquet)))));
        RegisterSignal(this, COMSIG_ITEM_APPLIED_TO_LIMB, nameof(on_applied_to_limb));
        RegisterSignal(this, COMSIG_ITEM_UNAPPLIED_FROM_LIMB, nameof(on_removed_from_limb));
        return dot;
    }

    // tg: code/game/objects/items/tourniquet.dm:24
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/tourniquet.dm:28
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins to wrap [] around [] neck too tight! It looks like [] trying to commit suicide!", user, this, p_their(), user.p_theyre())));
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, user)))
            return SHAME;
        obj_item_bodypart head = user.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
        if (head == null || !Dm.truthy(await head.dismember()))
            return SHAME;
        return OXYLOSS;
    }

    // tg: code/game/objects/items/tourniquet.dm:37
    [SignalHandler]
    public virtual void on_applied_to_limb(datum source = null, obj_item_bodypart limb = null)
    {
        limb.refresh_bleed_rate();
        if (Dm.eq(limb.body_zone, BODY_ZONE_L_ARM) || Dm.eq(limb.body_zone, BODY_ZONE_R_ARM))
        {
            obj_item_bodypart_arm arm = limb as obj_item_bodypart_arm;
            Waiting.NoSleep(arm.set_speed_modifiers(arm.interaction_modifier + 0.5f, arm.click_cd_modifier + 0.5f));
        }
        else if (Dm.eq(limb.body_zone, BODY_ZONE_L_LEG) || Dm.eq(limb.body_zone, BODY_ZONE_R_LEG))
        {
            obj_item_bodypart_leg leg = limb as obj_item_bodypart_leg;
            Waiting.NoSleep(leg.set_speed_modifier(Dm.num(leg.speed_modifier) + 0.5f));
        }
        else if (Dm.eq(limb.body_zone, BODY_ZONE_HEAD))
        {
            START_PROCESSING(SSobj, this);
        }
    }

    // tg: code/game/objects/items/tourniquet.dm:50
    [SignalHandler]
    public virtual void on_removed_from_limb(datum source = null, obj_item_bodypart limb = null)
    {
        limb.refresh_bleed_rate();
        if (Dm.eq(limb.body_zone, BODY_ZONE_L_ARM) || Dm.eq(limb.body_zone, BODY_ZONE_R_ARM))
        {
            obj_item_bodypart_arm arm = limb as obj_item_bodypart_arm;
            Waiting.NoSleep(arm.set_speed_modifiers(arm.interaction_modifier - 0.5f, arm.click_cd_modifier - 0.5f));
        }
        else if (Dm.eq(limb.body_zone, BODY_ZONE_L_LEG) || Dm.eq(limb.body_zone, BODY_ZONE_R_LEG))
        {
            obj_item_bodypart_leg leg = limb as obj_item_bodypart_leg;
            Waiting.NoSleep(leg.set_speed_modifier(Dm.num(leg.speed_modifier) - 0.5f));
        }
        else if (Dm.eq(limb.body_zone, BODY_ZONE_HEAD))
        {
            START_PROCESSING(SSobj, this); // tg starts, not stops, here; process() kills itself once off a limb
        }
    }

    // tg: code/game/objects/items/tourniquet.dm:63
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        obj_item_bodypart limb = loc as obj_item_bodypart;
        if (limb == null || limb.owner == null)
            return PROCESS_KILL;
        if (HAS_TRAIT(limb.owner, TRAIT_NOBREATH))
            return null; // you win this time

        float spt = Dm.num(seconds_per_tick);
        limb.owner.losebreath += 1 * spt; // incapable of breathing
        Waiting.NoSleep(limb.owner.apply_damage(1 * spt, OXY, BODY_ZONE_HEAD)); // no blood getting to brain
        if (SPT_PROB(6, seconds_per_tick))
            Waiting.NoSleep(limb.owner.apply_damage(10, STAMINA, BODY_ZONE_HEAD));
        if (SPT_PROB(5, seconds_per_tick))
            Waiting.NoSleep(limb.owner.adjust_eye_blur(4 * SECONDS));
        if (SPT_PROB(4, seconds_per_tick))
        {
            Waiting.NoSleep(limb.owner.adjust_dizzy(4 * SECONDS));
            Waiting.NoSleep(limb.owner.adjust_confusion(2 * SECONDS));
        }
        return null;
    }

    // tg: code/game/objects/items/tourniquet.dm:80
    public virtual async DmTask<object> do_apply_tourniquet(mob user = null, mob_living patient = null, obj_item_bodypart limb = null)
    {
        float speed_multiplier = 2;
        bool speed_boosted = false;
        foreach (datum_wound woundies in Dm.each_cast<datum_wound>(limb.wounds))
        {
            if (!Dm.truthy(woundies.blood_flow))
                continue;
            if (HAS_TRAIT(woundies, TRAIT_WOUND_SCANNED) && !Dm.eq(limb.body_zone, BODY_ZONE_HEAD))
            {
                speed_multiplier = 0.5f;
                speed_boosted = true;
                break;
            }
            speed_multiplier = 1;
        }

        if (Dm.eq(limb.body_zone, BODY_ZONE_HEAD))
            speed_multiplier *= 2; // are you sure about that?
        else if (user != patient)
            speed_multiplier *= 0.5f;

        if (Dm.eq(limb.body_zone, BODY_ZONE_HEAD))
        {
            user.visible_message(
                span_warning(I18n.T("[] begins applying [] to [] neck...", user, this, user == patient ? p_their() : I18n.T("[]'s", patient))),
                span_userdanger(I18n.T("You begin applying [] to [] neck, though you begin wonder if this is a good idea...?", this, user == patient ? I18n.T("your") : I18n.T("[]'s", patient))),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE,
                ignored_mobs: patient);
            if (user != patient)
                patient.show_message(
                    span_userdanger(I18n.T("[] begins applying [] to your neck! That can't be a good idea...!", user, this)),
                    MSG_VISUAL,
                    span_userdanger(I18n.T("You feel [] start wrapping something tight around your neck! That can't be a good idea...!", user)));
        }
        else if (speed_boosted)
        {
            user.visible_message(
                span_notice(I18n.T("[] begins expertly applying [] to [] own []...", user, this, user == patient ? p_their() : I18n.T("[]'s", patient), limb.plaintext_zone)),
                span_notice(I18n.T("You begin expertly applying [] to [] own [], keeping the holo-image indications in mind...", this, user == patient ? I18n.T("your") : I18n.T("[]'s", patient), limb.plaintext_zone)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE,
                ignored_mobs: patient);
            if (user != patient)
                patient.show_message(
                    span_notice(I18n.T("[] begins expertly applying [] to your []...", user, this, limb.plaintext_zone)),
                    MSG_VISUAL,
                    span_notice(I18n.T("You feel [] start wrapping something around your [] with expert precision...", user, limb.plaintext_zone)));
        }
        else
        {
            user.visible_message(
                span_warning(I18n.T("[] begins applying [] to [] []...", user, this, user == patient ? p_their() : I18n.T("[]'s", patient), limb.plaintext_zone)),
                span_warning(I18n.T("You begin applying [] to [] []...", this, user == patient ? I18n.T("your") : I18n.T("[]'s", patient), limb.plaintext_zone)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE,
                ignored_mobs: patient);
            if (user != patient)
                patient.show_message(
                    span_warning(I18n.T("[] begins applying [] to your []...", user, this, limb.plaintext_zone)),
                    MSG_VISUAL,
                    span_warning(I18n.T("You feel [] start wrapping something around your []...", user, limb.plaintext_zone)));
        }

        if (!Dm.truthy(await do_after(user, 5 * SECONDS * speed_multiplier, patient)))
            return FALSE;

        // pain from tight wrapping
        await patient.apply_damage(5, BRUTE, limb, attacking_item: this);
        await patient.apply_damage(20, STAMINA, limb, attacking_item: this);
        return TRUE;
    }
}
