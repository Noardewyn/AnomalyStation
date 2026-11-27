// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fish/types/tiziran.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the Tiziran fish (the moonfish that lays eggs and its dwarf breed that grows into one, the gunner jellyfish, the
// needlefish and the armorfish). The var blocks are generated (D111).

// tg: code/modules/fishing/fish/types/tiziran.dm:3
public partial class obj_item_fish_moonfish : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/tiziran.dm:31
    [DmParams("mapload", "apply_qualities")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_fish_growth), typeof(obj_item_food_moonfish_eggs), egg_laying_time, Dm.named("use_drop_loc", FALSE), Dm.named("del_on_grow", FALSE), Dm.named("inherit_name", FALSE)));
        RegisterSignal(this, COMSIG_FISH_BEFORE_GROWING, nameof(egg_checks));
        return dot;
    }

    ///Stop laying eggs if we're in an unsafe environment, starving of if there are simply too many eggs already.
    // tg: code/modules/fishing/fish/types/tiziran.dm:37
    public virtual async DmTask<object> egg_checks(datum source = null, object seconds_per_tick = null, object growth = null, object result_path = null)
    {
        if (!Dm.eq(result_path, typeof(obj_item_food_moonfish_eggs))) //Don't stop the growth of the dwarf subtype.
            return null;
        if (!Dm.truthy(await proper_environment()) || Dm.truthy(get_starvation_mult()))
            return COMPONENT_DONT_GROW;
        float count = 0;
        foreach (obj_item_food_moonfish_eggs egg in Dm.each<obj_item_food_moonfish_eggs>(loc))
        {
            count++;
            if (count > 10)
                return COMPONENT_DONT_GROW;
        }
        return null;
    }
}

// tg: code/modules/fishing/fish/types/tiziran.dm:48
public partial class obj_item_fish_moonfish_dwarf : obj_item_fish_moonfish
{
    // tg: code/modules/fishing/fish/types/tiziran.dm:64
    public override async DmTask update_size_and_weight(object new_size = null, object new_weight = null, float? update_materials = null)
    {
        new_size ??= average_size;
        new_weight ??= average_weight;
        update_materials ??= TRUE;
        await base.update_size_and_weight(new_size, new_weight, update_materials);
        float multiplier = (Dm.num(size) / (average_size * 1.5f)) * (Dm.num(weight) / (average_weight * 1.5f));

        await AddComponent(typeof(datum_component_fish_growth), typeof(datum_fish_evolution_moonfish), 2.5f * MINUTES * multiplier, Dm.named("use_drop_loc", FALSE));
    }
}

// tg: code/modules/fishing/fish/types/tiziran.dm:70
public partial class obj_item_fish_gunner_jellyfish : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/tiziran.dm:89
    [DmParams("mapload", "apply_qualities")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_quality_food_ingredient), FOOD_COMPLEXITY_2));
        return dot;
    }

    // tg: code/modules/fishing/fish/types/tiziran.dm:93
    public override object get_fish_taste()
    {
        return Dm.list(Dm.kv("cold jelly", 2));
    }

    // tg: code/modules/fishing/fish/types/tiziran.dm:96
    public override object get_fish_taste_cooked()
    {
        return Dm.list(Dm.kv("crunchy tenderness", 2));
    }
}

// tg: code/modules/fishing/fish/types/tiziran.dm:99
public partial class obj_item_fish_needlefish : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/tiziran.dm:116
    [DmParams("mapload", "apply_qualities")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_traits(Dm.list(TRAIT_FISHING_BAIT, TRAIT_GOOD_QUALITY_BAIT), INNATE_TRAIT);
        return dot;
    }
}

// tg: code/modules/fishing/fish/types/tiziran.dm:120
public partial class obj_item_fish_armorfish : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/tiziran.dm:137
    [DmParams("mapload", "apply_qualities")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_traits(Dm.list(TRAIT_FISHING_BAIT, TRAIT_GOOD_QUALITY_BAIT), INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/fishing/fish/types/tiziran.dm:141
    public override object get_fish_taste()
    {
        return Dm.list(Dm.kv("raw prawn", 2));
    }

    // tg: code/modules/fishing/fish/types/tiziran.dm:144
    public override object get_fish_taste_cooked()
    {
        return Dm.list(Dm.kv("cooked prawn", 2));
    }
}
