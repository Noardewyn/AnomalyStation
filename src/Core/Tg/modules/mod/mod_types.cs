// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/mod_types.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO1 (D333): the file whole. The pre-equipped MODsuits: a control unit made with its theme's skin, a core (a standard core
// gets a charged cell) and its list of modules installed, some of which it pins to its first wearer's action bar; the no-jetpack
// and plasmaman nuclear suits drop or add modules, the infiltrator suit hides its contraband, the responsory (ERT) suits add their
// insignia and extra modules; the empty suits (for the preferences menu) initialize at once. The var blocks are generated (D111).
//
// D132 seam (gaps MO1, narrowed by gaps MO2): the MOD modules are translated (gaps MO2) but the space ninja's (modules_ninja.dm, M10+:
// the ninja's cloak, hacker, weapon recall, energy net, adrenaline boost, EMP pulse, reinforced DNA lock and camera vision, which
// the ninja's and the administrative suits carry) and the circuit module (wiremod/shell/module.dm, wiremod is not ported), so those
// module types are inert. Initialize below skips an inert applied module (InertSeam.IsInertType) rather than make it and install
// it as a base module, and an inert applied core (the soul shard cores, the antagonists', D333) likewise; not-translated.md 219 and
// 222 have the rows, ModsuitsMO1Tests and ModsuitsMO2Tests pin them.

// tg: code/modules/mod/mod_types.dm:1
public partial class obj_item_mod_control_pre_equipped : obj_item_mod_control
{
    // tg: code/modules/mod/mod_types.dm:14
    [DmParams("mapload", "new_theme", "new_skin", "new_core")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_theme = Dm.arg(__extra, 0);
        object new_skin = Dm.arg(__extra, 1);
        object new_core = Dm.arg(__extra, 2);
        foreach (object module_to_pin in Dm.each(default_pins))
            default_pins[module_to_pin] = Dm.list();
        new_skin = applied_skin;
        // TEMPORARY SEAM (gaps MO1, D132; removed with the soul shard cores, the antagonists' (D333)): an inert core (the wizard's
        // enchanted suit's soul shard core) is not made and installed as if it were the base core: the suit is made coreless.
        new_core = InertSeam.IsInertType(applied_core as Type) ? null : Dm.@new(applied_core, this);
        if (istype(new_core, typeof(obj_item_mod_core_standard)))
        {
            obj_item_mod_core_standard cell_core = new_core as obj_item_mod_core_standard;
            cell_core.cell = Dm.@new(applied_cell) as obj_item_stock_parts_power_store;
        }
        object dot = base.Initialize(mapload, Dm.forward(__extra, new_theme, new_skin, new_core));
        foreach (Type module_type in Dm.each_cast<Type>(applied_modules))
        {
            // D132 seam (gaps MO1, narrowed by gaps MO2; removed with the ninja's modules, M10+, and wiremod): an inert module type
            // (a space ninja's module, the circuit module) is not made and installed as if it were the base module.
            if (InertSeam.IsInertType(module_type))
                continue;
            obj_item_mod_module module = Dm.@new(module_type, this) as obj_item_mod_module;
            Waiting.NoSleep(install(module));
        }
        return dot;
    }

    // tg: code/modules/mod/mod_types.dm:27
    public override async DmTask set_wearer(mob_living_carbon_human user = null)
    {
        await base.set_wearer(user);
        foreach (obj_item_mod_module module in Dm.each_cast<obj_item_mod_module>(modules))
        {
            if (!Dm.truthy(default_pins[module.type])) //this module isnt meant to be pinned by default
                continue;
            if (Dm.@in(REF(wearer), default_pins[module.type])) //if we already had pinned once to this user, don care anymore
                continue;
            DmList pinned = default_pins[module.type] as DmList;
            Dm.append(ref pinned, REF(wearer));
            default_pins[module.type] = pinned;
            await module.pin(wearer);
        }
    }

    // tg: code/modules/mod/mod_types.dm:37
    public override async DmTask uninstall(obj_item_mod_module old_module = null, float? deleting = null)
    {
        await base.uninstall(old_module, deleting);
        Dm.remove(ref default_pins, old_module.type);
    }
}

// tg: code/modules/mod/mod_types.dm:308
public partial class obj_item_mod_control_pre_equipped_nuclear_no_jetpack : obj_item_mod_control_pre_equipped_nuclear
{
    // tg: code/modules/mod/mod_types.dm:310
    [DmParams("mapload", "new_theme", "new_skin", "new_core")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Dm.remove(ref applied_modules, Dm.list(typeof(obj_item_mod_module_jetpack_advanced), typeof(obj_item_mod_module_jump_jet)));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/mod/mod_types.dm:314
public partial class obj_item_mod_control_pre_equipped_nuclear_plasmaman : obj_item_mod_control_pre_equipped_nuclear
{
    // tg: code/modules/mod/mod_types.dm:316
    [DmParams("mapload", "new_theme", "new_skin", "new_core")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Dm.append(ref applied_modules, typeof(obj_item_mod_module_plasma_stabilizer));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/mod/mod_types.dm:355
public partial class obj_item_mod_control_pre_equipped_infiltrator : obj_item_mod_control_pre_equipped
{
    // tg: code/modules/mod/mod_types.dm:379
    [DmParams("mapload", "new_theme", "new_skin", "new_core")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_CONTRABAND_BLOCKER, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/modules/mod/mod_types.dm:470
public partial class obj_item_mod_control_pre_equipped_responsory : obj_item_mod_control_pre_equipped
{
    // tg: code/modules/mod/mod_types.dm:487
    [DmParams("mapload", "new_theme", "new_skin", "new_core")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        applied_modules.Insert(1, insignia_type);
        if (Dm.truthy(additional_modules))
        {
            Dm.append(ref applied_modules, additional_modules);
            Dm.append(ref default_pins, additional_modules);
        }
        return base.Initialize(mapload, __extra);
    }
}

//these exist for the prefs menu
// tg: code/modules/mod/mod_types.dm:667
public partial class obj_item_mod_control_pre_equipped_empty : obj_item_mod_control_pre_equipped
{
    // The New() that INITIALIZE_IMMEDIATE(/obj/item/mod/control/pre_equipped/empty) defines (__DEFINES/subsystems.dm:96; inline,
    // rules 11.1 (c), (d))
    // tg: code/modules/mod/mod_types.dm:684
    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        object loc = Dm.arg(__extra, 0);
        await base.New(Dm.forward(__extra, loc));
        if (!Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
        {
            float previous_initialized_value = SSatoms.initialized;
            SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD;
            DmList args = Dm.args(Dm.forward(__extra, loc));
            args[1] = TRUE;
            SSatoms.InitAtom(this, FALSE, args);
            SSatoms.initialized = previous_initialized_value;
        }
    }
}
