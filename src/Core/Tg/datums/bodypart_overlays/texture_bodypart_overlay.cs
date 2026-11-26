// Translated from tgstation (snapshot 1d99bd7f): code/datums/bodypart_overlays/texture_bodypart_overlay.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps SP2c, D349): the base texture's procs (the cerulean's abyss texture derives from it); the mesh textures are not translated.
// The type itself is declared here, in the earliest .dme file that declares it (rules 2.3).

/// Bodypart overlays focused on texturing limbs
// tg: code/datums/bodypart_overlays/texture_bodypart_overlay.dm:2
[DmPath("/datum/bodypart_texture")]
public partial class datum_bodypart_texture : datum
{
    // tg: code/datums/bodypart_overlays/texture_bodypart_overlay.dm:12
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        cached_texture_icon = Dm.icon(texture_icon, Dm.str(texture_icon_state));
    }

    // tg: code/datums/bodypart_overlays/texture_bodypart_overlay.dm:16
    public virtual void modify_bodypart_appearance(image appearance = null, object overlay_flags = null)
    {
        float filter_width = ICON_SIZE_X * (Dm.truthy(Dm.bitand(overlay_flags, LIMB_OVERLAY_WIDE_ICON)) ? 3 : 1);
        float filter_height = ICON_SIZE_Y * (Dm.truthy(Dm.bitand(overlay_flags, LIMB_OVERLAY_TALL_ICON)) ? 3 : 1);

        for (float i = 0; i <= filter_width; i += ICON_SIZE_X)
        {
            for (float j = 0; j <= filter_height; j += ICON_SIZE_Y)
                appearance.add_filter(Dm.text("bodypart_texture_[]_[]/[]", texture_icon_state, i, j), 1, layering_filter(cached_texture_icon, x: i, y: j, blend_mode: BLEND_INSET_OVERLAY) as DmList);
        }
    }

    // tg: code/datums/bodypart_overlays/texture_bodypart_overlay.dm:24
    public virtual object icon_render_key()
    {
        return type;
    }

    // tg: code/datums/bodypart_overlays/texture_bodypart_overlay.dm:27
    public virtual object can_texture_bodypart(obj_item_bodypart bodypart_owner = null)
    {
        foreach (datum_bodypart_texture other_texture in Dm.each_cast<datum_bodypart_texture>(bodypart_owner.bodypart_textures))
        {
            if (other_texture.overlay_priority > overlay_priority)
                return FALSE;
        }
        return TRUE;
    }
}
