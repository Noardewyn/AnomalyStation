// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/mobs/guardians.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file (the guardian's own HUD buttons). The var blocks are generated.

// tg: code/datums/actions/mobs/guardians.dm:1
public partial class datum_action_cooldown_guardian : datum_action_cooldown
{
    // tg: code/datums/actions/mobs/guardians.dm:4
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return dot;
        return Dm.num(isguardian(owner));
    }
}

// tg: code/datums/actions/mobs/guardians.dm:10
public partial class datum_action_cooldown_guardian_check_type : datum_action_cooldown_guardian
{
    // tg: code/datums/actions/mobs/guardians.dm:17
    public override async DmTask<object> Activate(atom target = null)
    {
        object dot = await base.Activate(target);
        to_chat(owner, (owner as mob_living_basic_guardian)?.playstyle_string);
        return dot;
    }
}

// tg: code/datums/actions/mobs/guardians.dm:21
public partial class datum_action_cooldown_guardian_communicate : datum_action_cooldown_guardian
{
    // tg: code/datums/actions/mobs/guardians.dm:27
    public override async DmTask<object> Activate(atom target = null)
    {
        if (owner is mob_living_basic_guardian guardian)
            await guardian.communicate();
        return null;
    }
}

// tg: code/datums/actions/mobs/guardians.dm:30
public partial class datum_action_cooldown_guardian_manifest : datum_action_cooldown_guardian
{
    // tg: code/datums/actions/mobs/guardians.dm:36
    public override async DmTask<object> Activate(atom target = null)
    {
        if (owner is mob_living_basic_guardian guardian)
            return await guardian.manifest();
        return null;
    }
}

// tg: code/datums/actions/mobs/guardians.dm:39
public partial class datum_action_cooldown_guardian_recall : datum_action_cooldown_guardian
{
    // tg: code/datums/actions/mobs/guardians.dm:45
    public override async DmTask<object> Activate(atom target = null)
    {
        if (owner is mob_living_basic_guardian guardian)
            return await guardian.recall();
        return null;
    }
}

// tg: code/datums/actions/mobs/guardians.dm:48
public partial class datum_action_cooldown_guardian_toggle_light : datum_action_cooldown_guardian
{
    // tg: code/datums/actions/mobs/guardians.dm:53
    public override async DmTask<object> Activate(atom target = null)
    {
        (owner as mob_living_basic_guardian)?.toggle_light();
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/datums/actions/mobs/guardians.dm:56
public partial class datum_action_cooldown_guardian_toggle_mode : datum_action_cooldown_guardian
{
    // tg: code/datums/actions/mobs/guardians.dm:62
    public override async DmTask<object> Activate(atom target = null)
    {
        if (owner is mob_living_basic_guardian guardian)
            await guardian.toggle_modes();
        return null;
    }
}

// tg: code/datums/actions/mobs/guardians.dm:65
public partial class datum_action_cooldown_guardian_toggle_mode_assassin : datum_action_cooldown_guardian_toggle_mode
{
    // tg: code/datums/actions/mobs/guardians.dm:71
    public override object is_action_active(atom_movable_screen_movable_action_button current_button = null)
    {
        return owner.has_status_effect(typeof(datum_status_effect_guardian_stealth));
    }
}
