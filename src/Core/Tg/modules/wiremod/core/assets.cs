// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/core/assets.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): a seam for tg's circuit component asset (DV44: the asset transport is the client's). tg's
// /datum/asset/json/circuit_components generate() makes every circuit component once and sends their names, descriptions,
// categories, colours and ports to the browser as circuit_components.json; the port runs that body the first time an editor
// window wants it and keeps the data for the IntegratedCircuit window's static data (DV568: the live components only). The asset
// type itself stays inert (/datum/asset is not ported). /datum/asset/simple/circuit_assets (the grid background picture) is the
// client's own drawing.

public static partial class GlobalProcs
{
    /// The circuit component asset's data (tg's circuit_components.json), made once per world.
    // tg: code/modules/wiremod/core/assets.dm:4  /datum/asset/json/circuit_components/generate()
    public static async DmTask<DmList> circuit_components_json()
    {
        if (TgState.Current.circuit_components_json != null)
            return TgState.Current.circuit_components_json;
        DmList circuit_data = Dm.list();

        foreach (Type component_type in Dm.each<Type>(subtypesof(typeof(obj_item_circuit_component))))
        {
            // DEVIATION(DV568): a component whose type is not live (CI2's and CI3's, or tg's abstract ones a subtype completes)
            // is left out: its ports are not made, and the printers do not print it (DV384).
            if (InertSeam.IsInertType(component_type))
                continue;
            obj_item_circuit_component circuit_info = await Dm.new_async(component_type) as obj_item_circuit_component;
            DmList component_data = Dm.list();

            DmList input_info = Dm.list();
            foreach (datum_port_input port in Dm.each_cast<datum_port_input>(circuit_info.input_ports))
            {
                Dm.append(input_info, Dm.list(Dm.list(
                    Dm.kv("name", port.name),
                    Dm.kv("type", port.datatype),
                    Dm.kv("color", port.color),
                    Dm.kv("datatype_data", port.datatype_ui_data()))));
            }
            DmList output_info = Dm.list();
            foreach (datum_port_output port in Dm.each_cast<datum_port_output>(circuit_info.output_ports))
            {
                Dm.append(output_info, Dm.list(Dm.list(
                    Dm.kv("name", port.name),
                    Dm.kv("type", port.datatype),
                    Dm.kv("color", port.color),
                    Dm.kv("datatype_data", port.datatype_ui_data()))));
            }

            component_data["name"] = circuit_info.display_name;
            component_data["description"] = circuit_info.desc;
            component_data["category"] = circuit_info.category;
            component_data["color"] = circuit_info.ui_color;
            component_data["type"] = component_type;

            component_data["input_ports"] = input_info;
            component_data["output_ports"] = output_info;

            Dm.append(circuit_data, Dm.list(component_data));
            qdel(circuit_info);
        }

        TgState.Current.circuit_components_json = circuit_data;
        return circuit_data;
    }
}

public sealed partial class TgState
{
    /// The circuit component asset's data (circuit_components_json); tg's asset cache keeps it once generated.
    internal DmList circuit_components_json;
}
