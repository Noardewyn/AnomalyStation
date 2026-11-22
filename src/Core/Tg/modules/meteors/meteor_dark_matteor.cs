// Translated from tgstation (snapshot 1d99bd7f): code/modules/meteors/meteor_dark_matteor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2b (D327): the whole file: the dark matt-eor (its event is admin only), which raises the alert to red while it flies, warps
// the space around it, sparks and drops a dark matter singularity where it hits; a meteor shield satellite's beam is reflected and
// the satellite blown up. The var block is generated (D111).

/// The dark matt-eor. Only attracted by emagging 10 satellites and maximizing meteor chances, and it drops a singulo. Otherwise, it's not that bad.
// tg: code/modules/meteors/meteor_dark_matteor.dm:2
public partial class obj_effect_meteor_dark_matteor : obj_effect_meteor
{
    // tg: code/modules/meteors/meteor_dark_matteor.dm:21
    [DmParams("mapload", "target")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        float current_sec_level = Dm.num(SSsecurity_level.get_current_level_as_number());
        if (current_sec_level < SEC_LEVEL_RED)
        {
            previous_security_level = current_sec_level;
            SSsecurity_level.set_level(SEC_LEVEL_RED);
        }
        warp = Dm.@new<atom_movable_warp_effect>(this);
        Dm.append(vis_contents, warp);
        spark_system = Dm.@new<datum_effect_system_basic_spark_spread_quantum>(this, 4, TRUE);
        spark_system.attach(this);
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/modules/meteors/meteor_dark_matteor.dm:33
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        //meteor's warp quickly contracts then slowly expands its ring
        float spt = Dm.num(seconds_per_tick);
        Dm.animate(warp, Dm.named("time", spt * 3), Dm.named("transform", Dm.@new<matrix>().Scale(0.5f, 0.5f)));
        Dm.animate(Dm.named("time", spt * 7), Dm.named("transform", Dm.@new<matrix>()));
        return null;
    }

    // tg: code/modules/meteors/meteor_dark_matteor.dm:38
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
        if (Dm.truthy(same_z_layer))
            return;
        if (warp != null)
            SET_PLANE(warp, Dm.num(PLANE_TO_TRUE(warp.plane)), new_turf);
    }

    // tg: code/modules/meteors/meteor_dark_matteor.dm:45
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref spark_system);
        Dm.remove(vis_contents, warp);
        QDEL_NULL(ref warp);
        return base.Destroy(force);
    }

    // tg: code/modules/meteors/meteor_dark_matteor.dm:51
    public override async DmTask<object> Move(atom newloc = null, object direction = null, float? glide_size_override = null, float? update_dir = null)
    {
        object dot = await base.Move(newloc, direction, glide_size_override, update_dir);
        if (Dm.truthy(dot))
            await spark_system.start();
        return dot;
    }

    // tg: code/modules/meteors/meteor_dark_matteor.dm:56
    public override object shield_defense(atom defender = null)
    {
        defender.visible_message(span_danger(I18n.T("[]'s beam is reflected by []!", defender, this)));
        Dm.@new<obj_effect_temp_visual_explosion_fast>(get_turf(defender));
        qdel(defender);
        return FALSE;
    }

    // tg: code/modules/meteors/meteor_dark_matteor.dm:62
    public override void moved_off_z()
    {
        base.moved_off_z();
        // tg tests `if(previous_security_level && ...)`, which a green alert (0) fails, so a missed dark matt-eor left the station on red
        // when it had been green: the port tests that a level was kept (a tg slip, not-translated.md)
        if (previous_security_level != null && !Dm.eq(SSsecurity_level.get_current_level_as_number(), SEC_LEVEL_DELTA))
            SSsecurity_level.set_level(previous_security_level);
        priority_announce("Wow. The Dark Matt-eor actually missed your station. Don't forget to thank your Chaplain for his apparent divine intervention.", "Meteor Update");
    }
}
