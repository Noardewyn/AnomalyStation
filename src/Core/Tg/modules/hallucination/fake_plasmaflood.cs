// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/fake_plasmaflood.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. The var blocks are generated (D111).

// tg: code/modules/hallucination/fake_plasmaflood.dm:1  #define FAKE_FLOOD_EXPAND_TIME 20 (file-local)
// tg: code/modules/hallucination/fake_plasmaflood.dm:2  #define FAKE_FLOOD_MAX_RADIUS 10 (file-local)

/// Plasma starts flooding from the nearby vent
// tg: code/modules/hallucination/fake_plasmaflood.dm:5
public partial class datum_hallucination_fake_flood : datum_hallucination
{
    private const float FAKE_FLOOD_EXPAND_TIME = 20;
    private const float FAKE_FLOOD_MAX_RADIUS = 10;

    // tg: code/modules/hallucination/fake_plasmaflood.dm:17
    public override async DmTask<object> start()
    {
        // This hallucination is purely visual, so we don't need to bother for clientless mobs
        if (hallucinator.client == null)
            return FALSE;

        turf center = null;

        foreach (obj_machinery_atmospherics_components_unary_vent_pump nearby_pump in Dm.each<obj_machinery_atmospherics_components_unary_vent_pump>(orange(7, hallucinator)))
        {
            if (Dm.truthy(nearby_pump.welded))
                continue;

            center = get_turf(nearby_pump);
            break;
        }

        if (center == null)
            return FALSE;

        feedback_details += Dm.text("Vent Coords: ([], [], [])", center.x, center.y, center.z);
        create_new_plasma_image(center);
        Dm.combine(hallucinator.client.images, flood_images);

        next_expand = world.time + FAKE_FLOOD_EXPAND_TIME;
        START_PROCESSING(SSobj, this);
        return TRUE;
    }

    // tg: code/modules/hallucination/fake_plasmaflood.dm:39
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (next_expand > world.time)
            return null;

        radius++;
        if (radius > FAKE_FLOOD_MAX_RADIUS)
        {
            qdel(this);
            return null;
        }

        expand_flood();

        if (Dm.@in(get_turf(hallucinator), flood_turfs))
        {
            mob_living_carbon carbon_hallucinator = hallucinator as mob_living_carbon;
            if (carbon_hallucinator != null && !Dm.truthy(carbon_hallucinator.@internal))
                Waiting.NoSleep(hallucinator.cause_hallucination(typeof(datum_hallucination_fake_alert_bad_plasma), "fake plasmaflood hallucination"));
        }

        next_expand = world.time + FAKE_FLOOD_EXPAND_TIME;
        return null;
    }

    // tg: code/modules/hallucination/fake_plasmaflood.dm:60
    public virtual void expand_flood()
    {
        foreach (image flood_image in Dm.each<image>(flood_images))
            flood_image.alpha = min(flood_image.alpha + 50, 255);

        foreach (turf flooded_turf in Dm.each<turf>(flood_turfs.Copy()))
        {
            foreach (object dir in Dm.each(GLOB.cardinals))
            {
                turf nearby_turf = get_step(flooded_turf, Dm.num(dir));
                if (Dm.@in(nearby_turf, flood_turfs) || !TURFS_CAN_SHARE(nearby_turf, flooded_turf) || isspaceturf(nearby_turf))
                    continue;
                create_new_plasma_image(nearby_turf);
            }
        }

        Dm.combine(hallucinator.client.images, flood_images);
    }

    // tg: code/modules/hallucination/fake_plasmaflood.dm:74
    public virtual void create_new_plasma_image(turf to_flood = null)
    {
        Dm.append(flood_turfs, to_flood);

        obj_effect_plasma_image_holder image_holder = Dm.@new<obj_effect_plasma_image_holder>(to_flood);
        Dm.append(flood_image_holders, image_holder);

        image plasma_image = image(image_icon, image_holder, image_state, FLY_LAYER);
        plasma_image.alpha = 50;
        SET_PLANE_EXPLICIT(plasma_image, ABOVE_GAME_PLANE, to_flood);
        Dm.append(flood_images, plasma_image);
    }

    // tg: code/modules/hallucination/fake_plasmaflood.dm:85
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);

        if (hallucinator.client != null)
            Dm.remove(hallucinator.client.images, flood_images);

        flood_turfs.Cut(); // We don't own these
        flood_images.Cut(); // We also don't own these, kinda
        QDEL_LIST(flood_image_holders); // But we DO own these

        return base.Destroy(force);
    }
}
