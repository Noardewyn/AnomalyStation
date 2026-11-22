// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/configuration.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// See initialization order in /code/game/world.dm
// tg: code/_globalvars/configuration.dm:2  GLOBAL_REAL(config, /datum/controller/configuration)
public sealed partial class TgState
{
    internal datum_controller_configuration config;
}

public static partial class GlobalProcs
{
    /// <summary>tg <c>config</c> (rules 10.6): the configuration of this world.</summary>
    public static ref datum_controller_configuration config => ref TgState.Current.config;
}

public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/configuration.dm:4  GLOBAL_DATUM_INIT(revdata, /datum/getrev, new): not translated yet. /datum/getrev
    // reads the server's git revision and test merges (helper_datums/getrev.dm, M10); without its class there is no var and no
    // InitGlobalrevdata, which typesof(/datum/controller/global_vars/proc) skips (DV9).

    // tg: code/_globalvars/configuration.dm:6  GLOBAL_VAR(host)
    public object host;

    // tg: code/_globalvars/configuration.dm:6  (GLOBAL_UNMANAGED(host), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalhost()
    {
        return;
    }

    // tg: code/_globalvars/configuration.dm:7  GLOBAL_VAR(station_name)
    public object station_name;

    // tg: code/_globalvars/configuration.dm:7  (GLOBAL_UNMANAGED(station_name), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalstation_name()
    {
        return;
    }

    // tg: code/_globalvars/configuration.dm:8  GLOBAL_VAR_INIT(game_version, "/tg/Station 13")
    public string game_version;

    // tg: code/_globalvars/configuration.dm:8  (GLOBAL_MANAGED(game_version, "/tg/Station 13"), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalgame_version()
    {
        game_version = "/tg/Station 13";
        Dm.append(ref gvars_datum_init_order, "game_version");
    }

    // tg: code/_globalvars/configuration.dm:9  GLOBAL_VAR_INIT(changelog_hash, "") // object: world/SetupLogs stores md5(...) or 0 in it (game/world.dm:255; rules 3.5)
    public object changelog_hash;

    // tg: code/_globalvars/configuration.dm:9  (GLOBAL_MANAGED(changelog_hash, ""), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalchangelog_hash()
    {
        changelog_hash = "";
        Dm.append(ref gvars_datum_init_order, "changelog_hash");
    }

    // tg: code/_globalvars/configuration.dm:10  GLOBAL_VAR_INIT(hub_visibility, FALSE)
    public float hub_visibility;

    // tg: code/_globalvars/configuration.dm:10  (GLOBAL_MANAGED(hub_visibility, FALSE), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalhub_visibility()
    {
        hub_visibility = FALSE;
        Dm.append(ref gvars_datum_init_order, "hub_visibility");
    }

    // tg: code/_globalvars/configuration.dm:12  GLOBAL_VAR_INIT(ooc_allowed, TRUE) // used with admin verbs to disable ooc - not a config option apparently
    public float ooc_allowed;

    // tg: code/_globalvars/configuration.dm:12  (GLOBAL_MANAGED(ooc_allowed, TRUE), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalooc_allowed()
    {
        ooc_allowed = TRUE;
        Dm.append(ref gvars_datum_init_order, "ooc_allowed");
    }

    // tg: code/_globalvars/configuration.dm:13  GLOBAL_VAR_INIT(dooc_allowed, TRUE)
    public float dooc_allowed;

    // tg: code/_globalvars/configuration.dm:13  (GLOBAL_MANAGED(dooc_allowed, TRUE), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldooc_allowed()
    {
        dooc_allowed = TRUE;
        Dm.append(ref gvars_datum_init_order, "dooc_allowed");
    }

    // debugging_enabled is used in conjunction with a lot of admin verbs and therefore is actually legit.
    // im not sure why you would ever add behavoir, likely use a define like TESTING or even checking `Debugger?.enabled`
    /// global debug switch
    // tg: code/_globalvars/configuration.dm:19  GLOBAL_VAR_INIT(debugging_enabled, FALSE)
    public float debugging_enabled;

    // tg: code/_globalvars/configuration.dm:19  (GLOBAL_MANAGED(debugging_enabled, FALSE), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldebugging_enabled()
    {
        debugging_enabled = FALSE;
        Dm.append(ref gvars_datum_init_order, "debugging_enabled");
    }

    //This was a define, but I changed it to a variable so it can be changed in-game.(kept the all-caps definition because... code...) -Errorage
    //Protecting these because the proper way to edit them is with the config/secrets
    // tg: code/_globalvars/configuration.dm:23  GLOBAL_VAR_INIT(MAX_EX_DEVESTATION_RANGE, 3)
    public float MAX_EX_DEVESTATION_RANGE;

    // tg: code/_globalvars/configuration.dm:23  (GLOBAL_MANAGED(MAX_EX_DEVESTATION_RANGE, 3); the earlier of two definitions on this type, rules 7.5)
    public void InitGlobalMAX_EX_DEVESTATION_RANGE__d0()
    {
        MAX_EX_DEVESTATION_RANGE = 3;
        Dm.append(ref gvars_datum_init_order, "MAX_EX_DEVESTATION_RANGE");
    }

    // tg: code/_globalvars/configuration.dm:24  GLOBAL_PROTECT(MAX_EX_DEVESTATION_RANGE) (__DEFINES/__globals.dm:15)
    public virtual void InitGlobalMAX_EX_DEVESTATION_RANGE()
    {
        InitGlobalMAX_EX_DEVESTATION_RANGE__d0();
        gvars_datum_protected_varlist["MAX_EX_DEVESTATION_RANGE"] = TRUE;
    }

    // tg: code/_globalvars/configuration.dm:25  GLOBAL_VAR_INIT(MAX_EX_HEAVY_RANGE, 7)
    public float MAX_EX_HEAVY_RANGE;

    // tg: code/_globalvars/configuration.dm:25  (GLOBAL_MANAGED(MAX_EX_HEAVY_RANGE, 7); the earlier of two definitions on this type, rules 7.5)
    public void InitGlobalMAX_EX_HEAVY_RANGE__d0()
    {
        MAX_EX_HEAVY_RANGE = 7;
        Dm.append(ref gvars_datum_init_order, "MAX_EX_HEAVY_RANGE");
    }

    // tg: code/_globalvars/configuration.dm:26  GLOBAL_PROTECT(MAX_EX_HEAVY_RANGE) (__DEFINES/__globals.dm:15)
    public virtual void InitGlobalMAX_EX_HEAVY_RANGE()
    {
        InitGlobalMAX_EX_HEAVY_RANGE__d0();
        gvars_datum_protected_varlist["MAX_EX_HEAVY_RANGE"] = TRUE;
    }

    // tg: code/_globalvars/configuration.dm:27  GLOBAL_VAR_INIT(MAX_EX_LIGHT_RANGE, 14)
    public float MAX_EX_LIGHT_RANGE;

    // tg: code/_globalvars/configuration.dm:27  (GLOBAL_MANAGED(MAX_EX_LIGHT_RANGE, 14); the earlier of two definitions on this type, rules 7.5)
    public void InitGlobalMAX_EX_LIGHT_RANGE__d0()
    {
        MAX_EX_LIGHT_RANGE = 14;
        Dm.append(ref gvars_datum_init_order, "MAX_EX_LIGHT_RANGE");
    }

    // tg: code/_globalvars/configuration.dm:28  GLOBAL_PROTECT(MAX_EX_LIGHT_RANGE) (__DEFINES/__globals.dm:15)
    public virtual void InitGlobalMAX_EX_LIGHT_RANGE()
    {
        InitGlobalMAX_EX_LIGHT_RANGE__d0();
        gvars_datum_protected_varlist["MAX_EX_LIGHT_RANGE"] = TRUE;
    }

    // tg: code/_globalvars/configuration.dm:29  GLOBAL_VAR_INIT(MAX_EX_FLASH_RANGE, 14)
    public float MAX_EX_FLASH_RANGE;

    // tg: code/_globalvars/configuration.dm:29  (GLOBAL_MANAGED(MAX_EX_FLASH_RANGE, 14); the earlier of two definitions on this type, rules 7.5)
    public void InitGlobalMAX_EX_FLASH_RANGE__d0()
    {
        MAX_EX_FLASH_RANGE = 14;
        Dm.append(ref gvars_datum_init_order, "MAX_EX_FLASH_RANGE");
    }

    // tg: code/_globalvars/configuration.dm:30  GLOBAL_PROTECT(MAX_EX_FLASH_RANGE) (__DEFINES/__globals.dm:15)
    public virtual void InitGlobalMAX_EX_FLASH_RANGE()
    {
        InitGlobalMAX_EX_FLASH_RANGE__d0();
        gvars_datum_protected_varlist["MAX_EX_FLASH_RANGE"] = TRUE;
    }

    // tg: code/_globalvars/configuration.dm:31  GLOBAL_VAR_INIT(MAX_EX_FLAME_RANGE, 14)
    public float MAX_EX_FLAME_RANGE;

    // tg: code/_globalvars/configuration.dm:31  (GLOBAL_MANAGED(MAX_EX_FLAME_RANGE, 14); the earlier of two definitions on this type, rules 7.5)
    public void InitGlobalMAX_EX_FLAME_RANGE__d0()
    {
        MAX_EX_FLAME_RANGE = 14;
        Dm.append(ref gvars_datum_init_order, "MAX_EX_FLAME_RANGE");
    }

    // tg: code/_globalvars/configuration.dm:32  GLOBAL_PROTECT(MAX_EX_FLAME_RANGE) (__DEFINES/__globals.dm:15)
    public virtual void InitGlobalMAX_EX_FLAME_RANGE()
    {
        InitGlobalMAX_EX_FLAME_RANGE__d0();
        gvars_datum_protected_varlist["MAX_EX_FLAME_RANGE"] = TRUE;
    }

    // tg: code/_globalvars/configuration.dm:33  GLOBAL_VAR_INIT(DYN_EX_SCALE, 0.5)
    public float DYN_EX_SCALE;

    // tg: code/_globalvars/configuration.dm:33  (GLOBAL_MANAGED(DYN_EX_SCALE, 0.5), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalDYN_EX_SCALE()
    {
        DYN_EX_SCALE = 0.5f;
        Dm.append(ref gvars_datum_init_order, "DYN_EX_SCALE");
    }
}
