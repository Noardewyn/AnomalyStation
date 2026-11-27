// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/spray.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice C: the whole file but the suicides (the cyborgs' self-filling sprays since silicons SI2): the spray
// bottle (refilling from a tank, the puff and its stream mode, the range from the reagents' weight, the Empty verb's body),
// the space cleaner, the pepper spray, the hercuri spray, the water flower, the chem sprayers, the syndicate spray.
// The medical spray's reskins. Deferred leaf: the sprayer's attack log line (M10). The var blocks are generated.

// tg: code/modules/reagents/reagent_containers/spray.dm:1
public partial class obj_item_reagent_containers_spray : obj_item_reagent_containers
{
    // tg: code/modules/reagents/reagent_containers/spray.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_reagents_item_heatable)));
        RegisterSignal(this, COMSIG_ITEM_IN_UNWRAPPED_TRAITOR_MAIL, nameof(on_mail_unwrap));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/spray.dm:38
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return Dm.truthy(await try_spray(interacting_with, user)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/reagents/reagent_containers/spray.dm:41
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        // This is a hack to make spray bottles fillable from / transferable to these sources
        // However it can be completely removed when these objects are updated to use the new interaction system
        // (because the desired effect will just work out of the box)
        if (istype(interacting_with, typeof(obj_structure_sink)) || istype(interacting_with, typeof(obj_structure_mop_bucket_janitorialcart)) || istype(interacting_with, typeof(obj_machinery_hydroponics)))
            return NONE;
        // Always skip on storage and tables
        if (HAS_TRAIT(interacting_with, TRAIT_COMBAT_MODE_SKIP_INTERACTION))
            return NONE;

        return Dm.truthy(await try_spray(interacting_with, user)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/reagents/reagent_containers/spray.dm:52
    public virtual async DmTask<object> try_spray(atom target = null, mob user = null)
    {
        object adjacent = await user.Adjacent(target);
        datum_reagents holder = (datum_reagents)reagents;
        if ((Dm.truthy(target.is_drainable()) && !Dm.truthy(target.is_refillable())) && Dm.truthy(adjacent) && Dm.truthy(can_fill_from_container))
        {
            datum_reagents target_holder = (datum_reagents)target.reagents;
            if (!Dm.truthy(target_holder.total_volume))
            {
                to_chat(user, span_warning(I18n.T("[] is empty.", target)));
                return FALSE;
            }

            if (Dm.truthy(holder.holder_full()))
            {
                to_chat(user, span_warning(I18n.T("[] is full.", this)));
                return FALSE;
            }

            object trans = await target_holder.trans_to(this, 50, transferred_by: user); //transfer 50u , using the spray's transfer amount would take too long to refill
            to_chat(user, span_notice(I18n.T("You fill \\the [] with [] units of the contents of \\the [].", this, trans, target)));
            return FALSE;
        }

        if (holder.total_volume < amount_per_transfer_from_this)
        {
            to_chat(user, span_warning(I18n.T("Not enough left!")));
            return FALSE;
        }

        if (Dm.truthy(adjacent) && (Dm.truthy(target.density) || ismob(target)))
        {
            // If we're spraying an adjacent mob or a dense object, we start the spray on ITS tile rather than OURs
            // This is so we can use a spray bottle to clean stuff like windows without getting blocked by passflags
            await spray(target, user, get_turf(target));
        }
        else
            await spray(target, user);

        playsound(this, spray_sound, 50, TRUE, -6);
        user.changeNext_move(CLICK_CD_RANGE * 2);
        await user.newtonian_move(get_angle(target, user));
        return TRUE;
    }

    /// Handles creating a chem puff that travels towards the target atom, exposing reagents to everything it hits on the way.
    // tg: code/modules/reagents/reagent_containers/spray.dm:84
    public virtual async DmTask spray(atom target = null, mob user = null, turf start_turf = null)
    {
        start_turf ??= get_turf(this);
        float range = max(min(current_range, Dm.num(get_dist(this, target))), 1);

        obj_effect_decal_chempuff reagent_puff = Dm.@new<obj_effect_decal_chempuff>(start_turf);

        reagent_puff.create_reagents(amount_per_transfer_from_this);
        float puff_reagent_left = range; //how many turf, mob or dense objet we can react with before we consider the chem puff consumed
        datum_reagents holder = (datum_reagents)reagents;
        if (Dm.truthy(stream_mode))
        {
            await holder.trans_to(reagent_puff, amount_per_transfer_from_this);
            puff_reagent_left = 1;
        }
        else
            await holder.trans_to(reagent_puff, amount_per_transfer_from_this, 1 / range);
        datum_reagents puff_holder = (datum_reagents)reagent_puff.reagents;
        reagent_puff.color = mix_color_from_reagents(puff_holder.reagent_list);
        float wait_step = max(round(2 + 3 / range), 2);

        object puff_reagent_string = puff_holder.get_reagent_log_string();
        log_combat(user, start_turf, "fired a puff of reagents from", this, addition: Dm.text("with a range of \\[[]\\], containing [].", range, puff_reagent_string));
        user.log_message(Dm.text("fired a puff of reagents from \\a [] with a range of \\[[]\\] and containing [].", this, range, puff_reagent_string), LOG_ATTACK);

        // do_spray includes a series of step_towards and sleeps. As a result, it will handle deletion of the chempuff.
        await do_spray(target, wait_step, reagent_puff, range, puff_reagent_left, user);
    }

    /// Handles exposing atoms to the reagents contained in a spray's chempuff. Deletes the chempuff when it's completed.
    // tg: code/modules/reagents/reagent_containers/spray.dm:108
    public virtual async DmTask do_spray(atom target = null, object wait_step = null, obj_effect_decal_chempuff reagent_puff = null, object range = null, object puff_reagent_left = null, mob user = null)
    {
        reagent_puff.user = user;
        reagent_puff.sprayer = this;
        reagent_puff.stream = stream_mode;

        turf target_turf = get_turf(target);
        turf start_turf = get_turf(reagent_puff);
        if (target_turf == start_turf) // Don't need to bother movelooping if we don't move
        {
            await reagent_puff.setDir(user.dir);
            await reagent_puff.spray_down_turf(target_turf);
            reagent_puff.end_life();
            return;
        }

        datum_move_loop our_loop = GLOB.move_manager.move_towards_legacy(reagent_puff, target, wait_step, timeout: Dm.num(range) * Dm.num(wait_step), flags: MOVEMENT_LOOP_START_FAST, priority: MOVEMENT_ABOVE_SPACE_PRIORITY) as datum_move_loop;
        reagent_puff.RegisterSignal(our_loop, COMSIG_QDELETING, nameof(obj_effect_decal_chempuff.loop_ended));
        reagent_puff.RegisterSignal(our_loop, COMSIG_MOVELOOP_POSTPROCESS, nameof(obj_effect_decal_chempuff.check_move));
    }

    // tg: code/modules/reagents/reagent_containers/spray.dm:125
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        toggle_stream_mode(user);
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/spray.dm:129
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        object dot = await base.attack_self_secondary(user, modifiers);
        toggle_stream_mode(user);
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/spray.dm:133
    public virtual void toggle_stream_mode(mob user = null)
    {
        if (stream_range == spray_range || !Dm.truthy(stream_range) || !Dm.truthy(spray_range) || possible_transfer_amounts.len > 2 || !Dm.truthy(can_toggle_range))
            return;
        stream_mode = Dm.not(stream_mode);
        if (Dm.truthy(stream_mode))
            current_range = stream_range;
        else
            current_range = spray_range;
        to_chat(user, span_notice(I18n.T("You switch the nozzle setting to [].", Dm.truthy(stream_mode) ? "\"stream\"" : "\"spray\"")));
    }

    // tg: code/modules/reagents/reagent_containers/spray.dm:144  GAME_VERB_SRC(/obj/item/reagent_containers/spray, empty, usr, "Empty Spray Bottle", null)
    public virtual void empty(params object[] __extra)
    {
        // The verb queues its body (__gvb_empty, below) through SSverb_manager: the verbs reach the client with the command
        // layer (rules 7.10, M9).
        Dm.NotTranslated("code/modules/reagents/reagent_containers/spray.dm:144", "empty: the Empty Spray Bottle verb (verbs, M10+)");
    }

    // tg: code/modules/reagents/reagent_containers/spray.dm:144  (the verb's body, __gvb_empty)
    public virtual async DmTask __gvb_empty(DmList structured_args = null)
    {
        if (Dm.truthy(usr.incapacitated))
            return;
        // if (tgui_alert(usr, "Are you sure you want to empty that?", "Empty Bottle:", list("Yes", "No")) != "Yes") return
        Dm.NotTranslated("code/modules/reagents/reagent_containers/spray.dm:147", "empty: the confirmation's tgui_alert (verbs, M10+)");
        if (isturf(usr.loc) && loc == usr)
        {
            to_chat(usr, span_notice(I18n.T("You empty \\the [] onto the floor.", this)));
            await ((datum_reagents)reagents).expose(usr.loc);
            log_combat(usr, usr.loc, "emptied onto", this, addition: Dm.text("which had []", ((datum_reagents)reagents).get_reagent_log_string()));
            await ((datum_reagents)reagents).clear_reagents();
        }
    }

    // tg: code/modules/reagents/reagent_containers/spray.dm:156
    [SignalHandler]
    public virtual object on_mail_unwrap(atom source = null, mob user = null, obj_item letter = null)
    {
        if (((datum_reagents)reagents).total_volume < amount_per_transfer_from_this)
            return null;
        to_chat(user, span_danger(I18n.T("As you open [], a mist spritzes out from inside!", letter)));
        Waiting.NoSleep(spray(user, user));
        playsound(user, spray_sound, 50, TRUE, -6);
        Waiting.NoSleep(forceMove(user.loc));
        return COMPONENT_TRAITOR_MAIL_HANDLED;
    }

    /// Handles updating the spray distance when the reagents change.
    // tg: code/modules/reagents/reagent_containers/spray.dm:167
    public override void on_reagent_change(datum holder = null, params object[] __extra)
    {
        base.on_reagent_change(holder, __extra);
        datum_reagents reagents_holder = (datum_reagents)holder;
        float total_reagent_weight = 0;
        float number_of_reagents = 0;
        float amount_of_reagents = reagents_holder.total_volume;
        DmList cached_reagents = reagents_holder.reagent_list;
        foreach (datum_reagent reagent in Dm.each<datum_reagent>(cached_reagents))
        {
            total_reagent_weight += Dm.num(reagent.reagent_weight) * Dm.num(reagent.volume);
            number_of_reagents++;
        }
        if (Dm.truthy(total_reagent_weight) && Dm.truthy(number_of_reagents) && Dm.truthy(amount_of_reagents)) //don't bother if the container is empty - DIV/0
        {
            float average_reagent_weight = total_reagent_weight / amount_of_reagents;
            spray_range = clamp(round((Dm.num(Dm.initial(this, (obj_item_reagent_containers_spray p) => p.spray_range)) / average_reagent_weight) - ((number_of_reagents - 1) * 1)), 3, 5); //spray distance between 3 and 5 tiles rounded down; extra reagents lose a tile
        }
        else
            spray_range = Dm.num(Dm.initial(this, (obj_item_reagent_containers_spray p) => p.spray_range));
        if (stream_mode == 0)
            current_range = spray_range;
    }
}

// tg: code/modules/reagents/reagent_containers/spray.dm:244
public partial class obj_item_reagent_containers_spray_waterflower : obj_item_reagent_containers_spray
{
    // tg: code/modules/reagents/reagent_containers/spray.dm:256
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        return base.throw_impact(hit_atom, throwingdatum, FALSE);
    }
}

// tg: code/modules/reagents/reagent_containers/spray.dm:268
public partial class obj_item_reagent_containers_spray_waterflower_cyborg : obj_item_reagent_containers_spray_waterflower
{
    // tg: code/modules/reagents/reagent_containers/spray.dm:287
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSfastprocess, this);
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/spray.dm:291
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSfastprocess, this);
        return base.Destroy(force);
    }

    // tg: code/modules/reagents/reagent_containers/spray.dm:295
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (world.time < last_generate + generate_delay)
            return null;
        last_generate = world.time;
        generate_reagents();
        return null;
    }

    // tg: code/modules/reagents/reagent_containers/spray.dm:301
    public override void empty(params object[] __extra)
    {
        to_chat(usr, span_warning(I18n.T("You can not empty this!")));
    }

    // tg: code/modules/reagents/reagent_containers/spray.dm:305
    public virtual void generate_reagents()
    {
        Waiting.NoSleep(((datum_reagents)reagents).add_reagent(generate_type, generate_amount));
    }
}

//chemsprayer
// tg: code/modules/reagents/reagent_containers/spray.dm:309
public partial class obj_item_reagent_containers_spray_chemsprayer : obj_item_reagent_containers_spray
{
    // tg: code/modules/reagents/reagent_containers/spray.dm:326
    public override async DmTask spray(atom A = null, mob user = null, turf start_turf = null)
    {
        float direction = get_dir(this, A);
        turf T = get_turf(A);
        turf T1 = get_step(T, turn(direction, 90)) as turf;
        turf T2 = get_step(T, turn(direction, -90)) as turf;
        DmList the_targets = Dm.list(T, T1, T2);

        for (float i = 1; i <= 3; i++) // intialize sprays
        {
            if (((datum_reagents)reagents).total_volume < 1)
                return;
            await base.spray((atom)the_targets[i], user);
        }
    }
}

// tg: code/modules/reagents/reagent_containers/spray.dm:378
public partial class obj_item_reagent_containers_spray_chemsprayer_party : obj_item_reagent_containers_spray_chemsprayer
{
    // tg: code/modules/reagents/reagent_containers/spray.dm:393
    public override async DmTask spray(atom A = null, mob user = null, turf start_turf = null)
    {
        await base.spray(A, user, start_turf);
        icon_state = Dm.text("[]_used", icon_state);
    }
}

// tg: code/modules/reagents/reagent_containers/spray.dm:413
public partial class obj_item_reagent_containers_spray_syndicate : obj_item_reagent_containers_spray
{
    // tg: code/modules/reagents/reagent_containers/spray.dm:423
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = (string)pick("sprayer_sus_1", "sprayer_sus_2", "sprayer_sus_3", "sprayer_sus_4", "sprayer_sus_5", "sprayer_sus_6", "sprayer_sus_7", "sprayer_sus_8");
        return dot;
    }
}

// tg: code/modules/reagents/reagent_containers/spray.dm:447
public partial class obj_item_reagent_containers_spray_medical : obj_item_reagent_containers_spray
{
    // tg: code/modules/reagents/reagent_containers/spray.dm:455
    public override async DmTask setup_reskins()
    {
        await AddComponent(typeof(datum_component_reskinable_item), typeof(datum_atom_skin_med_spray));
    }
}

// tg: code/modules/reagents/reagent_containers/spray.dm:342
public partial class obj_item_reagent_containers_spray_chemsprayer_janitor : obj_item_reagent_containers_spray_chemsprayer
{
    // tg: code/modules/reagents/reagent_containers/spray.dm:358
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSfastprocess, this);
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/spray.dm:362
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSfastprocess, this);
        return base.Destroy(force);
    }

    // tg: code/modules/reagents/reagent_containers/spray.dm:366
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (world.time < last_generate + generate_delay)
            return null;
        last_generate = world.time;
        Waiting.NoSleep(((datum_reagents)reagents).add_reagent(generate_type, generate_amount));
        return null;
    }
}
