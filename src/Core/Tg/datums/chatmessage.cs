// Translated from tgstation (snapshot 1d99bd7f): code/datums/chatmessage.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice A (docs/ai/specs/social.md), DEVIATION(DV160): tg builds the runechat image on the server after asking the owner's BYOND client to
// measure the text (client.MeasureText at CHAT_MESSAGE_WIDTH), then moves the older lines over the same spot up and shortens their lives
// with animate() calls (finish_image_generation), and deletes the datum at the end of its life. The port keeps the server half up to
// the finished text (the cleanup, the clip at max_chat_length, the classes, the speaker's colour) and hands the line, its spot and its
// lifespan to the presentation seam (Runechat, protocol version 38); the client measures it with its fonts and does
// finish_image_generation's stacking, decay and fades (godot/scripts/Hud/Runechat). The radio and emote icons tg prefixes with \icon are
// drawn by the client from the line's classes (virtual-speaker, emote); the language icon is never shown for Galactic Common (the only
// language spoken in the port). The datum ends once it has handed the line over.

public partial class datum_chatmessage : datum
{
    /// How long the chat message will exist prior to any exponential decay
    // tg: code/datums/chatmessage.dm:4  #define CHAT_MESSAGE_LIFESPAN (5 SECONDS) (file-local)
    public const float CHAT_MESSAGE_LIFESPAN = 5 * SECONDS;

    /**
     * Constructs a chat message overlay
     *
     * Arguments:
     * * text - The text content of the overlay
     * * target - The target atom to display the overlay at
     * * owner - The mob that owns this overlay, only this mob will be able to view it
     * * language - The language this message was spoken in
     * * extra_classes - Extra classes to apply to the span that holds the text
     * * lifespan - The lifespan of the message in deciseconds
     */
    // tg: code/datums/chatmessage.dm:68
    [DmParams("text", "target", "owner", "language", "extra_classes", "lifespan", "message_mods")]
    public override async DmTask New(params object[] __extra)
    {
        object text = Dm.arg<object>(__extra, 0);
        atom target = Dm.arg<atom>(__extra, 1);
        mob owner = Dm.arg<mob>(__extra, 2);
        object language = Dm.arg<object>(__extra, 3);
        DmList extra_classes = Dm.arg<DmList>(__extra, 4) ?? Dm.list();
        float lifespan = Dm.num_or_null(Dm.arg<object>(__extra, 5)) ?? CHAT_MESSAGE_LIFESPAN;
        DmList message_mods = Dm.arg<DmList>(__extra, 6);
        await base.New();
        if (!istype(target, typeof(atom)))
            throw Dm.CRASH("Invalid target given for chatmessage", "code/datums/chatmessage.dm", 71);
        if (QDELETED(owner) || !istype(owner, typeof(mob)) || owner.client == null)
        {
            stack_trace(Dm.text("/datum/chatmessage created with [] mob owner", owner == null ? "null" : "invalid"), "code/datums/chatmessage.dm", 73);
            qdel(this);
            return;
        }
        Waiting.DetachSync(() => generate_image(text, target, owner, language, extra_classes, lifespan, message_mods));
    }

    /**
     * Generates a chat message image representation
     *
     * Arguments:
     * * text - The text content of the overlay
     * * target - The target atom to display the overlay at
     * * owner - The mob that owns this overlay, only this mob will be able to view it
     * * language - The language this message was spoken in
     * * extra_classes - Extra classes to apply to the span that holds the text
     * * lifespan - The lifespan of the message in deciseconds
     * * message_mods - All mods from whatever message triggered this (if applicable)
     */
    // tg: code/datums/chatmessage.dm:114
    public virtual void generate_image(object text = null, atom target = null, mob owner = null, object language = null, DmList extra_classes = null, float lifespan = CHAT_MESSAGE_LIFESPAN, DmList message_mods = null)
    {
        // Register client who owns this message
        owned_by = owner.client;
        // tg: :120  RegisterSignal(owned_by, COMSIG_QDELETING, PROC_REF(on_parent_qdel)): the datum ends at the end of this proc (DV160)

        // Remove spans in the message from things like the recorder
        text = Dm.regex(@"<\/?span[^>]*>", "gi").Replace(text, "");

        // Clip message
        float maxlen = Dm.num(safe_read_pref(owned_by, typeof(datum_preference_numeric_max_chat_length)));
        if (Dm.length_char(text) > maxlen)
            text = Dm.add(copytext_char(text, 1, maxlen + 1), "..."); // BYOND index moment

        // Get rid of any URL schemes that might cause BYOND to automatically wrap something in an anchor tag
        text = Dm.regex(@"[A-Za-z][A-Za-z0-9+-\.]*:\/\/", "g").Replace(text, "");

        // Reject whitespace
        if (Dm.truthy(Dm.regex(@"^\s*$").Find(text)))
        {
            qdel(this);
            return;
        }

        // Non mobs speakers can be small
        if (!ismob(target))
            Dm.combine(ref extra_classes, "small");

        // Why are you yelling?
        if (copytext_char(text, -2) == "!!")
            Dm.combine(ref extra_classes, SPAN_YELL);

        object chat_color_name_to_use = null;

        // Append radio icon if from a virtual speaker
        // tg: :149-155  the radio and emote icons (\icon of chat_icons.dmi) in front of the text: drawn by the client from these classes (DV160)
        if (!Dm.truthy(extra_classes.Find("virtual-speaker")) && Dm.truthy(extra_classes.Find("emote")))
            chat_color_name_to_use = target.get_visible_name(add_id_name: FALSE); // use face name for nonverbal messages

        if (chat_color_name_to_use == null)
        {
            if (HAS_TRAIT(target, TRAIT_SIGN_LANG))
                chat_color_name_to_use = target.get_visible_name(add_id_name: FALSE); // use face name for signers too
            else
                chat_color_name_to_use = target.get_voice(); // for everything else, use the target's voice name
        }

        // Calculate target color if not already present
        if (!Dm.truthy(target.chat_color) || !Dm.eq(target.chat_color_name, chat_color_name_to_use))
        {
            target.chat_color = colorize_string(chat_color_name_to_use);
            target.chat_color_darkened = colorize_string(chat_color_name_to_use, 0.85f, 0.85f);
            target.chat_color_name = chat_color_name_to_use;
        }

        // Append language icon if the language uses one
        datum_language language_instance = GLOB.language_datum_instances[language] as datum_language;
        object language_icon_type = (language_instance?.display_icon_type(owner, message_mods) is var or__1 && Dm.truthy(or__1) ? or__1 : DISPLAY_LANGUAGE_ICON_NONE);
        if (!Dm.eq(language_icon_type, DISPLAY_LANGUAGE_ICON_NONE))
        {
            // tg: :177-187  the language's icon (languages other than Galactic Common are the alpha's, not-translated.md section 70)
            Dm.Deferred("code/datums/chatmessage.dm:177", "generate_image: the language's icon over the speaker's head (the chat spritesheet, section 76)");
        }

        // We dim italicized text to make it more distinguishable from regular text
        object tgt_color = Dm.truthy(extra_classes.Find("italics")) ? target.chat_color_darkened : target.chat_color;

        // Approximate text height
        string complete_text = Dm.text("<span style='color: []'><span class='center []'>[]</span></span>", tgt_color, Dm.jointext(extra_classes, " "), apply_message_emphasis(text));

        // tg: :195-203  MeasureText and finish_image_generation: the client's (DV160)
        message_loc = isturf(target) ? target : get_atom_on_turf(target as atom_movable) as atom;
        DmWorldState.Current.Sinks.Messages.Runechat(owned_by, message_loc, MAPTEXT(complete_text), lifespan);
        qdel(this);
    }
}

public partial class mob : atom_movable
{
    /**
     * Creates a message overlay at a defined location for a given speaker
     *
     * Arguments:
     * * speaker - The atom who is saying this message
     * * message_language - The language that the message is said in
     * * raw_message - The text content of the message
     * * spans - Additional classes to be added to the message
     */
    // tg: code/datums/chatmessage.dm:322
    public virtual void create_chat_message(atom_movable speaker = null, object message_language = null, object raw_message = null, DmList spans = null, float? runechat_flags = null, DmList message_mods = null)
    {
        runechat_flags ??= NONE;
        // tg: :323  SSlag_switch.measures[DISABLE_RUNECHAT]: the lag switch is the admin tools (M10), never on
        if (HAS_TRAIT(speaker, TRAIT_RUNECHAT_HIDDEN))
            return;
        // Ensure the list we are using, if present, is a copy so we don't modify the list provided to us
        DmList classes = Dm.truthy(Dm.bitand(runechat_flags, EMOTE_MESSAGE)) ? Dm.list("emote", "italics") : LAZYCOPY(spans);

        // Check for virtual speakers (aka hearing a message through a radio)
        atom_movable original_speaker = speaker;
        if (!Dm.truthy(Dm.bitand(runechat_flags, EMOTE_MESSAGE)) && speaker is atom_movable_virtualspeaker virtual_speaker)
        {
            speaker = virtual_speaker.source;
            Dm.combine(ref classes, "virtual-speaker");
        }

        // Ignore virtual speaker (most often radio messages) from ourselves
        if (original_speaker != this && speaker == this)
            return;

        // Display visual above source
        Dm.@new<datum_chatmessage>(Dm.named("text", raw_message), Dm.named("target", speaker), Dm.named("owner", this), Dm.named("language", message_language), Dm.named("extra_classes", classes), Dm.named("message_mods", message_mods));
    }
}
