// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/mining_loot/megafauna/ash_drake.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated. The drake armour's and helm's drake mesh on the body (equipment_bodypart_texture)
// is not ported and is a row, as the bone armour's is.

// Ash drake suit

// tg: code/modules/mining/lavaland/mining_loot/megafauna/ash_drake.dm:3
public partial class obj_item_drake_remains : obj_item
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/ash_drake.dm:10
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_shared_particles(typeof(particles_bonfire));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/ash_drake.dm:14
    public override object Destroy(float? force = null)
    {
        remove_shared_particles(typeof(particles_bonfire));
        return base.Destroy(force);
    }
}

// tg: code/modules/mining/lavaland/mining_loot/megafauna/ash_drake.dm:18
public partial class obj_item_clothing_suit_hooded_cloak_drake : obj_item_clothing_suit_hooded_cloak
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/ash_drake.dm:44
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddElement(/datum/element/equipment_bodypart_texture, BODY_ZONE_CHEST, /datum/bodypart_texture/mesh/drake): the body textures are a row
        allowed = GLOB.mining_suit_allowed;
        return dot;
    }
}

// tg: code/modules/mining/lavaland/mining_loot/megafauna/ash_drake.dm:49
public partial class obj_item_clothing_head_hooded_cloakhood_drake : obj_item_clothing_head_hooded_cloakhood
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/ash_drake.dm:63
    // AddElement(/datum/element/equipment_bodypart_texture, BODY_ZONE_HEAD, /datum/bodypart_texture/mesh/drake): the body textures are a row
    public override object Initialize(object mapload = null, params object[] __extra) => base.Initialize(mapload, __extra);
}

// Spectral blade

// tg: code/modules/mining/lavaland/mining_loot/megafauna/ash_drake.dm:69
public partial class obj_item_melee_ghost_sword : obj_item_melee
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/ash_drake.dm:93
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        spirits = Dm.list();
        START_PROCESSING(SSobj, this);
        Waiting.NoSleep(SSpoints_of_interest.make_point_of_interest(this));
        alt_continuous = string_list(alt_continuous) as DmList;
        alt_simple = string_list(alt_simple) as DmList;
        Waiting.NoSleep(AddComponent(typeof(datum_component_alternative_sharpness), SHARP_POINTY, alt_continuous, alt_simple));
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_butchering),
            Dm.named("speed", 15 * SECONDS),
            Dm.named("effectiveness", 90)));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/ash_drake.dm:107
    public override object Destroy(float? force = null)
    {
        foreach (mob_dead_observer ghost in Dm.each<mob_dead_observer>(spirits))
            ghost.RemoveInvisibility(type);
        spirits.Cut();
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/ash_drake.dm:114
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!(summon_cooldown <= world.time)) // COOLDOWN_FINISHED(src, summon_cooldown), inline (rules 11.1 (a))
        {
            to_chat(user, span_warning(I18n.T("You just recently called out for aid. You don't want to annoy the spirits!")));
            return null;
        }

        summon_cooldown = world.time + 60 * SECONDS; // COOLDOWN_START(src, summon_cooldown, 60 SECONDS)
        to_chat(user, span_notice(I18n.T("You call out for aid, attempting to summon spirits to your side.")));
        notify_ghosts(
            I18n.T("[] is raising [] []!", user.real_name, user.p_their(), name),
            source: user,
            ignore_key: POLL_IGNORE_SPECTRAL_BLADE,
            header: "Spectral blade"
        );
        return null;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/ash_drake.dm:127
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        Waiting.NoSleep(ghost_check());
        return null;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/ash_drake.dm:130
    public virtual async DmTask<object> ghost_check()
    {
        turf cur_turf = get_turf(this);
        DmList contents = cur_turf.get_all_contents() as DmList;
        object current_spirits = Dm.list();
        foreach (atom random_thing in Dm.each<atom>(contents))
            await random_thing.transfer_observers_to(this);

        foreach (mob_dead_observer ghost in Dm.each<mob_dead_observer>(orbiters?.orbiter_list))
        {
            ghost.SetInvisibility(INVISIBILITY_NONE, id: type, priority: INVISIBILITY_PRIORITY_BASIC_ANTI_INVISIBILITY);
            Dm.combine(ref current_spirits, ghost);
        }

        foreach (mob_dead_observer ghost in Dm.each<mob_dead_observer>(Dm.sub(spirits, current_spirits)))
            ghost.RemoveInvisibility(type);

        float new_force = clamp((Dm.length(current_spirits) * 4), 0, 75);
        float old_force = clamp((Dm.length(spirits) * 4), 0, 75);
        force += new_force - old_force;
        spirits = current_spirits as DmList;
        return Dm.length(spirits);
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/ash_drake.dm:151
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        object ghost_counter = await ghost_check();
        user.visible_message(span_danger(I18n.T("[] strikes with the force of [] vengeful spirits!", user, ghost_counter)));
        return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/ash_drake.dm:156
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        damage_type ??= BRUTE;
        float ghost_counter = Dm.num(await ghost_check());
        final_block_chance += clamp((ghost_counter * 5), 0, 75);
        owner.visible_message(span_danger(I18n.T("[] is protected by a ring of [] ghosts!", owner, ghost_counter)));
        return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
    }
}

// Dragon's blood

// tg: code/modules/mining/lavaland/mining_loot/megafauna/ash_drake.dm:164
public partial class obj_item_dragons_blood : obj_item
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/ash_drake.dm:170
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!(user is mob_living_carbon_human))
            return null;

        mob_living_carbon_human consumer = user as mob_living_carbon_human;
        float random = rand(1, 4);

        if (random == 1)
        {
            to_chat(user, span_danger(I18n.T("Your appearance morphs to that of a very small humanoid ash dragon! You get to look like a freak with just a fragment of their abilities.")));
            consumer.dna.features = Dm.list(
                Dm.kv(FEATURE_MUTANT_COLOR, "#A02720"),
                Dm.kv(FEATURE_TAIL_LIZARD, "Dark Tiger"),
                Dm.kv(FEATURE_TAIL_CAT, "None"),
                Dm.kv(FEATURE_SNOUT, "Sharp"),
                Dm.kv(FEATURE_HORNS, "Curled"),
                Dm.kv(FEATURE_EARS, "None"),
                Dm.kv(FEATURE_WINGS, "None"),
                Dm.kv(FEATURE_FRILLS, "None"),
                Dm.kv(FEATURE_SPINES, "Long"),
                Dm.kv(FEATURE_LIZARD_MARKINGS, "Dark Tiger Body"),
                Dm.kv(FEATURE_LEGS, DIGITIGRADE_LEGS)
            );
            await consumer.set_eye_color("#FEE5A3");
            consumer.set_species(typeof(datum_species_lizard));
            await consumer.dna.add_mutation(typeof(datum_mutation_firebreath), MUTATION_SOURCE_MUTATOR);
            obj_item_organ_wings_dragon wings = Dm.@new<obj_item_organ_wings_dragon>();
            await wings.Insert(consumer);
        }
        else if (random == 2)
        {
            to_chat(user, span_danger(I18n.T("Your flesh begins to melt! Miraculously, you seem fine otherwise.")));
            consumer.set_species(typeof(datum_species_skeleton));
        }
        else if (random == 3)
        {
            to_chat(user, span_danger(I18n.T("Power courses through you! You can now shift your form at will.")));
            datum_action_cooldown_spell_shapeshift_dragon dragon_shapeshift = Dm.@new<datum_action_cooldown_spell_shapeshift_dragon>((object)user.mind ?? user);
            await dragon_shapeshift.Grant(user);
        }
        else if (random == 4)
        {
            to_chat(user, span_danger(I18n.T("You feel like you could walk straight through lava now.")));
            ADD_TRAIT(user, TRAIT_LAVA_IMMUNE, type);
        }

        playsound(user, Dm.resource("sound/items/drink.ogg"), 30, TRUE);
        qdel(this);
        return null;
    }
}

// Lava staff

// tg: code/modules/mining/lavaland/mining_loot/megafauna/ash_drake.dm:218
public partial class obj_item_lava_staff : obj_item
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/ash_drake.dm:239  var/static/list/banned_turfs
    public static DmList banned_turfs => TgState.Current.obj_item_lava_staff__banned_turfs ??= typecacheof(Dm.list(typeof(turf_open_space), typeof(turf_closed))) as DmList;

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/ash_drake.dm:242
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (interacting_with.atom_storage != null || (HAS_TRAIT(interacting_with, TRAIT_COMBAT_MODE_SKIP_INTERACTION) && Dm.truthy(user.combat_mode))) // SHOULD_SKIP_INTERACTION, inline
            return NONE;

        return await ranged_interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/ash_drake.dm:248
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!(use_cooldown <= world.time)) // COOLDOWN_FINISHED(src, use_cooldown), inline
            return NONE;

        if (Dm.truthy(is_type_in_typecache(interacting_with, banned_turfs)))
            return NONE;

        if (!Dm.@in(interacting_with, view(user.client?.view, get_turf(user))))
            return NONE;

        turf_open target_turf = get_turf(interacting_with) as turf_open;
        if (!(target_turf is turf_open))
            return NONE;

        if (islava(target_turf))
        {
            string old_name__1 = target_turf.name;
            if (Dm.truthy(await target_turf.TerraformTurf(reset_turf_type, flags: CHANGETURF_INHERIT_AIR)))
                return ITEM_INTERACT_FAILURE;

            use_cooldown = world.time + reset_cooldown; // COOLDOWN_START(src, use_cooldown, reset_cooldown)
            user.visible_message(span_danger(I18n.T("[] turns the [] into []!", user, old_name__1, reset_string)));
            playsound(target_turf, Dm.resource("sound/effects/magic/fireball.ogg"), 200, TRUE);
            return ITEM_INTERACT_SUCCESS;
        }

        obj_effect_temp_visual_lavastaff lava_visual = Dm.@new<obj_effect_temp_visual_lavastaff>(target_turf);
        lava_visual.alpha = 0;
        animate(lava_visual, Dm.named("alpha", 255), Dm.named("time", create_delay));
        user.visible_message(span_danger(I18n.T("[] points [] at []!", user, this, target_turf)));
        use_cooldown = world.time + (create_delay + 1); // COOLDOWN_START(src, use_cooldown, create_delay + 1)

        if (!Dm.truthy(await do_after(user, create_delay, target_turf)))
        {
            balloon_alert(user, "interrupted!");
            use_cooldown = 0; // COOLDOWN_RESET(src, use_cooldown)
            qdel(lava_visual);
            return ITEM_INTERACT_FAILURE;
        }

        string old_name = target_turf.name;
        if (!Dm.truthy(await target_turf.TerraformTurf(turf_type, flags: CHANGETURF_INHERIT_AIR)))
        {
            qdel(lava_visual);
            return ITEM_INTERACT_FAILURE;
        }
        target_turf = (turf_open)turf.dm_identity(target_turf); // TURF-IDENTITY: tg's target_turf names the cell, the lava from here on

        user.visible_message(span_danger(I18n.T("[] turns the [] into []!", user, old_name, transform_string)));
        message_admins(Dm.text("[] fired the lava staff at []", ADMIN_LOOKUPFLW(user), ADMIN_VERBOSEJMP(target_turf)));
        user.log_message(Dm.text("fired the lava staff at [].", AREACOORD(target_turf)), LOG_ATTACK);
        use_cooldown = world.time + create_cooldown; // COOLDOWN_START(src, use_cooldown, create_cooldown)
        playsound(target_turf, Dm.resource("sound/effects/magic/fireball.ogg"), 200, TRUE);
        qdel(lava_visual);
        return ITEM_INTERACT_SUCCESS;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/ash_drake.dm:239 (var/static, rules 10.6)
    internal DmList obj_item_lava_staff__banned_turfs;
}
