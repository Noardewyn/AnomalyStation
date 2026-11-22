// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/malf_ai/malf_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the zeroth law since silicons SI4, the rest SI8, D326). The var blocks are generated (D111).

// tg: code/modules/antagonists/malf_ai/malf_ai.dm:5
public partial class datum_antagonist_malf_ai : datum_antagonist
{
    /// Chance the malf AI gets a single special objective that isn't assassinate.
    // tg: code/modules/antagonists/malf_ai/malf_ai.dm:2  #define PROB_SPECIAL 30
    private const float PROB_SPECIAL = 30;

    // tg: code/modules/antagonists/malf_ai/malf_ai.dm:24
    public override async DmTask New(params object[] __extra)
    {
        object give_objectives = Dm.arg(__extra, 0) ?? TRUE;
        await base.New(__extra);
        this.give_objectives = Dm.num(give_objectives);
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai.dm:28
    public override async DmTask on_gain()
    {
        if (owner.current != null && !isAI(owner.current))
        {
            stack_trace(Dm.text("Attempted to give malf AI antag datum to \\[[]\\], who did not meet the requirements.", owner), "code/modules/antagonists/malf_ai/malf_ai.dm", 30);
            await base.on_gain();
            return;
        }

        if (Dm.truthy(give_objectives))
            await forge_ai_objectives();
        if (!Dm.truthy(employer))
            employer = Dm.str(pick(GLOB.ai_employers));

        malfunction_flavor = strings(MALFUNCTION_FLAVOR_FILE, employer) as DmList;

        await add_law_zero();
        if (malf_sound != null)
            owner.current.playsound_local(get_turf(owner.current), malf_sound, 100, FALSE, pressure_affected: FALSE, use_reverb: FALSE);
        owner.current.grant_language(typeof(datum_language_codespeak), source: LANGUAGE_MALF);

        var hackyhud = GLOB.huds[DATA_HUD_MALF_APC] as datum_atom_hud_data;
        hackyhud.show_to(owner.current);

        await base.on_gain();
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai.dm:51
    public override async DmTask on_removal()
    {
        if (owner.current is mob_living_silicon_ai malf_ai)
        {
            malf_ai.laws.clear_zeroth_law(force: TRUE);
            malf_ai.remove_malf_abilities();
            QDEL_NULL(ref malf_ai.malf_picker);
        }

        await base.on_removal();
    }

    /// Generates a complete set of malf AI objectives up to the traitor objective limit.
    // tg: code/modules/antagonists/malf_ai/malf_ai.dm:61
    public virtual async DmTask forge_ai_objectives()
    {
        if (prob(PROB_SPECIAL))
            await forge_special_objective();

        float objective_limit = Dm.num(config.Get(typeof(datum_config_entry_number_traitor_objectives_amount)));
        float objective_count = Dm.length(objectives);

        // for(in...to) loops iterate inclusively, so to reach objective_limit we need to loop to objective_limit - 1
        // This does not give them 1 fewer objectives than intended.
        for (float i = objective_count; i <= objective_limit - 1; i++)
        {
            var kill_objective = Dm.@new<datum_objective_assassinate>();
            kill_objective.owner = owner;
            await kill_objective.find_target();
            objectives.Add(kill_objective);
        }

        var dont_die_objective = Dm.@new<datum_objective_survive_malf>();
        dont_die_objective.owner = owner;
        objectives.Add(dont_die_objective);
    }

    /// Generates a special objective and adds it to the objective list.
    // tg: code/modules/antagonists/malf_ai/malf_ai.dm:80
    public virtual async DmTask forge_special_objective()
    {
        float special_pick = rand(1, 4);
        switch ((int)special_pick)
        {
            case 1:
            {
                var block_objective = Dm.@new<datum_objective_block>();
                block_objective.owner = owner;
                objectives.Add(block_objective);
                break;
            }
            case 2:
            {
                var purge_objective = Dm.@new<datum_objective_purge>();
                purge_objective.owner = owner;
                objectives.Add(purge_objective);
                break;
            }
            case 3:
            {
                var robot_objective = Dm.@new<datum_objective_robot_army>();
                robot_objective.owner = owner;
                objectives.Add(robot_objective);
                break;
            }
            case 4: //Protect and strand a target
            {
                var yandere_one = Dm.@new<datum_objective_protect>();
                yandere_one.owner = owner;
                objectives.Add(yandere_one);
                await yandere_one.find_target();
                var yandere_two = Dm.@new<datum_objective_maroon>();
                yandere_two.owner = owner;
                yandere_two.target = yandere_one.target;
                yandere_two.update_explanation_text(); // normally called in find_target()
                objectives.Add(yandere_two);
                break;
            }
        }
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai.dm:107
    public override async DmTask greet()
    {
        await base.greet();
        if (Dm.truthy(should_give_codewords))
            give_codewords();
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai.dm:112
    public override async DmTask<object> apply_innate_effects(mob_living mob_override = null)
    {
        object dot = await base.apply_innate_effects(mob_override);

        mob_living datum_owner = mob_override ?? owner.current as mob_living;

        if (datum_owner is mob_living_silicon_ai ai_owner)
            ai_owner.hack_software = TRUE;

        await datum_owner.AddComponent(typeof(datum_component_codeword_hearing), SStraitor.syndicate_code_phrase_regex, "blue", this);
        await datum_owner.AddComponent(typeof(datum_component_codeword_hearing), SStraitor.syndicate_code_response_regex, "red", this);
        return dot;
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai.dm:123
    public override async DmTask remove_innate_effects(mob_living mob_override = null)
    {
        mob_living datum_owner = mob_override ?? owner.current as mob_living;

        if (datum_owner is mob_living_silicon_ai ai_owner)
            ai_owner.hack_software = FALSE;

        foreach (datum_component_codeword_hearing component in Dm.each_cast<datum_component_codeword_hearing>(datum_owner.GetComponents(typeof(datum_component_codeword_hearing))))
            component.delete_if_from_source(this);
        await DmTask.CompletedTask;
    }

    /// Outputs this shift's codewords and responses to the antag's chat and copies them to their memory.
    // tg: code/modules/antagonists/malf_ai/malf_ai.dm:133
    public virtual void give_codewords()
    {
        if (owner.current == null)
            return;

        object phrases = jointext(SStraitor.syndicate_code_phrase, ", ");
        object responses = jointext(SStraitor.syndicate_code_response, ", ");

        antag_memory += Dm.text("<b>Code Phrase</b>: []<br>", span_blue(Dm.text("[]", phrases)));
        antag_memory += Dm.text("<b>Code Response</b>: []<br>", span_red(Dm.text("[]", responses)));
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai.dm:167
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        var malf_ai = (mob_living_silicon_ai)user;
        DmList data = Dm.list();
        data["processingTime"] = malf_ai.malf_picker.processing_time;
        data["hackedAPCs"] = Dm.length(malf_ai.hacked_apcs);
        return data;
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai.dm:173
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        //antag panel data
        data["has_codewords"] = should_give_codewords;
        if (Dm.truthy(should_give_codewords))
        {
            data["phrases"] = jointext(SStraitor.syndicate_code_phrase, ", ");
            data["responses"] = jointext(SStraitor.syndicate_code_response, ", ");
        }
        data["intro"] = malfunction_flavor?["introduction"];
        data["allies"] = malfunction_flavor?["allies"];
        data["goal"] = malfunction_flavor?["goal"];
        data["objectives"] = get_objectives();
        data["can_change_objective"] = can_assign_self_objectives;
        return data;
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai.dm:188
    public override object roundend_report()
    {
        DmList result = Dm.list();

        bool malf_ai_won = true;

        Dm.append(result, printplayer(owner));

        string objectives_text = "";
        if (Dm.truthy(Dm.length(objectives))) //If the traitor had no objectives, don't need to process this.
        {
            int count = 1;
            foreach (datum_objective objective in Dm.each<datum_objective>(objectives))
            {
                if (!Dm.truthy(objective.check_completion()))
                    malf_ai_won = false;
                objectives_text += Dm.text("<br><B>Objective #[]</B>: [] []", count, objective.explanation_text, objective.get_roundend_success_suffix());
                count++;
            }
        }

        Dm.append(result, objectives_text);

        string special_role_text = LOWER_TEXT(name);

        if (malf_ai_won)
            Dm.append(result, span_greentext(I18n.T("The [] was successful!", special_role_text)));
        else
        {
            Dm.append(result, span_redtext(I18n.T("The [] has failed!", special_role_text)));
            if (owner.current != null)
                Dm.output_to(owner.current, Dm.sound("sound/ambience/misc/ambifailure.ogg")); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
        }

        return result.Join("<br>");
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai.dm:216
    public override DmTask<object> get_preview_icon()
    {
        icon malf_ai_icon = Dm.icon(Dm.resource("icons/mob/silicon/ai.dmi"), "ai-red"); // uni_icon: the port's icons are BYOND's

        // Crop out the borders of the AI, just the face
        malf_ai_icon.Crop(5, 6, 28, 27);

        malf_ai_icon.Scale(ANTAGONIST_PREVIEW_ICON_SIZE, ANTAGONIST_PREVIEW_ICON_SIZE);

        return DmTask<object>.FromResult(malf_ai_icon);
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai.dm:142
    public virtual async DmTask add_law_zero()
    {
        mob_living_silicon_ai malf_ai = owner.current as mob_living_silicon_ai;

        if (malf_ai == null)
            return;

        object law = malfunction_flavor["zeroth_law"];
        //very purposefully not changing this with flavor, i don't want cyborgs throwing the round for their AI's roleplay suggestion
        string law_borg = "Accomplish your AI's objectives at all costs.";

        malf_ai.laws.set_zeroth_law(law, law_borg, force: TRUE);
        malf_ai.laws.protected_zeroth = TRUE;
        malf_ai.set_syndie_radio();
        await malf_ai.try_sync_laws();

        to_chat(malf_ai, I18n.T("Your radio has been upgraded! Use :t to speak on an encrypted channel with Syndicate Agents!"));

        if (malf_ai.malf_picker != null)
            return;
        await malf_ai.add_malf_picker();
    }
}

//Subtype of Malf AI datum, used for one of the traitor final objectives
// tg: code/modules/antagonists/malf_ai/malf_ai.dm:232
public partial class datum_antagonist_malf_ai_infected : datum_antagonist_malf_ai
{
    // tg: code/modules/antagonists/malf_ai/malf_ai.dm:241
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (Dm.arg(__extra, 1) is datum_mind new_boss)
            boss = new_boss;
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai.dm:246
    public override async DmTask forge_ai_objectives()
    {
        if (boss == null)
            return;
        var protection_objective = Dm.@new<datum_objective_protect>();
        protection_objective.owner = owner;
        protection_objective.target = boss;
        protection_objective.update_explanation_text();
        objectives.Add(protection_objective);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai.dm:255
    public override async DmTask add_law_zero()
    {
        if (boss == null)
            return;
        var malf_ai = (mob_living_silicon_ai)owner.current;
        mob_living boss_mob = boss.current as mob_living;

        malf_ai.no_law_rack_link = TRUE;
        await malf_ai.replace_law_set(typeof(datum_ai_laws_syndicate_override));
        malf_ai.laws.set_zeroth_law(Dm.text("Only [] and people [] designate[] as being such are Syndicate Agents.", boss_mob.real_name, boss_mob.p_they(), boss_mob.p_s()), force: TRUE);
        malf_ai.laws.protected_zeroth = TRUE;

        malf_ai.set_syndie_radio();
        to_chat(malf_ai, I18n.T("Your radio has been upgraded! Use :t to speak on an encrypted channel with Syndicate Agents!"));

        await malf_ai.add_malf_picker();
    }
}
