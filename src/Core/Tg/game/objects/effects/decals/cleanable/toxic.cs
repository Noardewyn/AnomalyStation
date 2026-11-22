// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/decals/cleanable/toxic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI1c, D326: industrial waste's sludge). Its particles are deferred leaves (a client visual not drawn
// yet). The var block is generated (D111).

// tg: code/game/objects/effects/decals/cleanable/toxic.dm:3
public partial class obj_effect_decal_cleanable_greenglow_waste : obj_effect_decal_cleanable_greenglow
{
    // tg: code/game/objects/effects/decals/cleanable/toxic.dm:1  #define DISSOLVE_DURATION (#undef at :73)
    private const float DISSOLVE_DURATION = 45 * SECONDS;

    // tg: code/game/objects/effects/decals/cleanable/toxic.dm:22
    [DmParams("mapload", "diseases")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        animate(this, Dm.named("alpha", 255), Dm.named("time", 0.5f * SECONDS));

        mutable_appearance splash_animation = mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "splash_hydroponics") as mutable_appearance;
        splash_animation.color = "#15ff00";
        flick_overlay_view(splash_animation, 1.1f * SECONDS);
        return dot;
    }

    // tg: code/game/objects/effects/decals/cleanable/toxic.dm:30
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref bubbling_audio);
        // remove_shared_particles(/particles/acid/toxic): particles (a client visual) are not drawn yet
        return base.Destroy(force);
    }

    /**
     * Sets up our waste to perform dissolve_floor after the timer goes off.
     */
    // tg: code/game/objects/effects/decals/cleanable/toxic.dm:38
    public virtual void pre_dissolve(float? display_message = null, float? dissolve_clock = null)
    {
        display_message ??= TRUE;
        dissolve_clock ??= DISSOLVE_DURATION;
        if (Dm.truthy(display_message))
            visible_message(span_warning(I18n.T("\\The [] begins corroding \\the []!", this, get_turf(this))));
        color = "#ffffffff";

        playsound(this, Dm.resource("sound/items/tools/welder.ogg"), 50, TRUE);
        bubbling_audio = Dm.@new<datum_looping_sound_soup_toxic>(this);
        bubbling_audio.start();

        dissolve_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(dissolve_floor)), dissolve_clock.Value, Dm.bitor(TIMER_STOPPABLE, TIMER_DELETE_ME), file: "code/game/objects/effects/decals/cleanable/toxic.dm", line: 48);
        active = TRUE;
        update_appearance();
        // add_shared_particles(/particles/acid/toxic): particles (a client visual) are not drawn yet
        Dm.Deferred("code/game/objects/effects/decals/cleanable/toxic.dm:50", "pre_dissolve: the toxic acid particles (add_shared_particles, a client visual) (M10+)");
    }

    // tg: code/game/objects/effects/decals/cleanable/toxic.dm:52
    public virtual async DmTask dissolve_floor()
    {
        if (QDELETED(this))
            return;
        atom splashed_turf = get_turf(this);
        if (!isfloorturf(splashed_turf))
            return;
        turf_open splash_floor = splashed_turf as turf_open;
        await splash_floor.ScrapeAway(flags: CHANGETURF_IGNORE_AIR); //Eat away the floor
        visible_message(span_warning(I18n.T("The waste eats away at the floor, leaving \\the [] behind.", get_turf(this))));
        animate(this, Dm.named("time", 0.5f * SECONDS), Dm.named("color", "#bebebe8e"));
        bubbling_audio?.stop();
        active = TRUE;
        update_appearance();
        // remove_shared_particles(/particles/acid/toxic): particles (a client visual) are not drawn yet
    }

    // tg: code/game/objects/effects/decals/cleanable/toxic.dm:67
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(active))
            icon_state = "waste_spill_active";
        else
            icon_state = "waste_spill";
        return dot;
    }
}
