// Translated from tgstation (snapshot 1d99bd7f): code/datums/id_trim/_id_trim.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P5 (the lead): the whole file (the var block is generated).

// tg: code/datums/id_trim/_id_trim.dm:2
[DmPath("/datum/id_trim")]
public partial class datum_id_trim : datum
{
}

// M4a contracts: the roots of this file's procs that no part declared yet, with the signatures of
// docs/ai/specs/generated/slots.tsv (DmTask exactly where suspends = yes), Dm.NotTranslated until M4a P5 translates
// them or leaves them for their milestone (docs/ai/specs/m4a-ownership.md). Overrides are not declared, so the inherited
// code keeps running until then.

// tg: code/datums/id_trim/_id_trim.dm:38
public partial class datum_id_trim : datum
{
    /// Returns the job datum associated with this trim, if any.
    // tg: code/datums/id_trim/_id_trim.dm:38
    public virtual object find_job()
    {
        return null;
    }
}

// tg: code/datums/id_trim/_id_trim.dm:42
public partial class obj_item : obj
{
    /// Returns the SecHUD job icon state for whatever this object's ID card is, if it has one.
    // tg: code/datums/id_trim/_id_trim.dm:42
    public virtual object get_sechud_job_icon()
    {
        obj_item_card_id id_card = GetID() as obj_item_card_id;

        object icon = id_card?.get_trim_sechud_icon();
        return Dm.truthy(icon) ? icon : Dm.resource("icons/mob/huds/hud.dmi"); // DEFAULT_HUDS_DMI (__DEFINES/atom_hud.dm:75), a resource expanded inline (rules 3.7)
    }

    /// Returns the SecHUD job icon state for whatever this object's ID card is, if it has one.
    // tg: code/datums/id_trim/_id_trim.dm:49
    public virtual object get_sechud_job_icon_state()
    {
        obj_item_card_id id_card = GetID() as obj_item_card_id;

        object icon_state = id_card?.get_trim_sechud_icon_state();
        return Dm.truthy(icon_state) ? icon_state : SECHUD_NO_ID;
    }
}
