// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fishing_equipment.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the fishing lines (the reinforced and sinew lines that fish in lava, the auto-reel that throws a hooked thing
// back, the bluespace line), the hooks (the magnetic, shiny, anomalous, weighted, rescue, bone, gyro-stabilized and jawed hooks),
// the fishing tip and the lures' instructions, the fishing spot capsule, the fishing bags, the fish gene-gun and the fish trait
// injector, and the hazard signs. The capsule's context tip is deferred with the other screentips. The var blocks are
// generated (D111).

/// Multipilier to the fishing weights of anything that's not a fish nor a dud for the magnet hook.
// tg: code/modules/fishing/fishing_equipment.dm:3  #define MAGNET_HOOK_BONUS_MULTIPLIER 5
// tg: code/modules/fishing/fishing_equipment.dm:5  #define RESCUE_HOOK_FISH_MULTIPLIER 0

// tg: code/modules/fishing/fishing_equipment.dm:26
public partial class obj_item_fishing_line_reinforced : obj_item_fishing_line
{
    // tg: code/modules/fishing/fishing_equipment.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ITEM_FISHING_ROD_SLOTTED, nameof(on_fishing_rod_slotted));
        RegisterSignal(this, COMSIG_ITEM_FISHING_ROD_UNSLOTTED, nameof(on_fishing_rod_unslotted));
        return dot;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:39
    [SignalHandler]
    public virtual void on_fishing_rod_slotted(datum source = null, obj_item_fishing_rod rod = null, object slot = null)
    {
        ADD_TRAIT(rod, TRAIT_ROD_LAVA_USABLE, REF(this));
    }

    // tg: code/modules/fishing/fishing_equipment.dm:43
    [SignalHandler]
    public virtual void on_fishing_rod_unslotted(datum source = null, obj_item_fishing_rod rod = null, object slot = null)
    {
        REMOVE_TRAIT(rod, TRAIT_ROD_LAVA_USABLE, REF(this));
    }
}

// tg: code/modules/fishing/fishing_equipment.dm:64
public partial class obj_item_fishing_line_sinew : obj_item_fishing_line
{
    // tg: code/modules/fishing/fishing_equipment.dm:73
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ITEM_FISHING_ROD_SLOTTED, nameof(on_fishing_rod_slotted));
        RegisterSignal(this, COMSIG_ITEM_FISHING_ROD_UNSLOTTED, nameof(on_fishing_rod_unslotted));
        return dot;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:78
    [SignalHandler]
    public virtual void on_fishing_rod_slotted(datum source = null, obj_item_fishing_rod rod = null, object slot = null)
    {
        ADD_TRAIT(rod, TRAIT_ROD_LAVA_USABLE, REF(this));
    }

    // tg: code/modules/fishing/fishing_equipment.dm:82
    [SignalHandler]
    public virtual void on_fishing_rod_unslotted(datum source = null, obj_item_fishing_rod rod = null, object slot = null)
    {
        REMOVE_TRAIT(rod, TRAIT_ROD_LAVA_USABLE, REF(this));
    }
}

/**
 * A special line reel that let you skip the biting phase of the minigame, netting you a completion bonus,
 * and thrown hooked items at you, so you can rapidly catch them from afar.
 * It may also work on mobs if the right hook is attached.
 */
// tg: code/modules/fishing/fishing_equipment.dm:91
public partial class obj_item_fishing_line_auto_reel : obj_item_fishing_line
{
    // tg: code/modules/fishing/fishing_equipment.dm:103
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ITEM_FISHING_ROD_SLOTTED, nameof(on_fishing_rod_slotted));
        RegisterSignal(this, COMSIG_ITEM_FISHING_ROD_UNSLOTTED, nameof(on_fishing_rod_unslotted));
        return dot;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:108
    [SignalHandler]
    public virtual void on_fishing_rod_slotted(datum source = null, obj_item_fishing_rod rod = null, object slot = null)
    {
        RegisterSignal(rod, COMSIG_FISHING_ROD_HOOKED_ITEM, nameof(on_hooked_item));
    }

    // tg: code/modules/fishing/fishing_equipment.dm:112
    [SignalHandler]
    public virtual void on_fishing_rod_unslotted(datum source = null, obj_item_fishing_rod rod = null, object slot = null)
    {
        UnregisterSignal(rod, COMSIG_FISHING_ROD_HOOKED_ITEM);
    }

    // tg: code/modules/fishing/fishing_equipment.dm:116
    [SignalHandler]
    public virtual void on_hooked_item(obj_item_fishing_rod source = null, atom_movable target = null, mob_living user = null)
    {
        if (target == null || Dm.truthy(target.anchored) || target.move_resist >= MOVE_FORCE_STRONG)
            return;
        bool please_be_gentle = false;
        atom destination = null;
        datum_callback throw_callback = null;
        if (isliving(target) || !isitem(target))
        {
            destination = get_step_towards(user, target);
            please_be_gentle = true;
        }
        else
        {
            destination = user;
            throw_callback = Dm.@new<datum_callback>(this, nameof(clear_hitby_signal), target);
            RegisterSignal(target, COMSIG_MOVABLE_PRE_IMPACT, nameof(catch_it_chucklenut));
        }

        if (!Dm.truthy(Waiting.NoSleep(target.safe_throw_at(destination, source.cast_range, 2, callback: throw_callback, gentle: please_be_gentle ? TRUE : FALSE))))
            UnregisterSignal(target, COMSIG_MOVABLE_PRE_IMPACT);
        else
            playsound(this, Dm.resource("sound/items/weapons/batonextend.ogg"), 50, TRUE);
    }

    // tg: code/modules/fishing/fishing_equipment.dm:137
    [SignalHandler]
    public virtual object catch_it_chucklenut(obj_item source = null, atom hit_atom = null, datum_thrownthing throwingdatum = null)
    {
        mob_living user = throwingdatum.initial_target?.resolve() as mob_living;
        if (QDELETED(user) || hit_atom != user)
            return NONE;
        if (!Dm.truthy(user.try_catch_item(source, skip_throw_mode_check: TRUE, try_offhand: TRUE)))
            return NONE;
        return COMPONENT_MOVABLE_IMPACT_NEVERMIND;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:147
    public virtual void clear_hitby_signal(obj_item item = null)
    {
        UnregisterSignal(item, COMSIG_MOVABLE_PRE_IMPACT);
    }
}

// Hooks

// tg: code/modules/fishing/fishing_equipment.dm:164
public partial class obj_item_fishing_hook : obj_item
{
    /**
     * Simple getter proc for hooks to implement special hook bonuses for
     * certain `fish_type` (or FISHING_DUD), additive. Is applied after
     * `get_hook_bonus_multiplicative()`.
     */
    // tg: code/modules/fishing/fishing_equipment.dm:183
    public virtual object get_hook_bonus_additive(object fish_type = null)
    {
        return FISHING_DEFAULT_HOOK_BONUS_ADDITIVE;
    }

    /**
     * Simple getter proc for hooks to implement special hook bonuses for
     * certain `fish_type` (or FISHING_DUD), multiplicative. Is applied before
     * `get_hook_bonus_additive()`.
     */
    // tg: code/modules/fishing/fishing_equipment.dm:192
    public virtual object get_hook_bonus_multiplicative(object fish_type = null)
    {
        return FISHING_DEFAULT_HOOK_BONUS_MULTIPLICATIVE;
    }

    ///Check if tha target can be caught by the hook
    // tg: code/modules/fishing/fishing_equipment.dm:196
    public virtual object can_be_hooked(atom target = null)
    {
        if (isliving(target))
        {
            mob_living mob = (mob_living)target;
            return Dm.bitand(mob.mob_biotypes, MOB_AQUATIC);
        }
        return isitem(target);
    }

    ///Any special effect when hooking a target that's not managed by the fishing rod.
    // tg: code/modules/fishing/fishing_equipment.dm:203
    public virtual async DmTask hook_attached(atom target = null, obj_item_fishing_rod rod = null)
    {
        await DmTask.CompletedTask;
    }

    /**
     * Is there a reason why this hook couldn't fish in target_fish_source?
     * If so, return the denial reason as a string, otherwise return `null`.
     */
    // tg: code/modules/fishing/fishing_equipment.dm:213
    public virtual object reason_we_cant_fish(datum_fish_source target_fish_source = null)
    {
        return null;
    }
}

// tg: code/modules/fishing/fishing_equipment.dm:217
public partial class obj_item_fishing_hook_magnet : obj_item_fishing_hook
{
    // tg: code/modules/fishing/fishing_equipment.dm:225
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ITEM_FISHING_ROD_SLOTTED, nameof(on_fishing_rod_slotted));
        RegisterSignal(this, COMSIG_ITEM_FISHING_ROD_UNSLOTTED, nameof(on_fishing_rod_unslotted));
        return dot;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:230
    [SignalHandler]
    public virtual void on_fishing_rod_slotted(datum source = null, obj_item_fishing_rod rod = null, object slot = null)
    {
        ADD_TRAIT(rod, TRAIT_ROD_REMOVE_FISHING_DUD, REF(this));
    }

    // tg: code/modules/fishing/fishing_equipment.dm:234
    [SignalHandler]
    public virtual void on_fishing_rod_unslotted(datum source = null, obj_item_fishing_rod rod = null, object slot = null)
    {
        REMOVE_TRAIT(rod, TRAIT_ROD_REMOVE_FISHING_DUD, REF(this));
    }

    // tg: code/modules/fishing/fishing_equipment.dm:238
    public override object get_hook_bonus_multiplicative(object fish_type = null)
    {
        if (Dm.eq(fish_type, FISHING_DUD) || ispath(fish_type, typeof(obj_item_fish)) || isfish(fish_type))
            return base.get_hook_bonus_multiplicative(fish_type);

        // We multiply the odds by five for everything that's not a fish nor a dud
        return 5; // MAGNET_HOOK_BONUS_MULTIPLIER
    }
}

// tg: code/modules/fishing/fishing_equipment.dm:245
public partial class obj_item_fishing_hook_shiny : obj_item_fishing_hook
{
    // tg: code/modules/fishing/fishing_equipment.dm:251
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ITEM_FISHING_ROD_SLOTTED, nameof(on_fishing_rod_slotted));
        RegisterSignal(this, COMSIG_ITEM_FISHING_ROD_UNSLOTTED, nameof(on_fishing_rod_unslotted));
        return dot;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:256
    [SignalHandler]
    public virtual void on_fishing_rod_slotted(datum source = null, obj_item_fishing_rod rod = null, object slot = null)
    {
        ADD_TRAIT(rod, TRAIT_ROD_ATTRACT_SHINY_LOVERS, REF(this));
    }

    // tg: code/modules/fishing/fishing_equipment.dm:260
    [SignalHandler]
    public virtual void on_fishing_rod_unslotted(datum source = null, obj_item_fishing_rod rod = null, object slot = null)
    {
        REMOVE_TRAIT(rod, TRAIT_ROD_ATTRACT_SHINY_LOVERS, REF(this));
    }
}

// tg: code/modules/fishing/fishing_equipment.dm:264
public partial class obj_item_fishing_hook_anomaly : obj_item_fishing_hook
{
    // tg: code/modules/fishing/fishing_equipment.dm:273
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ITEM_FISHING_ROD_SLOTTED, nameof(on_fishing_rod_slotted));
        RegisterSignal(this, COMSIG_ITEM_FISHING_ROD_UNSLOTTED, nameof(on_fishing_rod_unslotted));
        return dot;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:278
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (core != null)
            Dm.append(ref dot, span_info(I18n.T("There's \\a [] slotted into it.", core)));
        return dot;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:283
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        Dm.append(ref dot, I18n.T("&bull; A [] adds a chance for your catch to have its materials altered.", Dm.initial(typeof(obj_item_assembly_signaler_anomaly_dimensional), (atom p) => p.name)));
        Dm.append(ref dot, I18n.T("&bull; A [] adds a chance for your catch to have its traits or stats altered.", Dm.initial(typeof(obj_item_assembly_signaler_anomaly_bioscrambler), (atom p) => p.name)));
        Dm.append(ref dot, I18n.T("&bull; A [] adds a chance for your catch to be cooked.", Dm.initial(typeof(obj_item_assembly_signaler_anomaly_pyro), (atom p) => p.name)));
        Dm.append(ref dot, I18n.T("&bull; A [] adds a chance to catch haunted fish.", Dm.initial(typeof(obj_item_assembly_signaler_anomaly_ectoplasm), (atom p) => p.name)));
        Dm.append(ref dot, I18n.T("&bull; A [] adds a chance to cause your catches to randomly grow or shrink in size.", Dm.initial(typeof(obj_item_assembly_signaler_anomaly_hallucination), (atom p) => p.name)));
        Dm.append(ref dot, I18n.T("&bull; A [] reduces the gravity of the bobber, causing it to fall slower.", Dm.initial(typeof(obj_item_assembly_signaler_anomaly_grav), (atom p) => p.name)));
        Dm.append(ref dot, I18n.T("&bull; A [] reduces the bobber's overall bounciness.", Dm.initial(typeof(obj_item_assembly_signaler_anomaly_vortex), (atom p) => p.name)));
        Dm.append(ref dot, I18n.T("&bull; Unmentioned cores likely have no unique effect when applied."));
        return dot;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:294
    [SignalHandler]
    public virtual void on_fishing_rod_slotted(datum source = null, obj_item_fishing_rod rod = null, object slot = null)
    {
        Type core_type = core?.type;
        if (core_type == typeof(obj_item_assembly_signaler_anomaly_dimensional))
            RegisterSignal(rod, COMSIG_FISHING_ROD_CAUGHT_FISH, nameof(dimensional_catch_bonus));
        else if (core_type == typeof(obj_item_assembly_signaler_anomaly_bioscrambler))
            RegisterSignal(rod, COMSIG_FISHING_ROD_CAUGHT_FISH, nameof(bioscrambler_catch_bonus));
        else if (core_type == typeof(obj_item_assembly_signaler_anomaly_pyro))
            RegisterSignal(rod, COMSIG_FISHING_ROD_CAUGHT_FISH, nameof(pyro_catch_effect));
        else if (core_type == typeof(obj_item_assembly_signaler_anomaly_ectoplasm))
            RegisterSignal(rod, COMSIG_FISHING_ROD_CAUGHT_FISH, nameof(ectoplasm_catch_effect));
        else if (core_type == typeof(obj_item_assembly_signaler_anomaly_hallucination))
            RegisterSignal(rod, COMSIG_FISHING_ROD_CAUGHT_FISH, nameof(hallucination_catch_effect));
        else if (core_type == typeof(obj_item_assembly_signaler_anomaly_grav))
            rod.gravity_mult *= 0.4f;
        else if (core_type == typeof(obj_item_assembly_signaler_anomaly_vortex))
            rod.bounciness_mult *= 0.2f;
        else
            rod.balloon_alert_to_viewers(I18n.T("no core effect!"), vision_distance: 2);
    }

    // tg: code/modules/fishing/fishing_equipment.dm:313
    [SignalHandler]
    public virtual void on_fishing_rod_unslotted(datum source = null, obj_item_fishing_rod rod = null, object slot = null)
    {
        Type core_type = core?.type;
        if (core_type == typeof(obj_item_assembly_signaler_anomaly_grav))
            rod.gravity_mult /= 0.4f;
        else if (core_type == typeof(obj_item_assembly_signaler_anomaly_vortex))
            rod.bounciness_mult /= 0.2f;

        UnregisterSignal(rod, COMSIG_FISHING_ROD_CAUGHT_FISH);
    }

    // tg: code/modules/fishing/fishing_equipment.dm:324
    public virtual object get_probability_bonuses(obj_item_fishing_rod rod = null, mob_living user = null)
    {
        float skill_modifier = Dm.num(user.mind?.get_skill_level(typeof(datum_skill_fishing))) * 1.5f;
        float bait_modifier = 0;
        if (rod.bait_value != null)
        {
            if (HAS_TRAIT(rod.bait_value, TRAIT_GREAT_QUALITY_BAIT))
                bait_modifier += 16;
            else if (HAS_TRAIT(rod.bait_value, TRAIT_GOOD_QUALITY_BAIT))
                bait_modifier += 8;
            else if (HAS_TRAIT(rod.bait_value, TRAIT_BASIC_QUALITY_BAIT))
                bait_modifier += 4;
        }

        // up to a ~25% bonus
        return skill_modifier + bait_modifier;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:339
    [SignalHandler]
    public virtual void dimensional_catch_bonus(obj_item_fishing_rod rod = null, obj_item caught = null, mob_living user = null)
    {
        if (!isfish(caught) || !HAS_TRAIT(caught, TRAIT_FISH_JUST_SPAWNED))
            return;

        if (!prob(25 + Dm.num(get_probability_bonuses(rod, user))))
            return;

        obj_item_fish caught_fish = (obj_item_fish)caught;
        // number of materials applies to the fish
        DmList material_amounts = Dm.alist(
            Dm.kv(1, 16),
            Dm.kv(2, 3),
            Dm.kv(3, 1));
        // generic list of materials we may apply
        DmList material_weights = Dm.alist(
            Dm.kv(typeof(datum_material_gold), 20),
            Dm.kv(typeof(datum_material_silver), 20),
            Dm.kv(typeof(datum_material_plastic), 10),
            Dm.kv(typeof(datum_material_uranium), 5),
            Dm.kv(typeof(datum_material_plasma), 5));

        // adds chance of inheriting rod materials
        if (Dm.truthy(Dm.bitand(rod.material_flags, MATERIAL_EFFECTS)))
        {
            foreach (object rod_material_type in Dm.each(rod.custom_materials))
            {
                object rod_material_amount = rod.custom_materials[rod_material_type];
                material_weights[rod_material_type] = 40 * (Dm.num(rod_material_amount) / Dm.num(Dm.values_sum(rod.custom_materials)));
            }
        }

        // select a number of materials, then select what materials to apply
        float num_materials = Dm.num(pick_weight(material_amounts));
        DmList material_setup = Dm.list();
        for (int i = 1; i <= num_materials; i++)
        {
            object picked = pick_weight(material_weights);
            material_setup[picked] = Dm.num(material_setup[picked]) + round(Dm.num(caught_fish.weight) * (1 / num_materials));
        }

        Waiting.NoSleep(caught_fish.set_custom_materials(material_setup));
    }

    // tg: code/modules/fishing/fishing_equipment.dm:375
    [SignalHandler]
    public virtual void bioscrambler_catch_bonus(obj_item_fishing_rod rod = null, obj_item caught = null, mob_living user = null)
    {
        if (!isfish(caught) || !HAS_TRAIT(caught, TRAIT_FISH_JUST_SPAWNED) || !prob(25 + Dm.num(get_probability_bonuses(rod, user))))
            return;

        // DEVIATION(DV680): tg walks GLOB.fish_traits's keys, which are the traits' typepaths, reads the weight off a typepath and
        // stores the trait under its weight as a numeric index (`random_pool[random_trait.bioscramble_weight] = random_trait`, :385):
        // both are runtimes in BYOND, so the bioscrambler core never changes a catch's traits. The port weighs each trait singleton by
        // its bioscramble_weight, which pick_weight below expects.
        DmList random_pool = Dm.list();
        foreach (object trait_path in Dm.each(GLOB.fish_traits))
        {
            datum_fish_trait random_trait = GLOB.fish_traits[trait_path] as datum_fish_trait;
            if (random_trait.bioscramble_weight > 0)
                random_pool[random_trait] = random_trait.bioscramble_weight;
        }

        obj_item_fish caught_fish = (obj_item_fish)caught;
        Waiting.NoSleep(caught_fish.update_size_and_weight(new_weight: Dm.num(caught_fish.weight) * Dm.num(pick(0.6f, 0.8f, 1, 1.2f, 1.4f))));

        datum_fish_trait picked_trait = pick_weight(random_pool) as datum_fish_trait;
        Waiting.NoSleep(picked_trait.apply_to_fish(caught_fish));
    }

    // tg: code/modules/fishing/fishing_equipment.dm:391
    [SignalHandler]
    public virtual void pyro_catch_effect(obj_item_fishing_rod rod = null, obj_item caught = null, mob_living user = null)
    {
        if (!isfish(caught) || !HAS_TRAIT(caught, TRAIT_FISH_JUST_SPAWNED) || !prob(85 + Dm.num(get_probability_bonuses(rod, user))))
            return;

        DmList fry_times = Dm.alist(
            Dm.kv(FRYING_TIME_FRIED, 2),
            Dm.kv(FRYING_TIME_PERFECT, 5),
            Dm.kv(FRYING_TIME_BURNT, 3));

        Waiting.NoSleep(caught.AddElement(typeof(datum_element_fried_item), pick_weight(fry_times)));
    }

    // tg: code/modules/fishing/fishing_equipment.dm:404
    [SignalHandler]
    public virtual void ectoplasm_catch_effect(obj_item_fishing_rod rod = null, obj_item caught = null, mob_living user = null)
    {
        if (!isfish(caught) || !HAS_TRAIT(caught, TRAIT_FISH_JUST_SPAWNED) || !prob(50 + Dm.num(get_probability_bonuses(rod, user))))
            return;

        Waiting.NoSleep(caught.AddComponent(typeof(datum_component_haunted_item),
            Dm.named("haunt_color", "#52336e"),
            Dm.named("haunt_duration", 6 * MINUTES),
            Dm.named("spawn_message", span_revenwarning(I18n.T("[] slowly rises upward, flopping menacingly in the air...", caught))),
            Dm.named("despawn_message", span_revenwarning(I18n.T("[] settles to the floor, looking like a normal fish again...", caught)))));
    }

    // tg: code/modules/fishing/fishing_equipment.dm:416
    [SignalHandler]
    public virtual void hallucination_catch_effect(obj_item_fishing_rod rod = null, obj_item caught = null, mob_living user = null)
    {
        if (!isfish(caught) || !HAS_TRAIT(caught, TRAIT_FISH_JUST_SPAWNED) || !prob(50 + Dm.num(get_probability_bonuses(rod, user))))
            return;

        caught.transform = ((matrix)caught.transform).Scale(Dm.num(pick(0.5f, 0.75f, 1, 1.25f, 1.5f)));
    }

    // tg: code/modules/fishing/fishing_equipment.dm:424
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_assembly_signaler_anomaly)))
            return NONE;
        if (core != null)
        {
            balloon_alert(user, I18n.T("already has a core!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(user.is_holding(tool)))
        {
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
        }
        else
            await tool.forceMove(this);

        core = (obj_item_assembly_signaler_anomaly)tool;
        balloon_alert(user, I18n.T("core installed"));
        playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE, SILENCED_SOUND_EXTRARANGE);
        update_appearance(UPDATE_OVERLAYS);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:443
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (core == null)
            return dot;

        if (Dm.truthy(user.is_holding(this)))
            await user.put_in_hands(core);
        else
            await core.forceMove(drop_location() as atom);

        core = null;
        balloon_alert(user, I18n.T("core removed"));
        playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE, SILENCED_SOUND_EXTRARANGE);
        update_appearance(UPDATE_OVERLAYS);
        return TRUE;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:461
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (core == null)
            return dot;

        mutable_appearance core_base = mutable_appearance(icon, "anom_overlay_base", alpha: alpha) as mutable_appearance;
        Dm.append(ref dot, core_base);

        mutable_appearance core_color = mutable_appearance(icon, "anom_overlay_light", alpha: alpha) as mutable_appearance;
        core_color.color = core.core_color;
        Dm.append(ref dot, core_color);

        // DEVIATION(DV12): . += emissive_appearance(icon, "anom_overlay_light", src, alpha = src.alpha), the core's glow on the emissive plane
        return dot;
    }
}

// tg: code/modules/fishing/fishing_equipment.dm:484
public partial class obj_item_fishing_hook_rescue : obj_item_fishing_hook
{
    // tg: code/modules/fishing/fishing_equipment.dm:493
    public override object can_be_hooked(atom target = null)
    {
        return Dm.truthy(base.can_be_hooked(target)) || isliving(target);
    }

    // tg: code/modules/fishing/fishing_equipment.dm:496
    public override async DmTask hook_attached(atom target = null, obj_item_fishing_rod rod = null)
    {
        if (isliving(target))
        {
            mob_living living_target = (mob_living)target;
            await living_target.apply_status_effect(typeof(datum_status_effect_grouped_hooked), rod.fishing_line);
        }
    }

    // This hook can only fish in chasms.
    // tg: code/modules/fishing/fishing_equipment.dm:504
    public override object reason_we_cant_fish(datum_fish_source target_fish_source = null)
    {
        if (istype(target_fish_source, typeof(datum_fish_source_chasm)))
            return base.reason_we_cant_fish(target_fish_source);

        return I18n.T("The hook on your fishing rod wasn't meant for traditional fishing, rendering it useless at doing so!");
    }

    // tg: code/modules/fishing/fishing_equipment.dm:511
    public override object get_hook_bonus_multiplicative(object fish_type = null)
    {
        // Sorry, you won't catch fish with this.
        if (ispath(fish_type, typeof(obj_item_fish)) || isfish(fish_type))
            return 0; // RESCUE_HOOK_FISH_MULTIPLIER

        return base.get_hook_bonus_multiplicative(fish_type);
    }
}

// tg: code/modules/fishing/fishing_equipment.dm:527
public partial class obj_item_fishing_hook_stabilized : obj_item_fishing_hook
{
    // tg: code/modules/fishing/fishing_equipment.dm:536
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("While fishing, you can hold the <b>Right</b> Mouse Button to move the bait down, rather than up.")));
        return dot;
    }
}

// tg: code/modules/fishing/fishing_equipment.dm:540
public partial class obj_item_fishing_hook_jaws : obj_item_fishing_hook
{
    // tg: code/modules/fishing/fishing_equipment.dm:551
    public override object can_be_hooked(atom target = null)
    {
        return Dm.truthy(base.can_be_hooked(target)) || isliving(target);
    }

    // tg: code/modules/fishing/fishing_equipment.dm:554
    public override async DmTask hook_attached(atom target = null, obj_item_fishing_rod rod = null)
    {
        if (isliving(target))
        {
            mob_living living_target = (mob_living)target;
            await living_target.apply_status_effect(typeof(datum_status_effect_grouped_hooked_jaws), rod.fishing_line);
        }
    }
}

///Used to give the average player info about fishing stuff that's unknown to many.
// tg: code/modules/fishing/fishing_equipment.dm:560
public partial class obj_item_paper_paperslip_fishing_tip : obj_item_paper_paperslip
{
    // tg: code/modules/fishing/fishing_equipment.dm:564
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        default_raw_text = pick(GLOB.fishing_tips);
        return base.Initialize(mapload, __extra);
    }
}

///A modified mining capsule from the black market and sometimes random loot.
// tg: code/modules/fishing/fishing_equipment.dm:590
public partial class obj_item_survivalcapsule_fishing : obj_item_survivalcapsule
{
    // tg: code/modules/fishing/fishing_equipment.dm:602
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_CONTRABAND, INNATE_TRAIT);
        register_context();
        voice = SStts?.random_tts_voice(); // a toy room has no SStts
        return dot;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:613
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_info(I18n.T("[] to change the selected fishing spot when held.", EXAMINE_HINT(I18n.T("Right-Click")))));
        return dot;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:617
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        Dm.append(ref dot, span_tinynotice(I18n.T("A tiny print on the side reads: \"Use a cryptographic sequencer to disable safeties\".")));
        return dot;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:621
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        balloon_alert(user, I18n.T("safeties disabled"));
        playsound(this, SFX_SPARKS, 25, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        await DmTask.CompletedTask;
        return TRUE;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:629
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        object dot = await base.attack_self_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;
        dot = SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        if (Dm.truthy(used))
            return dot;
        DmList choices = Dm.list();
        DmList spot_ids_by_name = Dm.list();
        foreach (Type spot in Dm.each<Type>(typesof(typeof(datum_map_template_shelter_fishing))))
        {
            if (!Dm.truthy(Dm.initial(spot, (datum_map_template_shelter_fishing p) => p.safe)) && !Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                continue;
            string spot_name = Dm.initial(spot, (datum_map_template p) => p.name);
            choices[spot_name] = image(Dm.resource("icons/hud/radial_fishing.dmi"), icon_state: Dm.initial(spot, (datum_map_template_shelter_fishing p) => p.radial_icon));
            spot_ids_by_name[spot_name] = Dm.initial(spot, (datum_map_template_shelter p) => p.shelter_id);
        }
        object choice = await show_radial_menu(user, this, choices, radius: 38, custom_check: Dm.@new<datum_callback>(this, nameof(can_interact), user), tooltips: TRUE);
        if (!Dm.truthy(choice) || Dm.truthy(used) || !Dm.truthy(await can_interact(user)))
            return dot;
        template_id = spot_ids_by_name[choice] as string;
        template = SSmapping.shelter_templates[template_id] as datum_map_template_shelter;
        to_chat(user, span_notice(I18n.T("You change []'s selected fishing spot to [].", this, choice)));
        playsound(this, Dm.resource("sound/items/pen_click.ogg"), 20, TRUE, -3);
        return dot;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:651
    public override object get_ignore_flags()
    {
        object dot = base.get_ignore_flags();
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            dot = Dm.add(dot, Dm.bitor(CAPSULE_IGNORE_ANCHORED_OBJECTS, CAPSULE_IGNORE_BANNED_OBJECTS));
        return dot;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:656
    public override async DmTask fail_feedback(object status = null)
    {
        switch (Dm.str(status))
        {
            case SHELTER_DEPLOY_BAD_AREA:
                await say(I18n.T("I refuse to deploy in this area."));
                break;
            case SHELTER_DEPLOY_BAD_TURFS:
                await say(I18n.T("The walls are too close! I need []x[] area to deploy.", template.width, template.height));
                break;
            case SHELTER_DEPLOY_ANCHORED_OBJECTS:
                await say(I18n.T("Get these anchored objects out of the way! I need []x[] area to deploy.", template.width, template.height));
                break;
            case SHELTER_DEPLOY_BANNED_OBJECTS:
                await say(I18n.T("Remove all cables and pipes around me in a []x[] area or I won't deploy.", template.width, template.height));
                break;
            case SHELTER_DEPLOY_OUTSIDE_MAP:
                await say(I18n.T("For fucks sake, deploy me somewhere less far fatched!"));
                break;
        }
    }

    // tg: code/modules/fishing/fishing_equipment.dm:669
    public override void trigger_admin_alert(mob triggerer = null, turf trigger_loc = null)
    {
        datum_map_template_shelter_fishing spot = template as datum_map_template_shelter_fishing;
        if (Dm.truthy(spot.safe)) //Don't log if the fishing spot is safe
            return;

        area area = get_area(this) as area;

        if (!Dm.truthy(area.outdoors))
            message_admins(Dm.text("[] activated an unsafe fishing capsule at []", ADMIN_LOOKUPFLW(triggerer), ADMIN_VERBOSEJMP(trigger_loc)));
        log_admin(Dm.text("[] activated an unsafe fishing capsule at []", key_name(triggerer), AREACOORD(trigger_loc)));
    }
}

// tg: code/modules/fishing/fishing_equipment.dm:686
public partial class obj_item_storage_bag_fishing : obj_item_storage_bag
{
    // tg: code/modules/fishing/fishing_equipment.dm:699
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), fishing_modifier, ITEM_SLOT_HANDS));
        return dot;
    }
}

///An item that allows the user to add and remove traits from a fish at their own discretion.
// tg: code/modules/fishing/fishing_equipment.dm:714
public partial class obj_item_fish_genegun : obj_item
{
    // tg: code/modules/fishing/fishing_equipment.dm:735
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_eyestab)));
        return dot;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:739
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        if (loaded_injector == null)
        {
            Dm.append(ref dot, span_info(I18n.T("It's currently unloaded. Insert a syringe or fish gene injector.")));
            return dot;
        }
        string info = span_info(I18n.T("It's currently loaded with []. Use it to ", loaded_injector));
        if (istype(loaded_injector, typeof(obj_item_reagent_containers_syringe)))
            info += span_info(I18n.T("[] a gene from a fish or aquatic lifeform.", EXAMINE_HINT(I18n.T("extract"))));
        else
            info += span_info(I18n.T("[] the gene in a fish or aquatic lifeform.", EXAMINE_HINT(I18n.T("inject"))));
        Dm.append(ref dot, info);
        return dot;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:752
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = base_icon_state as string;
        if (loaded_injector == null)
            return dot;
        icon_state += istype(loaded_injector, typeof(obj_item_reagent_containers_syringe)) ? "_extract" : "_inject";
        return dot;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:759
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (loaded_injector == null)
        {
            balloon_alert(user, I18n.T("gene-gun is empty!"));
            return null;
        }
        obj_item loaded = loaded_injector;
        await loaded.forceMove(drop_location() as atom); //this will unset the loaded_injector variable
        if (Dm.truthy(await IsReachableBy(user))) //check that the user can actually reach the loaded injector (telekinesis yadda yadda)
            await user.put_in_hands(loaded);
        balloon_alert(user, I18n.T("gene-gun unloaded"));
        playsound(this, Dm.resource("sound/items/weapons/gun/general/magazine_remove_full.ogg"), 30, TRUE);
        return null;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:770
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == loaded_injector)
        {
            loaded_injector = null;
            update_appearance(UPDATE_ICON);
        }
        return dot;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:776
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item item = null, DmList modifiers = null, params object[] __extra)
    {
        bool is_syringe = istype(item, typeof(obj_item_reagent_containers_syringe));
        if (!is_syringe && !istype(item, typeof(obj_item_fish_gene)))
            return NONE;
        if (loaded_injector != null)
        {
            to_chat(user, span_warning(I18n.T("[] already has [] loaded in it.", this, loaded_injector)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (is_syringe && Dm.truthy((item.reagents as datum_reagents).total_volume))
        {
            to_chat(user, span_warning(I18n.T("[] cannot accept a syringe that isn't empty. Empty it first.", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(item, this)))
        {
            to_chat(user, span_warning(I18n.T("[] is stuck to your hands.", item)));
            return ITEM_INTERACT_BLOCKING;
        }
        to_chat(user, span_info(I18n.T("You load [] into [].", item, this)));
        loaded_injector = item;
        update_appearance(UPDATE_ICON);
        playsound(this, Dm.resource("sound/items/weapons/gun/general/magazine_insert_full.ogg"), 30, TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:797
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isfish(interacting_with))
            return NONE;
        if (loaded_injector == null)
        {
            balloon_alert(user, I18n.T("gene-gun is empty!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(Dm.bitand(interacting_with.flags_1, HOLOGRAM_1)))
        {
            to_chat(user, span_warning(I18n.T("[] is incompatible with []", interacting_with, this)));
            return ITEM_INTERACT_BLOCKING;
        }
        obj_item_fish fish = (obj_item_fish)interacting_with;
        bool is_syringe = istype(loaded_injector, typeof(obj_item_reagent_containers_syringe));
        if (Dm.eq(fish.status, FISH_DEAD))
        {
            to_chat(user, span_warning(I18n.T("[] cannot [] the deceased [].", this, is_syringe ? I18n.T("extract traits from") : I18n.T("inject traits into"), fish.name)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!is_syringe)
        {
            obj_item_fish_gene injector = (obj_item_fish_gene)loaded_injector;
            return await injector.inject_into_fish(fish, user, this);
        }

        if (!Dm.truthy(length(fish.fish_traits)))
        {
            to_chat(user, span_warning(I18n.T("[] has no traits that can be extracted from!", fish)));
            return ITEM_INTERACT_BLOCKING;
        }

        DmList choices = Dm.list();
        foreach (Type trait_type in Dm.each<Type>(fish.fish_traits))
            choices[Dm.initial(trait_type, (datum_fish_trait p) => p.name)] = trait_type;
        object choice = await tgui_input_list(user, I18n.T("Choose a trait to extract"), I18n.T("Fish Trait Extraction"), choices);
        if (!Dm.truthy(choice) || QDELETED(fish) || !Dm.truthy(user.is_holding(this)) || !Dm.truthy(await fish.IsReachableBy(user)))
            return ITEM_INTERACT_BLOCKING;

        if (!istype(loaded_injector, typeof(obj_item_reagent_containers_syringe))) //The syringe was taken out
        {
            to_chat(user, span_warning(I18n.T("[] is not loaded with an syringe to extract fish traits with.", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.eq(fish.status, FISH_DEAD))
        {
            to_chat(user, span_warning(I18n.T("[] cannot extract traits from the deceased [].", this, fish.name)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.@in(choices[choice], fish.fish_traits))
        {
            to_chat(user, span_warning(I18n.T("[] doesn't seem to have the \"[]\" trait anymore.", fish, choice)));
            return ITEM_INTERACT_BLOCKING;
        }

        QDEL_NULL(ref loaded_injector);
        Type chosen_type = choices[choice] as Type;
        datum_fish_trait trait = GLOB.fish_traits[chosen_type] as datum_fish_trait;
        await trait.remove_from_fish(fish);
        loaded_injector = await Dm.new_async<obj_item_fish_gene>(this, chosen_type);

        user.visible_message(span_notice(I18n.T("[] injects [] with [].", user, fish, this)), span_notice(I18n.T("You extract the \"[]\" trait into [].", Dm.initial(chosen_type, (datum_fish_trait p) => p.name), fish)));
        if (HAS_TRAIT(fish, TRAIT_FISH_GENEGUNNED))
            await fish.set_status(FISH_DEAD);
        ADD_TRAIT(fish, TRAIT_FISH_GENEGUNNED, TRAIT_GENERIC);
        playsound(fish, Dm.resource("sound/items/hypospray.ogg"), 30, TRUE);
        update_appearance(UPDATE_ICON);
        return ITEM_INTERACT_SUCCESS;
    }
}

///The injector for the fish trait. Can be used on its own without a fish gene-gun as well.
// tg: code/modules/fishing/fishing_equipment.dm:847
public partial class obj_item_fish_gene : obj_item
{
    // GAP(types): tg's `var/datum/fish_trait/trait_type` (:859) holds the trait's typepath (the injector reads its name with ::), so
    // the var is declared here as a Type and the var block with it.
    // tg: code/modules/fishing/fishing_equipment.dm:859
    public Type trait_type;

    // tg: code/modules/fishing/fishing_equipment.dm:848
    public obj_item_fish_gene()
    {
        name = "fish trait injector";
        icon = Dm.resource("icons/obj/fishing.dmi");
        icon_state = "fish_trait_injector";
        desc = "A single-use injector containing a specific trait that can be used on any (living) fish compatible with it.";
        w_class = 1;
        inhand_icon_state = "dnainjector";
        worn_icon_state = "pen";
        lefthand_file = Dm.resource("icons/mob/inhands/equipment/medical_lefthand.dmi");
        righthand_file = Dm.resource("icons/mob/inhands/equipment/medical_righthand.dmi");
        throw_speed = 3;
        throw_range = 5;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:861
    [DmParams("mapload", "trait_type")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Type new_trait_type = Dm.arg<Type>(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if (new_trait_type != null)
            trait_type = new_trait_type;
        if (trait_type != null)
            update_appearance(UPDATE_NAME);
        return dot;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:868
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        name = I18n.T("fish trait injector ([])", Dm.initial(trait_type, (datum_fish_trait p) => p.name));
        return dot;
    }

    // tg: code/modules/fishing/fishing_equipment.dm:872
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isfish(interacting_with))
            return NONE;
        if (Dm.truthy(Dm.bitand(interacting_with.flags_1, HOLOGRAM_1)))
        {
            to_chat(user, span_warning(I18n.T("[] is incompatible with []", interacting_with, this)));
            return ITEM_INTERACT_BLOCKING;
        }
        obj_item_fish fish = (obj_item_fish)interacting_with;
        if (Dm.eq(fish.status, FISH_DEAD))
        {
            to_chat(user, span_warning(I18n.T("[] cannot inject traits into the deceased [].", this, fish.name)));
            return ITEM_INTERACT_BLOCKING;
        }
        return await inject_into_fish(fish, user, this);
    }

    // tg: code/modules/fishing/fishing_equipment.dm:884
    public virtual async DmTask<object> inject_into_fish(obj_item_fish fish = null, mob_living user = null, obj_item tool = null)
    {
        tool ??= this;
        datum_fish_trait trait = GLOB.fish_traits[trait_type] as datum_fish_trait;
        if (!Dm.truthy(await trait.apply_to_fish(fish, initial: FALSE)))
        {
            to_chat(user, span_warning(I18n.T("You can't inject the \"[]\" trait into []. [] either [] it or [] incompatible with it.", Dm.initial(trait_type, (datum_fish_trait p) => p.name), fish, fish.p_They(), fish.p_have(), fish.p_are())));
            return ITEM_INTERACT_BLOCKING;
        }
        user.visible_message(span_notice(I18n.T("[] injects [] with [].", user, fish, tool)), span_notice(I18n.T("You inject the \"[]\" trait into [].", Dm.initial(trait_type, (datum_fish_trait p) => p.name), fish)));
        qdel(this);
        if (HAS_TRAIT(fish, TRAIT_FISH_GENEGUNNED))
            await fish.set_status(FISH_DEAD);
        ADD_TRAIT(fish, TRAIT_FISH_GENEGUNNED, TRAIT_GENERIC);
        playsound(fish, Dm.resource("sound/items/hypospray.ogg"), 25, TRUE);
        return ITEM_INTERACT_SUCCESS;
    }
}
