// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/ghost_direct_control.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/__DEFINES/admin.dm:157  GLOBAL_VAR_INIT(ghost_role_flags, ALL)
    public float ghost_role_flags;

    public virtual void InitGlobalghost_role_flags()
    {
        ghost_role_flags = ALL;
        Dm.append(ref gvars_datum_init_order, "ghost_role_flags");
    }
}

// The whole file but the role bans (gaps package C3); the ghosts' poll since syndicate X2 (SSpolling).

/**
 * Component which lets ghosts click on a mob to take control of it
 */
// tg: code/datums/components/ghost_direct_control.dm:4
public partial class datum_component_ghost_direct_control : datum_component
{
    // tg: code/datums/components/ghost_direct_control.dm:18
    [DmParams("ban_type", "role_name", "poll_question", "poll_candidates", "poll_announce_chosen", "poll_length", "poll_chat_border_icon", "poll_ignore_key", "assumed_control_message", "extra_control_checks", "after_assumed_control", "joinable_mobs_title")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object ban_type = Dm.arg(__extra, 0) ?? ROLE_SENTIENCE;
        object role_name = Dm.arg(__extra, 1);
        object poll_question = Dm.arg(__extra, 2);
        object poll_candidates = Dm.arg(__extra, 3) ?? TRUE;
        object poll_announce_chosen = Dm.arg(__extra, 4) ?? TRUE;
        object poll_length = Dm.arg(__extra, 5) ?? 10 * SECONDS;
        object poll_chat_border_icon = Dm.arg(__extra, 6);
        object poll_ignore_key = Dm.arg(__extra, 7) ?? POLL_IGNORE_SENTIENCE_POTION;
        object assumed_control_message = Dm.arg(__extra, 8);
        datum_callback extra_control_checks = Dm.arg<datum_callback>(__extra, 9);
        datum_callback after_assumed_control = Dm.arg<datum_callback>(__extra, 10);
        object joinable_mobs_title = Dm.arg(__extra, 11);
        await base.Initialize(__extra);
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;

        this.ban_type = ban_type;
        this.assumed_control_message = assumed_control_message ?? Dm.text("You are []!", parent);
        this.extra_control_checks = extra_control_checks;
        this.after_assumed_control = after_assumed_control;
        this.joinable_mobs_title = joinable_mobs_title;

        mob mob_parent = (mob)parent;
        LAZYADDASSOCLIST(ref GLOB.joinable_mobs, joinable_mobs_title ?? format_text(Dm.initial(mob_parent, (mob p) => p.name)), mob_parent);

        if (Dm.truthy(poll_candidates))
            Waiting.Detach(request_ghost_control(poll_question, role_name ?? Dm.text("[]", parent), poll_length, poll_ignore_key, poll_announce_chosen, poll_chat_border_icon));
        return null;
    }

    // tg: code/datums/components/ghost_direct_control.dm:51
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_ATOM_ATTACK_GHOST, nameof(on_ghost_clicked));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examined));
        RegisterSignal(parent, COMSIG_MOB_LOGIN, nameof(on_login));
    }

    // tg: code/datums/components/ghost_direct_control.dm:57
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_ATOM_ATTACK_GHOST, COMSIG_ATOM_EXAMINE, COMSIG_MOB_LOGIN));
        await base.UnregisterFromParent(__extra);
    }

    // tg: code/datums/components/ghost_direct_control.dm:61
    public override object Destroy(float? force = null)
    {
        extra_control_checks = null;
        after_assumed_control = null;

        mob mob_parent = (mob)parent;
        LAZYREMOVEASSOC(ref GLOB.joinable_mobs, joinable_mobs_title ?? format_text(Dm.initial(mob_parent, (mob p) => p.name)), mob_parent);
        return base.Destroy(force);
    }

    /// Inform ghosts that they can possess this
    // tg: code/datums/components/ghost_direct_control.dm:70
    [SignalHandler]
    public virtual void on_examined(datum source = null, mob user = null, DmList examine_text = null)
    {
        if (!isobserver(user))
            return;
        mob_living our_mob = (mob_living)parent;
        if (our_mob.stat == DEAD || our_mob.key != null || Dm.truthy(awaiting_ghosts))
            return;
        Dm.append(ref examine_text, span_boldnotice("You could take control of this mob by clicking on it."));
    }

    /// Send out a request for a brain
    // tg: code/datums/components/ghost_direct_control.dm:79
    public virtual async DmTask request_ghost_control(object poll_question = null, object role_name = null, object poll_length = null, object poll_ignore_key = null, object poll_announce_chosen = null, object poll_chat_border_icon = null)
    {
        if (!Dm.truthy(Dm.bitand(GLOB.ghost_role_flags, GHOSTROLE_SPAWNER)))
            return;
        awaiting_ghosts = TRUE;
        mob chosen_one = await SSpolling.poll_ghosts_for_target(
            question: poll_question,
            check_jobban: ban_type,
            poll_time: poll_length == null ? null : Dm.num(poll_length),
            checked_target: parent as atom_movable,
            ignore_category: poll_ignore_key,
            alert_pic: parent,
            role_name_text: role_name,
            chat_text_border_icon: poll_chat_border_icon,
            announce_chosen: poll_announce_chosen == null ? null : Dm.num(poll_announce_chosen)
        ) as mob;
        awaiting_ghosts = FALSE;
        if (chosen_one == null)
            return;
        await assume_direct_control(chosen_one);
    }

    /// A ghost clicked on us, they want to get in this body
    // tg: code/datums/components/ghost_direct_control.dm:100
    [SignalHandler]
    public virtual object on_ghost_clicked(mob our_mob = null, mob_dead_observer hopeful_ghost = null)
    {
        if (our_mob.key != null)
        {
            qdel(this);
            return null;
        }
        if (hopeful_ghost.client == null)
            return null;
        if (!Dm.truthy(Dm.bitand(GLOB.ghost_role_flags, GHOSTROLE_SPAWNER)))
        {
            to_chat(hopeful_ghost, span_warning("Ghost roles have been temporarily disabled!"));
            return null;
        }
        if (Dm.truthy(awaiting_ghosts))
        {
            to_chat(hopeful_ghost, span_warning("Ghost candidate selection currently in progress!"));
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }
        if (!Dm.truthy(SSticker.HasRoundStarted()))
        {
            to_chat(hopeful_ghost, span_warning("You cannot assume control of this until after the round has started!"));
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }
        Waiting.Detach(attempt_possession(our_mob, hopeful_ghost));
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    /// We got far enough to establish that this mob is a valid target, let's try to posssess it
    // tg: code/datums/components/ghost_direct_control.dm:119
    public virtual async DmTask attempt_possession(mob our_mob = null, mob_dead_observer hopeful_ghost = null)
    {
        object ghost_asked = await tgui_alert(usr ?? hopeful_ghost, Dm.text("Become []?", our_mob), "Are you sure?", Dm.list("Yes", "No"));
        if (!Dm.eq(ghost_asked, "Yes") || QDELETED(our_mob))
            return;
        await assume_direct_control(hopeful_ghost);
    }

    /// Grant possession of our mob, component is now no longer required
    // tg: code/datums/components/ghost_direct_control.dm:126
    public virtual async DmTask assume_direct_control(mob harbinger = null)
    {
        if (QDELETED(this))
        {
            to_chat(harbinger, span_warning("Offer to possess creature has expired!"));
            return;
        }
        // is_banned_from: the role bans (admin tooling)
        if (!Dm.truthy(Dm.bitand(GLOB.ghost_role_flags, GHOSTROLE_SPAWNER)))
        {
            to_chat(harbinger, span_warning("Ghost roles have been temporarily disabled!"));
            return;
        }
        mob_living new_body = (mob_living)parent;
        if (new_body.stat == DEAD)
        {
            to_chat(harbinger, span_warning("This body has passed away, it is of no use!"));
            return;
        }
        if (new_body.key != null)
        {
            to_chat(harbinger, span_warning(Dm.text("[] has already become sapient!", parent)));
            qdel(this);
            return;
        }
        if (extra_control_checks != null && !Dm.truthy(await extra_control_checks.Invoke(harbinger)))
            return;

        // doesn't transfer mind because that transfers antag datum as well
        new_body.PossessByPlayer(harbinger.ckey);

        // Already qdels due to below proc but just in case
        qdel(this);
    }

    /// When someone assumes control, get rid of our component
    // tg: code/datums/components/ghost_direct_control.dm:153
    [SignalHandler]
    public virtual void on_login(mob harbinger = null)
    {
        // This proc is called the very moment .key is set, so we need to force mind to initialize here if we want the invoke to affect the mind of the mob
        if (harbinger.mind == null)
            Waiting.NoSleep(harbinger.mind_initialize());
        to_chat(harbinger, span_boldnotice(assumed_control_message as string));
        if (after_assumed_control != null)
            Waiting.Detach(after_assumed_control.Invoke(harbinger));
        qdel(this);
    }
}
