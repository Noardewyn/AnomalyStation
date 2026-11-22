// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/xeno_attack.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. The xeno is an invisible effect with an image for the hallucinator, thrown over the tiles. The var blocks are generated (D111).

/// Xeno crawls from nearby vent, jumps at you, and goes back in.
// tg: code/modules/hallucination/xeno_attack.dm:2
public partial class datum_hallucination_xeno_attack : datum_hallucination
{
    // tg: code/modules/hallucination/xeno_attack.dm:6
    public override async DmTask<object> start()
    {
        if (IS_UNCONSCIOUS(hallucinator))
            return FALSE;

        turf xeno_attack_source = null;
        foreach (obj_machinery_atmospherics_components_unary_vent_pump nearby_pump in Dm.each<obj_machinery_atmospherics_components_unary_vent_pump>(orange(7, hallucinator)))
        {
            if (Dm.truthy(nearby_pump.welded))
                continue;
            xeno_attack_source = get_turf(nearby_pump);
            break;
        }

        if (xeno_attack_source == null)
            return FALSE;

        feedback_details += Dm.text("Vent Coords: ([], [], [])", xeno_attack_source.x, xeno_attack_source.y, xeno_attack_source.z);

        obj_effect_client_image_holder_hallucination_xeno fake_xeno = Dm.@new<obj_effect_client_image_holder_hallucination_xeno>(xeno_attack_source, hallucinator, this);
        _addtimer(Dm.@new<datum_callback>(this, nameof(leap_at_target), fake_xeno, xeno_attack_source), 1 * SECONDS, file: "code/modules/hallucination/xeno_attack.dm", line: 24);
        return TRUE;
    }

    /// Leaps from the vent to the hallucinator.
    // tg: code/modules/hallucination/xeno_attack.dm:27
    public virtual async DmTask leap_at_target(obj_effect_client_image_holder_hallucination_xeno fake_xeno = null, turf attack_source = null)
    {
        if (QDELETED(this))
            return;
        if (QDELETED(fake_xeno))
        {
            qdel(this);
            return;
        }

        fake_xeno.set_leaping();
        await fake_xeno.throw_at(hallucinator, 7, 1, spin: FALSE, diagonals_first: TRUE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(leap_back_to_pump), fake_xeno), 1 * SECONDS, file: "code/modules/hallucination/xeno_attack.dm", line: 37);
    }

    /// Leaps from the hallucinator back to the vent.
    // tg: code/modules/hallucination/xeno_attack.dm:40
    public virtual async DmTask leap_back_to_pump(obj_effect_client_image_holder_hallucination_xeno fake_xeno = null, turf attack_source = null)
    {
        if (QDELETED(this))
            return;
        if (QDELETED(fake_xeno) || attack_source == null)
        {
            qdel(this);
            return;
        }

        fake_xeno.set_leaping();
        await fake_xeno.throw_at(attack_source, 7, 1, spin: FALSE, diagonals_first: TRUE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(begin_crawling), fake_xeno), 1 * SECONDS, file: "code/modules/hallucination/xeno_attack.dm", line: 50);
    }

    /// Mimics ventcrawling into the vent.
    // tg: code/modules/hallucination/xeno_attack.dm:53
    public virtual void begin_crawling(obj_effect_client_image_holder_hallucination_xeno fake_xeno = null)
    {
        if (QDELETED(this))
            return;
        if (QDELETED(fake_xeno))
        {
            qdel(this);
            return;
        }

        to_chat(hallucinator, span_notice(Dm.text("[] begins climbing into the ventilation system...", fake_xeno.name)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(disappear), fake_xeno), 3 * SECONDS, file: "code/modules/hallucination/xeno_attack.dm", line: 63);
    }

    /// Disappears into the vent, ending the hallucination.
    // tg: code/modules/hallucination/xeno_attack.dm:66
    public virtual void disappear(obj_effect_client_image_holder_hallucination_xeno fake_xeno = null)
    {
        if (QDELETED(this))
            return;
        if (!QDELETED(fake_xeno))
            to_chat(hallucinator, span_notice(Dm.text("[] scrambles into the ventilation ducts!", fake_xeno.name)));

        qdel(this);
    }
}

/// The xeno hallucination that goes with the xeno attack hallucination.
// tg: code/modules/hallucination/xeno_attack.dm:75
public partial class obj_effect_client_image_holder_hallucination_xeno : obj_effect_client_image_holder_hallucination
{
    // tg: code/modules/hallucination/xeno_attack.dm:79
    [DmParams("mapload", "mobs_which_see_us", "parent")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        name = Dm.text("alien hunter ([])", rand(1, 1000));
        return dot;
    }

    // The hallucination "throws" us at the hallucinator, so whenever we impact, we're actually landing a "leap".
    // tg: code/modules/hallucination/xeno_attack.dm:84
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        set_unleaping();
        if (!isliving(hit_atom))
            return null;
        mob_living hit_living = (mob_living)hit_atom;
        if (hit_living != parent.hallucinator || hit_living.stat == DEAD)
            return null;
        Waiting.NoSleep(hit_living.Paralyze(10 * SECONDS));
        hit_living.visible_message(
            span_warning(Dm.text("[] flails around wildly.", hit_living)),
            span_userdanger(Dm.text("[] pounces on you!", name)));
        return null;
    }

    /// Sets our icon to look like we're leaping.
    // tg: code/modules/hallucination/xeno_attack.dm:96
    public virtual void set_leaping()
    {
        image_icon = Dm.resource("icons/mob/nonhuman-player/alienleap.dmi");
        image_state = "alienh_leap";
        image_pixel_x = -32;
        image_pixel_y = -32;
        update_appearance(UPDATE_ICON);
    }

    /// Resets our icon to our initial state.
    // tg: code/modules/hallucination/xeno_attack.dm:103
    public virtual void set_unleaping()
    {
        image_icon = Dm.initial(type, (obj_effect_client_image_holder p) => p.image_icon);
        image_state = Dm.initial(type, (obj_effect_client_image_holder p) => p.image_state);
        image_pixel_x = Dm.initial(type, (obj_effect_client_image_holder p) => p.image_pixel_x);
        image_pixel_y = Dm.initial(type, (obj_effect_client_image_holder p) => p.image_pixel_y);
        update_appearance(UPDATE_ICON);
    }
}
