// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/bubblegum_attack.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. The broken wall is an override image over the wall for the hallucinator (protocol 35). The var blocks are generated (D111).

/// Sends a fake bubblegum charging through a nearby wall to our target.
// tg: code/modules/hallucination/bubblegum_attack.dm:2
public partial class datum_hallucination_oh_yeah : datum_hallucination
{
    /// if haunt_them is TRUE, they will also be shown one of these lines when the hallucination occurs
    // tg: code/modules/hallucination/bubblegum_attack.dm:10  var/static/list/hallucination_lines = BUBBLEGUM_HALLUCINATION_LINES
    // tg: code/__HELPERS/hallucinations.dm:296  #define BUBBLEGUM_HALLUCINATION_LINES list(...)
    private static DmList BUBBLEGUM_HALLUCINATION_LINES() => Dm.list(
        "I AM IMMORTAL.",
        "I SHALL TAKE YOUR WORLD.",
        "I SEE YOU.",
        "YOU CANNOT ESCAPE ME FOREVER.",
        "NOTHING CAN HOLD ME.");

    // tg: code/modules/hallucination/bubblegum_attack.dm:12
    [DmParams("hallucinator", "source", "haunt_them")]
    public override async DmTask New(params object[] __extra)
    {
        object haunt_them = Dm.arg(__extra, 2) ?? FALSE;
        this.haunt_them = Dm.num(haunt_them);
        await base.New(__extra);
    }

    // tg: code/modules/hallucination/bubblegum_attack.dm:16
    public override object Destroy(float? force = null)
    {
        if (fake_broken_wall != null)
        {
            if (hallucinator.client != null)
                Dm.remove(hallucinator.client.images, fake_broken_wall);
            fake_broken_wall = null;
        }
        if (fake_rune != null)
        {
            if (hallucinator.client != null)
                Dm.remove(hallucinator.client.images, fake_rune);
            fake_rune = null;
        }

        return base.Destroy(force);
    }

    // tg: code/modules/hallucination/bubblegum_attack.dm:27
    public override async DmTask<object> start()
    {
        turf_closed_wall wall_source = null;
        foreach (turf_closed_wall found in Dm.each<turf_closed_wall>(range(7, hallucinator)))
        {
            wall_source = found; // locate() in
            break;
        }
        if (wall_source == null)
            return FALSE;

        feedback_details += Dm.text("Source: ([], [], [])", wall_source.x, wall_source.y, wall_source.z);

        turf target_landing_turf = get_turf(hallucinator);
        turf target_landing_image_turf = get_step(target_landing_turf, SOUTHWEST); // The icon is 3x3, so we shift down+left

        if (hallucinator.client != null)
        {
            fake_broken_wall = image(Dm.resource("icons/turf/floors.dmi"), wall_source, "plating", LOW_FLOOR_LAYER);
            SET_PLANE_EXPLICIT(fake_broken_wall, FLOOR_PLANE, wall_source);
            fake_broken_wall.@override = TRUE;
            fake_rune = image(Dm.resource("icons/effects/96x96.dmi"), target_landing_image_turf, "landing", ABOVE_OPEN_TURF_LAYER);
            SET_PLANE_EXPLICIT(fake_rune, FLOOR_PLANE, wall_source);

            Dm.combine(hallucinator.client.images, fake_broken_wall);
            Dm.combine(hallucinator.client.images, fake_rune);

            hallucinator.playsound_local(wall_source, Dm.resource("sound/effects/meteorimpact.ogg"), 150, TRUE);
        }

        if (Dm.truthy(haunt_them))
            to_chat(hallucinator, span_colossus(Dm.str(pick(BUBBLEGUM_HALLUCINATION_LINES()))));

        obj_effect_client_image_holder_hallucination_bubblegum fake_bubbles = Dm.@new<obj_effect_client_image_holder_hallucination_bubblegum>(wall_source, hallucinator, this);
        _addtimer(Dm.@new<datum_callback>(this, nameof(charge_loop), fake_bubbles, target_landing_turf), 1 * SECONDS, file: "code/modules/hallucination/bubblegum_attack.dm", line: 58);
        return TRUE;
    }

    /**
     * Recursive function that operates as a "fake charge" of our effect towards the target turf.
     */
    // tg: code/modules/hallucination/bubblegum_attack.dm:64
    public virtual async DmTask charge_loop(obj_effect_client_image_holder_hallucination_bubblegum fake_bubbles = null, turf landing_turf = null)
    {
        if (QDELETED(this))
            return;

        if (QDELETED(hallucinator)
            || QDELETED(fake_bubbles)
            || landing_turf == null
            || fake_bubbles.z != hallucinator.z
            || fake_bubbles.z != landing_turf.z)
        {
            qdel(this);
            return;
        }

        if (get_turf(fake_bubbles) == landing_turf || hallucinator.stat == DEAD)
        {
            QDEL_IN_HALLUCINATION(this, 3 * SECONDS, "code/modules/hallucination/bubblegum_attack.dm", 80);
            return;
        }

        await fake_bubbles.forceMove(get_step_towards(fake_bubbles, landing_turf));
        await fake_bubbles.setDir(get_dir(fake_bubbles, landing_turf));
        hallucinator.playsound_local(get_turf(fake_bubbles), Dm.resource("sound/effects/meteorimpact.ogg"), 150, TRUE);
        shake_camera(hallucinator, 2, 1);

        if (Dm.truthy(await fake_bubbles.Adjacent(hallucinator)))
        {
            await hallucinator.Paralyze(8 * SECONDS);
            await hallucinator.adjust_stamina_loss(40);
            await hallucinator.Move(get_step_away(hallucinator, fake_bubbles)); // step_away(hallucinator, fake_bubbles): BYOND's step away, as a move onto the tile away from it
            shake_camera(hallucinator, 4, 3);
            hallucinator.visible_message(
                span_warning(Dm.text("[] jumps backwards, falling on the ground!", hallucinator)),
                span_userdanger(Dm.text("[] slams into you!", fake_bubbles)));
            QDEL_IN_HALLUCINATION(this, 3 * SECONDS, "code/modules/hallucination/bubblegum_attack.dm", 99);
        }

        else
            _addtimer(Dm.@new<datum_callback>(this, nameof(charge_loop), fake_bubbles, landing_turf), 0.2f * SECONDS, file: "code/modules/hallucination/bubblegum_attack.dm", line: 101);
    }
}
