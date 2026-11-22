// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/blood_flow.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. The var block is generated (D111).

// tg: code/modules/hallucination/blood_flow.dm:1
public partial class datum_hallucination_blood_flow : datum_hallucination
{
    // tg: code/modules/hallucination/blood_flow.dm:8
    public override async DmTask<object> start()
    {
        if (hallucinator.client == null || !iscarbon(hallucinator))
            return FALSE;

        mob_living_carbon carb_hallucinator = (mob_living_carbon)hallucinator;
        DmList bodyparts = carb_hallucinator.get_bodyparts() as DmList;
        if (length(bodyparts) == 0 || !Dm.truthy(carb_hallucinator.can_bleed()))
            return FALSE;

        obj_item_bodypart picked = null;
        while (picked == null && length(bodyparts) != 0)
        {
            picked = pick_n_take(bodyparts) as obj_item_bodypart;
            if (!Dm.truthy(picked.can_bleed()))
                picked = null;
        }

        if (picked == null)
            return FALSE;

        bleeding_bodypart = picked;

        feedback_details += Dm.text("Bleeding: []", bleeding_bodypart);

        RegisterSignals(bleeding_bodypart, Dm.list(COMSIG_QDELETING, COMSIG_BODYPART_REMOVED), nameof(stop_bleeding));
        RegisterSignal(hallucinator, COMSIG_LIVING_UPDATE_BLOOD_STATUS, nameof(stop_bleeding));

        to_chat(hallucinator, span_warning(Dm.text("Your [] looses a spray of blood!", bleeding_bodypart.plaintext_zone)));
        float bleed_duration = rand(16 * SECONDS, 40 * SECONDS);
        _addtimer(Dm.@new<datum_callback>(this, nameof(stop_bleeding)), bleed_duration, file: "code/modules/hallucination/blood_flow.dm", line: 40);
        if (prob(25))
            _addtimer(Dm.@new<datum_callback>(this, nameof(by_god)), bleed_duration * Dm.num(pick(0.5f, 0.66f)), file: "code/modules/hallucination/blood_flow.dm", line: 42);
        stamina_loop();

        hallucinator.playsound_local(get_turf(hallucinator), pick(Dm.resource("sound/effects/wounds/blood1.ogg"), Dm.resource("sound/effects/wounds/blood2.ogg"), Dm.resource("sound/effects/wounds/blood3.ogg")), 50, TRUE);
        bleeding = image(
            icon: Dm.resource("icons/mob/effects/bleed_overlays.dmi"),
            icon_state: Dm.text("[]_[]", bleeding_bodypart.body_zone, pick(2, 3)),
            loc: hallucinator);
        datum_blood_type bloodtype = carb_hallucinator.get_bloodtype() as datum_blood_type;
        object wound_color = bloodtype != null ? await bloodtype.get_wound_color(carb_hallucinator) : null;
        bleeding.color = Dm.truthy(wound_color) ? wound_color : BLOOD_COLOR_RED;
        bleeding.layer = -WOUND_LAYER;
        if (hallucinator.client != null)
            Dm.append(hallucinator.client.images, bleeding);
        return TRUE;
    }

    // tg: code/modules/hallucination/blood_flow.dm:55
    public override object Destroy(float? force = null)
    {
        if (hallucinator.client != null)
            Dm.remove(hallucinator.client.images, bleeding);
        return base.Destroy(force);
    }

    // tg: code/modules/hallucination/blood_flow.dm:59
    public virtual void by_god()
    {
        if (QDELETED(this) || QDELETED(hallucinator) || QDELETED(bleeding_bodypart))
            return;

        to_chat(hallucinator, span_warning(Dm.text("The blood doesn't stop flowing, yet [] doesn't seem to hurt...", bleeding_bodypart.plaintext_zone)));
    }

    // tg: code/modules/hallucination/blood_flow.dm:65
    [SignalHandler]
    public virtual void on_update_blood_status(datum source = null, object had_blood = null, object has_blood = null, object old_blood_volume = null, object new_blood_volume = null)
    {
        if (!Dm.truthy(has_blood))
            stop_bleeding();
    }

    // tg: code/modules/hallucination/blood_flow.dm:70
    [SignalHandler]
    public virtual void stop_bleeding()
    {
        UnregisterSignal(bleeding_bodypart, Dm.list(COMSIG_QDELETING, COMSIG_BODYPART_REMOVED));
        UnregisterSignal(hallucinator, COMSIG_LIVING_UPDATE_BLOOD_STATUS);
        if (!QDELETED(bleeding_bodypart))
            to_chat(hallucinator, span_warning(Dm.text("Your [] stops bleeding.", bleeding_bodypart.plaintext_zone)));
        if (!QDELETED(this))
            qdel(this);
    }

    // tg: code/modules/hallucination/blood_flow.dm:79  set waitfor = FALSE
    public virtual void stamina_loop()
    {
        Waiting.WaitforFalse(stamina_loop__body());
    }

    private async DmTask stamina_loop__body()
    {
        while (!QDELETED(this) && !QDELETED(hallucinator))
        {
            await hallucinator.adjust_stamina_loss(5);
            await sleep(4 * SECONDS);
        }
    }
}
