// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/fishing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the fishing subsystem, which at start spawns every fish once to cache its properties (its baits, traits,
// evolutions and beauty score, which the encyclopedia shows) and the fish that can be caught by their ids. The var block
// is generated (D111).

/// subsystem for the fishing minigame processing.
// tg: code/controllers/subsystem/processing/fishing.dm:2 PROCESSING_SUBSYSTEM_DEF(fishing)
public partial class datum_controller_subsystem_processing_fishing : datum_controller_subsystem_processing
{
    // The New() that PROCESSING_SUBSYSTEM_DEF(fishing) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SSfishing)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/fishing.dm:2
    public override async DmTask New(params object[] __extra)
    {
        if (SSfishing != this)
        {
            if (SSfishing is datum_controller_subsystem_processing_fishing)
            {
                Recover();
                qdel(SSfishing);
            }
            SSfishing = this;
        }
        PreInit();
        await DmTask.CompletedTask;
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(fishing) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/processing/fishing.dm:2
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }

    // tg: code/controllers/subsystem/processing/fishing.dm:22
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        await base.Initialize(__extra);
        cached_fish_icons = Dm.list();
        cached_unknown_fish_icons = Dm.list();
        fish_properties = Dm.list();
        catchable_fish = Dm.list();

        // tg: fishing.dm:29-45  each fish's icon and its unknown-fish outline as base64 PNGs (icon2base64): the server renders no
        // icons (D104), and the two caches feed only the fishing achievement's window, which waits with the achievements (D332).
        DmList spawned_fish = Dm.list();
        DmList fish_subtypes = sortTim(subtypesof(typeof(obj_item_fish)) as DmList, "/proc/" + nameof(GlobalProcs.cmp_init_name_asc)) as DmList;
        foreach (Type fish_type in Dm.each<Type>(fish_subtypes))
        {
            obj_item_fish fish = await Dm.new_async(fish_type) as obj_item_fish;
            Dm.append(spawned_fish, fish);
            DmList properties = Dm.list();
            fish_properties[fish_type] = properties;
            properties[FISH_PROPERTIES_FAV_BAIT] = fish.favorite_bait.Copy();
            properties[FISH_PROPERTIES_BAD_BAIT] = fish.disliked_bait.Copy();
            properties[FISH_PROPERTIES_TRAITS] = fish.fish_traits.Copy();

            DmList evo_types = fish.evolution_types?.Copy();
            properties[FISH_PROPERTIES_EVOLUTIONS] = evo_types;
            foreach (object type in Dm.each(evo_types))
            {
                DmList by_evolution = GLOB.fishes_by_fish_evolution[type] as DmList;
                LAZYADD(ref by_evolution, fish_type);
                GLOB.fishes_by_fish_evolution[type] = by_evolution;
            }

            string beauty_score = "???";
            // DM's switch ranges: the first range that holds the value wins.
            if (fish.beauty <= FISH_BEAUTY_DISGUSTING)
                beauty_score = "OH HELL NAW!";
            else if (fish.beauty <= FISH_BEAUTY_UGLY)
                beauty_score = "☆☆☆☆☆";
            else if (fish.beauty <= FISH_BEAUTY_BAD)
                beauty_score = "★☆☆☆☆";
            else if (fish.beauty <= FISH_BEAUTY_NULL)
                beauty_score = "★★☆☆☆";
            else if (fish.beauty <= FISH_BEAUTY_GENERIC)
                beauty_score = "★★★☆☆";
            else if (fish.beauty <= FISH_BEAUTY_GOOD)
                beauty_score = "★★★★☆";
            else if (fish.beauty <= FISH_BEAUTY_GREAT)
                beauty_score = "★★★★★";
            else
                beauty_score = "★★★★★★";

            properties[FISH_PROPERTIES_BEAUTY_SCORE] = beauty_score;

            object fish_id;
            if (fish.fish_id_redirect_path != null)
            {
                Type other_path = fish.fish_id_redirect_path;
                if (!ispath(other_path, typeof(obj_item_fish)))
                {
                    stack_trace($"{fish.type} has a set 'fish_id_redirect_path' variable but it isn't a fish path but {other_path}", "code/controllers/subsystem/processing/fishing.dm", 83);
                    continue;
                }
                fish_id = Dm.initial(other_path, (obj_item_fish p) => p.fish_id);
            }
            else
                fish_id = fish.fish_id;
            if (!Dm.truthy(fish_id))
            {
                stack_trace($"{fish.type} doesn't have a set 'fish_id' variable despite being a catchable fish", "code/controllers/subsystem/processing/fishing.dm", 89);
                continue;
            }
            if (fish.fish_id_redirect_path != null)
                continue;
            if (Dm.truthy(catchable_fish[fish_id]))
            {
                stack_trace($"{fish.type} has a 'fish_id' value already assigned to {Dm.str(catchable_fish[fish_id])}. fish_id: {Dm.str(fish_id)}", "code/controllers/subsystem/processing/fishing.dm", 94);
                continue;
            }
            catchable_fish[fish_id] = fish.type;
        }

        ///init the list of things lures can catch
        lure_catchables = Dm.list();
        // tg: fishing.dm:100-107  each lure's catchable fish (is_catchable_fish): the lures come with the rods (not-translated section 250).
        Dm.Deferred("code/controllers/subsystem/processing/fishing.dm:100", "Initialize: the fish each fishing lure can catch (/obj/item/fishing_lure/is_catchable_fish), the rods' lures");

        QDEL_LIST(spawned_fish);

        //Populate the list of safe turfs for several fishes
        // GAP(the rods): GLOB.preset_fish_sources, the fishing spots, comes with the rods; until then no fish has safe turfs.
        DmList preset_fish_sources__gap = null;
        foreach (object source_path in Dm.each(preset_fish_sources__gap))
        {
            datum_fish_source source = preset_fish_sources__gap[source_path] as datum_fish_source;
            if (!Dm.truthy(length(source.associated_safe_turfs)))
                continue;
            foreach (object fish_path in Dm.each(source.fish_table))
            {
                if (!ispath(fish_path, typeof(obj_item_fish)))
                    continue;
                DmList safe_turfs = fish_safe_turfs_by_type[fish_path] as DmList;
                LAZYOR(ref safe_turfs, source.associated_safe_turfs);
                fish_safe_turfs_by_type[fish_path] = safe_turfs;
            }
        }
        //If a subtype doesn't have set safe turfs, it'll inherit them from the parent type.
        foreach (object fish_type in Dm.each(fish_safe_turfs_by_type))
        {
            foreach (object fish_subtype in Dm.each(subtypesof(fish_type)))
            {
                if (!Dm.truthy(length(fish_safe_turfs_by_type[fish_subtype])))
                    fish_safe_turfs_by_type[fish_subtype] = fish_safe_turfs_by_type[fish_type];
            }
        }

        return SS_INIT_SUCCESS;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/processing/fishing.dm:2  PROCESSING_SUBSYSTEM_DEF(fishing): GLOBAL_REAL(SSfishing, /datum/controller/subsystem/processing/fishing)
    internal datum_controller_subsystem_processing_fishing SSfishing;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_fishing SSfishing => ref TgState.Current.SSfishing;
}
