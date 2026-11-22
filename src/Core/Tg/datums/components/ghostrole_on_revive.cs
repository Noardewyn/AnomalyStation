// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/ghostrole_on_revive.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1: the recovered crew's body offered to a ghost when it is revived). The var block is generated (D111).

/// Proc ghosts to enter the body when it get's revived
// tg: code/datums/components/ghostrole_on_revive.dm:2
public partial class datum_component_ghostrole_on_revive : datum_component
{
    // tg: code/datums/components/ghostrole_on_revive.dm:18
    [DmParams("refuse_revival_if_failed", "on_successful_revive", "revive_title", "spawn_text", "you_are_text", "flavor_text", "important_text")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object refuse_revival_if_failed = Dm.arg(__extra, 0) ?? FALSE;
        datum_callback on_successful_revive = Dm.arg(__extra, 1) as datum_callback;
        object revive_title = Dm.arg(__extra, 2) ?? I18n.T("a recovered crewmember");
        object spawn_text = Dm.arg(__extra, 3) ?? I18n.T("Recovered Crew");
        object you_are_text = Dm.arg(__extra, 4) ?? I18n.T("You are a long dead crewmember, but are soon to be revived to rejoin the crew!");
        object flavor_text = Dm.arg(__extra, 5) ?? I18n.T("Get a job and get back to work!");
        object important_text = Dm.arg(__extra, 6) ?? I18n.T("Do your best to help the station. You still roll for midround antagonists.");
        object dot = await base.Initialize(__extra);
        this.refuse_revival_if_failed = refuse_revival_if_failed;
        this.on_successful_revive = on_successful_revive;
        this.revive_title = revive_title;
        this.spawn_text = spawn_text;
        this.you_are_text = you_are_text;
        this.flavor_text = flavor_text;
        this.important_text = important_text;

        if (ismob(parent))
        {
            await prepare_mob(parent as mob_living);
            return dot;
        }

        if (!istype(parent, typeof(obj_item_organ_brain)))
            return COMPONENT_INCOMPATIBLE;

        obj_item_organ_brain brain_parent = parent as obj_item_organ_brain;
        if (brain_parent.owner != null)
            await prepare_mob(brain_parent.owner);
        else
            prepare_brain(brain_parent);
        return dot;
    }

    /// Give the appropriate signals, and watch for organ removal
    // tg: code/datums/components/ghostrole_on_revive.dm:46
    public virtual async DmTask prepare_mob(mob_living to_prepare = null)
    {
        RegisterSignal(to_prepare, COMSIG_LIVING_REVIVE, nameof(on_revive));
        RegisterSignal(to_prepare, COMSIG_MOB_REAGENT_TICK, nameof(block_formaldehyde_metabolism));
        if (istype(parent, typeof(obj_item_organ_brain)))
            RegisterSignal(to_prepare, COMSIG_CARBON_LOSE_ORGAN, nameof(on_remove));
        ADD_TRAIT(to_prepare, TRAIT_GHOSTROLE_ON_REVIVE, REF(this));

        await add_orbit_twitching(to_prepare);
        to_prepare.med_hud_set_status();
    }

    // tg: code/datums/components/ghostrole_on_revive.dm:56
    public virtual void unprepare_mob(mob_living to_unprepare = null)
    {
        REMOVE_TRAIT(to_unprepare, TRAIT_GHOSTROLE_ON_REVIVE, REF(this));
        UnregisterSignal(to_unprepare, Dm.list(
            COMSIG_LIVING_REVIVE,
            COMSIG_MOB_REAGENT_TICK,
            COMSIG_CARBON_LOSE_ORGAN));

        to_unprepare.med_hud_set_status();
        remove_orbit_twitching(to_unprepare);
    }

    // tg: code/datums/components/ghostrole_on_revive.dm:67
    [SignalHandler]
    public virtual void on_remove(mob_living_carbon source = null, obj_item_organ removed_brain = null)
    {
        if (!istype(removed_brain, typeof(obj_item_organ_brain)))
            return;

        unprepare_mob(source);

        // we might have some lingering blinking eyes
        obj_item_bodypart_head head = source.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        if (head != null)
            Waiting.NoSleep(head.remove_bodypart_overlay(typeof(datum_bodypart_overlay_simple_soul_pending_eyes)));

        prepare_brain(removed_brain);
    }

    // tg: code/datums/components/ghostrole_on_revive.dm:81
    public virtual void prepare_brain(obj_item_organ source = null)
    {
        ADD_TRAIT(source, TRAIT_GHOSTROLE_ON_REVIVE, REF(this));
        RegisterSignal(source, COMSIG_ORGAN_IMPLANTED, nameof(prepare_mob_from_brain));
        RegisterSignal(source, COMSIG_ATOM_EXAMINE, nameof(brain_examine));
        UnregisterSignal(source, COMSIG_ORGAN_REMOVED);
    }

    // tg: code/datums/components/ghostrole_on_revive.dm:87
    public virtual void unprepare_brain(obj_item_organ source = null)
    {
        REMOVE_TRAIT(source, TRAIT_GHOSTROLE_ON_REVIVE, REF(this));
        UnregisterSignal(source, COMSIG_ORGAN_IMPLANTED);
        UnregisterSignal(source, COMSIG_ATOM_EXAMINE);
        RegisterSignal(source, COMSIG_ORGAN_REMOVED, nameof(prepare_brain));
        source.owner?.med_hud_set_status();
    }

    // tg: code/datums/components/ghostrole_on_revive.dm:94
    [SignalHandler]
    public virtual void prepare_mob_from_brain(obj_item_organ_brain source = null, mob_living owner = null)
    {
        REMOVE_TRAIT(source, TRAIT_GHOSTROLE_ON_REVIVE, REF(this));
        UnregisterSignal(source, COMSIG_ORGAN_IMPLANTED);
        Waiting.NoSleep(prepare_mob(owner));
    }

    // tg: code/datums/components/ghostrole_on_revive.dm:101
    [SignalHandler]
    public virtual void brain_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(examine_list, span_info(I18n.T("Another soul may take [] place if put in a body...", ((atom)source).p_their())));
    }

    // tg: code/datums/components/ghostrole_on_revive.dm:106
    [SignalHandler]
    public virtual void on_revive(mob_living source = null)
    {
        Waiting.Detach(poll_ghosts(source)); // INVOKE_ASYNC
    }

    // tg: code/datums/components/ghostrole_on_revive.dm:111
    public virtual async DmTask poll_ghosts(mob_living reviving = null)
    {
        datum_bodypart_overlay_simple_soul_pending_eyes soul_eyes = null;
        // adds soulful SOUL PENDING eyes to indicate what's happening to observers
        obj_item_bodypart_head head = reviving.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        if (head != null)
        {
            soul_eyes = Dm.@new<datum_bodypart_overlay_simple_soul_pending_eyes>();
            await head.add_bodypart_overlay(soul_eyes);
        }

        DmList jobbans = Dm.list(ROLE_RECOVERED_CREW);
        if (reviving.mind != null)
        {
            Dm.combine(jobbans, reviving.mind.assigned_role.title);
            foreach (datum_antagonist antag in Dm.each_cast<datum_antagonist>(reviving.mind.antag_datums))
            {
                if (Dm.truthy(antag.jobban_flag))
                    Dm.combine(jobbans, antag.jobban_flag);
                if (Dm.truthy(antag.pref_flag))
                    Dm.combine(jobbans, antag.pref_flag);
                if (!Dm.truthy(Dm.bitand(antag.antag_flags, ANTAG_FAKE)))
                    Dm.combine(jobbans, ROLE_SYNDICATE);
            }
        }

        mob_dead_observer chosen_one = await SSpolling.poll_ghosts_for_target(
            question: I18n.T("Would you like to play as []?", revive_title),
            check_jobban: jobbans,
            poll_time: 15 * SECONDS,
            checked_target: reviving,
            ignore_category: POLL_IGNORE_RECOVERED_CREW,
            alert_pic: reviving,
            role_name_text: revive_title) as mob_dead_observer;

        if (head != null)
            await head.remove_bodypart_overlay(soul_eyes);
        if (soul_eyes != null)
            qdel(soul_eyes);

        if (isobserver(chosen_one))
        {
            reviving.PossessByPlayer(chosen_one.ckey);
            if (on_successful_revive != null)
                await on_successful_revive.Invoke(reviving);
            qdel(this);
        }
        else if (Dm.truthy(refuse_revival_if_failed))
        {
            await reviving.death();
            reviving.visible_message(span_deadsay(I18n.T("[]'s soul is struggling to return!", reviving)));
        }
    }

    // tg: code/datums/components/ghostrole_on_revive.dm:157
    public virtual async DmTask add_orbit_twitching(mob_living parent_mob = null)
    {
        await parent_mob.AddElement(typeof(datum_element_orbit_twitcher), twitch_chance);
        // Add it to the ghostrole spawner menu. Note that we can't directly spawn from it, but we can make it twitch to alert bystanders to defib it
        LAZYADDASSOCLIST(ref GLOB.joinable_mobs, spawn_text, parent_mob);
        RegisterSignal(parent_mob, COMSIG_LIVING_GHOSTROLE_INFO, nameof(set_spawner_info));
    }

    // tg: code/datums/components/ghostrole_on_revive.dm:164
    [SignalHandler]
    public virtual void set_spawner_info(datum_spawners_menu menu = null, DmList string_info = null)
    {
        string_info["you_are_text"] = this.you_are_text;
        string_info["flavor_text"] = this.flavor_text;
        string_info["important_text"] = this.important_text;
    }

    // tg: code/datums/components/ghostrole_on_revive.dm:171
    public virtual void remove_orbit_twitching(mob_living parent_mob = null)
    {
        parent_mob.RemoveElement(typeof(datum_element_orbit_twitcher), twitch_chance);
        LAZYREMOVEASSOC(ref GLOB.joinable_mobs, spawn_text, parent_mob);
        UnregisterSignal(parent_mob, COMSIG_LIVING_GHOSTROLE_INFO);
    }

    // Block formaldehyde from being metabolized, Coroner QoL
    // tg: code/datums/components/ghostrole_on_revive.dm:177
    [SignalHandler]
    public virtual object block_formaldehyde_metabolism(mob_living source = null, datum_reagent chem = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        if (istype(chem, typeof(datum_reagent_toxin_formaldehyde)))
            return COMSIG_MOB_STOP_REAGENT_TICK;
        return null;
    }

    // tg: code/datums/components/ghostrole_on_revive.dm:183
    public override object Destroy(float? force = null)
    {
        if (isliving(parent))
            unprepare_mob(parent as mob_living);

        else if (istype(parent, typeof(obj_item_organ_brain)))
        {
            obj_item_organ_brain brain = parent as obj_item_organ_brain;
            if (brain.owner != null)
                unprepare_mob(brain.owner);
            else
                unprepare_brain(brain);
        }

        return base.Destroy(force);
    }
}
