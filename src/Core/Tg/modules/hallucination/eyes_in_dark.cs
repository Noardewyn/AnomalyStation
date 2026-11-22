// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/eyes_in_dark.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. The eyes are an effect that every other viewer is shown an empty override image over (the alternate
// appearance, protocol 35), so only the hallucinator sees them. The emissive eyes have no plane to draw on (DV12). The var blocks are generated (D111).

// tg: code/modules/hallucination/eyes_in_dark.dm:1
public partial class datum_hallucination_eyes_in_dark : datum_hallucination
{
    // tg: code/modules/hallucination/eyes_in_dark.dm:7
    public override object Destroy(float? force = null)
    {
        if (QDELETED(eyes))
            eyes = null;
        else
            QDEL_NULL(ref eyes);
        return base.Destroy(force);
    }

    // tg: code/modules/hallucination/eyes_in_dark.dm:14
    public override async DmTask<object> start()
    {
        if (hallucinator.client == null || IS_UNCONSCIOUS(hallucinator))
            return FALSE;

        if (hallucinator.lighting_cutoff >= 2.5f)
            return FALSE;

        DmList valid = Dm.list();
        foreach (turf_open nearby in Dm.each<turf_open>(view(hallucinator)))
        {
            if (Dm.truthy(nearby.check_lumcount_above(LIGHTING_TILE_IS_DARK)))
                continue;
            Dm.append(valid, nearby);
        }

        if (length(valid) == 0)
            return FALSE;

        if (prob(5))
            to_chat(hallucinator, span_warning("You feel like you're being watched..."));

        turf selected = pick(valid) as turf;
        feedback_details += Dm.text("Eye coords: [], [], []", selected.x, selected.y, selected.z);
        eyes = Dm.@new<obj_effect_abstract_floating_eyes>(selected, hallucinator);
        RegisterSignal(eyes, COMSIG_QDELETING, nameof(end_hallucination));
        _addtimer(Dm.@new<datum_callback>(this, nameof(end_hallucination_gracefully)), rand(60 * SECONDS, 180 * SECONDS), file: "code/modules/hallucination/eyes_in_dark.dm", line: 36);
        return TRUE;
    }

    // tg: code/modules/hallucination/eyes_in_dark.dm:40
    public virtual void end_hallucination_gracefully()
    {
        animate(eyes, Dm.named("alpha", 0), Dm.named("time", 1 * SECONDS));
        QDEL_IN_HALLUCINATION(this, 1.2f * SECONDS, "code/modules/hallucination/eyes_in_dark.dm", 42);
    }

    // tg: code/modules/hallucination/eyes_in_dark.dm:44
    [SignalHandler]
    public virtual void end_hallucination()
    {
        if (!QDELETED(this))
            qdel(this);
    }
}

// tg: code/modules/hallucination/eyes_in_dark.dm:49
public partial class obj_effect_abstract_floating_eyes : obj_effect_abstract
{
    // tg: code/modules/hallucination/eyes_in_dark.dm:54
    [DmParams("mapload", "seer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        mob seer = Dm.arg<mob>(__extra, 0);
        object dot = null;
        dot = base.Initialize(mapload, Dm.forward(__extra, seer));
        if (seer == null)
            return INITIALIZE_HINT_QDEL;

        seer_ref = WEAKREF(seer) as datum_weakref;
        image make_invis = image(null, this);
        make_invis.@override = TRUE;
        add_alt_appearance(typeof(datum_atom_hud_alternate_appearance_basic_one_person_reversed), "hallucination", make_invis, null, seer);
        START_PROCESSING(SSfastprocess, this);
        update_appearance();
        return dot;
    }

    // tg: code/modules/hallucination/eyes_in_dark.dm:64
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSfastprocess, this);
        return base.Destroy(force);
    }

    // tg: code/modules/hallucination/eyes_in_dark.dm:68
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        mutable_appearance r_eye = (mutable_appearance)mutable_appearance(Dm.resource("icons/mob/human/human_eyes.dmi"), "eyes_glow_r");
        r_eye.color = COLOR_DARK_RED;
        Dm.append(dot as DmList, r_eye);
        mutable_appearance l_eye = (mutable_appearance)mutable_appearance(Dm.resource("icons/mob/human/human_eyes.dmi"), "eyes_glow_l");
        l_eye.color = COLOR_DARK_RED;
        Dm.append(dot as DmList, l_eye);

        // . += emissive_appearance('icons/mob/human/human_eyes.dmi', "eyes_glow_l", src), "eyes_glow_r": DEVIATION(DV12), no emissive plane
        return dot;
    }

    // tg: code/modules/hallucination/eyes_in_dark.dm:80
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        turf below_us = get_turf(this);
        mob seer = seer_ref?.resolve() as mob;
        if (Dm.truthy(below_us.check_lumcount_above(LIGHTING_TILE_IS_DARK)) || (seer != null && seer.lighting_cutoff >= 2.5f) || get_dist(seer, this) <= 1)
            graceful_delete();
        return null;
    }

    // tg: code/modules/hallucination/eyes_in_dark.dm:86
    public virtual void graceful_delete()
    {
        STOP_PROCESSING(SSfastprocess, this);
        animate(this, Dm.named("alpha", 0), Dm.named("time", 0.5f * SECONDS));
        datum_hallucination.QDEL_IN_HALLUCINATION(this, 0.75f * SECONDS, "code/modules/hallucination/eyes_in_dark.dm", 89);
    }
}
