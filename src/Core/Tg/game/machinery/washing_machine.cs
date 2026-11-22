// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/washing_machine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H1: the whole file (the dye registry; the washing machine: its load, door, wash cycle and shaking, the dye of a crayon or any
// dyed item that colours the load, the bloody mess of a washed pet, a human forced in; dye_item and every machine_wash). The var
// blocks are generated (D111).

// tg: code/game/machinery/washing_machine.dm:2  GLOBAL_LIST_INIT(dye_registry, ...)
public partial class datum_controller_global_vars : datum_controller
{
    //dye registry, add dye colors and their resulting output here if you want the sprite to change instead of just the color.
    // tg: code/game/machinery/washing_machine.dm:2
    public DmList dye_registry;

    // tg: code/game/machinery/washing_machine.dm:2  (GLOBAL_MANAGED, __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldye_registry()
    {
        dye_registry = Dm.list(
            Dm.kv(DYE_REGISTRY_UNDER, Dm.list(
                Dm.kv(DYE_RED, typeof(obj_item_clothing_under_color_red)),
                Dm.kv(DYE_ORANGE, typeof(obj_item_clothing_under_color_orange)),
                Dm.kv(DYE_YELLOW, typeof(obj_item_clothing_under_color_yellow)),
                Dm.kv(DYE_GREEN, typeof(obj_item_clothing_under_color_green)),
                Dm.kv(DYE_BLUE, typeof(obj_item_clothing_under_color_blue)),
                Dm.kv(DYE_PURPLE, typeof(obj_item_clothing_under_color_lightpurple)),
                Dm.kv(DYE_BLACK, typeof(obj_item_clothing_under_color_black)),
                Dm.kv(DYE_WHITE, typeof(obj_item_clothing_under_color_white)),
                Dm.kv(DYE_RAINBOW, typeof(obj_item_clothing_under_color_rainbow)),
                Dm.kv(DYE_MIME, typeof(obj_item_clothing_under_rank_civilian_mime)),
                Dm.kv(DYE_CLOWN, typeof(obj_item_clothing_under_rank_civilian_clown)),
                Dm.kv(DYE_CHAP, typeof(obj_item_clothing_under_rank_civilian_chaplain)),
                Dm.kv(DYE_QM, typeof(obj_item_clothing_under_rank_cargo_qm)),
                Dm.kv(DYE_LAW, typeof(obj_item_clothing_under_suit_black)),
                Dm.kv(DYE_CAPTAIN, typeof(obj_item_clothing_under_rank_captain)),
                Dm.kv(DYE_HOP, typeof(obj_item_clothing_under_rank_civilian_head_of_personnel)),
                Dm.kv(DYE_HOS, typeof(obj_item_clothing_under_rank_security_head_of_security)),
                Dm.kv(DYE_CE, typeof(obj_item_clothing_under_rank_engineering_chief_engineer)),
                Dm.kv(DYE_RD, typeof(obj_item_clothing_under_rank_rnd_research_director)),
                Dm.kv(DYE_CMO, typeof(obj_item_clothing_under_rank_medical_chief_medical_officer)),
                Dm.kv(DYE_REDCOAT, typeof(obj_item_clothing_under_costume_redcoat)),
                Dm.kv(DYE_PRISONER, typeof(obj_item_clothing_under_rank_prisoner)),
                Dm.kv(DYE_SYNDICATE, typeof(obj_item_clothing_under_syndicate)),
                Dm.kv(DYE_CENTCOM, typeof(obj_item_clothing_under_rank_centcom_commander)),
                Dm.kv(DYE_COSMIC, typeof(obj_item_clothing_under_rank_station_trait_human_ai)))),
            Dm.kv(DYE_REGISTRY_JUMPSKIRT, Dm.list(
                Dm.kv(DYE_RED, typeof(obj_item_clothing_under_color_jumpskirt_red)),
                Dm.kv(DYE_ORANGE, typeof(obj_item_clothing_under_color_jumpskirt_orange)),
                Dm.kv(DYE_YELLOW, typeof(obj_item_clothing_under_color_jumpskirt_yellow)),
                Dm.kv(DYE_GREEN, typeof(obj_item_clothing_under_color_jumpskirt_green)),
                Dm.kv(DYE_BLUE, typeof(obj_item_clothing_under_color_jumpskirt_blue)),
                Dm.kv(DYE_PURPLE, typeof(obj_item_clothing_under_color_jumpskirt_lightpurple)),
                Dm.kv(DYE_BLACK, typeof(obj_item_clothing_under_color_jumpskirt_black)),
                Dm.kv(DYE_WHITE, typeof(obj_item_clothing_under_color_jumpskirt_white)),
                Dm.kv(DYE_RAINBOW, typeof(obj_item_clothing_under_color_jumpskirt_rainbow)),
                Dm.kv(DYE_MIME, typeof(obj_item_clothing_under_rank_civilian_mime_skirt)),
                Dm.kv(DYE_CHAP, typeof(obj_item_clothing_under_rank_civilian_chaplain_skirt)),
                Dm.kv(DYE_QM, typeof(obj_item_clothing_under_rank_cargo_qm_skirt)),
                Dm.kv(DYE_CAPTAIN, typeof(obj_item_clothing_under_rank_captain_skirt)),
                Dm.kv(DYE_HOP, typeof(obj_item_clothing_under_rank_civilian_head_of_personnel_skirt)),
                Dm.kv(DYE_HOS, typeof(obj_item_clothing_under_rank_security_head_of_security_skirt)),
                Dm.kv(DYE_CE, typeof(obj_item_clothing_under_rank_engineering_chief_engineer_skirt)),
                Dm.kv(DYE_RD, typeof(obj_item_clothing_under_rank_rnd_research_director_skirt)),
                Dm.kv(DYE_CMO, typeof(obj_item_clothing_under_rank_medical_chief_medical_officer_skirt)),
                Dm.kv(DYE_PRISONER, typeof(obj_item_clothing_under_rank_prisoner_skirt)))),
            Dm.kv(DYE_REGISTRY_GLOVES, Dm.list(
                Dm.kv(DYE_RED, typeof(obj_item_clothing_gloves_color_red)),
                Dm.kv(DYE_ORANGE, typeof(obj_item_clothing_gloves_color_orange)),
                Dm.kv(DYE_YELLOW, typeof(obj_item_clothing_gloves_color_yellow)),
                Dm.kv(DYE_GREEN, typeof(obj_item_clothing_gloves_color_green)),
                Dm.kv(DYE_BLUE, typeof(obj_item_clothing_gloves_color_blue)),
                Dm.kv(DYE_PURPLE, typeof(obj_item_clothing_gloves_color_purple)),
                Dm.kv(DYE_BLACK, typeof(obj_item_clothing_gloves_color_black)),
                Dm.kv(DYE_WHITE, typeof(obj_item_clothing_gloves_color_white)),
                Dm.kv(DYE_RAINBOW, typeof(obj_item_clothing_gloves_color_rainbow)),
                Dm.kv(DYE_MIME, typeof(obj_item_clothing_gloves_color_white)),
                Dm.kv(DYE_CLOWN, typeof(obj_item_clothing_gloves_color_rainbow)),
                Dm.kv(DYE_QM, typeof(obj_item_clothing_gloves_color_brown)),
                Dm.kv(DYE_CAPTAIN, typeof(obj_item_clothing_gloves_captain)),
                Dm.kv(DYE_HOP, typeof(obj_item_clothing_gloves_color_grey)),
                Dm.kv(DYE_HOS, typeof(obj_item_clothing_gloves_color_black_security)),
                Dm.kv(DYE_CE, typeof(obj_item_clothing_gloves_chief_engineer)),
                Dm.kv(DYE_RD, typeof(obj_item_clothing_gloves_color_grey)),
                Dm.kv(DYE_CMO, typeof(obj_item_clothing_gloves_latex_nitrile)),
                Dm.kv(DYE_REDCOAT, typeof(obj_item_clothing_gloves_color_white)),
                Dm.kv(DYE_SYNDICATE, typeof(obj_item_clothing_gloves_combat)),
                Dm.kv(DYE_CENTCOM, typeof(obj_item_clothing_gloves_combat)))),
            Dm.kv(DYE_REGISTRY_BANDANA, Dm.list(
                Dm.kv(DYE_RED, typeof(obj_item_clothing_mask_bandana_red)),
                Dm.kv(DYE_ORANGE, typeof(obj_item_clothing_mask_bandana_orange)),
                Dm.kv(DYE_YELLOW, typeof(obj_item_clothing_mask_bandana_gold)),
                Dm.kv(DYE_GREEN, typeof(obj_item_clothing_mask_bandana_green)),
                Dm.kv(DYE_BLUE, typeof(obj_item_clothing_mask_bandana_blue)),
                Dm.kv(DYE_PURPLE, typeof(obj_item_clothing_mask_bandana_purple)),
                Dm.kv(DYE_BLACK, typeof(obj_item_clothing_mask_bandana_black)),
                Dm.kv(DYE_WHITE, typeof(obj_item_clothing_mask_bandana_white)))),
            Dm.kv(DYE_REGISTRY_SNEAKERS, Dm.list(
                Dm.kv(DYE_RED, typeof(obj_item_clothing_shoes_sneakers_red)),
                Dm.kv(DYE_ORANGE, typeof(obj_item_clothing_shoes_sneakers_orange)),
                Dm.kv(DYE_YELLOW, typeof(obj_item_clothing_shoes_sneakers_yellow)),
                Dm.kv(DYE_GREEN, typeof(obj_item_clothing_shoes_sneakers_green)),
                Dm.kv(DYE_BLUE, typeof(obj_item_clothing_shoes_sneakers_blue)),
                Dm.kv(DYE_PURPLE, typeof(obj_item_clothing_shoes_sneakers_purple)),
                Dm.kv(DYE_BLACK, typeof(obj_item_clothing_shoes_sneakers_black)),
                Dm.kv(DYE_WHITE, typeof(obj_item_clothing_shoes_sneakers_white)),
                Dm.kv(DYE_RAINBOW, typeof(obj_item_clothing_shoes_sneakers_rainbow)),
                Dm.kv(DYE_MIME, typeof(obj_item_clothing_shoes_sneakers_black)),
                Dm.kv(DYE_CLOWN, typeof(obj_item_clothing_shoes_sneakers_rainbow)),
                Dm.kv(DYE_QM, typeof(obj_item_clothing_shoes_sneakers_brown)),
                Dm.kv(DYE_CAPTAIN, typeof(obj_item_clothing_shoes_sneakers_brown)),
                Dm.kv(DYE_HOP, typeof(obj_item_clothing_shoes_sneakers_brown)),
                Dm.kv(DYE_CE, typeof(obj_item_clothing_shoes_sneakers_brown)),
                Dm.kv(DYE_RD, typeof(obj_item_clothing_shoes_sneakers_brown)),
                Dm.kv(DYE_CMO, typeof(obj_item_clothing_shoes_sneakers_brown)),
                Dm.kv(DYE_SYNDICATE, typeof(obj_item_clothing_shoes_combat)),
                Dm.kv(DYE_CENTCOM, typeof(obj_item_clothing_shoes_combat)))),
            Dm.kv(DYE_REGISTRY_FANNYPACK, Dm.list(
                Dm.kv(DYE_RED, typeof(obj_item_storage_belt_fannypack_red)),
                Dm.kv(DYE_ORANGE, typeof(obj_item_storage_belt_fannypack_orange)),
                Dm.kv(DYE_YELLOW, typeof(obj_item_storage_belt_fannypack_yellow)),
                Dm.kv(DYE_GREEN, typeof(obj_item_storage_belt_fannypack_green)),
                Dm.kv(DYE_BLUE, typeof(obj_item_storage_belt_fannypack_blue)),
                Dm.kv(DYE_PURPLE, typeof(obj_item_storage_belt_fannypack_purple)),
                Dm.kv(DYE_BLACK, typeof(obj_item_storage_belt_fannypack_black)),
                Dm.kv(DYE_WHITE, typeof(obj_item_storage_belt_fannypack_white)),
                Dm.kv(DYE_SYNDICATE, typeof(obj_item_storage_belt_military)))),
            Dm.kv(DYE_REGISTRY_BEDSHEET, Dm.list(
                Dm.kv(DYE_RED, typeof(obj_item_bedsheet_red)),
                Dm.kv(DYE_ORANGE, typeof(obj_item_bedsheet_orange)),
                Dm.kv(DYE_YELLOW, typeof(obj_item_bedsheet_yellow)),
                Dm.kv(DYE_GREEN, typeof(obj_item_bedsheet_green)),
                Dm.kv(DYE_BLUE, typeof(obj_item_bedsheet_blue)),
                Dm.kv(DYE_PURPLE, typeof(obj_item_bedsheet_purple)),
                Dm.kv(DYE_BLACK, typeof(obj_item_bedsheet_black)),
                Dm.kv(DYE_WHITE, typeof(obj_item_bedsheet)),
                Dm.kv(DYE_RAINBOW, typeof(obj_item_bedsheet_rainbow)),
                Dm.kv(DYE_MIME, typeof(obj_item_bedsheet_mime)),
                Dm.kv(DYE_CLOWN, typeof(obj_item_bedsheet_clown)),
                Dm.kv(DYE_CHAP, typeof(obj_item_bedsheet_chaplain)),
                Dm.kv(DYE_QM, typeof(obj_item_bedsheet_qm)),
                Dm.kv(DYE_LAW, typeof(obj_item_bedsheet_black)),
                Dm.kv(DYE_CAPTAIN, typeof(obj_item_bedsheet_captain)),
                Dm.kv(DYE_HOP, typeof(obj_item_bedsheet_hop)),
                Dm.kv(DYE_HOS, typeof(obj_item_bedsheet_hos)),
                Dm.kv(DYE_CE, typeof(obj_item_bedsheet_ce)),
                Dm.kv(DYE_RD, typeof(obj_item_bedsheet_rd)),
                Dm.kv(DYE_CMO, typeof(obj_item_bedsheet_cmo)),
                Dm.kv(DYE_COSMIC, typeof(obj_item_bedsheet_cosmos)),
                Dm.kv(DYE_SYNDICATE, typeof(obj_item_bedsheet_syndie)),
                Dm.kv(DYE_CENTCOM, typeof(obj_item_bedsheet_centcom)))),
            Dm.kv(DYE_REGISTRY_DOUBLE_BEDSHEET, Dm.list(
                Dm.kv(DYE_RED, typeof(obj_item_bedsheet_red_double)),
                Dm.kv(DYE_ORANGE, typeof(obj_item_bedsheet_orange_double)),
                Dm.kv(DYE_YELLOW, typeof(obj_item_bedsheet_yellow_double)),
                Dm.kv(DYE_GREEN, typeof(obj_item_bedsheet_green_double)),
                Dm.kv(DYE_BLUE, typeof(obj_item_bedsheet_blue_double)),
                Dm.kv(DYE_PURPLE, typeof(obj_item_bedsheet_purple_double)),
                Dm.kv(DYE_BLACK, typeof(obj_item_bedsheet_black_double)),
                Dm.kv(DYE_WHITE, typeof(obj_item_bedsheet_double)),
                Dm.kv(DYE_RAINBOW, typeof(obj_item_bedsheet_rainbow_double)),
                Dm.kv(DYE_MIME, typeof(obj_item_bedsheet_mime_double)),
                Dm.kv(DYE_CLOWN, typeof(obj_item_bedsheet_clown_double)),
                Dm.kv(DYE_CHAP, typeof(obj_item_bedsheet_chaplain_double)),
                Dm.kv(DYE_QM, typeof(obj_item_bedsheet_qm_double)),
                Dm.kv(DYE_LAW, typeof(obj_item_bedsheet_black_double)),
                Dm.kv(DYE_CAPTAIN, typeof(obj_item_bedsheet_captain_double)),
                Dm.kv(DYE_HOP, typeof(obj_item_bedsheet_hop_double)),
                Dm.kv(DYE_HOS, typeof(obj_item_bedsheet_hos_double)),
                Dm.kv(DYE_CE, typeof(obj_item_bedsheet_ce_double)),
                Dm.kv(DYE_RD, typeof(obj_item_bedsheet_rd_double)),
                Dm.kv(DYE_CMO, typeof(obj_item_bedsheet_cmo_double)),
                Dm.kv(DYE_COSMIC, typeof(obj_item_bedsheet_cosmos_double)),
                Dm.kv(DYE_SYNDICATE, typeof(obj_item_bedsheet_syndie_double)),
                Dm.kv(DYE_CENTCOM, typeof(obj_item_bedsheet_centcom_double)))),
            Dm.kv(DYE_LAWYER_SPECIAL, Dm.list(
                Dm.kv(DYE_COSMIC, typeof(obj_item_clothing_under_rank_civilian_lawyer_galaxy)),
                Dm.kv(DYE_SYNDICATE, typeof(obj_item_clothing_under_rank_civilian_lawyer_galaxy_red)))),
            Dm.kv(DYE_LAWYER_SPECIAL_SKIRT, Dm.list(
                Dm.kv(DYE_COSMIC, typeof(obj_item_clothing_under_rank_civilian_lawyer_galaxy_skirt)),
                Dm.kv(DYE_SYNDICATE, typeof(obj_item_clothing_under_rank_civilian_lawyer_galaxy_red_skirt)))));
        Dm.append(ref gvars_datum_init_order, "dye_registry");
    }
}

// tg: code/game/machinery/washing_machine.dm:176
public partial class obj_machinery_washing_machine : obj_machinery
{
    // tg: code/game/machinery/washing_machine.dm:195
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!Dm.truthy(busy))
            Dm.append(ref dot, span_notice(I18n.T("<b>Right-click</b> with an empty hand to start a wash cycle.")));
        return dot;
    }

    // tg: code/game/machinery/washing_machine.dm:200
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        float total_volume = 0;
        foreach (obj_item_reagent_containers_cup_beaker beaker in Dm.each<obj_item_reagent_containers_cup_beaker>(component_parts))
            total_volume += ((datum_reagents)beaker.reagents).maximum_volume;
        max_wash_capacity = floor(total_volume / 20);

        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
            time_to_wash = 20 * SECONDS / Dm.num(servo.tier);
        return dot;
    }

    // tg: code/game/machinery/washing_machine.dm:210
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(busy))
        {
            animate(this, Dm.named("transform", matrix()), Dm.named("time", 2));
            return PROCESS_KILL;
        }
        if (Dm.truthy(anchored))
        {
            if (SPT_PROB(2.5f, seconds_per_tick))
            {
                var M = new matrix();
                M.Translate(rand(-1, 1), rand(0, 1));
                animate(this, Dm.named("transform", M), Dm.named("time", 1));
                animate(Dm.named("transform", matrix()), Dm.named("time", 1));
            }
        }
        else
        {
            if (SPT_PROB(0.5f, seconds_per_tick))
                Waiting.NoSleep(step(this, Dm.num(pick(GLOB.cardinals))));
            var M = new matrix();
            M.Translate(rand(-3, 3), rand(-1, 3));
            animate(this, Dm.named("transform", M), Dm.named("time", 2));
        }
        return null;
    }

    // tg: code/game/machinery/washing_machine.dm:227
    public override async DmTask<object> wash(object clean_types = null, params object[] __extra)
    {
        object dot = await base.wash(clean_types, __extra);
        if (!Dm.truthy(busy) && Dm.truthy(bloody_mess) && Dm.truthy(Dm.bitand(clean_types, CLEAN_TYPE_BLOOD)))
        {
            bloody_mess = FALSE;
            update_appearance();
            dot = Dm.bitor(dot, COMPONENT_CLEANED);
        }
        return dot;
    }

    // tg: code/game/machinery/washing_machine.dm:234
    public virtual async DmTask wash_cycle(mob user = null)
    {
        foreach (atom_movable AM in Dm.each<atom_movable>(contents.Copy()))
        {
            await AM.wash(CLEAN_WASH);
            await AM.machine_wash(this, user);
        }

        //if we had the ability to brainwash, remove that now
        REMOVE_TRAIT(this, TRAIT_BRAINWASHING, SKILLCHIP_TRAIT);
        busy = FALSE;
        if (color_source != null)
        {
            qdel(color_source);
            color_source = null;
        }
        update_appearance();
        await use_energy(active_power_usage);
    }

    // tg: code/game/machinery/washing_machine.dm:333
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        return await container_resist_act(user);
    }

    // tg: code/game/machinery/washing_machine.dm:336
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        if (!Dm.truthy(busy))
        {
            add_fingerprint(user);
            await open_machine();
        }
        return null;
    }

    // tg: code/game/machinery/washing_machine.dm:341
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(busy))
        {
            icon_state = Dm.text("wm_running_[]", bloody_mess);
            return await base.update_icon_state(__extra);
        }
        if (Dm.truthy(bloody_mess))
        {
            icon_state = Dm.text("wm_[]_blood", state_open);
            return await base.update_icon_state(__extra);
        }

        float full = Dm.truthy(total_load) ? 1 : 0;
        icon_state = Dm.text("wm_[]_[]", state_open, full);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/washing_machine.dm:353
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (Dm.truthy(panel_open))
            Dm.append(ref dot, "wm_panel");
        return dot;
    }

    // tg: code/game/machinery/washing_machine.dm:358
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(panel_open) || Dm.truthy(busy))
            return NONE;
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/washing_machine.dm:364
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(state_open))
            return NONE;

        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/washing_machine.dm:370
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        obj_item item = tool;
        if (Dm.truthy(user.combat_mode))
            return NONE;
        if (!Dm.truthy(state_open))
        {
            to_chat(user, span_warning(I18n.T("Open the door first!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(bloody_mess))
        {
            to_chat(user, span_warning(I18n.T("[] must be cleaned up first!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (total_load >= Dm.num(max_wash_capacity))
        {
            to_chat(user, span_warning(I18n.T("The washing machine is full!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(item, this)))
        {
            to_chat(user, span_warning(I18n.T("\\The [] is stuck to your hand, you cannot put it in the washing machine!", item)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(item.dye_color))
            color_source = item;
        total_load++;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/washing_machine.dm:392
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        var living = user as mob_living;
        if (Dm.truthy(busy))
        {
            to_chat(living, span_warning(I18n.T("[] is busy!", this)));
            return dot;
        }

        if (living.pulling != null && isliving(living.pulling))
        {
            var victim = (mob_living)living.pulling;
            if (victim.buckled != null || Dm.truthy(victim.has_buckled_mobs()))
                return dot;
            if (Dm.truthy(state_open))
            {
                if (victim is mob_living_basic_pet)
                {
                    await victim.forceMove(this);
                    update_appearance();
                }
                else if (ishuman(victim))
                {
                    if (living.grab_state < GRAB_AGGRESSIVE)
                    {
                        balloon_alert(living, "grab harder!");
                        return dot;
                    }

                    victim.visible_message(span_danger(I18n.T("[] is trying to force [] into []!", living, victim, this)));
                    log_game(Dm.text("[] is forcing [] into a washing machine", key_name_and_tag(living), key_name_and_tag(victim)));
                    if (!Dm.truthy(await do_after(living, 3 * SECONDS, target: this, timed_action_flags: IGNORE_HELD_ITEM, extra_checks: Dm.@new<datum_callback>(this, nameof(check_aggro_grab), living))))
                        return dot;
                    await victim.forceMove(this);
                    update_appearance();
                }
            }
            return dot;
        }

        if (!Dm.truthy(state_open))
            await open_machine();
        else
        {
            state_open = FALSE; //close the door
            update_appearance();
        }
        return dot;
    }

    // tg: code/game/machinery/washing_machine.dm:427
    public virtual object check_aggro_grab(mob_living user = null)
    {
        return user.grab_state >= GRAB_AGGRESSIVE;
    }

    // tg: code/game/machinery/washing_machine.dm:430
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;

        if (!Dm.truthy(await user.can_perform_action(this, ALLOW_SILICON_REACH)))
            return SECONDARY_ATTACK_CONTINUE_CHAIN;
        if (Dm.truthy(busy))
        {
            to_chat(user, span_warning(I18n.T("[] is busy!", this)));
            return SECONDARY_ATTACK_CONTINUE_CHAIN;
        }
        if (Dm.truthy(state_open))
        {
            to_chat(user, span_warning(I18n.T("Close the door first!")));
            return SECONDARY_ATTACK_CONTINUE_CHAIN;
        }
        if (Dm.truthy(bloody_mess))
        {
            to_chat(user, span_warning(I18n.T("[] must be cleaned up first!", this)));
            return SECONDARY_ATTACK_CONTINUE_CHAIN;
        }
        busy = TRUE;
        if (HAS_TRAIT(user, TRAIT_BRAINWASHING))
            ADD_TRAIT(this, TRAIT_BRAINWASHING, SKILLCHIP_TRAIT);
        update_appearance();
        _addtimer(Dm.@new<datum_callback>(this, nameof(wash_cycle), user), Dm.num(time_to_wash), file: "code/game/machinery/washing_machine.dm", line: 450);
        START_PROCESSING(SSfastprocess, this);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/machinery/washing_machine.dm:454
    public override async DmTask<object> attack_ai_secondary(mob user = null, DmList modifiers = null)
    {
        return await attack_hand_secondary(user, modifiers);
    }

    // tg: code/game/machinery/washing_machine.dm:457
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        Dm.@new<obj_item_stack_sheet_iron>(drop_location(), 2);
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/game/machinery/washing_machine.dm:460
    public override async DmTask<object> open_machine(float? drop = null, float? density_to_set = null)
    {
        object dot = await base.open_machine(drop ?? TRUE, density_to_set ?? FALSE);
        set_density(TRUE); //because machinery/open_machine() sets it to FALSE
        color_source = null;
        total_load = 0;
        return dot;
    }
}

// tg: code/game/machinery/washing_machine.dm:249
public partial class obj_item : obj
{
    // tg: code/game/machinery/washing_machine.dm:249
    public virtual async DmTask<object> dye_item(object dye_color = null, object dye_key_override = null)
    {
        object dye_key_selector = Dm.truthy(dye_key_override) ? dye_key_override : dying_key;
        if (Dm.truthy(undyeable) || !Dm.truthy(dye_key_selector))
            return FALSE;
        if (!Dm.truthy(GLOB.dye_registry[dye_key_selector]))
        {
            log_runtime(Dm.text("Item [] just tried to be dyed with an invalid registry key: []", type, dye_key_selector));
            return FALSE;
        }
        var target_type = (GLOB.dye_registry[dye_key_selector] as DmList)[dye_color] as Type;
        if (target_type == null)
            return FALSE;

        if (Dm.initial(target_type, (obj_item p) => p.greyscale_config) != null || Dm.truthy(Dm.initial(target_type, (obj_item p) => p.greyscale_colors)))
        {
            object new_config = Dm.initial(target_type, (obj_item p) => p.greyscale_config);
            Type new_worn_config = Dm.initial(target_type, (obj_item p) => p.greyscale_config_worn);
            Type new_inhand_left = Dm.initial(target_type, (obj_item p) => p.greyscale_config_inhand_left);
            Type new_inhand_right = Dm.initial(target_type, (obj_item p) => p.greyscale_config_inhand_right);

            if (new_config != null || new_worn_config != null || new_inhand_left != null || new_inhand_right != null)
            {
                object colors = Dm.truthy(Dm.initial(target_type, (obj_item p) => p.greyscale_colors)) ? Dm.initial(target_type, (obj_item p) => p.greyscale_colors) : COLOR_WHITE;
                await set_greyscale(colors, new_config, new_worn_config, new_inhand_left, new_inhand_right);
            }
        }
        else
        {
            icon = Dm.initial(target_type, (obj_item p) => p.icon);
            lefthand_file = Dm.initial(target_type, (obj_item p) => p.lefthand_file);
            righthand_file = Dm.initial(target_type, (obj_item p) => p.righthand_file);
            worn_icon = Dm.initial(target_type, (obj_item p) => p.worn_icon);
        }

        object post_init = Dm.initial(target_type, (obj_item p) => p.post_init_icon_state);
        icon_state = Dm.truthy(post_init) ? Dm.str(post_init) : Dm.initial(target_type, (obj_item p) => p.icon_state);
        inhand_icon_state = Dm.initial(target_type, (obj_item p) => p.inhand_icon_state);
        worn_icon_state = Dm.initial(target_type, (obj_item p) => p.worn_icon_state);
        inhand_x_dimension = Dm.initial(target_type, (obj_item p) => p.inhand_x_dimension);
        inhand_y_dimension = Dm.initial(target_type, (obj_item p) => p.inhand_y_dimension);
        name = Dm.initial(target_type, (obj_item p) => p.name);
        desc = Dm.text("[] The colors look a little dodgy.", Dm.initial(target_type, (obj_item p) => p.desc));
        return target_type; //successfully "appearance copy" dyed something; returns the target type as a hacky way of extending
    }

    // tg: code/game/machinery/washing_machine.dm:313
    public override async DmTask machine_wash(obj_machinery_washing_machine washer = null, params object[] __extra)
    {
        if (washer.color_source != null)
            await dye_item(washer.color_source.dye_color);
    }
}

// tg: code/game/machinery/washing_machine.dm:291
public partial class atom_movable : atom
{
    //what happens to this object when washed inside a washing machine
    // tg: code/game/machinery/washing_machine.dm:291
    [DmParams("washer", "user")]
    public virtual DmTask machine_wash(obj_machinery_washing_machine washer = null, params object[] __extra)
    {
        return DmTask.CompletedTask;
    }
}

// tg: code/game/machinery/washing_machine.dm:294
public partial class obj_item_stack_sheet_hairlesshide : obj_item_stack_sheet
{
    // tg: code/game/machinery/washing_machine.dm:294
    public override async DmTask machine_wash(obj_machinery_washing_machine washer = null, params object[] __extra)
    {
        Dm.@new<obj_item_stack_sheet_wethide>(drop_location(), amount);
        qdel(this);
        await DmTask.CompletedTask;
    }
}

// tg: code/game/machinery/washing_machine.dm:298
public partial class obj_item_clothing_suit_hooded_ian_costume : obj_item_clothing_suit_hooded
{
    // tg: code/game/machinery/washing_machine.dm:298
    public override async DmTask machine_wash(obj_machinery_washing_machine washer = null, params object[] __extra)
    {
        Dm.@new<obj_item_food_meat_slab_corgi>(loc);
        qdel(this);
        await DmTask.CompletedTask;
    }
}

// tg: code/game/machinery/washing_machine.dm:302
public partial class mob_living_basic_pet : mob_living_basic
{
    // tg: code/game/machinery/washing_machine.dm:302
    public override async DmTask machine_wash(obj_machinery_washing_machine washer = null, params object[] __extra)
    {
        washer.bloody_mess = TRUE;
        investigate_log("has been gibbed by a washing machine.", INVESTIGATE_DEATHS);
        await gib();
    }
}

// tg: code/game/machinery/washing_machine.dm:307
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/game/machinery/washing_machine.dm:307
    public override async DmTask machine_wash(obj_machinery_washing_machine washer = null, params object[] __extra)
    {
        var user = Dm.arg(__extra, 0) as mob;
        await adjust_wet_stacks(8);
        adjust_disgust(40, DISGUST_LEVEL_VERYDISGUSTED);
        await adjust_oxy_loss(12);
        log_combat(user, this, "machine washed (oxy)");
    }
}

// M2 F1: the undersuit, translated in modules/clothing/under/_under.cs; its [DmPath] is here, in the earliest .dme file that declares the
// type (rules 2.3)
// tg: code/game/machinery/washing_machine.dm:317
[DmPath("/obj/item/clothing/under")]
public partial class obj_item_clothing_under : obj_item_clothing
{
    // tg: code/game/machinery/washing_machine.dm:317
    public override async DmTask<object> dye_item(object dye_color = null, object dye_key_override = null)
    {
        object dot = await base.dye_item(dye_color, dye_key_override);
        if (Dm.truthy(dot))
        {
            var U = (Type)dot;
            can_adjust = Dm.initial(U, (obj_item_clothing_under p) => p.can_adjust);
            if (!Dm.truthy(can_adjust) && adjusted == ALT_STYLE) //we deadjust the uniform if it's now unadjustable
                toggle_jumpsuit_adjust();
        }
        return dot;
    }
}

// the earliest .dme file that declares the type (rules 2.3): its [DmPath] is here
// tg: code/game/machinery/washing_machine.dm:325
[DmPath("/obj/item/mob_holder")]
public partial class obj_item_mob_holder : obj_item
{
    // tg: code/game/machinery/washing_machine.dm:325
    public override async DmTask machine_wash(obj_machinery_washing_machine washer = null, params object[] __extra)
    {
        await base.machine_wash(washer, __extra);
        await held_mob.machine_wash(washer);
    }
}

// tg: code/game/machinery/washing_machine.dm:329
public partial class obj_item_clothing_shoes_sneakers_orange : obj_item_clothing_shoes_sneakers
{
    // tg: code/game/machinery/washing_machine.dm:329
    public override async DmTask machine_wash(obj_machinery_washing_machine washer = null, params object[] __extra)
    {
        if (attached_cuffs != null)
            await attached_cuffs.forceMove(loc);
        await base.machine_wash(washer, __extra);
    }
}
