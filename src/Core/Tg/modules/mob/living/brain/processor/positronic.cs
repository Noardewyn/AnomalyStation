// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/brain/processor/positronic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326): the positronic brain a ghost can enter, for cyborgs and the AI. The var block is generated
// (D111).

// tg: code/modules/mob/living/brain/processor/positronic.dm:3
public partial class obj_item_brain_processor_positronic : obj_item_brain_processor
{
    private const float POSIBRAIN_NAG_COOLDOWN = 60 * SECONDS;

    /// Time before pinging ghosts will also play a sound (STATIC_COOLDOWN_DECLARE: one cooldown for every posibrain)
    private static ref float ghost_nag_cooldown => ref TgState.Current.obj_item_brain_processor_positronic__ghost_nag_cooldown;

    // tg: code/modules/mob/living/brain/processor/positronic.dm:25
    [DmParams("mapload", "autoping")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object autoping = Dm.arg(__extra, 0) ?? TRUE;
        object dot = base.Initialize(mapload, __extra);
        // THIS brainmob is the only brainmob that we should ever use for all of our existance
        // If the brainmob becomes null or gets replaced, this is invalid behaviour
        set_brainmob(Dm.@new<mob_living_brain>(this));
        brainmob.grant_language(typeof(datum_language_machine), source: LANGUAGE_ATOM);

        object new_name = pick(GLOB.posibrain_names);
        string new_id = num2text(rand(1, 999), 3, 10);
        set_name(Dm.text("[]-[]", new_name, new_id));
        if (Dm.truthy(autoping))
            ping_ghosts("created", TRUE);
        return dot;
    }

    // tg: code/modules/mob/living/brain/processor/positronic.dm:38
    public override object Destroy(float? force = null)
    {
        stop_requesting_ghost();
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/brain/processor/positronic.dm:42
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        string suffix = null;
        if (Dm.truthy(is_searching()))
            suffix = "searching";
        else if (Dm.truthy(brainmob.ckey))
            suffix = "occupied";

        icon_state = Dm.text("[][]", base_icon_state, suffix != null ? Dm.text("-[]", suffix) : null);
        return dot;
    }

    // tg: code/modules/mob/living/brain/processor/positronic.dm:52
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(brainmob.key))
        {
            if (brainmob.stat >= DEAD)
                Dm.append(ref dot, span_deadsay(I18n.T("It appears to be completely inactive.")));
            else if (brainmob.client == null)
                Dm.append(ref dot, I18n.T("It appears to be in stand-by mode.")); //afk
            return dot;
        }

        if (Dm.truthy(is_searching()))
            Dm.append(ref dot, span_info(I18n.T("It's emitting light in a steady pulse.")));
        else
            Dm.append(ref dot, span_deadsay(I18n.T("It appears to be completely inactive. The reset light is blinking.")));

        if (Dm.truthy(requested_personality) && (isobserver(user) || Dm.truthy(user.is_holding(this))))
            Dm.append(ref dot, span_notice(I18n.T("Current consciousness seed: \"[]\"", requested_personality)));
        Dm.append(ref dot, span_boldnotice(I18n.T("Alt-click to set a consciousness seed, specifying what [] will be used for. This can help generate a personality interested in that role.", this)));
        return dot;
    }

    // tg: code/modules/mob/living/brain/processor/positronic.dm:70
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        object input_seed = await tgui_input_text(user, I18n.T("Enter a personality seed:"), I18n.T("Enter seed"), html_decode(requested_personality), max_length: MAX_NAME_LEN);

        if (!Dm.truthy(await user.can_perform_action(this)) || input_seed == null)
            return CLICK_ACTION_BLOCKING;

        if (!Dm.truthy(input_seed))
        {
            to_chat(user, span_notice(I18n.T("You clear the personality seed.")));
            requested_personality = null;
        }
        else
        {
            to_chat(user, span_notice(I18n.T("You set the personality seed to \"[]\".", input_seed)));
            requested_personality = input_seed;
        }

        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/mob/living/brain/processor/positronic.dm:85
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        dot = TRUE;

        if (Dm.truthy(is_occupied()))
        {
            balloon_alert(user, I18n.T("already active!"));
            return dot;
        }
        if (Dm.truthy(is_searching()))
        {
            balloon_alert(user, I18n.T("already searching!"));
            return dot;
        }
        if (!Dm.truthy(Dm.bitand(GLOB.ghost_role_flags, GHOSTROLE_SILICONS)))
        {
            to_chat(user, span_warning(I18n.T("Central Command has outlawed posibrain sentience in this sector.")));
            return dot;
        }

        to_chat(user, span_notice(I18n.T("You press the manual activation button and start []'s boot process.", this)));
        start_requesting_ghost();
        return dot;
    }

    // tg: code/modules/mob/living/brain/processor/positronic.dm:104
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        if (Dm.truthy(is_occupied()))
            return null;
        if (Dm.truthy(LAZYACCESS(ckeys_entered, user.ckey)))
        {
            to_chat(user, span_warning(I18n.T("You cannot re-enter [] a second time!", this)));
            return null;
        }

        if (Dm.truthy(is_banned_from(user.ckey, ROLE_POSIBRAIN)))
        {
            to_chat(user, span_warning(I18n.T("You are currently [] from playing as []!", span_bold(I18n.T("BANNED")), ROLE_POSIBRAIN)));
            return null;
        }
        if (QDELETED(this) || QDELETED(user)) // is_banned_from is a sleeping proc
            return null;

        object posi_ask = await tgui_alert(user, I18n.T("Become \\a []?\nYou will longer be revivable and all past lives will be forgotten!", Dm.initial(this, "name")), I18n.T("Confirm"), Dm.list("Yes", "No"));
        if (!Dm.eq(posi_ask, "Yes") || QDELETED(this) || QDELETED(user))
            return null;
        if (Dm.truthy(is_occupied())) // check one more time...
        {
            Waiting.Detach(tgui_alert(user, I18n.T("[] was possessed before you could confirm.", this), I18n.T("Too late"), Dm.list(pick("Dang", "Aw shucks", "Aww", ":(")))); // INVOKE_ASYNC(GLOBAL_PROC, tgui_alert, ...)
            return null;
        }

        // now insert the ghost
        insert_ghost(user);
        object policy = get_policy(ROLE_POSIBRAIN);
        if (Dm.truthy(policy))
            to_chat(brainmob, policy);

        stop_requesting_ghost();
        visible_message(span_notice(I18n.T("[] pings as [] lights start flashing!", this, p_their())), null, span_hear(I18n.T("You hear something make a \"ping\" sound.")));
        playsound(this, Dm.resource("sound/machines/ping.ogg"), 15, TRUE);
        return null;
    }

    /// Notify ghosts that the posibrain is up for grabs.
    // tg: code/modules/mob/living/brain/processor/positronic.dm:135
    public virtual void ping_ghosts(object msg = null, object forced = null)
    {
        bool should_nag = !Dm.truthy(forced) && ghost_nag_cooldown <= world.time; // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm), inline

        notify_ghosts(
            I18n.T("[] [] in []! []", name, msg, get_area(this), Dm.truthy(requested_personality) ? I18n.T("Personality requested: \\[[]\\]", requested_personality) : ""),
            source: this,
            header: "Ghost in the Machine",
            click_interact: TRUE,
            ghost_sound: should_nag ? Dm.resource("sound/effects/ghost2.ogg") : null,
            ignore_key: POLL_IGNORE_POSIBRAIN,
            notify_flags: GHOST_NOTIFY_IGNORE_MAPLOAD,
            notify_volume: 75
        );

        if (should_nag)
            ghost_nag_cooldown = world.time + POSIBRAIN_NAG_COOLDOWN; // COOLDOWN_START, inline
    }

    /// Sends out a request for a ghost to possess this posibrain.
    // tg: code/modules/mob/living/brain/processor/positronic.dm:156
    public virtual void start_requesting_ghost()
    {
        ghost_ping_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(stop_requesting_ghost)), POSIBRAIN_NAG_COOLDOWN, TIMER_STOPPABLE, file: "code/modules/mob/living/brain/processor/positronic.dm", line: 163);
        update_appearance(UPDATE_ICON);

        ping_ghosts("requested", FALSE);
    }

    // tg: code/modules/mob/living/brain/processor/positronic.dm:165
    public virtual void stop_requesting_ghost()
    {
        deltimer(ghost_ping_timer);
        ghost_ping_timer = null;
        update_appearance(UPDATE_ICON);

        if (!Dm.truthy(brainmob.ckey)) // not an ideal spot for this but it works...
            visible_message(span_notice(I18n.T("[]'s lights fade away as [] slowly turn[] dormant...", this, p_they(), p_s())));
    }

    /// Forces a ghost into the posibrain.
    // tg: code/modules/mob/living/brain/processor/positronic.dm:177
    public virtual void insert_ghost(mob_dead_observer user = null)
    {
        set_suicide(FALSE); // the old occupant may have suicided
        Waiting.NoSleep(brainmob.set_stat(STABLE));

        // Note that if we are currently possessed you will send the old ghost back to the lobby (and kick them if still logged in)
        brainmob.PossessByPlayer(user.ckey); // inits our mind if relevant
        brainmob.mind.set_assigned_role(SSjob.get_job_type(posibrain_job_path) as datum_job);

        LAZYSET(ref ckeys_entered, brainmob.ckey, TRUE);
    }

    // tg: code/modules/mob/living/brain/processor/positronic.dm:187
    public virtual object is_occupied()
    {
        if (Dm.truthy(brainmob.key))
            return TRUE;
        if ((loc as mob_living_silicon_robot)?.mmi == this)
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/mob/living/brain/processor/positronic.dm:194
    public virtual object is_searching()
    {
        return Dm.truthy(ghost_ping_timer) ? TRUE : FALSE; // lol
    }

    // tg: code/modules/mob/living/brain/processor/positronic.dm:197
    public override async DmTask transfer_identity(mob_living transferred_user = null)
    {
        await base.transfer_identity(transferred_user);
        // this is a little awkward...
        transferred_user.mind.set_assigned_role(SSjob.get_job_type(posibrain_job_path) as datum_job);
        await transferred_user.mind.remove_all_antag_datums();
        transferred_user.mind.wipe_memory();
        update_appearance(UPDATE_ICON);
    }
}

// tg: code/modules/mob/living/brain/processor/positronic.dm:205
public partial class obj_item_brain_processor_positronic_display : obj_item_brain_processor_positronic
{
    // tg: code/modules/mob/living/brain/processor/positronic.dm:209
    public override object is_occupied()
    {
        return TRUE;
    }
}

/// Posibrains but spherical. They can roll around and you can kick them
// tg: code/modules/mob/living/brain/processor/positronic.dm:213
public partial class obj_item_brain_processor_positronic_sphere : obj_item_brain_processor_positronic
{
    // tg: code/modules/mob/living/brain/processor/positronic.dm:225
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        matrix matrix = Dm.@new<matrix>();
        transform = matrix.Scale(0.8f, 0.8f);
        return dot;
    }

    // tg: code/modules/mob/living/brain/processor/positronic.dm:231
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        object dot = await base.relaymove(user, direction);
        if (isspaceturf(loc) || !Dm.truthy(direction))
            return dot;

        if (!(move_cooldown <= world.time)) // COOLDOWN_FINISHED, inline
            return dot;
        move_cooldown = world.time + Dm.num(move_delay); // COOLDOWN_START, inline

        // MOVE US
        if (isturf(loc))
        {
            await try_step_multiz(direction);
            SpinAnimation(Dm.num(move_delay), 1, Dm.eq(direction, NORTH) || Dm.eq(direction, EAST) ? TRUE : FALSE);
        }
        // ESCAPE PRISON
        else if (loc is obj_item_storage)
        {
            if (prob(25))
            {
                obj_item item = pick(loc.contents) as obj_item; // toss something out (which could be us!)
                await item.forceMove(loc.drop_location() as atom);
                playsound(this, SFX_RUSTLE, 30, TRUE);
            }
        }
        return dot;
    }

    // tg: code/modules/mob/living/brain/processor/positronic.dm:251
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (brainmob != null && isturf(loc))
        {
            anchored = TRUE; //anchor so we dont broom ourselves.
            await do_sweep(this, brainmob, loc, get_dir(old_loc, loc)); //movement dir doesnt work on objects
            anchored = FALSE;
        }
        return dot;
    }

    // tg: code/modules/mob/living/brain/processor/positronic.dm:258
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;

        visible_message(
            span_danger(I18n.T("[] punts [] hard, sending [] flying!", user, this, p_them())),
            span_userdanger(I18n.T("[] punts you like a football!", user)),
            span_danger(I18n.T("You hear something get hit!")));
        await user.do_attack_animation(this);
        playsound(this, SFX_PUNCH, 40, TRUE);

        await throw_at(get_edge_target_turf(this, get_dir(user, this)) as atom, 7, 1, user, TRUE);
        move_cooldown = world.time + Dm.num(move_delay); //pweeze stawp (COOLDOWN_START, inline)
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }
}

// tg: code/modules/mob/living/brain/processor/positronic.dm:16  (the proc's STATIC_COOLDOWN_DECLARE, rules 7.6)
public sealed partial class TgState
{
    internal float obj_item_brain_processor_positronic__ghost_nag_cooldown;
}
