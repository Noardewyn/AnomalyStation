// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/death_sound.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DE1 (D333): the whole file (a sound played when the mob dies while it lasts; the item kill sound component's). The var
// block is generated (D111).

// tg: code/datums/status_effects/death_sound.dm:1
public partial class datum_status_effect_death_sound : datum_status_effect
{
    // tg: code/datums/status_effects/death_sound.dm:9
    [DmParams("new_owner", "duration", "death_sound", "death_sound_volume")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        object death_sound = Dm.arg(__extra, 1);
        object death_sound_volume = Dm.arg(__extra, 2) ?? 100;
        this.duration = Dm.num_or_null(duration);
        this.death_sound = death_sound;
        this.death_sound_volume = Dm.num_or_null(death_sound_volume);
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/datums/status_effects/death_sound.dm:15
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        RegisterSignal(owner, COMSIG_LIVING_DEATH, nameof(on_death));
        return TRUE;
    }

    // tg: code/datums/status_effects/death_sound.dm:19
    [SignalHandler]
    public virtual void on_death()
    {
        playsound(owner, death_sound, death_sound_volume, FALSE);
    }

    // tg: code/datums/status_effects/death_sound.dm:23
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, COMSIG_LIVING_DEATH);
        return null;
    }
}
