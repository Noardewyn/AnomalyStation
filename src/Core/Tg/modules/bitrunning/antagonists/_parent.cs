// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/antagonists/_parent.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the bitrunning glitch, the virtual antagonist a quantum server's threat spawns in a domain (its fluff objective,
// its virtual body, the agent's look and ID) and its info window (AntagInfoGlitch). The var blocks are generated.

// tg: code/modules/bitrunning/antagonists/_parent.dm:5
public partial class datum_antagonist_bitrunning_glitch : datum_antagonist
{
    // tg: code/modules/bitrunning/antagonists/_parent.dm:19
    public override async DmTask greet()
    {
        await base.greet();

        await owner.announce_objectives();
    }

    // tg: code/modules/bitrunning/antagonists/_parent.dm:24
    public override async DmTask on_gain()
    {
        await base.on_gain();

        await forge_objectives();
        await owner.current.AddComponent(typeof(datum_component_npc_friendly));

        if (iscarbon(owner.current))
        {
            mob_living_carbon carbon_mob = owner.current as mob_living_carbon;
            carbon_mob.make_virtual_mob();
        }
    }

    // tg: code/modules/bitrunning/antagonists/_parent.dm:34
    public override async DmTask forge_objectives(params object[] __extra)
    {
        datum_objective_bitrunning_glitch_fluff objective = Dm.@new<datum_objective_bitrunning_glitch_fluff>();
        objective.owner = owner;
        Dm.append(ref objectives, objective);
    }

    /// Sets up the agent so that they look like cyber police && don't have an account ID
    // tg: code/modules/bitrunning/antagonists/_parent.dm:66
    public virtual async DmTask convert_agent()
    {
        if (!ishuman(owner.current))
            return;

        mob_living_carbon_human player = owner.current as mob_living_carbon_human;

        player.set_service_style();
        await player.equipOutfit(preview_outfit);
        await player.fully_replace_character_name(player.name, pick(GLOB.cyberauth_names));
        await fix_agent_id();
    }

    /// Resets the agent's ID and name. Needed so this doesn't show as "unknown"
    // tg: code/modules/bitrunning/antagonists/_parent.dm:79
    public virtual async DmTask fix_agent_id()
    {
        if (!ishuman(owner.current))
            return;

        mob_living_carbon_human player = owner.current as mob_living_carbon_human;

        obj_item_card_id outfit_id = player.wear_id as obj_item_card_id;
        if (outfit_id == null)
            return;

        outfit_id.set_account(Dm.@new<datum_bank_account>());
        outfit_id.registered_account.replaceable = FALSE;
        outfit_id.registered_account.account_id = null;
        outfit_id.registered_name = player.name;
        outfit_id.update_label();
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/bitrunning/antagonists/_parent.dm:39
public partial class datum_objective_bitrunning_glitch_fluff : datum_objective
{
    // tg: code/modules/bitrunning/antagonists/_parent.dm:41
    public override async DmTask New(params object[] __extra)
    {
        DmList explanation_texts = Dm.list(
            "Execute termination protocol on unauthorized entities.",
            "Initialize system purge of irregular anomalies.",
            "Deploy correction algorithms on aberrant code.",
            "Run debug routine on intruding elements.",
            "Start elimination procedure for system threats.",
            "Execute defense routine against non-conformity.",
            "Commence operation to neutralize intruding scripts.",
            "Commence clean-up protocol on corrupt data.",
            "Begin scan for aberrant code for termination.",
            "Initiate lockdown on all rogue scripts.",
            "Run integrity check and purge for digital disorder."
        );
        explanation_text = I18n.T(Dm.str(pick(explanation_texts)));
        await base.New(__extra);
    }

    // tg: code/modules/bitrunning/antagonists/_parent.dm:57
    public override object check_completion()
    {
        if (Dm.locate<mob_living_carbon>(Dm.sub(GLOB.alive_player_list, owner.current)) != null)
            return FALSE;

        return TRUE;
    }
}
