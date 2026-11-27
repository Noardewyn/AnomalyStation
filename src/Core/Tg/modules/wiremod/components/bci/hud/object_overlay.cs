// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/bci/hud/object_overlay.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The overlay is an image only the BCI's owner sees (a one-person alternate appearance in the owner's client images),
// drawn by the client over the target. The var block is generated.

/**
 * # Object Overlay Component
 *
 * Shows an overlay ontop of an object. Toggleable.
 * Requires a BCI shell.
 */
// tg: code/modules/wiremod/components/bci/hud/object_overlay.dm:10
public partial class obj_item_circuit_component_object_overlay : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/bci/hud/object_overlay.dm:8
    private const float OBJECT_OVERLAY_LIMIT = 10;

    // tg: code/modules/wiremod/components/bci/hud/object_overlay.dm:38
    public override async DmTask populate_ports()
    {
        target = add_input_port("Target", PORT_TYPE_ATOM) as datum_port_input;

        signal_on = add_input_port("Create Overlay", PORT_TYPE_SIGNAL) as datum_port_input;
        signal_off = add_input_port("Remove Overlay", PORT_TYPE_SIGNAL) as datum_port_input;

        image_pixel_x = add_input_port("X-Axis Shift", PORT_TYPE_NUMBER) as datum_port_input;
        image_pixel_y = add_input_port("Y-Axis Shift", PORT_TYPE_NUMBER) as datum_port_input;
        image_rotation = add_input_port("Overlay Rotation", PORT_TYPE_NUMBER) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/bci/hud/object_overlay.dm:48
    public override object Destroy(float? force = null)
    {
        QDEL_LIST_ASSOC_VAL(active_overlays);
        return base.Destroy(force);
    }

    // tg: code/modules/wiremod/components/bci/hud/object_overlay.dm:53  var/static/component_options
    private static ref DmList populate_options__component_options => ref TgState.Current.obj_item_circuit_component_object_overlay__populate_options__component_options;

    // tg: code/modules/wiremod/components/bci/hud/object_overlay.dm:52
    public override void populate_options()
    {
        object_overlay_options = add_option_port("Object", populate_options__component_options) as datum_port_input_option;
        options_map = populate_options__component_options;
    }

    // tg: code/modules/wiremod/components/bci/hud/object_overlay.dm:68
    public override async DmTask register_shell(atom_movable shell = null)
    {
        if (istype(shell, typeof(obj_item_organ_cyberimp_bci)))
        {
            bci = shell as obj_item_organ_cyberimp_bci;
            RegisterSignal(shell, COMSIG_ORGAN_REMOVED, nameof(on_organ_removed));
        }
    }

    // tg: code/modules/wiremod/components/bci/hud/object_overlay.dm:73
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        bci = null;
        UnregisterSignal(shell, COMSIG_ORGAN_REMOVED);
    }

    // tg: code/modules/wiremod/components/bci/hud/object_overlay.dm:77
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (bci == null)
            return null;

        mob_living owner = bci.owner;
        atom target_atom = target.value as atom;

        if (!(owner is mob_living) || owner.client == null || target_atom == null)
            return null;

        if (COMPONENT_TRIGGERED_BY(signal_on, port))
            show_to_owner(target_atom, owner);

        datum_atom_hud existing_overlay = active_overlays[REF(target_atom)] as datum_atom_hud;
        if (COMPONENT_TRIGGERED_BY(signal_off, port) && existing_overlay != null)
        {
            qdel(existing_overlay);
            Dm.remove(active_overlays, REF(target_atom));
        }
        return null;
    }

    // tg: code/modules/wiremod/components/bci/hud/object_overlay.dm:95
    public virtual void show_to_owner(atom target_atom = null, mob_living owner = null)
    {
        if (length(active_overlays) >= OBJECT_OVERLAY_LIMIT)
            return;

        datum_atom_hud existing_overlay = active_overlays[REF(target_atom)] as datum_atom_hud;
        if (existing_overlay != null)
            qdel(existing_overlay);

        image cool_overlay = image(icon: Dm.resource("icons/hud/screen_bci.dmi"), loc: target_atom, icon_state: options_map[object_overlay_options.value], layer: RIPPLE_LAYER);
        SET_PLANE_EXPLICIT(cool_overlay, ABOVE_LIGHTING_PLANE, target_atom);

        if (image_pixel_x.value != null)
            cool_overlay.pixel_w = Dm.num(image_pixel_x.value);

        if (image_pixel_y.value != null)
            cool_overlay.pixel_z = Dm.num(image_pixel_y.value);

        if (image_rotation.value != null)
        {
            matrix turn_matrix = cool_overlay.transform as matrix;
            turn_matrix.Turn(Dm.num(image_rotation.value));
            cool_overlay.transform = turn_matrix;
        }

        datum_atom_hud_alternate_appearance_basic_one_person alt_appearance = target_atom.add_alt_appearance(
            typeof(datum_atom_hud_alternate_appearance_basic_one_person),
            Dm.text("object_overlay_[]", REF(this)),
            cool_overlay,
            null,
            owner
        ) as datum_atom_hud_alternate_appearance_basic_one_person;
        alt_appearance.show_to(owner);

        active_overlays[REF(target_atom)] = alt_appearance;
    }

    // tg: code/modules/wiremod/components/bci/hud/object_overlay.dm:128
    [SignalHandler]
    public virtual void on_organ_removed(datum source = null, mob_living_carbon owner = null)
    {
        QDEL_LIST_ASSOC_VAL(active_overlays);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/components/bci/hud/object_overlay.dm:53  (var/static/component_options in /obj/item/circuit_component/object_overlay/populate_options)
    internal DmList obj_item_circuit_component_object_overlay__populate_options__component_options = Dm.list(
        Dm.kv("Corners (Blue)", "hud_corners"),
        Dm.kv("Corners (Red)", "hud_corners_red"),
        Dm.kv("Circle (Blue)", "hud_circle"),
        Dm.kv("Circle (Red)", "hud_circle_red"),
        Dm.kv("Small Corners (Blue)", "hud_corners_small"),
        Dm.kv("Small Corners (Red)", "hud_corners_small_red"),
        Dm.kv("Triangle (Blue)", "hud_triangle"),
        Dm.kv("Triangle (Red)", "hud_triangle_red"),
        Dm.kv("HUD mark (Blue)", "hud_mark"),
        Dm.kv("HUD mark (Red)", "hud_mark_red"));
}
