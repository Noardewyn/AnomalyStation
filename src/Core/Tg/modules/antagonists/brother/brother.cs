// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/brother/brother.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package X4 (D316): the whole file (the blood brother, the conversion by flash, the brothers' team with its shared
// objectives and name, the convert-a-brother objective, the admins' "Adjust Remaining Conversions" command, the AntagInfoBrother
// window). The preferences menu's preview picture (get_preview_icon) is a deferred leaf until the ethereal and moth species are
// live (not-translated.md section 133). The port has no TESTING build: the non-TESTING branches are the ones translated. The
// brother's var block is hand-written (OpenDream reads its VAR_PRIVATE block as a type path); the team's and the objective's are
// generated.

// tg: code/modules/antagonists/brother/brother.dm:2
public partial class datum_antagonist_brother : datum_antagonist
{
    public string special_role = ROLE_BROTHER; // tg: code/modules/antagonists/brother/brother.dm:5
    // tg: code/modules/antagonists/brother/brother.dm:14  VAR_PRIVATE/datum/team/brother_team/team
    private datum_team_brother_team team;

    // tg: code/modules/antagonists/brother/brother.dm:2
    public datum_antagonist_brother()
    {
        name = Dm.text("\\improper Brother");
        antagpanel_category = "Brother";
        pref_flag = ROLE_BROTHER;
        antag_hud_name = "brother";
        hijack_speed = 0.5f;
        ui_name = "AntagInfoBrother";
        suicide_cry = "FOR MY BROTHER!!";
        antag_moodlet = typeof(datum_mood_event_focused);
        hardcore_random_bonus = TRUE;
        stinger_sound = Dm.resource("sound/music/antag/traitor/tatoralert.ogg");
    }

    // tg: code/modules/antagonists/brother/brother.dm:16
    public override async DmTask create_team(datum_team new_team = null)
    {
        if (new_team == null)
        {
            team = await Dm.new_async<datum_team_brother_team>();
            return;
        }
        if (new_team is not datum_team_brother_team)
            stack_trace(Dm.text("Wrong team type passed to [] initialization.", type), "code/modules/antagonists/brother/brother.dm", 21);
        team = new_team as datum_team_brother_team;
    }

    // tg: code/modules/antagonists/brother/brother.dm:24
    public override object get_team()
    {
        return team;
    }

    // tg: code/modules/antagonists/brother/brother.dm:27
    public override async DmTask on_gain()
    {
        Dm.append(ref objectives, team.objectives);
        finalize_brother();

        if (team.brothers_left <= 0)
        {
            await base.on_gain();
            return;
        }

        mob_living_carbon carbon_owner = owner.current as mob_living_carbon;
        if (carbon_owner == null)
        {
            await base.on_gain();
            return;
        }

        await grant_conversion_skills();
        await carbon_owner.equip_conspicuous_item(await Dm.new_async<obj_item_assembly_flash>());

        bool is_first_brother = Dm.length(team.members) == 1;
        if (!is_first_brother)
            to_chat(carbon_owner, span_boldwarning(I18n.T("The Syndicate have higher expectations from you than others. They have granted you an extra flash to convert one other person.")));

        await base.on_gain();
    }

    // tg: code/modules/antagonists/brother/brother.dm:47
    public override async DmTask on_removal()
    {
        remove_conversion_skills();
        await base.on_removal();
    }

    /// Give us the ability to add another brother
    // tg: code/modules/antagonists/brother/brother.dm:52
    public virtual async DmTask grant_conversion_skills()
    {
        mob_living_carbon carbon_owner = owner.current as mob_living_carbon;
        if (carbon_owner == null)
            return;
        await carbon_owner.AddComponentFrom(REF(this), typeof(datum_component_can_flash_from_behind));
        RegisterSignal(carbon_owner, COMSIG_MOB_SUCCESSFUL_FLASHED_MOB, nameof(on_mob_successful_flashed_mob));
    }

    /// Take away the ability to add more brothers
    // tg: code/modules/antagonists/brother/brother.dm:60
    public virtual void remove_conversion_skills()
    {
        if (owner.current == null)
            return;
        mob carbon_owner = owner.current;
        carbon_owner.RemoveComponentSource(REF(this), typeof(datum_component_can_flash_from_behind));
        UnregisterSignal(carbon_owner, COMSIG_MOB_SUCCESSFUL_FLASHED_MOB);
    }

    // tg: code/modules/antagonists/brother/brother.dm:67
    [SignalHandler]
    public virtual void on_mob_successful_flashed_mob(mob_living source = null, mob_living flashed = null, obj_item_assembly_flash flash = null, object deviation = null)
    {
        if (flashed.stat == DEAD || issilicon(flashed) || isdrone(flashed))
            return;

        if (flashed.stat != STABLE)
        {
            flashed.balloon_alert(source, I18n.T("in critical!"));
            return;
        }

        if (IS_UNCONSCIOUS(flashed))
        {
            flashed.balloon_alert(source, I18n.T("unconscious!"));
            return;
        }

        // tg: code/modules/antagonists/brother/brother.dm:81  (#ifdef TESTING: the port has no TESTING build)
        if (flashed.mind == null || !Dm.truthy(GET_CLIENT(flashed)))
        {
            flashed.balloon_alert(source, I18n.T("[] mind is vacant!", flashed.p_their()));
            return;
        }

        foreach (datum_objective brother_objective in Dm.each<datum_objective>(source.mind.get_all_objectives()))
        {
            // If the objective has a target, are we flashing them?
            if (flashed == brother_objective.target?.current)
            {
                flashed.balloon_alert(source, I18n.T("that's your target!"));
                return;
            }
        }

        if (Dm.truthy(flashed.mind.has_antag_datum(typeof(datum_antagonist_brother))))
        {
            flashed.balloon_alert(source, I18n.T("[] loyal to someone else!", flashed.p_theyre()));
            return;
        }

        if (HAS_MIND_TRAIT(flashed, TRAIT_UNCONVERTABLE))
        {
            flashed.balloon_alert(source, I18n.T("[] resist[]!", flashed.p_they(), flashed.p_s()));
            return;
        }

        if (!Dm.truthy(Waiting.NoSleep(team.add_brother(flashed, key_name(source))))) // Shouldn't happen given the former, more specific checks but just in case
        {
            flashed.balloon_alert(source, I18n.T("failed!"));
            return;
        }

        source.log_message(Dm.text("converted [] to blood brother", key_name(flashed)), LOG_ATTACK);
        flashed.log_message(Dm.text("was converted by [] to blood brother", key_name(source)), LOG_ATTACK);
        log_game(Dm.text("[] was made into a blood brother by []", key_name(flashed), key_name(source)), Dm.list(
            Dm.kv("converted", flashed),
            Dm.kv("converted by", source)
        ));
        flash.burn_out();
        flashed.mind.add_memory(
            typeof(datum_memory_recruited_by_blood_brother),
            Dm.named("protagonist", flashed),
            Dm.named("antagonist", owner.current)
        );
        flashed.balloon_alert(source, I18n.T("converted"));
    }

    // tg: code/modules/antagonists/brother/brother.dm:122
    public override object antag_panel_data()
    {
        return Dm.text("Conspirators : [] | Remaining: []", get_brother_names(), team.brothers_left);
    }

    // tg: code/modules/antagonists/brother/brother.dm:125
    public override object get_admin_commands()
    {
        DmList dot = base.get_admin_commands() as DmList;
        dot["Adjust Remaining Conversions"] = Dm.@new<datum_callback>(this, nameof(update_recruitments_remaining));
        return dot;
    }

    /// Add or remove the potential to put more bros in here
    // tg: code/modules/antagonists/brother/brother.dm:130
    public virtual async DmTask update_recruitments_remaining(mob admin = null)
    {
        object new_count = await tgui_input_number(admin, "How many more people should be able to be recruited?", "Adjust Conversions Remaining", @default: 1, min_value: 0);
        if (new_count == null)
            return;
        await team.set_brothers_left(Dm.num(new_count));
    }

    // tg: code/modules/antagonists/brother/brother.dm:136
    public override async DmTask<object> get_preview_icon()
    {
        Dm.Deferred("code/modules/antagonists/brother/brother.dm:136", "get_preview_icon: the ethereal and moth dummies (the species are not live); the lobby shows no Blood Brother picture until they are");
        return null;
    }

    // tg: code/modules/antagonists/brother/brother.dm:168
    public virtual object get_brother_names()
    {
        DmList brothers = Dm.sub(team.members, owner) as DmList;
        if (!Dm.truthy(Dm.length(brothers)))
            return "none";

        string brother_text = "";
        for (int i = 1; i <= brothers.len; i++)
        {
            datum_mind M = brothers[i] as datum_mind;
            brother_text += M.name;
            if (i == brothers.len - 1)
                brother_text += " and ";
            else if (i != brothers.len)
                brother_text += ", ";
        }
        return brother_text;
    }

    // tg: code/modules/antagonists/brother/brother.dm:183
    public override async DmTask greet()
    {
        to_chat(owner.current, span_alertsyndie(I18n.T("You are a Blood Brother.")));
        await owner.announce_objectives();
    }

    // tg: code/modules/antagonists/brother/brother.dm:187
    public virtual void finalize_brother()
    {
        play_stinger();
        team.update_name();
    }

    // tg: code/modules/antagonists/brother/brother.dm:191
    public override async DmTask admin_add(datum_mind new_owner = null, mob admin = null)
    {
        datum_team_brother_team team = await Dm.new_async<datum_team_brother_team>();
        await team.add_member(new_owner);
        message_admins(Dm.text("[] made [] into a blood brother.", key_name_admin(admin), key_name_admin(new_owner)));
        log_admin(Dm.text("[] made [] into a blood brother.", key_name(admin), key_name(new_owner)));
    }

    // tg: code/modules/antagonists/brother/brother.dm:197
    public override async DmTask<object> apply_innate_effects(mob_living mob_override = null)
    {
        object dot = await base.apply_innate_effects(mob_override);
        mob_living the_mob = owner.current as mob_living ?? mob_override;
        add_team_hud(the_mob);
        return dot;
    }

    // tg: code/modules/antagonists/brother/brother.dm:202
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["antag_name"] = name;
        data["objectives"] = get_objectives();
        return data;
    }
}

// tg: code/modules/antagonists/brother/brother.dm:209
public partial class datum_team_brother_team : datum_team
{
    // tg: code/modules/antagonists/brother/brother.dm:214
    [DmParams("starting_members")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (prob(10))
            brothers_left += 1;
    }

    // tg: code/modules/antagonists/brother/brother.dm:219
    public override async DmTask add_member(datum_mind new_member = null)
    {
        await base.add_member(new_member);
        if (!Dm.truthy(Dm.length(objectives)))
            await forge_brother_objectives();
        if (!Dm.truthy(new_member.has_antag_datum(typeof(datum_antagonist_brother))))
            await add_brother(new_member.current as mob_living);
        else
        {
            // the only place a joining member spends a conversion slot; converts get here via add_brother()
            await set_brothers_left(brothers_left - 1);
        }
    }

    // tg: code/modules/antagonists/brother/brother.dm:229
    public override async DmTask remove_member(datum_mind member = null)
    {
        if (!Dm.@in(member, members))
            return;
        await base.remove_member(member);
        await member.remove_antag_datum(typeof(datum_antagonist_brother));
        if (!Dm.truthy(Dm.length(members)))
        {
            qdel(this);
            return;
        }
        if (member.current == null)
            return;
        foreach (datum_mind brother_mind in Dm.each<datum_mind>(members))
            to_chat(brother_mind, span_warning(I18n.T("[] is no longer your brother!", span_bold(I18n.T("[]", member.current.real_name)))));
        update_name();
    }

    /// Adds a new brother to the team
    // tg: code/modules/antagonists/brother/brother.dm:244
    public virtual async DmTask<object> add_brother(mob_living new_brother = null, object source = null)
    {
        // tg: code/modules/antagonists/brother/brother.dm:245  (#ifndef TESTING: the port has no TESTING build)
        if (new_brother == null || new_brother.mind == null || !Dm.truthy(GET_CLIENT(new_brother)) || Dm.truthy(new_brother.mind.has_antag_datum(typeof(datum_antagonist_brother))))
            return FALSE;

        // this spends a conversion slot via add_member()
        await new_brother.mind.add_antag_datum(typeof(datum_antagonist_brother), this);

        foreach (datum_mind brother_mind in Dm.each<datum_mind>(members))
        {
            if (brother_mind == new_brother.mind)
                continue;

            to_chat(brother_mind, span_notice(I18n.T("[] has been converted to aid you as your brother!", span_bold(I18n.T("[]", new_brother.real_name)))));
            if (brothers_left <= 0)
                to_chat(brother_mind, span_notice(I18n.T("You cannot recruit any more brothers.")));
        }

        return TRUE;
    }

    // tg: code/modules/antagonists/brother/brother.dm:268
    public virtual void update_name()
    {
        DmList last_names = Dm.list();
        foreach (datum_mind team_minds in Dm.each<datum_mind>(members))
        {
            DmList split_name = splittext(team_minds.name, " ");
            Dm.append(last_names, split_name[split_name.len]);
        }

        if (last_names.len == 1)
            name = Dm.text("[]'s Isolated Intifada", last_names[1]);
        else
            name = Dm.text("[] of ", Dm.initial(this, (datum_team_brother_team p) => p.name)) + last_names.Join(" & ");
    }

    // tg: code/modules/antagonists/brother/brother.dm:279
    public virtual async DmTask forge_brother_objectives()
    {
        objectives = Dm.list();

        await add_objective(Dm.@new<datum_objective_convert_brother>());

        bool is_hijacker = prob(10);
        float amount = Dm.num(config.Get(typeof(datum_config_entry_number_brother_objectives_amount))) /* CONFIG_GET(number/brother_objectives_amount) */;
        for (int i = 1; i <= Math.Max(1, amount + (brothers_left > 2 ? 1 : 0) - (is_hijacker ? 1 : 0)); i++)
            await forge_single_objective();
        if (is_hijacker)
        {
            if (Dm.locate<datum_objective_hijack>(objectives) == null)
                await add_objective(Dm.@new<datum_objective_hijack>());
        }
        else if (Dm.locate<datum_objective_escape>(objectives) == null)
            await add_objective(Dm.@new<datum_objective_escape>());
    }

    // tg: code/modules/antagonists/brother/brother.dm:292
    public virtual async DmTask forge_single_objective()
    {
        if (prob(50))
        {
            if (Dm.truthy(LAZYLEN(active_ais())) && prob(100 / GLOB.joined_player_list.len))
                await add_objective(Dm.@new<datum_objective_destroy>(), needs_target: TRUE);
            else if (prob(30))
                await add_objective(Dm.@new<datum_objective_maroon>(), needs_target: TRUE);
            else
                await add_objective(Dm.@new<datum_objective_assassinate>(), needs_target: TRUE);
        }
        else
            await add_objective(Dm.@new<datum_objective_steal>(), needs_target: TRUE);
    }

    /// Control how many more people we can recruit
    // tg: code/modules/antagonists/brother/brother.dm:304
    public virtual async DmTask set_brothers_left(float remaining_brothers)
    {
        if (brothers_left == remaining_brothers)
            return;

        if (brothers_left == 0 && remaining_brothers > 0)
        {
            foreach (datum_mind brother_mind in Dm.each<datum_mind>(members))
            {
                datum_antagonist_brother brother_datum = brother_mind.has_antag_datum(typeof(datum_antagonist_brother)) as datum_antagonist_brother;
                if (brother_datum != null)
                    await brother_datum.grant_conversion_skills();
            }
        }
        else if (brothers_left > 0 && remaining_brothers <= 0)
        {
            foreach (datum_mind brother_mind in Dm.each<datum_mind>(members))
            {
                datum_antagonist_brother brother_datum = brother_mind.has_antag_datum(typeof(datum_antagonist_brother)) as datum_antagonist_brother;
                brother_datum?.remove_conversion_skills();
            }
        }
        brothers_left = remaining_brothers;
    }
}

// tg: code/modules/antagonists/brother/brother.dm:318
public partial class datum_objective_convert_brother : datum_objective
{
    // tg: code/modules/antagonists/brother/brother.dm:324
    public override object check_completion()
    {
        return Dm.length((team as datum_team)?.members) > 1;
    }
}
