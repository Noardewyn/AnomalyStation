// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/dirtystation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

//Janitors!  Janitors, janitors, janitors!  -Sayu


//Conspicuously not-recent versions of suspicious cleanables

//This file was made not awful by Xhuis on September 13, 2016

//Making the station dirty, one tile at a time. Called by master controller's setup_objects

// tg: code/game/turfs/open/dirtystation.dm:10
public partial class turf_open_floor : turf_open
{
    // The proc-level var/static typecaches of MakeDirty (rules 10.6: TgState fields). typecacheof creates no datum, so tg's
    // world-start value and a creation at the first use are the same list.
    private static ref DmList MakeDirty__high_dirt_areas => ref TgState.Current.turf_open_floor__MakeDirty__high_dirt_areas;
    private static ref DmList MakeDirty__engine_dirt_areas => ref TgState.Current.turf_open_floor__MakeDirty__engine_dirt_areas;
    private static ref DmList MakeDirty__bathroom_dirt_areas => ref TgState.Current.turf_open_floor__MakeDirty__bathroom_dirt_areas;
    private static ref DmList MakeDirty__oily_areas => ref TgState.Current.turf_open_floor__MakeDirty__oily_areas;
    private static ref DmList MakeDirty__gib_covered_areas => ref TgState.Current.turf_open_floor__MakeDirty__gib_covered_areas;
    private static ref DmList MakeDirty__kitchen_dirt_areas => ref TgState.Current.turf_open_floor__MakeDirty__kitchen_dirt_areas;
    private static ref DmList MakeDirty__medical_dirt_areas => ref TgState.Current.turf_open_floor__MakeDirty__medical_dirt_areas;
    private static ref DmList MakeDirty__science_dirt_areas => ref TgState.Current.turf_open_floor__MakeDirty__science_dirt_areas;

    // tg: code/game/turfs/open/dirtystation.dm:10
    public virtual object MakeDirty()
    {
        // We start with a 1/3 chance of having this proc called by Initialize()

        if (!Dm.truthy(Dm.bitand(flags_1, CAN_BE_DIRTY_1)))
            return null;

        if (Dm.truthy(Dm.locate<obj_structure_grille>(contents)))
            return null;

        area A = get_area(this) as area;

        if (Dm.truthy(A) && !Dm.truthy(Dm.bitand(A.flags_1, CAN_BE_DIRTY_1)))
            return null;

        //The code below here isn't exactly optimal, but because of the individual decals that each area uses it's still applicable.

        //high dirt - 1/3 chance.
        MakeDirty__high_dirt_areas ??= typecacheof(Dm.list(
            typeof(area_mine_production),
            typeof(area_mine_living_quarters),
            typeof(area_station_commons_vacant_room_office),
            typeof(area_ruin_space))) as DmList;
        if (Dm.truthy(is_type_in_typecache(A, MakeDirty__high_dirt_areas)))
        {
            Dm.@new<obj_effect_decal_cleanable_dirt>(this); //vanilla, but it works
            return null;
        }


        if (prob(80)) //mid dirt  - 1/15
            return null;

        //Construction zones. Blood, sweat, and oil.  Oh, and dirt. A small colony of space-ants or two will pop up
        MakeDirty__engine_dirt_areas ??= typecacheof(Dm.list(
            typeof(area_station_engineering),
            typeof(area_station_command_heads_quarters_ce),
            typeof(area_station_science_robotics),
            typeof(area_station_maintenance),
            typeof(area_station_construction),
            typeof(area_station_commons_vacant_room_commissary),
            typeof(area_misc_survivalpod))) as DmList;
        if (Dm.truthy(is_type_in_typecache(A, MakeDirty__engine_dirt_areas)))
        {
            if (prob(3))
                Dm.@new<obj_effect_decal_cleanable_blood_old>(this);
            else
            {
                float sw__1 = rand(1, 100);
                if (sw__1 >= 1 && sw__1 <= 35) //35%
                {
                    if (prob(4))
                        Dm.@new<obj_effect_decal_cleanable_blood_gibs_robot_debris_old>(this);
                    else
                        Dm.@new<obj_effect_decal_cleanable_blood_oil>(this);
                }
                else if (sw__1 >= 35 && sw__1 <= 95) //60%
                    Dm.@new<obj_effect_decal_cleanable_dirt>(this);
                else if (sw__1 >= 95 && sw__1 <= 100) //5%
                    Dm.@new<obj_effect_decal_cleanable_ants>(this);
            }
            return null;
        }

        //Bathrooms. Blood, vomit, and shavings in the sinks.
        MakeDirty__bathroom_dirt_areas ??= typecacheof(Dm.list(
            typeof(area_station_commons_toilet),
            typeof(area_awaymission_research_interior_bathroom))) as DmList;
        if (Dm.truthy(is_type_in_typecache(A, MakeDirty__bathroom_dirt_areas)))
        {
            if (prob(40))
            {
                if (prob(90))
                    Dm.@new<obj_effect_decal_cleanable_vomit_old>(this);
                else
                    Dm.@new<obj_effect_decal_cleanable_blood_old>(this);
            }
            else if (prob(40)) //16% effective chance
                Dm.@new<obj_effect_decal_cleanable_ants>(this);
            return null;
        }

        // Cargo bays covered in oil.
        MakeDirty__oily_areas ??= typecacheof(typeof(area_station_cargo)) as DmList;
        if (Dm.truthy(is_type_in_typecache(A, MakeDirty__oily_areas)))
        {
            if (prob(25))
                Dm.@new<obj_effect_decal_cleanable_blood_oil>(this);
            else if (prob(20))
                // or occasionally the signs of opened packages
                Dm.@new<obj_effect_decal_cleanable_wrapping>(this);
            return null;
        }


        if (prob(75)) //low dirt  - 1/60
            return null;

        //Areas where gibs will be present. Robusting probably happened some time ago.
        MakeDirty__gib_covered_areas ??= typecacheof(Dm.list(
            typeof(area_station_ai_satellite),
            typeof(area_station_security),
            typeof(area_station_command_heads_quarters_hos))) as DmList;
        if (Dm.truthy(is_type_in_typecache(A, MakeDirty__gib_covered_areas)))
        {
            if (prob(20))
            {
                if (prob(5))
                    Dm.@new<obj_effect_decal_cleanable_blood_gibs_old>(this);
                else
                    Dm.@new<obj_effect_decal_cleanable_blood_old>(this);
            }
            return null;
        }

        //Kitchen areas. Broken eggs, flour, spilled milk (no crying allowed.), ants.
        MakeDirty__kitchen_dirt_areas ??= typecacheof(Dm.list(
            typeof(area_station_service_kitchen),
            typeof(area_station_service_cafeteria))) as DmList;
        if (Dm.truthy(is_type_in_typecache(A, MakeDirty__kitchen_dirt_areas)))
        {
            if (prob(60))
            {
                if (prob(50))
                    Dm.@new<obj_effect_decal_cleanable_food_egg_smudge>(this);
                else
                    Dm.@new<obj_effect_decal_cleanable_food_flour>(this);
            }
            else if (prob(20)) //12% effective chance
                Dm.@new<obj_effect_decal_cleanable_ants>(this);
            return null;
        }

        //Medical areas. Mostly clean by space-OSHA standards, but has some blood and oil spread about.
        MakeDirty__medical_dirt_areas ??= typecacheof(Dm.list(
            typeof(area_station_medical),
            typeof(area_station_command_heads_quarters_cmo))) as DmList;
        if (Dm.truthy(is_type_in_typecache(A, MakeDirty__medical_dirt_areas)))
        {
            if (prob(66))
            {
                if (prob(5))
                    Dm.@new<obj_effect_decal_cleanable_blood_gibs_old>(this);
                else
                    Dm.@new<obj_effect_decal_cleanable_blood_old>(this);
            }
            else if (prob(30))
            {
                if (A is area_station_medical_morgue)
                    Dm.@new<obj_item_ectoplasm>(this);
                else
                    Dm.@new<obj_effect_decal_cleanable_vomit_old>(this);
            }
            return null;
        }

        //Science messes. Mostly green glowy stuff -WHICH YOU SHOULD NOT INJEST-.
        MakeDirty__science_dirt_areas ??= typecacheof(Dm.list(
            typeof(area_station_science),
            typeof(area_station_command_heads_quarters_rd))) as DmList;
        if (Dm.truthy(is_type_in_typecache(A, MakeDirty__science_dirt_areas)))
        {
            if (prob(20))
                Dm.@new<obj_effect_decal_cleanable_greenglow_filled>(this); //this cleans itself up but it might startle you when you see it.
            return null;
        }

        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/game/turfs/open/dirtystation.dm:26-125  the var/static typecaches of /turf/open/floor/proc/MakeDirty
    internal DmList turf_open_floor__MakeDirty__high_dirt_areas;
    internal DmList turf_open_floor__MakeDirty__engine_dirt_areas;
    internal DmList turf_open_floor__MakeDirty__bathroom_dirt_areas;
    internal DmList turf_open_floor__MakeDirty__oily_areas;
    internal DmList turf_open_floor__MakeDirty__gib_covered_areas;
    internal DmList turf_open_floor__MakeDirty__kitchen_dirt_areas;
    internal DmList turf_open_floor__MakeDirty__medical_dirt_areas;
    internal DmList turf_open_floor__MakeDirty__science_dirt_areas;
}
