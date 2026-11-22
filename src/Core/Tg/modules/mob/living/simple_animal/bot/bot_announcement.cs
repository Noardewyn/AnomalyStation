// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/bot/bot_announcement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3) but the shortcut's prefix maptext on an action button (the client's action bar draws no maptext).

/// Say something and play a corresponding sound effect
// tg: code/modules/mob/living/simple_animal/bot/bot_announcement.dm:2
public partial class datum_action_cooldown_bot_announcement : datum_action_cooldown
{
    // tg: code/modules/mob/living/simple_animal/bot/bot_announcement.dm:18
    [DmParams("Target", "original", "automated_announcements")]
    public override async DmTask New(params object[] __extra)
    {
        automated_announcements = Dm.arg<DmList>(__extra, 2);
        await base.New(__extra);
    }

    // tg: code/modules/mob/living/simple_animal/bot/bot_announcement.dm:22
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return dot;
        if (!isbot(owner))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, "no announcement system!");
            return FALSE;
        }
        if (!Dm.truthy(length(automated_announcements)))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, "no valid announcements!");
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/mob/living/simple_animal/bot/bot_announcement.dm:36
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "BotAnnouncement", "Select announcement");
            ui.set_autoupdate(TRUE);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/mob/living/simple_animal/bot/bot_announcement.dm:43
    public override object ui_state(mob user = null)
    {
        return GLOB.conscious_state;
    }

    // tg: code/modules/mob/living/simple_animal/bot/bot_announcement.dm:46
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        object dot = await base.ui_status(user, state);
        if (user != owner)
            return UI_CLOSE;
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/bot/bot_announcement.dm:51
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        if (owner is mob_living_basic_bot bot_owner)
        {
            DmList channels = Dm.list();
            foreach (object channel in Dm.each<object>(bot_owner.internal_radio.channels))
                Dm.append(ref channels, channel);
            data["channels"] = channels;
        }
        else
            data["channels"] = Dm.list();
        DmList lines = Dm.list();
        foreach (object line in Dm.each<object>(automated_announcements))
            Dm.append(ref lines, line);
        data["lines"] = lines;
        DmList button_data = Dm.list();
        foreach (datum_action_cooldown_bot_announcement_shortcut button in Dm.each_cast<datum_action_cooldown_bot_announcement_shortcut>(buttons))
            Dm.append(ref button_data, Dm.list(Dm.list(Dm.kv("name", button.name), Dm.kv("channel", button.channel))));
        data["button_data"] = button_data;
        data["cooldown_left"] = next_use_time - world.time;
        return data;
    }

    // tg: code/modules/mob/living/simple_animal/bot/bot_announcement.dm:76
    public override object Destroy(float? force = null)
    {
        QDEL_LIST(buttons);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/simple_animal/bot/bot_announcement.dm:80
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action as string)
        {
            case "announce":
            {
                object picked = @params["picked"];
                object channel = @params["channel"];
                if (!Dm.@in(picked, automated_announcements))
                    return null;
                announce(picked, channel);
                StartCooldown();
                return TRUE;
            }
            case "set_button":
            {
                if (Dm.num(length(buttons)) >= max_buttons)
                    return null;
                object picked = @params["picked"];
                object channel = @params["channel"];
                if (!Dm.@in(picked, automated_announcements))
                    return null;
                await create_shortcut(picked, channel);
                return TRUE;
            }
            case "remove_button":
            {
                object index = text2num(@params["index"]);
                if (!Dm.truthy(index))
                    return null;
                if (Dm.num(index) < 1 || Dm.num(index) > Dm.num(length(buttons)))
                    return null;
                datum_action button = buttons[(int)Dm.num(index)] as datum_action;
                qdel(button);
                return TRUE;
            }
        }
        return null;
    }

    // tg: code/modules/mob/living/simple_animal/bot/bot_announcement.dm:113
    public override async DmTask<object> Activate(atom target = null)
    {
        if (Dm.num(length(automated_announcements)) > 1)
        {
            await ui_interact(owner);
            return null;
        }
        else if (Dm.num(length(automated_announcements)) == 1)
            announce(automated_announcements[1]);
        return await base.Activate(target);
    }

    // tg: code/modules/mob/living/simple_animal/bot/bot_announcement.dm:121
    public virtual async DmTask create_shortcut(object line = null, object channel = null)
    {
        datum_action_cooldown_bot_announcement_shortcut shortcut = await Dm.new_async<datum_action_cooldown_bot_announcement_shortcut>(this);

        shortcut.prefix = copytext(line, 1, 4);
        object color = GLOB.reserved_radio_colors[Dm.text("[]", channel)];
        if (Dm.truthy(color))
            shortcut.prefix_color = color as string;
        shortcut.name = line as string;
        shortcut.message = line;
        shortcut.channel = channel;
        shortcut.linked = this;
        await shortcut.Grant(owner);
        RegisterSignal(shortcut, COMSIG_QDELETING, nameof(on_shortcut_deleted));
        Dm.append(ref buttons, shortcut);
    }

    // tg: code/modules/mob/living/simple_animal/bot/bot_announcement.dm:136
    [SignalHandler]
    public virtual void on_shortcut_deleted(datum shortcut = null)
    {
        Dm.remove(ref buttons, shortcut);
    }

    // tg: code/modules/mob/living/simple_animal/bot/bot_announcement.dm:140
    public override async DmTask<object> Grant(mob grant_to = null, params object[] __extra)
    {
        object dot = await base.Grant(grant_to, __extra);
        foreach (datum_action action in Dm.each_cast<datum_action>(buttons))
            await action.Grant(grant_to);
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/bot/bot_announcement.dm:145
    public override async DmTask Remove(mob remove_from = null)
    {
        foreach (datum_action action in Dm.each_cast<datum_action>(buttons))
            await action.Remove(remove_from);
        await base.Remove(remove_from);
    }

    /// Speak the provided line on the provided radio channel
    // tg: code/modules/mob/living/simple_animal/bot/bot_announcement.dm:151
    public virtual void announce(object line = null, object channel = null)
    {
        mob_living_basic_bot bot_owner = owner as mob_living_basic_bot;
        if (!Dm.truthy(Dm.bitand(bot_owner.bot_mode_flags, BOT_MODE_ON)))
            return;

        Waiting.Detach(bot_owner.say(line));
        if (Dm.truthy(channel) && Dm.truthy(bot_owner.internal_radio.channels[channel]))
            bot_owner.internal_radio.talk_into(bot_owner, message: line, channel: channel);

        if (Dm.truthy(length(automated_announcements)) && automated_announcements[line] != null)
            playsound(bot_owner, automated_announcements[line], vol: 50, vary: FALSE);
    }
}

// tg: code/modules/mob/living/simple_animal/bot/bot_announcement.dm:164
public partial class datum_action_cooldown_bot_announcement_medbot : datum_action_cooldown_bot_announcement
{
    // tg: code/modules/mob/living/simple_animal/bot/bot_announcement.dm:166
    public override void announce(object line = null, object channel = null)
    {
        mob_living_basic_bot_medbot bot_owner = owner as mob_living_basic_bot_medbot;
        if (!Dm.truthy(Dm.bitand(bot_owner.medical_mode_flags, MEDBOT_SPEAK_MODE)))
            return;
        base.announce(line, channel);
    }
}

// tg: code/modules/mob/living/simple_animal/bot/bot_announcement.dm:172
public partial class datum_action_cooldown_bot_announcement_shortcut : datum_action_cooldown
{
    // tg: code/modules/mob/living/simple_animal/bot/bot_announcement.dm:193
    public override object Destroy(float? force = null)
    {
        linked = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/simple_animal/bot/bot_announcement.dm:197
    public override void apply_button_overlay(atom_movable_screen_movable_action_button current_button = null, float? force = null)
    {
        base.apply_button_overlay(current_button, force);
        Dm.Deferred("code/modules/mob/living/simple_animal/bot/bot_announcement.dm:197", "apply_button_overlay: the line's prefix as maptext on the action button (the client's action bar draws no maptext)");
    }

    // tg: code/modules/mob/living/simple_animal/bot/bot_announcement.dm:210
    public override async DmTask<object> Activate(atom target = null)
    {
        if (!Dm.truthy(message) || linked == null)
            return null;
        cooldown_time = linked.cooldown_time;
        linked.announce(message, channel);
        return await base.Activate(target);
    }
}
