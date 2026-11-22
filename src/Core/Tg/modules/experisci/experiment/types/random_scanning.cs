// Translated from tgstation (snapshot 1d99bd7f): code/modules/experisci/experiment/types/random_scanning.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RD3 (D333, the experiments): the whole file: the random scanning experiment, whose required types and counts are drawn when the
// techweb makes it, and the exosuit stress and load tests. The var blocks are generated (D111).

// tg: code/modules/experisci/experiment/types/random_scanning.dm:1
public partial class datum_experiment_scanning_random : datum_experiment_scanning
{
    // tg: code/modules/experisci/experiment/types/random_scanning.dm:11
    [DmParams("techweb")]
    public override async DmTask New(params object[] __extra)
    {
        // Generate random contents
        if (Dm.truthy(possible_types.len))
        {
            float picked = 0;
            while (picked < total_requirement)
            {
                float randum = min(rand(1, total_requirement - picked), max_requirement_per_type); //Short for "random num" and not "this is dum that I have to rename 100 different variable names"
                Dm.append_at(required_atoms, pick(possible_types), randum);
                picked += randum;
            }
        }

        // Fill the experiment as per usual
        await base.New(__extra);
    }
}

// tg: code/modules/experisci/experiment/types/random_scanning.dm:23
public partial class datum_experiment_scanning_random_mecha_damage_scan : datum_experiment_scanning_random
{
    // tg: code/modules/experisci/experiment/types/random_scanning.dm:23
    [DmParams("techweb")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        damage_percent = rand(15, 95);
        //updating the description with the damage_percent var set
        description = I18n.T("Your exosuit fabricators allow for rapid production on a small scale, but the structural integrity of created parts is inferior to those made with more traditional means. Damage a few exosuits to around []% integrity and scan them to help us determine how the armor fails under stress.", damage_percent);
    }

    // tg: code/modules/experisci/experiment/types/random_scanning.dm:29
    public override async DmTask<object> final_contributing_index_checks(datum_component_experiment_handler experiment_handler = null, atom target = null, object typepath = null)
    {
        float found_percent = round((Dm.div(Dm.num(target.get_integrity()), target.max_integrity)) * 100);
        return (await base.final_contributing_index_checks(experiment_handler, target, typepath) is var and__1 && !Dm.truthy(and__1) ? and__1 : Dm.num(ISINRANGE(found_percent, Dm.num(damage_percent) - 5, Dm.num(damage_percent) + 5)));
    }
}

// tg: code/modules/experisci/experiment/types/random_scanning.dm:33
public partial class datum_experiment_scanning_random_mecha_equipped_scan : datum_experiment_scanning_random
{
    // tg: code/modules/experisci/experiment/types/random_scanning.dm:33
    public override async DmTask<object> final_contributing_index_checks(datum_component_experiment_handler experiment_handler = null, atom target = null, object typepath = null)
    {
        obj_vehicle_sealed_mecha stompy = target as obj_vehicle_sealed_mecha;
        if (!(stompy is obj_vehicle_sealed_mecha))
            return FALSE; //Not a mech
        if (!HAS_TRAIT(stompy, TRAIT_MECHA_CREATED_NORMALLY))
        {
            await experiment_handler.announce_message(I18n.T("Scanned mech was not made by crew. There is nothing to learn here."));
            return FALSE; //Not hand-crafted
        }
        if (!(Dm.truthy(stompy.equip_by_category[MECHA_L_ARM]) && Dm.truthy(stompy.equip_by_category[MECHA_R_ARM])))
        {
            await experiment_handler.announce_message(I18n.T("Scanned mech is missing equipment on their arms to proceed this experiment."));
            return FALSE; //Both arms need be filled
        }
        return await base.final_contributing_index_checks(experiment_handler, target, typepath);
    }
}
