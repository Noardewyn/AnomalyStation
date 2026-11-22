// Translated from tgstation (snapshot 1d99bd7f): code/datums/bodypart_overlays/markings_bodypart_overlay.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP1 (D338): the whole file. The marking is drawn on the limb as tg draws it (a mutable appearance of the accessory's state on the
// body's adjacent layer), so the client composes it into the mob's picture with the rest of the body (D104).

/// For body markings applied on the species, which need some extra code
// tg: code/datums/bodypart_overlays/markings_bodypart_overlay.dm:2
public partial class datum_bodypart_overlay_simple_body_marking : datum_bodypart_overlay_simple
{
    /// Get the accessory list from SSaccessories. Used in species.dm to get the right sprite
    // tg: code/datums/bodypart_overlays/markings_bodypart_overlay.dm:20 (the earlier of the type's two definitions, rules 2.5)
    public object get_accessory__d0(object name = null)
    {
        throw Dm.CRASH(Dm.text("get_accessories() not overriden on [] !", type), "code/datums/bodypart_overlays/markings_bodypart_overlay.dm", 21);
    }

    // tg: code/datums/bodypart_overlays/markings_bodypart_overlay.dm:23
    [DmParams("name", "set_color")]
    public override void set_appearance(params object[] __extra)
    {
        object name = Dm.arg(__extra, 0), set_color = Dm.arg(__extra, 1);
        datum_sprite_accessory accessory = get_accessory(name) as datum_sprite_accessory;
        if (accessory == null)
            return;

        icon = accessory.icon;
        icon_state = accessory.icon_state;
        use_gender = accessory.gender_specific;
        draw_color = Dm.truthy(accessory.color_src) ? set_color : null;
    }

    // tg: code/datums/bodypart_overlays/markings_bodypart_overlay.dm:33
    public override object icon_render_key(obj_item_bodypart limb = null)
    {
        object dot = base.icon_render_key(limb);
        Dm.append(ref dot, use_gender);
        Dm.append(ref dot, draw_color);
        return dot;
    }

    // tg: code/datums/bodypart_overlays/markings_bodypart_overlay.dm:38
    public override object can_draw_on_bodypart(obj_item_bodypart bodypart_owner = null, mob_living_carbon owner = null)
    {
        return Dm.num(Dm.truthy(base.can_draw_on_bodypart(bodypart_owner, owner)) && !Dm.eq(icon_state, SPRITE_ACCESSORY_NONE));
    }

    // tg: code/datums/bodypart_overlays/markings_bodypart_overlay.dm:41
    public override object get_image(obj_item_bodypart limb = null, object layer_index = null, object layer_real = null)
    {
        // tg's precedence: (limb.gender == MALE ? MALE : (FEMALE + "_")); a limb's gender is BYOND's neuter, so a dimorphic limb draws the female marking
        object gender_string = Dm.truthy(use_gender) && Dm.truthy(limb.is_dimorphic) ? (Dm.eq(limb.gender, MALE) ? MALE : Dm.add(FEMALE, "_")) : ""; //we only got male and female sprites
        return mutable_appearance(icon, Dm.add(Dm.add(Dm.add(gender_string, icon_state), "_"), limb.body_zone) as string, layer: Dm.num(layer_real));
    }

    // tg: code/datums/bodypart_overlays/markings_bodypart_overlay.dm:45
    public virtual object get_accessory(object name = null)
    {
        return ((DmList)SSaccessories.feature_list[dna_feature_key])[name];
    }
}
