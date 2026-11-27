// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/legion/legion_brood.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// A spooky skull which heals lavaland mobs, attacks miners, and infests their bodies
// tg: code/modules/mob/living/basic/lavaland/legion/legion_brood.dm:2
public partial class mob_living_basic_mining_legion_brood : mob_living_basic_mining
{
    // tg: code/modules/mob/living/basic/lavaland/legion/legion_brood.dm:41
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_PERMANENTLY_MORTAL, INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_flying)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_swarming), Dm.named("max_x", 12), Dm.named("max_y", 12)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_clickbox), Dm.named("icon_state", "sphere"), Dm.named("max_scale", 2)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(death)), 10 * SECONDS, file: "code/modules/mob/living/basic/lavaland/legion/legion_brood.dm", line: 47);
        if (random_states <= 1)
        {
            if (Dm.truthy(has_emissive))
                update_appearance(UPDATE_OVERLAYS);
            return dot;
        }
        icon_state = Dm.text("[]_[]", Dm.initial(this, (mob_living_basic_mining_legion_brood p) => p.icon_state), rand(1, random_states));
        icon_living = icon_state;
        icon_dead = icon_state;
        update_appearance();
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/legion/legion_brood.dm:57
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (!Dm.truthy(gibbed))
            Dm.@new<obj_effect_temp_visual_despawn_effect>(get_turf(this), /* copy_from = */ this);
        return await base.death(gibbed, __extra);
    }

    // tg: code/modules/mob/living/basic/lavaland/legion/legion_brood.dm:62
    public override void add_ranged_armour(DmList vulnerable_projectiles = null)
    {
        return;
    }

    // tg: code/modules/mob/living/basic/lavaland/legion/legion_brood.dm:65
    public override async DmTask<object> melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        if (ishuman(target) && IS_UNCONSCIOUS(target))
        {
            await infest(target as mob_living_carbon_human);
            return null;
        }

        mob_living mob_creator = created_by?.resolve() as mob_living;
        if (mob_creator == null)
            created_by = null;

        if (isliving(target) && Dm.truthy(faction_check_atom(target as atom_movable)) && !istype(target, mob_creator?.type))
        {
            visible_message(span_warning(I18n.T("[] melds with []'s flesh!", this, target)));
            await (target as mob_living).apply_status_effect(typeof(datum_status_effect_regenerative_core));
            Dm.@new<obj_effect_temp_visual_heal>(get_turf(target), COLOR_HEALING_CYAN);
            await death();
            return null;
        }
        return await base.melee_attack(target, modifiers, ignore_cooldown);
    }

    /// Turn the targeted mob into one of us
    // tg: code/modules/mob/living/basic/lavaland/legion/legion_brood.dm:83
    public virtual async DmTask infest(mob_living_carbon_human target = null)
    {
        visible_message(span_warning(I18n.T("[] burrows into the flesh of []!", name, target)));
        Type spawn_type = get_legion_type(target) as Type;
        mob_living_basic_mining_legion new_legion = Dm.@new(spawn_type, loc) as mob_living_basic_mining_legion;
        await new_legion.consume(target);
        new_legion.set_faction(get_faction());
        qdel(this);
    }

    /// Returns the kind of legion we make out of the target
    // tg: code/modules/mob/living/basic/lavaland/legion/legion_brood.dm:92
    public virtual object get_legion_type(mob_living target = null)
    {
        if (HAS_TRAIT(target, TRAIT_LESSER_HUMANOID))
            return typeof(mob_living_basic_mining_legion_lesser);
        if (HAS_TRAIT(target, TRAIT_DWARF))
            return typeof(mob_living_basic_mining_legion_dwarf);
        return typeof(mob_living_basic_mining_legion);
    }

    /// Sets someone as our creator, mostly so you can't use skulls to heal yourself
    // tg: code/modules/mob/living/basic/lavaland/legion/legion_brood.dm:100
    public virtual void assign_creator(mob_living creator = null, float? copy_full_faction = null)
    {
        copy_full_faction ??= TRUE;
        if (Dm.truthy(copy_full_faction))
            set_faction(creator.get_faction());
        else
            add_ally(creator);
        created_by = WEAKREF(creator) as datum_weakref;
        RegisterSignal(creator, COMSIG_QDELETING, nameof(creator_destroyed));
        if (ai_controller == null)
            return;

        datum_ai_controller controller = ai_controller as datum_ai_controller;
        controller.set_blackboard_key(BB_LEGION_BROOD_CREATOR, creator);
        if (creator.ai_controller == null)
            return;

        datum_ai_controller creator_controller = creator.ai_controller as datum_ai_controller;
        // Inherit our creator's target and reinforcement requests
        controller.set_blackboard_key(BB_CURRENT_TARGET, creator_controller.blackboard[BB_CURRENT_TARGET]);
        // Own and track each inherited target independently of our creator's controller
        DmList reinforcement_requests = creator_controller.blackboard[BB_MINING_MOB_REINFORCEMENTS_REQUESTS] as DmList;
        foreach (object target in Dm.each(reinforcement_requests))
        {
            DmList request_times = reinforcement_requests[target] as DmList;
            controller.set_blackboard_key_assoc_lazylist(BB_MINING_MOB_REINFORCEMENTS_REQUESTS, target, request_times.Copy());
        }
    }

    /// Reference handling
    // tg: code/modules/mob/living/basic/lavaland/legion/legion_brood.dm:123
    [SignalHandler]
    public virtual void creator_destroyed()
    {
        created_by = null;
    }

    // tg: code/modules/mob/living/basic/lavaland/legion/legion_brood.dm:127
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(has_emissive))
            Dm.Deferred("code/modules/mob/living/basic/lavaland/legion/legion_brood.dm:130", "update_overlays: emissive_appearance(icon, \"[icon_living]_e\", src), the skull's glow in the dark (DEVIATION DV12)");
        return dot;
    }
}

/// Like the Legion's summoned skull but funnier (it's snow now)
// tg: code/modules/mob/living/basic/lavaland/legion/legion_brood.dm:133
public partial class mob_living_basic_mining_legion_brood_snow : mob_living_basic_mining_legion_brood
{
    // tg: code/modules/mob/living/basic/lavaland/legion/legion_brood.dm:142
    public override object get_legion_type(mob_living target = null)
    {
        if (HAS_TRAIT(target, TRAIT_LESSER_HUMANOID))
            return typeof(mob_living_basic_mining_legion_lesser_snow);
        return typeof(mob_living_basic_mining_legion_snow);
    }
}
