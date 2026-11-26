// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/mind_linker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP2c (D349): the whole file (the stargazer's Slime Link). The var blocks are generated (D111).

/**
 * # Mind Linker
 *
 * A component that handles linking multiple player's minds
 * into one network which allows them to talk directly to one another.
 * Like telepathy but for multiple people at once!
 */
// tg: code/datums/components/mind_linker.dm:8
public partial class datum_component_mind_linker : datum_component
{
    // tg: code/datums/components/mind_linker.dm:32
    [DmParams("network_name", "chat_color", "speech_action_icon", "speech_action_icon_state", "speech_action_background_icon_state", "speech_action_overlay_state", "signals_which_destroy_us", "post_unlink_callback", "show_balloon_alert")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        // The defaults of the DM parameters, for the ones the caller left out
        object network_name = Dm.arg(__extra, 0) ?? "Mind Link";
        object chat_color = Dm.arg(__extra, 1) ?? "#008CA2";
        object speech_action_icon = Dm.arg(__extra, 2) ?? Dm.resource("icons/mob/actions/actions_slime.dmi");
        object speech_action_icon_state = Dm.arg(__extra, 3) ?? "link_speech";
        object speech_action_background_icon_state = Dm.arg(__extra, 4) ?? "bg_alien";
        object speech_action_overlay_state = Dm.arg(__extra, 5) ?? "bg_alien_border";
        // Optional
        object signals_which_destroy_us = Dm.arg(__extra, 6);
        datum_callback post_unlink_callback = Dm.arg(__extra, 7) as datum_callback;
        object show_balloon_alert = Dm.arg(__extra, 8);

        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;

        mob_living owner = parent as mob_living;

        this.network_name = Dm.str(network_name);
        this.chat_color = chat_color;
        this.speech_action_icon = speech_action_icon;
        this.speech_action_icon_state = Dm.str(speech_action_icon_state);
        this.speech_action_background_icon_state = Dm.str(speech_action_background_icon_state);

        if (islist(signals_which_destroy_us))
            this.signals_which_destroy_us = signals_which_destroy_us as DmList;
        if (post_unlink_callback != null)
            this.post_unlink_callback = post_unlink_callback;
        if (show_balloon_alert != null)
            this.show_balloon_alert = Dm.num(show_balloon_alert);

        master_speech = Dm.@new<datum_action_innate_linked_speech>(this);
        await master_speech.Grant(owner);
        return null;
    }

    // tg: code/datums/components/mind_linker.dm:67
    public override object Destroy(float? force = null)
    {
        foreach (mob_living remaining_mob in Dm.each_cast<mob_living>(linked_mobs))
            unlink_mob(remaining_mob);
        linked_mobs.Cut();
        QDEL_NULL(ref master_speech);
        post_unlink_callback = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/mind_linker.dm:75
    public override async DmTask RegisterWithParent()
    {
        if (Dm.truthy(signals_which_destroy_us))
            RegisterSignals(parent, signals_which_destroy_us, nameof(destroy_link));
    }

    // tg: code/datums/components/mind_linker.dm:79
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        if (Dm.truthy(signals_which_destroy_us))
            UnregisterSignal(parent, signals_which_destroy_us);
    }

    /**
     * Attempts to link [to_link] to our network, giving them a speech action.
     *
     * Returns TRUE if successful, FALSE otherwise
     */
    // tg: code/datums/components/mind_linker.dm:88
    public virtual object link_mob(mob_living to_link = null)
    {
        if (QDELETED(to_link) || to_link.stat == DEAD)
            return FALSE;
        if (Dm.truthy(linked_mobs[to_link]))
            return FALSE;

        mob_living owner = parent as mob_living;
        if (to_link == owner)
            return FALSE;

        datum_action_innate_linked_speech new_link = Dm.@new<datum_action_innate_linked_speech>(this);
        Waiting.NoSleep(new_link.Grant(to_link));

        linked_mobs[to_link] = new_link;
        RegisterSignals(to_link, Dm.list(COMSIG_LIVING_DEATH, COMSIG_QDELETING), nameof(sig_unlink_mob));

        return TRUE;
    }

    // tg: code/datums/components/mind_linker.dm:106
    [SignalHandler]
    public virtual void sig_unlink_mob(mob_living to_unlink = null)
    {
        unlink_mob(to_unlink);
    }

    /**
     * Unlinks [to_unlink] from our network, deleting their speech action
     * and cleaning up anything involved.
     *
     * Also invokes post_unlink_callback, if supplied.
     */
    // tg: code/datums/components/mind_linker.dm:117
    public virtual object unlink_mob(mob_living to_unlink = null)
    {
        if (!Dm.truthy(linked_mobs[to_unlink]))
            return FALSE;

        if (post_unlink_callback != null)
            Waiting.NoSleep(post_unlink_callback.Invoke(to_unlink));

        UnregisterSignal(to_unlink, Dm.list(COMSIG_LIVING_DEATH, COMSIG_QDELETING));

        datum_action_innate_linked_speech old_link = linked_mobs[to_unlink] as datum_action_innate_linked_speech;
        Dm.remove(ref linked_mobs, to_unlink);
        qdel(old_link);
        return TRUE;
    }

    /**
     *  Signal proc sent from any signals given to us initialize.
     *  Destroys our component and unlinks everyone.
     */
    // tg: code/datums/components/mind_linker.dm:134
    [SignalHandler]
    public virtual void destroy_link(datum source = null)
    {
        if (isliving(source))
        {
            mob_living owner = source as mob_living;
            to_chat(owner, span_boldwarning(I18n.T("Your [] breaks!", network_name)));
        }

        qdel(this);
    }
}

/// Subtype of mind linker (I know) which is more active rather than passive,
/// which involves the master linking people manually rather than people being added automatically.
// tg: code/datums/components/mind_linker.dm:147
public partial class datum_component_mind_linker_active_linking : datum_component_mind_linker
{
    // tg: code/datums/components/mind_linker.dm:153
    [DmParams("network_name", "chat_color", "speech_action_icon", "speech_action_icon_state", "speech_action_background_icon_state", "speech_action_overlay_state", "signals_which_destroy_us", "post_unlink_callback", "show_balloon_alert", "link_message", "unlink_message", "linker_action_path")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object link_message = Dm.arg(__extra, 9);
        object unlink_message = Dm.arg(__extra, 10);
        // Required for this subtype
        object linker_action_path = Dm.arg(__extra, 11);

        object dot = await base.Initialize(__extra);
        if (Dm.eq(dot, COMPONENT_INCOMPATIBLE))
            return dot;

        mob_living owner = parent as mob_living;
        this.link_message = Dm.truthy(link_message) ? link_message : I18n.T("You are now connected to []'s [].", owner.real_name, network_name);
        this.unlink_message = Dm.truthy(unlink_message) ? unlink_message : I18n.T("You are no longer connected to []'s [].", owner.real_name, network_name);

        if (linker_action_path is Type linker_action_type)
        {
            linker_action = Dm.@new(linker_action_type, this) as datum_action;
            await linker_action.Grant(owner);
        }
        else
            stack_trace(Dm.text("[] was created without a valid linker_action_path. No one will be able to link to it.", type), "code/datums/components/mind_linker.dm", 177);

        to_chat(owner, span_boldnotice(I18n.T("You establish a [], allowing you to link minds to communicate telepathically.", network_name)));
        return dot;
    }

    // tg: code/datums/components/mind_linker.dm:188
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref linker_action);
        return base.Destroy(force);
    }

    // tg: code/datums/components/mind_linker.dm:192
    public override object link_mob(mob_living to_link = null)
    {
        if (HAS_MIND_TRAIT(to_link, TRAIT_UNCONVERTABLE)) // Protected mind, so they can't be added to the mindlink
            return FALSE;
        if (Dm.truthy(to_link.can_block_magic(MAGIC_RESISTANCE_MIND, charge_cost: 0)))
            return FALSE;

        object dot = base.link_mob(to_link);
        if (!Dm.truthy(dot))
            return dot;

        RegisterSignal(to_link, COMSIG_MINDSHIELD_IMPLANTED, nameof(sig_unlink_mob));
        mob_living owner = parent as mob_living;
        to_chat(to_link, span_notice(Dm.str(link_message)));
        to_chat(owner, span_notice(I18n.T("You connect []'s mind to your [].", to_link, network_name)));
        foreach (mob_living other_link in Dm.each_cast<mob_living>(linked_mobs))
            to_chat(other_link, span_notice(I18n.T("You feel a new presence within []'s [].", owner.real_name, network_name)));
        return dot;
    }

    // tg: code/datums/components/mind_linker.dm:209
    public override object unlink_mob(mob_living to_unlink = null)
    {
        object dot = base.unlink_mob(to_unlink);
        if (!Dm.truthy(dot))
            return dot;

        UnregisterSignal(to_unlink, COMSIG_MINDSHIELD_IMPLANTED);
        mob_living owner = parent as mob_living;
        to_chat(to_unlink, span_warning(Dm.str(unlink_message)));
        to_chat(owner, span_warning(I18n.T("You feel someone disconnect from your [].", network_name)));
        foreach (mob_living other_link in Dm.each_cast<mob_living>(linked_mobs))
            to_chat(other_link, span_warning(I18n.T("You feel a pressence disappear from []'s [].", owner.real_name, network_name)));
        return dot;
    }
}

// Used in mind linker to talk to everyone in the network.
// tg: code/datums/components/mind_linker.dm:223
public partial class datum_action_innate_linked_speech : datum_action_innate
{
    // tg: code/datums/components/mind_linker.dm:230
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        object Target = Dm.arg(__extra, 0);
        await base.New(__extra);
        if (!istype(Target, typeof(datum_component_mind_linker)))
        {
            stack_trace(Dm.text("[] ([]) was instantiated on a non-mind_linker target, this doesn't work.", name, type), "code/datums/components/mind_linker.dm", 233);
            qdel(this);
            return;
        }

        datum_component_mind_linker linker = Target as datum_component_mind_linker;
        name = I18n.T("[] Speech", linker.network_name);
        desc = I18n.T("Send a psychic message to everyone connected to your [].", linker.network_name);
        button_icon = linker.speech_action_icon;
        button_icon_state = linker.speech_action_icon_state;
        background_icon_state = linker.speech_action_background_icon_state;
    }

    // tg: code/datums/components/mind_linker.dm:243
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        return Dm.truthy(await base.IsAvailable(feedback)) && owner.stat != DEAD ? TRUE : FALSE;
    }

    // tg: code/datums/components/mind_linker.dm:246
    public override async DmTask<object> Activate()
    {
        datum_component_mind_linker linker = target as datum_component_mind_linker;
        mob_living linker_parent = linker.parent as mob_living;

        object message = await tgui_input_text(owner, I18n.T("Enter a message to transmit."), I18n.T("[] Telepathy", linker.network_name), max_length: MAX_MESSAGE_LEN);
        if (!Dm.truthy(message) || QDELETED(this) || QDELETED(owner) || owner.stat == DEAD)
            return null;

        if (QDELETED(linker))
        {
            to_chat(owner, span_warning(I18n.T("The link seems to have been severed.")));
            return null;
        }

        string formatted_message = Dm.text("<i><font color=[]>\\[[]'s []\\] <b>[]:</b> []</font></i>", linker.chat_color, linker_parent.real_name, linker.network_name, owner, message);
        log_directed_talk(owner, linker_parent, message, LOG_SAY, Dm.text("mind link ([])", linker.network_name));

        DmList all_who_can_hear = Dm.add(assoc_to_keys(linker.linked_mobs) as DmList, linker_parent) as DmList;

        foreach (mob_living recipient in Dm.each_cast<mob_living>(all_who_can_hear))
        {
            float avoid_highlighting = recipient == owner || recipient == linker_parent ? TRUE : FALSE;
            to_chat(recipient, formatted_message, type: MESSAGE_TYPE_RADIO, avoid_highlighting: avoid_highlighting);
            if (Dm.truthy(linker.show_balloon_alert) && recipient != owner)
                recipient.balloon_alert(recipient, I18n.T("you hear a voice from your []", linker.network_name));
        }

        foreach (object recipient in Dm.each(GLOB.dead_mob_list))
            to_chat(recipient, Dm.text("[] []", FOLLOW_LINK(recipient, owner), formatted_message), type: MESSAGE_TYPE_RADIO);
        return null;
    }
}
