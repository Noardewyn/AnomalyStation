// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/radio/intercom.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D: the radio): the wall intercoms, their area power, their locks, their mounting and detaching, and
// their departmental and special variants. emag_act came with the emag (G1a, D316). Telekinesis's attack_tk_grab came with gaps DV2. Left out with a comment:
// attack_ai and attack_robot (the silicons' clicks, a later milestone). The MAPPING_DIRECTIONAL_HELPERS subtypes are var blocks. The var blocks are generated (D111).

// tg: code/game/objects/items/devices/radio/intercom.dm:1
public partial class obj_item_radio_intercom : obj_item_radio
{
    // tg: code/game/objects/items/devices/radio/intercom.dm:35
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        area current_area = get_area(this) as area;
        if (current_area == null)
            return dot;
        RegisterSignal(current_area, COMSIG_AREA_POWER_CHANGE, nameof(AreaPowerCheck));
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        Dm.append(ref GLOB.intercoms_list, this);
        return dot;
    }

    // tg: code/game/objects/items/devices/radio/intercom.dm:45
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.intercoms_list, this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/radio/intercom.dm:49
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Use [] when nearby to speak into it.", MODE_TOKEN_INTERCOM)));
        if (!Dm.truthy(unscrewed))
            Dm.append(ref dot, span_notice(I18n.T("It's <b>screwed</b> and secured to the wall.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("It's <i>unscrewed</i> from the wall, and can be <b>detached</b>.")));

        if (Dm.truthy(anonymize))
            Dm.append(ref dot, span_notice(I18n.T("Speaking through this intercom will anonymize your voice.")));

        if (Dm.eq(freqlock, RADIO_FREQENCY_UNLOCKED))
        {
            if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) && Dm.eq(Dm.initial(this, (obj_item_radio_intercom p) => p.freqlock), RADIO_FREQENCY_EMAGGABLE_LOCK))
                Dm.append(ref dot, span_warning(I18n.T("Its frequency lock has been shorted...")));
        }
        else
            Dm.append(ref dot, span_notice(I18n.T("It has a frequency lock set to [].", Dm.num(frequency) / 10)));

        if (Dm.eq(keylock, RADIO_KEYSLOT_UNLOCKED))
        {
            if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) && Dm.eq(Dm.initial(this, (obj_item_radio_intercom p) => p.keylock), RADIO_KEYSLOT_EMAGGABLE_LOCK))
                Dm.append(ref dot, span_warning(I18n.T("Its keyslot's security screws have been uplifted...")));
        }
        else
            Dm.append(ref dot, span_notice(I18n.T("The screws in its keyslot are [], preventing the removal of its encryption key[].", Dm.eq(keylock, RADIO_KEYSLOT_LOCKED) ? "stripped" : "fastened tight", Dm.eq(keylock, RADIO_KEYSLOT_LOCKED) ? "" : " without some kind of magnet")));
        return dot;
    }

    // tg: code/game/objects/items/devices/radio/intercom.dm:73
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        if (Dm.eq(held_item?.tool_behaviour, TOOL_SCREWDRIVER))
        {
            context[SCREENTIP_CONTEXT_RMB] = Dm.truthy(unscrewed) ? "Secure to wall" : "Unscrew from wall";
            context[SCREENTIP_CONTEXT_LMB] = keyslot == null ? context[SCREENTIP_CONTEXT_RMB] : "Remove encryption key"; // sometimes same behavior
            dot = CONTEXTUAL_SCREENTIP_SET;
        }

        if (Dm.eq(held_item?.tool_behaviour, TOOL_WRENCH) && Dm.truthy(unscrewed))
        {
            context[SCREENTIP_CONTEXT_RMB] = "Detach from wall";
            context[SCREENTIP_CONTEXT_LMB] = context[SCREENTIP_CONTEXT_LMB]; // same behavior
            dot = CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/radio/intercom.dm:85
    public override async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(unscrewed))
        {
            user.visible_message(span_notice(I18n.T("[] starts tightening []'s screws...", user, this)), span_notice(I18n.T("You start screwing in []...", this)));
            if (Dm.truthy(await tool.use_tool(this, user, 30, volume: 50)))
            {
                user.visible_message(span_notice(I18n.T("[] tightens []'s screws!", user, this)), span_notice(I18n.T("You tighten []'s screws.", this)));
                unscrewed = FALSE;
                update_appearance(UPDATE_OVERLAYS);
            }
        }
        else
        {
            user.visible_message(span_notice(I18n.T("[] starts loosening []'s screws...", user, this)), span_notice(I18n.T("You start unscrewing []...", this)));
            if (Dm.truthy(await tool.use_tool(this, user, 40, volume: 50)))
            {
                user.visible_message(span_notice(I18n.T("[] loosens []'s screws!", user, this)), span_notice(I18n.T("You unscrew [], loosening it from the wall.", this)));
                unscrewed = TRUE;
                update_appearance(UPDATE_OVERLAYS);
            }
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/radio/intercom.dm:100
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (keyslot == null)
            return await screwdriver_act_secondary(user, tool);
        return await base.screwdriver_act(user, tool);
    }

    // tg: code/game/objects/items/devices/radio/intercom.dm:105
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(unscrewed))
        {
            to_chat(user, span_warning(I18n.T("You need to unscrew [] from the wall first!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        user.visible_message(span_notice(I18n.T("[] starts unsecuring []...", user, this)), span_notice(I18n.T("You start unsecuring []...", this)));
        tool.play_tool_sound(this);
        if (Dm.truthy(await tool.use_tool(this, user, 80)))
        {
            user.visible_message(span_notice(I18n.T("[] unsecures []!", user, this)), span_notice(I18n.T("You detach [] from the wall.", this)));
            playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            await deconstruct(TRUE);
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/radio/intercom.dm:117
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return await wrench_act(user, tool);
    }

    /**
     * Override attack_tk_grab instead of attack_tk because we actually want attack_tk's
     * functionality. What we DON'T want is attack_tk_grab attempting to pick up the
     * intercom as if it was an ordinary item.
     */
    // tg: code/game/objects/items/devices/radio/intercom.dm:125  (gaps DV2)
    public override async DmTask<object> attack_tk_grab(mob user = null)
    {
        await interact(user);
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/items/devices/radio/intercom.dm:130  attack_ai, :133 attack_robot: the silicons' clicks (a later milestone)

    // tg: code/game/objects/items/devices/radio/intercom.dm:136
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        await interact(user);
        return dot;
    }

    // tg: code/game/objects/items/devices/radio/intercom.dm:142
    public override object ui_state(mob user = null)
    {
        return GLOB.default_state;
    }

    // tg: code/game/objects/items/devices/radio/intercom.dm:145
    public override object can_receive(object freq = null, object levels = null)
    {
        if (!Dm.eq(levels, RADIO_NO_Z_LEVEL_RESTRICTION))
        {
            turf position = get_turf(this);
            if (position == null || !Dm.@in(position.z, levels))
                return FALSE;
        }

        if (Dm.eq(freq, FREQ_SYNDICATE))
        {
            special_channels = Dm.bitand(special_channels, RADIO_SPECIAL_SYNDIE); // tg's `&=` (a typo for `&`) keeps only that bit
            if (!Dm.truthy(special_channels))
                return FALSE;//Prevents broadcast of messages over devices lacking the encryption
        }

        return TRUE;
    }

    // tg: code/game/objects/items/devices/radio/intercom.dm:157
    public override async DmTask<object> Hear(atom_movable speaker = null, object message_language = null, object raw_message = null, object radio_freq = null, object radio_freq_name = null, object radio_freq_color = null, DmList spans = null, DmList message_mods = null, float? message_range = null)
    {
        message_mods ??= Dm.list();
        if (Dm.eq(message_mods[RADIO_EXTENSION], MODE_INTERCOM))
            return null;  // Avoid hearing the same thing twice
        return await base.Hear(speaker, message_language, raw_message, radio_freq, radio_freq_name, radio_freq_color, spans, message_mods, message_range);
    }

    // tg: code/game/objects/items/devices/radio/intercom.dm:162
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra); // Parent call here will set `on` to FALSE.
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/devices/radio/intercom.dm:166
    public override object end_emp_effect(object curremp = null)
    {
        object dot = base.end_emp_effect(curremp);
        AreaPowerCheck(); // Make sure the area/local APC is powered first before we actually turn back on.
        return dot;
    }

    // tg: code/game/objects/items/devices/radio/intercom.dm:170
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        object dot = await base.emag_act(user, emag_card, __extra);

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return dot;

        if (!Dm.truthy(freqlock) && !Dm.truthy(keylock))
        {
            balloon_alert(user, "no locks to break!");
            return dot;
        }

        string message = "";
        if (Dm.eq(freqlock, RADIO_FREQENCY_EMAGGABLE_LOCK) && Dm.eq(keylock, RADIO_KEYSLOT_EMAGGABLE_LOCK))
            message = "frequency and key lock";
        else if (Dm.eq(freqlock, RADIO_FREQENCY_EMAGGABLE_LOCK))
            message = "frequency lock";
        else if (Dm.eq(keylock, RADIO_KEYSLOT_EMAGGABLE_LOCK))
            message = "key lock";

        if (message == "")
        {
            balloon_alert(user, Dm.text("can't break lock[]!", Dm.truthy(freqlock) && Dm.truthy(keylock) ? "s" : ""));
            playsound(this, "sound/machines/buzz/buzz-two.ogg", 50, FALSE, SILENCED_SOUND_EXTRARANGE);
            return dot;
        }

        balloon_alert(user, Dm.text("[] broken", message));
        playsound(this, SFX_SPARKS, 75, TRUE, SILENCED_SOUND_EXTRARANGE);
        if (Dm.eq(freqlock, RADIO_FREQENCY_EMAGGABLE_LOCK))
            freqlock = RADIO_FREQENCY_UNLOCKED;
        if (Dm.eq(keylock, RADIO_KEYSLOT_EMAGGABLE_LOCK))
            keylock = RADIO_KEYSLOT_UNLOCKED;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        return TRUE;
    }

    // tg: code/game/objects/items/devices/radio/intercom.dm:202
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.truthy(on) ? Dm.str(Dm.initial(this, (obj_item_radio_intercom p) => p.icon_state)) : icon_off;
        return await base.update_icon_state(__extra);
    }

    /**
     * Proc called whenever the intercom's area loses or gains power. Responsible for setting the `on` variable and calling `update_icon()`.
     *
     * Normally called after the intercom's area receives the `COMSIG_AREA_POWER_CHANGE` signal, but it can also be called directly.
     * Arguments:
     * * source - the area that just had a power change.
     */
    // tg: code/game/objects/items/devices/radio/intercom.dm:213
    [SignalHandler]
    public virtual void AreaPowerCheck(datum source = null)
    {
        area current_area = get_area(this) as area;
        if (current_area == null)
            set_on(FALSE);
        else
            set_on(current_area.powered(AREA_USAGE_EQUIP)); // set "on" to the equipment power status of our area.
        update_appearance();
    }

    /**
     * Called by the wall mount component and reused during the tool deconstruction proc.
     */
    // tg: code/game/objects/items/devices/radio/intercom.dm:225
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        Dm.@new<obj_item_wallframe_intercom>(get_turf(this));
    }
}

// tg: code/game/objects/items/devices/radio/intercom.dm:25
public partial class obj_item_radio_intercom_prison : obj_item_radio_intercom
{
    // tg: code/game/objects/items/devices/radio/intercom.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (wires != null)
            Waiting.NoSleep(wires.cut(WIRE_TX));
        return dot;
    }
}

// Used in the confessional booth in the chapel, locked to the confessional frequency and hides voices
// tg: code/game/objects/items/devices/radio/intercom.dm:239
public partial class obj_item_radio_intercom_chapel : obj_item_radio_intercom
{
    // tg: code/game/objects/items/devices/radio/intercom.dm:245
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_frequency(FREQ_CONFESSIONAL);
        set_broadcasting(TRUE);
        return dot;
    }
}

// Set of intercoms for use in interrogation. Interior one starts broadcasting, exterior one hides voices.
// tg: code/game/objects/items/devices/radio/intercom.dm:260
public partial class obj_item_radio_intercom_interrogation : obj_item_radio_intercom
{
    // tg: code/game/objects/items/devices/radio/intercom.dm:265
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_frequency(FREQ_INTERROGATION);
        return dot;
    }
}

// tg: code/game/objects/items/devices/radio/intercom.dm:269
public partial class obj_item_radio_intercom_interrogation_inside : obj_item_radio_intercom_interrogation
{
    // tg: code/game/objects/items/devices/radio/intercom.dm:272
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_broadcasting(TRUE);
        set_listening(FALSE);
        return dot;
    }
}

// For use in the AI core to allow the AI to tune into any encrypted frequency if comms are down
// tg: code/game/objects/items/devices/radio/intercom.dm:288
public partial class obj_item_radio_intercom_freerange_ai_core : obj_item_radio_intercom_freerange
{
    // tg: code/game/objects/items/devices/radio/intercom.dm:291
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_listening(FALSE);
        return dot;
    }
}

// tg: code/game/objects/items/devices/radio/intercom.dm:315
public partial class obj_item_radio_intercom_ai_private : obj_item_radio_intercom
{
    // tg: code/game/objects/items/devices/radio/intercom.dm:319
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_frequency(FREQ_AI_PRIVATE);
        return dot;
    }
}

// For use in AI uploads: Tuned to AI private, actively broadcasting and relaying
// tg: code/game/objects/items/devices/radio/intercom.dm:324
public partial class obj_item_radio_intercom_ai_private_broadcasting : obj_item_radio_intercom_ai_private
{
    // tg: code/game/objects/items/devices/radio/intercom.dm:326
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_broadcasting(TRUE);
        return dot;
    }
}

// For use in AI chambers: Tuned to AI private, free-range allowed, otherwise doesn't broadcast or relay
// tg: code/game/objects/items/devices/radio/intercom.dm:331
public partial class obj_item_radio_intercom_ai_private_freerange : obj_item_radio_intercom_ai_private
{
    // tg: code/game/objects/items/devices/radio/intercom.dm:335
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_listening(FALSE);
        return dot;
    }
}

// For use in AI antechambers: Tuned to AI private, actively broadcasting, but not relaying
// tg: code/game/objects/items/devices/radio/intercom.dm:340
public partial class obj_item_radio_intercom_ai_private_quiet : obj_item_radio_intercom_ai_private
{
    // tg: code/game/objects/items/devices/radio/intercom.dm:342
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_listening(FALSE);
        return dot;
    }
}

// Subtype that spawns with an encryption key and has a key lock
// tg: code/game/objects/items/devices/radio/intercom.dm:347
public partial class obj_item_radio_intercom_departmental : obj_item_radio_intercom
{
    // tg: code/game/objects/items/devices/radio/intercom.dm:353
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (keyslot is obj_item_encryptionkey key && Dm.num(length(key.channels)) >= 1)
            set_frequency(GLOB.default_radio_channels[key.channels[1]]);
        return dot;
    }

    // tg: code/game/objects/items/devices/radio/intercom.dm:358
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(stripe_color))
            return dot;
        mutable_appearance stripe = mutable_appearance(icon, "intercom_stripe") as mutable_appearance;
        stripe.color = stripe_color;
        Dm.append(ref dot, stripe);
        return dot;
    }
}

// The AI's and cyborgs' clicks (silicons SI2, D326)
// tg: code/game/objects/items/devices/radio/intercom.dm:130
public partial class obj_item_radio_intercom : obj_item_radio
{
    // tg: code/game/objects/items/devices/radio/intercom.dm:130
    public override async DmTask<object> attack_ai(mob user = null)
    {
        await interact(user);
        return null;
    }

    // tg: code/game/objects/items/devices/radio/intercom.dm:133
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        await interact(user);
        return null;
    }
}
