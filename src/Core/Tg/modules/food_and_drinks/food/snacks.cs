// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/snacks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the South Bronx Paradise bar (its parasite and its second look), the chips' and corn chips' popable bags, the nuts'
// grinding, the mystery bags (peanuts, C&Ds, corn chips, Sticko and Shok-Roks: a random flavour of their kind) and the C&Ds' suicide.
// The var blocks are generated.

// tg: code/modules/food_and_drinks/food/snacks.dm:22
public partial class obj_item_food_candy_bronx : obj_item_food_candy
{
    // tg: code/modules/food_and_drinks/food/snacks.dm:41
    public override async DmTask make_edible()
    {
        await base.make_edible();
        await AddComponentFrom(SOURCE_EDIBLE_INNATE, typeof(datum_component_edible), Dm.named("on_consume", Dm.@new<datum_callback>(this, nameof(on_consume))));
    }

    // tg: code/modules/food_and_drinks/food/snacks.dm:45
    public virtual async DmTask on_consume(mob_living eater = null)
    {
        if (ishuman(eater))
        {
            mob_living_carbon_human carl = (mob_living_carbon_human)eater;
            datum_disease disease = Dm.@new<datum_disease_parasite>();
            await carl.ForceContractDisease(disease, make_copy: FALSE, del_on_fail: TRUE);
        }
    }

    // tg: code/modules/food_and_drinks/food/snacks.dm:51
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!Dm.truthy(revelation) && !isobserver(user))
        {
            Dm.append(ref dot, span_notice(I18n.T("Geeze, you need to get to get your eyes checked. You should look again...")));

            name = Dm.text("\\improper South Bronx Parasite bar");
            desc = "Lose weight, guaranteed! Caramel Mocha Flavor! WARNING: PRODUCT NOT FIT FOR HUMAN CONSUMPTION. CONTAINS LIVE DIAMPHIDIA SPECIMENS.";
            revelation = TRUE;
        }
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/snacks.dm:87
public partial class obj_item_food_chips : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/snacks.dm:105
    public override async DmTask make_leave_trash()
    {
        if (trash_type != null)
            await AddElement(typeof(datum_element_food_trash), trash_type, FOOD_TRASH_POPABLE);
    }
}

// tg: code/modules/food_and_drinks/food/snacks.dm:242
public partial class obj_item_food_peanuts : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/snacks.dm:256
    public override async DmTask<object> grind_results()
    {
        await DmTask.CompletedTask;
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_peanut_butter), 5), Dm.kv(typeof(datum_reagent_consumable_nutriment_fat_oil), 2));
    }
}

// tg: code/modules/food_and_drinks/food/snacks.dm:305
public partial class obj_item_food_peanuts_random : obj_item_food_peanuts
{
    // tg: code/modules/food_and_drinks/food/snacks.dm:320
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        // Generate a sample p
        object peanut_type = pick(GLOB.safe_peanut_types);
        obj_item_food sample = Dm.@new(peanut_type as Type, loc) as obj_item_food;

        name = sample.name;
        desc = sample.desc;
        food_reagents = sample.food_reagents;
        tastes = sample.tastes;

        qdel(sample);

        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/food_and_drinks/food/snacks.dm:311
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/food_and_drinks/food/snacks.dm:311  GLOBAL_LIST_INIT(safe_peanut_types, populate_safe_peanut_types())
    public DmList safe_peanut_types;

    // tg: code/modules/food_and_drinks/food/snacks.dm:311  (GLOBAL_MANAGED(safe_peanut_types, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsafe_peanut_types()
    {
        safe_peanut_types = populate_safe_peanut_types() as DmList;
        Dm.append(ref gvars_datum_init_order, "safe_peanut_types");
    }
}

// tg: code/modules/food_and_drinks/food/snacks.dm:313
public static partial class GlobalProcs
{
    // tg: code/modules/food_and_drinks/food/snacks.dm:313
    public static object populate_safe_peanut_types()
    {
        DmList dot = Dm.list();
        foreach (Type peanut_type in Dm.each<Type>(subtypesof(typeof(obj_item_food_peanuts))))
        {
            if (!Dm.truthy(Dm.initial(peanut_type, (obj_item_food_peanuts p) => p.safe_for_consumption)))
                continue;
            Dm.append(ref dot, peanut_type);
        }
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/snacks.dm:334
public partial class obj_item_food_cnds : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/snacks.dm:349
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is letting [] melt in [] hand! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
        await DmTask.CompletedTask;
        return TOXLOSS;
    }
}

// tg: code/modules/food_and_drinks/food/snacks.dm:394
public partial class obj_item_food_cnds_random : obj_item_food_cnds
{
    // tg: code/modules/food_and_drinks/food/snacks.dm:398
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object random_flavour = pick(Dm.sub(subtypesof(typeof(obj_item_food_cnds)), typeof(obj_item_food_cnds_random)));
        obj_item_food sample = Dm.@new(random_flavour as Type, loc) as obj_item_food;
        name = sample.name;
        desc = sample.desc;
        food_reagents = sample.food_reagents;
        tastes = sample.tastes;

        qdel(sample);

        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/food_and_drinks/food/snacks.dm:410
public partial class obj_item_food_pistachios : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/snacks.dm:426
    public override async DmTask<object> grind_results()
    {
        await DmTask.CompletedTask;
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_peanut_butter), 5), Dm.kv(typeof(datum_reagent_consumable_nutriment_fat_oil), 2));
    }
}

// tg: code/modules/food_and_drinks/food/snacks.dm:460
public partial class obj_item_food_cornchips : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/snacks.dm:478
    public override async DmTask make_leave_trash()
    {
        await AddElement(typeof(datum_element_food_trash), trash_type, FOOD_TRASH_POPABLE);
    }
}

// tg: code/modules/food_and_drinks/food/snacks.dm:537
public partial class obj_item_food_cornchips_random : obj_item_food_cornchips
{
    // tg: code/modules/food_and_drinks/food/snacks.dm:541
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object random_flavour = pick(Dm.sub(subtypesof(typeof(obj_item_food_cornchips)), typeof(obj_item_food_cornchips_random)));

        obj_item_food sample = Dm.@new(random_flavour as Type, loc) as obj_item_food;

        name = sample.name;
        desc = sample.desc;
        food_reagents = sample.food_reagents;
        icon_state = sample.icon_state;
        trash_type = sample.trash_type;
        tastes = sample.tastes;

        qdel(sample);

        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/food_and_drinks/food/snacks.dm:557
public partial class obj_item_food_hot_shots : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/snacks.dm:572
    public override async DmTask<object> grind_results()
    {
        await DmTask.CompletedTask;
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_nutriment_fat_oil), 3), Dm.kv(typeof(datum_reagent_consumable_caramel), 2));
    }
}

// tg: code/modules/food_and_drinks/food/snacks.dm:640
public partial class obj_item_food_sticko_random : obj_item_food_sticko
{
    // tg: code/modules/food_and_drinks/food/snacks.dm:644
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object random_flavour = pick(Dm.sub(subtypesof(typeof(obj_item_food_sticko)), typeof(obj_item_food_sticko_random)));
        obj_item_food sample = Dm.@new(random_flavour as Type, loc) as obj_item_food;
        name = sample.name;
        desc = sample.desc;
        food_reagents = sample.food_reagents;
        tastes = sample.tastes;

        qdel(sample);

        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/food_and_drinks/food/snacks.dm:696
public partial class obj_item_food_shok_roks_random : obj_item_food_shok_roks
{
    // tg: code/modules/food_and_drinks/food/snacks.dm:700
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object random_flavour = pick(Dm.sub(subtypesof(typeof(obj_item_food_shok_roks)), typeof(obj_item_food_shok_roks_random)));
        obj_item_food sample = Dm.@new(random_flavour as Type, loc) as obj_item_food;
        name = sample.name;
        desc = sample.desc;
        food_reagents = sample.food_reagents;
        tastes = sample.tastes;

        qdel(sample);

        return base.Initialize(mapload, __extra);
    }
}
