// Translated from tgstation (snapshot 1d99bd7f): code/controllers/configuration/entries/game_options.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only damage_multiplier (:17, read by every damage proc of living mobs) and the move delay entries translated (:219-246: /datum/config_entry/number/movedelay, run_delay and
// walk_delay; M2 P1, the living core: walk and run take their speeds from game_options.txt's RUN_DELAY and WALK_DELAY);
// and grey_assistants (:334; M2 F1: the assistant outfit's jumpsuit colour, colorful_assistants.dm); bombcap (:369, M5x slice 1:
// the explosions' caps);
// the other entries of game_options.dm are translated with the systems that read them. Their vv_edit_var overrides
// (:227) are View Variables, not translated (rules 13).

// tg: code/controllers/configuration/entries/game_options.dm:17
[DmPath("/datum/config_entry/number/damage_multiplier")]
public partial class datum_config_entry_number_damage_multiplier : datum_config_entry_number
{
    // tg: code/controllers/configuration/entries/game_options.dm:18-19
    public datum_config_entry_number_damage_multiplier()
    {
        @default = 1;
        integer = FALSE;
    }
}

//Used for modifying movement speed for mobs.
// tg: code/controllers/configuration/entries/game_options.dm:219
[DmPath("/datum/config_entry/number/movedelay")]
public partial class datum_config_entry_number_movedelay : datum_config_entry_number
{
    // tg: code/controllers/configuration/entries/game_options.dm:220
    public datum_config_entry_number_movedelay()
    {
        abstract_type = typeof(datum_config_entry_number_movedelay);
    }

    // tg: code/controllers/configuration/entries/game_options.dm:222
    public override async DmTask<object> ValidateAndSet(object str_val = null)
    {
        object dot = null;
        dot = await base.ValidateAndSet(str_val);
        if (Dm.truthy(dot))
            await update_mob_config_movespeeds();
        return dot;
    }
}

// tg: code/controllers/configuration/entries/game_options.dm:232
[DmPath("/datum/config_entry/number/movedelay/run_delay")]
public partial class datum_config_entry_number_movedelay_run_delay : datum_config_entry_number_movedelay
{
    // tg: code/controllers/configuration/entries/game_options.dm:233
    public datum_config_entry_number_movedelay_run_delay()
    {
        integer = FALSE;
    }

    // tg: code/controllers/configuration/entries/game_options.dm:235
    public override async DmTask<object> ValidateAndSet(object str_val = null)
    {
        object dot = null;
        dot = await base.ValidateAndSet(str_val);
        datum_movespeed_modifier_config_walk_run M = await get_cached_movespeed_modifier(typeof(datum_movespeed_modifier_config_walk_run_run)) as datum_movespeed_modifier_config_walk_run;
        M.sync();
        return dot;
    }
}

// tg: code/controllers/configuration/entries/game_options.dm:240
[DmPath("/datum/config_entry/number/movedelay/walk_delay")]
public partial class datum_config_entry_number_movedelay_walk_delay : datum_config_entry_number_movedelay
{
    // tg: code/controllers/configuration/entries/game_options.dm:241
    public datum_config_entry_number_movedelay_walk_delay()
    {
        integer = FALSE;
    }

    // tg: code/controllers/configuration/entries/game_options.dm:243
    public override async DmTask<object> ValidateAndSet(object str_val = null)
    {
        object dot = null;
        dot = await base.ValidateAndSet(str_val);
        datum_movespeed_modifier_config_walk_run M = await get_cached_movespeed_modifier(typeof(datum_movespeed_modifier_config_walk_run_walk)) as datum_movespeed_modifier_config_walk_run;
        M.sync();
        return dot;
    }
}

// tg: code/controllers/configuration/entries/game_options.dm:334
[DmPath("/datum/config_entry/flag/grey_assistants")]
public partial class datum_config_entry_flag_grey_assistants : datum_config_entry_flag
{
}

// M5x slice 1 (explosions, D149): the bomb cap. /datum/config_entry/flag/reactionary_explosions (:86) has no procs: its
// generated class is the whole entry (the host's config.txt sets it, as tg's game_options.txt ships it).
// tg: code/controllers/configuration/entries/game_options.dm:369  (the var block is generated)
public partial class datum_config_entry_number_bombcap : datum_config_entry_number
{
    // tg: code/controllers/configuration/entries/game_options.dm:373
    public override async DmTask<object> ValidateAndSet(object str_val = null)
    {
        object dot = null;
        dot = await base.ValidateAndSet(str_val);
        if (Dm.truthy(dot))
        {
            GLOB.MAX_EX_DEVESTATION_RANGE = round(Dm.num(config_entry_value) / 4);
            GLOB.MAX_EX_HEAVY_RANGE = round(Dm.num(config_entry_value) / 2);
            GLOB.MAX_EX_LIGHT_RANGE = Dm.num(config_entry_value);
            GLOB.MAX_EX_FLASH_RANGE = Dm.num(config_entry_value);
            GLOB.MAX_EX_FLAME_RANGE = Dm.num(config_entry_value);
        }
        return dot;
    }
}
