// Translated from tgstation (snapshot 1d99bd7f): code/modules/movespeed/modifiers/mobs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The type blocks (the var overrides of /datum/movespeed_modifier, rules 10.2: all 50 types of the file) were translated by
// the M2 contracts step; M2 P1 (the living core) translated the procs: the root config_walk_run/proc/sync (:56) and the
// sync() overrides of config_walk_run/walk and config_walk_run/run (:58-64), which also declare those two types.

// tg: code/modules/movespeed/modifiers/mobs.dm:1
[DmPath("/datum/movespeed_modifier/obesity")]
public partial class datum_movespeed_modifier_obesity : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:3
    public datum_movespeed_modifier_obesity()
    {
        // large weight slows even if flying and floating
        multiplicative_slowdown = 1.5f;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:5
[DmPath("/datum/movespeed_modifier/monkey_reagent_speedmod")]
public partial class datum_movespeed_modifier_monkey_reagent_speedmod : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:6
    public datum_movespeed_modifier_monkey_reagent_speedmod()
    {
        variable = TRUE;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:8
[DmPath("/datum/movespeed_modifier/monkey_health_speedmod")]
public partial class datum_movespeed_modifier_monkey_health_speedmod : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:9
    public datum_movespeed_modifier_monkey_health_speedmod()
    {
        variable = TRUE;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:11
[DmPath("/datum/movespeed_modifier/monkey_temperature_speedmod")]
public partial class datum_movespeed_modifier_monkey_temperature_speedmod : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:12
    public datum_movespeed_modifier_monkey_temperature_speedmod()
    {
        variable = TRUE;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:14
[DmPath("/datum/movespeed_modifier/golem_hunger")]
public partial class datum_movespeed_modifier_golem_hunger : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:15
    public datum_movespeed_modifier_golem_hunger()
    {
        variable = TRUE;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:17
[DmPath("/datum/movespeed_modifier/slaughter")]
public partial class datum_movespeed_modifier_slaughter : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:18
    public datum_movespeed_modifier_slaughter()
    {
        multiplicative_slowdown = -1;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:20
[DmPath("/datum/movespeed_modifier/resonance")]
public partial class datum_movespeed_modifier_resonance : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:21
    public datum_movespeed_modifier_resonance()
    {
        multiplicative_slowdown = 0.75f;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:23
[DmPath("/datum/movespeed_modifier/basic_stamina_slowdown")]
public partial class datum_movespeed_modifier_basic_stamina_slowdown : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:24
    public datum_movespeed_modifier_basic_stamina_slowdown()
    {
        variable = TRUE;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:26
[DmPath("/datum/movespeed_modifier/damage_slowdown")]
public partial class datum_movespeed_modifier_damage_slowdown : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:27-28
    public datum_movespeed_modifier_damage_slowdown()
    {
        blacklisted_movetypes = FLOATING;
        variable = TRUE;
    }
}

/// Movespeed modifier applied by worn equipment.
// tg: code/modules/movespeed/modifiers/mobs.dm:31
[DmPath("/datum/movespeed_modifier/equipment_speedmod")]
public partial class datum_movespeed_modifier_equipment_speedmod : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:32-33
    public datum_movespeed_modifier_equipment_speedmod()
    {
        variable = TRUE;
        blacklisted_movetypes = FLOATING;
    }
}

/// Movespeed modifier applied by immutably slow worn equipment. Should never be ignored, because that's the point.
// tg: code/modules/movespeed/modifiers/mobs.dm:36
[DmPath("/datum/movespeed_modifier/equipment_speedmod/immutable")]
public partial class datum_movespeed_modifier_equipment_speedmod_immutable : datum_movespeed_modifier_equipment_speedmod
{
}

// tg: code/modules/movespeed/modifiers/mobs.dm:38
[DmPath("/datum/movespeed_modifier/grab_slowdown")]
public partial class datum_movespeed_modifier_grab_slowdown : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:39-40
    public datum_movespeed_modifier_grab_slowdown()
    {
        id = MOVESPEED_ID_MOB_GRAB_STATE;
        blacklisted_movetypes = FLOATING;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:42
[DmPath("/datum/movespeed_modifier/grab_slowdown/aggressive")]
public partial class datum_movespeed_modifier_grab_slowdown_aggressive : datum_movespeed_modifier_grab_slowdown
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:43
    public datum_movespeed_modifier_grab_slowdown_aggressive()
    {
        multiplicative_slowdown = 3;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:45
[DmPath("/datum/movespeed_modifier/grab_slowdown/neck")]
public partial class datum_movespeed_modifier_grab_slowdown_neck : datum_movespeed_modifier_grab_slowdown
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:46
    public datum_movespeed_modifier_grab_slowdown_neck()
    {
        multiplicative_slowdown = 6;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:48
[DmPath("/datum/movespeed_modifier/grab_slowdown/kill")]
public partial class datum_movespeed_modifier_grab_slowdown_kill : datum_movespeed_modifier_grab_slowdown
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:49
    public datum_movespeed_modifier_grab_slowdown_kill()
    {
        multiplicative_slowdown = 9;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:51
[DmPath("/datum/movespeed_modifier/config_walk_run")]
public partial class datum_movespeed_modifier_config_walk_run : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:52-54
    public datum_movespeed_modifier_config_walk_run()
    {
        multiplicative_slowdown = 1;
        id = MOVESPEED_ID_MOB_WALK_RUN;
        flags = IGNORE_NOSLOW;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:56
public partial class datum_movespeed_modifier_config_walk_run : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:56
    public virtual void sync()
    {
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:58
[DmPath("/datum/movespeed_modifier/config_walk_run/walk")]
public partial class datum_movespeed_modifier_config_walk_run_walk : datum_movespeed_modifier_config_walk_run
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:58
    public override void sync()
    {
        object mod = config.Get(typeof(datum_config_entry_number_movedelay_walk_delay));
        multiplicative_slowdown = Dm.num_or_null(isnum(mod) ? mod : Dm.initial(this, (datum_movespeed_modifier_config_walk_run_walk p) => p.multiplicative_slowdown));
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:62
[DmPath("/datum/movespeed_modifier/config_walk_run/run")]
public partial class datum_movespeed_modifier_config_walk_run_run : datum_movespeed_modifier_config_walk_run
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:62
    public override void sync()
    {
        object mod = config.Get(typeof(datum_config_entry_number_movedelay_run_delay));
        multiplicative_slowdown = Dm.num_or_null(isnum(mod) ? mod : Dm.initial(this, (datum_movespeed_modifier_config_walk_run_run p) => p.multiplicative_slowdown));
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:66
[DmPath("/datum/movespeed_modifier/turf_slowdown")]
public partial class datum_movespeed_modifier_turf_slowdown : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:67-69
    public datum_movespeed_modifier_turf_slowdown()
    {
        movetypes = GROUND;
        blacklisted_movetypes = Dm.bitor(FLYING, FLOATING);
        variable = TRUE;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:71
[DmPath("/datum/movespeed_modifier/bulky_drag")]
public partial class datum_movespeed_modifier_bulky_drag : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:72-73
    public datum_movespeed_modifier_bulky_drag()
    {
        variable = TRUE;
        blacklisted_movetypes = FLOATING;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:75
[DmPath("/datum/movespeed_modifier/cold")]
public partial class datum_movespeed_modifier_cold : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:76-77
    public datum_movespeed_modifier_cold()
    {
        blacklisted_movetypes = FLOATING;
        variable = TRUE;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:79
[DmPath("/datum/movespeed_modifier/staggered")]
public partial class datum_movespeed_modifier_staggered : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:80
    public datum_movespeed_modifier_staggered()
    {
        multiplicative_slowdown = STAGGERED_SLOWDOWN_STRENGTH;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:82
[DmPath("/datum/movespeed_modifier/human_carry")]
public partial class datum_movespeed_modifier_human_carry : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:83-84
    public datum_movespeed_modifier_human_carry()
    {
        multiplicative_slowdown = HUMAN_CARRY_SLOWDOWN;
        blacklisted_movetypes = FLOATING;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:86
[DmPath("/datum/movespeed_modifier/limbless")]
public partial class datum_movespeed_modifier_limbless : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:87-90
    public datum_movespeed_modifier_limbless()
    {
        variable = TRUE;
        movetypes = GROUND;
        blacklisted_movetypes = Dm.bitor(FLOATING, FLYING);
        flags = IGNORE_NOSLOW;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:92
[DmPath("/datum/movespeed_modifier/simplemob_varspeed")]
public partial class datum_movespeed_modifier_simplemob_varspeed : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:93-94
    public datum_movespeed_modifier_simplemob_varspeed()
    {
        variable = TRUE;
        flags = IGNORE_NOSLOW;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:96
[DmPath("/datum/movespeed_modifier/fast_web")]
public partial class datum_movespeed_modifier_fast_web : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:97
    public datum_movespeed_modifier_fast_web()
    {
        multiplicative_slowdown = 0.2f;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:99
[DmPath("/datum/movespeed_modifier/young_web")]
public partial class datum_movespeed_modifier_young_web : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:100
    public datum_movespeed_modifier_young_web()
    {
        multiplicative_slowdown = 0.5f;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:102
[DmPath("/datum/movespeed_modifier/spiderling_web")]
public partial class datum_movespeed_modifier_spiderling_web : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:103
    public datum_movespeed_modifier_spiderling_web()
    {
        multiplicative_slowdown = 0.7f;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:105
[DmPath("/datum/movespeed_modifier/average_web")]
public partial class datum_movespeed_modifier_average_web : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:106
    public datum_movespeed_modifier_average_web()
    {
        multiplicative_slowdown = 1.2f;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:108
[DmPath("/datum/movespeed_modifier/below_average_web")]
public partial class datum_movespeed_modifier_below_average_web : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:109
    public datum_movespeed_modifier_below_average_web()
    {
        multiplicative_slowdown = 2.5f;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:111
[DmPath("/datum/movespeed_modifier/slow_web")]
public partial class datum_movespeed_modifier_slow_web : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:112
    public datum_movespeed_modifier_slow_web()
    {
        multiplicative_slowdown = 5;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:114
[DmPath("/datum/movespeed_modifier/viper_defensive")]
public partial class datum_movespeed_modifier_viper_defensive : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:115
    public datum_movespeed_modifier_viper_defensive()
    {
        multiplicative_slowdown = 1.5f;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:117
[DmPath("/datum/movespeed_modifier/gravity")]
public partial class datum_movespeed_modifier_gravity : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:118-120
    public datum_movespeed_modifier_gravity()
    {
        blacklisted_movetypes = FLOATING;
        variable = TRUE;
        flags = IGNORE_NOSLOW;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:122
[DmPath("/datum/movespeed_modifier/carbon_softcrit")]
public partial class datum_movespeed_modifier_carbon_softcrit : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:123-124
    public datum_movespeed_modifier_carbon_softcrit()
    {
        multiplicative_slowdown = SOFTCRIT_ADD_SLOWDOWN;
        flags = IGNORE_NOSLOW;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:126
[DmPath("/datum/movespeed_modifier/carbon_crawling")]
public partial class datum_movespeed_modifier_carbon_crawling : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:127-128
    public datum_movespeed_modifier_carbon_crawling()
    {
        multiplicative_slowdown = CRAWLING_ADD_SLOWDOWN;
        flags = IGNORE_NOSLOW;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:130
[DmPath("/datum/movespeed_modifier/mob_config_speedmod")]
public partial class datum_movespeed_modifier_mob_config_speedmod : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:131-132
    public datum_movespeed_modifier_mob_config_speedmod()
    {
        variable = TRUE;
        flags = IGNORE_NOSLOW;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:134
[DmPath("/datum/movespeed_modifier/metabolicboost")]
public partial class datum_movespeed_modifier_metabolicboost : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:135
    public datum_movespeed_modifier_metabolicboost()
    {
        multiplicative_slowdown = -1.5f;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:137
[DmPath("/datum/movespeed_modifier/dragon_rage")]
public partial class datum_movespeed_modifier_dragon_rage : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:138
    public datum_movespeed_modifier_dragon_rage()
    {
        multiplicative_slowdown = -0.5f;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:140
[DmPath("/datum/movespeed_modifier/dragon_depression")]
public partial class datum_movespeed_modifier_dragon_depression : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:141
    public datum_movespeed_modifier_dragon_depression()
    {
        multiplicative_slowdown = 5;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:143
[DmPath("/datum/movespeed_modifier/morph_disguised")]
public partial class datum_movespeed_modifier_morph_disguised : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:144
    public datum_movespeed_modifier_morph_disguised()
    {
        multiplicative_slowdown = -1;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:146
[DmPath("/datum/movespeed_modifier/auto_wash")]
public partial class datum_movespeed_modifier_auto_wash : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:147
    public datum_movespeed_modifier_auto_wash()
    {
        multiplicative_slowdown = 3;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:149
[DmPath("/datum/movespeed_modifier/player_spider_modifier")]
public partial class datum_movespeed_modifier_player_spider_modifier : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:150
    public datum_movespeed_modifier_player_spider_modifier()
    {
        variable = TRUE;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:152
[DmPath("/datum/movespeed_modifier/health_scaling_speed_buff")]
public partial class datum_movespeed_modifier_health_scaling_speed_buff : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:153
    public datum_movespeed_modifier_health_scaling_speed_buff()
    {
        variable = TRUE;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:155
[DmPath("/datum/movespeed_modifier/alien_speed")]
public partial class datum_movespeed_modifier_alien_speed : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:156
    public datum_movespeed_modifier_alien_speed()
    {
        variable = TRUE;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:158
[DmPath("/datum/movespeed_modifier/grown_killer_tomato")]
public partial class datum_movespeed_modifier_grown_killer_tomato : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:159
    public datum_movespeed_modifier_grown_killer_tomato()
    {
        variable = TRUE;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:161
[DmPath("/datum/movespeed_modifier/goldgrub_mount")]
public partial class datum_movespeed_modifier_goldgrub_mount : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:162
    public datum_movespeed_modifier_goldgrub_mount()
    {
        multiplicative_slowdown = -4.5f;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:164
[DmPath("/datum/movespeed_modifier/settler")]
public partial class datum_movespeed_modifier_settler : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:165-166
    public datum_movespeed_modifier_settler()
    {
        multiplicative_slowdown = 0.2f;
        blacklisted_movetypes = Dm.bitor(FLOATING, FLYING);
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:168
[DmPath("/datum/movespeed_modifier/basilisk_overheat")]
public partial class datum_movespeed_modifier_basilisk_overheat : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:169
    public datum_movespeed_modifier_basilisk_overheat()
    {
        multiplicative_slowdown = -18;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:171
[DmPath("/datum/movespeed_modifier/magic_ties")]
public partial class datum_movespeed_modifier_magic_ties : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:172
    public datum_movespeed_modifier_magic_ties()
    {
        multiplicative_slowdown = 0.5f;
    }
}

///Speed bonus given by the fish tail organ when inside water.
// tg: code/modules/movespeed/modifiers/mobs.dm:175
[DmPath("/datum/movespeed_modifier/fish_on_water")]
public partial class datum_movespeed_modifier_fish_on_water : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:176-177
    public datum_movespeed_modifier_fish_on_water()
    {
        blacklisted_movetypes = MOVETYPES_NOT_TOUCHING_GROUND;
        // (/turf/open/water::slowdown: the compile-time value of the type, rules 10.3)
        multiplicative_slowdown = -Dm.initial(typeof(turf_open_water), (turf_open_water p) => p.slowdown);
    }
}

///Slowdown for swimming on deep water tiles
// tg: code/modules/movespeed/modifiers/mobs.dm:180
[DmPath("/datum/movespeed_modifier/swimming_deep")]
public partial class datum_movespeed_modifier_swimming_deep : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:181-182
    public datum_movespeed_modifier_swimming_deep()
    {
        blacklisted_movetypes = MOVETYPES_NOT_TOUCHING_GROUND;
        multiplicative_slowdown = 7;
    }
}

// tg: code/modules/movespeed/modifiers/mobs.dm:184
[DmPath("/datum/movespeed_modifier/tail_dragger")]
public partial class datum_movespeed_modifier_tail_dragger : datum_movespeed_modifier
{
    // tg: code/modules/movespeed/modifiers/mobs.dm:185
    public datum_movespeed_modifier_tail_dragger()
    {
        multiplicative_slowdown = 4;
    }
}
