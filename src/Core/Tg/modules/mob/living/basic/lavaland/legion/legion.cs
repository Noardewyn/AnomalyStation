// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/legion/legion.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * Avoids players while throwing skulls at them.
 * Legion skulls heal allies, bite enemies, and infest dying humans to make more legions.
 */
// tg: code/modules/mob/living/basic/lavaland/legion/legion.dm:5
public partial class mob_living_basic_mining_legion : mob_living_basic_mining
{
    // tg: code/modules/mob/living/basic/lavaland/legion/legion.dm:39
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_content_barfer)));
        DmList drops = get_loot_list() as DmList;
        if (Dm.truthy(Dm.length(drops)))
            Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), string_list(drops)));
        Waiting.NoSleep(assign_abilities());
        if (Dm.truthy(has_emissive))
            update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    /// Give the Legion its spells
    // tg: code/modules/mob/living/basic/lavaland/legion/legion.dm:50
    public virtual async DmTask assign_abilities()
    {
        datum_action_cooldown_mob_cooldown_skull_launcher skull_launcher = Dm.@new<datum_action_cooldown_mob_cooldown_skull_launcher>(this);
        await skull_launcher.Grant(this);
        skull_launcher.spawn_type = brood_type;
        (ai_controller as datum_ai_controller).set_blackboard_key(BB_TARGETED_ACTION, skull_launcher);
    }

    /// Create what we want to drop on death, in proc form so we can always return a static list
    // tg: code/modules/mob/living/basic/lavaland/legion/legion.dm:57
    public virtual object get_loot_list()
    {
        return TgState.Current.mob_living_basic_mining_legion__get_loot_list__death_loot ??= Dm.list(typeof(obj_item_organ_monster_core_regenerative_core_legion));
    }

    // tg: code/modules/mob/living/basic/lavaland/legion/legion.dm:61
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone != stored_mob)
            return dot;
        UnregisterSignal(stored_mob, COMSIG_LIVING_REVIVE);
        (ai_controller as datum_ai_controller).clear_blackboard_key(BB_LEGION_CORPSE);
        stored_mob.remove_status_effect(typeof(datum_status_effect_grouped_stasis), STASIS_LEGION_EATEN);
        await stored_mob.add_mood_event("legion_core", typeof(datum_mood_event_healsbadman_long_term)); // This will still probably mostly be gone before you are alive
        stored_mob = null;
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/legion/legion.dm:71
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (stored_mob == null)
            Dm.@new(corpse_type, loc);
        return await base.death(gibbed, __extra);
    }

    // tg: code/modules/mob/living/basic/lavaland/legion/legion.dm:76
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        if (stored_mob != null && (!HAS_TRAIT(stored_mob, TRAIT_LESSER_HUMANOID) || stored_mob == GLOB.the_one_and_only_punpun))
            name = stored_mob.real_name as string;
        else
            name = Dm.initial(this, (mob_living_basic_mining_legion p) => p.name);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/legion/legion.dm:83
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        desc = Dm.initial(this, (mob_living_basic_mining_legion p) => p.desc);
        if (stored_mob == null || !Dm.truthy(desc_word_to_replace))
            return dot;
        object species_name = ((stored_mob as mob_living_carbon)?.dna?.species as datum_species)?.name;
        object replacement = Dm.truthy(LOWER_TEXT(species_name)) ? LOWER_TEXT(species_name) : Dm.initial(stored_mob, (mob_living p) => p.name);
        desc = replacetext(desc, desc_word_to_replace, replacement) as string;
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/legion/legion.dm:92
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (stat != DEAD && Dm.truthy(has_emissive)) // Shouldn't really happen but just in case
            Dm.Deferred("code/modules/mob/living/basic/lavaland/legion/legion.dm:95", "update_overlays: emissive_appearance(icon, \"[icon_living]_e\", src), the legion's glow in the dark (DEVIATION DV12)");
        return dot;
    }

    /// Put a corpse in this guy
    // tg: code/modules/mob/living/basic/lavaland/legion/legion.dm:98
    public virtual async DmTask consume(mob_living_carbon_human consumed = null)
    {
        Dm.@new<obj_effect_gibspawner_generic>(consumed.loc);
        gender = consumed.gender;
        consumed.investigate_log("has been killed by hivelord infestation.", INVESTIGATE_DEATHS);
        await consumed.death();
        consumed.extinguish_mob();
        await consumed.fully_heal(HEAL_DAMAGE);
        await consumed.apply_status_effect(typeof(datum_status_effect_grouped_stasis), STASIS_LEGION_EATEN);
        RegisterSignal(consumed, COMSIG_LIVING_REVIVE, nameof(on_consumed_revive));
        await consumed.forceMove(this);
        datum_ai_controller controller = ai_controller as datum_ai_controller;
        controller?.set_blackboard_key(BB_LEGION_CORPSE, consumed);
        controller?.set_blackboard_key(BB_LEGION_RECENT_LINES, consumed.copy_recent_speech(line_chance: 80));
        stored_mob = consumed;
        visible_message(span_warning(I18n.T("[] staggers to [] feet!", this, p_their())));
        update_appearance(Dm.bitor(UPDATE_NAME, UPDATE_DESC));
        if (prob(75))
            return;
        // Congratulations you have won a special prize: cancer
        obj_item_organ_legion_tumour cancer = Dm.@new<obj_item_organ_legion_tumour>();
        await cancer.Insert(consumed, special: TRUE, movement_flags: DELETE_IF_REPLACED);
    }

    /// A Legion which only drops skeletons instead of corpses which might have fun loot, so it cannot be farmed
    // tg: code/modules/mob/living/basic/lavaland/legion/legion.dm:121
    [SignalHandler]
    public virtual void on_consumed_revive(object full_heal_flags = null)
    {
        Waiting.NoSleep(gib());
    }
}

/// Like a Legion but it's an adorable snowman
// tg: code/modules/mob/living/basic/lavaland/legion/legion.dm:130
public partial class mob_living_basic_mining_legion_snow : mob_living_basic_mining_legion
{
    // tg: code/modules/mob/living/basic/lavaland/legion/legion.dm:142
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_appearance_on_aggro), Dm.named("aggro_state", "snowlegion_alive"))); // Surprise! I was real!
        return dot;
    }
}

/// Like a Legion but larger and spawns regular Legions, not currently used anywhere and very soulful
// tg: code/modules/mob/living/basic/lavaland/legion/legion.dm:164
public partial class mob_living_basic_mining_legion_large : mob_living_basic_mining_legion
{
    // tg: code/modules/mob/living/basic/lavaland/legion/legion.dm:183
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_spawner),
            Dm.named("spawn_types", Dm.list(typeof(mob_living_basic_mining_legion))),
            Dm.named("spawn_time", 20 * SECONDS),
            Dm.named("max_spawned", 3),
            Dm.named("spawn_text", "peels itself off from"),
            Dm.named("faction", faction),
            Dm.named("spawner_logic", SPAWN_CONTINUOUS_BEHAVIOR)));
        return dot;
    }

    /// Create what we want to drop on death, in proc form so we can always return a static list
    // tg: code/modules/mob/living/basic/lavaland/legion/legion.dm:196
    public override object get_loot_list()
    {
        return TgState.Current.mob_living_basic_mining_legion_large__get_loot_list__death_loot ??= Dm.list(Dm.kv(typeof(obj_item_organ_monster_core_regenerative_core_legion), 3), Dm.kv(typeof(obj_effect_mob_spawn_corpse_human_legioninfested), 4));
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/lavaland/legion/legion.dm:58 (var/static/list/death_loot)
    internal DmList mob_living_basic_mining_legion__get_loot_list__death_loot;
    // tg: code/modules/mob/living/basic/lavaland/legion/legion.dm:197 (var/static/list/death_loot)
    internal DmList mob_living_basic_mining_legion_large__get_loot_list__death_loot;
}
