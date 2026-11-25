// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/blood_limb_overlay.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3a (D349): the whole file (the skeleton's degloved bone limbs wear it).

/// An element which adds a blood overlay to limbs its attached to
// tg: code/datums/elements/blood_limb_overlay.dm:2
public partial class datum_element_blood_limb_overlay : datum_element
{
    // tg: code/datums/elements/blood_limb_overlay.dm:4
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isbodypart(target))
            return ELEMENT_INCOMPATIBLE;
        RegisterSignal(target, COMSIG_BODYPART_GET_LIMB_ICON, nameof(on_limb_icon));
        RegisterSignal(target, COMSIG_BODYPART_GENERATE_ICON_KEY, nameof(on_icon_key));
        return dot;
    }

    // tg: code/datums/elements/blood_limb_overlay.dm:11
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, Dm.list(COMSIG_BODYPART_GET_LIMB_ICON, COMSIG_BODYPART_GENERATE_ICON_KEY));
        return dot;
    }

    // tg: code/datums/elements/blood_limb_overlay.dm:15
    [SignalHandler]
    public virtual void on_limb_icon(obj_item_bodypart source = null, DmList limb_icons = null, object dropped = null)
    {
        DmList blood_dna_info = source.blood_dna_info;
        if (!Dm.truthy(LAZYLEN(blood_dna_info)) || Dm.truthy(source.is_invisible))
            return;

        image limb = limb_icons[1] as image;
        image blood_visual = image(limb.icon, icon_state: Dm.text("[]_blood", limb.icon_state), dir: Dm.truthy(dropped) ? SOUTH : null);
        // We need to convert it to HSV and then adjust the colors to make them look brighter on the grayscale blood overlay
        DmList target_color = rgb2num(Dm.str(Waiting.NoSleep(get_color_from_blood_list(blood_dna_info))), COLORSPACE_HSV);
        blood_visual.color = rgb(Dm.num(target_color[1]), ceil(Dm.num(target_color[2]) * 0.4f), clamp(ceil(Dm.num(target_color[3]) * 1.33f), 0, 100), space: COLORSPACE_HSV);
        Dm.append(ref limb.overlays, blood_visual);
        if (!Dm.truthy(source.aux_zone))
            return;

        image aux = limb_icons[2] as image;
        image aux_blood_visual = image(aux.icon, icon_state: Dm.text("[]_blood", aux.icon_state), dir: Dm.truthy(dropped) ? SOUTH : null);
        aux_blood_visual.color = blood_visual.color;
        Dm.append(ref aux.overlays, aux_blood_visual);
    }

    // tg: code/datums/elements/blood_limb_overlay.dm:36
    [SignalHandler]
    public virtual void on_icon_key(obj_item_bodypart source = null, DmList icon_keys = null)
    {
        DmList blood_dna_info = (source.blood_dna_info ?? source.owner?.get_blood_dna_list()) as DmList;
        if (Dm.truthy(LAZYLEN(blood_dna_info)) && !Dm.truthy(source.is_invisible))
            Dm.append(ref icon_keys, Dm.text("blood:[]", Waiting.NoSleep(get_color_from_blood_list(blood_dna_info))));
    }
}
