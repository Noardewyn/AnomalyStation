// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/syndicatebomb.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G2 (D316): syndicatebomb.dm whole: the syndicate bomb with its timer, beeps, countdown, wires and
// panel, the training, EMP, clown, empty and self-destruct bombs, every bomb core (the syndicate, training, badmin, large,
// miniature, chemical, EMP and multi-dimensional payloads) and the big red button. The admin
// messages and the bomb logs are rows. The var block of the bomb and of the dimensional core is hand-written: the generator
// types `payload` by its name as a Type (tg's var holds the subtype's typepath until Initialize puts the core itself there),
// and the dimensional core's `chosen_theme` holds a theme's typepath, the key of SSmaterials.dimensional_themes.

// tg: code/game/machinery/syndicatebomb.dm:4
public partial class obj_machinery_syndicatebomb : obj_machinery
{
    // tg: code/game/machinery/syndicatebomb.dm:1
    private const float BUTTON_COOLDOWN = 60; // cant delay the bomb forever
    private const float BUTTON_DELAY = 50; //five seconds

    /// Reference to the bomb core inside the bomb, which is the part that actually explodes.
    // tg: code/game/machinery/syndicatebomb.dm:46  (a typepath on the subtypes, the core itself after Initialize)
    public object payload = typeof(obj_item_bombcore_syndicate);

    // tg: code/game/machinery/syndicatebomb.dm:4
    public obj_machinery_syndicatebomb()
    {
        icon = Dm.resource("icons/obj/devices/assemblies.dmi");
        name = "syndicate bomb";
        icon_state = "syndicate-bomb";
        desc = "A large and menacing device. Can be bolted down with a wrench.";
        anchored = FALSE;
        density = FALSE;
        layer = BELOW_MOB_LAYER; //so people can't hide it and it's REALLY OBVIOUS
        resistance_flags = Dm.num(Dm.bitor(FIRE_PROOF, ACID_PROOF));
        processing_flags = START_PROCESSING_MANUALLY;
        subsystem_type = typeof(datum_controller_subsystem_processing_fastprocess);
        interaction_flags_machine = Dm.num(Dm.bitor(INTERACT_MACHINE_WIRES_IF_OPEN, INTERACT_MACHINE_OFFLINE));
        use_power = NO_POWER_USE;
    }

    // tg: code/game/machinery/syndicatebomb.dm:18
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        custom_materials = Dm.list(Dm.kv(typeof(datum_material_alloy_plasteel), SHEET_MATERIAL_AMOUNT * 10));
    }

    private obj_item_bombcore payload_core => payload as obj_item_bombcore;

    // tg: code/game/machinery/syndicatebomb.dm:55
    public virtual async DmTask<object> try_detonate(float? ignore_active = null)
    {
        ignore_active ??= FALSE;
        bool dot = payload_core != null && Dm.@in(payload_core, this) && (Dm.truthy(active) || Dm.truthy(ignore_active));
        if (dot)
            await payload_core.detonate();
        return dot;
    }

    // tg: code/game/machinery/syndicatebomb.dm:60
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        if (!Dm.truthy(await try_detonate()))
            await base.atom_break(damage_flag);
        return null;
    }

    // tg: code/game/machinery/syndicatebomb.dm:64
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        if (!Dm.truthy(await try_detonate()))
            await base.atom_destruction(damage_flag);
    }

    // tg: code/game/machinery/syndicatebomb.dm:68
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        return FALSE;
    }

    // tg: code/game/machinery/syndicatebomb.dm:71
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(), null); // set waitfor = FALSE, inherited from /datum/process (waiting-model.md 5.4)
    }

    private async DmTask<object> process__body()
    {
        if (!Dm.truthy(active))
            return PROCESS_KILL;

        // GAP: GLOB.active_cosmic_fields (the heretic's cosmic fields are not ported, so no field stops the bomb)
        DmList active_cosmic_fields__gap = null;
        foreach (atom potential_field in Dm.each<atom>(active_cosmic_fields__gap))
        {
            if (Dm.num(get_dist(potential_field, this)) < 3)
            {
                Dm.@new<obj_effect_temp_visual_revenant>(get_turf(this));
                defuse();
                return null;
            }
        }

        if (next_beep != null && Dm.num(next_beep) <= world.time)
        {
            float volume;
            float sw__1 = Dm.num(seconds_remaining());
            if (sw__1 >= 0 && sw__1 <= 5)
                volume = 50;
            else if (sw__1 >= 5 && sw__1 <= 10)
                volume = 40;
            else if (sw__1 >= 10 && sw__1 <= 15)
                volume = 30;
            else if (sw__1 >= 15 && sw__1 <= 20)
                volume = 20;
            else if (sw__1 >= 20 && sw__1 <= 25)
                volume = 10;
            else
                volume = 5;
            playsound(loc, beepsound, volume, FALSE);
            next_beep = world.time + 10;
        }

        if (Dm.truthy(active) && (Dm.num(detonation_timer) <= world.time || Dm.truthy(explode_now)))
        {
            active = FALSE;
            timer_set = Dm.num(Dm.initial(this, nameof(timer_set)));
            update_appearance();
            await try_detonate(TRUE);
        }
        return null;
    }

    // tg: code/game/machinery/syndicatebomb.dm:105
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_wires(Dm.@new<datum_wires_syndicatebomb>(this));
        if (payload != null)
            payload = Dm.@new((Type)payload, this);
        update_appearance();
        countdown = Dm.@new<obj_effect_countdown_syndicatebomb>(this);
        end_processing();
        RegisterSignal(this, COMSIG_ATOM_TIMESTOP_FREEZE, nameof(on_timestop));
        return dot;
    }

    // tg: code/game/machinery/syndicatebomb.dm:115
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref countdown);
        end_processing();
        return base.Destroy(force);
    }

    // tg: code/game/machinery/syndicatebomb.dm:120
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        dot.Add(I18n.T("The patented external shell design is resistant to \"probably all\" forms of external explosive compression, protecting the electronically-trigged bomb core from accidental early detonation."));
        if (payload_core != null)
            dot.Add(I18n.T("A small window reveals some information about the payload: [].", payload_core.desc));
        if (Dm.truthy(examinable_countdown))
        {
            dot.Add(span_notice(I18n.T("A digital display on it reads \"[]\".", seconds_remaining())));
            if (Dm.truthy(active))
                balloon_alert(user, Dm.text("[]", seconds_remaining()));
        }
        else
        {
            dot.Add(span_notice(I18n.T("The digital display on it is inactive.")));
        }
        return dot;
    }

    // tg: code/game/machinery/syndicatebomb.dm:132
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][][]", Dm.initial(this, nameof(icon_state)), Dm.truthy(active) ? "-active" : "-inactive", Dm.truthy(open_panel) ? "-wires" : "");
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/syndicatebomb.dm:136
    [SignalHandler]
    public virtual void on_timestop(params object[] __extra)
    {
        if (!Dm.truthy(active))
            return;
        end_processing();
        timestop_start = world.time;
        RegisterSignal(this, COMSIG_ATOM_TIMESTOP_UNFREEZE, nameof(on_timestop_end));
    }

    // tg: code/game/machinery/syndicatebomb.dm:144
    [SignalHandler]
    public virtual void on_timestop_end(params object[] __extra)
    {
        UnregisterSignal(this, COMSIG_ATOM_TIMESTOP_UNFREEZE);
        if (!Dm.truthy(active)) // defused DURING timestop? badass
            return;
        float time_spent_in_timestop = world.time - Dm.num(timestop_start);
        next_beep = Dm.num(next_beep) + time_spent_in_timestop;
        detonation_timer = Dm.num(detonation_timer) + time_spent_in_timestop;
        begin_processing();
        timestop_start = null;
    }

    // tg: code/game/machinery/syndicatebomb.dm:155
    public virtual object seconds_remaining()
    {
        if (Dm.truthy(active))
            return max(0, round((Dm.num(detonation_timer) - (Dm.truthy(timestop_start) ? Dm.num(timestop_start) : world.time)) / 10));
        return timer_set;
    }

    // tg: code/game/machinery/syndicatebomb.dm:162
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(can_unanchor))
            return FALSE;
        if (!Dm.truthy(anchored))
        {
            if (!isturf(loc) || isspaceturf(loc))
            {
                to_chat(user, span_notice(I18n.T("The bomb must be placed on solid ground to attach it.")));
            }
            else
            {
                to_chat(user, span_notice(I18n.T("You firmly wrench the bomb to the floor.")));
                tool.play_tool_sound(this);
                await set_anchored(TRUE);
                if (Dm.truthy(active))
                    to_chat(user, span_notice(I18n.T("The bolts lock in place.")));
            }
        }
        else
        {
            if (!Dm.truthy(active))
            {
                to_chat(user, span_notice(I18n.T("You wrench the bomb from the floor.")));
                tool.play_tool_sound(this);
                await set_anchored(FALSE);
            }
            else
            {
                to_chat(user, span_warning(I18n.T("The bolts are locked down!")));
            }
        }

        return TRUE;
    }

    // tg: code/game/machinery/syndicatebomb.dm:184
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        tool.play_tool_sound(this, 50);
        open_panel = Dm.truthy(open_panel) ? FALSE : TRUE;
        update_appearance();
        to_chat(user, span_notice(I18n.T("You [] the wire panel.", Dm.truthy(open_panel) ? I18n.T("open") : I18n.T("close"))));
        return TRUE;
    }

    // tg: code/game/machinery/syndicatebomb.dm:191
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        object dot = TRUE;
        if (Dm.truthy(open_panel) && Dm.truthy(wires.is_all_cut()))
        {
            if (payload != null)
            {
                tool.play_tool_sound(this, 25); // sshhh
                to_chat(user, span_notice(I18n.T("You carefully pry out [].", payload)));
                await payload_core.forceMove(drop_location() as atom);
                payload = null;
            }
            else
            {
                to_chat(user, span_warning(I18n.T("There isn't anything in here to remove!")));
            }
        }
        else if (Dm.truthy(open_panel))
        {
            to_chat(user, span_warning(I18n.T("The wires connecting the shell to the explosives are holding it down!")));
        }
        else
        {
            to_chat(user, span_warning(I18n.T("The cover is screwed on, it won't pry off!")));
        }
        return dot;
    }

    // tg: code/game/machinery/syndicatebomb.dm:206
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (payload != null || !Dm.truthy(wires.is_all_cut()) || !Dm.truthy(open_panel))
            return FALSE;

        if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
            return TRUE;

        to_chat(user, span_notice(I18n.T("You start to cut [] apart...", this)));
        if (Dm.truthy(await tool.use_tool(this, user, 20, volume: 50)))
        {
            to_chat(user, span_notice(I18n.T("You cut [] apart.", this)));
            Dm.@new<obj_item_stack_sheet_plasteel>(loc, 5);
            qdel(this);
        }
        return TRUE;
    }

    // tg: code/game/machinery/syndicatebomb.dm:221
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(is_wire_tool(tool)) && Dm.truthy(open_panel))
        {
            await wires.interact(user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_bombcore)))
        {
            if (payload != null)
            {
                to_chat(user, span_warning(I18n.T("[] is already loaded into []! You'll have to remove it first.", payload, this)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            payload = tool;
            to_chat(user, span_notice(I18n.T("You place [] into [].", payload, this)));
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/machinery/syndicatebomb.dm:238
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        float old_integ = Dm.num(atom_integrity);
        object dot = await base.attackby(attacking_item, user, modifiers, attack_modifiers);
        if (old_integ > Dm.num(atom_integrity) && Dm.truthy(active) && payload != null)
            to_chat(user, span_warning(I18n.T("That seems like a really bad idea...")));
        return dot;
    }

    // tg: code/game/machinery/syndicatebomb.dm:244
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        await wires.interact(user);
        if (!Dm.truthy(open_panel))
        {
            if (!Dm.truthy(active))
                await settings(user);
            else if (Dm.truthy(anchored))
                to_chat(user, span_warning(I18n.T("The bomb is bolted to the floor!")));
        }
        return null;
    }

    // tg: code/game/machinery/syndicatebomb.dm:252
    public virtual void activate()
    {
        active = TRUE;
        begin_processing();
        countdown.start();
        next_beep = world.time + 10;
        detonation_timer = world.time + timer_set * 10;
        // 2 booms, 0 duds at lowest timer
        // 12 booms, 6 duds at ~9 minutes
        datum_wires_syndicatebomb bomb_wires = (datum_wires_syndicatebomb)wires;
        if (Dm.truthy(add_boom_wires))
        {
            float boom_wires = clamp(round(timer_set / 45, 1), 2, 12);
            float dud_wires = 0;
            if (boom_wires >= 3)
            {
                dud_wires = floor(boom_wires / 2);
                boom_wires -= dud_wires;
            }
            bomb_wires.setup_wires(num_booms: boom_wires, num_duds: dud_wires);
        }
        else
        {
            bomb_wires.setup_wires(num_booms: 2, num_duds: 0);
        }
        playsound(this, Dm.resource("sound/machines/click.ogg"), 30, TRUE);
        update_appearance();
    }

    // tg: code/game/machinery/syndicatebomb.dm:273
    public virtual void defuse()
    {
        active = FALSE;
        delayedlittle = FALSE;
        delayedbig = FALSE;
        examinable_countdown = TRUE;
        end_processing();
        detonation_timer = null;
        next_beep = null;
        countdown.stop();
        if (payload_core != null && Dm.@in(payload_core, this))
            payload_core.defuse();
        datum_wires_syndicatebomb bomb_wires = (datum_wires_syndicatebomb)wires;
        bomb_wires.setup_wires(num_booms: 2);
        update_appearance();
    }

    // tg: code/game/machinery/syndicatebomb.dm:288
    public virtual async DmTask settings(mob user = null)
    {
        if (!Dm.truthy(await user.can_perform_action(this, ALLOW_SILICON_REACH)) || !Dm.truthy(await user.can_interact_with(this)))
            return;
        object new_timer = await tgui_input_number(user, I18n.T("Set the timer[]", Dm.truthy(add_boom_wires) ? I18n.T(" (the longer the timer, the harder to defuse!)") : ""), I18n.T("Countdown"), timer_set, maximum_timer, minimum_timer);
        if (!Dm.truthy(new_timer) || QDELETED(user) || QDELETED(this) || !Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return;
        timer_set = Dm.num(new_timer);
        // [icon2html(src, viewers(src))]: the bomb's picture in the chat line (chat icons, not-translated section 76)
        visible_message(span_notice(I18n.T("timer set for [] seconds.", timer_set)));
        object choice = await tgui_alert(user, I18n.T("Would you like to start the countdown now?"), I18n.T("Bomb Timer"), Dm.list("Yes", "No"));
        if (!Dm.eq(choice, "Yes") || QDELETED(user) || QDELETED(this) || !Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return;
        if (Dm.truthy(active))
        {
            to_chat(user, span_warning(I18n.T("The bomb is already active!")));
            return;
        }
        visible_message(span_danger(I18n.T("[] seconds until detonation, please clear the area.", timer_set)));
        activate();
        add_fingerprint(user);
        // We don't really concern ourselves with duds or fakes after this
        if (payload == null || istype(payload, typeof(obj_item_bombcore_training)))
            return;

        notify_ghosts(
            I18n.T("\\A [] has been activated at []!", this, get_area(this)),
            source: this,
            header: I18n.T("Bomb Planted"));
        user.add_mob_memory(typeof(datum_memory_bomb_planted_syndicate), Dm.named("antagonist", this));
        log_bomber(user, "has primed a", this, Dm.text("for detonation (Payload: [])", payload_core.name));
        payload_core.adminlog = I18n.T("\\The [] that [] had primed detonated!", this, key_name(user));
        user.log_message(Dm.text("primed the []. (Payload: [])", this, payload_core.name), LOG_GAME, log_globally: FALSE);
    }
}

// tg: code/game/machinery/syndicatebomb.dm:345
public partial class obj_machinery_syndicatebomb_empty : obj_machinery_syndicatebomb
{
    // tg: code/game/machinery/syndicatebomb.dm:352
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(wires.cut_all());
        return dot;
    }
}

// tg: code/game/machinery/syndicatebomb.dm:356
public partial class obj_machinery_syndicatebomb_nukie_empty : obj_machinery_syndicatebomb_nukie
{
    // tg: code/game/machinery/syndicatebomb.dm:363
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(wires.cut_all());
        return dot;
    }
}

///Bomb Cores///
// tg: code/game/machinery/syndicatebomb.dm:375
public partial class obj_item_bombcore : obj_item
{
    // tg: code/game/machinery/syndicatebomb.dm:395
    public override object ex_act(object severity = null, object target = null, params object[] __extra) // Little boom can chain a big boom.
    {
        Waiting.NoSleep(detonate());
        return TRUE;
    }

    // tg: code/game/machinery/syndicatebomb.dm:399
    public override async DmTask burn__proc()
    {
        if (Dm.truthy(explodes_when_burnt))
            await detonate();
        await base.burn__proc();
    }

    // tg: code/game/machinery/syndicatebomb.dm:404
    public virtual async DmTask detonate()
    {
        if (Dm.truthy(adminlog))
        {
            message_admins(adminlog);
            log_game(adminlog);
        }
        await explosion(this, range_heavy, range_medium, range_light, range_flame);
        if (loc != null && istype(loc, typeof(obj_machinery_syndicatebomb)))
            qdel(loc);
        qdel(this);
    }

    // tg: code/game/machinery/syndicatebomb.dm:413
    public virtual void defuse()
    {
        //Note: the machine's defusal is mostly done from the wires code, this is here if you want the core itself to do anything.
    }
}

/// Subtype for the bomb cores found inside syndicate bombs, which will not detonate due to explosion/burning.
// tg: code/game/machinery/syndicatebomb.dm:419
public partial class obj_item_bombcore_syndicate : obj_item_bombcore
{
    // tg: code/game/machinery/syndicatebomb.dm:424
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        return FALSE;
    }
}

// tg: code/game/machinery/syndicatebomb.dm:434
public partial class obj_item_bombcore_training : obj_item_bombcore
{
    // tg: code/game/machinery/syndicatebomb.dm:440
    public virtual async DmTask reset()
    {
        obj_machinery_syndicatebomb holder = loc as obj_machinery_syndicatebomb;
        if (holder != null)
        {
            if (holder.wires != null)
            {
                await holder.wires.repair();
                holder.wires.shuffle_wires();
            }
            holder.delayedbig = FALSE;
            holder.delayedlittle = FALSE;
            holder.explode_now = FALSE;
            holder.update_appearance();
            STOP_PROCESSING(SSfastprocess, holder);
        }
    }

    // tg: code/game/machinery/syndicatebomb.dm:452
    public override async DmTask detonate()
    {
        obj_machinery_syndicatebomb holder = loc as obj_machinery_syndicatebomb;
        if (holder != null)
        {
            attempts++;
            // [icon2html(holder, viewers(holder))]: the bomb's picture in the chat line (chat icons, not-translated section 76)
            holder.loc.visible_message(span_danger(I18n.T("Alert: Bomb has detonated. Your score is now [] for []. Resetting wires...", defusals, attempts)));
            await reset();
        }
        else
        {
            qdel(this);
        }
    }

    // tg: code/game/machinery/syndicatebomb.dm:461
    public override void defuse()
    {
        obj_machinery_syndicatebomb holder = loc as obj_machinery_syndicatebomb;
        if (holder != null)
        {
            attempts++;
            defusals++;
            // [icon2html(holder, viewers(holder))]: the bomb's picture in the chat line (chat icons, not-translated section 76)
            holder.loc.visible_message(span_notice(I18n.T("Alert: Bomb has been defused. Your score is now [] for []! Resetting wires in 5 seconds...", defusals, attempts)));
            _addtimer(Dm.@new<datum_callback>(this, nameof(reset)), 5 * SECONDS, file: "code/game/machinery/syndicatebomb.dm", line: 468); //Just in case someone is trying to remove the bomb core this gives them a little window to crowbar it out
        }
    }
}

// tg: code/game/machinery/syndicatebomb.dm:469
public partial class obj_item_bombcore_badmin : obj_item_bombcore
{
    // tg: code/game/machinery/syndicatebomb.dm:473
    public override void defuse() //because we wouldn't want them being harvested by players
    {
        obj_machinery_syndicatebomb B = loc as obj_machinery_syndicatebomb;
        qdel(B);
        qdel(this);
    }
}

// tg: code/game/machinery/syndicatebomb.dm:478
public partial class obj_item_bombcore_badmin_summon : obj_item_bombcore_badmin
{
    // tg: code/game/machinery/syndicatebomb.dm:482
    public override async DmTask detonate()
    {
        obj_machinery_syndicatebomb B = loc as obj_machinery_syndicatebomb;
        foreach (atom spawned in Dm.each_cast<atom>(await spawn_and_random_walk(summon_path, this, amt_summon, walk_chance: 50, admin_spawn: TRUE, cardinals_only: FALSE)))
            ADD_TRAIT(spawned, TRAIT_SPAWNED_MOB, INNATE_TRAIT);
        qdel(B);
        qdel(this);
    }
}

// tg: code/game/machinery/syndicatebomb.dm:489
public partial class obj_item_bombcore_badmin_summon_clown : obj_item_bombcore_badmin_summon
{
    // tg: code/game/machinery/syndicatebomb.dm:495
    public override void defuse()
    {
        playsound(this, Dm.resource("sound/misc/sadtrombone.ogg"), 50);
        base.defuse();
    }
}

// tg: code/game/machinery/syndicatebomb.dm:515
public partial class obj_item_bombcore_chemical : obj_item_bombcore
{
    // tg: code/game/machinery/syndicatebomb.dm:531
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(core_holder_volume);
        return dot;
    }

    // tg: code/game/machinery/syndicatebomb.dm:535
    public override async DmTask detonate()
    {
        if (time_release > 0)
        {
            float total_volume = (reagents as datum_reagents).total_volume;
            foreach (obj_item_reagent_containers RC in Dm.each<obj_item_reagent_containers>(beakers))
                total_volume += (RC.reagents as datum_reagents).total_volume;

            if (total_volume < time_release) // If it's empty, the detonation is complete.
            {
                if (loc != null && istype(loc, typeof(obj_machinery_syndicatebomb)))
                    qdel(loc);
                qdel(this);
                return;
            }

            float fraction = time_release / total_volume;
            datum_reagents reactants__1 = Dm.@new<datum_reagents>(time_release);
            reactants__1.my_atom = this;
            foreach (obj_item_reagent_containers RC in Dm.each<obj_item_reagent_containers>(beakers))
                await (RC.reagents as datum_reagents).trans_to(reactants__1, (RC.reagents as datum_reagents).total_volume * fraction, no_react: TRUE);
            await chem_splash(get_turf(this) as atom, reagents as datum_reagents, spread_range, Dm.list(reactants__1), temp_boost);

            // Detonate it again in one second, until it's out of juice.
            _addtimer(Dm.@new<datum_callback>(this, nameof(detonate)), 1 * SECONDS, file: "code/game/machinery/syndicatebomb.dm", line: 558);
        }

        // If it's not a time release bomb, do normal explosion

        DmList reactants = Dm.list();

        foreach (obj_item_reagent_containers_cup G in Dm.each<obj_item_reagent_containers_cup>(beakers))
            reactants.Add(G.reagents);

        foreach (obj_item_slime_extract S in Dm.each<obj_item_slime_extract>(beakers))
        {
            if (Dm.truthy(S.extract_uses))
            {
                foreach (obj_item_reagent_containers_cup G in Dm.each<obj_item_reagent_containers_cup>(beakers))
                    await (G.reagents as datum_reagents).trans_to(S, (G.reagents as datum_reagents).total_volume);

                if (S != null && S.reagents != null && Dm.truthy((S.reagents as datum_reagents).total_volume))
                    reactants.Add(S.reagents);
            }
        }

        if (!Dm.truthy(await chem_splash(get_turf(this) as atom, reagents as datum_reagents, spread_range, reactants, temp_boost)))
        {
            playsound(loc, Dm.resource("sound/items/tools/screwdriver2.ogg"), 50, TRUE);
            return; // The Explosion didn't do anything. No need to log, or disappear.
        }

        if (Dm.truthy(adminlog))
        {
            message_admins(adminlog);
            log_game(adminlog);
        }

        playsound(loc, Dm.resource("sound/effects/bamf.ogg"), 75, TRUE, 5);
    }

    // tg: code/game/machinery/syndicatebomb.dm:583
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_reagent_containers_cup_beaker)) && !istype(tool, typeof(obj_item_reagent_containers_cup_bottle)))
            return NONE;
        if (beakers.Count >= max_beakers)
        {
            to_chat(user, span_warning(I18n.T("[] won't fit! \\The [] can only hold up to [] containers.", tool, this, max_beakers)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;
        beakers.Add(tool);
        to_chat(user, span_notice(I18n.T("You load [] with [].", this, tool)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/syndicatebomb.dm:595
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(beakers.Count))
            return NONE;
        tool.play_tool_sound(this);
        foreach (obj_item beaker in Dm.each<obj_item>(beakers.Copy()))
        {
            await beaker.forceMove(drop_location() as atom);
            beakers.Remove(beaker);
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/syndicatebomb.dm:604
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        // Using different grenade casings, causes the payload to have different properties.
        obj_item_stock_parts_matter_bin bin = Dm.locate_type(typeof(obj_item_stock_parts_matter_bin), components) as obj_item_stock_parts_matter_bin;
        if (bin != null)
            max_beakers += Dm.num(bin.rating); // max beakers = 2-5.
        foreach (obj_item_grenade_chem_grenade nade in Dm.each<obj_item_grenade_chem_grenade>(components))
        {
            if (istype(nade, typeof(obj_item_grenade_chem_grenade_large)))
            {
                max_beakers += 1; // Adding two large grenades only allows for a maximum of 7 beakers.
                spread_range += 2; // Extra range, reduced density.
                temp_boost += 50; // maximum of +150K blast using only large beakers. Not enough to self ignite.
                foreach (obj_item_slime_extract slime in Dm.each<obj_item_slime_extract>(nade.beakers)) // And slime cores.
                {
                    if (beakers.Count < max_beakers)
                    {
                        beakers.Add(slime);
                        await slime.forceMove(this);
                    }
                    else
                    {
                        await slime.forceMove(drop_location() as atom);
                    }
                }
            }

            if (istype(nade, typeof(obj_item_grenade_chem_grenade_cryo)))
            {
                spread_range -= 1; // Reduced range, but increased density.
                temp_boost -= 100; // minimum of -150K blast.
            }

            if (istype(nade, typeof(obj_item_grenade_chem_grenade_pyro)))
                temp_boost += 150; // maximum of +350K blast, which is enough to self ignite. Which means a self igniting bomb can't take advantage of other grenade casing properties. Sorry?

            if (istype(nade, typeof(obj_item_grenade_chem_grenade_adv_release)))
                time_release += 5 * SECONDS; // A typical bomb, using basic beakers, will explode over 2-4 seconds. Using two will make the reaction last for less time, but it will be more dangerous overall.

            foreach (obj_item_reagent_containers_cup beaker in Dm.each<obj_item_reagent_containers_cup>(nade))
            {
                if (beakers.Count < max_beakers)
                {
                    beakers.Add(beaker);
                    await beaker.forceMove(this);
                }
                else
                {
                    await beaker.forceMove(drop_location() as atom);
                }
            }
        }

        await base.on_craft_completion(components, current_recipe, crafter);
    }
}

// tg: code/game/machinery/syndicatebomb.dm:645
public partial class obj_item_bombcore_emp : obj_item_bombcore
{
    // tg: code/game/machinery/syndicatebomb.dm:651
    public override async DmTask detonate()
    {
        if (Dm.truthy(adminlog))
        {
            message_admins(adminlog);
            log_game(adminlog);
        }

        await empulse(this, range_heavy, range_medium);

        qdel(this);
    }
}

// tg: code/game/machinery/syndicatebomb.dm:662
public partial class obj_item_bombcore_dimensional : obj_item_bombcore
{
    // tg: code/game/machinery/syndicatebomb.dm:660
    private const string DIMENSION_CHOICE_RANDOM = "None/Randomized";

    // tg: code/game/machinery/syndicatebomb.dm:666  (a theme's typepath, the key of SSmaterials.dimensional_themes)
    public object chosen_theme;

    // tg: code/game/machinery/syndicatebomb.dm:662
    public obj_item_bombcore_dimensional()
    {
        name = "multi-dimensional payload";
        desc = "A wicked payload meant to wildly transmutate terrain over a wide area, a power no mere human should wield.";
        range_heavy = 17;
    }

    // tg: code/game/machinery/syndicatebomb.dm:668
    public override object Destroy(float? force = null)
    {
        chosen_theme = null;
        return base.Destroy(force);
    }

    // tg: code/game/machinery/syndicatebomb.dm:672
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        await base.on_craft_completion(components, current_recipe, crafter);
        range_heavy = 13;
        foreach (obj_item_grenade_chem_grenade nade in Dm.each<obj_item_grenade_chem_grenade>(components))
        {
            if (istype(nade, typeof(obj_item_grenade_chem_grenade_large)) || istype(nade, typeof(obj_item_grenade_chem_grenade_adv_release)))
                range_heavy += 1;
            foreach (obj_item thing in Dm.each_cast<obj_item>(nade.beakers?.Copy())) //remove beakers, then delete the grenade.
                await thing.forceMove(drop_location() as atom);
        }
        obj_item_gibtonite ore = Dm.locate_type(typeof(obj_item_gibtonite), components) as obj_item_gibtonite;
        object sw__1 = ore.quality;
        if (Dm.eq(sw__1, GIBTONITE_QUALITY_LOW))
            range_heavy -= 2;
        else if (Dm.eq(sw__1, GIBTONITE_QUALITY_HIGH))
            range_heavy += 4;
    }

    // tg: code/game/machinery/syndicatebomb.dm:687
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (chosen_theme != null)
            return dot; // a runtime in tg: chosen_theme holds a typepath, and `chosen_theme?.name` reads a var of a type
        dot.Add(span_notice(I18n.T("Use in hand to change the linked dimension. Current dimension: [].", I18n.T("None, output will be random"))));
        return dot;
    }

    // tg: code/game/machinery/syndicatebomb.dm:691
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        DmList choosable_dimensions = Dm.list();
        datum_radial_menu_choice null_choice = Dm.@new<datum_radial_menu_choice>();
        null_choice.name = DIMENSION_CHOICE_RANDOM;
        choosable_dimensions[DIMENSION_CHOICE_RANDOM] = null_choice;
        foreach (object theme in Dm.each(SSmaterials.dimensional_themes))
        {
            datum_radial_menu_choice theme_choice = Dm.@new<datum_radial_menu_choice>();
            theme_choice.image = image(Dm.initial(theme, nameof(datum_dimension_theme.icon)), Dm.initial(theme, nameof(datum_dimension_theme.icon_state)));
            theme_choice.name = Dm.initial(theme, nameof(datum_dimension_theme.name));
            choosable_dimensions[theme] = theme_choice;
        }

        object picked = await show_radial_menu(user, this, choosable_dimensions, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), radius: 38, require_near: TRUE);
        if (picked == null)
            return dot;
        if (Dm.eq(picked, DIMENSION_CHOICE_RANDOM))
            chosen_theme = null;
        else
            chosen_theme = picked;
        if (chosen_theme != null)
            return dot; // a runtime in tg: `chosen_theme?.name` reads a var of a typepath
        balloon_alert(user, I18n.T("set to []", DIMENSION_CHOICE_RANDOM));
        return dot;
    }

    // tg: code/game/machinery/syndicatebomb.dm:712
    public virtual object check_menu(mob user = null)
    {
        if (!Dm.truthy(user.is_holding(this)) || Dm.truthy(((mob_living)user).incapacitated))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/machinery/syndicatebomb.dm:717
    public override async DmTask detonate()
    {
        DmList affected_turfs = circle_range_turfs(this, range_heavy) as DmList;
        float theme_count = Dm.length(SSmaterials.dimensional_themes);
        float num_affected = 0;
        foreach (turf affected in Dm.each_cast<turf>(affected_turfs))
        {
            datum_dimension_theme theme_to_use;
            if (chosen_theme == null)
                theme_to_use = SSmaterials.dimensional_themes[SSmaterials.dimensional_themes[rand(1, theme_count)]] as datum_dimension_theme;
            else
                theme_to_use = SSmaterials.dimensional_themes[chosen_theme] as datum_dimension_theme;
            if (!Dm.truthy(theme_to_use.can_convert(affected)))
                continue;
            num_affected++;
            bool skip_sound = false;
            if (Dm.truthy(Dm.mod(num_affected, 5))) //makes it play the sound more sparingly
                skip_sound = true;
            float time_mult = round(Dm.num(get_dist_euclidean(get_turf(this) as atom, affected))) + 1;
            _addtimer(Dm.@new<datum_callback>(theme_to_use, nameof(datum_dimension_theme.apply_theme), affected, skip_sound ? TRUE : FALSE, TRUE), 0.1f * SECONDS * time_mult, file: "code/game/machinery/syndicatebomb.dm", line: 734);
        }
        qdel(this);
    }
}

///Syndicate Detonator (aka the big red button)///
// tg: code/game/machinery/syndicatebomb.dm:741
public partial class obj_item_syndicatedetonator : obj_item
{
    // tg: code/game/machinery/syndicatebomb.dm:1
    private const float BUTTON_COOLDOWN = 60; // cant delay the bomb forever
    private const float BUTTON_DELAY = 50; //five seconds

    // tg: code/game/machinery/syndicatebomb.dm:754
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.num(timer) < world.time)
        {
            foreach (obj_machinery_syndicatebomb B in Dm.each_cast<obj_machinery_syndicatebomb>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_syndicatebomb))))
            {
                if (Dm.truthy(B.active))
                {
                    B.detonation_timer = world.time + BUTTON_DELAY;
                    detonated++;
                }
                existent++;
            }
            playsound(user, Dm.resource("sound/machines/click.ogg"), 20, TRUE);
            to_chat(user, span_notice(I18n.T("[] found, [] triggered.", existent, detonated)));
            if (Dm.truthy(detonated))
            {
                detonated--;
                log_bomber(user, Dm.text("remotely detonated [] using a", Dm.truthy(detonated) ? "syndicate bombs" : "a syndicate bomb"), this);
            }
            detonated = 0;
            existent = 0;
            timer = world.time + BUTTON_COOLDOWN;
        }
        return null;
    }
}
