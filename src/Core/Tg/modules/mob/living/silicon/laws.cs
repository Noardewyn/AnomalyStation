// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/laws.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/mob/living/silicon/laws.dm:6
public partial class mob_living_silicon : mob_living
{
    /**
     * Shows the silicon's current laws in chat.
     *
     * You can optionally pass a mob to show to instead of the silicon itself.
     */
    // tg: code/modules/mob/living/silicon/laws.dm:6
    public virtual async DmTask show_laws(mob show_to = null)
    {
        DmList law_box = Dm.list(span_bold(I18n.T("Obey these laws:")));
        Dm.append(law_box, laws.get_law_list(include_zeroth: TRUE));
        to_chat(show_to ?? this, boxed_message(jointext(law_box, "\n")));
    }

    /**
     * Logs the silicon's current laws to the server log.
     */
    // tg: code/modules/mob/living/silicon/laws.dm:14
    public virtual void log_current_laws()
    {
        DmList the_laws = laws.get_law_list(include_zeroth: TRUE) as DmList;
        object lawtext = the_laws.Join(" ");
        log_silicon(Dm.text("LAW: [] spawned with []", key_name(this), lawtext));
    }

    /**
     * Notify's dead chat that a silicon's laws (may) have changed, with a link to view the new laws.
     */
    // tg: code/modules/mob/living/silicon/laws.dm:22
    public virtual void deadchat_lawchange()
    {
        if (!Dm.truthy(SSticker.HasRoundStarted()))
            return;
        DmList the_laws = laws.get_law_list(include_zeroth: TRUE) as DmList;
        object lawtext = the_laws.Join("<br/>");
        deadchat_broadcast(Dm.text("'s <b>laws were changed.</b> <a href='byond://?src=[]&dead=1&printlawtext=[]'>View</a>", REF(this), url_encode(lawtext)), span_name(Dm.text("[]", this)), follow_target: this, message_type: DEADCHAT_LAWCHANGE);
    }

    /**
     * Throws a screen alert indicating that the silicon's laws have changed
     *
     * If announce is TRUE, also sends a chat message to the silicon and plays a sound,
     * followed by printing the new laws to the silicon's chat and informing deadchat of the law change.
     */
    // tg: code/modules/mob/living/silicon/laws.dm:35
    public virtual void announce_law_change(float? announce = null)
    {
        announce ??= TRUE;
        throw_alert(ALERT_NEW_LAW, typeof(atom_movable_screen_alert_newlaw));
        if (Dm.truthy(announce) && last_lawchange_announce != world.time)
        {
            to_chat(this, span_bolddanger(I18n.T("Your laws have been changed.")));
            Dm.output_to(this, sound(Dm.resource("sound/machines/cryo_warning.ogg"))); // SEND_SOUND (__DEFINES/logging.dm, rules 12)
            // lawset modules cause this function to be executed multiple times in a tick, so we wait for the next tick in order to be able to see the entire lawset
            _addtimer(Dm.@new<datum_callback>(this, nameof(show_laws)), 0, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/modules/mob/living/silicon/laws.dm", line: 41);
            _addtimer(Dm.@new<datum_callback>(this, nameof(deadchat_lawchange)), 0, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/modules/mob/living/silicon/laws.dm", line: 42);
            last_lawchange_announce = world.time;
        }
    }

    // tg: code/modules/mob/living/silicon/laws.dm:45
    public virtual void make_laws()
    {
        laws = Dm.@new<datum_ai_laws>();
        laws.name = "Inherent Laws";
    }

    /// Find the first law rack we can link to and link to it
    // tg: code/modules/mob/living/silicon/laws.dm:50
    public virtual async DmTask<object> link_to_first_rack()
    {
        foreach (obj_machinery_ai_law_rack_base_core law_rack in Dm.each(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_ai_law_rack_base_core))))
        {
            if (!Dm.truthy(law_rack.can_link_to(this)))
                continue;

            await law_rack.link_silicon(this, announce: FALSE);
            return law_rack;
        }
        return null;
    }

    /// Returns the law rack this silicon is linked to, or null if not linked.
    // tg: code/modules/mob/living/silicon/laws.dm:59
    public virtual object get_law_rack()
    {
        foreach (obj_machinery_ai_law_rack_base rack in Dm.each(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_ai_law_rack_base))))
        {
            foreach (object name in Dm.each(rack.linked_mobs))
            {
                if (rack.linked_mobs[name] == this)
                    return rack;
            }
        }
        return null;
    }

    /// Unlinks the silicon from the law rack, if it is linked.
    // tg: code/modules/mob/living/silicon/laws.dm:67
    public virtual async DmTask unlink_from_law_rack()
    {
        obj_machinery_ai_law_rack_base rack = get_law_rack() as obj_machinery_ai_law_rack_base;
        if (rack != null)
            await rack.unlink_silicon(this);
    }

    /**
     * When given a typepath to a law datum, replaces the silicon's current law set with a new one of that type.
     *
     * Before replacing the lawset, unlinks the silicon from any law rack
     *
     * Handles informing the silicon of the law change + syncing borgs for AIs
     */
    // tg: code/modules/mob/living/silicon/laws.dm:77
    public virtual async DmTask replace_law_set(object new_law_type = null)
    {
        await unlink_from_law_rack();
        laws = Dm.@new((Type)new_law_type) as datum_ai_laws;
        announce_law_change();
    }
}

// tg: code/modules/mob/living/silicon/laws.dm:82
public partial class mob_living_silicon_ai : mob_living_silicon
{
    // tg: code/modules/mob/living/silicon/laws.dm:82
    public override async DmTask replace_law_set(object new_law_type = null)
    {
        await base.replace_law_set(new_law_type);
        await try_sync_laws();
    }
}
