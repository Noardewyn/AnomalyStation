// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/mineral_scanner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J6 (D298): the file whole, the manual and automatic mining scanners and their ping. The ore vents stay inert (Lavaland's, not in
// the plan): none exists, so the pulse's vent loop finds nothing.

/**********************Mining Scanners**********************/
// tg: code/modules/mining/equipment/mineral_scanner.dm:3
public partial class obj_item_mining_scanner : obj_item
{
    // tg: code/modules/mining/equipment/mineral_scanner.dm:19
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (user.client == null)
            return null;
        if (current_cooldown <= world.time)
        {
            current_cooldown = world.time + cooldown;
            mineral_scan_pulse(get_turf(user), scanner: this);
        }
        return null;
    }
}

//Debug item to identify all ore spread quickly
// tg: code/modules/mining/equipment/mineral_scanner.dm:29
public partial class obj_item_mining_scanner_admin : obj_item_mining_scanner
{
    // tg: code/modules/mining/equipment/mineral_scanner.dm:29
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        foreach (turf_closed_mineral rock in Dm.each<turf_closed_mineral>(world))
        {
            if (Dm.truthy(rock.scan_state))
            {
                mutable_appearance rock_overlay = mutable_appearance(rock.scan_icon, rock.scan_state, FLASH_LAYER, rock, ABOVE_LIGHTING_PLANE, appearance_flags: RESET_TRANSFORM) as mutable_appearance;
                rock.add_overlay(rock_overlay);
            }
        }
        qdel(this);
        return null;
    }
}

// tg: code/modules/mining/equipment/mineral_scanner.dm:37
public partial class obj_item_t_scanner_adv_mining_scanner : obj_item_t_scanner
{
    //get no effects from the t-ray scanner, which auto-shuts off.
    // tg: code/modules/mining/equipment/mineral_scanner.dm:54
    public override async DmTask cyborg_unequip(mob user = null) { }

    // tg: code/modules/mining/equipment/mineral_scanner.dm:69
    public override void scan()
    {
        if (current_cooldown <= world.time)
        {
            current_cooldown = world.time + cooldown;
            turf t = get_turf(this);
            mineral_scan_pulse(t, range, this);
        }
    }
}

// tg: code/modules/mining/equipment/mineral_scanner.dm:57
public partial class obj_item_t_scanner_adv_mining_scanner_cyborg : obj_item_t_scanner_adv_mining_scanner
{
    // tg: code/modules/mining/equipment/mineral_scanner.dm:57
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        toggle_on();
        return dot;
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/mining/equipment/mineral_scanner.dm:75
    public static void mineral_scan_pulse(turf start_turf = null, object range = null, obj_item scanner = null)
    {
        range ??= world.view;
        // A runtime in tg: world.view is the text "15x15" and RANGE_TURFS does arithmetic on it; its half width is what the pulse means.
        if (range is string view_text)
            range = (Dm.num((getviewsize(view_text))[1]) - 1) / 2;
        // for(var/obj/structure/ore_vent/vent in range(range, start_turf)): the ore vents are Lavaland's (inert, none exists), so nothing
        // near sets vents_nearby and the pulse plays no sound of its own; add_mineral_overlays and the radar sound wait with them.
        Dm.Deferred("code/modules/mining/equipment/mineral_scanner.dm:80", "mineral_scan_pulse: the ore vents' overlays and the radar ping (Lavaland's ore vents, not in the plan)");

        foreach (turf_closed_mineral mineral in Dm.each<turf_closed_mineral>(RANGE_TURFS(range, start_turf)))
        {
            if (Dm.truthy(mineral.scan_state))
                mineral.flash_scan();
        }
    }
}

// tg: code/modules/mining/equipment/mineral_scanner.dm:102
public partial class obj_effect_temp_visual_mining_overlay : obj_effect_temp_visual
{
    // tg: code/modules/mining/equipment/mineral_scanner.dm:113
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        animate(this, Dm.named("alpha", 0), Dm.named("time", duration), Dm.named("easing", easing_style));
        return dot;
    }
}
