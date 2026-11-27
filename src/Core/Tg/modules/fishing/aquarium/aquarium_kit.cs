// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/aquarium/aquarium_kit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the stasis fish cases, which carry a fish safe from the air and start with one (the random, freshwater,
// saltwater, Tiziran and black-market cases; the syndicate case picks from the syndicate fishing portal's fish, which come
// with the rods). The aquarium kit and the aquarium props come with the aquariums. The var blocks are generated (D111).

/**
 * Stasis fish case container for moving fish between aquariums safely.
 * Their w_class scales with that of the fish inside it.
 * Most subtypes of this also start with a fish already inside.
 */
// tg: code/modules/fishing/aquarium/aquarium_kit.dm:22
public partial class obj_item_storage_fish_case : obj_item_storage
{
    // tg: code/modules/fishing/aquarium/aquarium_kit.dm:34
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_fish_safe_storage)));
        return dot;
    }

    // tg: code/modules/fishing/aquarium/aquarium_kit.dm:38
    public override async DmTask PopulateContents()
    {
        Type fish_type = get_fish_type() as Type;
        if (fish_type != null)
        {
            obj_item_fish spawned_fish = await Dm.new_async(fish_type, null) as obj_item_fish;
            spawned_fish.add_traits(Dm.list(TRAIT_NO_FISHING_ACHIEVEMENT, TRAIT_FISH_LOW_PRICE), INNATE_TRAIT);
            await spawned_fish.forceMove(this); // trigger storage.handle_entered
        }
    }

    // tg: code/modules/fishing/aquarium/aquarium_kit.dm:45
    public virtual object get_fish_type()
    {
        return null;
    }
}

// tg: code/modules/fishing/aquarium/aquarium_kit.dm:48
public partial class obj_item_storage_fish_case_random : obj_item_storage_fish_case
{
    // tg: code/modules/fishing/aquarium/aquarium_kit.dm:52
    public override object get_fish_type()
    {
        return random_fish_type(fluid_type);
    }
}

// tg: code/modules/fishing/aquarium/aquarium_kit.dm:73
public partial class obj_item_storage_fish_case_tiziran : obj_item_storage_fish_case
{
    // tg: code/modules/fishing/aquarium/aquarium_kit.dm:75
    public override object get_fish_type()
    {
        return pick_weight(Dm.list(
            Dm.kv(typeof(obj_item_fish_moonfish_dwarf), 2),
            Dm.kv(typeof(obj_item_fish_gunner_jellyfish), 2),
            Dm.kv(typeof(obj_item_fish_needlefish), 2),
            Dm.kv(typeof(obj_item_fish_armorfish), 2),
            Dm.kv(typeof(obj_item_fish_moonfish), 1)));
    }
}

///Subtype bought from the blackmarket at a gratuitously cheap price. The catch? The fish inside it is dead.
// tg: code/modules/fishing/aquarium/aquarium_kit.dm:85
public partial class obj_item_storage_fish_case_blackmarket : obj_item_storage_fish_case
{
    // tg: code/modules/fishing/aquarium/aquarium_kit.dm:89
    public override object get_fish_type()
    {
        DmList weighted_list = TgState.Current.obj_item_storage_fish_case_blackmarket__get_fish_type__weighted_list ??= Dm.list(
            Dm.kv(typeof(obj_item_fish_clownfish_lube), 2),
            Dm.kv(typeof(obj_item_fish_pufferfish), 2),
            Dm.kv(typeof(obj_item_fish_emulsijack), 2),
            Dm.kv(typeof(obj_item_fish_sludgefish_purple), 2),
            Dm.kv(typeof(obj_item_fish_slimefish), 2),
            Dm.kv(typeof(obj_item_fish_ratfish), 2),
            Dm.kv(typeof(obj_item_fish_zipzap), 2),
            Dm.kv(typeof(obj_item_fish_jumpercable), 2),
            Dm.kv(typeof(obj_item_fish_chasm_crab_ice), 1),
            Dm.kv(typeof(obj_item_fish_chasm_crab), 1),
            Dm.kv(typeof(obj_item_fish_boned), 1),
            Dm.kv(typeof(obj_item_fish_pike), 1),
            Dm.kv(typeof(obj_item_fish_swordfish), 1));
        return pick_weight(weighted_list);
    }

    // tg: code/modules/fishing/aquarium/aquarium_kit.dm:107
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        foreach (obj_item_fish fish in Dm.each<obj_item_fish>(contents))
            Waiting.NoSleep(fish.set_status(FISH_DEAD));
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/fishing/aquarium/aquarium_kit.dm:90  get_fish_type's var/static/list/weighted_list (rules 10.6)
    internal DmList obj_item_storage_fish_case_blackmarket__get_fish_type__weighted_list;
}
