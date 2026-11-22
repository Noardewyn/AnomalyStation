// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/squish_sound.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M6 slice 5 (gibbing): the whole file (the squelch of stepping on gibs). The var block is generated.

///Plays a sound when walked into, lower sounding if the person walking into it has light stepping.
// tg: code/datums/elements/squish_sound.dm:2
public partial class datum_element_squish_sound : datum_element
{
    // tg: code/datums/elements/squish_sound.dm:8
    [DmParams("target", "sound")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object sound = Dm.arg(__extra, 0) ?? Dm.resource("sound/effects/footstep/gib_step.ogg");
        object dot = await base.Attach(target, Dm.forward(__extra, sound));
        sound_to_play = sound;
        RegisterSignal(target, COMSIG_MOVABLE_CROSS, nameof(on_cross));
        return dot;
    }

    ///Plays the set sound upon being entered, as long as the person walking into it can actually walk.
    // tg: code/datums/elements/squish_sound.dm:17
    [SignalHandler]
    public virtual void on_cross(atom_movable source = null, atom_movable crossed = null)
    {
        if (!isliving(crossed) || Dm.truthy(Dm.bitand(crossed.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)) || Dm.truthy(crossed.throwing))
            return;
        playsound(
            source: source,
            soundin: sound_to_play,
            vol: HAS_TRAIT(crossed, TRAIT_LIGHT_STEP) ? 20 : 50,
            vary: TRUE);
    }
}
