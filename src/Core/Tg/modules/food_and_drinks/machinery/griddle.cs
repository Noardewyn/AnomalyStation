// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/machinery/griddle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the griddle and the griddle stand. The var blocks are generated (D111).

// tg: code/modules/food_and_drinks/machinery/griddle.dm:1
public partial class obj_machinery_griddle : obj_machinery
{
    // tg: code/modules/food_and_drinks/machinery/griddle.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        grill_loop = Dm.@new<datum_looping_sound_grill>(this, FALSE);
        if (isnum(variant))
        {
            variant = rand(1, 3);
            update_appearance();
        }
        RegisterSignal(this, COMSIG_ATOM_EXPOSE_REAGENT, nameof(on_expose_reagent));
        RegisterSignal(this, COMSIG_STORAGE_DUMP_CONTENT, nameof(on_storage_dump));
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/griddle.dm:35
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref grill_loop);
        return base.Destroy(force);
    }

    // tg: code/modules/food_and_drinks/machinery/griddle.dm:39
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item I = null)
    {
        object dot = await default_deconstruction_crowbar(user, I);
        if (Dm.truthy(dot))
            return dot;
        // this is dead code... default_deconstruction_crowbar will never fail
        variant = rand(1, 3);
        update_appearance();
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/griddle.dm:47
    public override object can_crowbar_deconstruct()
    {
        return TRUE;
    }

    // tg: code/modules/food_and_drinks/machinery/griddle.dm:50
    public override object IsContainedAtomAccessible(atom contained = null, atom_movable user = null)
    {
        return (base.IsContainedAtomAccessible(contained, user) is var or__1 && Dm.truthy(or__1) ? or__1 : Dm.num(Dm.@in(contained, griddled_objects)));
    }

    // tg: code/modules/food_and_drinks/machinery/griddle.dm:53
    [SignalHandler]
    public virtual object on_expose_reagent(atom parent_atom = null, datum_reagent exposing_reagent = null, object reac_volume = null, object methods = null)
    {
        if (griddled_objects.len >= max_items || exposing_reagent is not datum_reagent_consumable_pancakebatter || Dm.num(reac_volume) < 5)
            return NONE; //make sure you have space... it's actually batter... and a proper amount of it.

        for (float pancakes = 1, pancakes__end = FLOOR(Dm.num(reac_volume), 5); pancakes <= pancakes__end; pancakes += 5) //this adds as many pancakes as you possibly could make, with 5u needed per pancake
        {
            obj_item_food_pancakes_raw new_pancake = Dm.@new<obj_item_food_pancakes_raw>(this);
            new_pancake.pixel_x = rand(16, -16);
            new_pancake.pixel_y = rand(16, -16);
            AddToGrill(new_pancake);
            if (griddled_objects.len >= max_items)
                break;
        }
        visible_message(span_notice(I18n.T("[] begins to cook on [].", exposing_reagent, this)));
        return NONE;
    }

    // tg: code/modules/food_and_drinks/machinery/griddle.dm:69
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;

        if (tool.atom_storage != null)
        {
            if (Dm.length(griddled_objects) >= max_items)
            {
                balloon_alert(user, I18n.T("it's full!"));
                return ITEM_INTERACT_BLOCKING;
            }

            if (tool is not obj_item_storage_bag_tray)
            {
                // Non-tray dumping requires a do_after
                to_chat(user, span_notice(I18n.T("You start dumping out the contents of [] into []...", tool, this)));
                if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: tool)))
                    return ITEM_INTERACT_BLOCKING;
            }

            float loaded = 0;
            foreach (obj tray_item in Dm.each<obj>(tool))
            {
                if (!IS_EDIBLE(tray_item))
                    continue;
                if (Dm.length(griddled_objects) >= max_items)
                    break;
                if (Dm.truthy(tool.atom_storage.attempt_remove(tray_item as obj_item, this)))
                {
                    loaded++;
                    AddToGrill(tray_item as obj_item, user);
                }
            }
            if (!Dm.truthy(loaded))
                return ITEM_INTERACT_BLOCKING;
            to_chat(user, span_notice(I18n.T("You insert [] item\\s into [].", loaded, this)));
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (griddled_objects.len >= max_items)
        {
            to_chat(user, span_notice(I18n.T("[] can't fit more items!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        //Center the icon where the user clicked.
        if (!Dm.truthy(LAZYACCESS(modifiers, ICON_X)) || !Dm.truthy(LAZYACCESS(modifiers, ICON_Y)))
            return ITEM_INTERACT_BLOCKING;

        if (!Dm.truthy(await user.transferItemToLoc(tool, this, silent: FALSE)))
            return ITEM_INTERACT_BLOCKING;

        //Clamp it so that the icon never moves more than 16 pixels in either direction (thus leaving the table turf)
        tool.pixel_x = clamp(Dm.num(text2num(LAZYACCESS(modifiers, ICON_X))) - 16, -(ICON_SIZE_X / 2), ICON_SIZE_X / 2);
        tool.pixel_y = clamp(Dm.num(text2num(LAZYACCESS(modifiers, ICON_Y))) - 16, -(ICON_SIZE_Y / 2), ICON_SIZE_Y / 2);
        to_chat(user, span_notice(I18n.T("You place [] on [].", tool, this)));
        AddToGrill(tool, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/machinery/griddle.dm:118
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item item = null, DmList modifiers = null)
    {
        if (item.atom_storage == null)
            return NONE;

        foreach (obj tray_item in Dm.each<obj>(griddled_objects))
            item.atom_storage.attempt_insert(tray_item as obj_item, user, TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/machinery/griddle.dm:126
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        toggle_mode();
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/griddle.dm:130
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        object dot = await base.attack_robot(user, modifiers);
        toggle_mode();
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/griddle.dm:134
    public virtual void toggle_mode()
    {
        on = Dm.num(!Dm.truthy(on));
        if (Dm.truthy(on))
            begin_processing();
        else
            end_processing();
        update_appearance();
        update_grill_audio();
    }

    // tg: code/modules/food_and_drinks/machinery/griddle.dm:143
    public override void begin_processing()
    {
        base.begin_processing();
        foreach (obj_item item_to_grill in Dm.each_cast<obj_item>(griddled_objects))
        {
            if (Dm.truthy(item_to_grill._listen_lookup?[COMSIG_ITEM_GRILL_TURNED_ON]))
                item_to_grill._SendSignal(COMSIG_ITEM_GRILL_TURNED_ON, Dm.list(item_to_grill));
        }
    }

    // tg: code/modules/food_and_drinks/machinery/griddle.dm:148
    public override void end_processing()
    {
        base.end_processing();
        foreach (obj_item item_to_grill in Dm.each_cast<obj_item>(griddled_objects))
        {
            if (Dm.truthy(item_to_grill._listen_lookup?[COMSIG_ITEM_GRILL_TURNED_OFF]))
                item_to_grill._SendSignal(COMSIG_ITEM_GRILL_TURNED_OFF, Dm.list(item_to_grill));
        }
    }

    // tg: code/modules/food_and_drinks/machinery/griddle.dm:153
    public virtual void AddToGrill(obj_item item_to_grill = null, mob user = null)
    {
        Dm.append(vis_contents, item_to_grill);
        Dm.append(ref griddled_objects, item_to_grill);
        item_to_grill.vis_flags = Dm.bitor(item_to_grill.vis_flags, VIS_INHERIT_PLANE);

        if (Dm.truthy(item_to_grill._listen_lookup?[COMSIG_ITEM_GRILL_PLACED]))
            item_to_grill._SendSignal(COMSIG_ITEM_GRILL_PLACED, Dm.list(item_to_grill, user));
        if (Dm.truthy(on))
        {
            if (Dm.truthy(item_to_grill._listen_lookup?[COMSIG_ITEM_GRILL_TURNED_ON]))
                item_to_grill._SendSignal(COMSIG_ITEM_GRILL_TURNED_ON, Dm.list(item_to_grill));
        }
        RegisterSignal(item_to_grill, COMSIG_MOVABLE_MOVED, nameof(ItemMoved));
        RegisterSignal(item_to_grill, COMSIG_ITEM_GRILLED, nameof(GrillCompleted));
        RegisterSignal(item_to_grill, COMSIG_QDELETING, nameof(ItemRemovedFromGrill));
        update_grill_audio();
        update_appearance();
    }

    // tg: code/modules/food_and_drinks/machinery/griddle.dm:167
    [SignalHandler]
    public virtual void ItemRemovedFromGrill(obj_item ungrill = null)
    {
        ungrill.vis_flags = Dm.bitand(ungrill.vis_flags, Dm.bitnot(VIS_INHERIT_PLANE));
        Dm.remove(ref griddled_objects, ungrill);
        Dm.remove(vis_contents, ungrill);
        UnregisterSignal(ungrill, Dm.list(COMSIG_ITEM_GRILLED, COMSIG_MOVABLE_MOVED, COMSIG_QDELETING));
        update_grill_audio();
    }

    // tg: code/modules/food_and_drinks/machinery/griddle.dm:175
    [SignalHandler]
    public virtual void ItemMoved(obj_item I = null, atom OldLoc = null, object Dir = null, object Forced = null)
    {
        ItemRemovedFromGrill(I);
    }

    // tg: code/modules/food_and_drinks/machinery/griddle.dm:179
    [SignalHandler]
    public virtual void GrillCompleted(obj_item source = null, atom grilled_result = null)
    {
        AddToGrill(grilled_result as obj_item);
    }

    // tg: code/modules/food_and_drinks/machinery/griddle.dm:183
    public virtual void update_grill_audio()
    {
        if (Dm.truthy(on) && Dm.truthy(griddled_objects.len))
            grill_loop.start();
        else
            grill_loop.stop();
    }

    // tg: code/modules/food_and_drinks/machinery/griddle.dm:189
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool, time: 2 * SECONDS);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/machinery/griddle.dm:194
    [SignalHandler]
    public virtual object on_storage_dump(datum source = null, datum_storage storage = null, mob user = null)
    {
        foreach (obj_item to_dump in Dm.each<obj_item>(storage.real_location))
        {
            if (griddled_objects.len >= max_items)
                break;

            if (!Dm.truthy(storage.attempt_remove(to_dump, this, silent: TRUE)))
                continue;

            to_dump.pixel_x = to_dump.base_pixel_x + rand(-5, 5);
            to_dump.pixel_y = to_dump.base_pixel_y + rand(-5, 5);
            AddToGrill(to_dump, user);
        }

        to_chat(user, span_notice(I18n.T("You dump out [] onto [].", storage.parent, this)));
        return STORAGE_DUMP_HANDLED;
    }

    // tg: code/modules/food_and_drinks/machinery/griddle.dm:211
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        foreach (obj_item griddled_item in Dm.each_cast<obj_item>(griddled_objects))
        {
            if (Dm.truthy(Dm.bitand(!Dm.truthy(griddled_item._listen_lookup?[COMSIG_ITEM_GRILL_PROCESS]) ? NONE : griddled_item._SendSignal(COMSIG_ITEM_GRILL_PROCESS, Dm.list(griddled_item, this, seconds_per_tick)), COMPONENT_HANDLED_GRILLING)))
                continue;
            Waiting.NoSleep(griddled_item.fire_act(1000)); //Hot hot hot!
            if (prob(10))
                visible_message(span_danger(I18n.T("[] doesn't seem to be doing too great on the []!", griddled_item, this)));

            Waiting.NoSleep(use_energy(active_power_usage));
        }

        object griddle_loc = loc;
        if (isturf(griddle_loc))
            ((turf)griddle_loc).hotspot_expose(800, 100);
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/griddle.dm:225
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("griddle[]_[]", variant, Dm.truthy(on) ? "on" : "off");
        return await base.update_icon_state(__extra);
    }
}

// tg: code/modules/food_and_drinks/machinery/griddle.dm:229
public partial class obj_machinery_griddle_stand : obj_machinery_griddle
{
    // tg: code/modules/food_and_drinks/machinery/griddle.dm:234
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.append(ref dot, "front_bar");
        return dot;
    }
}
