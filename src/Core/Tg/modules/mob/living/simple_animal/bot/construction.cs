// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/bot/construction.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3). The vim's mech is not live: the vim's last step makes an inert mecha, as a row (the conveyor stack the repairbot takes is live since the conveyors, 2026-11-04).

//Bot Construction

// tg: code/modules/mob/living/simple_animal/bot/construction.dm:3
public partial class obj_item_bot_assembly : obj_item
{
    // tg: code/modules/mob/living/simple_animal/bot/construction.dm:14
    public override async DmTask<object> nameformat(object input = null, mob_living user = null)
    {
        created_name = input;
        return input;
    }

    // tg: code/modules/mob/living/simple_animal/bot/construction.dm:18
    public override async DmTask rename_reset()
    {
        created_name = Dm.initial(this, (obj_item_bot_assembly p) => p.created_name);
    }

    /**
     * Checks if the user can finish constructing a bot with a given item.
     *
     * Arguments:
     * * tool - Item to be used
     * * user - Mob doing the construction
     * * drop_item - Whether or no the item should be dropped; defaults to 1. Should be set to 0 if the item is a tool, stack, or otherwise doesn't need to be dropped. If not set to 0, item must be deleted afterwards.
     */
    // tg: code/modules/mob/living/simple_animal/bot/construction.dm:29
    public virtual async DmTask<object> can_finish_build(obj_item tool = null, mob user = null, float? drop_item = null)
    {
        drop_item ??= 1;
        if (loc is obj_item_storage_backpack)
        {
            to_chat(user, span_warning(Dm.text("You must take [] out of [] first!", this, loc)));
            return FALSE;
        }
        if (tool == null || user == null || (Dm.truthy(drop_item) && !Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool))))
            return FALSE;
        return TRUE;
    }
}

// Cleanbot assembly
// tg: code/modules/mob/living/simple_animal/bot/construction.dm:38
public partial class obj_item_bot_assembly_cleanbot : obj_item_bot_assembly
{
    // tg: code/modules/mob/living/simple_animal/bot/construction.dm:46
    [DmParams("mapload", "new_bucket")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_item_reagent_containers_cup_bucket new_bucket = Dm.arg<obj_item_reagent_containers_cup_bucket>(__extra, 0);
        if (new_bucket == null)
            new_bucket = Dm.@new<obj_item_reagent_containers_cup_bucket>();
        Waiting.NoSleep(new_bucket.forceMove(this));
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/mob/living/simple_animal/bot/construction.dm:52
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (arrived is obj_item_reagent_containers_cup_bucket bucket)
        {
            if (bucket_obj != null && bucket_obj != arrived)
                qdel(bucket_obj);
            bucket_obj = bucket;
        }
        return await base.Entered(arrived, old_loc, old_locs);
    }

    // tg: code/modules/mob/living/simple_animal/bot/construction.dm:59
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == bucket_obj)
            bucket_obj = null;
        return await base.Exited(gone, direction);
    }

    // tg: code/modules/mob/living/simple_animal/bot/construction.dm:65
    public override object Destroy(float? force = null)
    {
        qdel(bucket_obj);
        bucket_obj = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/simple_animal/bot/construction.dm:70
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_bodypart_arm_left_robot && tool is not obj_item_bodypart_arm_right_robot)
            return NONE;

        if (!Dm.truthy(await can_finish_build(tool, user)))
            return ITEM_INTERACT_BLOCKING;

        mob_living_basic_bot_cleanbot bot = await Dm.new_async<mob_living_basic_bot_cleanbot>(drop_location());
        await bucket_obj.forceMove(bot);
        bot.name = created_name as string;
        bot.robot_arm = tool.type;
        to_chat(user, span_notice(Dm.text("You add [] to []. Beep boop!", tool, this)));
        qdel(tool);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// Edbot Assembly
// tg: code/modules/mob/living/simple_animal/bot/construction.dm:87
public partial class obj_item_bot_assembly_ed209 : obj_item_bot_assembly
{
    // tg: code/modules/mob/living/simple_animal/bot/construction.dm:96
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        switch (Dm.num(build_step))
        {
            case ASSEMBLY_FIRST_STEP: case ASSEMBLY_SECOND_STEP:
                if (tool is not obj_item_bodypart_leg_left_robot && tool is not obj_item_bodypart_leg_right_robot)
                    return NONE;
                if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
                    return ITEM_INTERACT_BLOCKING;
                to_chat(user, span_notice(Dm.text("You add [] to [].", tool, this)));
                qdel(tool);
                name = "legs/frame assembly";
                if (Dm.num(build_step) == ASSEMBLY_FIRST_STEP)
                {
                    inhand_icon_state = "ed209_leg";
                    icon_state = "ed209_leg";
                }
                else
                {
                    inhand_icon_state = "ed209_legs";
                    icon_state = "ed209_legs";
                }
                build_step = Dm.num(build_step) + 1;
                return ITEM_INTERACT_SUCCESS;

            case ASSEMBLY_THIRD_STEP:
                if (tool is not obj_item_clothing_suit_armor_vest)
                    return NONE;
                if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
                    return ITEM_INTERACT_BLOCKING;
                to_chat(user, span_notice(Dm.text("You add [] to [].", tool, this)));
                qdel(tool);
                name = "vest/legs/frame assembly";
                inhand_icon_state = "ed209_shell";
                icon_state = "ed209_shell";
                build_step = Dm.num(build_step) + 1;
                return ITEM_INTERACT_SUCCESS;

            case ASSEMBLY_FOURTH_STEP:
                if (!Dm.eq(tool.tool_behaviour, TOOL_WELDER))
                    return NONE;
                if (!Dm.truthy(await tool.use_tool(this, user, 0, volume: 40)))
                    return ITEM_INTERACT_BLOCKING;
                name = "shielded frame assembly";
                to_chat(user, span_notice(Dm.text("You weld the vest to [].", this)));
                build_step = Dm.num(build_step) + 1;
                return ITEM_INTERACT_SUCCESS;

            case ASSEMBLY_FIFTH_STEP:
                if (tool is not obj_item_clothing_head_helmet_sec)
                    return NONE;
                if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
                    return ITEM_INTERACT_BLOCKING;
                to_chat(user, span_notice(Dm.text("You add [] to [].", tool, this)));
                qdel(tool);
                name = "covered and shielded frame assembly";
                inhand_icon_state = "ed209_hat";
                icon_state = "ed209_hat";
                build_step = Dm.num(build_step) + 1;
                return ITEM_INTERACT_SUCCESS;

            case ASSEMBLY_SIXTH_STEP:
                if (tool is not obj_item_assembly_prox_sensor)
                    return NONE;
                if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
                    return ITEM_INTERACT_BLOCKING;
                build_step = Dm.num(build_step) + 1;
                to_chat(user, span_notice(Dm.text("You add [] to [].", tool, this)));
                qdel(tool);
                name = "covered, shielded and sensored frame assembly";
                inhand_icon_state = "ed209_prox";
                icon_state = "ed209_prox";
                return ITEM_INTERACT_SUCCESS;

            case ASSEMBLY_SEVENTH_STEP:
                if (tool is not obj_item_stack_cable_coil coil)
                    return NONE;
                if (Dm.num(coil.get_amount()) < 1)
                {
                    to_chat(user, span_warning("You need one length of cable to wire the ED-209!"));
                    return ITEM_INTERACT_BLOCKING;
                }
                to_chat(user, span_notice(Dm.text("You start to wire []...", this)));
                if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: this)))
                    return ITEM_INTERACT_BLOCKING;
                if (Dm.num(coil.get_amount()) < 1 || Dm.num(build_step) != ASSEMBLY_SEVENTH_STEP)
                    return ITEM_INTERACT_BLOCKING;
                await coil.use(1);
                to_chat(user, span_notice(Dm.text("You wire [].", this)));
                name = "wired ED-209 assembly";
                build_step = Dm.num(build_step) + 1;
                return ITEM_INTERACT_SUCCESS;

            case ASSEMBLY_EIGHTH_STEP:
                if (tool is not obj_item_gun_energy_disabler)
                    return NONE;
                if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
                    return ITEM_INTERACT_BLOCKING;
                name = Dm.text("[] ED-209 assembly", tool.name);
                to_chat(user, span_notice(Dm.text("You add [] to [].", tool, this)));
                inhand_icon_state = "ed209_taser";
                icon_state = "ed209_taser";
                qdel(tool);
                build_step = Dm.num(build_step) + 1;
                return ITEM_INTERACT_SUCCESS;

            case ASSEMBLY_NINTH_STEP:
                if (!Dm.eq(tool.tool_behaviour, TOOL_SCREWDRIVER))
                    return NONE;
                to_chat(user, span_notice("You start attaching the gun to the frame..."));
                if (!Dm.truthy(await tool.use_tool(this, user, 40, volume: 100)))
                    return ITEM_INTERACT_BLOCKING;
                mob_living_basic_bot_secbot_ed209 new_bot = await Dm.new_async<mob_living_basic_bot_secbot_ed209>(drop_location());
                new_bot.name = created_name as string;
                to_chat(user, span_notice("You complete the ED-209."));
                qdel(this);
                return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }
}

// Repairbot assemblies
// tg: code/modules/mob/living/simple_animal/bot/construction.dm:208
public partial class obj_item_bot_assembly_repairbot : obj_item_bot_assembly
{
    // tg: code/modules/mob/living/simple_animal/bot/construction.dm:219
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/bot/construction.dm:223
    public virtual void set_color(object new_color = null)
    {
        Waiting.NoSleep(add_atom_colour(new_color, FIXED_COLOUR_PRIORITY));
        toolbox_color = new_color as string;
    }

    // tg: code/modules/mob/living/simple_animal/bot/construction.dm:227
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        if (Dm.num(build_step) == ASSEMBLY_FIRST_STEP)
            desc = "It's a toolbox with a giant monitor sticking out!.";
        else
            desc = Dm.initial(this, (obj_item_bot_assembly_repairbot p) => p.desc);
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/bot/construction.dm:235
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (Dm.num(build_step) >= ASSEMBLY_FIRST_STEP)
            Dm.append(ref dot, mutable_appearance(icon, "repairbot_base_sensor", appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)));
        if (Dm.num(build_step) >= ASSEMBLY_SECOND_STEP)
            Dm.append(ref dot, mutable_appearance(icon, "repairbot_base_arms", appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)));
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/bot/construction.dm:242
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        switch (Dm.num(build_step))
        {
            case ASSEMBLY_FIRST_STEP:
                if (tool is not obj_item_bodypart_arm_left_robot && tool is not obj_item_bodypart_arm_right_robot)
                    return NONE;
                if (!Dm.truthy(await can_finish_build(tool, user)))
                    return ITEM_INTERACT_BLOCKING;
                build_step = Dm.num(build_step) + 1;
                to_chat(user, span_notice(Dm.text("You add [] to []. Boop beep!", tool, this)));
                qdel(tool);
                update_appearance();
                return ITEM_INTERACT_SUCCESS;

            case ASSEMBLY_SECOND_STEP:
                if (tool is not obj_item_stack_conveyor)
                    return NONE;
                if (!Dm.truthy(await can_finish_build(tool, user)))
                    return ITEM_INTERACT_BLOCKING;
                mob_living_basic_bot_repairbot repair = await Dm.new_async<mob_living_basic_bot_repairbot>(drop_location());
                repair.name = created_name as string;
                repair.toolbox = toolbox;
                repair.set_color(toolbox_color);
                to_chat(user, span_notice(Dm.text("You add [] to []. Boop beep!", tool, this)));
                obj_item_stack crafting_stack = (obj_item_stack)tool;
                await crafting_stack.use(1);
                qdel(this);
                return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }
}

// Medbot Assembly
// tg: code/modules/mob/living/simple_animal/bot/construction.dm:271
public partial class obj_item_bot_assembly_medbot : obj_item_bot_assembly
{
    // tg: code/modules/mob/living/simple_animal/bot/construction.dm:281
    public virtual void set_skin(object skin = null)
    {
        this.skin = skin as string;
        if (Dm.truthy(skin))
            icon_state = Dm.text("[]_[]", base_icon_state, skin);
    }

    // tg: code/modules/mob/living/simple_animal/bot/construction.dm:286
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        switch (Dm.num(build_step))
        {
            case ASSEMBLY_FIRST_STEP:
                if (tool is not obj_item_healthanalyzer)
                    return NONE;
                if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
                    return ITEM_INTERACT_BLOCKING;
                healthanalyzer = tool.type;
                to_chat(user, span_notice(Dm.text("You add [] to [].", tool, this)));
                qdel(tool);
                name = "first aid/robot arm/health analyzer assembly";
                add_overlay(Dm.text("[]_analyzer", base_icon_state));
                build_step = Dm.num(build_step) + 1;
                return ITEM_INTERACT_SUCCESS;

            case ASSEMBLY_SECOND_STEP:
                if (tool is not obj_item_assembly_prox_sensor)
                    return NONE;
                if (!Dm.truthy(await can_finish_build(tool, user)))
                    return ITEM_INTERACT_BLOCKING;
                qdel(tool);
                mob_living_basic_bot_medbot medbot = await Dm.new_async<mob_living_basic_bot_medbot>(drop_location(), skin);
                to_chat(user, span_notice("You complete the Medbot. Beep boop!"));
                medbot.name = created_name as string;
                medbot.medkit_type = medkit_type;
                medbot.robot_arm = robot_arm;
                medbot.health_analyzer = healthanalyzer;
                object healed = Dm.initial(medkit_type, (obj_item_storage_medkit p) => p.damagetype_healed);
                medbot.damage_type_healer = Dm.truthy(healed) ? healed as string : BRUTE;
                qdel(this);
                return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }
}

// Honkbot Assembly
// tg: code/modules/mob/living/simple_animal/bot/construction.dm:320
public partial class obj_item_bot_assembly_honkbot : obj_item_bot_assembly
{
    // tg: code/modules/mob/living/simple_animal/bot/construction.dm:326
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        switch (Dm.num(build_step))
        {
            case ASSEMBLY_FIRST_STEP:
                if (tool is not obj_item_assembly_prox_sensor)
                    return NONE;
                if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
                    return ITEM_INTERACT_BLOCKING;
                to_chat(user, span_notice(Dm.text("You add the [] to []!", tool, this)));
                icon_state = "honkbot_proxy";
                name = "incomplete Honkbot assembly";
                qdel(tool);
                build_step = Dm.num(build_step) + 1;
                return ITEM_INTERACT_SUCCESS;

            case ASSEMBLY_SECOND_STEP:
                if (tool is not obj_item_bikehorn)
                    return NONE;
                if (!Dm.truthy(await can_finish_build(tool, user)))
                    return ITEM_INTERACT_BLOCKING;
                to_chat(user, span_notice(Dm.text("You add the [] to []! Honk!", tool, this)));
                mob_living_basic_bot_secbot_honkbot new_honkbot = await Dm.new_async<mob_living_basic_bot_secbot_honkbot>(drop_location());
                new_honkbot.name = created_name as string;
                playsound(new_honkbot, Dm.resource("sound/machines/ping.ogg"), 50, TRUE, -1);
                qdel(tool);
                qdel(this);
                return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }
}

// Secbot Assembly
// tg: code/modules/mob/living/simple_animal/bot/construction.dm:354
public partial class obj_item_bot_assembly_secbot : obj_item_bot_assembly
{
    // tg: code/modules/mob/living/simple_animal/bot/construction.dm:367
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        atom drop_loc = drop_location() as atom;
        switch (Dm.num(build_step))
        {
            case ASSEMBLY_FIRST_STEP:
                if (Dm.eq(tool.tool_behaviour, TOOL_WELDER))
                {
                    if (!Dm.truthy(await tool.use_tool(this, user, 0, volume: 40)))
                        return ITEM_INTERACT_BLOCKING;
                    add_overlay("hs_hole");
                    to_chat(user, span_notice(Dm.text("You weld a hole in []!", this)));
                    build_step = Dm.num(build_step) + 1;
                    return ITEM_INTERACT_SUCCESS;
                }

                if (!Dm.eq(tool.tool_behaviour, TOOL_SCREWDRIVER)) //deconstruct
                    return NONE;

                Dm.@new<obj_item_assembly_signaler>(drop_loc);
                Dm.@new<obj_item_clothing_head_helmet_sec>(drop_loc);
                to_chat(user, span_notice("You disconnect the signaler from the helmet."));
                qdel(this);
                return ITEM_INTERACT_SUCCESS;

            case ASSEMBLY_SECOND_STEP:
                if (tool is obj_item_assembly_prox_sensor)
                {
                    if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
                        return ITEM_INTERACT_BLOCKING;
                    to_chat(user, span_notice(Dm.text("You add [] to []!", tool, this)));
                    add_overlay("hs_eye");
                    name = "helmet/signaler/prox sensor assembly";
                    qdel(tool);
                    build_step = Dm.num(build_step) + 1;
                    return ITEM_INTERACT_SUCCESS;
                }

                if (!Dm.eq(tool.tool_behaviour, TOOL_WELDER)) //deconstruct
                    return NONE;

                if (!Dm.truthy(await tool.use_tool(this, user, 0, volume: 40)))
                    return ITEM_INTERACT_BLOCKING;

                cut_overlay("hs_hole");
                to_chat(user, span_notice(Dm.text("You weld the hole in [] shut!", this)));
                build_step = Dm.num(build_step) - 1;
                return ITEM_INTERACT_SUCCESS;

            case ASSEMBLY_THIRD_STEP:
                if (tool is obj_item_bodypart_arm_left_robot || tool is obj_item_bodypart_arm_right_robot)
                {
                    if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
                        return ITEM_INTERACT_BLOCKING;
                    to_chat(user, span_notice(Dm.text("You add [] to []!", tool, this)));
                    name = "helmet/signaler/prox sensor/robot arm assembly";
                    add_overlay("hs_arm");
                    robot_arm = tool.type;
                    qdel(tool);
                    build_step = Dm.num(build_step) + 1;
                    return ITEM_INTERACT_SUCCESS;
                }

                if (!Dm.eq(tool.tool_behaviour, TOOL_SCREWDRIVER)) //deconstruct
                    return NONE;

                cut_overlay("hs_eye");
                Dm.@new<obj_item_assembly_prox_sensor>(drop_loc);
                to_chat(user, span_notice(Dm.text("You detach the proximity sensor from [].", this)));
                build_step = Dm.num(build_step) - 1;
                return ITEM_INTERACT_SUCCESS;

            case ASSEMBLY_FOURTH_STEP:
            {
                if (tool is obj_item_melee_baton_security)
                {
                    if (!Dm.truthy(await can_finish_build(tool, user)))
                        return ITEM_INTERACT_BLOCKING;
                    to_chat(user, span_notice("You complete the Securitron! Beep boop."));
                    mob_living_basic_bot_secbot new_bot = await Dm.new_async<mob_living_basic_bot_secbot>(drop_loc);
                    new_bot.name = created_name as string;
                    new_bot.baton_type = tool.type;
                    new_bot.robot_arm = robot_arm;
                    qdel(tool);
                    qdel(this);
                    return ITEM_INTERACT_SUCCESS;
                }

                if (Dm.eq(tool.tool_behaviour, TOOL_WRENCH))
                {
                    to_chat(user, span_notice(Dm.text("You adjust []'s arm slots to mount extra weapons.", this)));
                    build_step = Dm.num(build_step) + 1;
                    return ITEM_INTERACT_SUCCESS;
                }

                if (Dm.eq(tool.tool_behaviour, TOOL_SCREWDRIVER)) //deconstruct
                {
                    cut_overlay("hs_arm");
                    obj_item_bodypart dropped_arm = await Dm.new_async(robot_arm, drop_loc) as obj_item_bodypart;
                    robot_arm = null;
                    to_chat(user, span_notice(Dm.text("You remove [] from [].", dropped_arm, this)));
                    build_step = Dm.num(build_step) - 1;
                    if (toyswordamt > 0 || Dm.truthy(toyswordamt))
                    {
                        toyswordamt = 0;
                        icon_state = Dm.initial(this, (obj_item_bot_assembly_secbot p) => p.icon_state);
                        to_chat(user, span_notice(Dm.text("The superglue binding []'s toy swords to its chassis snaps!", this)));
                        for (int IS = 1; IS <= toyswordamt; IS++)
                            Dm.@new<obj_item_toy_sword>(drop_loc);
                    }
                    return ITEM_INTERACT_SUCCESS;
                }

                if (tool is not obj_item_toy_sword)
                    return NONE;

                if (toyswordamt < 3 && swordamt <= 0)
                {
                    if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
                        return ITEM_INTERACT_BLOCKING;
                    created_name = "General Beepsky";
                    name = "helmet/signaler/prox sensor/robot arm/toy sword assembly";
                    icon_state = "grievous_assembly";
                    to_chat(user, span_notice(Dm.text("You superglue [] onto one of []'s arm slots.", tool, this)));
                    qdel(tool);
                    toyswordamt++;
                    return ITEM_INTERACT_SUCCESS;
                }

                if (!Dm.truthy(await can_finish_build(tool, user)))
                    return ITEM_INTERACT_BLOCKING;

                to_chat(user, span_notice("You complete the Securitron!...Something seems a bit wrong with it..?"));
                mob_living_basic_bot_secbot_grievous_toy toy_bot = await Dm.new_async<mob_living_basic_bot_secbot_grievous_toy>(drop_loc);
                toy_bot.name = created_name as string;
                toy_bot.robot_arm = robot_arm;
                qdel(tool);
                qdel(this);
                return ITEM_INTERACT_SUCCESS;
            }

            case ASSEMBLY_FIFTH_STEP:
            {
                if (Dm.eq(tool.tool_behaviour, TOOL_SCREWDRIVER)) //deconstruct
                {
                    build_step = Dm.num(build_step) - 1;
                    swordamt = 0;
                    icon_state = Dm.initial(this, (obj_item_bot_assembly_secbot p) => p.icon_state);
                    to_chat(user, span_notice(Dm.text("You unbolt []'s energy swords.", this)));
                    for (int IS = 1; IS <= swordamt; IS++)
                        Dm.@new<obj_item_melee_energy_sword_saber>(drop_loc);
                    return ITEM_INTERACT_SUCCESS;
                }

                if (tool is not obj_item_melee_energy_sword_saber)
                    return NONE;

                if (swordamt < 3)
                {
                    if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
                        return ITEM_INTERACT_BLOCKING;
                    created_name = "General Beepsky";
                    name = "helmet/signaler/prox sensor/robot arm/energy sword assembly";
                    icon_state = "grievous_assembly";
                    to_chat(user, span_notice(Dm.text("You bolt [] onto one of []'s arm slots.", tool, this)));
                    qdel(tool);
                    swordamt++;
                    return ITEM_INTERACT_SUCCESS;
                }

                if (!Dm.truthy(await can_finish_build(tool, user)))
                    return ITEM_INTERACT_BLOCKING;

                to_chat(user, span_notice("You complete the Securitron!...Something seems a bit wrong with it..?"));
                mob_living_basic_bot_secbot_grievous new_bot = await Dm.new_async<mob_living_basic_bot_secbot_grievous>(drop_loc);
                new_bot.name = created_name as string;
                new_bot.robot_arm = robot_arm;
                qdel(tool);
                qdel(this);
                return ITEM_INTERACT_SUCCESS;
            }
        }
        return null;
    }
}

//Firebot Assembly
// tg: code/modules/mob/living/simple_animal/bot/construction.dm:524
public partial class obj_item_bot_assembly_firebot : obj_item_bot_assembly
{
    // tg: code/modules/mob/living/simple_animal/bot/construction.dm:530
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        switch (Dm.num(build_step))
        {
            case ASSEMBLY_FIRST_STEP:
                if (tool is not obj_item_clothing_head_utility_hardhat_red)
                    return NONE;
                if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
                    return ITEM_INTERACT_BLOCKING;
                to_chat(user, span_notice(Dm.text("You add the [] to []!", tool, this)));
                icon_state = "firebot_helmet";
                desc = "An incomplete firebot assembly with a fire helmet.";
                qdel(tool);
                build_step = Dm.num(build_step) + 1;
                return ITEM_INTERACT_SUCCESS;

            case ASSEMBLY_SECOND_STEP:
                if (tool is not obj_item_assembly_prox_sensor)
                    return NONE;
                if (!Dm.truthy(await can_finish_build(tool, user)))
                    return ITEM_INTERACT_BLOCKING;
                to_chat(user, span_notice(Dm.text("You add the [] to []! Beep Boop!", tool, this)));
                mob_living_basic_bot_firebot firebot = await Dm.new_async<mob_living_basic_bot_firebot>(drop_location());
                firebot.name = created_name as string;
                qdel(tool);
                qdel(this);
                return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }
}

//Get cleaned
// tg: code/modules/mob/living/simple_animal/bot/construction.dm:557
public partial class obj_item_bot_assembly_hygienebot : obj_item_bot_assembly
{
    // tg: code/modules/mob/living/simple_animal/bot/construction.dm:564
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        atom drop_loc = drop_location() as atom;
        switch (Dm.num(build_step))
        {
            case ASSEMBLY_FIRST_STEP:
                if (Dm.eq(tool.tool_behaviour, TOOL_WELDER)) //Construct
                {
                    if (!Dm.truthy(await tool.use_tool(this, user, 0, volume: 40)))
                        return ITEM_INTERACT_BLOCKING;
                    to_chat(user, span_notice(Dm.text("You weld a water hole in []!", this)));
                    build_step = Dm.num(build_step) + 1;
                    return ITEM_INTERACT_SUCCESS;
                }

                if (!Dm.eq(tool.tool_behaviour, TOOL_WRENCH)) //Deconstruct
                    return NONE;
                if (!Dm.truthy(await tool.use_tool(this, user, 0, volume: 40)))
                    return ITEM_INTERACT_BLOCKING;
                Dm.@new<obj_item_stack_sheet_iron>(drop_loc, 2);
                to_chat(user, span_notice("You disconnect the hygienebot assembly."));
                qdel(this);
                return ITEM_INTERACT_SUCCESS;

            case ASSEMBLY_SECOND_STEP:
                if (tool is obj_item_assembly_prox_sensor) //Construct
                {
                    if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
                        return ITEM_INTERACT_BLOCKING;

                    build_step = Dm.num(build_step) + 1;
                    to_chat(user, span_notice(Dm.text("You add [] to [].", tool, this)));
                    qdel(tool);
                    return ITEM_INTERACT_SUCCESS;
                }

                if (!Dm.eq(tool.tool_behaviour, TOOL_WELDER)) //Deconstruct
                    return NONE;

                if (!Dm.truthy(await tool.use_tool(this, user, 0, volume: 30)))
                    return ITEM_INTERACT_BLOCKING;

                to_chat(user, span_notice(Dm.text("You weld close the water hole in []!", this)));
                build_step = Dm.num(build_step) - 1;
                return ITEM_INTERACT_SUCCESS;

            case ASSEMBLY_THIRD_STEP:
                if (!Dm.truthy(await can_finish_build(tool, user, 0)))
                    return ITEM_INTERACT_BLOCKING;

                if (Dm.eq(tool.tool_behaviour, TOOL_SCREWDRIVER)) //deconstruct
                {
                    Dm.@new<obj_item_assembly_prox_sensor>(drop_loc);
                    to_chat(user, span_notice(Dm.text("You detach the proximity sensor from [].", this)));
                    build_step = Dm.num(build_step) - 1;
                    return ITEM_INTERACT_SUCCESS;
                }

                if (tool is not obj_item_stack_ducts D) //Construct
                    return NONE;

                if (Dm.num(D.get_amount()) < 1)
                {
                    to_chat(user, span_warning(Dm.text("You need one fluid duct to finish []", this)));
                    return ITEM_INTERACT_BLOCKING;
                }
                to_chat(user, span_notice(Dm.text("You start to pipe up []...", this)));
                if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: this)) && Dm.truthy(await D.use(1)))
                    return ITEM_INTERACT_BLOCKING;
                to_chat(user, span_notice(Dm.text("You pipe up [].", this)));
                mob_living_basic_bot_hygienebot new_bot = await Dm.new_async<mob_living_basic_bot_hygienebot>(drop_location());
                new_bot.name = created_name as string;
                qdel(this);
                return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }
}

// Vim Assembly
// tg: code/modules/mob/living/simple_animal/bot/construction.dm:631
public partial class obj_item_bot_assembly_vim : obj_item_bot_assembly
{
    // tg: code/modules/mob/living/simple_animal/bot/construction.dm:637
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        switch (Dm.num(build_step))
        {
            case ASSEMBLY_FIRST_STEP:
                if (tool is not obj_item_bodypart_leg_left_robot && tool is not obj_item_bodypart_leg_right_robot)
                    return NONE;
                if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
                    return ITEM_INTERACT_BLOCKING;
                balloon_alert(user, "leg attached");
                icon_state = "vim_1";
                desc = "Some kind of incomplete mechanism. It seems to be missing the headlights.";
                qdel(tool);
                build_step = Dm.num(build_step) + 1;
                return ITEM_INTERACT_SUCCESS;

            case ASSEMBLY_SECOND_STEP:
                if (tool is not obj_item_flashlight)
                    return NONE;
                if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
                    return ITEM_INTERACT_SUCCESS;
                balloon_alert(user, "flashlight added");
                icon_state = "vim_2";
                desc = "Some kind of incomplete mechanism. The flashlight is added, but not secured.";
                qdel(tool);
                build_step = Dm.num(build_step) + 1;
                return ITEM_INTERACT_SUCCESS;

            case ASSEMBLY_THIRD_STEP:
                if (!Dm.eq(tool.tool_behaviour, TOOL_SCREWDRIVER))
                    return NONE;
                balloon_alert(user, "securing flashlight...");
                if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 100)))
                    return ITEM_INTERACT_BLOCKING;
                balloon_alert(user, "flashlight secured");
                icon_state = "vim_3";
                desc = "Some kind of incomplete mechanism. It seems nearly completed, and just needs a voice assembly.";
                build_step = Dm.num(build_step) + 1;
                return ITEM_INTERACT_SUCCESS;

            case ASSEMBLY_FOURTH_STEP:
                if (tool is not obj_item_assembly_voice)
                    return NONE;
                if (!Dm.truthy(await can_finish_build(tool, user)))
                    return ITEM_INTERACT_BLOCKING;
                balloon_alert(user, "assembly finished");
                obj_vehicle_sealed_mecha_vim new_vim = await Dm.new_async<obj_vehicle_sealed_mecha_vim>(drop_location());
                new_vim.name = created_name as string;
                qdel(tool);
                qdel(this);
                return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }
}
