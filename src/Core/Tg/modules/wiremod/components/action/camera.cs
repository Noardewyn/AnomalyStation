// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/action/camera.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only /obj/item/circuit_component/camera translated; /obj/item/circuit_component/mod_program/camera, a modular
// computer program's component, comes with the programs' circuit components (not-translated.md section 232). The var
// blocks are generated.

// tg: code/modules/wiremod/components/action/camera.dm:1
public partial class obj_item_circuit_component_camera : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/action/camera.dm:23
    public override async DmTask populate_ports()
    {
        picture_taken = add_output_port("Picture Taken", PORT_TYPE_SIGNAL) as datum_port_output;
        photographed_atom = add_output_port("Photographed Entity", PORT_TYPE_ATOM) as datum_port_output;

        picture_target = add_input_port("Picture Target", PORT_TYPE_ATOM) as datum_port_input;
        picture_coord_x = add_input_port("Picture Coordinate X", PORT_TYPE_NUMBER) as datum_port_input;
        picture_coord_y = add_input_port("Picture Coordinate Y", PORT_TYPE_NUMBER) as datum_port_input;
        adjust_size_x = add_input_port("Picture Size X", PORT_TYPE_NUMBER, trigger: nameof(sanitize_picture_size)) as datum_port_input;
        adjust_size_y = add_input_port("Picture Size Y", PORT_TYPE_NUMBER, trigger: nameof(sanitize_picture_size)) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/action/camera.dm:33
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        camera = shell as obj_item_camera;
        RegisterSignal(shell, COMSIG_CAMERA_IMAGE_CAPTURED, nameof(on_image_captured));
    }

    // tg: code/modules/wiremod/components/action/camera.dm:38
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        UnregisterSignal(shell, COMSIG_CAMERA_IMAGE_CAPTURED);
        camera = null;
        await base.unregister_shell(shell);
    }

    ///Adjuts the zoom of the camera
    // tg: code/modules/wiremod/components/action/camera.dm:44
    public virtual void sanitize_picture_size()
    {
        Waiting.NoSleep(camera.adjust_zoom(adjust_size_x.value, adjust_size_y.value));
    }

    // tg: code/modules/wiremod/components/action/camera.dm:47
    [SignalHandler]
    public virtual void on_image_captured(obj_item_camera source = null, atom target = null, mob user = null)
    {
        photographed_atom.set_output(target);
        picture_taken.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/wiremod/components/action/camera.dm:52
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        atom target = picture_target.value as atom;
        if (target == null)
        {
            turf our_turf = get_location() as turf;
            target = locate(our_turf.x + Dm.num(picture_coord_x.value), our_turf.y + Dm.num(picture_coord_y.value), our_turf.z);
            if (target == null)
                return null;
        }
        camera.attempt_picture(target);
        return null;
    }
}
