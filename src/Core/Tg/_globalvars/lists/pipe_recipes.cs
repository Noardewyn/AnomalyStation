// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/pipe_recipes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice D172 (the rapid pipe dispenser): the whole file. The disposal and transit tube recipes name types that are not
// translated (their construction structures, disposalconstruct and c_transit_tube): their initial values are read as tg's,
// so the RPD's window lists them; building one stops at Dm.NotTranslated (RPD.cs).

public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/lists/pipe_recipes.dm:1  GLOBAL_LIST_INIT(atmos_pipe_recipes, list(...))
    public DmList atmos_pipe_recipes;

    // tg: code/_globalvars/lists/pipe_recipes.dm:1  (GLOBAL_MANAGED, __DEFINES/__globals.dm:5)
    public virtual void InitGlobalatmos_pipe_recipes()
    {
        atmos_pipe_recipes = Dm.list(
            Dm.kv("Pipes", Dm.list(
                Dm.@new<datum_pipe_info_pipe>("Pipe", typeof(obj_machinery_atmospherics_pipe_smart), TRUE),
                Dm.@new<datum_pipe_info_pipe>("Layer Adapter", typeof(obj_machinery_atmospherics_pipe_layer_manifold), TRUE),
                Dm.@new<datum_pipe_info_pipe>("Color Adapter", typeof(obj_machinery_atmospherics_pipe_color_adapter), TRUE),
                Dm.@new<datum_pipe_info_pipe_bridge_pipe>(),
                Dm.@new<datum_pipe_info_pipe_multiz_connector>()
            )),
            Dm.kv("Binary", Dm.list(
                Dm.@new<datum_pipe_info_pipe_manual_valve>(),
                Dm.@new<datum_pipe_info_pipe_digital_valve>(),
                Dm.@new<datum_pipe_info_pipe_gas_pump>(),
                Dm.@new<datum_pipe_info_pipe_volume_pump>(),
                Dm.@new<datum_pipe_info_pipe_passive_gate>(),
                Dm.@new<datum_pipe_info_pipe_pressure_valve>(),
                Dm.@new<datum_pipe_info_pipe_temperature_gate>(),
                Dm.@new<datum_pipe_info_pipe_temperature_pump>()
            )),
            Dm.kv("Devices", Dm.list(
                Dm.@new<datum_pipe_info_pipe>("Gas Filter", typeof(obj_machinery_atmospherics_components_trinary_filter), TRUE),
                Dm.@new<datum_pipe_info_pipe>("Gas Mixer", typeof(obj_machinery_atmospherics_components_trinary_mixer), TRUE),
                Dm.@new<datum_pipe_info_pipe>("Connector", typeof(obj_machinery_atmospherics_components_unary_portables_connector), TRUE),
                Dm.@new<datum_pipe_info_pipe>("Injector", typeof(obj_machinery_atmospherics_components_unary_outlet_injector), TRUE),
                Dm.@new<datum_pipe_info_pipe>("Scrubber", typeof(obj_machinery_atmospherics_components_unary_vent_scrubber), TRUE),
                Dm.@new<datum_pipe_info_pipe_vent>(),
                Dm.@new<datum_pipe_info_pipe_passive_vent>(),
                Dm.@new<datum_pipe_info_meter>()
            )),
            Dm.kv("Heat Exchange", Dm.list(
                Dm.@new<datum_pipe_info_pipe>("Pipe", typeof(obj_machinery_atmospherics_pipe_heat_exchanging_simple), FALSE),
                Dm.@new<datum_pipe_info_pipe>("Manifold", typeof(obj_machinery_atmospherics_pipe_heat_exchanging_manifold), FALSE),
                Dm.@new<datum_pipe_info_pipe>("4-Way Manifold", typeof(obj_machinery_atmospherics_pipe_heat_exchanging_manifold4w), FALSE),
                Dm.@new<datum_pipe_info_pipe>("Junction", typeof(obj_machinery_atmospherics_pipe_heat_exchanging_junction), FALSE),
                Dm.@new<datum_pipe_info_pipe_heat_exchanger>()
            ))
        );
        Dm.append(ref gvars_datum_init_order, "atmos_pipe_recipes");
    }

    // tg: code/_globalvars/lists/pipe_recipes.dm:38  GLOBAL_LIST_INIT(disposal_pipe_recipes, list(...))
    public DmList disposal_pipe_recipes;

    // tg: code/_globalvars/lists/pipe_recipes.dm:38  (GLOBAL_MANAGED, __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldisposal_pipe_recipes()
    {
        disposal_pipe_recipes = Dm.list(
            Dm.kv("Disposal Pipes", Dm.list(
                Dm.@new<datum_pipe_info_disposal>("Pipe", typeof(obj_structure_disposalpipe_segment), PIPE_BENDABLE),
                Dm.@new<datum_pipe_info_disposal>("Junction", typeof(obj_structure_disposalpipe_junction), PIPE_TRIN_M),
                Dm.@new<datum_pipe_info_disposal>("Y-Junction", typeof(obj_structure_disposalpipe_junction_yjunction)),
                Dm.@new<datum_pipe_info_disposal>("Sort Junction", typeof(obj_structure_disposalpipe_sorting_mail), PIPE_TRIN_M),
                Dm.@new<datum_pipe_info_disposal>("Rotator", typeof(obj_structure_disposalpipe_rotator), PIPE_ONEDIR_FLIPPABLE),
                Dm.@new<datum_pipe_info_disposal>("Trunk", typeof(obj_structure_disposalpipe_trunk)),
                Dm.@new<datum_pipe_info_disposal>("Down Turn", typeof(obj_structure_disposalpipe_trunk_multiz_down)),
                Dm.@new<datum_pipe_info_disposal>("Up Turn", typeof(obj_structure_disposalpipe_trunk_multiz)),
                Dm.@new<datum_pipe_info_disposal>("Bin", typeof(obj_machinery_disposal_bin), PIPE_ONEDIR),
                Dm.@new<datum_pipe_info_disposal>("Outlet", typeof(obj_structure_disposaloutlet)),
                Dm.@new<datum_pipe_info_disposal>("Chute", typeof(obj_machinery_disposal_delivery_chute))
            ))
        );
        Dm.append(ref gvars_datum_init_order, "disposal_pipe_recipes");
    }

    // tg: code/_globalvars/lists/pipe_recipes.dm:54  GLOBAL_LIST_INIT(transit_tube_recipes, list(...))
    public DmList transit_tube_recipes;

    // tg: code/_globalvars/lists/pipe_recipes.dm:54  (GLOBAL_MANAGED, __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltransit_tube_recipes()
    {
        transit_tube_recipes = Dm.list(
            Dm.kv("Transit Tubes", Dm.list(
                Dm.@new<datum_pipe_info_transit>("Straight Tube", typeof(obj_structure_c_transit_tube), PIPE_STRAIGHT),
                Dm.@new<datum_pipe_info_transit>("Straight Tube with Crossing", typeof(obj_structure_c_transit_tube_crossing), PIPE_STRAIGHT),
                Dm.@new<datum_pipe_info_transit>("Curved Tube", typeof(obj_structure_c_transit_tube_curved), PIPE_UNARY_FLIPPABLE),
                Dm.@new<datum_pipe_info_transit>("Diagonal Tube", typeof(obj_structure_c_transit_tube_diagonal), PIPE_STRAIGHT),
                Dm.@new<datum_pipe_info_transit>("Diagonal Tube with Crossing", typeof(obj_structure_c_transit_tube_diagonal_crossing), PIPE_STRAIGHT),
                Dm.@new<datum_pipe_info_transit>("Junction", typeof(obj_structure_c_transit_tube_junction), PIPE_UNARY_FLIPPABLE)
            )),
            Dm.kv("Station Equipment", Dm.list(
                Dm.@new<datum_pipe_info_transit>("Through Tube Station", typeof(obj_structure_c_transit_tube_station), PIPE_STRAIGHT),
                Dm.@new<datum_pipe_info_transit>("Terminus Tube Station", typeof(obj_structure_c_transit_tube_station_reverse), PIPE_UNARY_FLIPPABLE),
                Dm.@new<datum_pipe_info_transit>("Through Tube Dispenser Station", typeof(obj_structure_c_transit_tube_station_dispenser), PIPE_STRAIGHT),
                Dm.@new<datum_pipe_info_transit>("Terminus Tube Dispenser Station", typeof(obj_structure_c_transit_tube_station_dispenser_reverse), PIPE_UNARY_FLIPPABLE),
                Dm.@new<datum_pipe_info_transit>("Transit Tube Pod", typeof(obj_structure_c_transit_tube_pod), PIPE_ONEDIR)
            ))
        );
        Dm.append(ref gvars_datum_init_order, "transit_tube_recipes");
    }
}
