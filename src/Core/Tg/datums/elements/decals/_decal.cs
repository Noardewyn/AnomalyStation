// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/decals/_decal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 P4: the whole file. The turf decals of the map put their appearance into their turf's overlays through this element
// (D115: decals show as floor overlays; scope 2e). The var block is generated.

// tg: code/datums/elements/decals/_decal.dm:18
public partial class datum_controller_subsystem_processing_dcs : datum_controller_subsystem_processing
{
    /// Remove old decals and apply new decals after rotation as necessary
    // tg: code/datums/elements/decals/_decal.dm:18
    [SignalHandler]
    public virtual void rotate_decals(datum source = null, object old_dir = null, object new_dir = null)
    {
        if (Dm.eq(old_dir, new_dir))
            return;

        DmList old_decals = Dm.list(); //instances
        if (Dm.truthy(source._listen_lookup?[COMSIG_ATOM_DECALS_ROTATING]))
            source._SendSignal(COMSIG_ATOM_DECALS_ROTATING, Dm.list(source, old_decals));

        if (!Dm.truthy(length(old_decals)))
        {
            UnregisterSignal(source, COMSIG_ATOM_DIR_CHANGE);
            return;
        }

        DmList resulting_decals_params = Dm.list(); // param lists
        foreach (datum_element_decal rotating in Dm.each_cast<datum_element_decal>(old_decals))
            Dm.append(ref resulting_decals_params, Dm.list(rotating.get_rotated_parameters(old_dir, new_dir)));

        //Instead we could generate ids and only remove duplicates to save on churn on four-corners symmetry ?
        foreach (datum_element_decal decal in Dm.each<datum_element_decal>(old_decals))
            decal.Detach(source);

        foreach (object result in Dm.each(resulting_decals_params))
        {
            DmList r = (DmList)result;
            Waiting.NoSleep(source.AddElement(typeof(datum_element_decal), r["icon"], r["icon_state"], r["dir"], PLANE_TO_TRUE(Dm.num(r["plane"])), r["layer"], r["alpha"], r["color"], r["smoothing"], r["cleanable"], r["desc"]));
        }
    }
}

// tg: code/datums/elements/decals/_decal.dm:1
public partial class datum_element_decal : datum_element
{
    // tg: code/datums/elements/decals/_decal.dm:43
    public virtual object get_rotated_parameters(object old_dir = null, object new_dir = null)
    {
        float rotation = 0;
        if (Dm.truthy(directional)) //Even when the dirs are the same rotation is coming out as not 0 for some reason
        {
            rotation = SIMPLIFY_DEGREES(Dm.num(Dm.sub(dir2angle(new_dir), dir2angle(old_dir))));
            new_dir = turn(pic.dir, -rotation);
        }

        object pic_color = pic.color;
        if (islist(pic_color))
            pic_color = string_list(pic_color as DmList);

        return Dm.list(
            Dm.kv("icon", pic.icon),
            Dm.kv("icon_state", base_icon_state),
            Dm.kv("dir", new_dir),
            Dm.kv("plane", pic.plane),
            Dm.kv("layer", pic.layer),
            Dm.kv("alpha", pic.alpha),
            Dm.kv("color", pic_color),
            Dm.kv("smoothing", smoothing),
            Dm.kv("cleanable", cleanable),
            Dm.kv("desc", description));
    }

    // tg: code/datums/elements/decals/_decal.dm:72
    [DmParams("target", "_icon", "_icon_state", "_dir", "_plane", "_layer", "_alpha", "_color", "_smoothing", "_cleanable", "_description", "_pic")]
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
        object _cleanable = Dm.arg(__extra, 8);
        object _description = Dm.arg(__extra, 9);
        mutable_appearance _pic = Dm.arg<mutable_appearance>(__extra, 10);
        _plane ??= FLOAT_PLANE;
        _layer ??= FLOAT_LAYER;
        _alpha ??= 255;
        _cleanable ??= FALSE;
        object dot = null;
        dot = await base.Attach(target, Dm.forward(__extra, _icon, _icon_state, _dir, _plane, _layer, _alpha, _color, _smoothing, _cleanable, _description, _pic));
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;
        atom target_atom = target as atom;
        // Color matrixes should be stringlisted as to avoid dupes
        if (islist(_color))
            _color = string_list(_color as DmList);
        if (Dm.truthy(_pic))
            pic = _pic;
        else if (!Dm.truthy(generate_appearance(_icon, _icon_state, _dir, _plane, _layer, _color, _alpha, _smoothing, target)))
            return ELEMENT_INCOMPATIBLE;
        description = _description;
        cleanable = _cleanable;
        directional = _dir;
        base_icon_state = _icon_state;
        smoothing = _smoothing;

        RegisterSignal(target, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(apply_overlay), TRUE);
        if (Dm.truthy(Dm.bitand(target_atom.flags_1, INITIALIZED_1)))
            target_atom.update_appearance(UPDATE_OVERLAYS); //could use some queuing here now maybe.
        else
            RegisterSignal(target, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZE, nameof(late_update_icon), TRUE);
        if (isitem(target))
            Waiting.DetachSync(() => ((obj_item)target).update_slot_icon()); // tg passes TRUE, which update_slot_icon does not declare (rules 7.3: dropped)
        if (Dm.truthy(_dir))
        {
            RegisterSignal(target, COMSIG_ATOM_DECALS_ROTATING, nameof(shuttle_rotate), TRUE);
            SSdcs.RegisterSignal(target, COMSIG_ATOM_DIR_CHANGE, nameof(datum_controller_subsystem_processing_dcs.rotate_decals), @override: TRUE);
        }
        if (_smoothing != null)
            RegisterSignal(target, COMSIG_ATOM_SMOOTHED_ICON, nameof(smooth_react), TRUE);
        if (Dm.truthy(_cleanable))
            RegisterSignal(target, COMSIG_COMPONENT_CLEAN_ACT, nameof(clean_react), TRUE);
        if (Dm.truthy(_description))
            RegisterSignal(target, COMSIG_ATOM_EXAMINE, nameof(examine), TRUE);

        RegisterSignal(target, COMSIG_TURF_ON_SHUTTLE_MOVE, nameof(shuttle_move_react), TRUE);
        return dot;
    }

    /**
     * ## generate_appearance
     *
     * If the decal was not given an appearance, it will generate one based on the other given arguments.
     * element won't be compatible if it cannot do either
     * all args are fed into creating an image, they are byond vars for images you'll recognize in the byond docs
     * (except source, source is the object whose appearance we're copying.)
     */
    // tg: code/datums/elements/decals/_decal.dm:112
    public virtual object generate_appearance(object _icon = null, object _icon_state = null, object _dir = null, object _plane = null, object _layer = null, object _color = null, object _alpha = null, object _smoothing = null, object source = null)
    {
        if (!Dm.truthy(_icon) || !Dm.truthy(_icon_state))
            return FALSE;

        if (Dm.eq(_plane, EMISSIVE_PLANE))
        {
            // pic = emissive_appearance(...) (__HELPERS/lighting.dm): emissives are lighting, not ported (D24, rules 13)
            Dm.NotTranslated("code/datums/elements/decals/_decal.dm:117", "generate_appearance: emissive_appearance (lighting, D24)");
            return TRUE;
        }

        image temp_image = image(_icon, null, _smoothing == null ? _icon_state : Dm.text("[]-[]", _icon_state, _smoothing), _layer, _dir);
        pic = Dm.@new<mutable_appearance>(temp_image);
        atom atom_source = source as atom;
        SET_PLANE_EXPLICIT(pic, Dm.num(_plane), atom_source);
        pic.color = _color;
        pic.alpha = Dm.num(_alpha);
        return TRUE;
    }

    // tg: code/datums/elements/decals/_decal.dm:127
    public override object Detach(datum source = null, params object[] __extra)
    {
        atom source_atom = source as atom;
        UnregisterSignal(source, Dm.list(COMSIG_ATOM_DIR_CHANGE, COMSIG_COMPONENT_CLEAN_ACT, COMSIG_ATOM_EXAMINE, COMSIG_ATOM_UPDATE_OVERLAYS, COMSIG_TURF_ON_SHUTTLE_MOVE, COMSIG_ATOM_SMOOTHED_ICON, COMSIG_ATOM_DECALS_ROTATING));
        SSdcs.UnregisterSignal(source, COMSIG_ATOM_DIR_CHANGE);
        source_atom.update_appearance(UPDATE_OVERLAYS);
        if (isitem(source))
            Waiting.DetachSync(() => ((obj_item)source).update_slot_icon());
        if (Dm.truthy(source._listen_lookup?[COMSIG_TURF_DECAL_DETACHED]))
            source._SendSignal(COMSIG_TURF_DECAL_DETACHED, Dm.list(source, description, cleanable, directional, pic));
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/decals/_decal.dm:137
    [SignalHandler]
    public virtual void late_update_icon(atom source = null)
    {
        if (source is atom && !Dm.truthy(Dm.bitand(source.flags_1, DECAL_INIT_UPDATE_EXPERIENCED_1)))
        {
            Dm.combine(ref source.flags_1, DECAL_INIT_UPDATE_EXPERIENCED_1); // I am so sorry, but it saves like 80ms I gotta
            source.update_appearance(UPDATE_OVERLAYS);
            UnregisterSignal(source, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZE);
        }
    }

    // tg: code/datums/elements/decals/_decal.dm:145
    [SignalHandler]
    public virtual void apply_overlay(atom source = null, DmList overlay_list = null)
    {
        Dm.append(ref overlay_list, pic);
    }

    // tg: code/datums/elements/decals/_decal.dm:150
    [SignalHandler]
    public virtual object clean_react(datum source = null, object clean_types = null)
    {
        if (Dm.truthy(Dm.bitand(clean_types, cleanable)))
        {
            Detach(source);
            return Dm.bitor(COMPONENT_CLEANED, COMPONENT_CLEANED_GAIN_XP);
        }
        return NONE;
    }

    // tg: code/datums/elements/decals/_decal.dm:158
    [SignalHandler]
    public virtual void examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(ref examine_list, description);
    }

    // tg: code/datums/elements/decals/_decal.dm:163
    [SignalHandler]
    public virtual void shuttle_move_react(datum source = null, turf new_turf = null)
    {
        if (new_turf == source)
            return;
        Detach(source);
        object pic_color = pic.color;
        if (islist(pic_color))
            pic_color = string_list(pic_color as DmList);
        Waiting.NoSleep(new_turf.AddElement(type, pic.icon, base_icon_state, directional, pic.plane, pic.layer, pic.alpha, pic_color, smoothing, cleanable, description));
    }

    // tg: code/datums/elements/decals/_decal.dm:174
    [SignalHandler]
    public virtual void shuttle_rotate(datum source = null, DmList rotating = null)
    {
        Dm.append(ref rotating, this);
    }

    /**
     * Reacts to the source atom smoothing.
     *
     * Arguments:
     * - [source][/atom]: The source of the signal and recently smoothed atom.
     */
    // tg: code/datums/elements/decals/_decal.dm:184
    [SignalHandler]
    public virtual object smooth_react(atom source = null)
    {
        object smoothing_junction = source.smoothing_junction;
        if (Dm.eq(smoothing_junction, smoothing))
            return NONE;

        Detach(source);
        object pic_color = pic.color;
        if (islist(pic_color))
            pic_color = string_list(pic_color as DmList);
        Waiting.NoSleep(source.AddElement(type, pic.icon, base_icon_state, directional, PLANE_TO_TRUE(pic.plane), pic.layer, pic.alpha, pic_color, smoothing_junction, cleanable, description));
        return NONE;
    }
}
