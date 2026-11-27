// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fish/types/mining.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the mining fish (the chasm crab that grows into a lobstrosity, its ice-moon kin, the bone fish and the
// lavaloop with its plasma-river form, thrown like a boomerang). The var blocks are generated (D111).

// tg: code/modules/fishing/fish/types/mining.dm:2
public partial class obj_item_fish_chasm_crab : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/mining.dm:34
    [DmParams("mapload", "apply_qualities")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_FISH_BEFORE_GROWING, nameof(growth_checks));
        RegisterSignal(this, COMSIG_FISH_FINISH_GROWING, nameof(on_growth));
        return dot;
    }

    // tg: code/modules/fishing/fish/types/mining.dm:39
    public override object get_fish_taste()
    {
        return Dm.list(Dm.kv("raw crab", 2));
    }

    // tg: code/modules/fishing/fish/types/mining.dm:42
    public override object get_fish_taste_cooked()
    {
        return Dm.list(Dm.kv("cooked crab", 2));
    }

    ///A chasm crab growth speed is determined by its initial weight and size, ergo bigger crabs for faster lobstrosities
    // tg: code/modules/fishing/fish/types/mining.dm:46
    public override async DmTask update_size_and_weight(object new_size = null, object new_weight = null, float? update_materials = null)
    {
        new_size ??= average_size;
        new_weight ??= average_weight;
        update_materials ??= TRUE;
        await base.update_size_and_weight(new_size, new_weight, update_materials);
        float multiplier = 1;
        float fish_size = Dm.num(size);
        // DM's switch ranges: the first range that holds the value wins (the tiny-to-small range adds nothing, as in tg).
        if (fish_size >= 0 && fish_size <= FISH_SIZE_TINY_MAX)
            multiplier -= 0.2f;
        else if (fish_size >= FISH_SIZE_SMALL_MAX && fish_size <= FISH_SIZE_NORMAL_MAX)
            multiplier += 0.2f;
        else if (fish_size >= FISH_SIZE_NORMAL_MAX && fish_size <= FISH_SIZE_BULKY_MAX)
            multiplier += 0.5f;
        else if (fish_size >= FISH_SIZE_BULKY_MAX)
            multiplier += 0.8f;

        float fish_weight = Dm.num(weight);
        if (fish_weight <= (average_weight - 200))
            multiplier -= 0.1f * round((average_weight - fish_weight) / 200);
        else if (fish_weight >= (average_weight + 500))
            multiplier += min(0.1f * round((fish_weight - average_weight) / 500), 2);
        await AddComponent(typeof(datum_component_fish_growth), lob_type, 10 * MINUTES * multiplier);
    }

    // tg: code/modules/fishing/fish/types/mining.dm:66
    public override async DmTask<object> pet_fish(mob_living user = null, object in_aquarium = null)
    {
        object dot = await base.pet_fish(user, in_aquarium);
        if (Dm.truthy(dot))
            anger -= min(anger, 6.5f);
        return dot;
    }

    // tg: code/modules/fishing/fish/types/mining.dm:71
    [SignalHandler]
    public virtual object growth_checks(datum source = null, object seconds_per_tick = null, object growth = null, object result_path = null)
    {
        float hunger = Dm.num(get_hunger());
        if (Dm.num(get_health_percentage()) <= 0.6f || hunger >= 0.6f) //if too hurt or hungry, don't grow.
        {
            anger += Dm.num(growth) * 2;
            return COMPONENT_DONT_GROW;
        }

        if (hunger >= 0.4f) //I'm hungry and angry
            anger += Dm.num(growth) * 0.6f;

        if (loc == null || !HAS_TRAIT(loc, TRAIT_IS_AQUARIUM))
            return null;

        if (HAS_TRAIT(loc, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH)) //the aquarium has breeding disabled
            return COMPONENT_DONT_GROW;
        if (Dm.locate<obj_item_aquarium_prop>(loc) == null) //the aquarium deco is quite barren
            anger += Dm.num(growth) * 0.25f;
        float fish_count = length(get_aquarium_fishes());
        if (!ISINRANGE(fish_count, 3, AQUARIUM_MAX_BREEDING_POPULATION * 0.5f)) //too lonely or overcrowded
            anger += Dm.num(growth) * 0.3f;
        if (fish_count > AQUARIUM_MAX_BREEDING_POPULATION * 0.5f) //check if there's enough room to maturate.
            return COMPONENT_DONT_GROW;
        return null;
    }

    // tg: code/modules/fishing/fish/types/mining.dm:94
    [SignalHandler]
    public virtual void on_growth(datum source = null, mob_living_basic_mining_lobstrosity_juvenile result = null)
    {
        if (Dm.@in(typeof(datum_fish_trait_territorial), fish_traits))
            return;
        if (!prob(anger))
        {
            qdel(result.ai_controller);
            result.ai_controller = Dm.@new<datum_ai_controller_basic_controller_lobstrosity_juvenile_calm>(result);
        }
        else if (anger < 30) //not really that mad, just a bit unstable.
        {
            qdel(result.ai_controller);
            result.ai_controller = Dm.@new<datum_ai_controller_basic_controller_lobstrosity_juvenile_capricious>(result);
        }
    }
}

// tg: code/modules/fishing/fish/types/mining.dm:117
public partial class obj_item_fish_boned : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/mining.dm:139
    [DmParams("mapload", "apply_qualities")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_FISH_MADE_OF_BONE, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/fishing/fish/types/mining.dm:143
    public override object fish_grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_bone_dust), 10));
    }

    // tg: code/modules/fishing/fish/types/mining.dm:146
    [DmParams("weight_val")]
    public override async DmTask make_edible(params object[] __extra)
    {
        await DmTask.CompletedTask; //it's all bones and no meat.
    }

    // tg: code/modules/fishing/fish/types/mining.dm:149
    public override async DmTask<object> get_health_warnings(mob user = null, float? always_deep = null)
    {
        await DmTask.CompletedTask;
        return Dm.list(span_deadsay(I18n.T("It's bones.")));
    }

    // tg: code/modules/fishing/fish/types/mining.dm:152
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] swallows [] whole! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        await forceMove(user);
        _addtimer(Dm.@new<datum_callback>(this, nameof(skeleton_appears), user), 2 * SECONDS, file: "code/modules/fishing/fish/types/mining.dm", line: 155);
        return MANUAL_SUICIDE_NONLETHAL; // chance not to die
    }

    // tg: code/modules/fishing/fish/types/mining.dm:158
    public virtual async DmTask skeleton_appears(mob_living user = null)
    {
        user.visible_message(span_warning(I18n.T("[]'s skin melts off!", user)), span_boldwarning(I18n.T("Your skin melts off!")));
        await user.spawn_gibs();
        await user.drop_everything(del_on_drop: FALSE, force: FALSE, del_if_nodrop: FALSE);
        user.set_species(typeof(datum_species_skeleton));
        await user.say("AAAAAAAAAAAAHHHHHHHHHH!!!!!!!!!!!!!!", forced: "bone fish suicide");
        if (prob(90))
        {
            _addtimer(Dm.@new<datum_callback>(user, nameof(mob_living.death)), 3 * SECONDS, file: "code/modules/fishing/fish/types/mining.dm", line: 165);
            user.set_suicide(TRUE);
        }
        qdel(this);
    }
}

// tg: code/modules/fishing/fish/types/mining.dm:169
public partial class obj_item_fish_lavaloop : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/mining.dm:197
    [DmParams("mapload", "apply_qualities")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_traits(Dm.list(TRAIT_FISHING_BAIT, TRAIT_GOOD_QUALITY_BAIT, TRAIT_BYPASS_RANGED_ARMOR), INNATE_TRAIT);
        Waiting.NoSleep(AddComponent(typeof(datum_component_boomerang), throw_range, TRUE));
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_throwbonus_on_windup),
            Dm.named("maximum_bonus", maximum_bonus),
            Dm.named("windup_increment_speed", 2),
            Dm.named("throw_text", I18n.T("starts cooking in your hands, it may explode soon!")),
            Dm.named("pass_maximum_callback", Dm.@new<datum_callback>(this, nameof(explode_on_user))),
            Dm.named("apply_bonus_callback", Dm.@new<datum_callback>(this, nameof(on_fish_land))),
            Dm.named("sound_on_success", Dm.resource("sound/items/weapons/parry.ogg")),
            Dm.named("effect_on_success", typeof(obj_effect_temp_visual_guardian_phase))));
        Waiting.NoSleep(AddElement(typeof(datum_element_raptor_food), Dm.named("attack_modifier", 1.5f), Dm.named("growth_modifier", -0.075f)));
        return dot;
    }

    // tg: code/modules/fishing/fish/types/mining.dm:213
    public override object get_fish_taste()
    {
        return Dm.list(Dm.kv("chewy fish", 2));
    }

    // tg: code/modules/fishing/fish/types/mining.dm:216
    public override object get_food_types()
    {
        return Dm.bitor(Dm.bitor(SEAFOOD, MEAT), GORE); //Well-cooked in lava/plasma
    }

    // tg: code/modules/fishing/fish/types/mining.dm:219
    public virtual async DmTask explode_on_user(mob_living user = null)
    {
        obj_item_bodypart_arm active_arm = user.get_active_hand() as obj_item_bodypart_arm;
        if (active_arm != null)
            await active_arm.dismember();
        to_chat(user, span_warning(I18n.T("[] explodes!", this)));
        playsound(this, Dm.resource("sound/effects/explosion/explosion1.ogg"), 40, TRUE);
        await user.flash_act(1, 1);
        qdel(this);
    }

    // tg: code/modules/fishing/fish/types/mining.dm:227
    public virtual async DmTask<object> on_fish_land(mob_living target = null, object bonus_value = null)
    {
        await DmTask.CompletedTask;
        if (target is not mob_living)
            return FALSE;
        return target.mob_size >= MOB_SIZE_LARGE;
    }
}

// tg: code/modules/fishing/fish/types/mining.dm:232
public partial class obj_item_fish_lavaloop_plasma_river : obj_item_fish_lavaloop
{
    // tg: code/modules/fishing/fish/types/mining.dm:244
    public override async DmTask explode_on_user(mob_living user = null)
    {
        playsound(this, Dm.resource("sound/effects/explosion/explosion1.ogg"), 40, TRUE);
        await user.flash_act(1, 1);
        await user.apply_status_effect(typeof(datum_status_effect_ice_block_talisman), 5 * SECONDS);
        qdel(this);
    }

    // tg: code/modules/fishing/fish/types/mining.dm:250
    public override async DmTask<object> on_fish_land(mob_living target = null, object bonus_value = null)
    {
        if (target is not mob_living)
            return FALSE;
        if (target.mob_size < MOB_SIZE_LARGE)
            return FALSE;
        float freeze_timer = (Dm.num(bonus_value) * 0.1f);
        if (freeze_timer <= 0)
            return FALSE;
        await target.apply_status_effect(typeof(datum_status_effect_ice_block_talisman), freeze_timer * SECONDS);
        return FALSE;
    }
}
