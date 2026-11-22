// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/nukeop/datums/operative_support.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2c, D316: the Overwatch Intelligence Agent the overwatch beacon calls). The var blocks are generated (D111).

// tg: code/modules/antagonists/nukeop/datums/operative_support.dm:1
public partial class datum_antagonist_nukeop_support : datum_antagonist_nukeop
{
    // tg: code/modules/antagonists/nukeop/datums/operative_support.dm:7
    public override async DmTask greet()
    {
        owner.current.playsound_local(get_turf(owner.current), Dm.resource("sound/machines/printer.ogg"), 100, 0, use_reverb: FALSE);
        to_chat(owner, span_big(I18n.T("You are a []! You've been temporarily assigned to provide camera overwatch and manage communications for a nuclear operative team!", name)));
        to_chat(owner, span_red(I18n.T("Use your tools to set up your equipment however you like, but do NOT attempt to leave your outpost.")));
        await owner.announce_objectives();
    }

    // tg: code/modules/antagonists/nukeop/datums/operative_support.dm:13
    public override async DmTask on_gain()
    {
        await base.on_gain();
        foreach (datum_mind teammate_mind in Dm.each<datum_mind>(nuke_team.members))
        {
            mob_living our_teammate = teammate_mind.current as mob_living;
            if (our_teammate == null) // If an agent is purchased after the death of an agent -- when they no longer have a body, we skip that mind because they're invalid.
                continue;

            await our_teammate.AddComponent(
                typeof(datum_component_simple_bodycam),
                Dm.named("camera_name", "operative bodycam"),
                Dm.named("c_tag", Dm.text("[]", our_teammate.real_name)),
                Dm.named("network", OPERATIVE_CAMERA_NET),
                Dm.named("emp_proof", FALSE));
            our_teammate.playsound_local(get_turf(owner.current), Dm.resource("sound/items/weapons/egloves.ogg"), 100, 0);
            to_chat(our_teammate, span_notice(I18n.T("A Syndicate Overwatch Intelligence Agent has been assigned to your team. Smile, you're on camera!")));
        }

        RegisterSignal(nuke_team, COMSIG_NUKE_TEAM_ADDITION, nameof(late_bodycam));

        owner.current.grant_language(typeof(datum_language_codespeak));
    }

    // tg: code/modules/antagonists/nukeop/datums/operative_support.dm:34
    public override object get_spawnpoint()
    {
        return pick(GLOB.nukeop_base_overwatch_start);
    }

    // tg: code/modules/antagonists/nukeop/datums/operative_support.dm:37
    public override DmTask forge_objectives(params object[] __extra)
    {
        var objective = Dm.@new<datum_objective_overwatch>();
        objective.owner = owner;
        Dm.append(ref objectives, objective);
        return DmTask.CompletedTask;
    }

    // tg: code/modules/antagonists/nukeop/datums/operative_support.dm:42
    [SignalHandler]
    public virtual void late_bodycam(datum source = null, mob_living new_teammate = null)
    {
        Waiting.NoSleep(new_teammate.AddComponent(
            typeof(datum_component_simple_bodycam),
            Dm.named("camera_name", "operative bodycam"),
            Dm.named("c_tag", Dm.text("[]", new_teammate.real_name)),
            Dm.named("network", OPERATIVE_CAMERA_NET),
            Dm.named("emp_proof", FALSE)));
        to_chat(new_teammate, span_notice(I18n.T("You have been equipped with a bodycam, viewable by your Overwatch Intelligence Agent. Make sure to show them a good performance!")));
    }
}

// tg: code/modules/antagonists/nukeop/datums/operative_support.dm:53
public partial class datum_objective_overwatch : datum_objective
{
    // tg: code/modules/antagonists/nukeop/datums/operative_support.dm:56
    public override object check_completion()
    {
        return GLOB.station_was_nuked;
    }
}
