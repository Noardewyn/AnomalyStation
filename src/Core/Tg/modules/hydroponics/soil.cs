// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/soil.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file: soil and the advanced soils, the soil sacks. The sack's `stored_soil` is hand-declared (rule
// V3 types it Type; tg stores the bagged soil machine in it).

///////////////////////////////////////////////////////////////////////////////
// tg: code/modules/hydroponics/soil.dm:4
public partial class obj_machinery_hydroponics_soil : obj_machinery_hydroponics //Not actually hydroponics at all! Honk!
{
    // tg: code/modules/hydroponics/soil.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_SCREWDRIVER));
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_CROWBAR));
        return dot;
    }

    // tg: code/modules/hydroponics/soil.dm:25
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = await base.update_icon(updates, __extra);
        if (Dm.truthy(self_sustaining))
            await add_atom_colour(rgb(255, 175, 0), FIXED_COLOUR_PRIORITY);
        return dot;
    }

    // tg: code/modules/hydroponics/soil.dm:30
    public override object update_status_light_overlays()
    {
        return null; // Has no lights
    }

    // tg: code/modules/hydroponics/soil.dm:33
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (!Dm.eq(tool.tool_behaviour, TOOL_SHOVEL)) //Spades can still uproot plants on left click
            return await base.item_interaction_secondary(user, tool, modifiers);

        balloon_alert(user, I18n.T("digging up soil..."));
        if (!Dm.truthy(await tool.use_tool(this, user, 3 * SECONDS, volume: 50)))
            return ITEM_INTERACT_BLOCKING;

        balloon_alert(user, I18n.T("bagged"));
        Dm.@new(sack_type, loc, this); //The bag handles sucking up the soil, stopping processing and setting relevants stats.
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/hydroponics/soil.dm:45
    public override async DmTask<object> click_ctrl(mob user = null)
    {
        return CLICK_ACTION_BLOCKING; //Soil has no electricity.
    }

    // tg: code/modules/hydroponics/soil.dm:48
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        Dm.@new<obj_item_stack_ore_glass>(drop_location(), 3);
        return null;
    }

    ///called when a soil is plopped down on the ground.
    // tg: code/modules/hydroponics/soil.dm:52
    public virtual void on_place()
    {
        return;
    }
}

// tg: code/modules/hydroponics/soil.dm:92
public partial class obj_machinery_hydroponics_soil_worm : obj_machinery_hydroponics_soil
{
    // tg: code/modules/hydroponics/soil.dm:102
    public override void on_place()
    {
        base.on_place();
        flick("soil_worm_wiggle", this);
    }
}

/////////////////// Soil Sacks ///////////////////////
/// Holder items that store the soils until deployed.
// tg: code/modules/hydroponics/soil.dm:114
public partial class obj_item_soil_sack : obj_item
{
    // tg: code/modules/hydroponics/soil.dm:140  (hand-declared: generated as a path, rule V3; tg stores the bagged soil here)
    public object stored_soil = typeof(obj_machinery_hydroponics_soil);

    // tg: code/modules/hydroponics/soil.dm:116  (the generated constructor, kept with the hand-declared var: types-converter.md 3.2)
    public obj_item_soil_sack()
    {
        name = "soil sack";
        desc = "A large plastic bag containing commercial garden soil. It is packed with sand, peat and manure. While you might not care much for such mixture, the plants have strange tastes.";
        icon = Dm.resource("icons/obj/service/hydroponics/equipment.dmi");
        icon_state = "soil_sack";
        inhand_icon_state = "soil_sack";
        lefthand_file = Dm.resource("icons/mob/inhands/equipment/hydroponics_lefthand.dmi");
        righthand_file = Dm.resource("icons/mob/inhands/equipment/hydroponics_righthand.dmi");
        base_icon_state = "soil_sack";
        force = 7;
        throwforce = 17;
        attack_speed = 12;
        damtype = "stamina";
        block_sound = Dm.resource("sound/effects/bodyfall/bodyfall1.ogg");
        w_class = 5;
        item_flags = 8;
        resistance_flags = 32;
        hitsound = Dm.resource("sound/items/pillow/pillow_hit.ogg");
        drop_sound = Dm.resource("sound/effects/footstep/woodbarefoot3.ogg");
        throw_drop_sound = Dm.resource("sound/effects/bodyfall/bodyfall3.ogg");
        custom_premium_price = 50;
        throw_range = 3;
        throw_speed = 1;
        slowdown = 1;
        drag_slowdown = 1;
    }

    // tg: code/modules/hydroponics/soil.dm:143
    [DmParams("mapload", "outside_soil")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_machinery_hydroponics_soil outside_soil = Dm.arg(__extra, 0) as obj_machinery_hydroponics_soil;
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed), Dm.named("force_multiplier", 2), Dm.named("wield_callback", Dm.@new<datum_callback>(this, nameof(on_wield))), Dm.named("unwield_callback", Dm.@new<datum_callback>(this, nameof(on_unwield)))));

        if (outside_soil != null)
        {
            stored_soil = outside_soil;
            Waiting.NoSleep(outside_soil.remove_plant());
            Waiting.NoSleep(outside_soil.forceMove(this));
            STOP_PROCESSING(SSmachines, outside_soil);
            animate(this, 100 * MILLISECONDS, Dm.named("pixel_z", 4), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_OUT)));
            animate(Dm.named("time", 100 * MILLISECONDS), Dm.named("pixel_z", 0), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_IN)));
            animate(Dm.named("time", 250 * MILLISECONDS), Dm.named("pixel_x", rand(-6, 6)), Dm.named("pixel_y", rand(-4, 4)), Dm.named("flags", ANIMATION_PARALLEL));
        }
        return dot;
    }

    // tg: code/modules/hydroponics/soil.dm:156
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone != null && gone == stored_soil)
        {
            stored_soil = null;
            qdel(this);
        }
        return dot;
    }

    // tg: code/modules/hydroponics/soil.dm:162
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isopenturf(interacting_with) || isgroundlessturf(interacting_with))
            return await base.interact_with_atom(interacting_with, user, modifiers);

        if (Dm.locate<obj_machinery_hydroponics_soil>(interacting_with) != null)
        {
            to_chat(user, span_alert(I18n.T("There is already a bed of soil there!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await do_after(user, 1 * SECONDS, interacting_with)))
            return ITEM_INTERACT_BLOCKING;

        await transfer_soil(interacting_with);
        return ITEM_INTERACT_SUCCESS;
    }

    //Proc responsible for placing the soil inside track onto the turf or inside a hydroponic tray
    // tg: code/modules/hydroponics/soil.dm:177
    public virtual async DmTask<object> transfer_soil(atom target = null, float? inside_tray = null)
    {
        inside_tray ??= FALSE;
        if (ispath(stored_soil))
        {
            obj_machinery_hydroponics_soil made = Dm.@new((Type)stored_soil, this) as obj_machinery_hydroponics_soil;
            stored_soil = made;
            if (Dm.truthy(inside_tray))
                STOP_PROCESSING(SSmachines, made);
            await ((datum_reagents)made.reagents).add_reagent(typeof(datum_reagent_plantnutriment_eznutriment), made.maxnutri / 2);
            made.waterlevel = made.maxwater;
        }
        else if (!Dm.truthy(inside_tray))
        {
            START_PROCESSING(SSmachines, stored_soil as datum);
        }

        obj_machinery_hydroponics_soil soil = stored_soil as obj_machinery_hydroponics_soil;
        playsound(target, placement_sound, 65, vary: TRUE);
        if (!Dm.truthy(inside_tray))
            soil.on_place();
        obj_machinery_hydroponics soil_ref = soil;
        await soil.forceMove(target); //stored_soil is set to null at this point, and the soil sack is deleted when that happens
        return soil_ref;
    }

    // tg: code/modules/hydroponics/soil.dm:194
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        if (attack_type == OVERWHELMING_ATTACK)
            return FALSE;
        return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
    }

    ///Remove slowdown and add block chance when wielded.
    // tg: code/modules/hydroponics/soil.dm:200
    public virtual async DmTask on_wield()
    {
        slowdown = 0;
        if (loc is mob wearer)
            await wearer.update_equipment_speed_mods();
        block_chance = 25;
        inhand_icon_state = Dm.text("[]_w", base_icon_state);
    }

    ///Reapply slowdown and remove block chance when unwielded.
    // tg: code/modules/hydroponics/soil.dm:209
    public virtual async DmTask on_unwield()
    {
        slowdown = Dm.num(Dm.initial(this, "slowdown"));
        if (loc is mob wearer)
            await wearer.update_equipment_speed_mods();
        block_chance = Dm.num(Dm.initial(this, "block_chance"));
        inhand_icon_state = base_icon_state;
    }
}
