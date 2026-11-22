// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/bounties/virus.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4c (D298): the whole file; the advanced diseases it reads came with gaps VI1. The var blocks are generated (D111).

// tg: code/modules/cargo/bounties/virus.dm:1
public partial class datum_bounty_virus : datum_bounty
{
    // tg: code/modules/cargo/bounties/virus.dm:7
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        stat_value = rand(4, 11);
        if (rand(3) == 1)
            stat_value *= -1;
        name = I18n.T("Virus ([] of [])", stat_name, stat_value);
        description = I18n.T("Nanotrasen is interested in a virus with a [] stat of exactly []. Central Command will pay handsomely for such a virus.", stat_name, stat_value);
        reward = Dm.num(reward) + rand(0, 4) * CARGO_CRATE_VALUE;
    }

    // tg: code/modules/cargo/bounties/virus.dm:16
    public override object print_required() => I18n.T("At least 1u");

    // tg: code/modules/cargo/bounties/virus.dm:19
    public override object can_claim() => shipped;

    // tg: code/modules/cargo/bounties/virus.dm:22
    public override object applies_to(obj export = null)
    {
        if (Dm.truthy(shipped))
            return FALSE;
        if (Dm.truthy(Dm.bitand(export.flags_1, HOLOGRAM_1)))
            return FALSE;
        // tg's istype(export, /obj/item/reagent_containers || ...): the path is the whole second argument
        if (export is not obj_item_reagent_containers)
            return FALSE;
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(((datum_reagents)export.reagents).reagent_list))
        {
            foreach (datum_disease_advance virus in Dm.each<datum_disease_advance>((reagent.data as DmList)?["viruses"]))
            {
                if (Dm.truthy(accepts_virus(virus)))
                    return TRUE;
            }
        }
        return FALSE;
    }

    // tg: code/modules/cargo/bounties/virus.dm:35
    public override async DmTask<object> ship(obj export = null)
    {
        if (!Dm.truthy(applies_to(export)))
            return FALSE;
        shipped = TRUE;
        return TRUE;
    }

    // tg: code/modules/cargo/bounties/virus.dm:41
    public virtual object accepts_virus(object virus = null) => TRUE;
}

// tg: code/modules/cargo/bounties/virus.dm:44
public partial class datum_bounty_virus_resistance : datum_bounty_virus
{
    // tg: code/modules/cargo/bounties/virus.dm:47
    public override object accepts_virus(object virus = null) => Dm.eq(((datum_disease_advance)virus).totalResistance(), stat_value) ? TRUE : FALSE;
}

// tg: code/modules/cargo/bounties/virus.dm:50
public partial class datum_bounty_virus_stage_speed : datum_bounty_virus
{
    // tg: code/modules/cargo/bounties/virus.dm:53
    public override object accepts_virus(object virus = null) => Dm.eq(((datum_disease_advance)virus).totalStageSpeed(), stat_value) ? TRUE : FALSE;
}

// tg: code/modules/cargo/bounties/virus.dm:56
public partial class datum_bounty_virus_stealth : datum_bounty_virus
{
    // tg: code/modules/cargo/bounties/virus.dm:59
    public override object accepts_virus(object virus = null) => Dm.eq(((datum_disease_advance)virus).totalStealth(), stat_value) ? TRUE : FALSE;
}

// tg: code/modules/cargo/bounties/virus.dm:62
public partial class datum_bounty_virus_transmit : datum_bounty_virus
{
    // tg: code/modules/cargo/bounties/virus.dm:65
    public override object accepts_virus(object virus = null) => Dm.eq(((datum_disease_advance)virus).totalTransmittable(), stat_value) ? TRUE : FALSE;
}
