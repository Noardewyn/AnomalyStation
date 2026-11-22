// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/noisy_movement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 7 (furniture): the whole file (the office chair's rolling sound). The var block is generated.

// tg: code/datums/elements/noisy_movement.dm:2
public partial class datum_element_noisy_movement : datum_element
{
    // tg: code/datums/elements/noisy_movement.dm:7
    [DmParams("target", "movement_sound", "volume")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object movement_sound = Dm.arg(__extra, 0);
        object volume = Dm.arg(__extra, 1);
        movement_sound ??= Dm.resource("sound/effects/roll.ogg");
        volume ??= 100;
        object dot = await base.Attach(target, Dm.forward(__extra, movement_sound, volume));
        if (!ismovable(target))
            return ELEMENT_INCOMPATIBLE;

        this.movement_sound = movement_sound;
        this.volume = volume;

        RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(play_sound));
        return dot;
    }

    // tg: code/datums/elements/noisy_movement.dm:17
    [SignalHandler]
    public virtual void play_sound(atom_movable source = null, object old_loc = null, object movement_dir = null, object forced = null)
    {
        if (!Dm.truthy(forced) && !Dm.truthy(CHECK_MOVE_LOOP_FLAGS(source, MOVEMENT_LOOP_OUTSIDE_CONTROL)) && Dm.truthy(source.has_gravity()))
            playsound(source, movement_sound, volume, TRUE);
    }
}
