// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/sound_player.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3d, D316: the clown virus disk's honking machines). The var block is generated (D111).

/**
 * Sound Player component
 *
 * Component that will play a sound upon receiving some signal
 */
// tg: code/datums/components/sound_player.dm:6
public partial class datum_component_sound_player : datum_component
{
    // tg: code/datums/components/sound_player.dm:14
    [DmParams("volume", "sounds", "uses", "signal_list")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object dot = await base.Initialize(__extra);
        volume = Dm.arg(__extra, 0) ?? 30;
        sounds = Dm.arg<DmList>(__extra, 1) ?? Dm.list(Dm.resource("sound/items/bikehorn.ogg"));
        uses = Dm.arg(__extra, 2) ?? -1;
        DmList signal_list = Dm.arg<DmList>(__extra, 3) ?? Dm.list(COMSIG_ATOM_ATTACK_HAND);

        RegisterSignals(parent, signal_list, nameof(play_sound));
        return dot;
    }

    /**
     * Attempt to play the sound on parent
     *
     * If out of uses, will qdel itself.
     */
    // tg: code/datums/components/sound_player.dm:31
    [SignalHandler]
    public virtual void play_sound()
    {
        playsound(parent as atom, pick(sounds), volume, TRUE);
        if (Dm.num(uses) <= -1)
            return;
        uses = Dm.num(uses) - 1;
        if (!Dm.truthy(uses))
            qdel(this);
    }
}
