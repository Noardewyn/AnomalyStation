// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/server/loot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the rewards: the bonuses, the grade, the certificate, the decrypted cache and curiosity the
// byteforge prints.

// tg: code/modules/bitrunning/server/loot.dm:1
public partial class obj_machinery_quantum_server : obj_machinery
{
    /// Handles calculating rewards based on number of players, parts, threats, etc
    // tg: code/modules/bitrunning/server/loot.dm:2
    public virtual object calculate_rewards()
    {
        float rewards_base = 0.8f;

        if (Dm.truthy(domain_randomized))
            rewards_base += 0.2f;

        rewards_base += servo_bonus;

        rewards_base += (length(spawned_threat_refs) * 2);

        rewards_base += Dm.num(get_multiplayer_bonus());

        rewards_base += Dm.num(get_nohit_bouns());

        return rewards_base;
    }

    /// Calculates total bonus from completing the domain in multiplayer
    // tg: code/modules/bitrunning/server/loot.dm:19
    public virtual object get_multiplayer_bonus()
    {
        float total = 0;
        float multiplayer = FALSE;
        foreach (datum_weakref connection_ref in Dm.each_cast<datum_weakref>(avatar_connection_refs))
        {
            datum_component_avatar_connection connection = connection_ref.resolve() as datum_component_avatar_connection;
            if (connection == null)
                continue;
            if (Dm.truthy(multiplayer))
                total += multiplayer_bonus;
            multiplayer = TRUE;
        }
        return total;
    }

    /// Calculates total bonus from completing the domain without taking damage
    // tg: code/modules/bitrunning/server/loot.dm:32
    public virtual object get_nohit_bouns()
    {
        if (Dm.truthy(Dm.bitand(generated_domain.domain_flags, DOMAIN_NO_NOHIT_BONUS)))
            return 0;

        float total = 0;
        foreach (datum_weakref connection_ref in Dm.each_cast<datum_weakref>(avatar_connection_refs))
        {
            datum_component_avatar_connection connection = connection_ref.resolve() as datum_component_avatar_connection;
            if (Dm.truthy(connection?.nohit))
                total += nohit_bonus;
        }
        return total;
    }

    /// Handles spawning the (new) crate and deleting the former
    // tg: code/modules/bitrunning/server/loot.dm:44
    public virtual async DmTask<object> generate_loot(obj cache = null, obj_machinery_byteforge chosen_forge = null)
    {
        SSblackbox.record_feedback("tally", "bitrunning_domain_primary_completed", 1, generated_domain.key);
        foreach (mob person in Dm.each<mob>(cache.contents))
            person.SEND_SIGNAL(COMSIG_BITRUNNER_CACHE_SEVER, Dm.list(person));

        spark_at_location(cache); // abracadabra!
        qdel(cache); // and it's gone!
        SEND_SIGNAL(COMSIG_BITRUNNER_DOMAIN_COMPLETE, Dm.list(this, cache, generated_domain.reward_points));

        points += generated_domain.reward_points;
        playsound(this, Dm.resource("sound/machines/terminal/terminal_success.ogg"), 30, vary: TRUE);

        object bonus = calculate_rewards();

        float time_difference = world.time - Dm.num(generated_domain.start_time);
        object grade = grade_completion(time_difference);

        var certificate = await Dm.new_async<obj_item_paper>();
        certificate.add_raw_text(get_completion_certificate(time_difference, grade));
        certificate.name = "certificate of domain completion";
        certificate.update_appearance();

        var reward_cache = await Dm.new_async<obj_structure_closet_crate_secure_bitrunning_decrypted>(this, generated_domain, bonus);
        reward_cache.manifest = WEAKREF(certificate) as datum_weakref;
        reward_cache.update_appearance();

        generated_domain.submit_grade(grade);

        if (Dm.truthy(can_generate_tech_disk(grade)))
        {
            SSblackbox.record_feedback("tally", "bitrunning_bepis_rewarded", 1, generated_domain.key);
            await Dm.new_async<obj_item_disk_design_disk_bepis_remove_tech>(reward_cache);
            generated_domain.disk_reward_spawned = TRUE;
        }

        await chosen_forge.start_to_spawn(reward_cache);

        domain_complete = TRUE;
        return TRUE;
    }

    /// Builds secondary loot if the achievements were met
    // tg: code/modules/bitrunning/server/loot.dm:84
    public virtual async DmTask<object> generate_secondary_loot(obj curiosity = null, obj_machinery_byteforge chosen_forge = null)
    {
        SSblackbox.record_feedback("tally", "bitrunning_domain_secondary_completed", 1, generated_domain.key);
        spark_at_location(curiosity); // abracadabra!
        qdel(curiosity); // and it's gone!

        var reward_curiosity = await Dm.new_async<obj_item_storage_lockbox_bitrunning_decrypted>(this, generated_domain);

        await chosen_forge.start_to_spawn(reward_curiosity);
        return TRUE;
    }

    /// Returns the markdown text containing domain completion information
    // tg: code/modules/bitrunning/server/loot.dm:96
    public virtual object get_completion_certificate(object time_difference = null, object grade = null)
    {
        float base_points = generated_domain.reward_points;
        if (Dm.truthy(domain_randomized))
            base_points -= 1;

        float bonuses = Dm.num(calculate_rewards());

        float domain_threats = length(spawned_threat_refs);

        string completion_time = Dm.text("### Completion Time: []\n", DisplayTimeText(time_difference));

        string completion_grade = Dm.text("\n---\n\n# Rating: []", grade);

        string text = "# Certificate of Domain Completion\n\n---\n\n";

        text += Dm.text("### [][]\n", generated_domain.name, Dm.truthy(domain_randomized) ? " (Randomized)" : "");
        text += Dm.text("- **Difficulty:** []\n", generated_domain.difficulty);
        text += Dm.text("- **Threats:** []\n", domain_threats);
        text += Dm.text("- **Base Reward:** [][]\n\n", base_points, Dm.truthy(domain_randomized) ? " +1" : "");
        text += Dm.text("- **Total Bonus:** []x\n\n", bonuses);

        if (bonuses <= 1)
        {
            text += completion_time;
            text += completion_grade;
            return text;
        }

        text += "### Bonuses\n";
        if (Dm.truthy(domain_randomized))
            text += "- **Randomized:** + 0.2\n";

        object mp_bonus = get_multiplayer_bonus();
        if (Dm.truthy(mp_bonus))
            text += Dm.text("- **Multiplayer:** + []\n", mp_bonus);

        object nohit_bonus = get_nohit_bouns();
        if (Dm.truthy(nohit_bonus))
            text += Dm.text("- **No hit:** + []\n", nohit_bonus);

        if (domain_threats > 0)
            text += Dm.text("- **Threats:** + []\n", domain_threats * 2);

        float servo_rating = servo_bonus;

        if (servo_rating > 0.2f)
            text += Dm.text("- **Components:** + []\n", servo_rating);

        text += completion_time;
        text += completion_grade;

        return text;
    }

    /// Checks if the players should get a bepis reward
    // tg: code/modules/bitrunning/server/loot.dm:148
    public virtual object can_generate_tech_disk(object grade = null)
    {
        if (Dm.truthy(generated_domain.disk_reward_spawned))
            return FALSE;

        if (!Dm.truthy(LAZYLEN(SSresearch.techweb_nodes_experimental)))
            return FALSE;

        DmList passing_grades = TgState.Current.obj_machinery_quantum_server__can_generate_tech_disk__passing_grades;
        return Dm.num(generated_domain.difficulty >= BITRUNNER_DIFFICULTY_MEDIUM && Dm.@in(grade, passing_grades));
    }

    /// Grades the player's run based on several factors
    // tg: code/modules/bitrunning/server/loot.dm:160
    public virtual object grade_completion(object completion_time = null)
    {
        float score = length(spawned_threat_refs) * 5;
        score += generated_domain.reward_points;

        float @base = generated_domain.difficulty + 1;
        float time_score = 1;

        if (Dm.num(completion_time) <= 1 * MINUTES)
            time_score = 10;
        else if (Dm.num(completion_time) <= 2 * MINUTES)
            time_score = 5;
        else if (Dm.num(completion_time) <= 5 * MINUTES)
            time_score = 3;
        else if (Dm.num(completion_time) <= 10 * MINUTES)
            time_score = 2;
        else
            time_score = 1;

        score += time_score * @base;

        // Increases the chance for glitches to spawn based on how well they're doing
        threat += score;

        float sw__1 = score;
        if (sw__1 >= 1 && sw__1 <= 4)
            return BITRUNNING_GRADE_D;
        else if (sw__1 >= 5 && sw__1 <= 7)
            return BITRUNNING_GRADE_C;
        else if (sw__1 >= 8 && sw__1 <= 10)
            return BITRUNNING_GRADE_B;
        else if (sw__1 >= 11 && sw__1 <= 13)
            return BITRUNNING_GRADE_A;
        else
            return BITRUNNING_GRADE_S;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/bitrunning/server/loot.dm:157  var/static/list/passing_grades = list(BITRUNNING_GRADE_A,BITRUNNING_GRADE_S)
    internal DmList obj_machinery_quantum_server__can_generate_tech_disk__passing_grades = Dm.list(BITRUNNING_GRADE_A, BITRUNNING_GRADE_S);
}
