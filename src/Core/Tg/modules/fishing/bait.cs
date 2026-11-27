// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/bait.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the cans of bait and worms, and the fishing lures, which never get eaten, have to be spun at intervals while
// fishing and each catch only the fish they were made for. The var blocks are generated (D111).

// tg: code/modules/fishing/bait.dm:1
public partial class obj_item_bait_can : obj_item
{
    // tg: code/modules/fishing/bait.dm:17
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        obj_item fresh_bait = retrieve_bait(user) as obj_item;
        if (fresh_bait != null)
            await user.put_in_hands(fresh_bait);
        return dot;
    }

    // tg: code/modules/fishing/bait.dm:23
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_info(I18n.T("It[].", uses_left != 0 ? I18n.T(" has got [] [] left", uses_left, Dm.initial(bait_type, (atom p) => p.name)) : I18n.T("'s empty"))));
        return dot;
    }

    // tg: code/modules/fishing/bait.dm:27
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = base_icon_state as string;
        if (uses_left <= Dm.num(Dm.initial(this, (obj_item_bait_can p) => p.uses_left)))
        {
            if (uses_left == 0)
                icon_state = Dm.text("[]_empty", icon_state);
            else
                icon_state = Dm.text("[]_open", icon_state);
        }
        return dot;
    }

    // tg: code/modules/fishing/bait.dm:36
    public virtual object retrieve_bait(mob user = null)
    {
        if (uses_left == 0)
        {
            user.balloon_alert(user, I18n.T("empty"));
            return null;
        }
        // COOLDOWN_FINISHED(src, bait_removal_cooldown) (__DEFINES/cooldowns.dm:113), inline (rules 11.1)
        if (!(bait_removal_cooldown < world.time))
        {
            user.balloon_alert(user, I18n.T("wait a bit"));
            return null;
        }
        bait_removal_cooldown = world.time + Dm.num(cooldown_time); // COOLDOWN_START(src, bait_removal_cooldown, cooldown_time), inline
        update_appearance();
        uses_left--;
        return Dm.@new(bait_type, this);
    }
}

// tg: code/modules/fishing/bait.dm:65
public partial class obj_item_fishing_lure : obj_item
{
    // tg: code/modules/fishing/bait.dm:76
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_traits(Dm.list(TRAIT_FISHING_BAIT, TRAIT_BAIT_ALLOW_FISHING_DUD, TRAIT_OMNI_BAIT, TRAIT_BAIT_UNCONSUMABLE), INNATE_TRAIT);
        RegisterSignal(this, COMSIG_ITEM_FISHING_ROD_SLOTTED, nameof(on_fishing_rod_slotted));
        RegisterSignal(this, COMSIG_ITEM_FISHING_ROD_UNSLOTTED, nameof(on_fishing_rod_unslotted));
        return dot;
    }

    // tg: code/modules/fishing/bait.dm:82
    [SignalHandler]
    public virtual void on_fishing_rod_slotted(datum source = null, obj_item_fishing_rod rod = null, object slot = null)
    {
        rod.spin_frequency = spin_frequency;
    }

    // tg: code/modules/fishing/bait.dm:86
    [SignalHandler]
    public virtual void on_fishing_rod_unslotted(datum source = null, obj_item_fishing_rod rod = null, object slot = null)
    {
        rod.spin_frequency = null;
    }

    ///Called for every fish subtype by the fishing subsystem when initializing, to populate the list of fish that can be catched with this lure.
    // tg: code/modules/fishing/bait.dm:91
    public virtual object is_catchable_fish(obj_item_fish fish = null, DmList fish_properties = null)
    {
        return TRUE;
    }

    // tg: code/modules/fishing/bait.dm:94
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_info(I18n.T("It has to be spun with a frequency of [] to [] seconds while fishing.", Dm.num(spin_frequency[1]) * 0.1f, Dm.num(spin_frequency[2]) * 0.1f)));
        if (HAS_MIND_TRAIT(user, TRAIT_EXAMINE_FISHING_SPOT))
            Dm.append(ref dot, span_tinynotice(I18n.T("Thanks to your experience, you can examine it again to get a list of fish you can catch with it.")));
        return dot;
    }

    // tg: code/modules/fishing/bait.dm:100
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        if (!HAS_MIND_TRAIT(user, TRAIT_EXAMINE_FISHING_SPOT))
            return dot;

        DmList known_fishes = Dm.list();
        foreach (Type fish_type in Dm.each<Type>(SSfishing.lure_catchables[type]))
        {
            if (Dm.truthy(Dm.bitand(Dm.initial(fish_type, (obj_item_fish p) => p.fish_flags), FISH_FLAG_SHOW_IN_CATALOG)))
                Dm.append(ref known_fishes, Dm.initial(fish_type, (atom p) => p.name));
        }

        if (!Dm.truthy(length(known_fishes)))
            return dot;

        Dm.append(ref dot, span_info(I18n.T("You can catch the following fish with this lure: [].", english_list(known_fishes))));
        return dot;
    }

    ///Check if the fish is in the list of catchable fish for this fishing lure. Return value is a multiplier.
    // tg: code/modules/fishing/bait.dm:116
    public override object check_bait(object fish = null)
    {
        float multiplier = 0;
        bool is_instance = istype(fish, typeof(obj_item_fish));
        DmList fish_properties = SSfishing.fish_properties[is_instance ? ((datum)fish).type : fish] as DmList;
        // tg: if(is_type_in_list(/obj/item/fishing_lure, fish_properties[FISH_PROPERTIES_FAV_BAIT])) multiplier += 2
        // is_type_in_list() runs istype() on its first argument, which is false for a typepath, so tg never adds this bonus;
        // kept as tg plays it.
        if (is_instance)
        {
            if (Dm.truthy(is_catchable_fish((obj_item_fish)fish, fish_properties)))
                multiplier += 10;
        }
        else if (Dm.@in(fish, SSfishing.lure_catchables[type]))
            multiplier += 10;
        return multiplier;
    }
}

// tg: code/modules/fishing/bait.dm:130
public partial class obj_item_fishing_lure_minnow : obj_item_fishing_lure
{
    // tg: code/modules/fishing/bait.dm:135
    public override object is_catchable_fish(obj_item_fish fish = null, DmList fish_properties = null)
    {
        float intermediate_size = FISH_SIZE_SMALL_MAX + (FISH_SIZE_NORMAL_MAX - FISH_SIZE_SMALL_MAX);
        if (!ISINRANGE(Dm.num(fish.size), FISH_SIZE_TINY_MAX * 0.5f, intermediate_size))
            return FALSE;
        if (Dm.truthy(length(Dm.bitand(Dm.list(typeof(datum_fish_trait_vegan), typeof(datum_fish_trait_picky_eater), typeof(datum_fish_trait_nocturnal), typeof(datum_fish_trait_heavy)), fish.fish_traits))))
            return FALSE;
        return TRUE;
    }
}

// tg: code/modules/fishing/bait.dm:143
public partial class obj_item_fishing_lure_plug : obj_item_fishing_lure
{
    // tg: code/modules/fishing/bait.dm:148
    public override object is_catchable_fish(obj_item_fish fish = null, DmList fish_properties = null)
    {
        if (Dm.num(fish.size) <= FISH_SIZE_SMALL_MAX)
            return FALSE;
        if (Dm.truthy(length(Dm.bitand(Dm.list(typeof(datum_fish_trait_vegan), typeof(datum_fish_trait_picky_eater), typeof(datum_fish_trait_nocturnal), typeof(datum_fish_trait_heavy)), fish.fish_traits))))
            return FALSE;
        return TRUE;
    }
}

// tg: code/modules/fishing/bait.dm:155
public partial class obj_item_fishing_lure_dropping : obj_item_fishing_lure
{
    // tg: code/modules/fishing/bait.dm:161
    public override object is_catchable_fish(obj_item_fish fish = null, DmList fish_properties = null)
    {
        DmList sources = Dm.list(typeof(datum_fish_source_toilet), typeof(datum_fish_source_moisture_trap));
        // tg reads the toilet's table for each of the two sources (:163); kept as tg has it.
        foreach (Type source in Dm.each<Type>(sources))
        {
            datum_fish_source instance = GLOB.preset_fish_sources[typeof(datum_fish_source_toilet)] as datum_fish_source;
            if (Dm.@in(fish.type, instance.fish_table))
                return TRUE;
        }
        DmList fav_baits = fish_properties[FISH_PROPERTIES_FAV_BAIT] as DmList;
        foreach (DmList identifier in Dm.each<DmList>(fav_baits))
        {
            if (Dm.eq(identifier[FISH_BAIT_TYPE], FISH_BAIT_FOODTYPE) && Dm.truthy(Dm.bitand(identifier[FISH_BAIT_VALUE], Dm.bitor(Dm.bitor(JUNKFOOD, GROSS), TOXIC))))
                return TRUE;
        }
        if (Dm.num(fish.beauty) <= FISH_BEAUTY_DISGUSTING)
            return TRUE;
        return FALSE;
    }
}

// tg: code/modules/fishing/bait.dm:176
public partial class obj_item_fishing_lure_spoon : obj_item_fishing_lure
{
    // tg: code/modules/fishing/bait.dm:182
    public override object is_catchable_fish(obj_item_fish fish = null, DmList fish_properties = null)
    {
        if (!ISINRANGE(Dm.num(fish.size), FISH_SIZE_TINY_MAX + 1, FISH_SIZE_NORMAL_MAX))
            return FALSE;
        if (Dm.truthy(length(Dm.bitand(Dm.list(typeof(datum_fish_trait_vegan), typeof(datum_fish_trait_picky_eater), typeof(datum_fish_trait_nocturnal), typeof(datum_fish_trait_heavy)), fish.fish_traits))))
            return FALSE;
        object fluid_type = fish.required_fluid_type;
        if (Dm.eq(fluid_type, AQUARIUM_FLUID_FRESHWATER) || Dm.eq(fluid_type, AQUARIUM_FLUID_ANADROMOUS) || Dm.eq(fluid_type, AQUARIUM_FLUID_ANY_WATER))
            return TRUE;
        if (Dm.@in(typeof(datum_fish_trait_amphibious), fish.fish_traits) && Dm.eq(fluid_type, AQUARIUM_FLUID_AIR))
            return TRUE;
        return FALSE;
    }
}

// tg: code/modules/fishing/bait.dm:194
public partial class obj_item_fishing_lure_artificial_fly : obj_item_fishing_lure
{
    // tg: code/modules/fishing/bait.dm:200
    public override object is_catchable_fish(obj_item_fish fish = null, DmList fish_properties = null)
    {
        if (Dm.@in(typeof(datum_fish_trait_picky_eater), fish.fish_traits))
            return TRUE;
        return FALSE;
    }
}

// tg: code/modules/fishing/bait.dm:205
public partial class obj_item_fishing_lure_led : obj_item_fishing_lure
{
    // tg: code/modules/fishing/bait.dm:211
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/modules/fishing/bait.dm:215
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        // DEVIATION(DV12): . += emissive_appearance(icon, "led_emissive", src), the LED's glow on the emissive plane
        return dot;
    }

    // tg: code/modules/fishing/bait.dm:219
    [DmParams("rod", "slot")]
    public override void on_fishing_rod_slotted(datum source = null, obj_item_fishing_rod rod = null, object slot = null)
    {
        // tg declares this override as (rod, slot), so the rod arrives where the parent's signal handler has the source
        // (:219): the rod here is the lure itself and the trait goes on the lure, as in tg.
        base.on_fishing_rod_slotted(source, rod, slot);
        ADD_TRAIT(source, TRAIT_ROD_IGNORE_ENVIRONMENT, type);
    }

    // tg: code/modules/fishing/bait.dm:223
    [DmParams("rod", "slot")]
    public override void on_fishing_rod_unslotted(datum source = null, obj_item_fishing_rod rod = null, object slot = null)
    {
        base.on_fishing_rod_unslotted(source, rod, slot);
        REMOVE_TRAIT(source, TRAIT_ROD_IGNORE_ENVIRONMENT, type);
    }

    // tg: code/modules/fishing/bait.dm:227
    public override object is_catchable_fish(obj_item_fish fish = null, DmList fish_properties = null)
    {
        if (Dm.truthy(length(Dm.bitand(Dm.list(typeof(datum_fish_trait_nocturnal), typeof(datum_fish_trait_heavy)), fish.fish_traits))))
            return TRUE;
        return FALSE;
    }
}

// tg: code/modules/fishing/bait.dm:232
public partial class obj_item_fishing_lure_lucky_coin : obj_item_fishing_lure
{
    // tg: code/modules/fishing/bait.dm:238
    [DmParams("rod", "slot")]
    public override void on_fishing_rod_slotted(datum source = null, obj_item_fishing_rod rod = null, object slot = null)
    {
        // As the LED lure's (:219): tg's override names the signal's source "rod", so the trait goes on the lure.
        base.on_fishing_rod_slotted(source, rod, slot);
        ADD_TRAIT(source, TRAIT_ROD_ATTRACT_SHINY_LOVERS, REF(this));
    }

    // tg: code/modules/fishing/bait.dm:242
    [DmParams("rod", "slot")]
    public override void on_fishing_rod_unslotted(datum source = null, obj_item_fishing_rod rod = null, object slot = null)
    {
        base.on_fishing_rod_unslotted(source, rod, slot);
        REMOVE_TRAIT(source, TRAIT_ROD_ATTRACT_SHINY_LOVERS, REF(this));
    }

    // tg: code/modules/fishing/bait.dm:246
    public override object is_catchable_fish(obj_item_fish fish = null, DmList fish_properties = null)
    {
        if (Dm.@in(typeof(datum_fish_trait_shiny_lover), fish.fish_traits))
            return TRUE;
        return FALSE;
    }
}

// tg: code/modules/fishing/bait.dm:251
public partial class obj_item_fishing_lure_algae : obj_item_fishing_lure
{
    // tg: code/modules/fishing/bait.dm:257
    public override object is_catchable_fish(obj_item_fish fish = null, DmList fish_properties = null)
    {
        if (Dm.@in(typeof(datum_fish_trait_vegan), fish.fish_traits))
            return TRUE;
        return FALSE;
    }
}

// tg: code/modules/fishing/bait.dm:262
public partial class obj_item_fishing_lure_grub : obj_item_fishing_lure
{
    // tg: code/modules/fishing/bait.dm:268
    public override object is_catchable_fish(obj_item_fish fish = null, DmList fish_properties = null)
    {
        if (Dm.num(fish.size) >= FISH_SIZE_SMALL_MAX)
            return FALSE;
        if (Dm.truthy(length(Dm.bitand(Dm.list(typeof(datum_fish_trait_vegan), typeof(datum_fish_trait_picky_eater)), fish.fish_traits))))
            return FALSE;
        return TRUE;
    }
}

// tg: code/modules/fishing/bait.dm:275
public partial class obj_item_fishing_lure_buzzbait : obj_item_fishing_lure
{
    // tg: code/modules/fishing/bait.dm:281
    public override object is_catchable_fish(obj_item_fish fish = null, DmList fish_properties = null)
    {
        if (HAS_TRAIT(fish, TRAIT_FISH_ELECTROGENESIS))
            return TRUE;
        return FALSE;
    }
}

// tg: code/modules/fishing/bait.dm:286
public partial class obj_item_fishing_lure_spinnerbait : obj_item_fishing_lure
{
    // tg: code/modules/fishing/bait.dm:292
    public override object is_catchable_fish(obj_item_fish fish = null, DmList fish_properties = null)
    {
        if (!Dm.@in(typeof(datum_fish_trait_predator), fish.fish_traits))
            return FALSE;
        object init_fluid_type = fish.required_fluid_type;
        if (Dm.eq(init_fluid_type, AQUARIUM_FLUID_FRESHWATER) || Dm.eq(init_fluid_type, AQUARIUM_FLUID_ANADROMOUS) || Dm.eq(init_fluid_type, AQUARIUM_FLUID_ANY_WATER))
            return TRUE;
        if (Dm.@in(typeof(datum_fish_trait_amphibious), fish.fish_traits) && Dm.eq(init_fluid_type, AQUARIUM_FLUID_AIR)) //fluid type is changed to freshwater on init
            return TRUE;
        return FALSE;
    }
}

// tg: code/modules/fishing/bait.dm:302
public partial class obj_item_fishing_lure_daisy_chain : obj_item_fishing_lure
{
    // tg: code/modules/fishing/bait.dm:308
    public override object is_catchable_fish(obj_item_fish fish = null, DmList fish_properties = null)
    {
        if (!Dm.@in(typeof(datum_fish_trait_predator), fish.fish_traits))
            return FALSE;
        object init_fluid_type = fish.required_fluid_type;
        if (Dm.eq(init_fluid_type, AQUARIUM_FLUID_SALTWATER) || Dm.eq(init_fluid_type, AQUARIUM_FLUID_ANADROMOUS) || Dm.eq(init_fluid_type, AQUARIUM_FLUID_ANY_WATER))
            return TRUE;
        return FALSE;
    }
}
