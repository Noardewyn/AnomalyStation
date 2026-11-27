// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fish/types/freshwater.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the freshwater fish (goldfish and McGill, the three-eyed goldfish, angelfish, guppy, plasma tetra, catfish,
// the anxious zipzap, the tadpole that grows into a frog, perch and sacabambaspis). The var blocks are generated (D111).

// tg: code/modules/fishing/fish/types/freshwater.dm:1
public partial class obj_item_fish_goldfish : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/freshwater.dm:21
    [DmParams("mapload", "apply_qualities")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_traits(Dm.list(TRAIT_FISHING_BAIT, TRAIT_GOOD_QUALITY_BAIT), INNATE_TRAIT);
        return dot;
    }
}

// tg: code/modules/fishing/fish/types/freshwater.dm:25
public partial class obj_item_fish_goldfish_gill : obj_item_fish_goldfish
{
    // tg: code/modules/fishing/fish/types/freshwater.dm:36
    public override object get_fish_taste()
    {
        return Dm.list(Dm.kv("raw fish", 2.5f), Dm.kv("objection", 1));
    }
}

// tg: code/modules/fishing/fish/types/freshwater.dm:39
public partial class obj_item_fish_goldfish_three_eyes : obj_item_fish_goldfish
{
    // tg: code/modules/fishing/fish/types/freshwater.dm:59
    public override object get_fish_taste()
    {
        return Dm.list(Dm.kv("raw fish", 2.5f), Dm.kv("chemical waste", 0.5f));
    }
}

// tg: code/modules/fishing/fish/types/freshwater.dm:62
public partial class obj_item_fish_goldfish_three_eyes_gill : obj_item_fish_goldfish_three_eyes
{
    // tg: code/modules/fishing/fish/types/freshwater.dm:72
    public override object get_fish_taste()
    {
        return Dm.list(Dm.kv("raw fish", 2.5f), Dm.kv("objection", 1));
    }
}

// tg: code/modules/fishing/fish/types/freshwater.dm:104
public partial class obj_item_fish_plasmatetra : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/freshwater.dm:117
    [DmParams("mapload", "apply_qualities")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_traits(Dm.list(TRAIT_FISHING_BAIT, TRAIT_GOOD_QUALITY_BAIT), INNATE_TRAIT);
        return dot;
    }
}

// tg: code/modules/fishing/fish/types/freshwater.dm:142
public partial class obj_item_fish_zipzap : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/freshwater.dm:168
    public override object get_fish_taste()
    {
        return Dm.list(Dm.kv("raw fish", 2), Dm.kv("anxiety", 1));
    }

    // tg: code/modules/fishing/fish/types/freshwater.dm:171
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (!Dm.truthy(await electrocute_mob(user as mob_living_carbon, power_source: get_area(this), source: this, siemens_coeff: 1, dist_check: FALSE)))
        {
            user.visible_message(span_suicide(I18n.T("[] tries to slap []self with [], but they're immune to electricity!", user, user.p_them(), this)));
            return SHAME;
        }
        return await base.suicide_act(user);
    }

    // real suicide handled by og fish proc
    // tg: code/modules/fishing/fish/types/freshwater.dm:178
    public override async DmTask slapperoni(mob_living user = null, object iteration = null)
    {
        await electrocute_mob(user as mob_living_carbon, power_source: get_area(this), source: this, siemens_coeff: 1, dist_check: FALSE); // how do i make this use electrogenesis_power
        await base.slapperoni(user, iteration);
    }
}

// tg: code/modules/fishing/fish/types/freshwater.dm:182
public partial class obj_item_fish_tadpole : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/freshwater.dm:202
    [DmParams("mapload", "apply_qualities")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Type output_path = prob(99) ? typeof(mob_living_basic_frog) : typeof(mob_living_basic_frog_rare);
        Waiting.NoSleep(AddComponent(typeof(datum_component_fish_growth), output_path, rand(2 * MINUTES, 3 * MINUTES)));
        RegisterSignal(this, COMSIG_FISH_BEFORE_GROWING, nameof(growth_checks));
        RegisterSignal(this, COMSIG_FISH_FINISH_GROWING, nameof(on_growth));
        return dot;
    }

    // tg: code/modules/fishing/fish/types/freshwater.dm:209
    [DmParams()]
    public override async DmTask make_edible(params object[] __extra)
    {
        await DmTask.CompletedTask;
    }

    // tg: code/modules/fishing/fish/types/freshwater.dm:212
    public override async DmTask set_status(object new_status = null, float? silent = null)
    {
        await base.set_status(new_status, silent);
        if (status == FISH_DEAD)
        {
            if (loc is not obj_structure_fish_mount)
                // QDEL_IN_STOPPABLE(src, 12 SECONDS), inline (rules 11.1 (f))
                del_timerid = _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), this), 12 * SECONDS, TIMER_STOPPABLE, file: "code/modules/fishing/fish/types/freshwater.dm", line: 216);
        }
        else
            deltimer(del_timerid);
    }

    // tg: code/modules/fishing/fish/types/freshwater.dm:220
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (QDELETED(this) || status != FISH_DEAD || old_loc is not obj_structure_fish_mount)
            return dot;
        qdel(this);
        return dot;
    }

    // tg: code/modules/fishing/fish/types/freshwater.dm:226
    [SignalHandler]
    public virtual object growth_checks(datum source = null, object seconds_per_tick = null, object growth = null, object result_path = null)
    {
        float hunger = Dm.num(get_hunger());
        if (hunger >= 0.7f) //too hungry to grow
            return COMPONENT_DONT_GROW;
        if (HAS_TRAIT(loc, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH)) //the aquarium has breeding disabled
            return COMPONENT_DONT_GROW;
        return null;
    }

    // tg: code/modules/fishing/fish/types/freshwater.dm:234
    [SignalHandler]
    public virtual void on_growth(datum source = null, mob_living_basic_frog result = null)
    {
        playsound(result, result.attack_sound, 50, TRUE); // reeeeeeeeeeeeeee...
    }

    // tg: code/modules/fishing/fish/types/freshwater.dm:238
    [DmParams("price", "elasticity_percent")]
    public override object get_export_price(object price = null, params object[] __extra)
    {
        return 2; //two credits. Tadpoles aren't really that valueable.
    }

    // tg: code/modules/fishing/fish/types/freshwater.dm:241
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] swallows [] whole!", user, this)));
        await forceMove(user);
        if (status == FISH_DEAD)
        {
            await user.emote("gasp");
            user.visible_message(span_suicide(I18n.T("[] croaks!", user)));
            return OXYLOSS;
        }

        // the frogg grows
        _addtimer(Dm.@new<datum_callback>(this, nameof(gestation), user), 5 * SECONDS, file: "code/modules/fishing/fish/types/freshwater.dm", line: 250);
        return MANUAL_SUICIDE;
    }

    // tg: code/modules/fishing/fish/types/freshwater.dm:253
    public virtual async DmTask gestation(mob_living user = null)
    {
        if (QDELETED(user) || QDELETED(this))
            return;
        user.visible_message(span_suicide(I18n.T("A live frog bursts out of []!", user)));
        Dm.@new<obj_effect_spawner_random_frog>(user.drop_location());

        obj_item_bodypart chest = user.get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart;

        if (chest != null)
        {
            await chest.dismember();
            await user.death();
        }
        else
            await user.gib(DROP_ALL_REMAINS);

        qdel(this);
    }
}

///Memetic fish from a paleontologically inaccurate, goofy replica of a specimen. Sells decently for its size.
// tg: code/modules/fishing/fish/types/freshwater.dm:294
public partial class obj_item_fish_sacabambaspis : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/freshwater.dm:309
    [DmParams("price", "percent")]
    public override object get_export_price(object price = null, params object[] __extra)
    {
        return Dm.num(base.get_export_price(price, __extra)) * 4.5f;
    }
}
