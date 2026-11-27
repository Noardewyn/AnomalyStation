// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fish/types/holographic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the holodeck's fish, which derez outside the holodeck and fade when they die. The var blocks are generated
// (D111).

// tg: code/modules/fishing/fish/types/holographic.dm:2
public partial class obj_item_fish_holo : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/holographic.dm:22
    [DmParams("mapload", "apply_qualities")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        area_station_holodeck holo_area = get_area(this) as area_station_holodeck;
        if (holo_area == null)
        {
            _addtimer(Dm.@new<datum_callback>(this, nameof(set_status), FISH_DEAD), 1 * MINUTES, file: "code/modules/fishing/fish/types/holographic.dm", line: 26);
            return dot;
        }
        // holo_area.linked.add_to_spawned(src): the holodeck computer's spawned list, which derezzes the fish with the program,
        // comes with the holodeck
        Dm.Deferred("code/modules/fishing/fish/types/holographic.dm:28", "Initialize: the holodeck computer's add_to_spawned (the holodeck)");
        return dot;
    }

    // tg: code/modules/fishing/fish/types/holographic.dm:30
    [DmParams("weight_val")]
    public override async DmTask make_edible(params object[] __extra)
    {
        await DmTask.CompletedTask;
    }

    // tg: code/modules/fishing/fish/types/holographic.dm:33
    public override async DmTask set_status(object new_status = null, float? silent = null)
    {
        await base.set_status(new_status, silent);
        if (status == FISH_DEAD)
        {
            animate(this, Dm.named("alpha", 0), 3 * SECONDS, Dm.named("easing", SINE_EASING));
            // QDEL_IN(src, 3 SECONDS), inline (rules 11.1 (f))
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), this), 3 * SECONDS, file: "code/modules/fishing/fish/types/holographic.dm", line: 37);
        }
    }

    // tg: code/modules/fishing/fish/types/holographic.dm:39
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        visible_message(span_suicide(I18n.T("[] swallows [] whole! It looks like [] trying to derez []!", user, this, user.p_theyre(), user.p_themselves())));
        area_station_holodeck holo_area = get_area(this) as area_station_holodeck;
        if (holo_area == null)
        {
            await user.dust(just_ash: TRUE, drop_items: TRUE);
            return MANUAL_SUICIDE;
        }
        // holo_area.linked.add_to_spawned(user) // oh no: the holodeck computer's spawned list comes with the holodeck
        Dm.Deferred("code/modules/fishing/fish/types/holographic.dm:45", "suicide_act: the holodeck computer's add_to_spawned of the user (the holodeck)");
        return MANUAL_SUICIDE_NONLETHAL;
    }
}

// tg: code/modules/fishing/fish/types/holographic.dm:101
public partial class obj_item_fish_holo_checkered : obj_item_fish_holo
{
    // tg: code/modules/fishing/fish/types/holographic.dm:114
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (!iscarbon(user))
            return await base.suicide_act(user);

        visible_message(span_suicide(I18n.T("[] slaps the [] to their skin, causing it to slide under and meld in! It looks like [] trying to 0xF800F8 0x000000 []!", user, name, user.p_theyre(), user.p_themselves())));
        foreach (obj_item_bodypart limb in Dm.each<obj_item_bodypart>(user.get_bodyparts()))
            await limb.add_bodypart_texture(typeof(datum_bodypart_texture_checkered), update: FALSE);

        obj_item_bodypart_head head = user.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        if (head != null)
            Dm.mask(ref head.head_flags, Dm.bitnot(HEAD_EYESPRITES));
        await user.update_body();
        qdel(this);
        return TOXLOSS;
    }
}
