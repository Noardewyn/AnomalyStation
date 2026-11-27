// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fish/types/station.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the station's own fish (the ratfish, the sludgefish, the aquatic slime, the fryish and the fritterish that
// grows out of it, with the fritterish's dinosaur and pterodactyl forms, and the nessie-fish). The var blocks are generated (D111).

// tg: code/modules/fishing/fish/types/station.dm:1
public partial class obj_item_fish_ratfish : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/station.dm:24
    public override object get_fish_taste()
    {
        return Dm.list(Dm.kv("vermin", 2), Dm.kv("maintenance", 1));
    }

    // tg: code/modules/fishing/fish/types/station.dm:27
    public override object get_fish_taste_cooked()
    {
        return Dm.list(Dm.kv("cooked vermin", 2), Dm.kv("burned fur", 0.5f));
    }

    // tg: code/modules/fishing/fish/types/station.dm:30
    public override object get_food_types()
    {
        return Dm.bitor(Dm.bitor(MEAT, RAW), GORE); //Not-so-quite-seafood
    }

    // tg: code/modules/fishing/fish/types/station.dm:33
    public override object get_base_edible_reagents_to_add()
    {
        DmList return_list = base.get_base_edible_reagents_to_add() as DmList;
        return_list[typeof(datum_reagent_rat_spit)] = 1;
        return return_list;
    }

    // tg: code/modules/fishing/fish/types/station.dm:38
    [DmParams("mapload", "apply_qualities")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        //stable pop reflects the config for how many mice migrate. powerful...
        stable_population = Dm.num(config.Get(typeof(datum_config_entry_number_mice_roundstart)));
        return dot;
    }
}

// tg: code/modules/fishing/fish/types/station.dm:43
public partial class obj_item_fish_sludgefish : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/station.dm:62
    public override object get_fish_taste()
    {
        return Dm.list(Dm.kv("raw fish", 2), Dm.kv("eau de toilet", 1));
    }
}

// tg: code/modules/fishing/fish/types/station.dm:73
public partial class obj_item_fish_slimefish : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/station.dm:102
    public override object get_food_types()
    {
        return Dm.bitor(SEAFOOD, TOXIC);
    }

    // tg: code/modules/fishing/fish/types/station.dm:105
    public override object get_base_edible_reagents_to_add()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_toxin_slimejelly), 5));
    }
}

// tg: code/modules/fishing/fish/types/station.dm:108
public partial class obj_item_fish_fryish : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/station.dm:148
    [DmParams("mapload", "apply_qualities")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(is_bait))
            add_traits(Dm.list(TRAIT_FISHING_BAIT, TRAIT_GREAT_QUALITY_BAIT), INNATE_TRAIT);
        ADD_TRAIT(this, TRAIT_FISH_SURVIVE_COOKING, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/fishing/fish/types/station.dm:154
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] swallows [] whole! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        await user.say("Mmmm! Delicious!", forced: "fryfish suicide");
        qdel(this);
        return OXYLOSS;
    }

    // tg: code/modules/fishing/fish/types/station.dm:160
    public override async DmTask update_size_and_weight(object new_size = null, object new_weight = null, float? update_materials = null)
    {
        new_size ??= average_size;
        new_weight ??= average_weight;
        update_materials ??= TRUE;
        await base.update_size_and_weight(new_size, new_weight, update_materials);
        if (next_type == null)
            return;
        float multiplier = (Dm.num(size) / average_size) * (Dm.num(weight) / average_weight);

        await AddComponent(typeof(datum_component_fish_growth), next_type, Dm.num(growth_time) * multiplier, Dm.named("use_drop_loc", FALSE));
    }

    // tg: code/modules/fishing/fish/types/station.dm:168
    public override object get_fish_taste()
    {
        return Dm.list(Dm.kv("fried fish", 1));
    }

    // tg: code/modules/fishing/fish/types/station.dm:171
    public override object get_fish_taste_cooked()
    {
        return Dm.list(Dm.kv("extra-fried fish", 1));
    }

    // tg: code/modules/fishing/fish/types/station.dm:174
    public override object get_food_types()
    {
        return Dm.bitor(Dm.bitor(FRIED, MEAT), SEAFOOD);
    }

    // tg: code/modules/fishing/fish/types/station.dm:177
    public override object get_base_edible_reagents_to_add()
    {
        DmList return_list = Dm.list(
            Dm.kv(typeof(datum_reagent_consumable_nutriment_protein), 2),
            Dm.kv(typeof(datum_reagent_consumable_nutriment_fat), 1.5f));
        return return_list;
    }
}

// tg: code/modules/fishing/fish/types/station.dm:188
public partial class obj_item_fish_fryish_fritterish : obj_item_fish_fryish
{
    // tg: code/modules/fishing/fish/types/station.dm:184-186  #define FISH_FRITTERISH, FISH_BERNARD, FISH_MATTHEW (file-local)
    private const string FISH_FRITTERISH = "fritterish";
    private const string FISH_BERNARD = "bernard";
    private const string FISH_MATTHEW = "matthew";
    // tg: code/modules/fishing/fish/types/station.dm:224  #define PERSISTENCE_FISH_FRITTERISH_VARIANT (file-local)
    private const string PERSISTENCE_FISH_FRITTERISH_VARIANT = "fritterish_variant";

    // tg: code/modules/fishing/fish/types/station.dm:207
    [DmParams("mapload", "apply_qualities")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // Kept as tg has it: the variant is picked twice, and the second pick is the one loaded.
        variant = (string)pick(FISH_FRITTERISH, FISH_BERNARD, FISH_MATTHEW);
        load_variant(pick(FISH_FRITTERISH, FISH_BERNARD, FISH_MATTHEW));
        return dot;
    }

    // tg: code/modules/fishing/fish/types/station.dm:212
    public virtual void load_variant(object new_variant = null)
    {
        variant = (string)new_variant;
        switch (variant)
        {
            case FISH_BERNARD:
                sprite_width = 4;
                sprite_height = 6;
                base_icon_state = "bernardfish";
                break;
            case FISH_MATTHEW:
                sprite_width = 6;
                base_icon_state = "matthewfish";
                break;
        }
        update_appearance();
    }

    // tg: code/modules/fishing/fish/types/station.dm:226
    public override void persistence_save(DmList data = null)
    {
        data[PERSISTENCE_FISH_FRITTERISH_VARIANT] = variant;
    }

    // tg: code/modules/fishing/fish/types/station.dm:229
    public override async DmTask persistence_load(DmList data = null)
    {
        load_variant(data[PERSISTENCE_FISH_FRITTERISH_VARIANT]);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/fishing/fish/types/station.dm:234
    public override object update_name(float? updates = null)
    {
        switch (variant)
        {
            case FISH_BERNARD:
                name = I18n.T("bernard-fish");
                break;
            case FISH_MATTHEW:
                name = I18n.T("matthew-fish");
                break;
        }
        return base.update_name(updates);
    }

    // tg: code/modules/fishing/fish/types/station.dm:242
    public override object update_desc(float? updates = null)
    {
        switch (variant)
        {
            case FISH_BERNARD:
                desc = I18n.T("A <u>deliciously</u> extremophile alien fish shaped like a dinosaur. Children love it.");
                break;
            case FISH_MATTHEW:
                desc = I18n.T("A <u>deliciously</u> extremophile alien fish shaped like a pterodactyl. Children love it.");
                break;
        }
        return base.update_desc(updates);
    }
}
