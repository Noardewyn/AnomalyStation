// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/silicon_say.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/mob/living/silicon/silicon_say.dm:1
public partial class mob_living : mob
{
    // tg: code/modules/mob/living/silicon/silicon_say.dm:1
    public virtual void robot_talk(object message = null, DmList spans = null, DmList message_mods = null)
    {
        spans ??= Dm.list();
        message_mods ??= Dm.list();
        log_sayverb_talk(message, message_mods, tag: "binary");

        object designation = "Default Cyborg";
        Dm.combine(ref spans, SPAN_ROBOT);

        if (issilicon(this))
        {
            mob_living_silicon player = this as mob_living_silicon;
            designation = trim_left(Dm.str(player.designation) + " " + Dm.str(player.job));
        }

        if (HAS_TRAIT(mind, TRAIT_DISPLAY_JOB_IN_BINARY))
            designation = mind.assigned_role.title;

        if (HAS_TRAIT(this, TRAIT_LOUD_BINARY))
            // AIs are loud and ugly
            Dm.combine(ref spans, SPAN_COMMAND);

        object messagepart = generate_messagepart(
            message,
            spans,
            message_mods);

        object namepart = name;
        // If carbon, use voice to account for voice changers
        if (iscarbon(this))
            namepart = get_voice(add_id_name: TRUE);

        // AI in carbon body should still have its real name
        obj_item_organ_brain_cybernetic_ai brain = get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain_cybernetic_ai;
        if (brain is obj_item_organ_brain_cybernetic_ai)
        {
            namepart = brain.mainframe.name;
            designation = brain.mainframe.job;
        }
        foreach (mob hearing_mob in Dm.each<mob>(GLOB.player_list))
        {
            if (Dm.truthy(hearing_mob.binarycheck()))
            {
                if (isAI(hearing_mob))
                {
                    to_chat(
                        hearing_mob,
                        span_binarysay(Dm.text(
                            "Robotic Talk, <a href='byond://?src=[];track=[]'>[]</a> <span class='message'>[]</span>",
                            REF(hearing_mob), html_encode(namepart), span_name(Dm.text("[] ([])", namepart, designation)), messagepart)),
                        type: MESSAGE_TYPE_RADIO,
                        avoid_highlighting: (this == hearing_mob) ? TRUE : FALSE);
                }
                else
                {
                    to_chat(
                        hearing_mob,
                        span_binarysay(Dm.text(
                            "Robotic Talk, [] <span class='message'>[]</span>",
                            span_name(Dm.text("[]", namepart)), messagepart)),
                        type: MESSAGE_TYPE_RADIO,
                        avoid_highlighting: (this == hearing_mob) ? TRUE : FALSE);
                }
            }

            if (isobserver(hearing_mob))
            {
                atom following = this;

                // If the AI talks on binary chat, we still want to follow
                // its camera eye, like if it talked on the radio

                if (isAI(this))
                {
                    mob_living_silicon_ai ai = this as mob_living_silicon_ai;
                    following = ai.eyeobj;
                }

                object follow_link = FOLLOW_LINK(hearing_mob, following);

                to_chat(
                    hearing_mob,
                    span_binarysay(Dm.text(
                        "[] Robotic Talk, [] <span class='message'>[]</span>",
                        follow_link, span_name(Dm.text("[]", namepart)), messagepart)),
                    type: MESSAGE_TYPE_RADIO,
                    avoid_highlighting: (this == hearing_mob) ? TRUE : FALSE);
            }
        }
    }
}

// tg: code/modules/mob/living/silicon/silicon_say.dm:81
public partial class mob_living_silicon : mob_living
{
    // tg: code/modules/mob/living/silicon/silicon_say.dm:81
    public override object binarycheck()
    {
        area our_area = get_area(this) as area;
        if (Dm.truthy(Dm.bitand(our_area.area_flags, BINARY_JAMMING)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/silicon/silicon_say.dm:87
    public override async DmTask<object> radio__proc(object message = null, DmList message_mods = null, DmList spans = null, object language = null)
    {
        message_mods ??= Dm.list();
        object dot = await base.radio__proc(message, message_mods, spans, language);
        if (Dm.truthy(dot))
        {
            do_tts_message(message, language, message_mods, Dm.list(), Dm.list());
            return dot;
        }
        obj_item_radio radio = this.radio as obj_item_radio;
        if (Dm.truthy(message_mods[MODE_HEADSET]))
        {
            if (radio != null)
            {
                radio.talk_into(this, message, null, spans, language, message_mods);
                do_tts_message(message, language, message_mods, Dm.list(), Dm.list());
            }
            return NOPASS;
        }
        else if (Dm.@in(message_mods[RADIO_EXTENSION], GLOB.default_radio_channels))
        {
            if (radio != null)
            {
                radio.talk_into(this, message, message_mods[RADIO_EXTENSION], spans, language, message_mods);
                do_tts_message(message, language, message_mods, Dm.list(), Dm.list());
                return NOPASS;
            }
        }
        return FALSE;
    }
}
