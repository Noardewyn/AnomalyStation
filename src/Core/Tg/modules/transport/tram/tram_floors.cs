// Translated from tgstation (snapshot 1d99bd7f): code/modules/transport/tram/tram_floors.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// The tram port (2026-11-12): the whole file.

// tg: code/modules/transport/tram/tram_floors.dm:9
public partial class turf_open_floor_tram : turf_open_floor
{
    // tg: code/modules/transport/tram/tram_floors.dm:22
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        Dm.append(ref dot, await base.examine(user, __extra));
        Dm.append(ref dot, span_notice(I18n.T("The reinforcement bolts are [] firmly in place. Use a [] to remove the plate.", EXAMINE_HINT("wrenched"), EXAMINE_HINT("wrench"))));
        return dot;
    }

    // tg: code/modules/transport/tram/tram_floors.dm:26
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_stack_thermoplastic tiles)
        {
            await build_with_transport_tiles(tiles, user);
            return ITEM_INTERACT_SUCCESS;
        }
        if (tool is obj_item_stack_sheet_mineral_titanium titanium)
        {
            await build_with_titanium(titanium, user);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/modules/transport/tram/tram_floors.dm:35
    public override async DmTask<object> make_plating(float? force = null)
    {
        force ??= FALSE;
        if (Dm.truthy(force))
            return await base.make_plating(force);
        return null; //unplateable
    }

    // tg: code/modules/transport/tram/tram_floors.dm:40
    public override async DmTask<object> try_replace_tile(obj_item_stack_tile tile = null, mob user = null, DmList modifiers = null)
    {
        return null;
    }

    // tg: code/modules/transport/tram/tram_floors.dm:43
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return null;
    }

    // tg: code/modules/transport/tram/tram_floors.dm:46
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        to_chat(user, span_notice(I18n.T("You begin removing the plate...")));
        if (Dm.truthy(await tool.use_tool(this, user, 30, volume: 80)))
        {
            if (this is not turf_open_floor_tram)
                return TRUE;
            if (floor_tile != null)
                Dm.@new(floor_tile, this, 2);
            await ScrapeAway(flags: CHANGETURF_INHERIT_AIR);
        }
        return TRUE;
    }

    // tg: code/modules/transport/tram/tram_floors.dm:57
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        if (target == this)
        {
            Waiting.NoSleep(ScrapeAway(flags: CHANGETURF_INHERIT_AIR));
            return TRUE;
        }
        if (Dm.truthy(is_explosion_shielded(severity)))
            return FALSE;
        float level = Dm.num(severity);
        if (level == EXPLODE_DEVASTATE)
        {
            if (prob(80))
            {
                if (!ispath(baseturf_at_depth(2), typeof(turf_open_floor)))
                    Waiting.NoSleep(attempt_lattice_replacement());
                else
                    Waiting.NoSleep(ScrapeAway(2, flags: CHANGETURF_INHERIT_AIR));
            }
            else
            {
                break_tile();
            }
        }
        else if (level == EXPLODE_HEAVY)
        {
            if (prob(30))
            {
                if (!ispath(baseturf_at_depth(2), typeof(turf_open_floor)))
                    Waiting.NoSleep(attempt_lattice_replacement());
                else
                    Waiting.NoSleep(ScrapeAway(2, flags: CHANGETURF_INHERIT_AIR));
            }
            else
            {
                break_tile();
            }
        }
        else if (level == EXPLODE_LIGHT)
        {
            if (prob(50))
                break_tile();
        }
        return TRUE;
    }

    // tg: code/modules/transport/tram/tram_floors.dm:83
    public override object broken_states() => Dm.list("tram_platform-damaged1", "tram_platform-damaged2");
}

// tg: code/modules/transport/tram/tram_floors.dm:89
public partial class turf_open_floor_tram_tram_platform : turf_open_floor_tram
{
    // tg: code/modules/transport/tram/tram_floors.dm:86
    public override object burnt_states() => Dm.list("tram_platform-scorched1", "tram_platform-scorched2");
}

// tg: code/modules/transport/tram/tram_floors.dm:93
public partial class turf_open_floor_tram_plate : turf_open_floor_tram
{
    // tg: code/modules/transport/tram/tram_floors.dm:101
    public override object broken_states() => Dm.list("tram_plate-damaged1", "tram_plate-damaged2");

    // tg: code/modules/transport/tram/tram_floors.dm:104
    public override object burnt_states() => Dm.list("tram_plate-scorched1", "tram_plate-scorched2");
}

// tg: code/modules/transport/tram/tram_floors.dm:107
public partial class turf_open_floor_tram_plate_energized : turf_open_floor_tram_plate
{
    // tg: code/modules/transport/tram/tram_floors.dm:116
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_energized), inbound, outbound, specific_transport_id));
        return dot;
    }

    // tg: code/modules/transport/tram/tram_floors.dm:120
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(broken) || Dm.truthy(burnt))
        {
            Dm.append(ref dot, span_danger(I18n.T("It looks damaged and the electrical components exposed!")));
            Dm.append(ref dot, span_notice(I18n.T("The plate can be repaired using a [].", EXAMINE_HINT("titanium sheet"))));
        }
        return dot;
    }

    // tg: code/modules/transport/tram/tram_floors.dm:126
    public override object broken_states() => Dm.list("energized_plate_damaged");

    // tg: code/modules/transport/tram/tram_floors.dm:129
    public override object burnt_states() => Dm.list("energized_plate_damaged");

    // tg: code/modules/transport/tram/tram_floors.dm:132
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(broken) && !Dm.truthy(burnt))
            return NONE;
        if (tool is not obj_item_stack_sheet_mineral_titanium titanium)
            return NONE;
        if (!Dm.truthy(await titanium.use(1)))
            return ITEM_INTERACT_BLOCKING;
        broken = FALSE;
        update_appearance();
        balloon_alert(user, I18n.T("plate replaced"));
        return ITEM_INTERACT_SUCCESS;
    }
}

// Resetting the tram contents to its original state needs the turf to be there
// tg: code/modules/transport/tram/tram_floors.dm:148
public partial class turf_open_indestructible_tram : turf_open_indestructible
{
    // tg: code/modules/transport/tram/tram_floors.dm:158
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_stack_thermoplastic tiles)
        {
            await build_with_transport_tiles(tiles, user);
            return ITEM_INTERACT_SUCCESS;
        }
        if (tool is obj_item_stack_sheet_mineral_titanium titanium)
        {
            await build_with_titanium(titanium, user);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }
}

// tg: code/modules/transport/tram/tram_floors.dm:178
public partial class turf_open_floor_glass_reinforced_tram : turf_open_floor_glass_reinforced
{
    // tg: code/modules/transport/tram/tram_floors.dm:174
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RemoveElement(typeof(datum_element_atmos_sensitive), mapload);
        return dot;
    }
}

// tg: code/modules/transport/tram/tram_floors.dm:182
public partial class obj_structure_thermoplastic : obj_structure
{
    // tg: code/modules/transport/tram/tram_floors.dm:202
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_force_move_pulled)));
        return dot;
    }

    // tg: code/modules/transport/tram/tram_floors.dm:219
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(secured))
        {
            Dm.append(ref dot, span_notice(I18n.T("It is secured with a set of [] To remove tile use a []", EXAMINE_HINT("screws."), EXAMINE_HINT("screwdriver."))));
        }
        else
        {
            Dm.append(ref dot, span_notice(I18n.T("You can [] to remove the tile.", EXAMINE_HINT("crowbar"))));
            Dm.append(ref dot, span_notice(I18n.T("It can be re-secured using a []", EXAMINE_HINT("screwdriver."))));
        }
        return dot;
    }

    // tg: code/modules/transport/tram/tram_floors.dm:227
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        if (Dm.truthy(dot)) //received damage
            update_appearance();
        return dot;
    }

    // tg: code/modules/transport/tram/tram_floors.dm:232
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        float ratio = Dm.num(atom_integrity) / Dm.num(max_integrity);
        ratio = ceil(ratio * 4) * 25;
        if (ratio > 75)
        {
            icon_state = Dm.text("[]", base_icon_state);
            return dot;
        }
        icon_state = Dm.text("[]_damage[]", base_icon_state, ratio);
        return dot;
    }

    // tg: code/modules/transport/tram/tram_floors.dm:242
    public override async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null)
    {
        await base.screwdriver_act_secondary(user, tool);
        if (Dm.truthy(secured))
        {
            user.visible_message(span_notice(I18n.T("[] begins to unscrew the tile...", user)), span_notice(I18n.T("You begin to unscrew the tile...")));
            if (Dm.truthy(await tool.use_tool(this, user, 1 * SECONDS, volume: 50)))
            {
                secured = FALSE;
                to_chat(user, span_notice(I18n.T("The screws come out, and a gap forms around the edge of the tile.")));
            }
        }
        else
        {
            user.visible_message(span_notice(I18n.T("[] begins to fasten the tile...", user)), span_notice(I18n.T("You begin to fasten the tile...")));
            if (Dm.truthy(await tool.use_tool(this, user, 1 * SECONDS, volume: 50)))
            {
                secured = TRUE;
                to_chat(user, span_notice(I18n.T("The tile is securely screwed in place.")));
            }
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/transport/tram/tram_floors.dm:258
    public override async DmTask<object> crowbar_act_secondary(mob_living user = null, obj_item tool = null)
    {
        await base.crowbar_act_secondary(user, tool);
        if (Dm.truthy(secured))
        {
            to_chat(user, span_warning(I18n.T("The security screws need to be removed first!")));
            return FALSE;
        }
        else
        {
            user.visible_message(span_notice(I18n.T("[] wedges [] into the tile's gap in the edge and starts prying...", user, tool)), span_notice(I18n.T("You wedge [] into the tram panel's gap in the frame and start prying...", tool)));
            if (Dm.truthy(await tool.use_tool(this, user, 1 * SECONDS, volume: 50)))
            {
                to_chat(user, span_notice(I18n.T("The panel pops out of the frame.")));
                obj_item_stack_thermoplastic pulled_tile = Dm.@new(floor_tile) as obj_item_stack_thermoplastic;
                pulled_tile.update_integrity(atom_integrity);
                await user.put_in_hands(pulled_tile);
                qdel(this);
            }
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/transport/tram/tram_floors.dm:275
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.num(atom_integrity) >= Dm.num(max_integrity))
        {
            to_chat(user, span_warning(I18n.T("[] is already in good condition!", this)));
            return ITEM_INTERACT_SUCCESS;
        }
        if (!Dm.truthy(tool.tool_start_check(user, amount: 0, heat_required: HIGH_TEMPERATURE_REQUIRED)))
            return FALSE;
        to_chat(user, span_notice(I18n.T("You begin repairing []...", this)));
        float integrity_to_repair = Dm.num(max_integrity) - Dm.num(atom_integrity);
        if (Dm.truthy(await tool.use_tool(this, user, integrity_to_repair * 0.5f, volume: 50)))
        {
            atom_integrity = max_integrity;
            to_chat(user, span_notice(I18n.T("You repair [].", this)));
            update_appearance();
        }
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/transport/tram/tram_floors.dm:291
[DmPath("/obj/item/stack/thermoplastic")]
public partial class obj_item_stack_thermoplastic : obj_item_stack
{
    // tg: code/modules/transport/tram/tram_floors.dm:318
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        pixel_x = rand(-3, 3);
        pixel_y = rand(-3, 3); //randomize a little
        return dot;
    }

    // tg: code/modules/transport/tram/tram_floors.dm:323  (X3)
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(throwforce) && !Dm.truthy(is_cyborg)) //do not want to divide by zero or show the message to borgs who can't throw
        {
            object damage_value = null;
            float throws_to_crit = ceil(MAX_LIVING_HEALTH / Dm.num(throwforce)); //throws to crit a human
            if (throws_to_crit >= 1 && throws_to_crit <= 3)
                damage_value = I18n.T("superb");
            else if (throws_to_crit >= 4 && throws_to_crit <= 6)
                damage_value = I18n.T("great");
            else if (throws_to_crit >= 7 && throws_to_crit <= 9)
                damage_value = I18n.T("good");
            else if (throws_to_crit >= 10 && throws_to_crit <= 12)
                damage_value = I18n.T("fairly decent");
            else if (throws_to_crit >= 13 && throws_to_crit <= 15)
                damage_value = I18n.T("mediocre");
            if (!Dm.truthy(damage_value))
                return dot;
            Dm.append(ref dot, span_notice(I18n.T("Those could work as a [] throwing weapon.", damage_value)));
        }
        return dot;
    }
}
