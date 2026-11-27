// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/bci/hud/counter_overlay.dm
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
 * # Counter Overlay Component
 *
 * Shows an counter overlay.
 * Requires a BCI shell.
 */
// tg: code/modules/wiremod/components/bci/hud/counter_overlay.dm:8
public partial class obj_item_circuit_component_counter_overlay : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/bci/hud/counter_overlay.dm:26
    public override async DmTask populate_ports()
    {
        counter_number = add_input_port("Displayed Number", PORT_TYPE_NUMBER) as datum_port_input;

        signal_update = add_input_port("Update Overlay", PORT_TYPE_SIGNAL) as datum_port_input;

        image_pixel_x = add_input_port("X-Axis Shift", PORT_TYPE_NUMBER) as datum_port_input;
        image_pixel_y = add_input_port("Y-Axis Shift", PORT_TYPE_NUMBER) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/bci/hud/counter_overlay.dm:34
    public override async DmTask register_shell(atom_movable shell = null)
    {
        if (istype(shell, typeof(obj_item_organ_cyberimp_bci)))
        {
            bci = shell as obj_item_organ_cyberimp_bci;
            RegisterSignal(shell, COMSIG_ORGAN_REMOVED, nameof(on_organ_removed));
        }
    }

    // tg: code/modules/wiremod/components/bci/hud/counter_overlay.dm:39
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        bci = null;
        foreach (datum_weakref number in Dm.each_cast<datum_weakref>(numbers))
        {
            datum_atom_hud number_overlay = number?.resolve() as datum_atom_hud;
            QDEL_NULL(ref number_overlay);
        }
        numbers = Dm.list();

        datum_atom_hud overlay = counter_appearance?.resolve() as datum_atom_hud;
        QDEL_NULL(ref overlay);
        UnregisterSignal(shell, COMSIG_ORGAN_REMOVED);
    }

    // tg: code/modules/wiremod/components/bci/hud/counter_overlay.dm:50
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (bci == null)
            return null;

        mob_living owner = bci.owner;

        if (owner == null || !(owner is mob_living) || owner.client == null)
            return null;

        foreach (datum_weakref number in Dm.each_cast<datum_weakref>(numbers))
        {
            datum_atom_hud number_overlay = number?.resolve() as datum_atom_hud;
            QDEL_NULL(ref number_overlay);
        }
        numbers = Dm.list();

        datum_atom_hud overlay = counter_appearance?.resolve() as datum_atom_hud;
        QDEL_NULL(ref overlay);

        image counter = image(icon: Dm.resource("icons/hud/screen_bci.dmi"), icon_state: "hud_numbers", loc: owner, layer: RIPPLE_LAYER);
        SET_PLANE_EXPLICIT(counter, ABOVE_LIGHTING_PLANE, owner);

        if (image_pixel_x.value != null)
            counter.pixel_w = Dm.num(image_pixel_x.value);
        if (image_pixel_y.value != null)
            counter.pixel_z = Dm.num(image_pixel_y.value);

        datum_atom_hud_alternate_appearance_basic_one_person alt_appearance = owner.add_alt_appearance(
            typeof(datum_atom_hud_alternate_appearance_basic_one_person),
            Dm.text("counter_overlay_[]", REF(this)),
            counter,
            null,
            owner
        ) as datum_atom_hud_alternate_appearance_basic_one_person;
        alt_appearance.show_to(owner);

        counter_appearance = WEAKREF(alt_appearance) as datum_weakref;

        float cleared_number = clamp(round(Dm.num(counter_number.value)), 0, 999);

        for (float i = 1; i <= 3; i++)
        {
            float cur_num = Dm.num(Dm.mod(round(cleared_number / Dm.pow(10, 3 - i)), 10));
            image number = image(icon: Dm.resource("icons/hud/screen_bci.dmi"), icon_state: Dm.text("hud_number_[]", cur_num), loc: owner, layer: RIPPLE_LAYER);
            SET_PLANE_EXPLICIT(number, ABOVE_LIGHTING_PLANE, owner);

            if (image_pixel_x.value != null)
                number.pixel_w = Dm.num(image_pixel_x.value) + (i - 1) * 9;
            else
                number.pixel_w = (i - 1) * 9;

            if (image_pixel_y.value != null)
                number.pixel_z = Dm.num(image_pixel_y.value);

            datum_atom_hud_alternate_appearance_basic_one_person number_alt_appearance = owner.add_alt_appearance(
                typeof(datum_atom_hud_alternate_appearance_basic_one_person),
                Dm.text("counter_overlay_[]_[]", REF(this), i),
                number,
                null,
                owner
            ) as datum_atom_hud_alternate_appearance_basic_one_person;
            number_alt_appearance.show_to(owner);

            Dm.append(numbers, WEAKREF(number_alt_appearance));
        }
        return null;
    }

    // tg: code/modules/wiremod/components/bci/hud/counter_overlay.dm:112
    [SignalHandler]
    public virtual void on_organ_removed(datum source = null, mob_living_carbon owner = null)
    {
        foreach (datum_weakref number in Dm.each_cast<datum_weakref>(numbers))
        {
            datum_atom_hud number_overlay = number?.resolve() as datum_atom_hud;
            QDEL_NULL(ref number_overlay);
        }
        numbers = Dm.list();

        datum_atom_hud overlay = counter_appearance?.resolve() as datum_atom_hud;
        overlay.hide_from(owner);
        QDEL_NULL(ref overlay);
    }

    // tg: code/modules/wiremod/components/bci/hud/counter_overlay.dm:123
    public override object Destroy(float? force = null)
    {
        foreach (datum_weakref number in Dm.each_cast<datum_weakref>(numbers))
        {
            datum_atom_hud number_overlay = number?.resolve() as datum_atom_hud;
            QDEL_NULL(ref number_overlay);
        }
        numbers = Dm.list();

        datum_atom_hud overlay = counter_appearance?.resolve() as datum_atom_hud;
        QDEL_NULL(ref overlay);
        return base.Destroy(force);
    }
}
