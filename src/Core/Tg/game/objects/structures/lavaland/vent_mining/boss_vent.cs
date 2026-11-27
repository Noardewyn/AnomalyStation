// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/lavaland/vent_mining/boss_vent.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L2c (D304): the whole file. The var block is generated (D111).

// tg: code/game/objects/structures/lavaland/vent_mining/boss_vent.dm:2
public partial class obj_structure_ore_vent_boss : obj_structure_ore_vent
{
    // tg: code/game/objects/structures/lavaland/vent_mining/boss_vent.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        summoned_boss = pick(defending_mobs);
        return dot;
    }

    // tg: code/game/objects/structures/lavaland/vent_mining/boss_vent.dm:34
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        summoned_boss = null;
        return dot;
    }

    // tg: code/game/objects/structures/lavaland/vent_mining/boss_vent.dm:38
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        string boss_string = "";
        if (Dm.eq(summoned_boss, typeof(mob_living_simple_animal_hostile_megafauna_bubblegum)))
            boss_string = I18n.T("A giant fleshbound beast");
        else if (Dm.eq(summoned_boss, typeof(mob_living_simple_animal_hostile_megafauna_dragon)))
            boss_string = I18n.T("Sharp teeth and scales");
        else if (Dm.eq(summoned_boss, typeof(mob_living_simple_animal_hostile_megafauna_colossus)))
            boss_string = I18n.T("A giant, armored behemoth");
        else if (Dm.eq(summoned_boss, typeof(mob_living_simple_animal_hostile_megafauna_demonic_frost_miner)))
            boss_string = I18n.T("A bloody drillmark");
        else if (Dm.eq(summoned_boss, typeof(mob_living_simple_animal_hostile_megafauna_wendigo_noportal)))
            boss_string = I18n.T("A chilling skull");
        Dm.append(ref dot, span_notice(I18n.T("[] is etched onto the side of the vent.", boss_string)));
        return dot;
    }

    // tg: code/game/objects/structures/lavaland/vent_mining/boss_vent.dm:54
    public override async DmTask start_wave_defense()
    {
        if (!(wave_cooldown <= world.time))
            return;
        // Completely override the normal wave defense, and just spawn the boss.
        mob_living_simple_animal_hostile_megafauna boss = Dm.@new((System.Type)summoned_boss, loc) as mob_living_simple_animal_hostile_megafauna;
        RegisterSignal(boss, COMSIG_VENT_WAVE_CONCLUDED, nameof(handle_wave_conclusion));
        SSblackbox.record_feedback("tally", "ore_vent_mobs_spawned", 1, summoned_boss);
        await boss.say(boss.summon_line, language: typeof(datum_language_common), forced: "summon line"); //Pull their specific summon line to say. Default is meme text so make sure that they have theirs set already.
    }

    // tg: code/game/objects/structures/lavaland/vent_mining/boss_vent.dm:63
    [SignalHandler]
    public override void handle_wave_conclusion(datum source = null)
    {
        node = Dm.@new<mob_living_basic_node_drone>(loc); //We're spawning the vent after the boss dies, so the player can just focus on the boss.
        SSblackbox.record_feedback("tally", "ore_vent_mobs_killed", 1, summoned_boss);
        wave_cooldown = 0; // COOLDOWN_RESET (__DEFINES/cooldowns.dm), inline
        base.handle_wave_conclusion(source);
    }
}

/** Debug vent: The one we have for testing.*/
// tg: code/game/objects/structures/lavaland/vent_mining/boss_vent.dm:79
public partial class obj_structure_ore_vent_debug : obj_structure_ore_vent
{
    // tg: code/game/objects/structures/lavaland/vent_mining/boss_vent.dm:91
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        object choice = await tgui_input_list(user, I18n.T("Choose a material to add/remove."), I18n.T("New material"), subtypesof(typeof(datum_material)));
        if (!Dm.truthy(choice))
            return dot;
        if (Dm.truthy(mineral_breakdown[choice]))
        {
            mineral_breakdown.Remove(choice);
            balloon_alert_to_viewers(I18n.T("removed []", Dm.initial((System.Type)choice, "name")));
            return dot;
        }
        mineral_breakdown.Add(choice);
        balloon_alert_to_viewers(I18n.T("added []", Dm.initial((System.Type)choice, "name")));
        object value = await tgui_input_number(user, I18n.T("What weight should it have?"), I18n.T("ore pickweight"), 1, 100, 1);
        mineral_breakdown[choice] = value;
        balloon_alert_to_viewers(I18n.T("weighting of [] added", value));
        generate_description();
        return dot;
    }

    // tg: code/game/objects/structures/lavaland/vent_mining/boss_vent.dm:107
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        object choice = await tgui_input_list(user, I18n.T("Choose a vent size."), I18n.T("New size"), Dm.list(SMALL_VENT_TYPE, MEDIUM_VENT_TYPE, LARGE_VENT_TYPE));
        if (!Dm.truthy(choice))
            return dot;
        vent_size_setup(random: FALSE, force_size: choice, map_loading: FALSE);
        return dot;
    }
}
