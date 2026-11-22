// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/decals/blood.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M6 slice 1 (blood in the world): the whole file, the blood stain drawn over a bloodied item (tg's "itemblood" state inset
// into the item's own pixels) and its "blood-stained" examine name. The emissive stain of glowing blood is lighting (D24):
// a deferred leaf. The var block is generated (D111).

// tg: code/datums/elements/decals/blood.dm:1
public partial class datum_element_decal_blood : datum_element_decal
{
    // tg: code/datums/elements/decals/blood.dm:7
    [DmParams("target", "_icon", "_icon_state", "_dir", "_plane", "_layer", "_alpha", "_color", "_smoothing", "_cleanable", "_description", "_pic", "_uses_filter", "_emissive")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object _icon = Dm.arg(__extra, 0);
        object _icon_state = Dm.arg(__extra, 1);
        object _dir = Dm.arg(__extra, 2);
        object _plane = Dm.arg(__extra, 3);
        object _layer = Dm.arg(__extra, 4);
        object _alpha = Dm.arg(__extra, 5);
        object _color = Dm.arg(__extra, 6);
        object _smoothing = Dm.arg(__extra, 7);
        object _cleanable = Dm.arg(__extra, 8) ?? CLEAN_TYPE_BLOOD;
        object _description = Dm.arg(__extra, 9);
        mutable_appearance _pic = Dm.arg<mutable_appearance>(__extra, 10);
        object _uses_filter = Dm.arg(__extra, 11);
        object _emissive = Dm.arg(__extra, 12);
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;

        obj_item as_item = (obj_item)target;
        if (Dm.truthy(_uses_filter) != (as_item.cached_color_filter != null))
        {
            await as_item.AddElement(type, _icon, _icon_state, _dir, _plane, _layer, _alpha, _color, _smoothing, _cleanable, _description, _pic, (as_item.cached_color_filter != null) ? TRUE : FALSE, _emissive);
            return null;
        }

        uses_filter = Dm.truthy(_uses_filter) ? TRUE : FALSE;
        emissive_alpha = _emissive;
        object dot = await base.Attach(target, _icon, _icon_state, _dir, _plane, _layer, _alpha, _color, _smoothing, _cleanable, _description, _pic);
        RegisterSignal(as_item, COMSIG_ATOM_GET_EXAMINE_NAME, nameof(get_examine_name), TRUE);
        RegisterSignal(as_item, COMSIG_ATOM_COLOR_UPDATED, nameof(on_color_update), TRUE);
        return dot;
    }

    // tg: code/datums/elements/decals/blood.dm:22
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, Dm.list(COMSIG_ATOM_GET_EXAMINE_NAME, COMSIG_ATOM_COLOR_UPDATED));
        if (isitem(source))
        {
            obj_item source_item = (obj_item)source;
            REMOVE_KEEP_TOGETHER(source_item, type);
        }
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/decals/blood.dm:29
    public override object generate_appearance(object _icon = null, object _icon_state = null, object _dir = null, object _plane = null, object _layer = null, object _color = null, object _alpha = null, object _smoothing = null, object source = null)
    {
        _color ??= BLOOD_COLOR_RED;
        obj_item as_item = source as obj_item;
        ADD_KEEP_TOGETHER(as_item, type);
        // as_item.get_cached_width() / ICON_SIZE_X (game/data_huds.dm:490: get_icon_dimensions of the item's icon, IconSizes)
        float scale_factor_x = ((as_item.icon == null) ? 0 : IconSizes.Of(as_item.icon).Width) / (float)ICON_SIZE_X;
        float scale_factor_y = ((as_item.icon == null) ? 0 : IconSizes.Of(as_item.icon).Height) / (float)ICON_SIZE_Y;
        mutable_appearance blood_splatter = (mutable_appearance)mutable_appearance(Dm.resource("icons/effects/blood.dmi"), "itemblood", appearance_flags: RESET_COLOR); //MA of the blood that we apply
        blood_splatter.transform = ((matrix)blood_splatter.transform).Scale(scale_factor_x, scale_factor_y);
        blood_splatter.blend_mode = BLEND_INSET_OVERLAY;
        blood_splatter.color = _color;
        if (Dm.truthy(emissive_alpha))
        {
            // emissive_splatter = emissive_appearance('icons/effects/blood.dmi', "itemblood", ...): glowing blood (lighting, D24)
            Dm.Deferred("code/datums/elements/decals/blood.dm:40", "generate_appearance: the emissive splatter of glowing blood (emissives, D24)");
        }
        if (Dm.truthy(uses_filter))
        {
            blood_splatter.appearance_flags = Dm.bitor(blood_splatter.appearance_flags, KEEP_APART);
            if (!Dm.truthy(as_item.render_target))
                as_item.render_target = Dm.text("blood_target_[]", REF(as_item));
            blood_splatter.add_filter("blood_cutout", -1, alpha_mask_filter(render_source: as_item.render_target) as DmList); // M6 slice 7b (D156)
            // if (emissive_splatter) emissive_splatter.add_filter("blood_cutout", ...) (:47): the emissive splatter waits for emissives (D24)
        }
        pic = blood_splatter;
        return TRUE;
    }

    // tg: code/datums/elements/decals/blood.dm:54  (SIGNAL_HANDLER is dropped, rules 7.9)
    [SignalHandler]
    public virtual void get_examine_name(atom source = null, mob user = null, DmList @override = null)
    {
        DmList blood_stains = GET_ATOM_BLOOD_DECALS(source);
        if (!Dm.truthy(Dm.length(blood_stains)))
            return;
        datum_blood_type blood_type = blood_stains[blood_stains[Dm.length(blood_stains)]] as datum_blood_type;
        string blood_descriptor = "blood";
        if (blood_type != null)
            blood_descriptor = LOWER_TEXT(blood_type.get_blood_name()) as string;

        @override[EXAMINE_POSITION_BEFORE] = Dm.text("[]-stained", blood_descriptor);
    }

    // tg: code/datums/elements/decals/blood.dm:67  (SIGNAL_HANDLER is dropped, rules 7.9)
    [SignalHandler]
    public virtual object on_color_update(obj_item source = null, object color_updated = null)
    {
        if (!Dm.truthy(color_updated) || Dm.truthy(uses_filter) == (source.cached_color_filter != null))
            return null;

        Detach(source);
        Waiting.NoSleep(source.AddElement(type, pic.icon, base_icon_state, directional, pic.plane, pic.layer, pic.alpha, pic.color, smoothing, cleanable, description, null, (source.cached_color_filter != null) ? TRUE : FALSE));
        return COMPONENT_CANCEL_COLOR_APPEARANCE_UPDATE;
    }
}
