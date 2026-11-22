// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2 and SI4, D326): the AI's clicks, the atom roots of its shortcuts and the airlocks', APCs', turret
// controls' and holopads' AI shortcuts.

// tg: code/_onclick/ai.dm:104
public partial class atom
{
    // tg: code/_onclick/ai.dm:104
    public virtual async DmTask<object> attack_ai(mob user = null)
    {
        return null;
    }

    /**
     * What happens when the AI holds right-click on an item. Returns a SECONDARY_ATTACK_* value.
     *
     * Arguments:
     * * user The mob holding the right click
     * * modifiers The list of the custom click modifiers
     */
    // tg: code/_onclick/ai.dm:114
    public virtual async DmTask<object> attack_ai_secondary(mob user = null, DmList modifiers = null)
    {
        return SECONDARY_ATTACK_CALL_NORMAL;
    }

    /*
        The following criminally helpful code is just the previous code cleaned up;
        I have no idea why it was in atoms.dm instead of respective files.
    */
    /* Questions: Instead of an Emag check on every function, can we not add to airlocks onclick if emag return? */

    /* Atom Procs */
    // tg: code/_onclick/ai.dm:160
    public virtual async DmTask AICtrlClick(mob_living_silicon user = null)
    {
        return;
    }

    // tg: code/_onclick/ai.dm:163
    public virtual async DmTask<object> ai_click_alt(mob_living_silicon user = null)
    {
        return null;
    }

    // tg: code/_onclick/ai.dm:167
    public virtual async DmTask<object> AIShiftClick(mob_living_silicon user = null)
    {
        return FALSE;
    }

    // tg: code/_onclick/ai.dm:170
    public virtual async DmTask AICtrlShiftClick(mob_living_silicon user = null)
    {
        return;
    }
}

/* Airlocks */
// tg: code/_onclick/ai.dm:174
public partial class obj_machinery_door_airlock : obj_machinery_door
{
    // tg: code/_onclick/ai.dm:174
    public override async DmTask AICtrlClick(mob_living_silicon user = null) // Bolts doors
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return;
        toggle_bolt(user);
        add_hiddenprint(user);
    }

    // tg: code/_onclick/ai.dm:181
    public override async DmTask<object> ai_click_alt(mob_living_silicon user = null)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return NONE;
        if (!Dm.truthy(secondsElectrified))
            await shock_perm(user);
        else
            await shock_restore(user);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/_onclick/ai.dm:191
    public override async DmTask<object> AIShiftClick(mob_living_silicon user = null) // Opens and closes doors!
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        await user_toggle_open(user);
        add_hiddenprint(user);
        return TRUE;
    }

    // tg: code/_onclick/ai.dm:199
    public override async DmTask AICtrlShiftClick(mob_living_silicon user = null) // Sets/Unsets Emergency Access Override
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return;
        toggle_emergency(user);
        add_hiddenprint(user);
    }
}

/////////////
/*   APC   */
/////////////

// tg: code/_onclick/ai.dm:211
public partial class obj_machinery_power_apc : obj_machinery_power
{
    /// Toggle APC power settings
    // tg: code/_onclick/ai.dm:211
    public override async DmTask AICtrlClick(mob_living_silicon user = null)
    {
        if (!Dm.truthy(can_use(user, loud: TRUE)))
            return;

        await toggle_breaker(user);
    }

    /// Toggle APC environment settings (atmos)
    // tg: code/_onclick/ai.dm:218
    public override async DmTask AICtrlShiftClick(mob_living_silicon user = null)
    {
        if (!Dm.truthy(can_use(user, loud: TRUE)))
            return;

        if (!Dm.truthy(is_operational) || Dm.truthy(failure_timer))
            return;

        environ = Dm.truthy(environ) ? APC_CHANNEL_OFF : APC_CHANNEL_ON;
        if (user != null)
        {
            add_hiddenprint(user);
            string enabled_or_disabled = Dm.truthy(environ) ? "enabled" : "disabled";
            balloon_alert(user, I18n.T("environment power []", enabled_or_disabled));
            user.log_message(Dm.text("[] the [] environment settings", enabled_or_disabled, this), LOG_GAME);
        }
        update_appearance();
        update();
    }

    /// Toggle APC lighting settings
    // tg: code/_onclick/ai.dm:235
    public override async DmTask<object> AIShiftClick(mob_living_silicon user = null)
    {
        if (!Dm.truthy(can_use(user, loud: TRUE)))
            return FALSE;

        if (!Dm.truthy(is_operational) || Dm.truthy(failure_timer))
            return FALSE;

        lighting = Dm.truthy(lighting) ? APC_CHANNEL_OFF : APC_CHANNEL_ON;
        if (user != null)
        {
            string enabled_or_disabled = Dm.truthy(lighting) ? "enabled" : "disabled";
            add_hiddenprint(user);
            balloon_alert(user, I18n.T("lighting power toggled []", enabled_or_disabled));
            user.log_message(Dm.text("turned [] the [] lighting settings", enabled_or_disabled, this), LOG_GAME);
        }
        update_appearance();
        update();
        return TRUE;
    }

    /// Toggle APC equipment settings
    // tg: code/_onclick/ai.dm:253
    public override async DmTask<object> ai_click_alt(mob_living_silicon user = null)
    {
        if (!Dm.truthy(can_use(user, loud: TRUE)))
            return NONE;

        if (!Dm.truthy(is_operational) || Dm.truthy(failure_timer))
            return CLICK_ACTION_BLOCKING;

        equipment = Dm.truthy(equipment) ? APC_CHANNEL_OFF : APC_CHANNEL_ON;
        if (user != null)
        {
            string enabled_or_disabled = Dm.truthy(equipment) ? "enabled" : "disabled";
            balloon_alert(user, I18n.T("equipment power toggled []", enabled_or_disabled));
            add_hiddenprint(user);
            user.log_message(Dm.text("turned [] the [] equipment settings", enabled_or_disabled, this), LOG_GAME);
        }
        update_appearance();
        update();
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/_onclick/ai.dm:270
    public override async DmTask<object> attack_ai_secondary(mob user = null, DmList modifiers = null)
    {
        if (Dm.truthy(can_use(user, loud: TRUE)))
            togglelock(user as mob_living);

        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }
}

/* AI Turrets */
// tg: code/_onclick/ai.dm:285
public partial class obj_machinery_turretid : obj_machinery
{
    // tg: code/_onclick/ai.dm:285
    public override async DmTask<object> ai_click_alt(mob_living_silicon user = null) //toggles lethal on turrets
    {
        if (Dm.truthy(is_ai_locked(user)))
            return CLICK_ACTION_BLOCKING;
        toggle_lethal(user);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/_onclick/ai.dm:291
    public override async DmTask AICtrlClick(mob_living_silicon user = null) //turns off/on Turrets
    {
        if (Dm.truthy(is_ai_locked(user)))
            return;
        toggle_on(user);
    }
}

/*
	AI ClickOn()

	The AI can double click to move the camera (this was already true but is cleaner),
	or double click a mob to track them.

	Note that AI have no need for the adjacency proc, and so this proc is a lot cleaner.
*/
// tg: code/_onclick/ai.dm:9
public partial class mob_living_silicon_ai : mob_living_silicon
{
    // tg: code/_onclick/ai.dm:9
    public override async DmTask DblClickOn(atom A = null, object @params = null)
    {
        if (Dm.truthy(control_disabled) || Dm.truthy(incapacitated))
            return;

        if (ismob(A))
            ai_tracking_tool.track_mob(this, A as mob_living);
        else
            A.move_camera_by_click();
        await DmTask.CompletedTask;
    }

    // tg: code/_onclick/ai.dm:18
    public override async DmTask ClickOn(atom A = null, object @params = null)
    {
        if (world.time <= next_click)
            return;
        next_click = world.time + 1;

        DmList modifiers = params2list(@params);

        if (Dm.truthy(_listen_lookup?[COMSIG_MOB_CLICKON]) && Dm.truthy(Dm.bitand(_SendSignal(COMSIG_MOB_CLICKON, Dm.list(this, A, modifiers)), COMSIG_MOB_CANCEL_CLICKON)))
            return;

        if (!Dm.truthy(await can_interact_with(A)))
            return;

        if (Dm.truthy(multicam_on))
        {
            turf T = get_turf(A);
            if (T != null)
            {
                foreach (atom_movable_screen_movable_pic_in_pic_ai P in Dm.each<atom_movable_screen_movable_pic_in_pic_ai>(T.vis_locs))
                {
                    if (P.ai == this)
                    {
                        await P.Click(@params);
                        break;
                    }
                }
            }
        }

        if (Dm.truthy(await check_click_intercept(@params, A)))
            return;

        if (Dm.truthy(control_disabled) || Dm.truthy(incapacitated))
            return;

        object pixel_turf = get_turf_pixel(A);
        if (pixel_turf == null)
            return;
        if (!Dm.truthy(can_see(A)))
            return;

        if (Dm.truthy(LAZYACCESS(modifiers, SHIFT_CLICK)))
        {
            if (Dm.truthy(LAZYACCESS(modifiers, CTRL_CLICK)))
            {
                await CtrlShiftClickOn(A);
                return;
            }
            if (Dm.truthy(LAZYACCESS(modifiers, MIDDLE_CLICK)))
            {
                await ShiftMiddleClickOn(A);
                return;
            }
            await ShiftClickOn(A);
            return;
        }
        if (Dm.truthy(LAZYACCESS(modifiers, ALT_CLICK))) // alt and alt-gr (rightalt)
        {
            await ai_base_click_alt(A);
            return;
        }
        if (Dm.truthy(LAZYACCESS(modifiers, CTRL_CLICK)))
        {
            await CtrlClickOn(A);
            return;
        }
        if (Dm.truthy(LAZYACCESS(modifiers, MIDDLE_CLICK)))
        {
            MiddleClickOn(A, @params);
            return;
        }
        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
        {
            object secondary_result = await A.attack_ai_secondary(this, modifiers);
            if (Dm.eq(secondary_result, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN) || Dm.eq(secondary_result, SECONDARY_ATTACK_CONTINUE_CHAIN))
                return;
            else if (!Dm.eq(secondary_result, SECONDARY_ATTACK_CALL_NORMAL))
                throw Dm.CRASH("attack_ai_secondary did not return a SECONDARY_ATTACK_* define.", "code/_onclick/ai.dm", 72);
        }

        if (world.time <= Dm.num(next_move))
            return;

        if (Dm.truthy(setting_waypoint))
        {
            setting_waypoint = FALSE;
            await set_waypoint(A);
            return;
        }

        if (Dm.truthy(A._listen_lookup?[COMSIG_ATOM_ATTACK_AI]) && Dm.truthy(Dm.bitand(A._SendSignal(COMSIG_ATOM_ATTACK_AI, Dm.list(A, this, @params)), COMPONENT_CANCEL_ATTACK_CHAIN)))
            return;
        await A.attack_ai(this);
    }

    /*
        AI has no need for the UnarmedAttack() and RangedAttack() procs,
        because the AI code is not generic; attack_ai() is used instead.
        The below is only really for safety, or you can alter the way
        it functions and re-insert it above.
    */
    // tg: code/_onclick/ai.dm:93
    public override async DmTask<object> UnarmedAttack(atom A = null, object proximity_flag = null, DmList modifiers = null)
    {
        if (Dm.truthy(A._listen_lookup?[COMSIG_ATOM_ATTACK_AI]) && Dm.truthy(Dm.bitand(A._SendSignal(COMSIG_ATOM_ATTACK_AI, Dm.list(A, this)), COMPONENT_CANCEL_ATTACK_CHAIN)))
            return null;
        await A.attack_ai(this);
        return null;
    }

    // tg: code/_onclick/ai.dm:98
    public override async DmTask<object> RangedAttack(atom A = null, object modifiers = null)
    {
        if (Dm.truthy(A._listen_lookup?[COMSIG_ATOM_ATTACK_AI]) && Dm.truthy(Dm.bitand(A._SendSignal(COMSIG_ATOM_ATTACK_AI, Dm.list(A, this)), COMPONENT_CANCEL_ATTACK_CHAIN)))
            return null;
        await A.attack_ai(this);
        return null;
    }

    /*
        Since the AI handles shift, ctrl, and alt-click differently
        than anything else in the game, atoms have separate procs
        for AI shift, ctrl, and alt clicking.
    */

    // tg: code/_onclick/ai.dm:124
    public override async DmTask CtrlShiftClickOn(atom target = null)
    {
        await target.AICtrlShiftClick(this);
    }

    // tg: code/_onclick/ai.dm:127
    public override async DmTask ShiftClickOn(atom target = null)
    {
        if (Dm.truthy(await target.AIShiftClick(this)))
            return;
        await base.ShiftClickOn(target);
    }

    // tg: code/_onclick/ai.dm:132
    public override async DmTask CtrlClickOn(atom target = null)
    {
        await target.AICtrlClick(this);
    }

    /// Reimplementation of base_click_alt for AI
    // tg: code/_onclick/ai.dm:137
    public virtual async DmTask ai_base_click_alt(atom target = null)
    {
        // If for some reason we can't alt click
        if (Dm.truthy(_listen_lookup?[COMSIG_MOB_ALTCLICKON]) && Dm.truthy(Dm.bitand(_SendSignal(COMSIG_MOB_ALTCLICKON, Dm.list(this, target)), COMSIG_MOB_CANCEL_CLICKON)))
            return;

        if (!isturf(target) && Dm.truthy(await can_perform_action(target, Dm.bitor(target.interaction_flags_click, SILENT_ADJACENCY))))
        {
            // Signal intercept
            if (Dm.truthy(target._listen_lookup?[COMSIG_CLICK_ALT]) && Dm.truthy(Dm.bitand(target._SendSignal(COMSIG_CLICK_ALT, Dm.list(target, this)), CLICK_ACTION_ANY)))
                return;

            // AI alt click interaction succeeds
            if (Dm.truthy(Dm.bitand(await target.ai_click_alt(this), CLICK_ACTION_ANY)))
                return;
        }

        await client.loot_panel.open(get_turf(target));
    }

    // tg: code/_onclick/ai.dm:288
    public override async DmTask ShiftMiddleClickOn(atom A = null)
    {
        if (Dm.truthy(control_disabled) || Dm.truthy(incapacitated))
            return;
        if (!Dm.truthy(can_see(A)))
            return;
        if (!Dm.truthy(await point_at(A, TRUE)))
            return;
        log_message(Dm.text("points at [] using holopad", A), LOG_EMOTE);
    }

    //
    // Override TurfAdjacent for AltClicking
    //

    // tg: code/_onclick/ai.dm:307
    public override async DmTask<object> TurfAdjacent(turf tile = null)
    {
        await DmTask.CompletedTask;
        return SScameras.is_visible_by_cameras(tile);
    }
}

/* Holopads */
// tg: code/_onclick/ai.dm:299
public partial class obj_machinery_holopad : obj_machinery
{
    // tg: code/_onclick/ai.dm:299
    public override async DmTask<object> ai_click_alt(mob_living_silicon user = null)
    {
        if (user != null)
        {
            balloon_alert(user, "disrupted all active calls");
            add_hiddenprint(user);
        }
        await hangup_all_calls();
        return CLICK_ACTION_SUCCESS;
    }
}

