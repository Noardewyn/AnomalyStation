// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapping/preloader.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// global datum that will preload variables on atoms instanciation
// Each GLOBAL_VAR_INIT / GLOBAL_LIST_INIT (__DEFINES/__globals.dm:36, :39) is expanded inline (rules 10.6, 11.1 (b)): the
// GLOB var and its InitGlobalX proc (GLOBAL_MANAGED, __DEFINES/__globals.dm:5).
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/mapping/preloader.dm:2  GLOBAL_VAR_INIT(use_preloader, FALSE)
    // RULE-GAP: GLOBAL_VAR_INIT declares an untyped var with no default (GLOBAL_RAW) and sets it in InitGlobalX; 3.2 V7
    // makes that object (as GLOBAL_VAR in _globalvars/logging.cs), although the init value is a number.
    public object use_preloader;

    // tg: code/modules/mapping/preloader.dm:2
    public virtual void InitGlobaluse_preloader()
    {
        use_preloader = FALSE;
        Dm.append(ref gvars_datum_init_order, "use_preloader");
    }

    // tg: code/modules/mapping/preloader.dm:3  GLOBAL_LIST_INIT(_preloader_attributes, null)
    public DmList _preloader_attributes;

    // tg: code/modules/mapping/preloader.dm:3
    public virtual void InitGlobal_preloader_attributes()
    {
        _preloader_attributes = null;
        Dm.append(ref gvars_datum_init_order, "_preloader_attributes");
    }

    // tg: code/modules/mapping/preloader.dm:4  GLOBAL_LIST_INIT(_preloader_path, null)
    // (object, rules 3.5: declared a list, but world.preloader_setup stores a typepath in it, :15)
    public object _preloader_path;

    // tg: code/modules/mapping/preloader.dm:4
    public virtual void InitGlobal_preloader_path()
    {
        _preloader_path = null;
        Dm.append(ref gvars_datum_init_order, "_preloader_path");
    }
}

/// Preloader datum
// tg: code/modules/mapping/preloader.dm:7
[DmPath("/datum/map_preloader")]
public partial class datum_map_preloader : datum
{
    // tg: code/modules/mapping/preloader.dm:8
    public DmList attributes;
    // tg: code/modules/mapping/preloader.dm:9
    public object target_path;
}

public static partial class GlobalProcs
{
    // tg: code/modules/mapping/preloader.dm:11
    public static void preloader_setup(this DmWorld world, DmList the_attributes = null, object path = null)
    {
        if (Dm.truthy(the_attributes.len))
        {
            GLOB.use_preloader = TRUE;
            GLOB._preloader_attributes = the_attributes;
            GLOB._preloader_path = path;
        }
    }

    // tg: code/modules/mapping/preloader.dm:17
    public static void preloader_load(this DmWorld world, atom what = null)
    {
        GLOB.use_preloader = FALSE;
        DmList attributes = GLOB._preloader_attributes;
        foreach (object attribute in Dm.each(attributes))
        {
            object value = attributes[attribute];
            if (islist(value))
                value = deep_copy_list(value);
            // tg: #ifdef TESTING ... #endif not translated (inactive in production)
            // what.vars[attribute] = value: the engine's by-name var set (MapVars, rules 10.7: vars is not translated). The
            // list was deep-copied just above, as tg does; MapVars copies a list it is given again, which changes nothing.
            // PORT: BYOND's undefined-var runtime ends preloader_load (and the New that called it); here it is reported and
            // the other attributes are still set, so a bad edit does not leave the atom half-made. The converter refuses
            // unknown vars (docs/ai/specs/map-format.md 6), so only a value of a C# type the var cannot hold (rules 3.5)
            // reaches it.
            if (!MapVars.TrySet(what, (string)attribute, value, out string error))
                World.Current.ReportRuntime(new DmRuntimeException(Dm.text("preloader: [] of [] at []: []", attribute, what.type, what.loc, error)));
            // DEVIATION(DV508): a thing whose map says the way it faces keeps it against the 3D models' own facing rule
            else if ((string)attribute == "dir")
                MapSeam.NoteMapDir(what);
        }
    }
}

/// Template noop (no operation) is used to skip a turf or area when the template is loaded this allows for template transparency
/// ex. if a ship has gaps in its design, you would use template_noop to fill these in so that when the ship moves z-level, any
/// tiles these gaps land on will not be deleted and replaced with the ships (empty) tiles
// tg: code/modules/mapping/preloader.dm:35
[DmPath("/area/template_noop")]
public partial class area_template_noop : area
{
    // tg: code/modules/mapping/preloader.dm:36
    public area_template_noop()
    {
        name = "Area Passthrough";
    }
}

/// See above explanation
// tg: code/modules/mapping/preloader.dm:39
[DmPath("/turf/template_noop")]
public partial class turf_template_noop : turf
{
    // tg: code/modules/mapping/preloader.dm:40
    public turf_template_noop()
    {
        name = "Turf Passthrough";
        icon_state = "noop";
        bullet_bounce_sound = null;
    }
}
