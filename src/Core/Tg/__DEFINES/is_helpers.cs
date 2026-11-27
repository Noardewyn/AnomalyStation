// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/is_helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only isdatum and isweakref (the list, sort and comparator helpers, code/__HELPERS/_lists.dm and cmp.dm, use
// them), isspaceturf (the atoms package, wave 3: /atom/proc/isinspace) and the type tests the wave-4 packages use
// (W4.0 contracts: isatom, isliving, ishuman, isobserver, isitem, ismachinery, is_multi_tile_object) and client/Move uses
// (M1: isAI), M3 P4's isimage (remove_filter), isfloorturf (the floor's ChangeTurf) and isprojectile (the grille's and table's passage), and M3 P3's isclosedturf,
// isindestructiblewall, isplatingturf, isdrone, ismecha and is_area_shuttle, and M4b slice 2's islava (the heat-exchanging
// pipes), M4a P4b's in_range (the reinforced and operating tables) and M4b audio slice 1's istransparentturf and isnewplayer, M4b's isalien and isorgan (the extinguisher cabinet, reagent transfers)
// (playsound), and M4c slice 5's isopenspaceturf, is_space_or_openspace and isshuttleturf (the airlock pump) and the ghosts' is_job, the M7 projectiles slice's isvehicle and the M7 supermatter slice's isplasmaman, islarva, iscameramob, iseffect and is_clown_job, and the M7 delamination slice's isindestructiblefloor, ischasm and isdead, and M7x slice B's IN_GIVEN_RANGE, isgun and isammobox and M8z slice B's isalienadult and isbot, and gaps ST2's isbook with GLOB.book_types and isProbablyWallMounted, and gaps BR1's is_area_virtual translated; the rest of is_helpers.dm is
// translated with the waves that use it (rules 10.4, 11.1).
public static partial class GlobalProcs
{
    // tg: code/__DEFINES/is_helpers.dm:289  (silicons SI6) Checks if clothing item is actually a held mob
    public static bool ispickedupmob(object A) => A is obj_item_mob_holder;

    // tg: code/__DEFINES/is_helpers.dm:10
    public static bool isdatum(object thing)
    {
        return thing is datum;
    }
// M2 P1 (the living core) added the members of its paths, marked "M2 P1" below.

    // tg: code/__DEFINES/is_helpers.dm:12
    public static bool isweakref(object D)
    {
        return D is datum_weakref;
    }

    // tg: code/__DEFINES/is_helpers.dm:76
    public static bool isspaceturf(object A)
    {
        return A is turf_open_space;
    }

    // tg: code/__DEFINES/is_helpers.dm:3  (M4a P4b: the reinforced table's attempt_electrocution, the operating table's
    // check_mask_range). DM's `?:` is the null-safe runtime member access: the two turfs' z.
    public static bool in_range(object source, object user)
    {
        return get_dist(source, user) <= 1 && Dm.eq(get_step(source, 0)?.z, get_step(user, 0)?.z);
    }

    // tg: code/__DEFINES/is_helpers.dm:6  #define IN_GIVEN_RANGE(source, other, given_range) (M7x slice B: the gun's gunpoint range check)
    public static bool IN_GIVEN_RANGE(object source, object other, float given_range)
    {
        return get_dist(source, other) <= given_range && Dm.eq(get_step(source, 0)?.z, get_step(other, 0)?.z);
    }

    // tg: code/__DEFINES/is_helpers.dm:261  #define isgrenade(A) (istype(A, /obj/item/grenade))  (syndicate N2a: the pneumatic grenade launcher)
    public static bool isgrenade(object A) => istype(A, typeof(obj_item_grenade));

    // tg: code/__DEFINES/is_helpers.dm:297  #define isgun(A) (istype(A, /obj/item/gun))  (M7x slice B)
    public static bool isgun(object A) => istype(A, typeof(obj_item_gun));

    // tg: code/__DEFINES/is_helpers.dm:299  #define isammobox(A) (istype(A, /obj/item/ammo_box))  (M7x slice B)
    public static bool isammobox(object A) => istype(A, typeof(obj_item_ammo_box));

    // tg: code/__DEFINES/is_helpers.dm:8
    public static bool isatom(object A)
    {
        return isloc(A);
    }

    // tg: code/__DEFINES/is_helpers.dm:80  (M3 P4: /turf/open/floor/ChangeTurf, floor.dm:118)
    public static bool isfloorturf(object A)
    {
        return A is turf_open_floor;
    }

    // tg: code/__DEFINES/is_helpers.dm:291  (M4a P5: the ID card's context tips and item_interaction)
    public static bool iscash(object A)
    {
        return A is obj_item_coin || A is obj_item_stack_spacecash || A is obj_item_holochip || A is obj_item_poker_chip;
    }

    // tg: code/__DEFINES/is_helpers.dm:295  (M3 P4: the grille's and the table's CanAllowThrough)
    public static bool isprojectile(object A)
    {
        return A is obj_projectile;
    }

    // tg: code/__DEFINES/is_helpers.dm:14  (M3 P4: remove_filter's ASSERT, datums/datum.dm:504)
    public static bool isimage(object thing)
    {
        return thing is image;
    }

    // tg: code/__DEFINES/is_helpers.dm:118
    public static bool isliving(object A)
    {
        return A is mob_living;
    }

    // tg: code/__DEFINES/is_helpers.dm:125
    public static bool ishuman(object A)
    {
        return A is mob_living_carbon_human;
    }

    // tg: code/__DEFINES/is_helpers.dm:127
    public static bool isdummy(object A)
    {
        return A is mob_living_carbon_human_dummy;
    }

    // tg: code/__DEFINES/is_helpers.dm:173
    public static bool isAI(object A)
    {
        return A is mob_living_silicon_ai;
    }

    // tg: code/__DEFINES/is_helpers.dm:235
    public static bool iseyemob(object A)
    {
        return A is mob_eye;
    }

    // tg: code/__DEFINES/is_helpers.dm:241
    public static bool isaicamera(object A)
    {
        return A is mob_eye_camera_ai;
    }

    // tg: code/__DEFINES/is_helpers.dm:248
    public static bool isobserver(object A)
    {
        return A is mob_dead_observer;
    }

    // tg: code/__DEFINES/is_helpers.dm:255
    public static bool isitem(object A)
    {
        return A is obj_item;
    }

    // tg: code/__DEFINES/is_helpers.dm:259  #define isstack(A) (istype(A, /obj/item/stack)) (M5 slice 2: stacks)
    public static bool isstack(object A) => istype(A, typeof(obj_item_stack));

    // tg: code/__DEFINES/is_helpers.dm:308  #define isassembly(O) (istype(O, /obj/item/assembly)) (gaps F1: the chem grenade's trigger)
    public static bool isassembly(object O) => istype(O, typeof(obj_item_assembly));

    // tg: code/__DEFINES/is_helpers.dm:352  #define is_assistant_job(job_type) (istype(job_type, /datum/job/assistant)) (M10 slice F: the late join)
    public static bool is_assistant_job(object job_type) => istype(job_type, typeof(datum_job_assistant));

    // tg: code/__DEFINES/is_helpers.dm:354  #define is_captain_job(job_type) (istype(job_type, /datum/job/captain)) (M10 slice F: the late join)
    public static bool is_captain_job(object job_type) => istype(job_type, typeof(datum_job_captain));

    // tg: code/__DEFINES/is_helpers.dm:357  #define is_mime_job(job_type) (istype(job_type, /datum/job/mime)) (syndicate A2: give_uplink)
    public static bool is_mime_job(object job_type) => istype(job_type, typeof(datum_job_mime));

    // tg: code/__DEFINES/is_helpers.dm:362  #define is_unassigned_job(job_type) (istype(job_type, /datum/job/unassigned)) (M10 slice F: the round start)
    public static bool is_unassigned_job(object job_type) => istype(job_type, typeof(datum_job_unassigned));

    // tg: code/__DEFINES/is_helpers.dm:351  #define is_job(job_type) (istype(job_type, /datum/job)) (the ghosts: the mind's set_assigned_role)
    public static bool is_job(object job_type) => istype(job_type, typeof(datum_job));

    // tg: code/__DEFINES/is_helpers.dm:324  #define is_glass_sheet(O) (is_type_in_typecache(O, GLOB.glass_sheet_types)) (M5 slice 5: glass on a grille)
    public static bool is_glass_sheet(object O) => Dm.truthy(is_type_in_typecache(O, GLOB.glass_sheet_types));

    // tg: code/__DEFINES/is_helpers.dm:88  #define iswallturf(A) (istype(A, /turf/closed/wall)) (M5 slice 4: walls)
    public static bool iswallturf(object A) => istype(A, typeof(turf_closed_wall));

    // tg: code/__DEFINES/is_helpers.dm:228  #define ismegafauna(A) (istype(A, /mob/living/simple_animal/hostile/megafauna) || istype(A, /mob/living/basic/boss))
    // (the M7 zaps slice: a megafauna rips an emitter from the floor, emitter.dm:197)
    public static bool ismegafauna(object A) => (istype(A, typeof(mob_living_simple_animal_hostile_megafauna)) || istype(A, typeof(mob_living_basic_boss)));

    // tg: code/__DEFINES/is_helpers.dm:226  #define isguardian(A) (istype(A, /mob/living/basic/guardian)) (syndicate X2: the holoparasites)
    public static bool isguardian(object A) => istype(A, typeof(mob_living_basic_guardian));

    // tg: code/__DEFINES/is_helpers.dm:273  #define isstructure(A) (istype(A, /obj/structure)) (M5 slice 4: girder wall recipes)
    public static bool isstructure(object A) => istype(A, typeof(obj_structure));

    // tg: code/__DEFINES/is_helpers.dm:336  #define ismetaltile(tile_thing) (istype(tile_thing, /obj/item/stack/tile/iron) ||
    // istype(tile_thing, /obj/item/stack/tile/material) && tile_thing.has_material_type(/datum/material/iron)) (M5 slice 4:
    // tiles on space; has_material_type does not sleep)
    public static bool ismetaltile(object tile_thing)
        => istype(tile_thing, typeof(obj_item_stack_tile_iron))
            || istype(tile_thing, typeof(obj_item_stack_tile_material)) && Dm.truthy(Waiting.NoSleep(((atom)tile_thing).has_material_type(typeof(datum_material_iron))));

    // tg: code/__DEFINES/is_helpers.dm:275
    public static bool ismachinery(object A)
    {
        return A is obj_machinery;
    }

    // tg: code/__DEFINES/is_helpers.dm:365
    public static bool is_multi_tile_object(object atom)
    {
        return ((atom_movable)atom).bound_width > ICON_SIZE_X || ((atom_movable)atom).bound_height > ICON_SIZE_Y;
    }

    // M2 P1 (living core): the type tests of the living mob and status effect paths.

    // tg: code/__DEFINES/is_helpers.dm:39
    public static bool isgroundlessturf(object A)
    {
        return Dm.truthy(is_type_in_typecache(A, GLOB.turfs_without_ground));
    }

    // tg: code/__DEFINES/is_helpers.dm:52  (M4b slice 3: the water vapour condensation)
    public static bool isnoslipturf(object A)
    {
        return Dm.truthy(is_type_in_typecache(A, GLOB.turfs_no_slip_water));
    }

    // tg: code/__DEFINES/is_helpers.dm:59  #define isopenspaceturf(A) (is_type_in_typecache(A, GLOB.turfs_openspace)) (M4c slice 5: the airlock pump)
    // tg: code/__DEFINES/is_helpers.dm  #define ismineralturf(A) (istype(A, /turf/closed/mineral))
    public static bool ismineralturf(object A) => istype(A, typeof(turf_closed_mineral));

    // tg: code/__DEFINES/is_helpers.dm:100  #define isasteroidturf(A) (istype(A, /turf/open/misc/asteroid)) (gaps E2c: the earthquake)
    public static bool isasteroidturf(object A) => istype(A, typeof(turf_open_misc_asteroid));

    // tg: code/__DEFINES/is_helpers.dm  #define ismining(A) (A.mob_biotypes & MOB_MINING)
    public static bool ismining(mob_living A) => Dm.truthy(Dm.bitand(A.mob_biotypes, MOB_MINING));

    public static bool isopenspaceturf(object A)
    {
        return Dm.truthy(is_type_in_typecache(A, GLOB.turfs_openspace));
    }

    // tg: code/__DEFINES/is_helpers.dm:78  #define is_space_or_openspace(A) (isopenspaceturf(A) || isspaceturf(A)) (M4c slice 5: the airlock pump)
    public static bool is_space_or_openspace(object A)
    {
        return (isopenspaceturf(A) || isspaceturf(A));
    }

    // tg: code/__DEFINES/is_helpers.dm:330  #define isshuttleturf(T) (!isnull(T.depth_to_find_baseturf(/turf/baseturf_skipover/shuttle))) (M4c slice 5: the airlock pump)
    public static bool isshuttleturf(turf T)
    {
        return (T.depth_to_find_baseturf(typeof(turf_baseturf_skipover_shuttle)) != null);
    }

    // tg: code/__DEFINES/is_helpers.dm:69
    public static bool isopenturf(object A)
    {
        return A is turf_open;
    }

    // tg: code/__DEFINES/is_helpers.dm:120
    public static bool isbrain(object A)
    {
        return A is mob_living_brain;
    }

    // tg: code/__DEFINES/is_helpers.dm:123
    public static bool iscarbon(object A)
    {
        return A is mob_living_carbon;
    }

    // tg: code/__DEFINES/is_helpers.dm:172
    public static bool issilicon(object A)
    {
        return A is mob_living_silicon;
    }

    // tg: code/__DEFINES/is_helpers.dm:175  #define ispAI(A) (istype(A, /mob/living/silicon/pai)) (M9 slice D: the radio's virtual speaker)
    public static bool ispAI(object A) => A is mob_living_silicon_pai;

    // tg: code/__DEFINES/is_helpers.dm:174
    public static bool iscyborg(object A) => A is mob_living_silicon_robot;

    // tg: code/__DEFINES/is_helpers.dm:269  (M8 slice C: a syringe embedded with its dart's casing)
    public static bool isammocasing(object A) => A is obj_item_ammo_casing;

    // tg: code/__DEFINES/is_helpers.dm:161  (M8z slice B: the strip menu)
    public static bool isalienadult(object A)
    {
        return A is mob_living_carbon_alien_adult || A is mob_living_basic_alien;
    }

    // tg: code/__DEFINES/is_helpers.dm:208  (M8z slice B: the strip menu)
    public static bool isbot(object A)
    {
        return A is mob_living_basic_bot;
    }

    // tg: code/__DEFINES/is_helpers.dm:157  (M4b: the extinguisher cabinet refuses aliens)
    public static bool isalien(object A)
    {
        return A is mob_living_carbon_alien;
    }

    // tg: code/__DEFINES/is_helpers.dm:183
    public static bool isbasicmob(object A)
    {
        return A is mob_living_basic;
    }

    // tg: code/__DEFINES/is_helpers.dm:210  #define ismouse(A) (istype(A, /mob/living/basic/mouse)) (silicons SI2: the synthesizer)
    public static bool ismouse(object A) => A is mob_living_basic_mouse;

    // tg: code/__DEFINES/is_helpers.dm:224  #define isregalrat(A) (istype(A, /mob/living/basic/regal_rat)) (gaps AS1: the mousetrap)
    public static bool isregalrat(object A) => A is mob_living_basic_regal_rat;

    // tg: code/__DEFINES/is_helpers.dm:185  #define isconstruct(A) (istype(A, /mob/living/basic/construct)) (gaps K1: the changeling's Last Resort)
    public static bool isconstruct(object A) => A is mob_living_basic_construct;

    // tg: code/__DEFINES/is_helpers.dm:198
    public static bool isanimal_or_basicmob(object A)
    {
        return A is mob_living_simple_animal || A is mob_living_basic;
    }

    // tg: code/__DEFINES/is_helpers.dm:212
    public static bool isslime(object A)
    {
        return A is mob_living_basic_slime;
    }

    // tg: code/__DEFINES/is_helpers.dm:204
    public static bool isanimal(object A)
    {
        return A is mob_living_simple_animal;
    }

    // tg: code/__DEFINES/is_helpers.dm:222
    public static bool ishostile(object A)
    {
        return A is mob_living_simple_animal_hostile;
    }

    // tg: code/__DEFINES/is_helpers.dm:285  (M4b: a reagent transfer into an organ)
    public static bool isorgan(object A)
    {
        return A is obj_item_organ;
    }

    // tg: code/__DEFINES/is_helpers.dm:206
    public static bool isrevenant(object A)
    {
        // GAP(lead): /mob/living/basic/revenant (modules/mob/living/basic/space_fauna/revenant/_revenant.dm) is not
        // declared, so no value is one (revenants, a later milestone).
        return false;
    }

    // tg: code/__DEFINES/is_helpers.dm:253  (overrides BYOND's isobj, which is true for children of /atom/movable that aren't objs)
    public static bool isobj(object A)
    {
        return A is obj;
    }

    // tg: code/__DEFINES/is_helpers.dm:293
    public static bool isbodypart(object A)
    {
        return A is obj_item_bodypart;
    }

    // tg: code/__DEFINES/is_helpers.dm:287
    public static bool isclothing(object A)
    {
        return A is obj_item_clothing;
    }

    // M3 P3 (machinery, doors and lights): the turf, area and mob type tests their procs use.

    // tg: code/__DEFINES/is_helpers.dm:84
    public static bool isclosedturf(object A)
    {
        return A is turf_closed;
    }

    // tg: code/__DEFINES/is_helpers.dm:92  #define islava(A) (istype(A, /turf/open/lava)) (M4b slice 2: the heat-exchanging pipes)
    public static bool islava(object A)
    {
        return A is turf_open_lava;
    }

    // tg: code/__DEFINES/is_helpers.dm:86
    public static bool isindestructiblewall(object A)
    {
        return A is turf_closed_indestructible;
    }

    // tg: code/__DEFINES/is_helpers.dm:71  #define isindestructiblefloor(A) (istype(A, /turf/open/indestructible)) (the M7 delamination slice: the dimensional anomaly's themes)
    public static bool isindestructiblefloor(object A)
    {
        return A is turf_open_indestructible;
    }

    // tg: code/__DEFINES/is_helpers.dm:74  #define is_anchorable_floor(A) (isfloorturf(A) || isindestructiblefloor(A)) (silicons SI1: the law rack)
    public static bool is_anchorable_floor(object A) => (isfloorturf(A) || isindestructiblefloor(A));

    // tg: code/__DEFINES/is_helpers.dm:94  #define ischasm(A) (istype(A, /turf/open/chasm)) (the M7 delamination slice: the anomaly placer)
    public static bool ischasm(object A)
    {
        return A is turf_open_chasm;
    }

    // tg: code/__DEFINES/is_helpers.dm:246  #define isdead(A) (istype(A, /mob/dead)) (the M7 delamination slice: the cascade's demoralization)
    public static bool isdead(object A)
    {
        return A is mob_dead;
    }

    // tg: code/__DEFINES/is_helpers.dm:96
    public static bool isplatingturf(object A)
    {
        return A is turf_open_floor_plating;
    }

    // tg: code/__DEFINES/is_helpers.dm:214
    public static bool isdrone(object A)
    {
        return A is mob_living_basic_drone;
    }

    // tg: code/__DEFINES/is_helpers.dm:281  (the M7 projectiles slice: a vehicle's gunners in on_hit's log)
    public static bool isvehicle(object A)
    {
        return A is obj_vehicle;
    }

    // tg: code/__DEFINES/is_helpers.dm:283
    public static bool ismecha(object A)
    {
        return A is obj_vehicle_sealed_mecha;
    }

    // tg: code/__DEFINES/is_helpers.dm:368 (gaps BR1: the bitrunning domains' areas, bitrunning/areas.dm's GLOB.virtual_areas)
    public static bool is_area_virtual(object checked_area)
    {
        return GLOB.virtual_areas != null && Dm.truthy(GLOB.virtual_areas[((area)checked_area).type]);
    }

    // tg: code/__DEFINES/is_helpers.dm:369
    public static bool is_area_shuttle(object checked_area)
    {
        return checked_area is area_shuttle;
    }

    // tg: code/__DEFINES/is_helpers.dm:102  #define istransparentturf(A) (HAS_TRAIT(A, TURF_Z_TRANSPARENT_TRAIT)) (M4b audio slice 1: playsound)
    public static bool istransparentturf(object A)
    {
        return (HAS_TRAIT(A, TURF_Z_TRANSPARENT_TRAIT));
    }

    // tg: code/__DEFINES/is_helpers.dm:250  #define isnewplayer(A) (istype(A, /mob/dead/new_player)) (M4b audio slice 1: sound_to_playing_players)
    public static bool isnewplayer(object A)
    {
        return A is mob_dead_new_player;
    }

    // tg: code/__DEFINES/is_helpers.dm:140  (the M7 supermatter slice: a dusted plasmaman leaves plasma bones)
    public static bool isplasmaman(object A)
    {
        return Dm.truthy(is_species(A, typeof(datum_species_plasmaman)));
    }

    // tg: code/__DEFINES/is_helpers.dm:159  (the M7 supermatter slice: a larva's touch dusts it)
    public static bool islarva(object A)
    {
        return A is mob_living_carbon_alien_larva;
    }

    // tg: code/__DEFINES/is_helpers.dm:239  (the M7 supermatter slice: a camera eye's touch does nothing)
    public static bool iscameramob(object A)
    {
        return A is mob_eye_camera;
    }

    // tg: code/__DEFINES/is_helpers.dm:326  (the M7 supermatter slice: the crystal consumes no effect)
    public static bool iseffect(object O)
    {
        return O is obj_effect;
    }

    // tg: code/__DEFINES/is_helpers.dm:178  #define HAS_SILICON_ACCESS(possible_silicon) (M9 slice E: a computer's activation message)
    // GAP(lead): isAdminGhostAI (mob_helpers.dm:357) needs an admin observer with admin rights (check_rights_for, M10), so no mob is one
    public static bool HAS_AI_ACCESS(object possible_ai) => HAS_TRAIT(possible_ai as datum, TRAIT_AI_ACCESS);
    public static bool HAS_SILICON_ACCESS(object possible_silicon) => HAS_TRAIT(possible_silicon as datum, TRAIT_SILICON_ACCESS);

    // tg: code/__DEFINES/is_helpers.dm:356  (the M7 supermatter slice: a dusted clown's damage roll)
    public static bool is_clown_job(object job_type)
    {
        return job_type is datum_job_clown;
    }
}

// tg: code/__DEFINES/is_helpers.dm:30
public partial class datum_controller_global_vars
{
    // tg: code/__DEFINES/is_helpers.dm:30  GLOBAL_LIST_INIT(turfs_without_ground, typecacheof(list(...)))
    public DmList turfs_without_ground;

    // tg: code/__DEFINES/is_helpers.dm:30  (GLOBAL_MANAGED(turfs_without_ground, typecacheof(list(...))), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalturfs_without_ground()
    {
        // GAP(lead): /turf/open/chasm, /turf/open/lava and /turf/open/openspace are not declared (chasms and lava with the
        // planets, open space with multi-z, later milestones): no turf is one, so leaving them out of the typecache gives
        // the same answers; /turf/open/space/openspace is a subtype of /turf/open/space, which typecacheof covers.
        turfs_without_ground = typecacheof(Dm.list(
            typeof(turf_open_space),
            typeof(turf_open_water)
            )) as DmList;
        Dm.append(ref gvars_datum_init_order, "turfs_without_ground");
    }
}

// tg: code/__DEFINES/is_helpers.dm:41  (M4b slice 3)
public partial class datum_controller_global_vars
{
    // tg: code/__DEFINES/is_helpers.dm:41  GLOBAL_LIST_INIT(turfs_no_slip_water, typecacheof(list(...)))
    public DmList turfs_no_slip_water;

    // tg: code/__DEFINES/is_helpers.dm:41  (GLOBAL_MANAGED(turfs_no_slip_water, typecacheof(list(...))), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalturfs_no_slip_water()
    {
        turfs_no_slip_water = typecacheof(Dm.list(
            typeof(turf_open_misc_asteroid),
            typeof(turf_open_misc_dirt),
            typeof(turf_open_misc_grass),
            typeof(turf_open_misc_basalt),
            typeof(turf_open_misc_ashplanet),
            typeof(turf_open_misc_snow),
            typeof(turf_open_misc_sandy_dirt),
            typeof(turf_open_floor_noslip)
            )) as DmList;
        Dm.append(ref gvars_datum_init_order, "turfs_no_slip_water");
    }
}

// tg: code/__DEFINES/is_helpers.dm:53  (M4c slice 5: the airlock pump's space test)
public partial class datum_controller_global_vars
{
    // tg: code/__DEFINES/is_helpers.dm:53  GLOBAL_LIST_INIT(turfs_openspace, typecacheof(list(...)))
    public DmList turfs_openspace;

    // tg: code/__DEFINES/is_helpers.dm:53  (GLOBAL_MANAGED(turfs_openspace, typecacheof(list(...))), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalturfs_openspace()
    {
        turfs_openspace = typecacheof(Dm.list(
            typeof(turf_open_openspace),
            typeof(turf_open_space_openspace)
            )) as DmList;
        Dm.append(ref gvars_datum_init_order, "turfs_openspace");
    }
}

// tg: code/__DEFINES/is_helpers.dm:338  (gaps ST2: what a bookcase shelves)
public partial class datum_controller_global_vars
{
    // tg: code/__DEFINES/is_helpers.dm:338  GLOBAL_LIST_INIT(book_types, typecacheof(list(...)))
    public DmList book_types;

    // tg: code/__DEFINES/is_helpers.dm:338  (GLOBAL_MANAGED(book_types, typecacheof(list(...))), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalbook_types()
    {
        book_types = typecacheof(Dm.list(
            typeof(obj_item_book),
            typeof(obj_item_spellbook),
            typeof(obj_item_tgui_book),
            typeof(obj_item_storage_photo_album),
            typeof(obj_item_storage_card_binder),
            typeof(obj_item_codex_cicatrix),
            typeof(obj_item_toy_eldritch_book),
            typeof(obj_item_toy_talking_codex_gigas),
            typeof(obj_item_book_of_babel)
            )) as DmList;
        Dm.append(ref gvars_datum_init_order, "book_types");
    }
}

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/is_helpers.dm:332  #define isProbablyWallMounted(O) (O.pixel_x > 20 || O.pixel_x < -20 || O.pixel_y > 20 || O.pixel_y < -20) (gaps ST2: the static plaques)
    public static bool isProbablyWallMounted(atom O) => O.pixel_x > 20 || O.pixel_x < -20 || O.pixel_y > 20 || O.pixel_y < -20;

    // tg: code/__DEFINES/is_helpers.dm:333  #define isbook(O) (is_type_in_typecache(O, GLOB.book_types)) (gaps ST2: the bookcases)
    public static bool isbook(object O) => Dm.truthy(is_type_in_typecache(O, GLOB.book_types));
}

// tg: code/__DEFINES/is_helpers.dm:316  (M5 slice 5: the glass sheets a grille takes)
public partial class datum_controller_global_vars
{
    // tg: code/__DEFINES/is_helpers.dm:316  GLOBAL_LIST_INIT(glass_sheet_types, typecacheof(list(...)))
    public DmList glass_sheet_types;

    // tg: code/__DEFINES/is_helpers.dm:316  (GLOBAL_MANAGED(glass_sheet_types, typecacheof(list(...))), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalglass_sheet_types()
    {
        glass_sheet_types = typecacheof(Dm.list(
            typeof(obj_item_stack_sheet_glass),
            typeof(obj_item_stack_sheet_rglass),
            typeof(obj_item_stack_sheet_plasmaglass),
            typeof(obj_item_stack_sheet_plasmarglass),
            typeof(obj_item_stack_sheet_titaniumglass),
            typeof(obj_item_stack_sheet_plastitaniumglass)
            )) as DmList;
        Dm.append(ref gvars_datum_init_order, "glass_sheet_types");
    }
}

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/is_helpers.dm:265  #define iscloset(A) (istype(A, /obj/structure/closet)) (E16: the manufacturing storage unit and unloader)
    public static bool iscloset(object A)
    {
        return A is obj_structure_closet;
    }

    // tg: code/__DEFINES/is_helpers.dm:271  #define isidcard(I) (istype(I, /obj/item/card/id)) (E16: the big manipulator's ID lock)
    public static bool isidcard(object I)
    {
        return I is obj_item_card_id;
    }

    // tg: code/__DEFINES/is_helpers.dm:303  #define is_reagent_container(O) (istype(O, /obj/item/reagent_containers)) (gaps S1: a reagent's drink quality)
    public static bool is_reagent_container(object O) => O is obj_item_reagent_containers;

    // tg: code/__DEFINES/is_helpers.dm:135  #define isabductor(A) (is_species(A, /datum/species/abductor)) (gaps P1: the dissection's value)
    public static bool isabductor(object A) => Dm.truthy(is_species(A, typeof(datum_species_abductor)));

    // tg: code/__DEFINES/is_helpers.dm:150  #define isdullahan(A) (is_species(A, /datum/species/dullahan))
    public static bool isdullahan(object A) => Dm.truthy(is_species(A, typeof(datum_species_dullahan)));

    // tg: code/__DEFINES/is_helpers.dm:137  #define isgolem(A) (is_species(A, /datum/species/golem)) (gaps P1: the dissection's value)
    public static bool isgolem(object A) => Dm.truthy(is_species(A, typeof(datum_species_golem)));

    // tg: code/__DEFINES/is_helpers.dm:138  #define islizard(A) (is_species(A, /datum/species/lizard)) (gaps SP1: the lizards)
    public static bool islizard(object A) => Dm.truthy(is_species(A, typeof(datum_species_lizard)));

    // tg: code/__DEFINES/is_helpers.dm:141  #define ispodperson(A) (is_species(A, /datum/species/pod)) (gaps P1: the dissection's value)
    public static bool ispodperson(object A) => Dm.truthy(is_species(A, typeof(datum_species_pod)));

    // tg: code/__DEFINES/is_helpers.dm:142  #define isflyperson(A) (is_species(A, /datum/species/fly)) (silicons SI2: the flyswatter)
    public static bool isflyperson(object A) => Dm.truthy(is_species(A, typeof(datum_species_fly)));

    // tg: code/__DEFINES/is_helpers.dm:143  #define isjellyperson(A) (is_species(A, /datum/species/jelly)) (gaps P1: the dissection's value)
    public static bool isjellyperson(object A) => Dm.truthy(is_species(A, typeof(datum_species_jelly)));

    // tg: code/__DEFINES/is_helpers.dm:146  #define ismoth(A) (is_species(A, /datum/species/moth)) (silicons SI2: the flyswatter)
    public static bool ismoth(object A) => Dm.truthy(is_species(A, typeof(datum_species_moth)));

    // tg: code/__DEFINES/is_helpers.dm:147  #define isfelinid(A) (is_species(A, /datum/species/human/felinid)) (gaps SP1: the felinids)
    public static bool isfelinid(object A) => Dm.truthy(is_species(A, typeof(datum_species_human_felinid)));

    // tg: code/__DEFINES/is_helpers.dm:167  #define isalienroyal(A) (istype(A, /mob/living/carbon/alien/adult/royal)) (gaps P1: the dissection's value)
    public static bool isalienroyal(object A) => A is mob_living_carbon_alien_adult_royal;

    // tg: code/__DEFINES/is_helpers.dm:163  #define isalienhunter(A) (istype(A, /mob/living/carbon/alien/adult/hunter)) (gaps X5: the alien HUD)
    public static bool isalienhunter(object A) => A is mob_living_carbon_alien_adult_hunter;

    // tg: code/__DEFINES/is_helpers.dm:165  #define isaliensentinel(A) (istype(A, /mob/living/carbon/alien/adult/sentinel)) (gaps X5)
    public static bool isaliensentinel(object A) => A is mob_living_carbon_alien_adult_sentinel;

    // tg: code/__DEFINES/is_helpers.dm:169  #define isalienqueen(A) (istype(A, /mob/living/carbon/alien/adult/royal/queen)) (gaps X5: the alien HUD)
    public static bool isalienqueen(object A) => A is mob_living_carbon_alien_adult_royal_queen;

    // tg: code/__DEFINES/is_helpers.dm:364  #define isprojectilespell(thing) (istype(thing, /datum/action/cooldown/spell/pointed/projectile)) (gaps E4b: the on-hit effect element)
    public static bool isprojectilespell(object thing) => thing is datum_action_cooldown_spell_pointed_projectile;
}
