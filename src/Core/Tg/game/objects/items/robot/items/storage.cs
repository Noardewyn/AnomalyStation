// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/robot/items/storage.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326): the cyborgs' storage apparatuses (beakers, drinks, organs, sheets, circuit boards, food). The
// var blocks are generated (D111).

// tg: code/game/objects/items/robot/items/storage.dm:1
public partial class obj_item_borg_apparatus : obj_item_borg
{
    // tg: code/game/objects/items/robot/items/storage.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        RegisterSignal(loc.loc, COMSIG_BORG_SAFE_DECONSTRUCT, nameof(safedecon));
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/objects/items/robot/items/storage.dm:15
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref stored);
        return base.Destroy(force);
    }

    ///If we're safely deconstructed, we put the item neatly onto the ground, rather than deleting it.
    // tg: code/game/objects/items/robot/items/storage.dm:20
    [SignalHandler]
    public virtual void safedecon()
    {
        if (stored != null)
        {
            Waiting.NoSleep(stored.forceMove(get_turf(this)));
            stored = null;
        }
    }

    // tg: code/game/objects/items/robot/items/storage.dm:27
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == stored) //sanity check
        {
            UnregisterSignal(stored, COMSIG_ATOM_UPDATED_ICON);
            stored = null;
        }
        update_appearance();
        return await base.Exited(gone, direction);
    }

    ///A right-click verb, for those not using hotkey mode.
    // tg: code/game/objects/items/robot/items/storage.dm:35  GAME_VERB(/obj/item/borg/apparatus, verb_dropHeld, "Drop", null)
    // The verb hands its arguments to SSverbs.invoke_verb, which runs the body for a caller with a client (observer.cs's
    // reenter_corpse); the verb panel and SSverbs are M9.
    public virtual void verb_dropHeld(params object[] __extra)
    {
        mob resolved_caller = usr;
        if (resolved_caller?.client == null)
            return;
        Waiting.Detach(__gvb_verb_dropHeld(Dm.list()));
    }

    // tg: code/game/objects/items/robot/items/storage.dm:35  (the verb's body, __gvb_verb_dropHeld)
    public virtual async DmTask __gvb_verb_dropHeld(DmList structured_args = null)
    {
        if (usr != loc || stored == null)
            return;
        await stored.forceMove(get_turf(usr));
    }

    // tg: code/game/objects/items/robot/items/storage.dm:42
    public override object get_proxy_attacker_for(atom target = null, mob user = null)
    {
        if (stored != null) // Use the stored item if available
            return stored;
        return base.get_proxy_attacker_for(target, user);
    }

    /**
    * Attack_self will pass for the stored item.
    */
    // tg: code/game/objects/items/robot/items/storage.dm:50
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (stored == null || !issilicon(user))
            return await base.attack_self(user, modifiers, __extra);
        await stored.attack_self(user);
        return null;
    }

    //Alt click drops the stored item.
    // tg: code/game/objects/items/robot/items/storage.dm:56
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (stored == null || !issilicon(user))
            return CLICK_ACTION_BLOCKING;
        await stored.forceMove(user.drop_location() as atom);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/robot/items/storage.dm:62
    public override async DmTask<object> pre_attack(atom atom = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (atom.loc is mob_living_silicon_robot || atom.loc is obj_item_robot_model || HAS_TRAIT(atom, TRAIT_NODROP))
            return await base.pre_attack(atom, user, modifiers, attack_modifiers); // Borgs should not be grabbing their own modules

        bool itemcheck = false;
        foreach (object storable_type in Dm.each(storable))
        {
            if (istype(atom, (Type)storable_type))
            {
                itemcheck = true;
                break;
            }
        }
        if (itemcheck)
        {
            obj_item item = atom as obj_item;
            item.pixel_x = 0;
            item.pixel_y = 0;
            await item.forceMove(this);
            stored = item;
            RegisterSignal(stored, COMSIG_ATOM_UPDATED_ICON, nameof(on_stored_updated_icon));
            update_appearance();
            return TRUE;
        }
        return await base.pre_attack(atom, user, modifiers, attack_modifiers);
    }

    /**
     * Updates the appearance of the apparatus when the stored object's icon gets updated.
     *
     * Returns NONE as we have not done anything to the stored object itself,
     * which is where this signal that this handler intercepts is sent from.
     */
    // tg: code/game/objects/items/robot/items/storage.dm:88
    [SignalHandler]
    public virtual object on_stored_updated_icon(datum source = null, object updates = null)
    {
        update_appearance();
        return NONE;
    }

    // tg: code/game/objects/items/robot/items/storage.dm:93
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (stored == null)
            return NONE;
        await tool.melee_attack_chain(user, stored, modifiers);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/objects/items/robot/items/storage.dm:99
public partial class obj_item_borg_apparatus_beaker : obj_item_borg_apparatus
{
    // tg: code/game/objects/items/robot/items/storage.dm:109
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(add_glass());
        RegisterSignal(stored, COMSIG_ATOM_UPDATED_ICON, nameof(on_stored_updated_icon));
        update_appearance();
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/objects/items/robot/items/storage.dm:115
    public virtual async DmTask add_glass()
    {
        stored = Dm.@new<obj_item_reagent_containers_cup_beaker_large>(this);
    }

    // tg: code/game/objects/items/robot/items/storage.dm:118
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref stored);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/robot/items/storage.dm:122
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (stored != null)
        {
            obj_item_reagent_containers reagent_container = stored as obj_item_reagent_containers;
            Dm.append(ref dot, I18n.T("The apparatus currently has [] secured, which contains:", reagent_container));
            datum_reagents container_reagents = reagent_container.reagents as datum_reagents;
            if (Dm.truthy(length(container_reagents.reagent_list)))
            {
                foreach (datum_reagent reagent in Dm.each<datum_reagent>(container_reagents.reagent_list))
                    Dm.append(ref dot, I18n.T("[] units of []", reagent.volume, reagent.name));
            }
            else
                Dm.append(ref dot, I18n.T("Nothing."));
        }

        Dm.append(ref dot, span_notice(I18n.T(" <i>Alt-click</i> will drop the currently stored beaker. ")));
        return dot;
    }

    // tg: code/game/objects/items/robot/items/storage.dm:135
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        mutable_appearance arm = mutable_appearance(icon: icon, icon_state: "borg_beaker_apparatus_arm") as mutable_appearance;
        if (stored != null)
        {
            stored.pixel_w = 0;
            stored.pixel_z = 0;
            mutable_appearance stored_copy = Dm.@new<mutable_appearance>(stored);
            if (stored is obj_item_reagent_containers_cup_beaker)
                arm.pixel_z -= 3;
            stored_copy.layer = FLOAT_LAYER;
            stored_copy.plane = FLOAT_PLANE;
            Dm.append(ref dot, stored_copy);
        }
        else
            arm.pixel_z -= 5;
        Dm.append(ref dot, arm);
        return dot;
    }
}

// tg: code/game/objects/items/robot/items/storage.dm:155
public partial class obj_item_borg_apparatus_beaker_service : obj_item_borg_apparatus_beaker
{
    // tg: code/game/objects/items/robot/items/storage.dm:166
    public override async DmTask add_glass()
    {
        stored = Dm.@new<obj_item_reagent_containers_cup_glass_drinkingglass>(this);
        await handle_reflling(stored as obj_item_reagent_containers_cup, loc.loc as mob_living_silicon_robot, force: TRUE);
    }

    // tg: code/game/objects/items/robot/items/storage.dm:170
    public virtual async DmTask handle_reflling(obj_item_reagent_containers_cup glass = null, mob_living_silicon_robot bro = null, float? force = null)
    {
        force ??= FALSE;
        object bro__value = bro;
        if (bro__value == null)
            bro__value = loc;
        if (!iscyborg(bro__value))
            return;

        if (stored == null || Dm.truthy(force))
            await glass.AddComponent(typeof(datum_component_reagent_refiller), Dm.named("power_draw_callback", Dm.@new<datum_callback>(bro__value, nameof(mob_living_silicon_robot.draw_power))));
    }

    // tg: code/game/objects/items/robot/items/storage.dm:179
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (arrived is not obj_item_reagent_containers_cup_glass arrived_glass)
            return null;
        await handle_reflling(arrived_glass);
        return await base.Entered(arrived, old_loc, old_locs);
    }
}

// tg: code/game/objects/items/robot/items/storage.dm:200
public partial class obj_item_borg_apparatus_beaker_service2 : obj_item_borg_apparatus_beaker
{
    // tg: code/game/objects/items/robot/items/storage.dm:198
    public override async DmTask add_glass()
    {
        stored = Dm.@new<obj_item_reagent_containers_cup_glass_drinkingglass>(this);
    }
}

/// allows medical cyborgs to manipulate organs without hands
// tg: code/game/objects/items/robot/items/storage.dm:202
public partial class obj_item_borg_apparatus_organ_storage : obj_item_borg_apparatus
{
    // tg: code/game/objects/items/robot/items/storage.dm:213
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (stored_underlay != null)
            Dm.remove(underlays, stored_underlay);
        if (stored == null)
            return dot;
        stored_underlay = image(stored);
        stored_underlay.layer = FLOAT_LAYER;
        stored_underlay.plane = FLOAT_PLANE;
        stored_underlay.pixel_w = 0;
        stored_underlay.pixel_x = 0;
        stored_underlay.pixel_y = 0;
        stored_underlay.pixel_z = 0;
        Dm.append(underlays, stored_underlay);
        return dot;
    }

    // tg: code/game/objects/items/robot/items/storage.dm:228
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, I18n.T("The organ bag currently contains:"));
        if (stored != null)
        {
            obj_item organ = stored;
            Dm.append(ref dot, organ.name);
        }
        else
            Dm.append(ref dot, I18n.T("Nothing."));
        Dm.append(ref dot, span_notice(I18n.T(" <i>Alt-click</i> will drop the currently stored organ. ")));
        return dot;
    }

    // tg: code/game/objects/items/robot/items/storage.dm:238
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (stored == null)
        {
            to_chat(user, span_notice(I18n.T("[] is empty.", this)));
            return CLICK_ACTION_BLOCKING;
        }

        obj_item organ = stored;
        user.visible_message(span_notice(I18n.T("[] dumps [] from [].", user, organ, this)), span_notice(I18n.T("You dump [] from [].", organ, this)));
        await organ.forceMove(drop_location() as atom);
        return CLICK_ACTION_SUCCESS;
    }
}

///Apparatus to allow Engineering/Sabo borgs to manipulate any material sheets.
// tg: code/game/objects/items/robot/items/storage.dm:249
public partial class obj_item_borg_apparatus_sheet_manipulator : obj_item_borg_apparatus
{
    // tg: code/game/objects/items/robot/items/storage.dm:258
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        update_appearance();
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/objects/items/robot/items/storage.dm:262
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        mutable_appearance arm = mutable_appearance(icon, "borg_stack_apparatus_arm1") as mutable_appearance;
        if (stored != null)
        {
            stored.pixel_w = 0;
            stored.pixel_z = 0;
            arm.icon_state = "borg_stack_apparatus_arm2";
            mutable_appearance stored_copy = Dm.@new<mutable_appearance>(stored);
            object underscore = findtext(stored_copy.icon_state, "_");
            if (Dm.truthy(underscore))
                stored_copy.icon_state = Dm.str(Dm.initial(stored, "icon_state")); //how we use the icon_state of single sheets, even with full stacks
            stored_copy.layer = FLOAT_LAYER;
            stored_copy.plane = FLOAT_PLANE;
            Dm.append(ref dot, stored_copy);
        }
        Dm.append(ref dot, arm);
        return dot;
    }

    // tg: code/game/objects/items/robot/items/storage.dm:278
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (stored != null)
            Dm.append(ref dot, I18n.T("The apparatus currently has [] secured.", stored));
        Dm.append(ref dot, span_notice(I18n.T(" <i>Alt-click</i> will drop the currently stored sheets. ")));
        return dot;
    }
}

///Apparatus allowing Engineer/Sabo borgs to manipulate circuit boards and more
// tg: code/game/objects/items/robot/items/storage.dm:285
public partial class obj_item_borg_apparatus_engineering : obj_item_borg_apparatus
{
    // tg: code/game/objects/items/robot/items/storage.dm:297
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        update_appearance();
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/objects/items/robot/items/storage.dm:301
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        mutable_appearance arm = mutable_appearance(icon, "borg_hardware_apparatus_arm1") as mutable_appearance;
        if (stored != null)
        {
            stored.pixel_w = -3;
            stored.pixel_z = 0;
            if (stored is not obj_item_circuitboard)
                arm.icon_state = "borg_hardware_apparatus_arm2";
            else if (stored is obj_item_circuitboard) // tg: istype(stored, /obj/item/circuitboard || /obj/item/stock_parts/power_store) reads the first path
            {
                stored.pixel_w = -5;
                stored.pixel_z = 2;
                arm.icon_state = "borg_hardware_apparatus_arm1";
            }
            mutable_appearance stored_copy = Dm.@new<mutable_appearance>(stored);
            stored_copy.layer = FLOAT_LAYER;
            stored_copy.plane = FLOAT_PLANE;
            Dm.append(ref dot, stored_copy);
        }
        Dm.append(ref dot, arm);
        return dot;
    }

    // tg: code/game/objects/items/robot/items/storage.dm:319
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (stored != null)
            Dm.append(ref dot, I18n.T("The apparatus currently has [] secured.", stored));
        Dm.append(ref dot, span_notice(I18n.T(" <i>Alt-click</i> will drop the currently stored item. ")));
        return dot;
    }

    // tg: code/game/objects/items/robot/items/storage.dm:325
    public override async DmTask<object> pre_attack(atom atom = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (atom is obj_item_ai_module_law && stored == null) //If an admin wants a borg to upload laws, who am I to stop them? Otherwise, we can hint that it fails
        {
            to_chat(user, span_warning(I18n.T("This circuit board doesn't seem to have standard robot apparatus pin holes. You're unable to pick it up.")));
            return TRUE;
        }
        return await base.pre_attack(atom, user, modifiers, attack_modifiers);
    }

    // stops them from cell interactions with other borgos
    // tg: code/game/objects/items/robot/items/storage.dm:332
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (iscyborg(user) && iscyborg(interacting_with))
        {
            balloon_alert(user, I18n.T("your manipulator isn't dexterous enough to interact with this properly."));
            return ITEM_INTERACT_FAILURE;
        }
        return null;
    }
}

// tg: code/game/objects/items/robot/items/storage.dm:337
public partial class obj_item_borg_apparatus_service : obj_item_borg_apparatus
{
    // tg: code/game/objects/items/robot/items/storage.dm:352
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        update_appearance();
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/objects/items/robot/items/storage.dm:356
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        mutable_appearance arm = mutable_appearance(icon, "borg_hardware_apparatus_arm1") as mutable_appearance;
        if (stored != null)
        {
            stored.pixel_w = -3;
            stored.pixel_z = 0;
            if ((stored is not obj_item_plate_oven_tray) || (stored is not obj_item_food))
                arm.icon_state = "borg_hardware_apparatus_arm2";
            mutable_appearance stored_copy = Dm.@new<mutable_appearance>(stored);
            stored_copy.layer = FLOAT_LAYER;
            stored_copy.plane = FLOAT_PLANE;
            Dm.append(ref dot, stored_copy);
        }
        Dm.append(ref dot, arm);
        return dot;
    }

    // tg: code/game/objects/items/robot/items/storage.dm:370
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (stored != null)
            Dm.append(ref dot, I18n.T("The apparatus currently has [] secured.", stored));
        Dm.append(ref dot, span_notice(I18n.T("<i>Alt-click</i> will drop the currently secured item.")));
        return dot;
    }
}
