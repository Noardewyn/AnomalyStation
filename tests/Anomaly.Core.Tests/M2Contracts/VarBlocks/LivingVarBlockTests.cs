using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M2Contracts.VarBlocks;

/// <summary>
/// The var blocks of the M2 contracts step's living area (/mob/living, the status effects, the keybindings, the mob movespeed
/// modifiers, the blood types, SSmobs, the movetype_handler element): each type's [DmPath] and a handful of its tg
/// defaults, read from the prototypes with Dm.initial (constructor only, no world). Object-creating defaults are checked on
/// world-free C# instances (constructor plus dm_init_vars); the statics on a bare world's TgState.
/// </summary>
public class LivingVarBlockTests
{
    [Theory]
    [InlineData(typeof(mob_living), "/mob/living")]
    [InlineData(typeof(datum_status_effect), "/datum/status_effect")]
    [InlineData(typeof(datum_status_effect_grouped), "/datum/status_effect/grouped")]
    [InlineData(typeof(datum_status_effect_incapacitating), "/datum/status_effect/incapacitating")]
    [InlineData(typeof(datum_status_effect_incapacitating_stun), "/datum/status_effect/incapacitating/stun")]
    [InlineData(typeof(datum_status_effect_incapacitating_knockdown), "/datum/status_effect/incapacitating/knockdown")]
    [InlineData(typeof(datum_status_effect_incapacitating_immobilized), "/datum/status_effect/incapacitating/immobilized")]
    [InlineData(typeof(datum_status_effect_incapacitating_paralyzed), "/datum/status_effect/incapacitating/paralyzed")]
    [InlineData(typeof(datum_status_effect_incapacitating_incapacitated), "/datum/status_effect/incapacitating/incapacitated")]
    [InlineData(typeof(datum_status_effect_incapacitating_unconscious), "/datum/status_effect/incapacitating/unconscious")]
    [InlineData(typeof(datum_status_effect_incapacitating_sleeping), "/datum/status_effect/incapacitating/sleeping")]
    [InlineData(typeof(datum_status_effect_knocked_out), "/datum/status_effect/knocked_out")]
    [InlineData(typeof(datum_status_effect_staggered), "/datum/status_effect/staggered")]
    [InlineData(typeof(datum_status_effect_dazed), "/datum/status_effect/dazed")]
    [InlineData(typeof(datum_status_effect_no_side_kick), "/datum/status_effect/no_side_kick")]
    [InlineData(typeof(datum_status_effect_grouped_blindness), "/datum/status_effect/grouped/blindness")]
    [InlineData(typeof(datum_status_effect_temporary_blindness), "/datum/status_effect/temporary_blindness")]
    [InlineData(typeof(datum_keybinding), "/datum/keybinding")]
    [InlineData(typeof(datum_keybinding_living), "/datum/keybinding/living")]
    [InlineData(typeof(datum_keybinding_living_rest), "/datum/keybinding/living/rest")]
    [InlineData(typeof(datum_keybinding_living_toggle_combat_mode), "/datum/keybinding/living/toggle_combat_mode")]
    [InlineData(typeof(datum_keybinding_living_toggle_move_intent), "/datum/keybinding/living/toggle_move_intent")]
    [InlineData(typeof(datum_keybinding_living_toggle_move_intent_alternative), "/datum/keybinding/living/toggle_move_intent_alternative")]
    [InlineData(typeof(datum_blood_type), "/datum/blood_type")]
    [InlineData(typeof(datum_blood_type_human), "/datum/blood_type/human")]
    [InlineData(typeof(datum_blood_type_human_a_minus), "/datum/blood_type/human/a_minus")]
    [InlineData(typeof(datum_blood_type_human_a_plus), "/datum/blood_type/human/a_plus")]
    [InlineData(typeof(datum_blood_type_human_b_minus), "/datum/blood_type/human/b_minus")]
    [InlineData(typeof(datum_blood_type_human_b_plus), "/datum/blood_type/human/b_plus")]
    [InlineData(typeof(datum_blood_type_human_ab_minus), "/datum/blood_type/human/ab_minus")]
    [InlineData(typeof(datum_blood_type_human_ab_plus), "/datum/blood_type/human/ab_plus")]
    [InlineData(typeof(datum_blood_type_human_o_minus), "/datum/blood_type/human/o_minus")]
    [InlineData(typeof(datum_blood_type_human_o_plus), "/datum/blood_type/human/o_plus")]
    [InlineData(typeof(datum_controller_subsystem_mobs), "/datum/controller/subsystem/mobs")]
    [InlineData(typeof(datum_element_movetype_handler), "/datum/element/movetype_handler")]
    // type-only skeletons the var blocks name
    [InlineData(typeof(datum_hud_living), "/datum/hud/living")]
    [InlineData(typeof(atom_movable_screen_alert_status_effect), "/atom/movable/screen/alert/status_effect")]
    [InlineData(typeof(atom_movable_screen_alert_status_effect_asleep), "/atom/movable/screen/alert/status_effect/asleep")]
    [InlineData(typeof(atom_movable_screen_alert_status_effect_blind), "/atom/movable/screen/alert/status_effect/blind")]
    [InlineData(typeof(obj_effect_abstract_particle_holder), "/obj/effect/abstract/particle_holder")]
    [InlineData(typeof(obj_item_mob_holder), "/obj/item/mob_holder")]
    [InlineData(typeof(atom_movable_looking_holder), "/atom/movable/looking_holder")]
    [InlineData(typeof(datum_cell_tracker), "/datum/cell_tracker")]
    [InlineData(typeof(datum_mood), "/datum/mood")]
    [InlineData(typeof(datum_reagent_blood), "/datum/reagent/blood")]
    [InlineData(typeof(datum_reagent_iron), "/datum/reagent/iron")]
    [InlineData(typeof(turf_open_water), "/turf/open/water")]
    public void TypesCarryTheirTgPaths(Type type, string path)
    {
        Assert.Equal(path, DmTypes.PathOf(type));
    }

    [Theory]
        [InlineData(typeof(datum_movespeed_modifier_obesity), "/datum/movespeed_modifier/obesity")]
        [InlineData(typeof(datum_movespeed_modifier_monkey_reagent_speedmod), "/datum/movespeed_modifier/monkey_reagent_speedmod")]
        [InlineData(typeof(datum_movespeed_modifier_monkey_health_speedmod), "/datum/movespeed_modifier/monkey_health_speedmod")]
        [InlineData(typeof(datum_movespeed_modifier_monkey_temperature_speedmod), "/datum/movespeed_modifier/monkey_temperature_speedmod")]
        [InlineData(typeof(datum_movespeed_modifier_golem_hunger), "/datum/movespeed_modifier/golem_hunger")]
        [InlineData(typeof(datum_movespeed_modifier_slaughter), "/datum/movespeed_modifier/slaughter")]
        [InlineData(typeof(datum_movespeed_modifier_resonance), "/datum/movespeed_modifier/resonance")]
        [InlineData(typeof(datum_movespeed_modifier_basic_stamina_slowdown), "/datum/movespeed_modifier/basic_stamina_slowdown")]
        [InlineData(typeof(datum_movespeed_modifier_damage_slowdown), "/datum/movespeed_modifier/damage_slowdown")]
        [InlineData(typeof(datum_movespeed_modifier_equipment_speedmod), "/datum/movespeed_modifier/equipment_speedmod")]
        [InlineData(typeof(datum_movespeed_modifier_equipment_speedmod_immutable), "/datum/movespeed_modifier/equipment_speedmod/immutable")]
        [InlineData(typeof(datum_movespeed_modifier_grab_slowdown), "/datum/movespeed_modifier/grab_slowdown")]
        [InlineData(typeof(datum_movespeed_modifier_grab_slowdown_aggressive), "/datum/movespeed_modifier/grab_slowdown/aggressive")]
        [InlineData(typeof(datum_movespeed_modifier_grab_slowdown_neck), "/datum/movespeed_modifier/grab_slowdown/neck")]
        [InlineData(typeof(datum_movespeed_modifier_grab_slowdown_kill), "/datum/movespeed_modifier/grab_slowdown/kill")]
        [InlineData(typeof(datum_movespeed_modifier_config_walk_run), "/datum/movespeed_modifier/config_walk_run")]
        [InlineData(typeof(datum_movespeed_modifier_turf_slowdown), "/datum/movespeed_modifier/turf_slowdown")]
        [InlineData(typeof(datum_movespeed_modifier_bulky_drag), "/datum/movespeed_modifier/bulky_drag")]
        [InlineData(typeof(datum_movespeed_modifier_cold), "/datum/movespeed_modifier/cold")]
        [InlineData(typeof(datum_movespeed_modifier_staggered), "/datum/movespeed_modifier/staggered")]
        [InlineData(typeof(datum_movespeed_modifier_human_carry), "/datum/movespeed_modifier/human_carry")]
        [InlineData(typeof(datum_movespeed_modifier_limbless), "/datum/movespeed_modifier/limbless")]
        [InlineData(typeof(datum_movespeed_modifier_simplemob_varspeed), "/datum/movespeed_modifier/simplemob_varspeed")]
        [InlineData(typeof(datum_movespeed_modifier_fast_web), "/datum/movespeed_modifier/fast_web")]
        [InlineData(typeof(datum_movespeed_modifier_young_web), "/datum/movespeed_modifier/young_web")]
        [InlineData(typeof(datum_movespeed_modifier_spiderling_web), "/datum/movespeed_modifier/spiderling_web")]
        [InlineData(typeof(datum_movespeed_modifier_average_web), "/datum/movespeed_modifier/average_web")]
        [InlineData(typeof(datum_movespeed_modifier_below_average_web), "/datum/movespeed_modifier/below_average_web")]
        [InlineData(typeof(datum_movespeed_modifier_slow_web), "/datum/movespeed_modifier/slow_web")]
        [InlineData(typeof(datum_movespeed_modifier_viper_defensive), "/datum/movespeed_modifier/viper_defensive")]
        [InlineData(typeof(datum_movespeed_modifier_gravity), "/datum/movespeed_modifier/gravity")]
        [InlineData(typeof(datum_movespeed_modifier_carbon_softcrit), "/datum/movespeed_modifier/carbon_softcrit")]
        [InlineData(typeof(datum_movespeed_modifier_carbon_crawling), "/datum/movespeed_modifier/carbon_crawling")]
        [InlineData(typeof(datum_movespeed_modifier_mob_config_speedmod), "/datum/movespeed_modifier/mob_config_speedmod")]
        [InlineData(typeof(datum_movespeed_modifier_metabolicboost), "/datum/movespeed_modifier/metabolicboost")]
        [InlineData(typeof(datum_movespeed_modifier_dragon_rage), "/datum/movespeed_modifier/dragon_rage")]
        [InlineData(typeof(datum_movespeed_modifier_dragon_depression), "/datum/movespeed_modifier/dragon_depression")]
        [InlineData(typeof(datum_movespeed_modifier_morph_disguised), "/datum/movespeed_modifier/morph_disguised")]
        [InlineData(typeof(datum_movespeed_modifier_auto_wash), "/datum/movespeed_modifier/auto_wash")]
        [InlineData(typeof(datum_movespeed_modifier_player_spider_modifier), "/datum/movespeed_modifier/player_spider_modifier")]
        [InlineData(typeof(datum_movespeed_modifier_health_scaling_speed_buff), "/datum/movespeed_modifier/health_scaling_speed_buff")]
        [InlineData(typeof(datum_movespeed_modifier_alien_speed), "/datum/movespeed_modifier/alien_speed")]
        [InlineData(typeof(datum_movespeed_modifier_grown_killer_tomato), "/datum/movespeed_modifier/grown_killer_tomato")]
        [InlineData(typeof(datum_movespeed_modifier_goldgrub_mount), "/datum/movespeed_modifier/goldgrub_mount")]
        [InlineData(typeof(datum_movespeed_modifier_settler), "/datum/movespeed_modifier/settler")]
        [InlineData(typeof(datum_movespeed_modifier_basilisk_overheat), "/datum/movespeed_modifier/basilisk_overheat")]
        [InlineData(typeof(datum_movespeed_modifier_magic_ties), "/datum/movespeed_modifier/magic_ties")]
        [InlineData(typeof(datum_movespeed_modifier_fish_on_water), "/datum/movespeed_modifier/fish_on_water")]
        [InlineData(typeof(datum_movespeed_modifier_swimming_deep), "/datum/movespeed_modifier/swimming_deep")]
        [InlineData(typeof(datum_movespeed_modifier_tail_dragger), "/datum/movespeed_modifier/tail_dragger")]
    public void MobMovespeedModifiersCarryTheirTgPaths(Type type, string path)
    {
        Assert.Equal(path, DmTypes.PathOf(type));
    }

    [Fact]
    public void MobLivingDefaults()
    {
        // the overrides of the type block (living_defines.dm:2-8)
        Assert.Equal(25f, Dm.initial(typeof(mob_living), (mob p) => p.see_invisible));
        Assert.Equal(typeof(mob_living), Dm.initial(typeof(mob_living), (datum p) => p.abstract_type));
        Assert.Equal(10f, Dm.initial(typeof(mob_living), (atom_movable p) => p.pressure_resistance));
        Assert.Equal(typeof(datum_hud_living), Dm.initial(typeof(mob_living), (mob p) => p.hud_type));
        Assert.Equal(128f, Dm.initial(typeof(mob_living), (atom p) => p.interaction_flags_click));
        // the var declarations
        Assert.Equal(100f, Dm.initial(typeof(mob_living), (mob_living p) => p.maxHealth));
        Assert.Equal(100f, Dm.initial(typeof(mob_living), (mob_living p) => p.health));
        Assert.Equal(120f, Dm.initial(typeof(mob_living), (mob_living p) => p.max_stamina));
        Assert.Equal("run", Dm.initial(typeof(mob_living), (mob_living p) => p.move_intent));
        Assert.Equal(-0.05f, Dm.initial(typeof(mob_living), (mob_living p) => p.fire_stack_decay_rate));
        Assert.Equal(-30f, Dm.initial(typeof(mob_living), (mob_living p) => p.hardcrit_threshold));
        Assert.Equal(-100f, Dm.initial(typeof(mob_living), (mob_living p) => p.dead_threshold));
        Assert.Equal(127f, Dm.initial(typeof(mob_living), (mob_living p) => p.mobility_flags));
        Assert.True(Dm.eq(Dm.initial(typeof(mob_living), (mob_living p) => p.mob_size), 2));
        Assert.True(Dm.eq(Dm.initial(typeof(mob_living), (mob_living p) => p.combat_mode), 0));
        Assert.Equal("default", Dm.initial(typeof(mob_living), (mob_living p) => p.bubble_icon));
        Assert.Equal(3f, Dm.initial(typeof(mob_living), (mob_living p) => p.ventcrawl_layer));
        Assert.True(Dm.eq(Dm.initial(typeof(mob_living), (mob_living p) => p.held_lh), Dm.resource("icons/mob/inhands/pets_held_lh.dmi")));
        Assert.Equal(typeof(obj_item_mob_holder), Dm.initial(typeof(mob_living), (mob_living p) => p.inhand_holder_type));
        Assert.Equal(100f, Dm.initial(typeof(mob_living), (mob_living p) => p.stamina_regen_time));    // 10 SECONDS
        Assert.Equal(1f, Dm.initial(typeof(mob_living), (mob_living p) => p.eavesdrop_range));
        // runtime-initialized (dm_init_vars): null on the prototype, as initial() in DM
        Assert.Null(Dm.initial(typeof(mob_living), (mob_living p) => p.pipes_shown));
    }

    [Fact]
    public void StatusEffectDefaults()
    {
        Assert.True(Dm.eq(Dm.initial(typeof(datum_status_effect), (datum_status_effect p) => p.id), "effect"));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_status_effect), (datum_status_effect p) => p.duration), -1));
        Assert.Equal(10f, Dm.initial(typeof(datum_status_effect), (datum_status_effect p) => p.tick_interval));
        Assert.Equal(1f, Dm.initial(typeof(datum_status_effect), (datum_status_effect p) => p.status_type));
        Assert.Equal(typeof(atom_movable_screen_alert_status_effect), Dm.initial(typeof(datum_status_effect), (datum_status_effect p) => p.alert_type));
        Assert.Equal(0f, Dm.initial(typeof(datum_status_effect), (datum_status_effect p) => p.processing_speed));
        Assert.Equal(65536f, Dm.initial(typeof(datum_status_effect), (datum_status_effect p) => p.heal_flag_necessary));

        Assert.True(Dm.eq(Dm.initial(typeof(datum_status_effect_grouped), (datum_status_effect p) => p.id), "abstract"));
        Assert.Null(Dm.initial(typeof(datum_status_effect_grouped), (datum_status_effect p) => p.alert_type));
        Assert.Equal(0f, Dm.initial(typeof(datum_status_effect_grouped), (datum_status_effect p) => p.status_type));

        Assert.Equal(-1f, Dm.initial(typeof(datum_status_effect_incapacitating), (datum_status_effect p) => p.tick_interval));
        Assert.Equal(2f, Dm.initial(typeof(datum_status_effect_incapacitating), (datum_status_effect p) => p.status_type));
        Assert.Equal(2f, Dm.initial(typeof(datum_status_effect_incapacitating), (datum_status_effect p) => p.processing_speed));
        Assert.Equal(1f, Dm.initial(typeof(datum_status_effect_incapacitating), (datum_status_effect p) => p.remove_on_fullheal));
        Assert.Equal(131072f, Dm.initial(typeof(datum_status_effect_incapacitating), (datum_status_effect p) => p.heal_flag_necessary));
        Assert.Equal(66f, Dm.initial(typeof(datum_status_effect_incapacitating), (datum_status_effect_incapacitating p) => p.force_say_chance));
        Assert.Equal(0f, Dm.initial(typeof(datum_status_effect_incapacitating), (datum_status_effect_incapacitating p) => p.needs_update_stat));

        Assert.True(Dm.eq(Dm.initial(typeof(datum_status_effect_incapacitating_stun), (datum_status_effect p) => p.id), "stun"));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_status_effect_incapacitating_knockdown), (datum_status_effect p) => p.id), "knockdown"));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_status_effect_incapacitating_immobilized), (datum_status_effect p) => p.id), "immobilized"));
        Assert.Equal(0f, Dm.initial(typeof(datum_status_effect_incapacitating_immobilized), (datum_status_effect_incapacitating p) => p.force_say_chance));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_status_effect_incapacitating_paralyzed), (datum_status_effect p) => p.id), "paralyzed"));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_status_effect_incapacitating_incapacitated), (datum_status_effect p) => p.id), "incapacitated"));
        Assert.Equal(0f, Dm.initial(typeof(datum_status_effect_incapacitating_incapacitated), (datum_status_effect_incapacitating p) => p.force_say_chance));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_status_effect_incapacitating_unconscious), (datum_status_effect p) => p.id), "unconscious"));
        Assert.Equal(100f, Dm.initial(typeof(datum_status_effect_incapacitating_unconscious), (datum_status_effect_incapacitating p) => p.force_say_chance));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_status_effect_knocked_out), (datum_status_effect p) => p.id), "is_knocked_out"));
        Assert.Null(Dm.initial(typeof(datum_status_effect_knocked_out), (datum_status_effect p) => p.alert_type));

        Assert.True(Dm.eq(Dm.initial(typeof(datum_status_effect_incapacitating_sleeping), (datum_status_effect p) => p.id), "sleeping"));
        Assert.Equal(typeof(atom_movable_screen_alert_status_effect_asleep), Dm.initial(typeof(datum_status_effect_incapacitating_sleeping), (datum_status_effect p) => p.alert_type));
        Assert.Equal(1f, Dm.initial(typeof(datum_status_effect_incapacitating_sleeping), (datum_status_effect_incapacitating p) => p.needs_update_stat));
        Assert.Equal(20f, Dm.initial(typeof(datum_status_effect_incapacitating_sleeping), (datum_status_effect p) => p.tick_interval));
        Assert.Equal(100f, Dm.initial(typeof(datum_status_effect_incapacitating_sleeping), (datum_status_effect_incapacitating p) => p.force_say_chance));

        Assert.True(Dm.eq(Dm.initial(typeof(datum_status_effect_staggered), (datum_status_effect p) => p.id), "staggered"));
        Assert.Equal(8f, Dm.initial(typeof(datum_status_effect_staggered), (datum_status_effect p) => p.tick_interval));
        Assert.Null(Dm.initial(typeof(datum_status_effect_staggered), (datum_status_effect p) => p.alert_type));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_status_effect_dazed), (datum_status_effect p) => p.id), "dazed"));
        Assert.Equal(6f, Dm.initial(typeof(datum_status_effect_dazed), (datum_status_effect p) => p.tick_interval));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_status_effect_no_side_kick), (datum_status_effect p) => p.id), "no side kick"));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_status_effect_no_side_kick), (datum_status_effect p) => p.duration), 36));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_status_effect_grouped_blindness), (datum_status_effect p) => p.id), "blindness"));
        Assert.Equal(-1f, Dm.initial(typeof(datum_status_effect_grouped_blindness), (datum_status_effect p) => p.tick_interval));
        Assert.Equal(typeof(atom_movable_screen_alert_status_effect_blind), Dm.initial(typeof(datum_status_effect_grouped_blindness), (datum_status_effect p) => p.alert_type));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_status_effect_temporary_blindness), (datum_status_effect p) => p.id), "temporary_blindness"));
        Assert.Equal(20f, Dm.initial(typeof(datum_status_effect_temporary_blindness), (datum_status_effect p) => p.tick_interval));
    }

    [Fact]
    public void StatusEffectObjectCreatingDefaults()
    {
        var grouped = new datum_status_effect_grouped();
        grouped.dm_init_vars();
        Assert.Equal(0, grouped.sources.Count);

        var paralyzed = new datum_status_effect_incapacitating_paralyzed();
        paralyzed.dm_init_vars();
        Assert.Equal(new object[] { "" }, Dm.each(paralyzed.alter_phrases).Cast<object>().ToArray());

        var sleeping = new datum_status_effect_incapacitating_sleeping();
        sleeping.dm_init_vars();
        Assert.Equal(9, sleeping.alter_phrases.Count);
        Assert.Equal("mimimimimimi...", sleeping.alter_phrases[9]);
    }

    [Fact]
    public void BlindnessStaticsArePerWorld()
    {
        var w = new World();
        using (w.Enter())
        {
            Assert.Equal(new object[] { "removetrait perfect_sight", "addtrait perfect_sight" },
                Dm.each(datum_status_effect_grouped_blindness.update_signals).Cast<object>().ToArray());
            // TRAIT_STATUS_EFFECT(/datum/status_effect/knocked_out::id)
            Assert.Equal(new object[] { "quirk_trait", "is_knocked_out-trait" },
                Dm.each(datum_status_effect_grouped_blindness.blocking_sources).Cast<object>().ToArray());
        }
    }

    [Fact]
    public void KeybindingDefaults()
    {
        Assert.Equal("", Dm.initial(typeof(datum_keybinding), (datum_keybinding p) => p.description));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_keybinding), (datum_keybinding p) => p.category), "MISC"));
        Assert.Equal(999f, Dm.initial(typeof(datum_keybinding), (datum_keybinding p) => p.weight));
        Assert.Equal(1f, Dm.initial(typeof(datum_keybinding), (datum_keybinding p) => p.can_edit));
        Assert.Null(Dm.initial(typeof(datum_keybinding), (datum_keybinding p) => p.keybind_signal));

        Assert.True(Dm.eq(Dm.initial(typeof(datum_keybinding_living), (datum_keybinding p) => p.category), "HUMAN"));
        Assert.Equal(40f, Dm.initial(typeof(datum_keybinding_living), (datum_keybinding p) => p.weight));

        Assert.True(Dm.eq(Dm.initial(typeof(datum_keybinding_living_rest), (datum_keybinding p) => p.name), "rest"));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_keybinding_living_rest), (datum_keybinding p) => p.keybind_signal), "keybinding_living_rest_down"));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_keybinding_living_toggle_combat_mode), (datum_keybinding p) => p.full_name), "Toggle Combat Mode"));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_keybinding_living_toggle_combat_mode), (datum_keybinding p) => p.keybind_signal), "keybinding_living_toggle_combat_down"));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_keybinding_living_toggle_move_intent), (datum_keybinding p) => p.name), "toggle_move_intent"));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_keybinding_living_toggle_move_intent), (datum_keybinding p) => p.keybind_signal), "keybinding_mob_togglemoveintent_down"));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_keybinding_living_toggle_move_intent_alternative), (datum_keybinding p) => p.name), "toggle_move_intent_alt"));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_keybinding_living_toggle_move_intent_alternative), (datum_keybinding p) => p.keybind_signal), "keybinding_mob_togglemoveintentalt_down"));
        Assert.Equal(40f, Dm.initial(typeof(datum_keybinding_living_toggle_move_intent_alternative), (datum_keybinding p) => p.weight));
        // runtime-initialized (dm_init_vars): null on the prototype
        Assert.Null(Dm.initial(typeof(datum_keybinding_living_rest), (datum_keybinding p) => p.hotkey_keys));
    }

    [Fact]
    public void KeybindingHotkeysAreObjectCreatingDefaults()
    {
        (datum_keybinding kb, string key)[] cases =
        {
            (new datum_keybinding_living_rest(), "U"),
            (new datum_keybinding_living_toggle_combat_mode(), "F"),
            (new datum_keybinding_living_toggle_move_intent(), "C"),
            (new datum_keybinding_living_toggle_move_intent_alternative(), "Unbound"),
        };
        foreach (var (kb, key) in cases)
        {
            kb.dm_init_vars();
            Assert.Equal(new object[] { key }, Dm.each(kb.hotkey_keys).Cast<object>().ToArray());
        }
    }

    [Fact]
    public void MobMovespeedModifierOverrides()
    {
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_obesity), (datum_movespeed_modifier p) => p.multiplicative_slowdown), 1.5f));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_monkey_reagent_speedmod), (datum_movespeed_modifier p) => p.variable), TRUE));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_monkey_health_speedmod), (datum_movespeed_modifier p) => p.variable), TRUE));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_monkey_temperature_speedmod), (datum_movespeed_modifier p) => p.variable), TRUE));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_golem_hunger), (datum_movespeed_modifier p) => p.variable), TRUE));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_slaughter), (datum_movespeed_modifier p) => p.multiplicative_slowdown), -1));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_resonance), (datum_movespeed_modifier p) => p.multiplicative_slowdown), 0.75f));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_basic_stamina_slowdown), (datum_movespeed_modifier p) => p.variable), TRUE));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_damage_slowdown), (datum_movespeed_modifier p) => p.blacklisted_movetypes), FLOATING));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_damage_slowdown), (datum_movespeed_modifier p) => p.variable), TRUE));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_equipment_speedmod), (datum_movespeed_modifier p) => p.variable), TRUE));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_equipment_speedmod), (datum_movespeed_modifier p) => p.blacklisted_movetypes), FLOATING));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_grab_slowdown), (datum_movespeed_modifier p) => p.id), MOVESPEED_ID_MOB_GRAB_STATE));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_grab_slowdown), (datum_movespeed_modifier p) => p.blacklisted_movetypes), FLOATING));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_grab_slowdown_aggressive), (datum_movespeed_modifier p) => p.multiplicative_slowdown), 3));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_grab_slowdown_neck), (datum_movespeed_modifier p) => p.multiplicative_slowdown), 6));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_grab_slowdown_kill), (datum_movespeed_modifier p) => p.multiplicative_slowdown), 9));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_config_walk_run), (datum_movespeed_modifier p) => p.multiplicative_slowdown), 1));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_config_walk_run), (datum_movespeed_modifier p) => p.id), MOVESPEED_ID_MOB_WALK_RUN));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_config_walk_run), (datum_movespeed_modifier p) => p.flags), IGNORE_NOSLOW));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_turf_slowdown), (datum_movespeed_modifier p) => p.movetypes), GROUND));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_turf_slowdown), (datum_movespeed_modifier p) => p.blacklisted_movetypes), Dm.bitor(FLYING, FLOATING)));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_turf_slowdown), (datum_movespeed_modifier p) => p.variable), TRUE));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_bulky_drag), (datum_movespeed_modifier p) => p.variable), TRUE));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_bulky_drag), (datum_movespeed_modifier p) => p.blacklisted_movetypes), FLOATING));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_cold), (datum_movespeed_modifier p) => p.blacklisted_movetypes), FLOATING));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_cold), (datum_movespeed_modifier p) => p.variable), TRUE));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_staggered), (datum_movespeed_modifier p) => p.multiplicative_slowdown), STAGGERED_SLOWDOWN_STRENGTH));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_human_carry), (datum_movespeed_modifier p) => p.multiplicative_slowdown), HUMAN_CARRY_SLOWDOWN));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_human_carry), (datum_movespeed_modifier p) => p.blacklisted_movetypes), FLOATING));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_limbless), (datum_movespeed_modifier p) => p.variable), TRUE));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_limbless), (datum_movespeed_modifier p) => p.movetypes), GROUND));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_limbless), (datum_movespeed_modifier p) => p.blacklisted_movetypes), Dm.bitor(FLOATING, FLYING)));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_limbless), (datum_movespeed_modifier p) => p.flags), IGNORE_NOSLOW));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_simplemob_varspeed), (datum_movespeed_modifier p) => p.variable), TRUE));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_simplemob_varspeed), (datum_movespeed_modifier p) => p.flags), IGNORE_NOSLOW));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_fast_web), (datum_movespeed_modifier p) => p.multiplicative_slowdown), 0.2f));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_young_web), (datum_movespeed_modifier p) => p.multiplicative_slowdown), 0.5f));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_spiderling_web), (datum_movespeed_modifier p) => p.multiplicative_slowdown), 0.7f));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_average_web), (datum_movespeed_modifier p) => p.multiplicative_slowdown), 1.2f));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_below_average_web), (datum_movespeed_modifier p) => p.multiplicative_slowdown), 2.5f));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_slow_web), (datum_movespeed_modifier p) => p.multiplicative_slowdown), 5));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_viper_defensive), (datum_movespeed_modifier p) => p.multiplicative_slowdown), 1.5f));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_gravity), (datum_movespeed_modifier p) => p.blacklisted_movetypes), FLOATING));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_gravity), (datum_movespeed_modifier p) => p.variable), TRUE));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_gravity), (datum_movespeed_modifier p) => p.flags), IGNORE_NOSLOW));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_carbon_softcrit), (datum_movespeed_modifier p) => p.multiplicative_slowdown), SOFTCRIT_ADD_SLOWDOWN));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_carbon_softcrit), (datum_movespeed_modifier p) => p.flags), IGNORE_NOSLOW));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_carbon_crawling), (datum_movespeed_modifier p) => p.multiplicative_slowdown), CRAWLING_ADD_SLOWDOWN));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_carbon_crawling), (datum_movespeed_modifier p) => p.flags), IGNORE_NOSLOW));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_mob_config_speedmod), (datum_movespeed_modifier p) => p.variable), TRUE));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_mob_config_speedmod), (datum_movespeed_modifier p) => p.flags), IGNORE_NOSLOW));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_metabolicboost), (datum_movespeed_modifier p) => p.multiplicative_slowdown), -1.5f));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_dragon_rage), (datum_movespeed_modifier p) => p.multiplicative_slowdown), -0.5f));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_dragon_depression), (datum_movespeed_modifier p) => p.multiplicative_slowdown), 5));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_morph_disguised), (datum_movespeed_modifier p) => p.multiplicative_slowdown), -1));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_auto_wash), (datum_movespeed_modifier p) => p.multiplicative_slowdown), 3));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_player_spider_modifier), (datum_movespeed_modifier p) => p.variable), TRUE));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_health_scaling_speed_buff), (datum_movespeed_modifier p) => p.variable), TRUE));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_alien_speed), (datum_movespeed_modifier p) => p.variable), TRUE));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_grown_killer_tomato), (datum_movespeed_modifier p) => p.variable), TRUE));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_goldgrub_mount), (datum_movespeed_modifier p) => p.multiplicative_slowdown), -4.5f));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_settler), (datum_movespeed_modifier p) => p.multiplicative_slowdown), 0.2f));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_settler), (datum_movespeed_modifier p) => p.blacklisted_movetypes), Dm.bitor(FLOATING, FLYING)));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_basilisk_overheat), (datum_movespeed_modifier p) => p.multiplicative_slowdown), -18));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_magic_ties), (datum_movespeed_modifier p) => p.multiplicative_slowdown), 0.5f));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_fish_on_water), (datum_movespeed_modifier p) => p.blacklisted_movetypes), MOVETYPES_NOT_TOUCHING_GROUND));
        // datum_movespeed_modifier_fish_on_water.multiplicative_slowdown = - /turf/open/water::slowdown: not checked (water.dm is a type-only skeleton, its slowdown = 1 is not translated)
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_swimming_deep), (datum_movespeed_modifier p) => p.blacklisted_movetypes), MOVETYPES_NOT_TOUCHING_GROUND));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_swimming_deep), (datum_movespeed_modifier p) => p.multiplicative_slowdown), 7));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_movespeed_modifier_tail_dragger), (datum_movespeed_modifier p) => p.multiplicative_slowdown), 4));
    }

    [Fact]
    public void BloodTypeDefaults()
    {
        Assert.Equal("?", Dm.initial(typeof(datum_blood_type), (datum_blood_type p) => p.name));
        Assert.Equal("Unknown DNA", Dm.initial(typeof(datum_blood_type), (datum_blood_type p) => p.dna_string));
        Assert.Equal("#AA1010", Dm.initial(typeof(datum_blood_type), (datum_blood_type p) => p.color));
        Assert.Equal(typeof(datum_reagent_blood), Dm.initial(typeof(datum_blood_type), (datum_blood_type p) => p.reagent_type));
        Assert.Equal(typeof(datum_reagent_iron), Dm.initial(typeof(datum_blood_type), (datum_blood_type p) => p.restoration_chem));
        Assert.Equal(31f, Dm.initial(typeof(datum_blood_type), (datum_blood_type p) => p.blood_flags));    // 1 | 14 | 16
        Assert.Null(Dm.initial(typeof(datum_blood_type), (datum_blood_type p) => p.desc));

        Assert.True(Dm.eq(Dm.initial(typeof(datum_blood_type_human), (datum_blood_type p) => p.desc),
            "Blood cells suspended in plasma, the most abundant of which being the hemoglobin-containing red blood cells."));
        Assert.Equal("Human DNA", Dm.initial(typeof(datum_blood_type_human), (datum_blood_type p) => p.dna_string));
        Assert.Equal(typeof(datum_blood_type_human), Dm.initial(typeof(datum_blood_type_human), (datum p) => p.abstract_type));
        Assert.Equal("A-", Dm.initial(typeof(datum_blood_type_human_a_minus), (datum_blood_type p) => p.name));
        Assert.Equal("AB+", Dm.initial(typeof(datum_blood_type_human_ab_plus), (datum_blood_type p) => p.name));
        Assert.Equal("O+", Dm.initial(typeof(datum_blood_type_human_o_plus), (datum_blood_type p) => p.name));
        Assert.Equal("Human DNA", Dm.initial(typeof(datum_blood_type_human_o_minus), (datum_blood_type p) => p.dna_string));
    }

    [Fact]
    public void BloodTypeCompatibleTypesAreObjectCreatingDefaults()
    {
        var plain = new datum_blood_type();
        plain.dm_init_vars();
        Assert.Equal(0, plain.compatible_types.Count);

        var a_minus = new datum_blood_type_human_a_minus();
        a_minus.dm_init_vars();
        Assert.Equal(new object[] { typeof(datum_blood_type_human_a_minus), typeof(datum_blood_type_human_o_minus) },
            Dm.each(a_minus.compatible_types).Cast<object>().ToArray());

        var ab_plus = new datum_blood_type_human_ab_plus();
        ab_plus.dm_init_vars();
        Assert.Equal(8, ab_plus.compatible_types.Count);
        Assert.Equal(typeof(datum_blood_type_human_ab_plus), ab_plus.compatible_types[8]);
    }

    [Fact]
    public void MobsSubsystemVarBlock()
    {
        var ss = new datum_controller_subsystem_mobs();
        ss.dm_init_vars();
        Assert.Equal(0, ss.currentrun.Count);

        var w = new World();
        using (w.Enter())
        {
            Assert.Null(datum_controller_subsystem_mobs.clients_by_zlevel);             // var/static/list/clients_by_zlevel[][]
            Assert.Equal(1, datum_controller_subsystem_mobs.dead_players_by_zlevel.Count); // list(list())
            Assert.Equal(0, ((DmList)datum_controller_subsystem_mobs.dead_players_by_zlevel[1]).Count);
            Assert.Equal(0, datum_controller_subsystem_mobs.cubemonkeys.Count);
            Assert.Equal(0, datum_controller_subsystem_mobs.cheeserats.Count);
            Assert.Equal(0, datum_controller_subsystem_mobs.relicmobs.Count);
        }
    }

    [Fact]
    public void MovetypeHandlerVarBlock()
    {
        Assert.Equal(1f, Dm.initial(typeof(datum_element_movetype_handler), (datum_element p) => p.element_flags));   // ELEMENT_DETACH_ON_HOST_DESTROY
        var e = new datum_element_movetype_handler();
        e.dm_init_vars();
        Assert.Equal(0, e.attached_atoms.Count);
    }
}
