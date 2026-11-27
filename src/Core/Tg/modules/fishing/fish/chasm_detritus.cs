// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fish/chasm_detritus.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: what a fishing rod pulls out of a chasm, the things and the bodies fallen into it or the bones, slag,
// skeletons and lobstrosities it holds anyway. The chasms' fishing spot is fishing's own. The var blocks are generated (D111).

// tg: code/modules/fishing/fish/chasm_detritus.dm:9 (interleaved types, rules 1.9)
public partial class datum_controller_global_vars : datum_controller
{
    ///List containing chasm detritus singletons.
    // tg: code/modules/fishing/fish/chasm_detritus.dm:9  GLOBAL_LIST_INIT_TYPED(chasm_detritus_types, /datum/chasm_detritus, init_chasm_detritus())
    public DmList chasm_detritus_types;

    // tg: code/modules/fishing/fish/chasm_detritus.dm:9  (GLOBAL_MANAGED, __DEFINES/__globals.dm:5)
    public virtual async DmTask InitGlobalchasm_detritus_types()
    {
        chasm_detritus_types = await init_chasm_detritus() as DmList;
        Dm.append(ref gvars_datum_init_order, "chasm_detritus_types");
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/fishing/fish/chasm_detritus.dm:11
    public static async DmTask<object> init_chasm_detritus()
    {
        //as the name suggests, init_subtypes_w_path_keys doesn't init the prototype, so here we go.
        DmList glob_list = Dm.list();
        glob_list[typeof(datum_chasm_detritus)] = Dm.@new<datum_chasm_detritus>();
        return await init_subtypes_w_path_keys(typeof(datum_chasm_detritus), glob_list);
    }
}

/// A datum that retrieves something which fell into a chasm.
// tg: code/modules/fishing/fish/chasm_detritus.dm:18
public partial class datum_chasm_detritus : datum
{
    /// Regular default contents.
    // tg: code/modules/fishing/fish/chasm_detritus.dm:2  #define NORMAL_CONTENTS "normal" (file-local)
    private const string NORMAL_CONTENTS = "normal";
    /// Bodies-only default contents.
    // tg: code/modules/fishing/fish/chasm_detritus.dm:4  #define BODIES_ONLY "bodies_only" (file-local)
    private const string BODIES_ONLY = "bodies_only";
    /// No corpses (so there's still lobstrosities because they're the main threat with this).
    // tg: code/modules/fishing/fish/chasm_detritus.dm:6  #define NO_CORPSES "no_corpses" (file-local)
    private const string NO_CORPSES = "no_corpses";

    /// Stuff which you can always fish up even if nothing fell into a hole. Associative by type.
    // tg: code/modules/fishing/fish/chasm_detritus.dm:26  var/static/list/default_contents (rules 10.6)
    public static DmList default_contents => TgState.Current.datum_chasm_detritus__default_contents ??= Dm.list(
        Dm.kv(NORMAL_CONTENTS, Dm.list(
            Dm.kv(typeof(obj_item_stack_sheet_bone), 6),
            Dm.kv(typeof(obj_item_stack_ore_slag), 4),
            Dm.kv(typeof(obj_effect_mob_spawn_corpse_human_skeleton), 2),
            Dm.kv(typeof(obj_effect_mob_spawn_corpse_human_skeleton_cerulean), 1),
            Dm.kv(typeof(mob_living_basic_mining_lobstrosity_lava), 1),
            Dm.kv(typeof(mob_living_basic_mining_lobstrosity_juvenile_lava), 1))),
        Dm.kv(BODIES_ONLY, Dm.list(
            Dm.kv(typeof(obj_effect_mob_spawn_corpse_human_skeleton), 6),
            Dm.kv(typeof(obj_effect_mob_spawn_corpse_human_skeleton_cerulean), 1),
            Dm.kv(typeof(mob_living_basic_mining_lobstrosity_lava), 1),
            Dm.kv(typeof(mob_living_basic_mining_lobstrosity_juvenile_lava), 1))),
        Dm.kv(NO_CORPSES, Dm.list(
            Dm.kv(typeof(obj_item_stack_sheet_bone), 28),
            Dm.kv(typeof(obj_item_stack_ore_slag), 20),
            Dm.kv(typeof(mob_living_basic_mining_lobstrosity_lava), 1),
            Dm.kv(typeof(mob_living_basic_mining_lobstrosity_juvenile_lava), 1))));

    // tg: code/modules/fishing/fish/chasm_detritus.dm:48
    public virtual async DmTask<object> dispense_detritus(atom spawn_location = null, atom fishing_spot = null)
    {
        if (prob(default_contents_chance))
        {
            object default_spawn = pick(default_contents[default_contents_key] as DmList);
            return Dm.@new((Type)default_spawn, spawn_location);
        }
        return await find_chasm_contents(get_turf(fishing_spot) as turf, spawn_location as turf);
    }

    /// Returns the chosen detritus from the given list of things to choose from
    // tg: code/modules/fishing/fish/chasm_detritus.dm:55
    public virtual object determine_detritus(DmList chasm_stuff = null)
    {
        return pick(chasm_stuff);
    }

    /// Returns an object which is currently inside of a nearby chasm.
    // tg: code/modules/fishing/fish/chasm_detritus.dm:59
    public virtual async DmTask<object> find_chasm_contents(turf fishing_spot = null, turf fisher_turf = null)
    {
        DmList chasm_contents = get_chasm_contents(fishing_spot) as DmList;

        if (!Dm.truthy(length(chasm_contents)))
        {
            object default_spawn = pick(default_contents[default_contents_key] as DmList);
            return Dm.@new((Type)default_spawn, fisher_turf);
        }

        atom_movable detritus = determine_detritus(chasm_contents) as atom_movable;
        await detritus.forceMove(fisher_turf);
        return detritus;
    }

    // tg: code/modules/fishing/fish/chasm_detritus.dm:70
    public virtual object get_chasm_contents(turf fishing_spot = null)
    {
        object dot = Dm.list();
        foreach (obj_effect_abstract_chasm_storage storage in Dm.each<obj_effect_abstract_chasm_storage>(range(5, fishing_spot)))
        {
            foreach (object thing in Dm.each(storage.contents))
                Dm.append(ref dot, thing);
        }
        return dot;
    }
}

/// Variant of the chasm detritus that allows for an easier time at fishing out
/// bodies, and sometimes less desireable monsters too.
// tg: code/modules/fishing/fish/chasm_detritus.dm:78
public partial class datum_chasm_detritus_restricted : datum_chasm_detritus
{
    // tg: code/modules/fishing/fish/chasm_detritus.dm:83
    public override object get_chasm_contents(turf fishing_spot = null)
    {
        object dot = Dm.list();
        foreach (obj_effect_abstract_chasm_storage storage in Dm.each<obj_effect_abstract_chasm_storage>(range(5, fishing_spot)))
        {
            foreach (object thing in Dm.each(storage.contents))
            {
                if (!istype(thing, chasm_storage_restricted_type))
                    continue;
                Dm.append(ref dot, thing);
            }
        }
        return dot;
    }
}

// tg: code/modules/fishing/fish/chasm_detritus.dm:95
public partial class datum_chasm_detritus_restricted_bodies : datum_chasm_detritus_restricted
{
    /// This also includes all mobs fallen into chasms, regardless of distance
    // tg: code/modules/fishing/fish/chasm_detritus.dm:101
    public override object get_chasm_contents(turf fishing_spot = null)
    {
        object dot = base.get_chasm_contents(fishing_spot);
        Dm.combine(ref dot, GLOB.chasm_fallen_mobs[get_chasm_category(fishing_spot)]);
        return dot;
    }

    /// Body detritus is selected in favor of bodies belonging to sentient mobs
    /// The first sentient body found in the list of contents is returned, otherwise
    /// if none are sentient choose randomly.
    // tg: code/modules/fishing/fish/chasm_detritus.dm:108
    public override object determine_detritus(DmList chasm_stuff = null)
    {
        foreach (mob fallen_mob in Dm.each_cast<mob>(chasm_stuff))
        {
            if (fallen_mob.mind != null)
                return fallen_mob;
        }
        return base.determine_detritus(chasm_stuff);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/fishing/fish/chasm_detritus.dm:26  /datum/chasm_detritus var/static/list/default_contents
    internal DmList datum_chasm_detritus__default_contents;
}
