// Translated from tgstation (snapshot 1d99bd7f): code/datums/saymode.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M9 slice D: SSradio's say modes): the base /datum/saymode and (silicons SI1, D326) the silicons' and drones' binary.
// Silicons SI4 added the AI's holopad; gaps K1 (D327) the changelings' hivemind; gaps X5 the xenomorphs' hivemind. The other
// subtype (the vocal cords: not-translated.md 73) keeps its generated code. The var block is generated (D111).

// tg: code/datums/saymode.dm:1
public partial class datum_saymode : datum
{
    /// Checks whether this saymode can be used by the given user. May send feedback.
    // tg: code/datums/saymode.dm:10
    public virtual object can_be_used_by(mob_living user = null)
    {
        return TRUE;
    }

    /**
     * Handles actually modifying or forwarding our message.
     * Returns `SAYMODE_[X]` flags.
     *
     * user - The living speaking using this say mode.
     * message - The message to be said.
     * spans - A list of spans to attach to the message.
     * language - The language the message was said in.
     * message_mods - A list of message modifiers, i.e. whispering/singing.
     */
    // tg: code/datums/saymode.dm:23 (gaps K1: user is a mob, not tg's declared mob/living: the changeling hivemind's members, imaginary
    // friends (/mob/eye), speak through the changeling say mode, absorb.dm:212; rules 3.5, a "-" cell, per-slot decision)
    public virtual async DmTask<object> handle_message(mob user = null, object message = null, DmList spans = null, object language = null, DmList message_mods = null)
    {
        spans ??= Dm.list();
        message_mods ??= Dm.list();
        return NONE;
    }
}

// tg: code/datums/saymode.dm:33
public partial class datum_saymode_changeling : datum_saymode
{
    // tg: code/datums/saymode.dm:37
    public override object can_be_used_by(mob_living user = null)
    {
        if (Dm.truthy(user.mind?.has_antag_datum(typeof(datum_antagonist_fallen_changeling))))
        {
            // special message for you
            to_chat(user, span_changeling(I18n.T("<b>We're cut off from the hivemind! We've lost everything! EVERYTHING!!</b>")));
            return FALSE;
        }
        if (!HAS_TRAIT(user, TRAIT_CHANGELING_HIVEMIND))
            return FALSE;
        if (Dm.truthy(is_muted(user)))
        {
            to_chat(user, span_warning(I18n.T("The poison in the air hinders our ability to interact with the hivemind.")));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/datums/saymode.dm:49 (user is a mob, not tg's declared mob/living: get_lings passes every hivemind mob, the absorbed's
    // imaginary friends among them; rules 3.5, a "-" cell)
    public virtual object is_muted(mob user = null)
    {
        return ((datum_reagents)user.reagents)?.has_reagent(typeof(datum_reagent_bz_metabolites), needs_metabolizing: TRUE);
    }

    // tg: code/datums/saymode.dm:52
    public virtual object get_lings()
    {
        DmList dot = null;
        dot = Dm.list();
        foreach (mob ling_mob in Dm.each_cast<mob>(GLOB.mob_list))
        {
            //removes types that override the presence of being changeling (for example, borged lings still can't hivemind chat)
            if (!HAS_TRAIT(ling_mob, TRAIT_CHANGELING_HIVEMIND) || issilicon(ling_mob) || isbrain(ling_mob))
                continue;
            // can't receive messages on the hivemind right now
            if (Dm.truthy(is_muted(ling_mob)))
                continue;
            Dm.append(ref dot, ling_mob);
        }
        return dot;
    }

    // tg: code/datums/saymode.dm:63
    public override async DmTask<object> handle_message(mob user = null, object message = null, DmList spans = null, object language = null, DmList message_mods = null)
    {
        spans ??= Dm.list();
        message_mods ??= Dm.list();
        datum_antagonist_changeling ling_sender = IS_CHANGELING(user) as datum_antagonist_changeling;

        object id = (ling_sender?.changelingID is var or__1 && Dm.truthy(or__1) ? (object)or__1 : user.real_name);

        user.log_talk(message, LOG_SAY, tag: Dm.text("[]", id));
        string msg = span_changeling(I18n.T("<b>[]:</b> []", id, message));

        // Send the message to our other changelings.
        foreach (mob ling_mob in Dm.each_cast<mob>(get_lings()))
            to_chat(ling_mob, msg, type: MESSAGE_TYPE_RADIO, avoid_highlighting: Dm.num(ling_mob == user));

        foreach (object ghost in Dm.each(GLOB.dead_mob_list)) // var/mob/dead/ghost as anything: the list holds dead living mobs too (rules 3.4)
            to_chat(ghost, Dm.text("[] []", FOLLOW_LINK(ghost, user), msg), type: MESSAGE_TYPE_RADIO);
        return SAYMODE_MESSAGE_HANDLED;
    }
}

// tg: code/datums/saymode.dm:87  (gaps X5: the xenomorphs' hivemind, ":a")
public partial class datum_saymode_xeno : datum_saymode
{
    // tg: code/datums/saymode.dm:91
    public override object can_be_used_by(mob_living user = null)
    {
        if (!Dm.truthy(user.hivecheck()))
            return FALSE;
        return TRUE;
    }
}

// tg: code/datums/saymode.dm:96 (tg defines the proc on a subtype named handle_message by mistake, as the binary's; SSradio's PreInit
// makes one of each say mode, and this subtype, coming after /datum/saymode/xeno, takes the "a" key, so hivemind speech runs it as in tg)
public partial class datum_saymode_xeno_handle_message : datum_saymode_xeno
{
    // tg: code/datums/saymode.dm:96
    public override async DmTask<object> handle_message(mob user = null, object message = null, DmList spans = null, object language = null, DmList message_mods = null)
    {
        spans ??= Dm.list();
        message_mods ??= Dm.list();
        ((mob_living)user).alien_talk(message, spans, message_mods); // tg's user is a mob/living (the slot's user is a mob since gaps K1)
        return SAYMODE_MESSAGE_HANDLED;
    }
}

// tg: code/datums/saymode.dm:107 (gaps OG1: the vocal cords' channel)
public partial class datum_saymode_vocalcords : datum_saymode
{
    // tg: code/datums/saymode.dm:111
    public override object can_be_used_by(mob_living user = null)
    {
        if (!iscarbon(user))
            return FALSE;
        return TRUE;
    }
}

// tg: code/datums/saymode.dm:116 (gaps OG1; the same tg mistake as the xeno's and the binary's: the subtype, coming after
// /datum/saymode/vocalcords, takes the "x" key, so vocal cord speech runs it as in tg)
public partial class datum_saymode_vocalcords_handle_message : datum_saymode_vocalcords
{
    // tg: code/datums/saymode.dm:116
    public override async DmTask<object> handle_message(mob user = null, object message = null, DmList spans = null, object language = null, DmList message_mods = null)
    {
        spans ??= Dm.list();
        message_mods ??= Dm.list();
        mob_living_carbon carbon_user = user as mob_living_carbon;
        obj_item_organ_vocal_cords our_vocal_cords = carbon_user.get_organ_slot(ORGAN_SLOT_VOICE) as obj_item_organ_vocal_cords;
        if (Dm.truthy(our_vocal_cords?.can_speak_with()))
        {
            await our_vocal_cords.handle_speech(message); //message
            await our_vocal_cords.speak_with(message); //action
        }
        return SAYMODE_MESSAGE_HANDLED;
    }
}

// tg: code/datums/saymode.dm:132
public partial class datum_saymode_binary : datum_saymode //everything that uses .b (silicons, drones)
{
    // tg: code/datums/saymode.dm:136
    public override object can_be_used_by(mob_living user = null)
    {
        if (!isdrone(user) && !Dm.truthy(user.binarycheck()))
            return FALSE;
        return TRUE;
    }
}

// tg: code/datums/saymode.dm:141 (tg defines the proc on a subtype named handle_message by mistake; SSradio's PreInit makes one of
// each say mode, and this subtype, coming after /datum/saymode/binary, takes the "b" key, so binary speech runs it as in tg)
public partial class datum_saymode_binary_handle_message : datum_saymode_binary
{
    // tg: code/datums/saymode.dm:141
    public override async DmTask<object> handle_message(mob user = null, object message = null, DmList spans = null, object language = null, DmList message_mods = null)
    {
        spans ??= Dm.list();
        message_mods ??= Dm.list();
        if (isdrone(user))
        {
            mob_living_basic_drone drone_user = user as mob_living_basic_drone;
            drone_user.drone_chat(message, spans, message_mods);
        }
        else if (Dm.truthy(((mob_living)user).binarycheck()))
            ((mob_living)user).robot_talk(message, spans, message_mods);
        return SAYMODE_MESSAGE_HANDLED;
    }
}

// tg: code/datums/saymode.dm:156
public partial class datum_saymode_holopad : datum_saymode
{
    // tg: code/datums/saymode.dm:161
    public override object can_be_used_by(mob_living user = null)
    {
        if (!isAI(user))
            return FALSE;
        return TRUE;
    }
}

// tg: code/datums/saymode.dm:166 (tg defines the proc on a subtype named handle_message by mistake, as the binary's; SSradio's PreInit
// makes one of each say mode, and this subtype, coming after /datum/saymode/holopad, takes the "h" key, so holopad speech runs it as in tg)
public partial class datum_saymode_holopad_handle_message : datum_saymode_holopad
{
    // tg: code/datums/saymode.dm:166
    public override async DmTask<object> handle_message(mob user = null, object message = null, DmList spans = null, object language = null, DmList message_mods = null)
    {
        spans ??= Dm.list();
        message_mods ??= Dm.list();
        mob_living_silicon_ai ai_user = user as mob_living_silicon_ai;
        await ai_user.holopad_talk(message, spans, language, message_mods);
        return SAYMODE_MESSAGE_HANDLED;
    }
}

