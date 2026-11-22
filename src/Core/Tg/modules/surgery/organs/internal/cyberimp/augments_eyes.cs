// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/cyberimp/augments_eyes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// Gaps package P1 (D298): the HUD eye implants (the CMO's autosurgeon's medical HUD, the research ones); the whole file.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// HUD implants
// tg: code/modules/surgery/organs/internal/cyberimp/augments_eyes.dm:10
public partial class obj_item_organ_cyberimp_eyes_hud : obj_item_organ_cyberimp_eyes
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_eyes.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(toggled_on))
        {
            foreach (object hud_trait in Dm.each(HUD_traits))
                add_organ_trait(hud_trait);
        }
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_eyes.dm:27
    public virtual async DmTask toggle_hud(mob_living_carbon_human eye_owner = null)
    {
        if (Dm.truthy(toggled_on))
        {
            toggled_on = FALSE;
            foreach (object hud_trait in Dm.each(HUD_traits))
                remove_organ_trait(hud_trait);
            balloon_alert(eye_owner, I18n.T("hud disabled"));
            if (Dm.truthy(hud_color))
                await eye_owner.remove_eye_color(EYE_COLOR_HUD_PRIORITY);
            return;
        }
        toggled_on = TRUE;
        foreach (object hud_trait in Dm.each(HUD_traits))
            add_organ_trait(hud_trait);
        balloon_alert(eye_owner, I18n.T("hud enabled"));
        if (Dm.truthy(hud_color))
            await eye_owner.add_eye_color_right(hud_color, EYE_COLOR_HUD_PRIORITY);
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_eyes.dm:43
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        mob_living_carbon_human eye_owner = organ_owner as mob_living_carbon_human; // tg types it human: a non-human carbon owner is a runtime in tg (guarded)
        if (Dm.truthy(toggled_on) && Dm.truthy(hud_color) && eye_owner != null)
            await eye_owner.add_eye_color_right(hud_color, EYE_COLOR_HUD_PRIORITY, Dm.truthy(special) ? FALSE : TRUE);
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_eyes.dm:48
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        mob_living_carbon_human eye_owner = organ_owner as mob_living_carbon_human;
        if (Dm.truthy(toggled_on) && Dm.truthy(hud_color) && eye_owner != null)
            await eye_owner.remove_eye_color(EYE_COLOR_HUD_PRIORITY, Dm.truthy(special) ? FALSE : TRUE);
    }
}
