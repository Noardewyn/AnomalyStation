// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/radio/headset.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D: the radio): the headsets, their second key, the channel list on examine, the high-volume mode and
// the languages their keys grant. Left out with a comment: the bowman headsets' ear protection (the wearer-targeting ear
// protection component, with the flashbangs) and the mining headset's callouts (the callouts component, with mining). The var
// blocks are generated (D111).

public partial class datum_controller_global_vars
{
    // Used for translating channels to tokens on examination
    // tg: code/game/objects/items/devices/radio/headset.dm:2  GLOBAL_LIST_INIT(channel_tokens, list(...))
    public DmList channel_tokens;

    // tg: code/game/objects/items/devices/radio/headset.dm:2
    public virtual void InitGlobalchannel_tokens()
    {
        channel_tokens = Dm.list(
            Dm.kv(RADIO_CHANNEL_COMMON, RADIO_KEY_COMMON),
            Dm.kv(RADIO_CHANNEL_SCIENCE, RADIO_TOKEN_SCIENCE),
            Dm.kv(RADIO_CHANNEL_COMMAND, RADIO_TOKEN_COMMAND),
            Dm.kv(RADIO_CHANNEL_MEDICAL, RADIO_TOKEN_MEDICAL),
            Dm.kv(RADIO_CHANNEL_ENGINEERING, RADIO_TOKEN_ENGINEERING),
            Dm.kv(RADIO_CHANNEL_SECURITY, RADIO_TOKEN_SECURITY),
            Dm.kv(RADIO_CHANNEL_CENTCOM, RADIO_TOKEN_CENTCOM),
            Dm.kv(RADIO_CHANNEL_SYNDICATE, RADIO_TOKEN_SYNDICATE),
            Dm.kv(RADIO_CHANNEL_SUPPLY, RADIO_TOKEN_SUPPLY),
            Dm.kv(RADIO_CHANNEL_SERVICE, RADIO_TOKEN_SERVICE),
            Dm.kv(MODE_BINARY, MODE_TOKEN_BINARY),
            Dm.kv(RADIO_CHANNEL_AI_PRIVATE, RADIO_TOKEN_AI_PRIVATE),
            Dm.kv(RADIO_CHANNEL_ENTERTAINMENT, RADIO_TOKEN_ENTERTAINMENT));
        Dm.append(ref gvars_datum_init_order, "channel_tokens");
    }
}

// tg: code/game/objects/items/devices/radio/headset.dm:18
[DmPath("/obj/item/radio/headset")]
public partial class obj_item_radio_headset : obj_item_radio
{
    // tg: code/game/objects/items/devices/radio/headset.dm:44
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins putting \\the []'s antenna up [] nose! It looks like [] trying to give []self cancer!", user, this, user.p_their(), user.p_theyre(), user.p_them())));
        return TOXLOSS;
    }

    // tg: code/game/objects/items/devices/radio/headset.dm:48
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        if (!Dm.truthy(Dm.bitand(item_flags, IN_INVENTORY)) || loc != user)
        {
            Dm.append(ref dot, span_notice(I18n.T("A small screen on the headset flashes, it's too small to read without holding or wearing the headset.")));
            return dot;
        }

        // construction of frequency description
        DmList available_channels = Dm.list();
        Dm.append(ref available_channels, I18n.T("<li><b>[]</b> for the currently tuned frequency</li>", span_radio(RADIO_KEY_COMMON)));
        if (Dm.truthy(Dm.bitand(special_channels, RADIO_SPECIAL_BINARY)))
            Dm.append(ref available_channels, I18n.T("<li><b>[] for []</b></li>", span_binarysay(MODE_TOKEN_BINARY), span_binarysay(Dm.str(capitalize(MODE_BINARY)))));

        for (float i = 1, i__end = Dm.num(length(channels)); i <= i__end; i++)
        {
            object channel_name = channels[i];
            object channel_token = GLOB.channel_tokens[channel_name];
            object channel_span_class = get_radio_span(GLOB.default_radio_channels[channel_name]);

            if (i == 1)
                Dm.append(ref available_channels, I18n.T("<li><b>[]</b> or <b>[]</b> for <b>[]</b></li>", span_class(channel_span_class, MODE_TOKEN_DEPARTMENT), span_class(channel_span_class, channel_token), span_class(channel_span_class, channel_name)));
            else
                Dm.append(ref available_channels, I18n.T("<li><b>[]</b> for <b>[]</b></li>", span_class(channel_span_class, channel_token), span_class(channel_span_class, channel_name)));
        }

        Dm.append(ref dot, span_notice(I18n.T("A small screen on the headset displays the following available frequencies:")));
        Dm.append(ref dot, span_notice(Dm.text("<ul style='display:inline-block; margin: 0; list-style: square;'>[]</ul>", available_channels.Join("", 1, 0))));

        if (Dm.truthy(command))
            Dm.append(ref dot, span_info(I18n.T("<b>Alt-click</b> to toggle the high-volume mode.")));
        return dot;
    }

    // tg: code/game/objects/items/devices/radio/headset.dm:77
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (ispath(keyslot2))
            keyslot2 = Dm.@new((Type)keyslot2, this);
        set_listening(TRUE);
        set_broadcasting(TRUE);
        recalculateChannels();
        possibly_deactivate_in_loc();
        return dot;
    }

    // tg: code/game/objects/items/devices/radio/headset.dm:86
    public virtual void possibly_deactivate_in_loc()
    {
        if (ismob(loc))
            set_listening(should_be_listening);
        else
            set_listening(FALSE, actual_setting: FALSE);
    }

    // tg: code/game/objects/items/devices/radio/headset.dm:92
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        possibly_deactivate_in_loc();
        return dot;
    }

    // tg: code/game/objects/items/devices/radio/headset.dm:96
    public override object Destroy(float? force = null)
    {
        if (keyslot2 is obj_item_encryptionkey)
        {
            qdel(keyslot2);
            keyslot2 = null;
        }
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/radio/headset.dm:101
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = await base.ui_data(user) as DmList;
        dot["headset"] = TRUE;
        return dot;
    }

    // tg: code/game/objects/items/devices/radio/headset.dm:105
    public override async DmTask<object> mouse_drop_dragged(atom over = null, mob user = null, object src_location = null, object over_location = null, object @params = null)
    {
        if (user == over)
            return await attack_self(user);
        return null;
    }

    /// Grants all the languages this headset allows the mob to understand via installed chips.
    // tg: code/game/objects/items/devices/radio/headset.dm:110
    public virtual void grant_headset_languages(mob grant_to = null)
    {
        DmList language_list = (keyslot as obj_item_encryptionkey)?.language_data?.Copy();

        if (keyslot2 is obj_item_encryptionkey key2)
        {
            if (Dm.truthy(length(language_list)))
            {
                foreach (object language in Dm.each(key2.language_data))
                {
                    if (Dm.lt(language_list[language], key2.language_data[language]))
                    {
                        language_list[language] = key2.language_data[language];
                        continue;
                    }
                    language_list[language] = key2.language_data[language];
                }
            }

            else
                language_list = key2.language_data?.Copy();
        }

        foreach (object language in Dm.each(language_list))
        {
            object amount_understood = language_list[language];
            if (Dm.ge(amount_understood, 100))
                grant_to.grant_language(language, language_flags: UNDERSTOOD_LANGUAGE, source: LANGUAGE_RADIOKEY);
            else
                grant_to.grant_partial_language(language, amount: Dm.num(amount_understood), source: LANGUAGE_RADIOKEY);
        }
    }

    /// Clears all radio related languages from the mob.
    // tg: code/game/objects/items/devices/radio/headset.dm:132
    public virtual void remove_headset_languages(mob remove_from = null)
    {
        if (QDELETED(remove_from)) //This can be called as a part of destroy
            return;
        remove_from.remove_all_languages(source: LANGUAGE_RADIOKEY);
        remove_from.remove_all_partial_languages(source: LANGUAGE_RADIOKEY);
    }

    // tg: code/game/objects/items/devices/radio/headset.dm:138
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.truthy(Dm.bitand(slot_flags, slot)))
            return;

        grant_headset_languages(user);
    }

    // tg: code/game/objects/items/devices/radio/headset.dm:145
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        remove_headset_languages(user);
        return dot;
    }

    // Headsets do not become hearing sensitive as broadcasting instead controls their talk_into capabilities
    // tg: code/game/objects/items/devices/radio/headset.dm:150
    public override void set_broadcasting(object new_broadcasting = null, float? actual_setting = null)
    {
        actual_setting ??= TRUE;
        broadcasting = Dm.num(new_broadcasting);
        if (Dm.truthy(actual_setting))
            should_be_broadcasting = broadcasting;

        if (Dm.truthy(perform_update_icon) && overlay_mic_idle != null)
            Waiting.NoSleep(update_icon());
        else if (!Dm.truthy(perform_update_icon))
            should_update_icon = TRUE;
    }

    // tg: code/game/objects/items/devices/radio/headset.dm:160
    public override async DmTask<object> talk_into_impl(atom_movable talking_movable = null, object message = null, object channel = null, DmList spans = null, object language = null, DmList message_mods = null)
    {
        if (!Dm.truthy(broadcasting))
            return null;
        return await base.talk_into_impl(talking_movable, message, channel, spans, language, message_mods);
    }

    // tg: code/game/objects/items/devices/radio/headset.dm:435
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == keyslot2)
        {
            keyslot2 = null;
            if (!QDELING(this))
                recalculateChannels();
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/radio/headset.dm:442
    public override async DmTask<DmList> remove_keys(mob_living user = null)
    {
        DmList dot = await base.remove_keys(user);
        if (!(keyslot2 is obj_item_encryptionkey key2))
            return dot;

        Dm.append(ref dot, key2);
        await user.put_in_hands(key2); // null via Exited
        return dot;
    }

    // tg: code/game/objects/items/devices/radio/headset.dm:450
    public override async DmTask<object> install_key(mob_living user = null, obj_item_encryptionkey key = null)
    {
        if (!Dm.truthy(keyslot))
            return await base.install_key(user, key);

        if (Dm.truthy(keyslot2))
        {
            loc.balloon_alert(user, I18n.T("cannot hold a third key!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.transferItemToLoc(key, this)))
        {
            loc.balloon_alert(user, I18n.T("cannot install!"));
            return ITEM_INTERACT_BLOCKING;
        }

        keyslot2 = key;
        recalculateChannels();
        playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        loc.balloon_alert(user, I18n.T("encryption key installed"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/radio/headset.dm:468
    public override void recalculateChannels()
    {
        base.recalculateChannels();
        if (keyslot2 is obj_item_encryptionkey key2)
        {
            foreach (object channel_name in Dm.each(key2.channels))
            {
                if (!Dm.@in(channel_name, channels))
                    channels[channel_name] = key2.channels[channel_name];
            }

            special_channels = Dm.bitor(special_channels, key2.special_channels);

            foreach (object ch_name in Dm.each(channels))
                LAZYSET(ref secure_radio_connections, ch_name, add_radio(this, GLOB.default_radio_channels[ch_name]));
        }

        // Updates radio languages entirely for the mob wearing the headset
        if (loc is mob mob_loc && mob_loc.get_item_by_slot(slot_flags) == this)
        {
            remove_headset_languages(mob_loc);
            grant_headset_languages(mob_loc);
        }
    }

    // tg: code/game/objects/items/devices/radio/headset.dm:486
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!(user is mob_living) || !Dm.truthy(command))
            return CLICK_ACTION_BLOCKING;
        use_command = Dm.num(!Dm.truthy(use_command));
        to_chat(user, span_notice(I18n.T("You toggle high-volume mode [].", Dm.truthy(use_command) ? "on" : "off")));
        return CLICK_ACTION_SUCCESS;
    }
}

// tg: code/game/objects/items/devices/radio/headset.dm:165
public partial class obj_item_radio_headset_syndicate : obj_item_radio_headset //disguised to look like a normal headset for stealth ops
{
    // tg: code/game/objects/items/devices/radio/headset.dm:167
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        make_syndie();
        return dot;
    }
}

// tg: code/game/objects/items/devices/radio/headset.dm:171
public partial class obj_item_radio_headset_syndicate_alt : obj_item_radio_headset_syndicate //undisguised bowman with flash protection
{
    // tg: code/game/objects/items/devices/radio/headset.dm:177
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddComponent(/datum/component/wearertargeting/earprotection): the ear protection from the bangs (with the flashbangs)
        Dm.Deferred("code/game/objects/items/devices/radio/headset.dm:179", "Initialize: AddComponent(/datum/component/wearertargeting/earprotection), the bowman headset's ear protection (with the flashbangs)");
        return dot;
    }
}

// tg: code/game/objects/items/devices/radio/headset.dm:195
public partial class obj_item_radio_headset_headset_sec_alt : obj_item_radio_headset_headset_sec
{
    // tg: code/game/objects/items/devices/radio/headset.dm:201
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/game/objects/items/devices/radio/headset.dm:203", "Initialize: AddComponent(/datum/component/wearertargeting/earprotection), the bowman headset's ear protection (with the flashbangs)");
        return dot;
    }
}

// tg: code/game/objects/items/devices/radio/headset.dm:278
public partial class obj_item_radio_headset_heads_captain_alt : obj_item_radio_headset_heads_captain
{
    // tg: code/game/objects/items/devices/radio/headset.dm:284
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/game/objects/items/devices/radio/headset.dm:286", "Initialize: AddComponent(/datum/component/wearertargeting/earprotection), the bowman headset's ear protection (with the flashbangs)");
        return dot;
    }
}

// tg: code/game/objects/items/devices/radio/headset.dm:304
public partial class obj_item_radio_headset_heads_hos_alt : obj_item_radio_headset_heads_hos
{
    // tg: code/game/objects/items/devices/radio/headset.dm:310
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/game/objects/items/devices/radio/headset.dm:312", "Initialize: AddComponent(/datum/component/wearertargeting/earprotection), the bowman headset's ear protection (with the flashbangs)");
        return dot;
    }
}

// tg: code/game/objects/items/devices/radio/headset.dm:341
public partial class obj_item_radio_headset_headset_cargo_mining : obj_item_radio_headset_headset_cargo
{
    // tg: code/game/objects/items/devices/radio/headset.dm:351
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddComponent(/datum/component/callouts, ITEM_SLOT_EARS, ...): the miners' callouts (with mining)
        Dm.Deferred("code/game/objects/items/devices/radio/headset.dm:353", "Initialize: AddComponent(/datum/component/callouts), the mining headset's callouts (with mining)");
        return dot;
    }

    // tg: code/game/objects/items/devices/radio/headset.dm:355
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_EARS)))
            ADD_TRAIT(user, TRAIT_SPEECH_BOOSTER, CLOTHING_TRAIT);
    }

    // tg: code/game/objects/items/devices/radio/headset.dm:360
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        REMOVE_TRAIT(user, TRAIT_SPEECH_BOOSTER, CLOTHING_TRAIT);
        return dot;
    }
}

// tg: code/game/objects/items/devices/radio/headset.dm:387
public partial class obj_item_radio_headset_headset_cent_alt : obj_item_radio_headset_headset_cent
{
    // tg: code/game/objects/items/devices/radio/headset.dm:394
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/game/objects/items/devices/radio/headset.dm:396", "Initialize: AddComponent(/datum/component/wearertargeting/earprotection), the bowman headset's ear protection (with the flashbangs)");
        return dot;
    }
}

// tg: code/game/objects/items/devices/radio/headset.dm:410
public partial class obj_item_radio_headset_silicon_human_ai : obj_item_radio_headset_silicon
{
    // tg: code/game/objects/items/devices/radio/headset.dm:418
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        ADD_TRAIT(user, TRAIT_LOUD_BINARY, REF(this));
    }

    // tg: code/game/objects/items/devices/radio/headset.dm:422
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        REMOVE_TRAIT(user, TRAIT_LOUD_BINARY, REF(this));
        return dot;
    }
}

// tg: code/game/objects/items/devices/radio/headset.dm:426
public partial class obj_item_radio_headset_silicon_ai_evil : obj_item_radio_headset_silicon_ai
{
    // tg: code/game/objects/items/devices/radio/headset.dm:431
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        make_syndie();
        return dot;
    }
}
