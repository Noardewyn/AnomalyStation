// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/water_structures/sink.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H1: the whole file (the sink with its water reclaimer and plumbing, washing hands, the face and items, filling and draining
// containers, wetting a mop, the kitchen sink, the plasma fuel station, the greyscale sink and its frame). The var blocks are
// generated (D111).

// tg: code/game/objects/structures/water_structures/sink.dm:1
public partial class obj_structure_sink : obj_structure
{
    // tg: code/game/objects/structures/water_structures/sink.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        create_reagents(capacity, NO_REACT);
        if (Dm.truthy(has_water_reclaimer))
            Waiting.NoSleep(((datum_reagents)reagents).add_reagent(dispensedreagent, capacity));
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_simple_demand_extended)));

        register_context();

        if (Dm.truthy(mapload) && !Dm.truthy(Waiting.NoSleep(find_and_mount_on_atom(mark_for_late_init: TRUE))))
            return INITIALIZE_HINT_LATELOAD;
        return dot;
    }

    // tg: code/game/objects/structures/water_structures/sink.dm:39
    public override void LateInitialize(params object[] __extra)
    {
        Waiting.NoSleep(find_and_mount_on_atom(late_init: TRUE));
    }

    // tg: code/game/objects/structures/water_structures/sink.dm:42
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        if (buildstacktype != null)
            Dm.@new(buildstacktype, loc, buildstackamount);
        else
        {
            foreach (object i in Dm.each<object>(custom_materials))
            {
                datum_material M = i as datum_material;
                Dm.@new(M.sheet_type, loc, FLOOR(Dm.num(custom_materials[M]) / SHEET_MATERIAL_AMOUNT, 1));
            }
        }
        if (Dm.truthy(has_water_reclaimer))
            Dm.@new<obj_item_stock_parts_water_recycler>(drop_location());
        await DmTask.CompletedTask;
    }

    // tg: code/game/objects/structures/water_structures/sink.dm:52
    public override object is_mountable_turf(turf target = null)
    {
        return !isgroundlessturf(target);
    }

    // tg: code/game/objects/structures/water_structures/sink.dm:55
    public override object get_turfs_to_mount_on()
    {
        return Dm.list(get_turf(this));
    }

    // tg: code/game/objects/structures/water_structures/sink.dm:58
    public override object get_mountable_objects()
    {
        return Dm.list();
    }

    // tg: code/game/objects/structures/water_structures/sink.dm:61
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item == null)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Wash hands";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (is_reagent_container(held_item))
        {
            if (Dm.truthy(held_item.is_refillable()) && !Dm.truthy(((datum_reagents)held_item.reagents).holder_full()))
                context[SCREENTIP_CONTEXT_LMB] = "Fill container";
            if (((datum_reagents)held_item.reagents).total_volume > 0)
                context[SCREENTIP_CONTEXT_RMB] = "Drain container";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (held_item is obj_item_mop || (held_item as obj_item_rag)?.blood_level == 0)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Wet mop";
            context[SCREENTIP_CONTEXT_RMB] = "Wash out mop";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (held_item is obj_item_stock_parts_water_recycler && !Dm.truthy(has_water_reclaimer))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Install recycler";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (held_item is obj_item_storage_fancy_pickles_jar)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Clean pickle jar";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (!Dm.truthy((user as mob_living)?.combat_mode) || Dm.truthy(Dm.bitand(held_item.item_flags, NOBLUDGEON)))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Clean item";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return NONE;
    }

    // tg: code/game/objects/structures/water_structures/sink.dm:91
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(has_water_reclaimer))
            Dm.append(ref dot, span_notice(I18n.T("A water recycler is installed. It looks like you could pry it out.")));
        Dm.append(ref dot, span_notice(I18n.T("[]/[] liquids remaining.", ((datum_reagents)reagents).total_volume, ((datum_reagents)reagents).maximum_volume)));
        Dm.append(ref dot, span_notice(I18n.T("You could [] it with a container to empty it down the drain.", EXAMINE_HINT("right-click"))));
        return dot;
    }

    // tg: code/game/objects/structures/water_structures/sink.dm:98
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (user is not mob_living living)
            return dot;
        if (!iscarbon(living))
            return dot;
        if (!Dm.truthy(await Adjacent(living)))
            return dot;
        if (((datum_reagents)reagents).total_volume < 5)
        {
            to_chat(living, span_warning(I18n.T("The sink is dry!")));
            return dot;
        }
        if (Dm.truthy(busy))
        {
            to_chat(living, span_warning(I18n.T("Someone's already washing here!")));
            return dot;
        }

        object selected_area = living.parse_zone_with_bodypart(living.zone_selected);
        bool washing_face = Dm.@in(selected_area, Dm.list(BODY_ZONE_HEAD, BODY_ZONE_PRECISE_MOUTH, BODY_ZONE_PRECISE_EYES));

        playsound(this, Dm.resource("sound/machines/sink-faucet.ogg"), 50);
        string part = washing_face ? I18n.T("face") : I18n.T("hands");
        living.visible_message(span_notice(I18n.T("[] starts washing [] []...", living, living.p_their(), part)),
            span_notice(I18n.T("You start washing your []...", part)));
        busy = TRUE;

        if (!Dm.truthy(await do_after(living, 4 * SECONDS, target: this)))
        {
            busy = FALSE;
            return dot;
        }

        busy = FALSE;
        await ((datum_reagents)reagents).expose(living, TOUCH, 5f / max(((datum_reagents)reagents).total_volume, 5f));
        await ((datum_reagents)reagents).remove_all(5);
        START_PROCESSING(SSobj, this);
        if (washing_face)
            living.SEND_SIGNAL(COMSIG_COMPONENT_CLEAN_FACE_ACT, Dm.list(living, CLEAN_WASH));
        else if (living is mob_living_carbon_human human_user)
        {
            if (!Dm.truthy(await human_user.wash_hands(CLEAN_WASH)))
            {
                to_chat(living, span_warning(I18n.T("Your hands are covered by something!")));
                return dot;
            }
        }
        else
            await living.wash(CLEAN_WASH);

        living.visible_message(span_notice(I18n.T("[] washes [] [] using [].", living, living.p_their(), part, this)),
            span_notice(I18n.T("You wash your [] using [].", part, this)));
        return dot;
    }

    // tg: code/game/objects/structures/water_structures/sink.dm:146
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(busy))
        {
            to_chat(user, span_warning(I18n.T("Someone's already washing here!")));
            return ITEM_INTERACT_FAILURE;
        }

        if (is_reagent_container(tool))
        {
            var RG = tool as obj_item_reagent_containers;
            if (!Dm.truthy(((datum_reagents)reagents).total_volume))
            {
                to_chat(user, span_notice(I18n.T("\\The [] is dry.", this)));
                return ITEM_INTERACT_FAILURE;
            }
            if (Dm.truthy(RG.is_refillable()))
            {
                if (!Dm.truthy(((datum_reagents)RG.reagents).holder_full()))
                {
                    await ((datum_reagents)reagents).trans_to(RG, RG.amount_per_transfer_from_this, transferred_by: user);
                    START_PROCESSING(SSobj, this);
                    to_chat(user, span_notice(I18n.T("You fill [] from [].", RG, this)));
                    return ITEM_INTERACT_SUCCESS;
                }
                to_chat(user, span_notice(I18n.T("\\The [] is full.", RG)));
            }
            return ITEM_INTERACT_FAILURE;
        }

        if (tool is obj_item_mop || (tool as obj_item_rag)?.blood_level == 0)
        {
            if (!Dm.truthy(((datum_reagents)reagents).total_volume))
            {
                to_chat(user, span_notice(I18n.T("\\The [] is dry.", this)));
                return ITEM_INTERACT_FAILURE;
            }
            await ((datum_reagents)reagents).trans_to(tool, 5, transferred_by: user);
            START_PROCESSING(SSobj, this);
            to_chat(user, span_notice(I18n.T("You wet [] in [].", tool, this)));
            playsound(loc, Dm.resource("sound/effects/slosh.ogg"), 25, TRUE);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_stock_parts_water_recycler)
        {
            if (Dm.truthy(has_water_reclaimer))
            {
                to_chat(user, span_warning(I18n.T("There is already has a water recycler installed.")));
                return ITEM_INTERACT_FAILURE;
            }

            playsound(this, Dm.resource("sound/machines/click.ogg"), 20, TRUE);
            qdel(tool);
            has_water_reclaimer = TRUE;
            START_PROCESSING(SSobj, this);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_storage_fancy_pickles_jar)
        {
            if (Dm.truthy(tool.contents.len))
            {
                to_chat(user, span_notice(I18n.T("Looks like there's something left in the jar")));
                return ITEM_INTERACT_FAILURE;
            }
            qdel(tool);
            to_chat(user, span_notice(I18n.T("You washed the jar, ridding it of the brine.")));
            await user.put_in_active_hand(Dm.@new<obj_item_reagent_containers_cup_beaker_large>(loc));
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(user.combat_mode) || Dm.truthy(Dm.bitand(tool.item_flags, NOBLUDGEON)))
        {
            if (((datum_reagents)reagents).total_volume < 5)
            {
                to_chat(user, span_warning(I18n.T("The sink is dry!")));
                return ITEM_INTERACT_FAILURE;
            }

            to_chat(user, span_notice(I18n.T("You start washing []...", tool)));
            playsound(this, Dm.resource("sound/machines/sink-faucet.ogg"), 50);

            if (tool is obj_item_melee_baton_security baton && Dm.truthy(baton.active) && baton.cell != null
                && Dm.truthy(await baton.cell.use(baton.cell_hit_cost, TRUE)))
            {
                flick("baton_active", this);
                await user.Paralyze(baton.knockdown_time);
                await user.set_stutter(baton.knockdown_time);
                user.visible_message(span_warning(I18n.T("[] shocks []self while attempting to wash the active []!", user, user.p_them(), baton.name)),
                    span_userdanger(I18n.T("You unwisely attempt to wash [] while it's still on.", baton)));
                playsound(this, baton.on_stun_sound, 50, TRUE);
                return ITEM_INTERACT_FAILURE;
            }

            busy = TRUE;
            if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: this)))
            {
                busy = FALSE;
                return ITEM_INTERACT_FAILURE;
            }
            busy = FALSE;
            await tool.wash(CLEAN_WASH);
            await ((datum_reagents)reagents).expose(tool, TOUCH, 5f / max(((datum_reagents)reagents).total_volume, 5f));
            await ((datum_reagents)reagents).remove_all(5);
            START_PROCESSING(SSobj, this);
            user.visible_message(span_notice(I18n.T("[] washes [] using [].", user, tool, this)),
                span_notice(I18n.T("You wash [] using [].", tool, this)));
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/game/objects/structures/water_structures/sink.dm:227
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (!is_reagent_container(tool) && tool is not obj_item_mop)
            return await base.item_interaction_secondary(user, tool, modifiers);

        if (Dm.truthy(busy))
        {
            to_chat(user, span_warning(I18n.T("Someone's already washing here!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (((datum_reagents)tool.reagents).total_volume <= 0)
        {
            balloon_alert(user, "already empty!");
            return ITEM_INTERACT_BLOCKING;
        }

        await ((datum_reagents)tool.reagents).clear_reagents();
        playsound(this, Dm.resource("sound/effects/slosh.ogg"), 25, TRUE);

        user.visible_message(
            span_notice(I18n.T("[] empties [] into [].", user, tool, this)),
            span_notice(I18n.T("You empty [] into [], washing its contents down the drain.", tool, this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/water_structures/sink.dm:248
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        tool.play_tool_sound(this);
        await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/water_structures/sink.dm:253
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        await base.crowbar_act(user, tool);

        if (!Dm.truthy(has_water_reclaimer))
        {
            to_chat(user, span_warning(I18n.T("There isn't a water recycler to remove.")));
            return ITEM_INTERACT_FAILURE;
        }

        tool.play_tool_sound(this);
        has_water_reclaimer = FALSE;
        Dm.@new<obj_item_stock_parts_water_recycler>(get_turf(loc));
        to_chat(user, span_notice(I18n.T("You remove the water reclaimer from [].", this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/water_structures/sink.dm:266
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        // Water reclamation complete?
        if (!Dm.truthy(has_water_reclaimer) || Dm.truthy(((datum_reagents)reagents).holder_full()))
            return PROCESS_KILL;

        Waiting.NoSleep(((datum_reagents)reagents).add_reagent(dispensedreagent, reclaim_rate * Dm.num(seconds_per_tick)));
        return null;
    }
}

// tg: code/game/objects/structures/water_structures/sink.dm:287
public partial class obj_structure_sink_greyscale : obj_structure_sink
{
    // tg: code/game/objects/structures/water_structures/sink.dm:293
    public override async DmTask setDir(object newdir = null)
    {
        await base.setDir(REVERSE_DIR(Dm.num(newdir)));
    }
}

// tg: code/game/objects/structures/water_structures/sink.dm:299
public partial class obj_item_wallframe_sinkframe : obj_item_wallframe
{
    // tg: code/game/objects/structures/water_structures/sink.dm:310
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item is obj_item_stock_parts_water_recycler && result_path == typeof(obj_structure_sink_greyscale))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Install recycler";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        return base.add_context(source, context, held_item, user);
    }

    // tg: code/game/objects/structures/water_structures/sink.dm:317
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (result_path == typeof(obj_structure_sink_greyscale_filled))
            Dm.append(ref dot, span_notice(I18n.T("It has a [] installed.", EXAMINE_HINT("water recycler"))));
        else
            Dm.append(ref dot, span_notice(I18n.T("It can be fitted with a [].", EXAMINE_HINT("water recycler"))));
        return dot;
    }

    // tg: code/game/objects/structures/water_structures/sink.dm:324
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_stock_parts_water_recycler && result_path == typeof(obj_structure_sink_greyscale))
        {
            qdel(tool);
            result_path = typeof(obj_structure_sink_greyscale_filled);
            playsound(this, Dm.resource("sound/machines/click.ogg"), 20, TRUE);
            balloon_alert(user, "water recycler installed!");
            return ITEM_INTERACT_SUCCESS;
        }
        await DmTask.CompletedTask;
        return NONE;
    }

    // tg: code/game/objects/structures/water_structures/sink.dm:333
    public override void after_attach(obj attached_to = null)
    {
        var sink = attached_to as obj_structure_sink_greyscale;
        Waiting.NoSleep(sink.set_custom_materials(custom_materials));
        sink.update_appearance(UPDATE_OVERLAYS);
    }
}
