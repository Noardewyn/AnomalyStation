// Translated from tgstation (snapshot 1d99bd7f): code/datums/voice_of_god_command.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps B1 (D327): the whole file, the voice of god (the godwoken trauma speaks with it; the colossus's vocal cords wait for their
// organ): the ominous shout, its listeners by name or job, and every command it gives. The var blocks are generated (D111). tg defines
// /datum/voice_of_god_command/walk/execute twice (the second is meant for /run): the later one wins, so "slow down" sets the run
// intent and "run" does nothing but its cooldown; the earlier definition is execute__d0 (rules 2.5).

/// Used to stop listeners with silly or clown-esque (first) names such as "Honk" or "Flip" from screwing up certain commands.
// tg: code/datums/voice_of_god_command.dm:7  GLOBAL_DATUM(all_voice_of_god_triggers, /regex)
public partial class datum_controller_global_vars
{
    // tg: code/datums/voice_of_god_command.dm:7  GLOBAL_DATUM(all_voice_of_god_triggers, /regex)
    public regex all_voice_of_god_triggers;

    // tg: code/datums/voice_of_god_command.dm:7  (GLOBAL_UNMANAGED(all_voice_of_god_triggers), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalall_voice_of_god_triggers()
    {
        return;
    }

    /// List of all voice of god commands
    // tg: code/datums/voice_of_god_command.dm:9  GLOBAL_LIST_INIT(voice_of_god_commands, init_voice_of_god_commands())
    public DmList voice_of_god_commands;

    // tg: code/datums/voice_of_god_command.dm:9  (GLOBAL_MANAGED(voice_of_god_commands, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalvoice_of_god_commands()
    {
        voice_of_god_commands = (DmList)init_voice_of_god_commands();
        Dm.append(ref gvars_datum_init_order, "voice_of_god_commands");
    }
}

public static partial class GlobalProcs
{
    // tg: code/datums/voice_of_god_command.dm:11
    public static object init_voice_of_god_commands()
    {
        DmList dot = Dm.list();
        object all_triggers = null;
        object separator = null;
        foreach (Type prototype in Dm.each_cast<Type>(subtypesof(typeof(datum_voice_of_god_command))))
        {
            object init_trigger = Dm.initial(prototype, (datum_voice_of_god_command p) => p.trigger);
            if (!Dm.truthy(init_trigger))
                continue;
            Dm.append(dot, Dm.@new(prototype));
            all_triggers = Dm.text("[][][]", all_triggers, separator, init_trigger);
            separator = "|"; // Shouldn't be at the start or end of the regex, or it won't work.
        }
        GLOB.all_voice_of_god_triggers = Dm.regex(all_triggers, "i");
        return dot;
    }

    /*
     * The main proc for the voice of god power. it makes the user shout a message in an ominous way,
     * The first matching command (from a list of static datums) the listeners must obey,
     * and the return value of this proc the cooldown variable of the command dictates. (only relevant for things with cooldowns i guess)
     */
    // tg: code/datums/voice_of_god_command.dm:29
    public static async DmTask<object> voice_of_god(object message = null, mob_living user = null, DmList span_list = null, float? base_multiplier = null, float? include_speaker = null, object forced = null, float? ignore_spam = null)
    {
        base_multiplier ??= 1;
        include_speaker ??= FALSE;
        ignore_spam ??= FALSE;
        object dot = null;
        object log_message = uppertext(message);
        bool is_cultie = Dm.truthy(IS_CULTIST(user));
        if (Dm.truthy(LAZYLEN(span_list)) && is_cultie)
            span_list = Dm.list("narsiesmall");

        if (!Dm.truthy(await user.say(message, spans: span_list, sanitize: FALSE, ignore_spam: ignore_spam, forced: forced)))
            return dot;

        message = LOWER_TEXT(message);

        DmList listeners = Dm.list();
        //used to check if the speaker specified a name or a job to focus on
        DmList specific_listeners = Dm.list();
        // string to remove at the end of the following of the following loop, so saying "Burn Mr. Hopkins" doesn't also burn the HoP later when we check jobs.
        object to_remove_string = null;
        DmList candidates = Dm.sub(get_hearers_in_view(8, user), Dm.truthy(include_speaker) ? null : user) as DmList;
        foreach (mob_living candidate in Dm.each<mob_living>(candidates))
        {
            if (candidate.stat != DEAD && !HAS_TRAIT(candidate, TRAIT_DEAF))
            {
                if (Dm.truthy(candidate.can_block_magic(Dm.bitor(MAGIC_RESISTANCE_HOLY, MAGIC_RESISTANCE_MIND), charge_cost: 0)))
                {
                    to_chat(user, span_userdanger(I18n.T("Something's wrong! [] seems to be resisting your commands.", candidate)));
                    continue;
                }

                Dm.append(listeners, candidate);

                //Let's ensure the listener's name is not matched within another word or command (and vice-versa). e.g. "Saul" in "somersault"
                object their_first_name = first_name(candidate.name);
                if (!Dm.truthy(GLOB.all_voice_of_god_triggers.Find(their_first_name)) && Dm.truthy(findtext(message, Dm.regex(Dm.text("(\\L|^)[](\\L|$)", their_first_name), "i"))))
                {
                    Dm.append(specific_listeners, candidate); //focus on those with the specified name
                    to_remove_string = Dm.text("[][][]", to_remove_string, Dm.truthy(to_remove_string) ? "|" : null, their_first_name);
                    continue;
                }
                object their_last_name = last_name(candidate.name);
                if (!Dm.eq(their_last_name, their_first_name) && !Dm.truthy(GLOB.all_voice_of_god_triggers.Find(their_last_name)) && Dm.truthy(findtext(message, Dm.regex(Dm.text("(\\L|^)[](\\L|$)", their_last_name), "i"))))
                {
                    Dm.append(specific_listeners, candidate); // Ditto
                    to_remove_string = Dm.text("[][][]", to_remove_string, Dm.truthy(to_remove_string) ? "|" : null, their_last_name);
                }
            }
        }

        if (!Dm.truthy(listeners.len))
            return dot;
        if (Dm.truthy(to_remove_string))
        {
            to_remove_string = Dm.text("(\\L|^)([])(\\L|$)", to_remove_string);
            message = replacetext(message, Dm.regex(to_remove_string, "i"), "");
        }

        float power_multiplier = Dm.num(base_multiplier) * (user.mind?.assigned_role?.voice_of_god_power is float vog && vog != 0 ? vog : 1);

        //Cultists are closer to their gods and are more powerful, but they'll give themselves away
        if (is_cultie)
            power_multiplier *= 2;

        //Now get the proper job titles and check for matches.
        object job_message = get_full_job_name(message);
        foreach (mob_living candidate in Dm.each<mob_living>(candidates))
        {
            object their_role = candidate.mind?.assigned_role?.title;
            if (Dm.truthy(their_role) && Dm.truthy(findtext(job_message, their_role)))
                Dm.combine(specific_listeners, candidate); //focus on those with the specified job. "|=" instead "+=" so "Mrs. Capri the Captain" doesn't get affected twice.
        }

        if (Dm.truthy(specific_listeners.len))
        {
            listeners = specific_listeners;
            power_multiplier *= (1 + (1 / specific_listeners.len)); //2x on a single guy, 1.5x on two and so on
        }

        foreach (datum_voice_of_god_command command in Dm.each_cast<datum_voice_of_god_command>(GLOB.voice_of_god_commands))
        {
            if (Dm.truthy(findtext(message, command.trigger)))
            {
                object executed = await command.execute(listeners, user, power_multiplier, message);
                dot = Dm.truthy(executed) ? executed : command.cooldown;
                break;
            }
        }

        if (!Dm.truthy(forced))
            message_admins(Dm.text("[] said '[]' with Voice of God, affecting [], with a power multiplier of [].", ADMIN_LOOKUPFLW(user), log_message, english_list(listeners), power_multiplier));
        user.log_message(Dm.text("said '[]' with Voice of God[], affecting [], with a power multiplier of [].", log_message, Dm.truthy(forced) ? Dm.text(" forced by []", forced) : "", english_list(listeners), power_multiplier), LOG_GAME, color: "red");
        SSblackbox.record_feedback("tally", "voice_of_god", 1, log_message);
        return dot;
    }
}

/// Voice of god command datums that are used in [/proc/voice_of_god()]
// tg: code/datums/voice_of_god_command.dm:99
public partial class datum_voice_of_god_command : datum
{
    // tg: code/datums/voice_of_god_command.dm:1  #define COOLDOWN_STUN (120 SECONDS) (#undef at :480)
    protected const float COOLDOWN_STUN = 120 * SECONDS;

    // tg: code/datums/voice_of_god_command.dm:107
    public override async DmTask New(params object[] __extra)
    {
        if (Dm.truthy(is_regex))
            trigger = Dm.regex(trigger);
        await DmTask.CompletedTask;
    }

    /*
     * What happens when the command is triggered.
     * If a return value is set, it'll be used in place of the 'cooldown' var.
     * Args:
     * * listeners: the list of living mobs who are affected by the command.
     * * user: the one who casted Voice of God
     * * power_multiplier: multiplies the power of the command, most times.
     */
    // tg: code/datums/voice_of_god_command.dm:119
    public virtual async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        await DmTask.CompletedTask;
        return null;
    }
}

/// This command knocks the listeners down.
// tg: code/datums/voice_of_god_command.dm:123
public partial class datum_voice_of_god_command_knockdown : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:127
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        power_multiplier ??= 1;
        foreach (mob_living target in Dm.each_cast<mob_living>(listeners))
            await target.Knockdown(4 * SECONDS * power_multiplier);
        return null;
    }
}

/// This command stops the listeners from moving.
// tg: code/datums/voice_of_god_command.dm:132
public partial class datum_voice_of_god_command_immobilize : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:136
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        power_multiplier ??= 1;
        foreach (mob_living target in Dm.each_cast<mob_living>(listeners))
            await target.Immobilize(4 * SECONDS * power_multiplier);
        return null;
    }
}

/// This command makes carbon listeners throw up like Mr. Creosote.
// tg: code/datums/voice_of_god_command.dm:141
public partial class datum_voice_of_god_command_vomit : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:145
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        power_multiplier ??= 1;
        foreach (mob_living_carbon target in Dm.each<mob_living_carbon>(listeners))
            await target.vomit(vomit_flags: Dm.bitor(MOB_VOMIT_MESSAGE, MOB_VOMIT_HARM), lost_nutrition: (power_multiplier * 10), distance: power_multiplier);
        return null;
    }
}

/// This command silences the listeners. Thrice as effective is the user is a mime or curator.
// tg: code/datums/voice_of_god_command.dm:150
public partial class datum_voice_of_god_command_silence : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:154
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        power_multiplier ??= 1;
        power_multiplier *= user.mind?.assigned_role?.voice_of_god_silence_power is float vog && vog != 0 ? vog : 1;
        foreach (mob_living_carbon target in Dm.each<mob_living_carbon>(listeners))
            await target.adjust_silence(20 * SECONDS * power_multiplier);
        return null;
    }
}

/// This command makes the listeners see others as corgis, carps, skellies etcetera etcetera.
// tg: code/datums/voice_of_god_command.dm:160
public partial class datum_voice_of_god_command_hallucinate : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:163
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        power_multiplier ??= 1;
        foreach (mob_living target in Dm.each<mob_living>(listeners))
        {
            await target.cause_hallucination(
                get_random_valid_hallucination_subtype(typeof(datum_hallucination_delusion_preset)),
                "voice of god",
                Dm.named("duration", 15 * SECONDS * power_multiplier),
                Dm.named("affects_us", FALSE),
                Dm.named("affects_others", TRUE),
                Dm.named("skip_nearby", FALSE));
        }
        return null;
    }
}

/// This command wakes up the listeners.
// tg: code/datums/voice_of_god_command.dm:175
public partial class datum_voice_of_god_command_wake_up : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:179
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        foreach (mob_living target in Dm.each_cast<mob_living>(listeners))
            await target.SetSleeping(0);
        return null;
    }
}

/// This command heals the listeners for 10 points of total damage.
// tg: code/datums/voice_of_god_command.dm:184
public partial class datum_voice_of_god_command_heal : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:188
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        power_multiplier ??= 1;
        foreach (mob_living target in Dm.each_cast<mob_living>(listeners))
            await target.heal_overall_damage(10 * power_multiplier, 10 * power_multiplier);
        return null;
    }
}

/// This command applies 15 points of brute damage to the listeners. There's subtle theological irony in this being more powerful than healing.
// tg: code/datums/voice_of_god_command.dm:193
public partial class datum_voice_of_god_command_brute : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:197
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        power_multiplier ??= 1;
        foreach (mob_living target in Dm.each_cast<mob_living>(listeners))
            await target.apply_damage(15 * power_multiplier, def_zone: BODY_ZONE_CHEST, wound_bonus: CANT_WOUND);
        return null;
    }
}

/// This command makes carbon listeners bleed from a random body part.
// tg: code/datums/voice_of_god_command.dm:202
public partial class datum_voice_of_god_command_bleed : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:206
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        foreach (mob_living_carbon_human target in Dm.each<mob_living_carbon_human>(listeners))
        {
            obj_item_bodypart chosen_part = pick(target.get_bodyparts() as DmList) as obj_item_bodypart;
            float add_stacks = HAS_TRAIT(target, TRAIT_BLOOD_FOUNTAIN) ? 7 : 5;
            chosen_part.adjustBleedStacks(add_stacks);
        }
        await DmTask.CompletedTask;
        return null;
    }
}

/// This command sets the listeners ablaze.
// tg: code/datums/voice_of_god_command.dm:213
public partial class datum_voice_of_god_command_burn : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:217
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        power_multiplier ??= 1;
        foreach (mob_living target in Dm.each_cast<mob_living>(listeners))
        {
            await target.adjust_fire_stacks(1 * power_multiplier);
            target.ignite_mob();
        }
        return null;
    }
}

/// This command heats the listeners up like boiling water.
// tg: code/datums/voice_of_god_command.dm:223
public partial class datum_voice_of_god_command_hot : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:227
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        power_multiplier ??= 1;
        foreach (mob_living target in Dm.each_cast<mob_living>(listeners))
            target.adjust_bodytemperature(50 * power_multiplier);
        await DmTask.CompletedTask;
        return null;
    }
}

/// This command cools the listeners down like freezing water.
// tg: code/datums/voice_of_god_command.dm:232
public partial class datum_voice_of_god_command_cold : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:236
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        power_multiplier ??= 1;
        foreach (mob_living target in Dm.each_cast<mob_living>(listeners))
            target.adjust_bodytemperature(-50 * power_multiplier);
        await DmTask.CompletedTask;
        return null;
    }
}

/// This command throws the listeners away from the user.
// tg: code/datums/voice_of_god_command.dm:241
public partial class datum_voice_of_god_command_repulse : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:245
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        power_multiplier ??= 1;
        foreach (mob_living target in Dm.each_cast<mob_living>(listeners))
        {
            turf throwtarget = get_edge_target_turf(user, get_dir(user, get_step_away(target, user))) as turf;
            await target.throw_at(throwtarget, 3 * power_multiplier, 1 * power_multiplier);
        }
        return null;
    }
}

/// This command throws the listeners at the user.
// tg: code/datums/voice_of_god_command.dm:251
public partial class datum_voice_of_god_command_attract : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:255
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        power_multiplier ??= 1;
        foreach (mob_living target in Dm.each_cast<mob_living>(listeners))
            await target.throw_at(get_step_towards(user, target), 3 * power_multiplier, 1 * power_multiplier);
        return null;
    }
}

/// This command forces the listeners to say their true name (so masks and hoods won't help).
/// Basic and simple mobs who are forced to state their name and don't have one already will... reveal their actual one!
// tg: code/datums/voice_of_god_command.dm:261
public partial class datum_voice_of_god_command_who_are_you : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:264
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        float iteration = 1;
        foreach (mob_living target in Dm.each_cast<mob_living>(listeners))
        {
            _addtimer(Dm.@new<datum_callback>(this, nameof(state_name), target), 0.5f * SECONDS * iteration, file: "code/datums/voice_of_god_command.dm", line: 267);
            iteration++;
        }
        await DmTask.CompletedTask;
        return null;
    }

    ///just states the target's name, but also includes the renaming funny.
    // tg: code/datums/voice_of_god_command.dm:271
    public virtual async DmTask state_name(mob_living target = null)
    {
        if (QDELETED(target))
            return;
        float gold_core_spawnable = NO_SPAWN;
        if (isbasicmob(target))
        {
            mob_living_basic basic_bandy = (mob_living_basic)target;
            gold_core_spawnable = basic_bandy.gold_core_spawnable;
        }
        else if (isanimal(target))
        {
            mob_living_simple_animal simple_sandy = (mob_living_simple_animal)target;
            gold_core_spawnable = simple_sandy.gold_core_spawnable;
        }
        if (Dm.eq(target.name, Dm.initial(target, (atom p) => p.name)) && gold_core_spawnable == FRIENDLY_SPAWN)
        {
            object canonical_deep_lore_name;
            switch (target.gender)
            {
                case MALE:
                    canonical_deep_lore_name = pick(GLOB.first_names_male);
                    break;
                case FEMALE:
                    canonical_deep_lore_name = pick(GLOB.first_names_female);
                    break;
                default:
                    canonical_deep_lore_name = pick(GLOB.first_names);
                    break;
            }
            await target.fully_replace_character_name(target.real_name, canonical_deep_lore_name);
        }
        await target.say(target.real_name);
    }
}

/// This command forces the listeners to say the user's name
// tg: code/datums/voice_of_god_command.dm:294
public partial class datum_voice_of_god_command_say_my_name : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:297
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        float iteration = 1;
        regex smartass_regex = Dm.regex(@"^say my name[.!]*$");
        foreach (mob_living target in Dm.each_cast<mob_living>(listeners))
        {
            object to_say = user.name;
            // 0.1% chance to be a smartass
            if (Dm.truthy(findtext(LOWER_TEXT(message), smartass_regex)) && prob(0.1f))
                to_say = I18n.T("My name");
            _addtimer(Dm.@new<datum_callback>(target, nameof(atom_movable.say), to_say), 0.5f * SECONDS * iteration, file: "code/datums/voice_of_god_command.dm", line: 305);
            iteration++;
        }
        await DmTask.CompletedTask;
        return null;
    }
}

/// This command forces the listeners to say "Who's there?".
// tg: code/datums/voice_of_god_command.dm:309
public partial class datum_voice_of_god_command_knock_knock : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:312
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        float iteration = 1;
        foreach (mob_living target in Dm.each_cast<mob_living>(listeners))
        {
            _addtimer(Dm.@new<datum_callback>(target, nameof(atom_movable.say), I18n.T("Who's there?")), 0.5f * SECONDS * iteration, file: "code/datums/voice_of_god_command.dm", line: 315);
            iteration++;
        }
        await DmTask.CompletedTask;
        return null;
    }
}

/// This command forces silicon listeners to state all their laws.
// tg: code/datums/voice_of_god_command.dm:319
public partial class datum_voice_of_god_command_state_laws : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:322
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        float iteration = 0;
        foreach (mob_living_silicon target in Dm.each<mob_living_silicon>(listeners))
        {
            _addtimer(Dm.@new<datum_callback>(target, nameof(mob_living_silicon.statelaws), TRUE), (3 * SECONDS * iteration) + 0.5f * SECONDS, file: "code/datums/voice_of_god_command.dm", line: 325);
            iteration++;
        }
        await DmTask.CompletedTask;
        return null;
    }
}

/// This command forces the listeners to take step in a direction chosen by the user, otherwise a random cardinal one.
// tg: code/datums/voice_of_god_command.dm:329
public partial class datum_voice_of_god_command_move : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:331  var/static/up_words (rules 10.6; built on the first use: a regex has no world id)
    public static ref regex up_words => ref TgState.Current.datum_voice_of_god_command_move__up_words;
    // tg: code/datums/voice_of_god_command.dm:332  var/static/down_words
    public static ref regex down_words => ref TgState.Current.datum_voice_of_god_command_move__down_words;
    // tg: code/datums/voice_of_god_command.dm:333  var/static/left_words
    public static ref regex left_words => ref TgState.Current.datum_voice_of_god_command_move__left_words;
    // tg: code/datums/voice_of_god_command.dm:334  var/static/right_words
    public static ref regex right_words => ref TgState.Current.datum_voice_of_god_command_move__right_words;

    // tg: code/datums/voice_of_god_command.dm:336
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        up_words ??= Dm.regex("up|north|fore");
        down_words ??= Dm.regex("down|south|aft");
        left_words ??= Dm.regex("left|west|port");
        right_words ??= Dm.regex("right|east|starboard");
        float iteration = 1;
        object direction = null;
        if (Dm.truthy(findtext(message, up_words)))
            direction = NORTH;
        else if (Dm.truthy(findtext(message, down_words)))
            direction = SOUTH;
        else if (Dm.truthy(findtext(message, left_words)))
            direction = WEST;
        else if (Dm.truthy(findtext(message, right_words)))
            direction = EAST;
        foreach (mob_living target in Dm.each_cast<mob_living>(listeners))
        {
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs._step), target, Dm.truthy(direction) ? direction : pick(GLOB.cardinals)), 1 * SECONDS * (iteration - 1), file: "code/datums/voice_of_god_command.dm", line: 348);
            iteration++;
        }
        await DmTask.CompletedTask;
        return null;
    }
}

/// This command forces the listeners to switch to walk intent.
// tg: code/datums/voice_of_god_command.dm:352
public partial class datum_voice_of_god_command_walk : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:355  (the earlier of the type's two definitions of execute, rules 2.5: nothing calls it)
    public virtual async DmTask<object> execute__d0(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        foreach (mob_living target in Dm.each_cast<mob_living>(listeners))
            await target.set_move_intent(MOVE_INTENT_WALK);
        return null;
    }

    // tg: code/datums/voice_of_god_command.dm:364  (tg's second /datum/voice_of_god_command/walk/execute, meant for /run: it wins)
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        foreach (mob_living target in Dm.each_cast<mob_living>(listeners))
            await target.set_move_intent(MOVE_INTENT_RUN);
        return null;
    }
}

/// This command turns the listeners' throw mode on.
// tg: code/datums/voice_of_god_command.dm:369
public partial class datum_voice_of_god_command_throw_catch : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:372
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        foreach (mob_living_carbon target in Dm.each<mob_living_carbon>(listeners))
            target.throw_mode_on(THROW_MODE_TOGGLE);
        await DmTask.CompletedTask;
        return null;
    }
}

/// This command forces the listeners to say a brain damage line.
// tg: code/datums/voice_of_god_command.dm:377
public partial class datum_voice_of_god_command_speak : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:380
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        float iteration = 1;
        foreach (mob_living target in Dm.each<mob_living>(listeners))
        {
            _addtimer(Dm.@new<datum_callback>(target, nameof(atom_movable.say), pick_list_replacements(BRAIN_DAMAGE_FILE, "brain_damage")), 0.5f * SECONDS * iteration, file: "code/datums/voice_of_god_command.dm", line: 383);
            iteration++;
        }
        await DmTask.CompletedTask;
        return null;
    }
}

/// This command forces the listeners to get the fuck up, resetting all stuns.
// tg: code/datums/voice_of_god_command.dm:387
public partial class datum_voice_of_god_command_getup : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:391
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        foreach (mob_living target in Dm.each_cast<mob_living>(listeners))
        {
            await target.set_resting(FALSE);
            await target.SetAllImmobility(0);
        }
        return null;
    }
}

/// This command forces each listener to buckle to a chair found on the same tile.
// tg: code/datums/voice_of_god_command.dm:397
public partial class datum_voice_of_god_command_sit : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:401
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        foreach (mob_living target in Dm.each_cast<mob_living>(listeners))
        {
            obj_structure_chair chair = Dm.locate<obj_structure_chair>(get_turf(target));
            if (chair != null)
                await chair.buckle_mob(target);
        }
        return null;
    }
}

/// This command forces each listener to unbuckle from whatever they are buckled to.
// tg: code/datums/voice_of_god_command.dm:407
public partial class datum_voice_of_god_command_stand : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:411
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        foreach (mob_living target in Dm.each_cast<mob_living>(listeners))
        {
            if (target.buckled != null)
                await target.buckled.unbuckle_mob(target);
        }
        return null;
    }
}

/// This command forces the listener to do the jump emote 3/4 of the times or reply "HOW HIGH?!!".
// tg: code/datums/voice_of_god_command.dm:416
public partial class datum_voice_of_god_command_jump : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:420
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        float iteration = 1;
        foreach (mob_living target in Dm.each_cast<mob_living>(listeners))
        {
            if (prob(25))
                _addtimer(Dm.@new<datum_callback>(target, nameof(atom_movable.say), I18n.T("HOW HIGH?!!")), 0.5f * SECONDS * iteration, file: "code/datums/voice_of_god_command.dm", line: 424);
            else
                _addtimer(Dm.@new<datum_callback>(target, nameof(mob.emote), "jump"), 0.5f * SECONDS * iteration, file: "code/datums/voice_of_god_command.dm", line: 426);
            iteration++;
        }
        await DmTask.CompletedTask;
        return null;
    }
}

///This command plays a bikehorn sound after 2 seconds and a half have passed, and also slips listeners if the user is a clown.
// tg: code/datums/voice_of_god_command.dm:430
public partial class datum_voice_of_god_command_honk : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:433
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        power_multiplier ??= 1;
        object dot = null;
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.playsound), get_turf(user), Dm.resource("sound/items/bikehorn.ogg"), 300, 1), 2.5f * SECONDS, file: "code/datums/voice_of_god_command.dm", line: 434);
        if (is_clown_job(user.mind?.assigned_role))
        {
            dot = COOLDOWN_STUN; //it slips.
            foreach (mob_living_carbon target in Dm.each<mob_living_carbon>(listeners))
                await target.slip(14 * SECONDS * power_multiplier);
        }
        return dot;
    }
}

///This command spins the listeners 1800° degrees clockwise.
// tg: code/datums/voice_of_god_command.dm:441
public partial class datum_voice_of_god_command_multispin : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:444
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        foreach (mob_living target in Dm.each_cast<mob_living>(listeners))
            target.SpinAnimation(speed: 10, loops: 5);
        await DmTask.CompletedTask;
        return null;
    }
}

/// Supertype of all those commands that make people emote and nothing else. Fuck copypasta.
// tg: code/datums/voice_of_god_command.dm:449
public partial class datum_voice_of_god_command_emote : datum_voice_of_god_command
{
    // tg: code/datums/voice_of_god_command.dm:453
    public override async DmTask<object> execute(DmList listeners = null, mob_living user = null, float? power_multiplier = null, object message = null)
    {
        float iteration = 1;
        foreach (mob_living target in Dm.each_cast<mob_living>(listeners))
        {
            _addtimer(Dm.@new<datum_callback>(target, nameof(mob.emote), emote_name), 0.5f * SECONDS * iteration, file: "code/datums/voice_of_god_command.dm", line: 456);
            iteration++;
        }
        await DmTask.CompletedTask;
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/datums/voice_of_god_command.dm:331-334  /datum/voice_of_god_command/move var/static/up_words, down_words, left_words, right_words
    internal regex datum_voice_of_god_command_move__up_words;
    internal regex datum_voice_of_god_command_move__down_words;
    internal regex datum_voice_of_god_command_move__left_words;
    internal regex datum_voice_of_god_command_move__right_words;
}
