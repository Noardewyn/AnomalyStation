// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/ice_cube.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. The var block is generated (D111).

/// Causes the hallucinator to believe themselves frozen in ice. Man am I glad he's frozen in there etc etc
// tg: code/modules/hallucination/ice_cube.dm:2
public partial class datum_hallucination_ice : datum_hallucination
{
    // tg: code/modules/hallucination/ice_cube.dm:16
    [DmParams("hallucinator", "duration", "play_freeze_sound")]
    public override async DmTask New(params object[] __extra)
    {
        object duration = Dm.arg(__extra, 1) ?? (6 * SECONDS);
        object play_freeze_sound = Dm.arg(__extra, 2) ?? TRUE;
        this.ice_duration = duration;
        this.play_ice_sound = play_freeze_sound;
        await base.New(__extra);
    }

    // tg: code/modules/hallucination/ice_cube.dm:21
    public override async DmTask<object> start()
    {
        ice_overlay = image(ice_icon, hallucinator, ice_icon_state, ABOVE_MOB_LAYER);
        SET_PLANE_EXPLICIT(ice_overlay, ABOVE_GAME_PLANE, hallucinator);
        if (hallucinator.client != null)
            Dm.combine(hallucinator.client.images, ice_overlay);
        ADD_TRAIT(hallucinator, TRAIT_IMMOBILIZED, HALLUCINATION_TRAIT);
        to_chat(hallucinator, span_userdanger("You become frozen in a cube!"));
        await hallucinator.cause_hallucination(typeof(datum_hallucination_fake_alert_cold), "ice hallucination", Dm.named("duration", Dm.num(ice_duration) + 6 * SECONDS));
        if (Dm.truthy(play_ice_sound))
            await hallucinator.cause_hallucination(typeof(datum_hallucination_fake_sound_weird_ice_crack), "ice hallucination");

        await hallucinator.set_jitter_if_lower(Dm.num(ice_duration) + 6 * SECONDS);
        await hallucinator.set_stutter_if_lower(Dm.num(ice_duration) + 6 * SECONDS);

        QDEL_IN_HALLUCINATION(this, Dm.num(ice_duration), "code/modules/hallucination/ice_cube.dm", 35);
        return TRUE;
    }

    // tg: code/modules/hallucination/ice_cube.dm:38
    public override object Destroy(float? force = null)
    {
        unfreeze();
        return base.Destroy(force);
    }

    // tg: code/modules/hallucination/ice_cube.dm:42
    public virtual void unfreeze()
    {
        REMOVE_TRAIT(hallucinator, TRAIT_IMMOBILIZED, HALLUCINATION_TRAIT);
        if (ice_overlay != null)
        {
            if (hallucinator.client != null)
                Dm.remove(hallucinator.client.images, ice_overlay);
            ice_overlay = null;
        }
    }
}
