// Translated from tgstation (snapshot 1d99bd7f): code/datums/mind/initialization.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only /mob/mind_initialize, /mob/living/carbon/mind_initialize and /mob/ghost_before_admin_delete translated (the
// ghosts, 2026-10-16, D151), the cyborg's override (:29, silicons SI2, D326) and the AI's (:24, silicons SI4: the first of its two
// definitions, ai.dm:128 overrides it, rules 2.5); the pAI's (:35) comes with the pAI (silicons SI6).

//Initialisation procs
// tg: code/datums/mind/initialization.dm:2
public partial class mob : atom_movable
{
    // tg: code/datums/mind/initialization.dm:2
    public virtual async DmTask mind_initialize()
    {
        if (Dm.truthy(mind))
        {
            mind.key = key;
        }
        else
        {
            mind = Dm.@new<datum_mind>(key);
            Dm.append(ref SSticker.minds, mind);
        }
        if (!Dm.truthy(mind.name))
            mind.name = real_name;
        mind.set_current(this);
        // There's nowhere else to set this up, mind code makes me depressed
        mind.antag_hud = add_alt_appearance(typeof(datum_atom_hud_alternate_appearance_basic_antagonist_hud), "combo_hud", mind) as datum_atom_hud_alternate_appearance_basic_antagonist_hud;
        RegisterSignal(this, COMSIG_ADMIN_DELETING, nameof(ghost_before_admin_delete), @override: TRUE);
        if (Dm.truthy(_listen_lookup?[COMSIG_MOB_MIND_INITIALIZED]))
            _SendSignal(COMSIG_MOB_MIND_INITIALIZED, Dm.list(this, mind));
    }

    /// Signal proc for [COMSIG_ADMIN_DELETING], to ghostize a mob beforehand if an admin is manually deleting it.
    // tg: code/datums/mind/initialization.dm:43
    [SignalHandler]
    public virtual void ghost_before_admin_delete(datum source = null)
    {
        ghostize(can_reenter_corpse: FALSE);
    }
}

// tg: code/datums/mind/initialization.dm:19
public partial class mob_living_carbon : mob_living
{
    // tg: code/datums/mind/initialization.dm:19
    public override async DmTask mind_initialize()
    {
        await base.mind_initialize();
        last_mind = mind;
    }
}

//AI
// tg: code/datums/mind/initialization.dm:24
public partial class mob_living_silicon_ai : mob_living_silicon
{
    // The first definition of mind_initialize (tg defines it again in ai.dm:128, whose ..() calls this one, rules 2.5).
    // tg: code/datums/mind/initialization.dm:24
    public virtual async DmTask mind_initialize__d0()
    {
        await base.mind_initialize();
        mind.set_assigned_role(SSjob.get_job_type(typeof(datum_job_ai)) as datum_job);
    }
}

//BORG
// tg: code/datums/mind/initialization.dm:29
public partial class mob_living_silicon_robot : mob_living_silicon
{
    // tg: code/datums/mind/initialization.dm:29
    public override async DmTask mind_initialize()
    {
        await base.mind_initialize();
        mind.set_assigned_role(SSjob.get_job_type(typeof(datum_job_cyborg)) as datum_job);
    }
}

//PAI
// tg: code/datums/mind/initialization.dm:36  (silicons SI6)
public partial class mob_living_silicon_pai : mob_living_silicon
{
    // tg: code/datums/mind/initialization.dm:36
    public override async DmTask mind_initialize()
    {
        await base.mind_initialize();
        mind.set_assigned_role(SSjob.get_job_type(typeof(datum_job_personal_ai)) as datum_job);
    }
}
