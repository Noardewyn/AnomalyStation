// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/aquarium/aquarium.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the room aquarium (its glass, water and borders drawn over the fish, the panel, wrenching, the glass broken by
// damage that spills the fish and the water, and two glass sheets to fix it), the prefilled one, and the portable fish tank that
// limits the size of its fish, grows heavier and slower with them, and the lawyer's tank with its goldfish. The var blocks are
// generated (D111).

// tg: code/modules/fishing/aquarium/aquarium.dm:1
public partial class obj_structure_aquarium : obj_structure
{
    // tg: code/modules/fishing/aquarium/aquarium.dm:29
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_aquarium), aquarium_zone_min_pw, aquarium_zone_max_pw, aquarium_zone_min_pz, aquarium_zone_max_pz, default_beauty, Dm.named("init_mode", init_mode)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_aquarium)));
        RegisterSignal(this, COMSIG_AQUARIUM_FLUID_CHANGED, nameof(on_aquarium_liquid_changed));
        update_appearance();
        return dot;
    }

    // tg: code/modules/fishing/aquarium/aquarium.dm:36
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = await base.update_icon(updates, __extra);
        ///"aquarium_map" is used for mapping, so mappers can tell what it's.
        icon_state = base_icon_state + "_base";
        return dot;
    }

    // tg: code/modules/fishing/aquarium/aquarium.dm:41
    [SignalHandler]
    public virtual void on_aquarium_liquid_changed(datum source = null, object fluid_type = null)
    {
        this.fluid_type = Dm.str(fluid_type);
        update_appearance();
    }

    // tg: code/modules/fishing/aquarium/aquarium.dm:46
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (HAS_TRAIT(this, TRAIT_AQUARIUM_PANEL_OPEN))
            Dm.append(ref dot, base_icon_state + "_panel");

        string icon_suffix = fluid_type == AQUARIUM_FLUID_AIR ? "air" : "water";
        ///The glass overlay
        if (Dm.truthy(broken))
        {
            icon_suffix += "_broken";
            Dm.append(ref dot, mutable_appearance(icon, base_icon_state + "_glass_cracks", layer: layer + AQUARIUM_BORDERS_LAYER));
        }
        Dm.append(ref dot, mutable_appearance(icon, base_icon_state + "_glass_" + icon_suffix, layer: layer + AQUARIUM_GLASS_LAYER));
        Dm.append(ref dot, mutable_appearance(icon, base_icon_state + "_borders", layer: layer + AQUARIUM_BORDERS_LAYER));
        return dot;
    }

    // tg: code/modules/fishing/aquarium/aquarium.dm:59
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/fishing/aquarium/aquarium.dm:64
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_stack_sheet_glass)))
            return null;
        if (!Dm.truthy(broken))
        {
            balloon_alert(user, I18n.T("aquarium not broken!"));
            return ITEM_INTERACT_BLOCKING;
        }
        obj_item_stack_sheet_glass glass = (obj_item_stack_sheet_glass)tool;
        if (Dm.num(glass.get_amount()) < 2)
        {
            balloon_alert(user, I18n.T("it needs two sheets!"));
            return ITEM_INTERACT_BLOCKING;
        }
        balloon_alert(user, I18n.T("fixing the aquarium..."));
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
            return ITEM_INTERACT_BLOCKING;
        await glass.use(2);
        broken = FALSE;
        atom_integrity = max_integrity;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/fishing/aquarium/aquarium.dm:83
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (!Dm.truthy(broken))
            await aquarium_smash();
        return dot;
    }

    // tg: code/modules/fishing/aquarium/aquarium.dm:88
    public virtual async DmTask aquarium_smash()
    {
        broken = TRUE;
        DmList possible_destinations_for_fish = Dm.list();
        object droploc = drop_location();
        if (isturf(droploc))
            possible_destinations_for_fish = get_adjacent_open_turfs((atom)droploc) as DmList;
        else
            possible_destinations_for_fish = Dm.list(droploc);
        playsound(this, Dm.resource("sound/effects/glass/glassbr3.ogg"), 100, TRUE);
        foreach (atom_movable content in Dm.each<atom_movable>(contents))
            await content.forceMove((atom)pick(possible_destinations_for_fish));
        if (fluid_type != AQUARIUM_FLUID_AIR)
        {
            datum_reagents reagent_splash = Dm.@new<datum_reagents>();
            await reagent_splash.add_reagent(typeof(datum_reagent_water), 30);
            await chem_splash(droploc as atom, null, 3, Dm.list(reagent_splash));
        }
        update_appearance();
    }
}

// tg: code/modules/fishing/aquarium/aquarium.dm:106
public partial class obj_structure_aquarium_prefilled : obj_structure_aquarium
{
    // tg: code/modules/fishing/aquarium/aquarium.dm:110
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Dm.@new<obj_item_aquarium_prop_sand>(this);
        Dm.@new<obj_item_aquarium_prop_seaweed>(this);

        Dm.@new<obj_item_fish_goldfish>(this);
        Dm.@new<obj_item_fish_angelfish>(this);
        Dm.@new<obj_item_fish_guppy>(this);

        //They'll be alive for about 30 minutes with this amount.
        Waiting.NoSleep(((datum_reagents)reagents).add_reagent(typeof(datum_reagent_consumable_nutriment), 3));
        return dot;
    }
}

// tg: code/modules/fishing/aquarium/aquarium.dm:123
public partial class obj_item_fish_tank : obj_item
{
    // tg: code/modules/fishing/aquarium/aquarium.dm:161
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_aquarium),
            Dm.named("min_px", 6),
            Dm.named("max_px", 26),
            Dm.named("min_py", 7),
            Dm.named("max_py", 24),
            Dm.named("default_beauty", 100),
            Dm.named("reagents_size", reagent_size),
            Dm.named("min_fluid_temp", min_fluid_temp),
            Dm.named("max_fluid_temp", max_fluid_temp),
            Dm.named("init_mode", init_mode)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_aquarium)));
        RegisterSignal(this, COMSIG_AQUARIUM_FLUID_CHANGED, nameof(on_aquarium_liquid_changed));
        RegisterSignal(this, COMSIG_AQUARIUM_CAN_INSERT, nameof(can_insert));
        RegisterSignal(this, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(on_new_fish));
        return dot;
    }

    // tg: code/modules/fishing/aquarium/aquarium.dm:182
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = await base.update_icon(updates, __extra);
        ///"aquarium_map" is used for mapping, so mappers can tell what it's.
        icon_state = Dm.str(base_icon_state);
        return dot;
    }

    // tg: code/modules/fishing/aquarium/aquarium.dm:187
    [SignalHandler]
    public virtual void on_aquarium_liquid_changed(datum source = null, object fluid_type = null)
    {
        this.fluid_type = Dm.str(fluid_type);
        update_appearance();
    }

    // tg: code/modules/fishing/aquarium/aquarium.dm:192
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.append(ref dot, Dm.text("[]_panel[]", base_icon_state, HAS_TRAIT(this, TRAIT_AQUARIUM_PANEL_OPEN) ? "_open" : ""));
        Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_[]", base_icon_state, fluid_type == AQUARIUM_FLUID_AIR ? "air" : "water"), layer: layer + AQUARIUM_GLASS_LAYER));
        Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_borders", base_icon_state), layer: layer + AQUARIUM_BORDERS_LAYER));
        return dot;
    }

    // tg: code/modules/fishing/aquarium/aquarium.dm:198
    [SignalHandler]
    public virtual object can_insert(atom_movable source = null, obj_item item = null, mob_living user = null)
    {
        if (!isfish(item))
            return null;
        obj_item_fish fish = (obj_item_fish)item;
        if (Dm.num(fish.size) > maximum_relative_size)
        {
            balloon_alert(user, I18n.T("fish is too big!"));
            return COMSIG_CANNOT_INSERT_IN_AQUARIUM;
        }
        if (current_summed_size > max_total_size)
        {
            balloon_alert(user, I18n.T("fish tank is full!"));
            return COMSIG_CANNOT_INSERT_IN_AQUARIUM;
        }
        return COMSIG_CAN_INSERT_IN_AQUARIUM;
    }

    // tg: code/modules/fishing/aquarium/aquarium.dm:211
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        on_new_fish(this, arrived);
        return dot;
    }

    // tg: code/modules/fishing/aquarium/aquarium.dm:215
    [SignalHandler]
    public virtual void on_new_fish(datum source = null, atom_movable movable = null)
    {
        if (!isfish(movable))
            return;
        obj_item_fish fish = (obj_item_fish)movable;
        Waiting.NoSleep(change_size_weight(fish.size, fish.weight));
        RegisterSignal(fish, COMSIG_FISH_UPDATE_SIZE_AND_WEIGHT, nameof(on_fish_size_weight_updated));
    }

    // tg: code/modules/fishing/aquarium/aquarium.dm:223
    [SignalHandler]
    public virtual void on_fish_size_weight_updated(obj_item_fish source = null, object new_size = null, object new_weight = null)
    {
        Waiting.NoSleep(change_size_weight(Dm.num(new_size) - Dm.num(source.size), Dm.num(new_weight) - Dm.num(source.weight)));
    }

    // tg: code/modules/fishing/aquarium/aquarium.dm:227
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (isfish(gone))
        {
            obj_item_fish fish = (obj_item_fish)gone;
            await change_size_weight(-Dm.num(fish.size), -Dm.num(fish.weight));
            UnregisterSignal(fish, COMSIG_FISH_UPDATE_SIZE_AND_WEIGHT);
        }
        return await base.Exited(gone, direction);
    }

    // tg: code/modules/fishing/aquarium/aquarium.dm:234
    public virtual async DmTask change_size_weight(object size_change = null, object weight_change = null)
    {
        current_summed_size += Dm.num(size_change);
        current_summed_weight += Dm.num(weight_change);
        if (current_summed_size > max_total_size)
            ADD_TRAIT(this, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH, INNATE_TRAIT);
        else
            REMOVE_TRAIT(this, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH, INNATE_TRAIT);
        if (HAS_TRAIT(this, TRAIT_SPEED_POTIONED) || current_summed_weight < FISH_WEIGHT_SLOWDOWN)
        {
            slowdown = 0;
            drag_slowdown = 0f;
        }
        else
        {
            slowdown = GET_FISH_SLOWDOWN(current_summed_weight) * slowdown_coeff;
            drag_slowdown = slowdown * 0.5f;
        }
        if (ismob(loc))
        {
            mob mob = (mob)loc;
            await mob.update_equipment_speed_mods();
        }

        force = min(2 + (GET_FISH_WEIGHT_RANK(current_summed_weight) * 3), 21);
        throwforce = force;
    }

    // tg: code/modules/fishing/aquarium/aquarium.dm:254
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (HAS_TRAIT_FROM(this, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH, INNATE_TRAIT))
            Dm.append(ref dot, span_warning(I18n.T("It's cramped in there. There's no more room for fish to reproduce and grow!")));
        return dot;
    }
}

///The lawyer's own pet goldfish's fish tank. It used to be an aquarium, but now it can be held and carried around.
// tg: code/modules/fishing/aquarium/aquarium.dm:260
public partial class obj_item_fish_tank_lawyer : obj_item_fish_tank
{
    // tg: code/modules/fishing/aquarium/aquarium.dm:263
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Dm.@new<obj_item_aquarium_prop_sand>(this);
        Dm.@new<obj_item_aquarium_prop_seaweed>(this);

        if (prob(85))
        {
            Dm.@new<obj_item_fish_goldfish_gill>(this);
            Waiting.NoSleep(((datum_reagents)reagents).add_reagent(typeof(datum_reagent_consumable_nutriment), 3));
        }
        else
        {
            Dm.@new<obj_item_fish_goldfish_three_eyes_gill>(this);
            Waiting.NoSleep(((datum_reagents)reagents).add_reagent(typeof(datum_reagent_toxin_mutagen), 3)); //three eyes goldfish feed on mutagen.
        }
        return dot;
    }
}
