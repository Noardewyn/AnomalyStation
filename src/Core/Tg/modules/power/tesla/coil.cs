// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/tesla/coil.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 zaps slice: the whole file, the tesla coil and the grounding rod (MetaStation's engine room has two of each, anchored).
// The var blocks are generated (D111).

// tg: code/modules/power/tesla/coil.dm:1
public partial class obj_machinery_power_energy_accumulator_tesla_coil : obj_machinery_power_energy_accumulator
{
    // tg: code/modules/power/tesla/coil.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        set_wires(Dm.@new<datum_wires_tesla_coil>(this));
        return dot;
    }

    // tg: code/modules/power/tesla/coil.dm:38
    public override async DmTask<object> cable_layer_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(panel_open))
            return NONE;
        if (Dm.truthy(anchored))
        {
            balloon_alert(user, I18n.T("unanchor first!"));
            return ITEM_INTERACT_BLOCKING;
        }
        return await base.cable_layer_act(user, tool);
    }

    // tg: code/modules/power/tesla/coil.dm:46
    public override async DmTask<object> RefreshParts()
    {
        object dot = null;
        dot = await base.RefreshParts();
        float power_multiplier = 0;
        zap_cooldown = 100;
        foreach (datum_stock_part_capacitor capacitor in Dm.each<datum_stock_part_capacitor>(component_parts))
        {
            power_multiplier += Dm.num(capacitor.tier);
            zap_cooldown -= (Dm.num(capacitor.tier) * 20);
        }
        input_power_multiplier = max(1 * (power_multiplier / 8), 0.25f); //Max out at 50% efficency.
        return dot;
    }

    // tg: code/modules/power/tesla/coil.dm:55
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        if (Dm.truthy(in_range(user, this)) || isobserver(user))
        {
            Dm.append(ref dot, span_notice(I18n.T("The status display reads:<br>Power generation at <b>[]%</b>.<br>Shock interval at <b>[]</b> seconds.<br>Stored <b>[]</b>.<br>Processing <b>[]</b>.",
                input_power_multiplier * 100, zap_cooldown * 0.1f, display_energy(get_stored_joules()), display_power(processed_energy))));
        }
        return dot;
    }

    // tg: code/modules/power/tesla/coil.dm:64
    public override async DmTask<object> set_anchored(object anchorvalue = null)
    {
        object dot = null;
        dot = await base.set_anchored(anchorvalue);
        update_cable_icons_on_turf(get_turf(this));
        return dot;
    }

    // tg: code/modules/power/tesla/coil.dm:68
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = null;
        dot = await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/tesla/coil.dm:73
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][][]", base_icon_state, Dm.truthy(panel_open) ? "_open" : "", anchored);
        return dot;
    }

    // tg: code/modules/power/tesla/coil.dm:77
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/power/tesla/coil.dm:80
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/power/tesla/coil.dm:83
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        object dot = null;
        dot = base.process(seconds_per_tick, __extra);
        zap_sound_volume = min(Dm.num(energy_to_power(processed_energy)) / (4 * KILO * WATTS), 100); // 1 sound volume per 4kW.
        zap_sound_range = min(Dm.num(energy_to_power(processed_energy)) / (80 * KILO * WATTS), 10); // 1 sound range per 80kW.
        return dot;
    }

    // tg: code/modules/power/tesla/coil.dm:88
    public override async DmTask<object> zap_act(object power = null, object zap_flags = null)
    {
        if (!Dm.truthy(anchored) || Dm.truthy(panel_open))
            return await base.zap_act(power, zap_flags);
        ADD_TRAIT(this, TRAIT_BEING_SHOCKED, WAS_SHOCKED);
        _addtimer(TRAIT_CALLBACK_REMOVE(this, TRAIT_BEING_SHOCKED, WAS_SHOCKED), 1 * SECONDS, file: "code/modules/power/tesla/coil.dm", line: 92);
        flick(Dm.text("[]hit", base_icon_state), this);
        if (!Dm.truthy(Dm.bitand(zap_flags, ZAP_GENERATES_POWER))) //Prevent infinite recursive power
            return 0;
        if (Dm.truthy(Dm.bitand(zap_flags, ZAP_LOW_POWER_GEN)))
            power = Dm.div(power, 10);
        await zap_buckle_check(power);
        float power_removed = Dm.truthy(powernet) ? Dm.num(power) * input_power_multiplier : Dm.num(power);
        stored_energy += max(power_removed, 0);
        return max(Dm.num(power) - power_removed, 0); //You get back the amount we didn't use
    }

    // tg: code/modules/power/tesla/coil.dm:103
    public virtual async DmTask<object> zap()
    {
        if ((last_zap + zap_cooldown) > world.time || !Dm.truthy(powernet))
            return FALSE;
        last_zap = world.time;
        float power = (((datum_powernet)powernet).avail) * 0.2f * input_power_multiplier; //Always always always use more then you output for the love of god
        power = min(Dm.num(surplus()), power); //Take the smaller of the two
        add_load(power);
        playsound(loc, Dm.resource("sound/effects/magic/lightningshock.ogg"), zap_sound_volume, TRUE, zap_sound_range);
        await tesla_zap(source: this, zap_range: 10, power: power, cutoff: 1e3f, zap_flags: zap_flags);
        await zap_buckle_check(power);
        return null;
    }
}

// tg: code/modules/power/tesla/coil.dm:115
public partial class obj_machinery_power_energy_accumulator_grounding_rod : obj_machinery_power_energy_accumulator
{
    // tg: code/modules/power/tesla/coil.dm:133
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        if (Dm.truthy(in_range(user, this)) || isobserver(user))
        {
            Dm.append(ref dot, span_notice(I18n.T("The status display reads:<br>Recently grounded <b>[]</b>.<br>This energy would sustainably release <b>[]</b>.",
                display_energy(get_stored_joules()), display_power(calculate_sustainable_power(), convert: FALSE))));
        }
        return dot;
    }

    // tg: code/modules/power/tesla/coil.dm:140
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = null;
        dot = await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/tesla/coil.dm:145
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][][]", base_icon_state, Dm.truthy(panel_open) ? "_open" : "", anchored);
        return dot;
    }

    // tg: code/modules/power/tesla/coil.dm:149
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/power/tesla/coil.dm:152
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/power/tesla/coil.dm:155
    public override async DmTask<object> zap_act(object power = null, object zap_flags = null)
    {
        object energy = power;
        if (Dm.truthy(anchored) && !Dm.truthy(panel_open))
        {
            flick(Dm.text("[]hit", base_icon_state), this);
            await zap_buckle_check(energy);
            stored_energy += Dm.num(energy);
            return 0;
        }
        return await base.zap_act(energy, zap_flags);
    }

    // tg: code/modules/power/tesla/coil.dm:163
    public override object release_energy(float? joules = null)
    {
        joules ??= 0;
        stored_energy -= joules.Value;
        processed_energy = joules.Value;
        return FALSE; //Grounding rods don't release energy to the grid.
    }
}
