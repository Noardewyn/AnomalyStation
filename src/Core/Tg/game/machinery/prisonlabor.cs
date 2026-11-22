// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/prisonlabor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// Gaps LC1 (lavaland.md L3's labour camp): the whole file. The prison's labour: the license plate press (an empty plate in, four
// seconds of pressing, a filled plate out; a prisoner's ID earns points for each plate loaded) and the produceporter (grown food
// in, a lever pull sends it to a produce pad with points for a prisoner and a supply radio announcement), with the produce pad
// that registers itself as a drop-off. The announcement's config entry is vars only (generated).

using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public partial class datum_controller_global_vars
{
    // tg: code/game/machinery/prisonlabor.dm:221  GLOBAL_LIST_EMPTY(produce_locations)
    public DmList produce_locations;

    // tg: code/game/machinery/prisonlabor.dm:221  (GLOBAL_MANAGED(produce_locations, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalproduce_locations()
    {
        produce_locations = Dm.list();
        Dm.append(ref gvars_datum_init_order, "produce_locations");
    }
}

// License plate printing
// tg: code/game/machinery/prisonlabor.dm:2
public partial class obj_machinery_plate_press : obj_machinery
{
    // tg: code/game/machinery/prisonlabor.dm:15
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (!Dm.truthy(is_operational))
        {
            icon_state = "offline";
            return await base.update_icon_state(__extra);
        }
        if (Dm.truthy(pressing))
        {
            icon_state = "loop";
            return await base.update_icon_state(__extra);
        }
        if (current_plate != null)
        {
            icon_state = "online_loaded";
            return await base.update_icon_state(__extra);
        }
        icon_state = "online";
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/prisonlabor.dm:28
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref current_plate);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/prisonlabor.dm:32
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_stack_license_plates_empty plate)
            return NONE;
        if (!Dm.truthy(is_operational))
        {
            to_chat(user, span_warning(I18n.T("[] has to be on to be loaded!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (current_plate != null)
        {
            to_chat(user, span_warning(I18n.T("[] already has a plate in it!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        await plate.use(1);
        current_plate = Dm.@new(plate.GetType(), this, 1) as obj_item_stack_license_plates_empty; //Spawn a new single sheet in the machine
        update_appearance();
        post_press(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/prisonlabor.dm:49
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (!Dm.truthy(pressing) && current_plate != null)
            await work_press(user as mob_living);
        return dot;
    }

    ///This proc attempts to create a plate. User cannot move during this process.
    // tg: code/game/machinery/prisonlabor.dm:55
    public virtual async DmTask<object> work_press(mob_living user)
    {
        pressing = TRUE;
        update_appearance();
        to_chat(user, span_notice(I18n.T("You start pressing a new license plate!")));
        playsound(this, Dm.resource("sound/machines/kerchunk.ogg"), 80);

        if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: this)))
        {
            pressing = FALSE;
            update_appearance();
            return FALSE;
        }

        await use_energy(active_power_usage);
        to_chat(user, span_notice(I18n.T("You finish pressing a new license plate!")));

        pressing = FALSE;
        QDEL_NULL(ref current_plate);
        update_appearance();

        Dm.@new<obj_item_stack_license_plates_filled>(drop_location());
        return null;
    }

    /// Side effects after successfully pressing a license plate, including granting labor points.
    // tg: code/game/machinery/prisonlabor.dm:77
    public virtual void post_press(mob_living user)
    {
        if (user == null)
            return;
        if (user.get_idcard(TRUE) is not obj_item_card_id_advanced_prisoner prison_id)
            return; // No bonus effects if we're not a prisoner or there's no ID.
        prison_id.points += PRISON_LABOR_PLATE;
        to_chat(user, span_notice(I18n.T("[] points added!", PRISON_LABOR_PLATE)));
    }
}

//Grown crop recepticle.
// tg: code/game/machinery/prisonlabor.dm:89
public partial class obj_machinery_produceporter : obj_machinery
{
    // tg: code/game/machinery/prisonlabor.dm:103
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        update_appearance();
        return dot;
    }

    // tg: code/game/machinery/prisonlabor.dm:108
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (!Dm.truthy(is_operational))
        {
            icon_state = "produceporter_off";
            return await base.update_icon_state(__extra);
        }
        icon_state = "produceporter";
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/prisonlabor.dm:115
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        // DEVIATION(DV12): if(is_operational) . += emissive_appearance(icon, "produceporter_emissive", src), the glow on the emissive
        // plane, is not created
        return dot;
    }

    // tg: code/game/machinery/prisonlabor.dm:120
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (current_produce >= max_produce)
            Dm.append(ref dot, span_notice(I18n.T("\\The []'s bin is full, and can be sent back to the station for labor points.", this)));
        else
            Dm.append(ref dot, span_notice(I18n.T("\\The [] has room for more produce to be inserted.", this)));
        if (!Dm.truthy(length(GLOB.produce_locations)))
            Dm.append(ref dot, span_notice(I18n.T("A dirty screen shows a warning, saying there are no [] to teleport crops to.", span_boldnotice(I18n.T("produce pads")))));
        else
            Dm.append(ref dot, span_notice(I18n.T("The lever can be pulled with [] to send produce back to the station.", span_boldnotice(I18n.T("Right Click")))));
        return dot;
    }

    // tg: code/game/machinery/prisonlabor.dm:131
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_food_grown)
            return NONE;
        if (current_produce >= max_produce)
        {
            balloon_alert(user, I18n.T("bin's full!"));
            return ITEM_INTERACT_FAILURE;
        }
        await tool.forceMove(this);
        playsound(this, Dm.resource("sound/items/handling/component_drop.ogg"), 30);
        update_produce();
        balloon_alert(user, I18n.T("holding [] / []", current_produce, max_produce));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/prisonlabor.dm:143
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        await base.attack_hand_secondary(user, modifiers);
        if (!Dm.truthy(is_operational))
        {
            balloon_alert(user, I18n.T("nothing happens"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        if (!Dm.truthy(current_produce))
        {
            balloon_alert(user, I18n.T("bin's empty!"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        if (!Dm.truthy(length(GLOB.produce_locations)))
        {
            balloon_alert(user, I18n.T("no dropoffs!"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        flick("produceporter_active", this);
        await use_energy(active_power_usage);
        playsound(this, Dm.resource("sound/items/weapons/emitter2.ogg"), 50);
        float points = 0;
        float produce_count = 0;
        foreach (obj_item_food_grown produce in Dm.each<obj_item_food_grown>((contents as DmList)?.Copy()))
        {
            var dropoff = pick(GLOB.produce_locations) as atom;
            await do_teleport(produce, dropoff.drop_location() as atom, 0, asoundout: Dm.resource("sound/machines/woosh.ogg"));
            points += (produce.seed as obj_item_seeds)?.potency > 50 ? (PRISON_LABOR_CROPS * 2) : PRISON_LABOR_CROPS;
            produce_count++;
        }
        balloon_alert_to_viewers(I18n.T("kachunk!"));
        await post_delivery(user as mob_living, points, produce_count);
        update_produce();
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/machinery/prisonlabor.dm:169
    public override async DmTask ShiftClick(mob user = null)
    {
        await base.ShiftClick(user);
        foreach (obj_item_food_grown crops in Dm.each<obj_item_food_grown>((contents as DmList)?.Copy()))
            await crops.forceMove(drop_location() as atom);
        visible_message(I18n.T("\\The []'s bin empties out onto the floor.", this));
    }

    // tg: code/game/machinery/prisonlabor.dm:175
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        base.add_context(source, context, held_item, user);
        if (Dm.truthy(is_operational) && current_produce > 0)
            context[SCREENTIP_CONTEXT_RMB] = I18n.T("Send Produce");
        if (held_item is obj_item_food_grown)
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Deposit Produce");
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/game/machinery/prisonlabor.dm:183
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = ITEM_INTERACT_BLOCKING;
        if (Dm.eq(await default_unfasten_wrench(user, tool, time: 1.5f * SECONDS), SUCCESSFUL_UNFASTEN))
        {
            update_appearance(UPDATE_ICON_STATE);
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/game/machinery/prisonlabor.dm:189
    public virtual object update_produce()
    {
        current_produce = 0;
        foreach (obj_item_food_grown crops in Dm.each<obj_item_food_grown>(contents))
        {
            if (current_produce > max_produce)
                return null;
            current_produce++;
        }
        return current_produce;
    }

    // tg: code/game/machinery/prisonlabor.dm:197
    public virtual async DmTask post_delivery(mob_living user, float labor_points, float produce_count)
    {
        if (user == null || labor_points == 0 || produce_count == 0)
            return;
        var id_card = user.get_idcard(TRUE) as obj_item_card_id;
        if (id_card is not obj_item_card_id_advanced_prisoner prison_id)
            return; // No bonus effects if we're not a prisoner or there's no ID.
        prison_id.points += labor_points;
        to_chat(user, span_notice(I18n.T("[] labor points added!", labor_points)));

        await aas_config_announce(typeof(datum_aas_config_entry_gulag_produce), Dm.list(
            Dm.kv("LOCATION", get_area_name(this)),
            Dm.kv("COUNT", produce_count)), this, Dm.list(RADIO_CHANNEL_SUPPLY));
    }
}

// tg: code/game/machinery/prisonlabor.dm:223
public partial class obj_item_producepad : obj_item
{
    // tg: code/game/machinery/prisonlabor.dm:230
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref GLOB.produce_locations, this);
        return dot;
    }

    // tg: code/game/machinery/prisonlabor.dm:234
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.produce_locations, this);
        return base.Destroy(force);
    }
}
