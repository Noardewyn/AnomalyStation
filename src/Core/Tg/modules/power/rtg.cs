// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/rtg.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering package E11: the radioisotope thermoelectric generator and its subtypes. The RTG adds its free power to the cable every
// machines cycle, its capacitors' tiers multiply it, its panel shows the open sprite, the screwdriver and crowbar deconstruct it; the
// advanced, debug, lavaland (power only on a mining level's lava) and old station (it falls apart to ash) RTGs, and the abductors'
// void core, which overloads (sparks, a tesla zap, an explosion ten seconds later) when shot, blasted, burnt, blobbed or zapped by an
// explosive zap. Not here: the variable editor's refresh (vv_edit_var, admin tools). The var blocks are generated.

// Radioisotope Thermoelectric Generator (RTG)
// Simple power generator that would replace "magic SMES" on various derelicts.

// tg: code/modules/power/rtg.dm:4
public partial class obj_machinery_power_rtg : obj_machinery_power
{
    // tg: code/modules/power/rtg.dm:26
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base_power_gen = power_gen;
        object dot = base.Initialize(mapload, __extra);
        connect_to_network();
        Waiting.NoSleep(RefreshParts());
        return dot;
    }

    // tg: code/modules/power/rtg.dm:32
    [DmParams()]
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        add_avail(power_to_energy(power_gen));
        return null;
    }

    // tg: code/modules/power/rtg.dm:35
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        float new_power_gen = Dm.num(get_base_power_gen());
        if (Dm.truthy(affected_by_parts))
        {
            float part_level = 0;
            foreach (datum_stock_part stock_part in Dm.each<datum_stock_part>(component_parts))
                part_level += Dm.num(stock_part.tier);

            new_power_gen = Dm.num(base_power_gen) * (Dm.truthy(part_level) ? part_level : 1);
        }

        power_gen = new_power_gen;
        return dot;
    }

    // tg: code/modules/power/rtg.dm:47
    public virtual object get_base_power_gen()
    {
        return base_power_gen;
    }

    // tg: code/modules/power/rtg.dm:50
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (in_range(user, this) || isobserver(user))
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: Power generation at <b>[]</b>.", display_power(power_gen, convert: FALSE))));
        return dot;
    }

    // tg: code/modules/power/rtg.dm:55
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(panel_open) ? Dm.text("[]-open", base_icon_state) : (string)base_icon_state;
        return dot;
    }

    // tg: code/modules/power/rtg.dm:59
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/power/rtg.dm:62
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }
}

// Void Core, power source for Abductor ships and bases.
// Provides a lot of power, but tends to explode when mistreated.

// tg: code/modules/power/rtg.dm:79
public partial class obj_machinery_power_rtg_abductor : obj_machinery_power_rtg
{
    // tg: code/modules/power/rtg.dm:91
    public virtual async DmTask overload()
    {
        if (Dm.truthy(going_kaboom))
            return;
        going_kaboom = TRUE;
        visible_message(
            message: span_danger(I18n.T("[] lets out a shower of sparks as it starts to lose stability!", this)),
            blind_message: span_hear(I18n.T("You hear a loud electrical crack!")));
        playsound(this, Dm.resource("sound/effects/magic/lightningshock.ogg"), 100, TRUE, extrarange: 5);
        await tesla_zap(source: this, zap_range: 5, power: power_gen * 20);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.explosion), this, 2, 3, 4, null, 8), 10 * SECONDS, file: "code/modules/power/rtg.dm", line: 101); // Not a normal explosion.
    }

    // tg: code/modules/power/rtg.dm:103
    public override async DmTask<object> bullet_act(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        obj_projectile proj = hitting_projectile;
        object dot = await base.bullet_act(proj, def_zone, piercing_hit, blocked);
        if (!Dm.truthy(going_kaboom) && proj != null && Dm.num(proj.damage) > 0 && ((proj.damage_type == BURN) || (proj.damage_type == BRUTE)))
        {
            log_bomber(proj.firer, "triggered a", this, "explosion via projectile");
            await overload();
        }
        return dot;
    }

    // tg: code/modules/power/rtg.dm:109
    public override async DmTask<object> blob_act(obj_structure_blob attacking_blob = null) // the root slot (no live ancestor declares it yet)
    {
        await overload();
        return null;
    }

    // tg: code/modules/power/rtg.dm:112
    [DmParams()]
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        if (Dm.truthy(going_kaboom))
            qdel(this);
        else
            Waiting.NoSleep(overload());

        return TRUE;
    }

    // tg: code/modules/power/rtg.dm:120
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        await overload();
        return null;
    }

    // tg: code/modules/power/rtg.dm:123
    public override async DmTask<object> zap_act(object power = null, object zap_flags = null)
    {
        object dot = await base.zap_act(power, zap_flags); //extend the zap
        if (Dm.truthy(Dm.bitand(zap_flags, ZAP_MACHINE_EXPLOSIVE)))
            await overload();
        return dot;
    }
}

// tg: code/modules/power/rtg.dm:136
public partial class obj_machinery_power_rtg_lavaland : obj_machinery_power_rtg
{
    // tg: code/modules/power/rtg.dm:143
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        await RefreshParts();
        return dot;
    }

    // tg: code/modules/power/rtg.dm:147
    public override object get_base_power_gen()
    {
        turf our_turf = get_turf(this);
        if (islava(our_turf) && Dm.truthy(is_mining_level(our_turf.z)))
            return base_power_gen;
        return 0;
    }
}

// tg: code/modules/power/rtg.dm:154
public partial class obj_machinery_power_rtg_old_station : obj_machinery_power_rtg
{
    // tg: code/modules/power/rtg.dm:160
    public override object default_deconstruction_screwdriver(mob user = null, obj_item screwdriver = null)
    {
        object dot = base.default_deconstruction_screwdriver(user, screwdriver);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_SUCCESS)))
            to_chat(user, span_warning(I18n.T("You feel [] crumbling under your hands!", this)));
        return dot;
    }

    // tg: code/modules/power/rtg.dm:165
    [DmParams("user", "crowbar", "ignore_panel", "custom_deconstruct")]
    public override async DmTask<object> default_deconstruction_crowbar(mob_living user = null, obj_item crowbar = null, params object[] __extra)
    {
        to_chat(user, span_warning(I18n.T("As you pry, [] starts to fall apart!", this)));
        if (!Dm.truthy(await crowbar.use_tool(this, user, 3 * SECONDS, volume: 50)))
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_warning(I18n.T("You feel like you made a mistake.")));
        Dm.@new<obj_effect_decal_cleanable_ash_large>(drop_location());
        await deconstruct(FALSE);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/power/rtg.dm:65  (interleaved, rules 1.9)
public partial class obj_machinery_power_rtg : obj_machinery_power
{
    // tg: code/modules/power/rtg.dm:65
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        object dot = await base.vv_edit_var(var_name, var_value);
        if (Dm.eq(var_name, nameof(power_gen)) || Dm.eq(var_name, nameof(base_power_gen)) || Dm.eq(var_name, nameof(affected_by_parts)))
            await RefreshParts();
        return dot;
    }
}
