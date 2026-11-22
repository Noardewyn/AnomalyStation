// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/nukeop/datums/operative_leader.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1b, D316), with the leader's get_spawnpoint of operative.dm. The var block is generated (D111).

// tg: code/modules/antagonists/nukeop/datums/operative_leader.dm:1
public partial class datum_antagonist_nukeop_leader : datum_antagonist_nukeop
{
    // tg: code/modules/antagonists/nukeop/datums/operative.dm:85
    public override object get_spawnpoint()
    {
        return pick(GLOB.nukeop_base_leader_start);
    }

    // tg: code/modules/antagonists/nukeop/datums/operative_leader.dm:11
    public override void memorize_code()
    {
        base.memorize_code();
        var nuke_code_paper = Dm.@new<obj_item_paper>(get_turf(owner.current));
        nuke_code_paper.add_raw_text(Dm.text("The nuclear authorization code is: <b>[]</b>", nuke_team.memorized_code));
        nuke_code_paper.name = "nuclear bomb code";
        nuke_code_paper.update_appearance();
        Waiting.NoSleep(owner.current.put_in_hands(nuke_code_paper));
    }

    // tg: code/modules/antagonists/nukeop/datums/operative_leader.dm:19
    public override async DmTask give_alias()
    {
        title ??= Dm.str(pick("Czar", "Boss", "Commander", "Chief", "Kingpin", "Director", "Overlord"));
        await base.give_alias();
        if (ishuman(owner.current))
            await owner.current.fully_replace_character_name(owner.current.real_name, Dm.text("[] []", title, owner.current.real_name));
        else
            await owner.current.fully_replace_character_name(owner.current.real_name, Dm.text("[] []", nuke_team.syndicate_name, title));
    }

    // tg: code/modules/antagonists/nukeop/datums/operative_leader.dm:27
    public override async DmTask greet()
    {
        play_stinger();
        to_chat(owner, Dm.text("<span class='warningplain'><B>[]</B></span>", I18n.T("You are the Syndicate [] for this mission. You are responsible for guiding your team.", title)));
        to_chat(owner, Dm.text("<span class='warningplain'><B>[]</B></span>", I18n.T("If you feel you are not up to this task, trade your headset with another operative.")));
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_disable_warops)))) /* CONFIG_GET(flag/disable_warops) */
            to_chat(owner, Dm.text("<span class='warningplain'><B>[]</B></span>", I18n.T("In your hand you will find a special item capable of triggering a greater challenge for your team. Examine it carefully and consult with your fellow operatives before activating it.")));
        await owner.announce_objectives();
    }

    // tg: code/modules/antagonists/nukeop/datums/operative_leader.dm:35
    public override async DmTask on_gain()
    {
        await base.on_gain();
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_disable_warops)))) /* CONFIG_GET(flag/disable_warops) */
        {
            var leader = (mob_living_carbon_human)owner.current;
            var war_declaration = Dm.@new(challengeitem, leader.drop_location()) as obj_item;
            await leader.put_in_hands(war_declaration);
            nuke_team.war_button_ref = WEAKREF(war_declaration) as datum_weakref;
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(nuketeam_name_assign)), 0.1f * SECONDS, file: "code/modules/antagonists/nukeop/datums/operative_leader.dm", line: 42);
    }

    // tg: code/modules/antagonists/nukeop/datums/operative_leader.dm:44
    public virtual async DmTask nuketeam_name_assign()
    {
        if (nuke_team == null)
            return;
        await nuke_team.rename_team(await ask_name());
    }

    // tg: code/modules/antagonists/nukeop/datums/operative_leader.dm:49
    public virtual async DmTask<object> ask_name()
    {
        object randomname = pick(GLOB.last_names);
        object newname = await tgui_input_text(
            owner.current,
            Dm.text("You are the nuclear operative []. Please choose a last name for your family.", title),
            "Name change",
            randomname,
            max_length: MAX_NAME_LEN);
        if (!Dm.truthy(newname))
            newname = randomname;
        else
        {
            newname = reject_bad_name(newname);
            if (!Dm.truthy(newname))
                newname = randomname;
        }

        return capitalize(newname);
    }

    // tg: code/modules/antagonists/nukeop/datums/operative_leader.dm:67
    public override async DmTask create_team(datum_team new_team = null)
    {
        if (Dm.truthy(spawn_ship))
            await spawn_infiltrator();
        if (new_team != null)
        {
            await base.create_team(new_team);
            return;
        }
        // Leaders always make new teams
        nuke_team = await Dm.new_async(typeof(datum_team_nuclear)) as datum_team_nuclear;
    }
}
