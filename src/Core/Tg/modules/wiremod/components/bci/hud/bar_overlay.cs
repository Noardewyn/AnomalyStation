// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/bci/hud/bar_overlay.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

/**
 * # Bar Overlay Component
 *
 * Basically an advanced verion of object overlay component that shows a horizontal/vertical bar.
 * Requires a BCI shell.
 */
// tg: code/modules/wiremod/components/bci/hud/bar_overlay.dm:11
public partial class obj_item_circuit_component_object_overlay_bar : obj_item_circuit_component_object_overlay
{
    // tg: code/modules/wiremod/components/bci/hud/bar_overlay.dm:1
    private const string COMP_BAR_OVERLAY_VERTICAL = "Vertical";
    // tg: code/modules/wiremod/components/bci/hud/bar_overlay.dm:2
    private const string COMP_BAR_OVERLAY_HORIZONTAL = "Horizontal";

    // tg: code/modules/wiremod/components/bci/hud/bar_overlay.dm:21
    public override async DmTask populate_ports()
    {
        await base.populate_ports();
        bar_number = add_input_port("Number", PORT_TYPE_NUMBER) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/bci/hud/bar_overlay.dm:26  var/static/component_options_bar
    private static ref DmList populate_options__component_options_bar => ref TgState.Current.obj_item_circuit_component_object_overlay_bar__populate_options__component_options_bar;

    // tg: code/modules/wiremod/components/bci/hud/bar_overlay.dm:25
    public override void populate_options()
    {
        bar_overlay_options = add_option_port("Bar Overlay Options", populate_options__component_options_bar) as datum_port_input_option;
        options_map = populate_options__component_options_bar;
    }

    // tg: code/modules/wiremod/components/bci/hud/bar_overlay.dm:33
    public override void show_to_owner(atom target_atom = null, mob_living owner = null)
    {
        if (LAZYLEN(active_overlays) >= overlay_limit)
            return;

        object current_option = bar_overlay_options.value;

        // tg keys the bar's overlays by the atom and stores weak references, where the object overlay keys them by the atom's
        // REF and stores the alternate appearance: the parent's Remove Overlay and organ removal never find or delete a bar
        // (they delete the weak reference); kept as tg has it
        if (Dm.truthy(active_overlays[target_atom]))
        {
            qdel(active_overlays[target_atom]);
            active_overlays[target_atom] = null;
        }

        object number_clear = clamp(bar_number.value, 0, 100);
        if (Dm.eq(current_option, COMP_BAR_OVERLAY_HORIZONTAL))
            number_clear = round(Dm.num(number_clear) / 6.25f) * 6.25f;
        else if (Dm.eq(current_option, COMP_BAR_OVERLAY_VERTICAL))
            number_clear = round(Dm.num(number_clear) / 10) * 10;

        image cool_overlay = image(icon: Dm.resource("icons/hud/screen_bci.dmi"), loc: target_atom, icon_state: Dm.text("[][]", options_map[current_option], number_clear), layer: RIPPLE_LAYER);
        SET_PLANE_EXPLICIT(cool_overlay, ABOVE_LIGHTING_PLANE, target_atom);

        if (image_pixel_x.value != null)
            cool_overlay.pixel_w = Dm.num(image_pixel_x.value);

        if (image_pixel_y.value != null)
            cool_overlay.pixel_z = Dm.num(image_pixel_y.value);

        datum_atom_hud_alternate_appearance_basic_one_person alt_appearance = target_atom.add_alt_appearance(
            typeof(datum_atom_hud_alternate_appearance_basic_one_person),
            Dm.text("bar_overlay_[]", REF(this)),
            cool_overlay,
            null,
            owner
        ) as datum_atom_hud_alternate_appearance_basic_one_person;
        alt_appearance.show_to(owner);

        active_overlays[target_atom] = WEAKREF(alt_appearance);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/components/bci/hud/bar_overlay.dm:26  (var/static/component_options_bar in /obj/item/circuit_component/object_overlay/bar/populate_options)
    internal DmList obj_item_circuit_component_object_overlay_bar__populate_options__component_options_bar = Dm.list(
        Dm.kv("Vertical", "barvert"),
        Dm.kv("Horizontal", "barhoriz"));
}
