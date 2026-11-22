// Translated from tgstation (snapshot 1d99bd7f): code/datums/martial/kaza_ruk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X3b (D316): Kaza Ruk (its three stance buttons, the leg sweep, the lung punch, the neck chop, the disarm and
// the punch's follow-up strike) and its gloves, whole but the security gloves' reskins (/datum/atom_skin and the reskinable item are
// not ported, not-translated.md section 147). The var blocks are generated (D111).

// tg: code/datums/martial/kaza_ruk.dm:1
public partial class datum_martial_art_kaza_ruk : datum_martial_art
{
    // tg: code/datums/martial/kaza_ruk.dm:10 and :93. tg defines activate_style twice on the type; the later one's ..() runs the
    // earlier one (the follow-up's signal), then it greets and grants the three stances.
    public override async DmTask activate_style(mob_living new_holder = null)
    {
        await base.activate_style(new_holder);
        RegisterSignal(new_holder, COMSIG_HUMAN_PUNCHED, nameof(blow_followup));
        to_chat(new_holder, span_userdanger(I18n.T("You know the arts of []!", name)));
        to_chat(new_holder, span_danger(I18n.T("Place your cursor over a move at the top of the screen to see what it does.")));
        await neckchop.Grant(new_holder);
        await lowsweep.Grant(new_holder);
        await lungpunch.Grant(new_holder);
    }

    // tg: code/datums/martial/kaza_ruk.dm:101 and :14: the later deactivate_style forgets and removes the stances, and its ..() runs
    // the earlier one (the base, then the follow-up's signal).
    public override async DmTask deactivate_style(mob_living remove_from = null)
    {
        to_chat(remove_from, span_userdanger(I18n.T("You suddenly forget the arts of []...", name)));
        if (neckchop != null)
            await neckchop.Remove(remove_from);
        if (lowsweep != null)
            await lowsweep.Remove(remove_from);
        if (lungpunch != null)
            await lungpunch.Remove(remove_from);
        await base.deactivate_style(remove_from);
        UnregisterSignal(remove_from, COMSIG_HUMAN_PUNCHED);
    }

    // tg: code/datums/martial/kaza_ruk.dm:18
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        neckchop = Dm.@new<datum_action_neck_chop>(this);
        lowsweep = Dm.@new<datum_action_low_sweep>(this);
        lungpunch = Dm.@new<datum_action_lung_punch>(this);
    }

    // tg: code/datums/martial/kaza_ruk.dm:24
    public override object Destroy(float? force = null)
    {
        neckchop = null;
        lowsweep = null;
        lungpunch = null;
        return base.Destroy(force);
    }

    // tg: code/datums/martial/kaza_ruk.dm:108
    public virtual async DmTask<object> check_streak(mob_living attacker = null, mob_living defender = null)
    {
        switch (streak)
        {
            case "neck_chop":
                streak = "";
                await neck_chop(attacker, defender);
                return TRUE;
            case "low_sweep":
                streak = "";
                await low_sweep(attacker, defender);
                return TRUE;
            case "quick_choke": //is actually lung punch
                streak = "";
                await quick_choke(attacker, defender);
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/datums/martial/kaza_ruk.dm:124
    public virtual async DmTask<object> low_sweep(mob_living attacker = null, mob_living defender = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(defender) || Dm.truthy(defender.IsParalyzed()))
            return MARTIAL_ATTACK_INVALID;
        if (HAS_TRAIT(attacker, TRAIT_PACIFISM))
            return MARTIAL_ATTACK_INVALID; // Does 5 damage, so we can't let pacifists leg sweep.

        bool tail_sweeping = false;
        string sweeping_language = "leg";
        if (ishuman(attacker))
        {
            var possible_human = (mob_living_carbon)attacker;
            if (possible_human.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL) is obj_item_organ_tail_lizard)
            {
                tail_sweeping = true;
                sweeping_language = "tail";
            }
        }

        defender.visible_message(
            span_warning(I18n.T("[] [] sweeps []!", attacker, sweeping_language, defender)),
            span_userdanger(I18n.T("Your legs are sweeped by []!", attacker)),
            span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
            null,
            attacker);
        to_chat(attacker, span_danger(I18n.T("You [] sweep []!", sweeping_language, defender)));
        playsound(attacker, "sound/effects/hit_kick.ogg", 50, TRUE, -1);

        if (tail_sweeping)
            await attacker.emote("spin");

        await defender.apply_damage(5, BRUTE, BODY_ZONE_CHEST);
        await defender.Knockdown(6 * SECONDS);
        log_combat(attacker, defender, "leg sweeped");
        return MARTIAL_ATTACK_SUCCESS;
    }

    // tg: code/datums/martial/kaza_ruk.dm:157
    public virtual async DmTask<object> quick_choke(mob_living attacker = null, mob_living defender = null) //is actually lung punch
    {
        await attacker.do_attack_animation(defender);
        defender.visible_message(
            span_warning(I18n.T("[] pounds [] on the chest!", attacker, defender)),
            span_userdanger(I18n.T("Your chest is slammed by []! You can't breathe!", attacker)),
            span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
            COMBAT_MESSAGE_RANGE,
            attacker);
        to_chat(attacker, span_danger(I18n.T("You pound [] on the chest!", defender)));
        playsound(attacker, "sound/effects/hit_punch.ogg", 50, TRUE, -1);
        if (Dm.num(defender.losebreath) <= 10)
            defender.losebreath = clamp(Dm.num(defender.losebreath) + 5, 0, 10);
        await defender.adjust_oxy_loss(10);
        log_combat(attacker, defender, "quickchoked");
        return MARTIAL_ATTACK_SUCCESS;
    }

    // tg: code/datums/martial/kaza_ruk.dm:174
    public virtual async DmTask<object> neck_chop(mob_living attacker = null, mob_living defender = null)
    {
        if (HAS_TRAIT(attacker, TRAIT_PACIFISM))
            return MARTIAL_ATTACK_INVALID; // Does 10 damage, so we can't let pacifists neck chop.
        await attacker.do_attack_animation(defender);
        defender.visible_message(
            span_warning(I18n.T("[] karate chops []'s neck!", attacker, defender)),
            span_userdanger(I18n.T("Your neck is karate chopped by [], rendering you unable to speak!", attacker)),
            span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
            COMBAT_MESSAGE_RANGE,
            attacker);
        to_chat(attacker, span_danger(I18n.T("You karate chop []'s neck, rendering [] unable to speak!", defender, defender.p_them())));
        playsound(attacker, "sound/effects/hit_punch.ogg", 50, TRUE, -1);
        await defender.apply_damage(10, Dm.str(attacker.get_attack_type()), BODY_ZONE_HEAD);
        await defender.adjust_silence_up_to(20 * SECONDS, 20 * SECONDS);
        log_combat(attacker, defender, "neck chopped");
        return MARTIAL_ATTACK_SUCCESS;
    }

    // tg: code/datums/martial/kaza_ruk.dm:192
    public override async DmTask<object> harm_act(mob_living attacker = null, mob_living defender = null)
    {
        if (Dm.truthy(await defender.check_block(attacker, 10, attacker.name, UNARMED_ATTACK)))
            return MARTIAL_ATTACK_FAIL;

        if (Dm.truthy(await check_streak(attacker, defender)))
            return MARTIAL_ATTACK_SUCCESS;

        return MARTIAL_ATTACK_INVALID;
    }

    // tg: code/datums/martial/kaza_ruk.dm:201
    public override async DmTask<object> disarm_act(mob_living attacker = null, mob_living defender = null)
    {
        if (Dm.truthy(await defender.check_block(attacker, 0, attacker.name, UNARMED_ATTACK)))
            return MARTIAL_ATTACK_FAIL;
        if (Dm.truthy(await check_streak(attacker, defender)))
            return MARTIAL_ATTACK_SUCCESS;
        var stuff_in_hand = defender.get_active_held_item() as obj_item;
        if (prob(60) && stuff_in_hand != null && Dm.truthy(await defender.temporarilyRemoveItemFromInventory(stuff_in_hand)))
        {
            await attacker.put_in_hands(stuff_in_hand);
            defender.visible_message(
                span_danger(I18n.T("[] disarms []!", attacker, defender)),
                span_userdanger(I18n.T("You're disarmed by []!", attacker)),
                span_hear(I18n.T("You hear aggressive shuffling!")),
                COMBAT_MESSAGE_RANGE,
                attacker);
            to_chat(attacker, span_danger(I18n.T("You disarm []!", defender)));
            playsound(defender, "sound/items/weapons/thudswoosh.ogg", 50, TRUE, -1);
            log_combat(attacker, defender, "disarmed (Kaza Ruk)", addition: Dm.text("(disarmed of [])", stuff_in_hand));
        }
        return MARTIAL_ATTACK_INVALID; // normal shove
    }

    /// First, determine if we're going to execute our followup attack
    // tg: code/datums/martial/kaza_ruk.dm:223
    [SignalHandler]
    public virtual void blow_followup(mob_living source = null, mob_living target = null, object damage = null, object attack_type = null, object atk_effect = null, obj_item_bodypart affecting = null, object final_armor_block = null, object limb_sharpness = null)
    {
        if (!prob(50))
            return;

        _addtimer(Dm.@new<datum_callback>(this, nameof(execute_followup), source, target, damage, attack_type, atk_effect, affecting, final_armor_block, limb_sharpness), 0.25f * SECONDS, file: "code/datums/martial/kaza_ruk.dm", line: 229);
    }

    /// After our delay, do the followup.
    // tg: code/datums/martial/kaza_ruk.dm:233
    public virtual async DmTask execute_followup(mob_living source = null, mob_living target = null, object damage = null, object attack_type = null, object atk_effect = null, obj_item_bodypart affecting = null, object final_armor_block = null, object limb_sharpness = null)
    {
        if (QDELETED(source) || QDELETED(target))
            return;

        if (!Dm.truthy(await source.Adjacent(target)))
            return;

        bool tail_usage = false;
        bool kicking = Dm.eq(atk_effect, ATTACK_EFFECT_KICK);
        string kick_language = "an axe kick";
        string strike_language = "an elbow strike";
        if (ishuman(source))
        {
            var possible_human = (mob_living_carbon)source;
            if (possible_human.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL) is obj_item_organ_tail_lizard)
            {
                tail_usage = true;
                kick_language = "a helmsplitter tail strike";
                strike_language = "a whiplash tail spin";
            }
        }

        source.visible_message(
            span_warning(I18n.T("[] follows up with [] against []!", source, kicking ? kick_language : strike_language, target)),
            span_notice(I18n.T("You deliver [] against []!", kicking ? kick_language : strike_language, target)),
            span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")));
        if (tail_usage)
            await source.emote(kicking ? "flip" : "spin");
        playsound(source, "sound/effects/hit_punch.ogg", 50, TRUE, -1);
        await source.do_attack_animation(target, ATTACK_EFFECT_KICK);
        await target.apply_damage(round(Dm.num(damage) / 3, 1), Dm.str(attack_type), affecting, Dm.num(final_armor_block), wound_bonus: Dm.num(damage)); //Ostensibly, apply a third of our damage again // We're not being too fussy about limb bonuses for this
        log_combat(source, target, "auto-followup strike (Kaza Ruk)");
    }
}

// tg: code/datums/martial/kaza_ruk.dm:30
public partial class datum_action_neck_chop : datum_action
{
    // tg: code/datums/martial/kaza_ruk.dm:37
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        var source = (datum_martial_art)target;
        if (source.streak == "neck_chop")
        {
            owner.visible_message(span_danger(I18n.T("[] assumes a neutral stance.", owner)), I18n.T("<b><i>Your next attack is cleared.</i></b>"));
            owner.balloon_alert(owner, "cleared");
            source.streak = "";
        }
        else
        {
            owner.visible_message(span_danger(I18n.T("[] assumes the Neck Chop stance!", owner)), I18n.T("<b><i>Your next attack will be a Neck Chop.</i></b>"));
            owner.balloon_alert(owner, "neck chop");
            source.streak = "neck_chop";
        }
        return dot;
    }
}

// tg: code/datums/martial/kaza_ruk.dm:51
public partial class datum_action_low_sweep : datum_action
{
    // tg: code/datums/martial/kaza_ruk.dm:58
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        var source = (datum_martial_art)target;
        if (source.streak == "low_sweep")
        {
            owner.visible_message(span_danger(I18n.T("[] assumes a neutral stance.", owner)), I18n.T("<b><i>Your next attack is cleared.</i></b>"));
            owner.balloon_alert(owner, "cleared");
            source.streak = "";
        }
        else
        {
            owner.visible_message(span_danger(I18n.T("[] assumes the Low Sweep stance!", owner)), I18n.T("<b><i>Your next attack will be a Low Sweep.</i></b>"));
            owner.balloon_alert(owner, "low sweep");
            source.streak = "low_sweep";
        }
        return dot;
    }
}

// tg: code/datums/martial/kaza_ruk.dm:72
public partial class datum_action_lung_punch : datum_action //referred to internally as 'quick choke'
{
    // tg: code/datums/martial/kaza_ruk.dm:79
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        var source = (datum_martial_art)target;
        if (source.streak == "quick_choke")
        {
            owner.visible_message(span_danger(I18n.T("[] assumes a neutral stance.", owner)), I18n.T("<b><i>Your next attack is cleared.</i></b>"));
            owner.balloon_alert(owner, "cleared");
            source.streak = "";
        }
        else
        {
            owner.visible_message(span_danger(I18n.T("[] assumes the Lung Punch stance!", owner)), I18n.T("<b><i>Your next attack will be a Lung Punch.</i></b>"));
            owner.balloon_alert(owner, "lung punch");
            source.streak = "quick_choke"; //internal name for lung punch
        }
        return dot;
    }
}

//Kaza Ruk Gloves

// tg: code/datums/martial/kaza_ruk.dm:266
public partial class obj_item_clothing_gloves_kaza_ruk : obj_item_clothing_gloves
{
    // tg: code/datums/martial/kaza_ruk.dm:270
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_martial_art_giver), typeof(datum_martial_art_kaza_ruk)));
        return dot;
    }
}

// tg: code/datums/martial/kaza_ruk.dm:303
public partial class obj_item_clothing_gloves_kaza_ruk_sec : obj_item_clothing_gloves_kaza_ruk
{
    // tg: code/datums/martial/kaza_ruk.dm:315
    public override DmTask setup_reskins()
    {
        Dm.Deferred("code/datums/martial/kaza_ruk.dm:316", "setup_reskins: AddComponent(/datum/component/reskinable_item, /datum/atom_skin/kaza_ruk, infinite = TRUE), the red and blue skins (the atom skins are not ported)");
        return DmTask.CompletedTask;
    }
}
