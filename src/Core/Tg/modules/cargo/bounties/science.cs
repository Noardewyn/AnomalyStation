// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/bounties/science.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4c (D298): the whole file; the science bounties are var blocks (generated) but the relic's, the genetics', the computers'
// and the reference anomaly's. A DNA injector's mutations are typepaths: their instability is read off the type (DV409).

// tg: code/modules/cargo/bounties/science.dm:2
public partial class datum_bounty_item_science_relic : datum_bounty_item_science
{
    // tg: code/modules/cargo/bounties/science.dm:8
    public override object applies_to(obj O = null)
    {
        if (!Dm.truthy(base.applies_to(O)))
            return FALSE;
        var experiment = (obj_item_assembly_relic)O;
        if (Dm.truthy(experiment.activated))
            return TRUE;
        return null;
    }
}

// tg: code/modules/cargo/bounties/science.dm:25
public partial class datum_bounty_item_science_genetics : datum_bounty_item_science
{
    // tg: code/modules/cargo/bounties/science.dm:33
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        desired_instability = rand(10, 40);
        reward = Dm.num(reward) + desired_instability * (CARGO_CRATE_VALUE * 0.2f);
        description = Dm.text("[][]", description, I18n.T(" We want a DNA injector whose total instability is higher than [] points.", desired_instability));
    }

    // tg: code/modules/cargo/bounties/science.dm:39
    public override object applies_to(obj O = null)
    {
        if (!Dm.truthy(base.applies_to(O)))
            return FALSE;
        var mutator = (obj_item_dnainjector)O;
        if (Dm.truthy(mutator.used))
            return FALSE;
        float inst_total = 0;
        foreach (object pot_mut in Dm.each(mutator.add_mutations))
        {
            var mutation = pot_mut as Type;
            if (!Dm.eq(Dm.initial(mutation, (datum_mutation p) => p.quality), POSITIVE))
                continue;
            inst_total += Dm.num(Dm.initial(mutation, (datum_mutation p) => p.instability)); // DEVIATION(DV409): tg's mutation.instability of the typepath
        }
        if (inst_total >= desired_instability)
            return TRUE;
        return FALSE;
    }
}

// tg: code/modules/cargo/bounties/science.dm:56
public partial class datum_bounty_item_science_ntnet : datum_bounty_item_science
{
    // tg: code/modules/cargo/bounties/science.dm:64
    public override object applies_to(obj O = null)
    {
        if (!Dm.truthy(base.applies_to(O)))
            return FALSE;
        if (Dm.truthy(require_powered))
        {
            if (O is not obj_item_modular_computer computer || !Dm.truthy(computer.enabled))
                return FALSE;
        }
        return TRUE;
    }
}

// tg: code/modules/cargo/bounties/science.dm:80
public partial class datum_bounty_item_science_ntnet_console : datum_bounty_item_science_ntnet
{
    // tg: code/modules/cargo/bounties/science.dm:88
    public override object applies_to(obj O = null)
    {
        if (!Dm.truthy(base.applies_to(O)))
            return FALSE;
        if (O is not obj_machinery_modular_computer computer || computer.cpu == null)
            return FALSE;
        return TRUE;
    }
}

// tg: code/modules/cargo/bounties/science.dm:98
public partial class datum_bounty_item_science_ref_anomaly : datum_bounty_item_science
{
    // tg: code/modules/cargo/bounties/science.dm:104
    public override object can_get()
    {
        object anomaly_type = wanted_types[1];
        if (Dm.num(SSresearch.created_anomaly_types[anomaly_type]) >= Dm.num(SSresearch.anomaly_hard_limit_by_type[anomaly_type]))
            return FALSE;
        return TRUE;
    }
}
