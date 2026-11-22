// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/listen_prayers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R3 (D327): the whole file: the component that lets a mind hear the crew's prayers (Santa hears those that name him), and its
// toggle action. The prayer's symbol in the chat line (icon2html) waits for the chat icons (not-translated.md 70), as the admins'
// prayer lines in pray.cs. The var blocks are generated (D111).

/**
 * A component that allows the attached mind to listen prayers from other mobs.
 * Unlike admins, they cannot smite the person for it, also the prayers are anonymous unless the person says who he's in the message.
 */
// tg: code/datums/components/listen_prayers.dm:5
public partial class datum_component_listen_prayers : datum_component
{
    // tg: code/datums/components/listen_prayers.dm:15
    [DmParams("pre_prayer_callback", "deity_name", "toggle_desc")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        var pre_prayer_callback = Dm.arg<datum_callback>(__extra, 0);
        object deity_name = Dm.arg(__extra, 1);
        object toggle_desc = Dm.arg(__extra, 2);
        if (parent is not datum_mind)
            return COMPONENT_INCOMPATIBLE;

        if (pre_prayer_callback != null)
            this.pre_prayer_callback = pre_prayer_callback;
        if (Dm.truthy(deity_name))
            this.deity_name = deity_name;
        if (Dm.truthy(toggle_desc))
            this.toggle_desc = Dm.str(toggle_desc);
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/datums/components/listen_prayers.dm:26
    public override object Destroy(float? force = null)
    {
        pre_prayer_callback = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/listen_prayers.dm:30
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(SSdcs, COMSIG_GLOB_SEND_PRAYER, nameof(on_sent_prayer));
        var mind = (datum_mind)parent;
        toggle = Dm.@new<datum_action_innate_listen_prayers>(mind);
        if (Dm.truthy(toggle_desc))
            toggle.desc = toggle_desc;
        if (mind.current != null)
            await toggle.Grant(mind.current);
    }

    // tg: code/datums/components/listen_prayers.dm:39
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(SSdcs, COMSIG_GLOB_SEND_PRAYER);
        QDEL_NULL(ref toggle);
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/listen_prayers.dm:43
    public virtual object on_sent_prayer(object source = null, mob praying = null, object message = null, object prayer_type = null, object symbol = null, DmList deities_that_listened = null, params object[] __extra)
    {
        var mind = (datum_mind)parent;
        if (mind.current == null || mind.current.client == null || IS_UNCONSCIOUS(mind.current)) //You can't hear prayers if unconscious or disconnected
            return null;
        if (!isliving(praying) || praying.stat == DEAD)
            return FALSE; //I don't see any reason in hell to why dead people should be allowed into this. This isn't a knockoff TRAIT_SIXTHSENSE.
        if (praying == mind.current) //Ignore prayers coming from ourselves.
            return null;
        if (Dm.@in(mind.current.client, GLOB.admins)) //This is redundant if we're adminning
            return null;
        if (HAS_MIND_TRAIT(mind.current, TRAIT_DONT_HEAR_PRAYERS))
            return null;
        DmList arguments = Dm.list(praying, message, prayer_type, symbol, deities_that_listened); // args.Copy(2)
        if (pre_prayer_callback != null && !Dm.truthy(Waiting.NoSleep(pre_prayer_callback.Invoke(arguments))))
            return null;
        prayer_type = arguments[ARG_PRAYER_TYPE];
        symbol = arguments[ARG_PRAYER_SYMBOL];
        // tg: :59  icon2html(arguments[ARG_PRAYER_SYMBOL], mind.current.client): the chat icons (not-translated.md 70)
        message = Dm.text("<b><font color=[]>[]: </font></b> []", GLOB.prayer_type_to_font_color[prayer_type], prayer_type, span_notice(Dm.str(arguments[ARG_PRAYER_MSG])));
        to_chat(mind.current, custom_boxed_message(GLOB.prayer_type_to_message_box[prayer_type] as string, message));
        Dm.output_to(mind.current, sound(Dm.resource("sound/effects/pray.ogg"))); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
        if (Dm.truthy(deity_name))
            Dm.append(ref deities_that_listened, deity_name);
        return null;
    }
}

///An action to stop hearing prayers on command. Doesn't do a whole lot without the associated component
// tg: code/datums/components/listen_prayers.dm:68
public partial class datum_action_innate_listen_prayers : datum_action_innate
{
    // tg: code/datums/components/listen_prayers.dm:75
    public override object Destroy(float? force = null)
    {
        REMOVE_TRAIT(owner, TRAIT_DONT_HEAR_PRAYERS, ACTION_TRAIT);
        return base.Destroy(force);
    }

    // tg: code/datums/components/listen_prayers.dm:79
    public override object is_action_active(atom_movable_screen_movable_action_button current_button = null)
    {
        return !HAS_TRAIT_FROM(owner, TRAIT_DONT_HEAR_PRAYERS, ACTION_TRAIT) ? TRUE : FALSE;
    }

    // tg: code/datums/components/listen_prayers.dm:82
    public override async DmTask<object> Activate()
    {
        active = TRUE;
        REMOVE_TRAIT(owner, TRAIT_DONT_HEAR_PRAYERS, ACTION_TRAIT);
        to_chat(owner, span_green(I18n.T("You are ready to listen to prayers once again.")));
        await build_all_button_icons(UPDATE_BUTTON_BACKGROUND);
        return null;
    }

    // tg: code/datums/components/listen_prayers.dm:88
    public override async DmTask<object> Deactivate()
    {
        active = FALSE;
        ADD_TRAIT(owner, TRAIT_DONT_HEAR_PRAYERS, ACTION_TRAIT);
        to_chat(owner, span_green(I18n.T("You stop listening to prayers.")));
        await build_all_button_icons(UPDATE_BUTTON_BACKGROUND);
        return null;
    }
}
