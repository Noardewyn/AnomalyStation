// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/shock.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. The images hang on the hallucinator himself, which first person never draws (DV110); the black override
// picture is what others would see in tg, here only the hallucinator's client gets it. The var block is generated (D111).

/// Causes a fake "zap" to the hallucinator.
// tg: code/modules/hallucination/shock.dm:2
public partial class datum_hallucination_shock : datum_hallucination
{
    // tg: code/modules/hallucination/shock.dm:11
    [DmParams("hallucinator")]
    public override async DmTask New(params object[] __extra)
    {
        electrocution_icon_state = ishuman(Dm.arg(__extra, 0)) ? "electrocuted_base" : "electrocuted_generic";
        await base.New(__extra);
    }

    // tg: code/modules/hallucination/shock.dm:15
    public override object Destroy(float? force = null)
    {
        if (shock_image != null)
        {
            if (hallucinator.client != null)
                Dm.remove(hallucinator.client.images, shock_image);
            shock_image = null;
        }
        if (electrocution_skeleton_anim != null)
        {
            if (hallucinator.client != null)
                Dm.remove(hallucinator.client.images, electrocution_skeleton_anim);
            electrocution_skeleton_anim = null;
        }

        return base.Destroy(force);
    }

    // tg: code/modules/hallucination/shock.dm:26
    public override async DmTask<object> start()
    {
        shock_image = image(hallucinator, hallucinator, dir: hallucinator.dir);
        Dm.combine(ref shock_image.appearance_flags, KEEP_APART);
        shock_image.color = rgb(0, 0, 0);
        shock_image.@override = TRUE;

        electrocution_skeleton_anim = image(electrocution_icon, hallucinator, electrocution_icon_state, ABOVE_MOB_LAYER);
        Dm.combine(ref electrocution_skeleton_anim.appearance_flags, Dm.bitor(RESET_COLOR, KEEP_APART));
        if (ishuman(hallucinator))
        {
            mob_living_carbon_human human_hallucinator = (mob_living_carbon_human)hallucinator;
            human_hallucinator.apply_height(electrocution_skeleton_anim, ENTIRE_BODY);
        }

        SET_PLANE_EXPLICIT(shock_image, ABOVE_GAME_PLANE, hallucinator);
        SET_PLANE_EXPLICIT(electrocution_skeleton_anim, ABOVE_GAME_PLANE, hallucinator);

        to_chat(hallucinator, span_userdanger("You feel a powerful shock course through your body!"));
        hallucinator.visible_message(span_warning(Dm.text("[] falls to the ground, shaking!", hallucinator)), ignored_mobs: hallucinator);
        if (hallucinator.client != null)
        {
            Dm.combine(hallucinator.client.images, shock_image);
            Dm.combine(hallucinator.client.images, electrocution_skeleton_anim);
        }

        hallucinator.playsound_local(get_turf(this), SFX_SPARKS, 100, TRUE);
        await hallucinator.adjust_stamina_loss(50);
        await hallucinator.Stun(4 * SECONDS);
        hallucinator.do_jitter_animation(300); // Maximum jitter
        await hallucinator.adjust_jitter(20 * SECONDS);

        _addtimer(Dm.@new<datum_callback>(this, nameof(reset_shock_animation)), 4 * SECONDS, file: "code/modules/hallucination/shock.dm", line: 52);
        _addtimer(Dm.@new<datum_callback>(this, nameof(shock_drop)), 2 * SECONDS, file: "code/modules/hallucination/shock.dm", line: 53);
        QDEL_IN_HALLUCINATION(this, 4 * SECONDS, "code/modules/hallucination/shock.dm", 54);
        return TRUE;
    }

    // tg: code/modules/hallucination/shock.dm:57
    public virtual void reset_shock_animation()
    {
        if (QDELETED(hallucinator))
            return;

        if (hallucinator.client != null)
            Dm.remove(hallucinator.client.images, shock_image);
        shock_image = null;

        if (hallucinator.client != null)
            Dm.remove(hallucinator.client.images, electrocution_skeleton_anim);
        electrocution_skeleton_anim = null;
    }

    // tg: code/modules/hallucination/shock.dm:66
    public virtual async DmTask shock_drop()
    {
        if (QDELETED(hallucinator))
            return;

        await hallucinator.Paralyze(6 * SECONDS);
    }
}
