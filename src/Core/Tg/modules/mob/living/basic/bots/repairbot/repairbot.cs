// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/repairbot/repairbot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:2
public partial class mob_living_basic_bot_repairbot : mob_living_basic_bot
{
    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:42
    private static DmList possible_stack_interactions => TgState.Current.mob_living_basic_bot_repairbot__possible_stack_interactions ??= Dm.list(
        Dm.kv(typeof(obj_item_stack_sheet_iron), typecacheof(Dm.list(typeof(obj_structure_girder)))),
        Dm.kv(typeof(obj_item_stack_tile), typecacheof(Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating)))),
        Dm.kv(typeof(obj_item_stack_sheet_glass), typecacheof(Dm.list(typeof(obj_structure_grille)))));
    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:47
    private static DmList possible_tool_interactions => TgState.Current.mob_living_basic_bot_repairbot__possible_tool_interactions ??= Dm.list(
        Dm.kv(typeof(obj_item_weldingtool_repairbot), typecacheof(Dm.list(typeof(obj_structure_window)))),
        Dm.kv(typeof(obj_item_crowbar), typecacheof(Dm.list(typeof(obj_machinery_door), typeof(turf_open_floor)))));
    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:52
    private static DmList neutral_voicelines => TgState.Current.mob_living_basic_bot_repairbot__neutral_voicelines ??= Dm.list(
        Dm.kv(REPAIRBOT_VOICED_BRICK, Dm.resource("sound/mobs/non-humanoids/repairbot/brick.ogg")),
        Dm.kv(REPAIRBOT_VOICED_ENTROPY, Dm.resource("sound/mobs/non-humanoids/repairbot/entropy.ogg")),
        Dm.kv(REPAIRBOT_VOICED_FIX_IT, Dm.resource("sound/mobs/non-humanoids/repairbot/fixit.ogg")),
        Dm.kv(REPAIRBOT_VOICED_FIX_TOUCH, Dm.resource("sound/mobs/non-humanoids/repairbot/fixtouch.ogg")),
        Dm.kv(REPAIRBOT_VOICED_HOLE, Dm.resource("sound/mobs/non-humanoids/repairbot/patchingholes.ogg")),
        Dm.kv(REPAIRBOT_VOICED_PAY, Dm.resource("sound/mobs/non-humanoids/repairbot/pay.ogg")));
    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:61
    private static DmList emagged_voicelines => TgState.Current.mob_living_basic_bot_repairbot__emagged_voicelines ??= Dm.list(
        Dm.kv(REPAIRBOT_VOICED_ENTROPY, Dm.resource("sound/mobs/non-humanoids/repairbot/entropy.ogg")),
        Dm.kv(REPAIRBOT_VOICED_STRINGS, Dm.resource("sound/mobs/non-humanoids/repairbot/strings.ogg")),
        Dm.kv(REPAIRBOT_VOICED_PASSION, Dm.resource("sound/mobs/non-humanoids/repairbot/passionproject.ogg")));
    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:67
    private static DmList retrievable_types => TgState.Current.mob_living_basic_bot_repairbot__retrievable_types ??= Dm.list(typeof(obj_item_stack_sheet_iron), typeof(obj_item_stack_sheet_glass), typeof(obj_item_stack_tile));

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:80
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_ai_controller controller = (datum_ai_controller)ai_controller;
        controller.set_blackboard_key(BB_REPAIRBOT_EMAGGED_SPEECH, emagged_voicelines);
        controller.set_blackboard_key(BB_REPAIRBOT_NORMAL_SPEECH, neutral_voicelines);
        grant_actions_by_list(Dm.list(Dm.kv(typeof(datum_action_cooldown_mob_cooldown_bot_build_girder), BB_GIRDER_BUILD_ABILITY), Dm.kv(typeof(datum_action_repairbot_resources), null)));
        add_traits(Dm.list(TRAIT_SPACEWALK, TRAIT_NEGATES_GRAVITY, TRAIT_MOB_MERGE_STACKS, TRAIT_FIREDOOR_OPENER), INNATE_TRAIT);
        our_welder = Dm.@new<obj_item_weldingtool_repairbot>(this);
        our_welder.switched_on(this);
        our_crowbar = Dm.@new<obj_item_crowbar>(this);
        our_screwdriver = Dm.@new<obj_item_screwdriver>(this);
        our_rods = Dm.@new<obj_item_stack_rods>(this, Dm.initial(typeof(obj_item_stack_rods), (obj_item_stack_rods p) => p.max_amount));
        set_color(toolbox_color);
        Waiting.NoSleep(AddElement(typeof(datum_element_can_be_held)));
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:98
    public virtual void set_color(object new_color = null)
    {
        toolbox_color = new_color as string;
        update_appearance();
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:102
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_stack tool_stack)
            return await base.item_interaction(user, tool, modifiers, __extra);

        await attempt_merge(tool_stack, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:109
    public virtual async DmTask attempt_merge(obj_item_stack potential_stack = null, mob_living user = null)
    {
        DmList our_contents = TgState.Current.mob_living_basic_bot_repairbot__our_contents ??= Dm.list(typeof(obj_item_stack_sheet_iron), typeof(obj_item_stack_sheet_glass), typeof(obj_item_stack_tile), typeof(obj_item_stack_rods));
        foreach (Type content in Dm.each<Type>(our_contents))
        {
            if (!Dm.istype(potential_stack, content))
                continue;
            obj_item_stack our_sheet = Dm.locate_type(content, this) as obj_item_stack;
            if (our_sheet == null)
            {
                if (!Dm.truthy(await user.transferItemToLoc(potential_stack, this)))
                {
                    user.balloon_alert(user, "stuck to your hand!");
                    return;
                }
                balloon_alert(user, "inserted");
                return;
            }
            if (Dm.num(our_sheet.amount) >= Dm.num(our_sheet.max_amount))
            {
                user?.balloon_alert(user, "full!");
                return;
            }
            if (!Dm.truthy(our_sheet.can_merge(potential_stack)))
            {
                user?.balloon_alert(user, "not suitable!");
                return;
            }
            atom_movable to_move = await potential_stack.split_stack(Math.Min(Dm.num(our_sheet.max_amount) - Dm.num(our_sheet.amount), Dm.num(potential_stack.amount))) as atom_movable;
            await to_move.forceMove(this);
            balloon_alert(user, "inserted");
            return;
        }
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:131
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        if (arrived is obj_item_stack_sheet_iron iron && our_iron == null) //show iron tiles and glass in our hands
        {
            our_iron = iron;
            update_appearance();
        }
        if (arrived is obj_item_stack_sheet_glass glass && our_glass == null)
        {
            our_glass = glass;
            update_appearance();
        }
        if (arrived is obj_item_stack_tile tile && our_tiles == null)
            our_tiles = tile;
        if (arrived is obj_item_stack_rods rods && our_rods == null)
            our_rods = rods;
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:144
    public override async DmTask<object> UnarmedAttack(atom A = null, object proximity_flag = null, DmList modifiers = null)
    {
        atom target = A;
        object dot = await base.UnarmedAttack(target, proximity_flag, modifiers);

        if (!Dm.truthy(dot) || !Dm.truthy(proximity_flag))
            return dot;

        if (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED)))
        {
            await emagged_interactions(target, modifiers);
            return dot;
        }

        if (target is obj_item_stack target_stack_item)
        {
            await attempt_merge(target_stack_item, this);
            return dot;
        }

        //priority interactions
        if (target is turf_open_space space_target)
        {
            if (!Dm.truthy(space_target.has_valid_support()) && Dm.locate<obj_structure_lattice>(space_target) == null)
                await attempt_use_stack(our_rods != null ? our_rods : Dm.initial(typeof(obj_item_stack_rods), (obj_item_stack_rods p) => p.name), space_target);
        }

        if (target is obj_structure_grille grille_target)
        {
            if (Dm.truthy(grille_target.broken))
                await attempt_use_stack(our_rods != null ? our_rods : Dm.initial(typeof(obj_item_stack_rods), (obj_item_stack_rods p) => p.name), grille_target);
        }

        if (target is turf_open open_target)
        {
            if (Dm.truthy(open_target.broken) || Dm.truthy(open_target.burnt))
            {
                if (our_welder != null)
                    await our_welder.melee_attack_chain(this, open_target);
            }
        }

        if (target is obj_structure_window target_window)
        {
            if (!Dm.truthy(target_window.anchored))
            {
                if (our_screwdriver != null)
                    await our_screwdriver.melee_attack_chain(this, target_window);
            }
        }

        //stack interactions
        foreach (Type stack_type in Dm.each<Type>(possible_stack_interactions))
        {
            if (!Dm.truthy(is_type_in_typecache(target, possible_stack_interactions[stack_type] as DmList)))
                continue;
            obj_item target_stack = Dm.locate_type(stack_type, this) as obj_item;
            await attempt_use_stack(target_stack != null ? target_stack : Dm.initial(stack_type, (obj_item p) => p.name), target);
            return dot;
        }

        //tool interactions
        DmList our_tools = Dm.list(our_welder, our_crowbar);
        foreach (obj_item tool in Dm.each<obj_item>(our_tools))
        {
            if (Dm.truthy(is_type_in_typecache(target, possible_tool_interactions[tool.type] as DmList)) && !Dm.truthy(combat_mode))
            {
                await tool.melee_attack_chain(this, target);
                return dot;
            }
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:194
    public virtual async DmTask emagged_interactions(atom target = null, object modifiers = null)
    {
        if (target is not mob_living_silicon_robot)
        {
            if (deconstruction_device != null)
                await deconstruction_device.interact_with_atom_secondary(target, this, modifiers as DmList);
            return;
        }
        if (HAS_TRAIT(target, TRAIT_MOB_TIPPED))
            return;
        object old_combat_mode = combat_mode;
        set_combat_mode(TRUE);
        await target.attack_hand_secondary(this, modifiers as DmList); //tip the guy!
        set_combat_mode(old_combat_mode);
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:205
    public override async DmTask<object> start_pulling(atom_movable pulled_atom = null, object state = null, object force = null, float? supress_message = null)
    {
        object dot = await base.start_pulling(pulled_atom, state, force, supress_message);
        if (pulling != null)
            await setGrabState(GRAB_AGGRESSIVE); //automatically aggro grab everything!
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:210
    public virtual async DmTask attempt_use_stack(object stack_to_use = null, atom target = null)
    {
        if (stack_to_use is not obj_item stack_item)
        {
            to_chat(this, span_warning(Dm.text("You do not have anymore []!", stack_to_use)));
            return;
        }
        await stack_item.melee_attack_chain(this, target);
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:216
    public override async DmTask<object> flash_act(float? intensity = null, float? override_blindness_check = null, float? affect_silicon = null, float? visual = null, Type type = null, float? length = null)
    {
        if (Dm.truthy(affect_silicon))
            return await base.flash_act(intensity, override_blindness_check, affect_silicon, visual, type, length);
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:220
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        qdel(our_iron);
        our_iron = null;
        qdel(our_glass);
        our_glass = null;
        qdel(our_tiles);
        our_tiles = null;
        qdel(our_welder);
        our_welder = null;
        qdel(our_screwdriver);
        our_screwdriver = null;
        qdel(our_crowbar);
        our_crowbar = null;
        qdel(our_rods);
        our_rods = null;
        qdel(deconstruction_device);
        deconstruction_device = null;
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:231
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == our_crowbar)
            our_crowbar = null;
        if (gone == our_screwdriver)
            our_screwdriver = null;
        if (gone == our_welder)
            our_welder = null;
        if (gone == our_tiles)
            our_tiles = null;
        if (gone == our_iron)
            our_iron = null;
        if (gone == our_glass)
            our_glass = null;
        if (gone == our_rods)
            our_rods = null;
        update_appearance();
        return await base.Exited(gone, direction);
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:248
    public override object process(object seconds_per_tick = null, params object[] __extra) //generate 1 iron rod every 2 seconds
    {
        if (our_rods == null || Dm.num(our_rods.amount) < Dm.num(our_rods.max_amount))
        {
            obj_item_stack_rods new_rods = Dm.@new<obj_item_stack_rods>();
            Waiting.NoSleep(new_rods.forceMove(this));
        }
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:253
    public override object turn_on(mob user = null)
    {
        object dot = base.turn_on(user);
        if (!Dm.truthy(dot))
            return dot;
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:259
    public override async DmTask turn_off()
    {
        await base.turn_off();
        STOP_PROCESSING(SSobj, this);
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:263
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        mutable_appearance our_box = mutable_appearance(icon, "repairbot_box", BELOW_MOB_LAYER - 0.02f) as mutable_appearance;
        our_box.color = toolbox_color;
        Dm.append(ref dot, our_box);
        if (our_glass != null)
        {
            mutable_appearance glass = mutable_appearance(icon, "repairbot_glass_overlay", BELOW_MOB_LAYER - 0.02f, appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)) as mutable_appearance;
            glass.pixel_w = -6;
            glass.pixel_z = -5;
            Dm.append(ref dot, glass);
        }
        if (our_iron != null)
        {
            mutable_appearance iron = mutable_appearance(icon, "repairbot_iron_overlay", BELOW_MOB_LAYER - 0.02f, appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)) as mutable_appearance;
            iron.pixel_w = 7;
            iron.pixel_z = -5;
            Dm.append(ref dot, iron);
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:278
    public override object generate_speak_list()
    {
        return Dm.add(neutral_voicelines, emagged_voicelines);
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:281
    public override async DmTask<object> Bump(atom bumped_atom = null)
    {
        object dot = await base.Bump(bumped_atom);
        if (bumped_atom is obj_machinery_door_firedoor && Dm.truthy(bumped_atom.density))
            await our_crowbar.melee_attack_chain(this, bumped_atom);
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:286
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = await base.ui_data(user) as DmList;
        DmList materials = Dm.list();
        data["repairbot_materials"] = materials;
        if (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_LOCKED)) && !issilicon(user)) // isAdminGhostAI: admin tooling
            return data;
        DmList custom = data["custom_controls"] as DmList;
        custom["fix_breaches"] = Dm.bitand(repairbot_flags, REPAIRBOT_FIX_BREACHES);
        custom["replace_windows"] = Dm.bitand(repairbot_flags, REPAIRBOT_REPLACE_WINDOWS);
        custom["replace_tiles"] = Dm.bitand(repairbot_flags, REPAIRBOT_REPLACE_TILES);
        custom["fix_girders"] = Dm.bitand(repairbot_flags, REPAIRBOT_FIX_GIRDERS);
        custom["build_girders"] = Dm.bitand(repairbot_flags, REPAIRBOT_BUILD_GIRDERS);

        foreach (Type data_path in Dm.each<Type>(retrievable_types))
        {
            atom to_retrieve = Dm.locate_type(data_path, this) as atom;
            if (to_retrieve == null)
                continue;

            Dm.append(ref materials, Dm.list(Dm.list(
                Dm.kv("material_ref", REF(to_retrieve)),
                Dm.kv("material_icon", Dm.initial(to_retrieve, (atom p) => p.icon)),
                Dm.kv("material_icon_state", Dm.initial(to_retrieve, (atom p) => p.icon_state)))));
        }

        return data;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:310
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot) || !isliving(ui.user) || (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_LOCKED)) && !HAS_SILICON_ACCESS(ui.user)))
            return dot;
        switch (action as string)
        {
            case "fix_breaches":
                repairbot_flags = Dm.bitxor(repairbot_flags, REPAIRBOT_FIX_BREACHES);
                break;
            case "replace_windows":
                repairbot_flags = Dm.bitxor(repairbot_flags, REPAIRBOT_REPLACE_WINDOWS);
                break;
            case "replace_tiles":
                repairbot_flags = Dm.bitxor(repairbot_flags, REPAIRBOT_REPLACE_TILES);
                break;
            case "fix_girders":
                repairbot_flags = Dm.bitxor(repairbot_flags, REPAIRBOT_FIX_GIRDERS);
                break;
            case "build_girders":
                repairbot_flags = Dm.bitxor(repairbot_flags, REPAIRBOT_BUILD_GIRDERS);
                break;
            case "remove_item":
            {
                object item_params = @params["item_reference"];
                if (item_params == null)
                    return TRUE;
                obj_item retrieved = Dm.locate_ref(item_params) as obj_item;
                if (retrieved != null && !Dm.@in(retrieved, contents))
                    retrieved = null;
                if (retrieved == null || !Dm.truthy(is_type_in_list(retrieved, retrievable_types)))
                    return TRUE;
                mob_living user = ui.user as mob_living;
                await user.put_in_hands(retrieved);
                break;
            }
        }
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:335
    public override object emag_effects(object user = null)
    {
        if (deconstruction_device == null)
            deconstruction_device = Dm.@new<obj_item_construction_rcd_repairbot>(this);
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:339
    public override async DmTask explode()
    {
        await drop_part(toolbox, drop_location());
        await base.explode();
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:351
    public override async DmTask mob_pickup(mob_living user = null)
    {
        obj_item_carried_repairbot carried = Dm.@new<obj_item_carried_repairbot>(get_turf(this));
        await carried.set_bot(this);
        if (Dm.eq(carried.icon_state, "toolbox_default"))
            await carried.add_atom_colour(toolbox_color, FIXED_COLOUR_PRIORITY);
        user.visible_message(span_warning(Dm.text("[] scoops up []!", user, this)));
        await user.put_in_hands(carried);
    }
}

// tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:359
public partial class obj_item_carried_repairbot : obj_item
{
    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:370
    public virtual async DmTask set_bot(mob_living_basic_bot_repairbot repairbot = null)
    {
        Type bot_toolbox = repairbot.toolbox;
        name = repairbot.name;
        icon = Dm.initial(bot_toolbox, (obj_item p) => p.icon);
        icon_state = Dm.initial(bot_toolbox, (obj_item p) => p.icon_state);
        lefthand_file = Dm.initial(bot_toolbox, (obj_item p) => p.lefthand_file);
        righthand_file = Dm.initial(bot_toolbox, (obj_item p) => p.righthand_file);
        inhand_icon_state = Dm.initial(bot_toolbox, (obj_item p) => p.inhand_icon_state);
        force = Dm.initial(bot_toolbox, (obj_item p) => p.force);
        await repairbot.forceMove(this);
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:381
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (isturf(loc))
            await release_bot();
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:386
    public virtual async DmTask release_bot(float? bypass_delete = null)
    {
        bypass_delete ??= FALSE;
        if (our_bot != null)
        {
            await our_bot.forceMove(drop_location() as atom);
            our_bot.balloon_alert_to_viewers("plops down");
        }
        if (!Dm.truthy(bypass_delete))
            qdel(this);
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:393
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        Waiting.NoSleep(release_bot(bypass_delete: TRUE));
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:397
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        if (isliving(arrived))
            our_bot = arrived;
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot.dm:402
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == our_bot)
            our_bot = null;
        return await base.Exited(gone, direction);
    }
}

// rules 10.6: the static vars above, made on first use
public sealed partial class TgState
{
    internal DmList mob_living_basic_bot_repairbot__possible_stack_interactions;
    internal DmList mob_living_basic_bot_repairbot__possible_tool_interactions;
    internal DmList mob_living_basic_bot_repairbot__neutral_voicelines;
    internal DmList mob_living_basic_bot_repairbot__emagged_voicelines;
    internal DmList mob_living_basic_bot_repairbot__retrievable_types;
    internal DmList mob_living_basic_bot_repairbot__our_contents;
}
