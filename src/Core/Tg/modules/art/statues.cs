// Translated from tgstation (snapshot 1d99bd7f): code/modules/art/statues.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI2 (D326): the statues, the chisel (the service cyborg's), the carving block and the custom statue, whole but one leaf (the
// chisel shaping a wet concrete floor came with the concrete, the MetaStation leftovers): the sculptors'
// personality moods (personalities are not ported, as elephantgraveyard.cs). The drake's and dragonman's glowing eyes are emissive
// overlays (DV12: not created). The var blocks are generated (D111).

/// This controls the delay for the sculpt rock breaking sound
/// Every 4th iterator while sculpting will emit a sound (rougly every couple of seconds)
// tg: code/modules/art/statues.dm:3  #define SCULPT_SOUND_INCREMENT 4

// tg: code/modules/art/statues.dm:5
public partial class obj_structure_statue : obj_structure
{
    // tg: code/modules/art/statues.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(art_type, impressiveness));
        Waiting.NoSleep(AddElement(typeof(datum_element_beauty), impressiveness * 75));
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_marionette)));
        return dot;
    }

    // tg: code/modules/art/statues.dm:33
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/art/statues.dm:38
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        add_fingerprint(user);
        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/modules/art/statues.dm:42
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        add_fingerprint(user);
        if (!Dm.truthy(tool.tool_start_check(user, amount: 1, heat_required: HIGH_TEMPERATURE_REQUIRED)))
            return ITEM_INTERACT_BLOCKING;

        user.balloon_alert(user, I18n.T("slicing apart..."));
        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 50)))
            return ITEM_INTERACT_BLOCKING;

        await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/art/statues.dm:54
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        float amount_mod = Dm.truthy(disassembled) ? 0 : -2;
        foreach (object mat in Dm.each(custom_materials))
        {
            datum_material custom_material = await SSmaterials.get_material(mat) as datum_material;
            float amount = max(0, round(Dm.num(custom_materials[mat]) / SHEET_MATERIAL_AMOUNT) + amount_mod);
            if (amount > 0)
                Dm.@new(custom_material.sheet_type, drop_location(), amount);
        }
    }
}

//////////////////////////////////////STATUES/////////////////////////////////////////////////////////////

// tg: code/modules/art/statues.dm:64
public partial class obj_structure_statue_drake : obj_structure_statue
{
    // tg: code/modules/art/statues.dm:70
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(25))
        {
            icon_state = "drake_headless";
            desc = I18n.T("Statue of a lesser drake. Time has not been kind.");
        }
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/modules/art/statues.dm:77
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        // DEVIATION(DV12): if (icon_state == "drake") . += emissive_appearance(icon, "drake_emissive", src): the glow on the emissive
        // plane is not created
        return dot;
    }
}

// tg: code/modules/art/statues.dm:82
public partial class obj_structure_statue_dragonman : obj_structure_statue
{
    // tg: code/modules/art/statues.dm:88
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/modules/art/statues.dm:92
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        // DEVIATION(DV12): . += emissive_appearance(icon, "dragonman_emissive", src): the glow on the emissive plane is not created
        return dot;
    }
}

///////////Other Stuff//////////////////////////////////////////////
// tg: code/modules/art/statues.dm:301
public partial class obj_item_chisel : obj_item
{
    // tg: code/modules/art/statues.dm:335
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_eyestab)));
        Waiting.NoSleep(AddElement(typeof(datum_element_wall_engraver)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_bane), Dm.named("damage_multiplier", 40), Dm.named("should_bane_callback", Dm.@new<datum_callback>(this, nameof(bane_check))), Dm.named("label_text", "statues")));
        return dot;
    }

    // tg: code/modules/art/statues.dm:341
    public override object Destroy(float? force = null)
    {
        prepared_block = null;
        tracked_user = null;
        return base.Destroy(force);
    }

    /// Bane component callback
    // tg: code/modules/art/statues.dm:347
    public virtual object bane_check(mob_living target = null)
    {
        return target is mob_living_basic_statue ? TRUE : FALSE;
    }

    /*
    Hit the block to start
    Point with the chisel at the target to choose what to sculpt or hit block to choose from preset statue types.
    Hit block again to start sculpting.
    Moving interrupts
    */
    // tg: code/modules/art/statues.dm:356
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (Dm.truthy(sculpting))
            return ITEM_INTERACT_BLOCKING;

        if (interacting_with is obj_structure_carving_block sculpt_block)
        {
            if (Dm.truthy(sculpt_block.completion)) // someone already started sculpting this so just finish
            {
                set_block(sculpt_block, user, silent: TRUE);
                await start_sculpting(user);
            }
            else if (sculpt_block == prepared_block && (Dm.truthy(prepared_block.current_target) || Dm.truthy(prepared_block.current_preset_type)))
                await start_sculpting(user);
            else if (prepared_block == null)
                set_block(sculpt_block, user);
            else if (sculpt_block == prepared_block)
                await show_generic_statues_prompt(user);
            return ITEM_INTERACT_SUCCESS;
        }

        else if (prepared_block != null) //We're aiming at something next to us with block prepared
        {
            prepared_block.set_target(interacting_with as atom_movable, user);
            return ITEM_INTERACT_SUCCESS;
        }

        else if (interacting_with is turf_open_floor_concrete concrete_floor)
        {
            return await concrete_floor.handle_shape(user);
        }

        return NONE;
    }

    // We aim at something distant.
    // tg: code/modules/art/statues.dm:385
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(sculpting) && prepared_block != null && ismovable(interacting_with) && prepared_block.completion == 0)
        {
            prepared_block.set_target((atom_movable)interacting_with, user);
            return ITEM_INTERACT_SUCCESS;
        }
        return ITEM_INTERACT_BLOCKING;
    }

    /// Starts or continues the sculpting action on the carving block material
    // tg: code/modules/art/statues.dm:392
    public virtual async DmTask start_sculpting(mob_living user = null)
    {
        user.balloon_alert(user, I18n.T("sculpting block..."));
        playsound(this, pick(usesound), 75, TRUE);
        sculpting = TRUE;
        //How long whole process takes
        float sculpting_time = 30 * SECONDS;
        //Single interruptible progress period
        float sculpting_period = round(sculpting_time / ICON_SIZE_Y); //this is just so it reveals pixels line by line for each.
        bool interrupted = false;
        float remaining_time = sculpting_time - (prepared_block.completion * sculpting_time);

        datum_progressbar total_progress_bar = Dm.@new<datum_progressbar>(user, sculpting_time, prepared_block);
        while (remaining_time > 0 && !interrupted)
        {
            if (Dm.truthy(await do_after(user, sculpting_period, prepared_block, show_progress: FALSE)))
            {
                bool time_delay = !Dm.truthy(Dm.mod(remaining_time, 4)); // SCULPT_SOUND_INCREMENT
                if (time_delay)
                    playsound(this, Dm.resource("sound/effects/break_stone.ogg"), 50, TRUE);
                remaining_time -= sculpting_period;
                prepared_block.set_completion((sculpting_time - remaining_time) / sculpting_time);
                total_progress_bar.update(sculpting_time - remaining_time);
            }
            else
                interrupted = true;
        }
        total_progress_bar.end_progress();
        if (!interrupted && !QDELETED(prepared_block))
        {
            prepared_block.create_statue();
            user.balloon_alert(user, I18n.T("statue finished"));
            // HAS_PERSONALITY(user, creative/unimaginative): the sculptors' moods (personalities are not ported)
        }
        stop_sculpting(silent: !interrupted ? TRUE : FALSE);
    }

    /// To setup the sculpting target for the carving block
    // tg: code/modules/art/statues.dm:425
    public virtual void set_block(obj_structure_carving_block B = null, mob_living user = null, float? silent = null)
    {
        silent ??= FALSE;
        prepared_block = B;
        tracked_user = user;
        RegisterSignal(tracked_user, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        if (!Dm.truthy(silent))
            user.balloon_alert(user, I18n.T("select sculpt target"));
    }

    // tg: code/modules/art/statues.dm:432
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        stop_sculpting();
        return dot;
    }

    /// Cancel the sculpting action
    // tg: code/modules/art/statues.dm:437
    public virtual void stop_sculpting(float? silent = null)
    {
        silent ??= FALSE;
        sculpting = FALSE;
        if (prepared_block != null && prepared_block.completion == 0)
            prepared_block.reset_target();
        prepared_block = null;

        if (!Dm.truthy(silent) && tracked_user != null)
            tracked_user.balloon_alert(tracked_user, I18n.T("sculpting cancelled!"));

        if (tracked_user != null)
        {
            UnregisterSignal(tracked_user, COMSIG_MOVABLE_MOVED);
            tracked_user = null;
        }
    }

    // tg: code/modules/art/statues.dm:450
    [SignalHandler]
    public virtual void on_moved()
    {
        stop_sculpting();
    }

    // tg: code/modules/art/statues.dm:455
    public virtual async DmTask show_generic_statues_prompt(mob_living user = null)
    {
        DmList choices = Dm.list();
        foreach (Type statue_path in Dm.each<Type>(prepared_block.get_possible_statues()))
            choices[statue_path] = image(icon: Dm.initial(statue_path, (obj_structure_statue p) => p.icon), icon_state: Dm.initial(statue_path, (obj_structure_statue p) => p.icon_state));

        if (!Dm.truthy(choices.len))
            user.balloon_alert(user, I18n.T("no statues for material!"));

        object choice = await show_radial_menu(user, prepared_block, choices, require_near: TRUE);
        if (Dm.truthy(choice))
        {
            prepared_block.current_preset_type = choice;
            image chosen_looks = choices[choice] as image;
            prepared_block.current_target = chosen_looks.appearance;
            user.balloon_alert(user, I18n.T("statue selected"));
        }
    }
}

// tg: code/modules/art/statues.dm:471
public partial class obj_structure_carving_block : obj_structure
{
    //Table of required materials for each non-abstract statue type
    // tg: code/modules/art/statues.dm:485  var/static/list/statue_costs
    private static DmList statue_costs
    {
        get => TgState.Current.obj_structure_carving_block__statue_costs;
        set => TgState.Current.obj_structure_carving_block__statue_costs = value;
    }

    /// HSV color filters parameters
    // tg: code/modules/art/statues.dm:491  var/static/list/greyscale_with_value_bump
    internal static DmList greyscale_with_value_bump => Dm.list(0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, -0.05f);

    // tg: code/modules/art/statues.dm:493
    public override object Destroy(float? force = null)
    {
        current_target = null;
        target_appearance_with_filters = null;
        return base.Destroy(force);
    }

    // tg: code/modules/art/statues.dm:498
    public virtual void set_target(atom_movable target = null, mob_living user = null)
    {
        if (!Dm.truthy(is_viable_target(user, target)))
            return;
        if (target is obj_structure_statue_custom original)
            current_target = original.content_ma;
        else
            current_target = target.appearance;
        user.balloon_alert(user, I18n.T("sculpt target is []", appearance_name(current_target)));
    }

    // the name of an appearance read through a mutable appearance var (tg's `var/mutable_appearance/ma = current_target`): the target is
    // a mutable appearance (a custom statue's or a preset's image) or an atom's appearance snapshot
    private static string appearance_name(object appearance) => appearance is image i ? i.name : (appearance as DmAppearance)?.name;

    // tg: code/modules/art/statues.dm:509
    public virtual void reset_target()
    {
        current_target = null;
        current_preset_type = null;
        target_appearance_with_filters = null;
    }

    // tg: code/modules/art/statues.dm:514
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (target_appearance_with_filters == null)
            return dot;
        //We're only keeping one instance here that changes in the middle so we have to clone it to avoid managed overlay issues
        mutable_appearance clone = Dm.@new<mutable_appearance>(target_appearance_with_filters);
        Dm.append(ref dot, clone);
        return dot;
    }

    // tg: code/modules/art/statues.dm:522
    public virtual object is_viable_target(mob_living user = null, atom_movable target = null)
    {
        //Only things on turfs
        if (!isturf(target.loc))
        {
            user.balloon_alert(user, I18n.T("no sculpt target!"));
            return FALSE;
        }
        //No big icon things
        DmList icon_dimensions = get_icon_dimensions(target.icon);
        if (Dm.num(icon_dimensions["width"]) != ICON_SIZE_X || Dm.num(icon_dimensions["height"]) != ICON_SIZE_Y)
        {
            user.balloon_alert(user, I18n.T("sculpt target is too big!"));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/art/statues.dm:534
    public virtual void create_statue()
    {
        if (Dm.truthy(current_preset_type))
        {
            obj_structure_statue preset_statue = Dm.@new((Type)current_preset_type, get_turf(this)) as obj_structure_statue;
            Waiting.NoSleep(preset_statue.set_custom_materials(custom_materials));
            qdel(this);
        }
        else if (Dm.truthy(current_target))
        {
            obj_structure_statue_custom new_statue = Dm.@new<obj_structure_statue_custom>(get_turf(this));
            new_statue.set_visuals(current_target);
            Waiting.NoSleep(new_statue.set_custom_materials(custom_materials));
            // var/mutable_appearance/ma = current_target: an appearance's name
            new_statue.name = I18n.T("statue of []", appearance_name(current_target));
            new_statue.desc = I18n.T("A carved statue depicting [].", appearance_name(current_target));
            qdel(this);
        }
    }

    // tg: code/modules/art/statues.dm:548
    public virtual void set_completion(float value = 0)
    {
        if (!Dm.truthy(current_target))
            return;
        if (target_appearance_with_filters == null)
        {
            target_appearance_with_filters = Dm.@new<mutable_appearance>(current_target);
            // KEEP_APART in case carving block gets KEEP_TOGETHER from somewhere like material texture filters.
            target_appearance_with_filters.appearance_flags = Dm.bitor(target_appearance_with_filters.appearance_flags, Dm.bitor(KEEP_TOGETHER, KEEP_APART));
            //Doesn't use filter helpers because MAs aren't atoms
            target_appearance_with_filters.filters = Dm.list(Dm.filter(Dm.named("type", "color"), Dm.named("color", greyscale_with_value_bump), Dm.named("space", FILTER_COLOR_HSV)));
        }
        completion = value;
        icon white = TgState.Current.obj_structure_carving_block__set_completion__white ??= Dm.icon(Dm.resource("icons/effects/alphacolors.dmi"), "white");
        if (value == 0)
        {
            //delete uncovered and reset filters
            remove_filter("partial_uncover");
            target_appearance_with_filters = null;
        }
        else
        {
            float mask_offset = min(ICON_SIZE_Y, round(completion * ICON_SIZE_Y));
            remove_filter("partial_uncover");
            add_filter("partial_uncover", 1, alpha_mask_filter(icon: white, y: -mask_offset) as DmList);
            target_appearance_with_filters.filters = Dm.list(Dm.filter(Dm.named("type", "alpha"), Dm.named("icon", white), Dm.named("y", -mask_offset), Dm.named("flags", MASK_INVERSE)));
        }
        update_appearance();
    }

    /// Returns a list of preset statues carvable from this block depending on the custom materials
    // tg: code/modules/art/statues.dm:573
    public virtual DmList get_possible_statues()
    {
        DmList dot = Dm.list();
        if (statue_costs == null)
            statue_costs = build_statue_cost_table();
        foreach (object statue_path in Dm.each(statue_costs))
        {
            DmList carving_cost = statue_costs[statue_path] as DmList;
            bool enough_materials = true;
            foreach (object required_material in Dm.each(carving_cost))
            {
                if (!Dm.truthy(Waiting.NoSleep(has_material_type(required_material, Dm.num(carving_cost[required_material])))))
                {
                    enough_materials = false;
                    break;
                }
            }
            if (enough_materials)
                Dm.append(ref dot, statue_path);
        }
        return dot;
    }

    // tg: code/modules/art/statues.dm:587
    public virtual DmList build_statue_cost_table()
    {
        DmList dot = Dm.list();
        foreach (Type statue_type in Dm.each<Type>(Dm.sub(subtypesof(typeof(obj_structure_statue)), typeof(obj_structure_statue_custom))))
        {
            obj_structure_statue fake_statue = Dm.@new(statue_type) as obj_structure_statue;
            if (!Dm.truthy(fake_statue.icon_state) || fake_statue.abstract_type == fake_statue.type || Dm.truthy(fake_statue.uncarveable) || !Dm.truthy(fake_statue.custom_materials))
                continue;
            dot[fake_statue.type] = fake_statue.custom_materials;
            qdel(fake_statue);
        }
        return dot;
    }
}

// tg: code/modules/art/statues.dm:596
public partial class obj_structure_statue_custom : obj_structure_statue
{
    // tg: code/modules/art/statues.dm:606
    public override object Destroy(float? force = null)
    {
        content_ma = null;
        return base.Destroy(force);
    }

    // tg: code/modules/art/statues.dm:610
    public virtual void set_visuals(object model_appearance = null)
    {
        if (content_ma != null)
            QDEL_NULL(ref content_ma);
        content_ma = copy_appearance_filter_overlays(model_appearance);
        content_ma.pixel_x = 0;
        content_ma.pixel_y = 0;
        content_ma.alpha = 255;
        content_ma.appearance_flags = Dm.bitand(content_ma.appearance_flags, Dm.bitnot(KEEP_APART)); //Don't want this
        content_ma.filters = Dm.list(Dm.filter(Dm.named("type", "color"), Dm.named("color", obj_structure_carving_block.greyscale_with_value_bump), Dm.named("space", FILTER_COLOR_HSV)));
        update_content_planes();
        update_appearance();
    }

    // tg: code/modules/art/statues.dm:622
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        if (Dm.truthy(same_z_layer))
        {
            await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
            return;
        }
        update_content_planes();
        update_appearance();
    }

    // tg: code/modules/art/statues.dm:628
    public virtual void update_content_planes()
    {
        if (content_ma == null)
            return;
        turf our_turf = get_turf(this);
        // MA's stored in the overlays list are not actually mutable, they've been flattened
        // This proc unflattens them, updates them, and then reapplies
        DmList created = update_appearance_planes(Dm.list(content_ma), GET_TURF_PLANE_OFFSET(our_turf)) as DmList;
        content_ma = created[1] as mutable_appearance;
    }

    // tg: code/modules/art/statues.dm:637
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (content_ma != null)
            Dm.append(ref dot, content_ma);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/art/statues.dm:485  var/static/list/statue_costs
    internal DmList obj_structure_carving_block__statue_costs;
    // tg: code/modules/art/statues.dm:558  var/static/icon/white
    internal icon obj_structure_carving_block__set_completion__white;
}
