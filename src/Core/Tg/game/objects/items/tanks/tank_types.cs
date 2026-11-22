// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tanks/tank_types.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M4c slice 1): the internals tanks, alt-click to toggle the valve, and each type's gas. The var blocks
// are generated (D111).

/* Types of tanks!
 * Contains:
 * Oxygen
 * Anesthetic
 * Air
 * Plasma
 * Emergency Oxygen
 * Generic
 */

// tg: code/game/objects/items/tanks/tank_types.dm:10
public partial class obj_item_tank_internals : obj_item_tank
{
    /// Allows carbon to toggle internals via AltClick of the equipped tank.
    // tg: code/game/objects/items/tanks/tank_types.dm:15
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await toggle_internals(user as mob_living_carbon);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/tanks/tank_types.dm:19
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Alt-click the tank to toggle the valve.")));
        return dot;
    }
}

/*
 * Oxygen
 */
// tg: code/game/objects/items/tanks/tank_types.dm:26
public partial class obj_item_tank_internals_oxygen : obj_item_tank_internals
{
    // tg: code/game/objects/items/tanks/tank_types.dm:37
    public override void populate_gas()
    {
        air_contents.set_gas(typeof(datum_gas_oxygen), (6 * ONE_ATMOSPHERE) * Dm.num(volume) / (R_IDEAL_GAS_EQUATION * T20C));
    }
}

// tg: code/game/objects/items/tanks/tank_types.dm:55
public partial class obj_item_tank_internals_oxygen_empty : obj_item_tank_internals_oxygen
{
    // tg: code/game/objects/items/tanks/tank_types.dm:55
    public override void populate_gas()
    {
        return;
    }
}

/*
 * Anesthetic
 */
// tg: code/game/objects/items/tanks/tank_types.dm:61
public partial class obj_item_tank_internals_anesthetic : obj_item_tank_internals
{
    // tg: code/game/objects/items/tanks/tank_types.dm:69
    public override void populate_gas()
    {
        air_contents.set_gas(typeof(datum_gas_oxygen), (3 * ONE_ATMOSPHERE) * Dm.num(volume) / (R_IDEAL_GAS_EQUATION * T20C) * O2STANDARD);
        air_contents.set_gas(typeof(datum_gas_nitrous_oxide), (3 * ONE_ATMOSPHERE) * Dm.num(volume) / (R_IDEAL_GAS_EQUATION * T20C) * N2STANDARD);
    }

    // tg: code/game/objects/items/tanks/tank_types.dm:74
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("A warning is etched into []...", this)));
        Dm.append(ref dot, span_warning(I18n.T("There is no process in the body that uses N2O, so patients will exhale the N2O... exposing you to it. Make sure to work in a well-ventilated space to avoid sleepy mishaps.")));
        return dot;
    }
}

// tg: code/game/objects/items/tanks/tank_types.dm:79
public partial class obj_item_tank_internals_anesthetic_pure : obj_item_tank_internals_anesthetic
{
    // tg: code/game/objects/items/tanks/tank_types.dm:83
    public override void populate_gas()
    {
        air_contents.adjust_gas(typeof(datum_gas_nitrous_oxide), (10 * ONE_ATMOSPHERE) * Dm.num(volume) / (R_IDEAL_GAS_EQUATION * T20C));
    }
}

/*
 * Plasma
 */
// tg: code/game/objects/items/tanks/tank_types.dm:89
public partial class obj_item_tank_internals_plasma : obj_item_tank_internals
{
    // tg: code/game/objects/items/tanks/tank_types.dm:101
    public override void populate_gas()
    {
        air_contents.set_gas(typeof(datum_gas_plasma), (3 * ONE_ATMOSPHERE) * Dm.num(volume) / (R_IDEAL_GAS_EQUATION * T20C));
    }

    // tg: code/game/objects/items/tanks/tank_types.dm:104
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!(tool is obj_item_flamethrower))
            return await base.item_interaction(user, tool, modifiers, __extra);
        obj_item_flamethrower spewer = tool as obj_item_flamethrower;
        if (Dm.truthy(spewer.status) || spewer.ptank != null)
            return ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(await user.transferItemToLoc(this, spewer)))
            return ITEM_INTERACT_BLOCKING;
        master = spewer; // does master even do anything anymore?
        spewer.ptank = this;
        spewer.update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/objects/items/tanks/tank_types.dm:117
public partial class obj_item_tank_internals_plasma_full : obj_item_tank_internals_plasma
{
    // tg: code/game/objects/items/tanks/tank_types.dm:117
    public override void populate_gas()
    {
        air_contents.set_gas(typeof(datum_gas_plasma), (10 * ONE_ATMOSPHERE) * Dm.num(volume) / (R_IDEAL_GAS_EQUATION * T20C));
    }
}

// tg: code/game/objects/items/tanks/tank_types.dm:120
public partial class obj_item_tank_internals_plasma_empty : obj_item_tank_internals_plasma
{
    // tg: code/game/objects/items/tanks/tank_types.dm:120
    public override void populate_gas()
    {
        return;
    }
}

/*
 * Plasmaman Plasma Tank
 */

// tg: code/game/objects/items/tanks/tank_types.dm:127
public partial class obj_item_tank_internals_plasmaman : obj_item_tank_internals
{
    // tg: code/game/objects/items/tanks/tank_types.dm:136
    public override void populate_gas()
    {
        air_contents.set_gas(typeof(datum_gas_plasma), (3 * ONE_ATMOSPHERE) * Dm.num(volume) / (R_IDEAL_GAS_EQUATION * T20C));
    }
}

// tg: code/game/objects/items/tanks/tank_types.dm:139
public partial class obj_item_tank_internals_plasmaman_full : obj_item_tank_internals_plasmaman
{
    // tg: code/game/objects/items/tanks/tank_types.dm:139
    public override void populate_gas()
    {
        air_contents.set_gas(typeof(datum_gas_plasma), (10 * ONE_ATMOSPHERE) * Dm.num(volume) / (R_IDEAL_GAS_EQUATION * T20C));
    }
}

// tg: code/game/objects/items/tanks/tank_types.dm:155
public partial class obj_item_tank_internals_plasmaman_belt_full : obj_item_tank_internals_plasmaman_belt
{
    // tg: code/game/objects/items/tanks/tank_types.dm:155
    public override void populate_gas()
    {
        air_contents.set_gas(typeof(datum_gas_plasma), (10 * ONE_ATMOSPHERE) * Dm.num(volume) / (R_IDEAL_GAS_EQUATION * T20C));
    }
}

// tg: code/game/objects/items/tanks/tank_types.dm:158
public partial class obj_item_tank_internals_plasmaman_belt_empty : obj_item_tank_internals_plasmaman_belt
{
    // tg: code/game/objects/items/tanks/tank_types.dm:158
    public override void populate_gas()
    {
        return;
    }
}

/*
 * Emergency Oxygen
 */
// tg: code/game/objects/items/tanks/tank_types.dm:166
public partial class obj_item_tank_internals_emergency_oxygen : obj_item_tank_internals
{
    // tg: code/game/objects/items/tanks/tank_types.dm:182
    public override void populate_gas()
    {
        air_contents.set_gas(typeof(datum_gas_oxygen), (10 * ONE_ATMOSPHERE) * Dm.num(volume) / (R_IDEAL_GAS_EQUATION * T20C));
    }
}

// tg: code/game/objects/items/tanks/tank_types.dm:186
public partial class obj_item_tank_internals_emergency_oxygen_empty : obj_item_tank_internals_emergency_oxygen
{
    // tg: code/game/objects/items/tanks/tank_types.dm:186
    public override void populate_gas()
    {
        return;
    }
}

// tg: code/game/objects/items/tanks/tank_types.dm:199
public partial class obj_item_tank_internals_emergency_oxygen_engi_empty : obj_item_tank_internals_emergency_oxygen_engi
{
    // tg: code/game/objects/items/tanks/tank_types.dm:199
    public override void populate_gas()
    {
        return;
    }
}

// tg: code/game/objects/items/tanks/tank_types.dm:210
public partial class obj_item_tank_internals_emergency_oxygen_double_empty : obj_item_tank_internals_emergency_oxygen_double
{
    // tg: code/game/objects/items/tanks/tank_types.dm:210
    public override void populate_gas()
    {
        return;
    }
}

// *
// * GENERIC
// *

// tg: code/game/objects/items/tanks/tank_types.dm:217
public partial class obj_item_tank_internals_generic : obj_item_tank_internals
{
    // tg: code/game/objects/items/tanks/tank_types.dm:227
    public override void populate_gas()
    {
        return;
    }
}

/*
 * Funny internals
 */
// tg: code/game/objects/items/tanks/tank_types.dm:242
public partial class obj_item_tank_internals_emergency_oxygen_engi_clown_n2o : obj_item_tank_internals_emergency_oxygen_engi_clown
{
    // tg: code/game/objects/items/tanks/tank_types.dm:244
    public override void populate_gas()
    {
        air_contents.set_gas(typeof(datum_gas_oxygen), (10 * ONE_ATMOSPHERE) * Dm.num(volume) / (R_IDEAL_GAS_EQUATION * T20C) * 0.95f);
        air_contents.set_gas(typeof(datum_gas_nitrous_oxide), (10 * ONE_ATMOSPHERE) * Dm.num(volume) / (R_IDEAL_GAS_EQUATION * T20C) * 0.05f);
    }
}

// tg: code/game/objects/items/tanks/tank_types.dm:248
public partial class obj_item_tank_internals_emergency_oxygen_engi_clown_bz : obj_item_tank_internals_emergency_oxygen_engi_clown
{
    // tg: code/game/objects/items/tanks/tank_types.dm:250
    public override void populate_gas()
    {
        air_contents.set_gas(typeof(datum_gas_oxygen), (10 * ONE_ATMOSPHERE) * Dm.num(volume) / (R_IDEAL_GAS_EQUATION * T20C) * 0.9f);
        air_contents.set_gas(typeof(datum_gas_bz), (10 * ONE_ATMOSPHERE) * Dm.num(volume) / (R_IDEAL_GAS_EQUATION * T20C) * 0.1f);
    }
}

// tg: code/game/objects/items/tanks/tank_types.dm:254
public partial class obj_item_tank_internals_emergency_oxygen_engi_clown_helium : obj_item_tank_internals_emergency_oxygen_engi_clown
{
    // tg: code/game/objects/items/tanks/tank_types.dm:257
    public override void populate_gas()
    {
        air_contents.set_gas(typeof(datum_gas_oxygen), (10 * ONE_ATMOSPHERE) * Dm.num(volume) / (R_IDEAL_GAS_EQUATION * T20C) * 0.75f);
        air_contents.set_gas(typeof(datum_gas_helium), (10 * ONE_ATMOSPHERE) * Dm.num(volume) / (R_IDEAL_GAS_EQUATION * T20C) * 0.25f);
    }
}
