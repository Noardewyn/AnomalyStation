// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/pipes/smart.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The smart pipe's procs (M3 I1b, D123: the pipes' pictures); GLOB.atmos_components (:1) is not translated (nothing reads
// it in M3: not-translated.md).

//Smart pipes... or are they?
// tg: code/modules/atmospherics/machinery/pipes/smart.dm:3
public partial class obj_machinery_atmospherics_pipe_smart : obj_machinery_atmospherics_pipe
{
    // tg: code/modules/atmospherics/machinery/pipes/smart.dm:18
    public override void update_pipe_icon()
    {
        icon = Dm.resource("icons/obj/pipes_n_cables/!pipes_bitmask.dmi");

        //find all directions this pipe is connected with other nodes
        connections = NONE;
        float new_volume = 0;
        for (float i = 1, i__end = Dm.num(device_type); i <= i__end; i++)
        {
            if (!Dm.truthy(nodes[i]))
                continue;
            obj_machinery_atmospherics node = nodes[i] as obj_machinery_atmospherics;
            float connected_dir = get_dir(this, node);
            connections = Dm.bitor(Dm.num(connections), connected_dir);
            new_volume += UNARY_PIPE_VOLUME;
        }
        new_volume = Dm.max(new_volume, UNARY_PIPE_VOLUME * 2);
        set_volume(new_volume);

        //set the correct direction for this node in case of binary directions
        float sw__1 = Dm.num(connections);
        if (sw__1 == Dm.bitor(EAST, WEST))
            dir = EAST;
        else if (sw__1 == Dm.bitor(SOUTH, NORTH))
            dir = SOUTH;
        else
            dir = Dm.num(connections);

        // Smart pipe icons differ from classic pipe icons in that we stop adding
        // short pipe directions as soon as we find a valid sprite, rather than
        // adding in all connectable directions.
        // This prevents a lot of visual clutter, though it does make it harder to
        // notice completely disconnected pipes.
        float bitfield = CARDINAL_TO_FULLPIPES(Dm.num(connections));
        if (ISSTUB(Dm.num(connections)))
        {
            float bits_to_add = NONE;
            if (Dm.num(connections) != NONE)
                bits_to_add = Dm.bitor(bits_to_add, Dm.bitand(REVERSE_DIR(Dm.num(connections)), initialize_directions));

            float candidate = 0;
            float shift = 0;

            // Note that candidates "should" never reach 0, as stub pipes are not allowed and break things
            while (ISSTUB(Dm.bitor(Dm.num(connections), bits_to_add)) && Dm.shr(initialize_directions, shift) != 0)
            {
                //lets see if this direction is eligable to be added
                candidate = Dm.bitand(initialize_directions, Dm.shl(1, shift));
                //we dont want to add connections again else it creates wrong values & its also redundant[bitfield was already initialized with connections so we shoudnt append it again]
                if (!Dm.truthy(Dm.bitand(candidate, Dm.num(connections))))
                    bits_to_add = Dm.bitor(bits_to_add, candidate);
                shift += 1;
            }
            bitfield = Dm.bitor(bitfield, CARDINAL_TO_SHORTPIPES(bits_to_add));
        }
        icon_state = Dm.text("[]_[]", bitfield, piping_layer);
    }

    // tg: code/modules/atmospherics/machinery/pipes/smart.dm:68
    public override void set_init_directions(object init_dir = null)
    {
        if (Dm.truthy(init_dir))
        {
            float new_volume = 0;
            initialize_directions = Dm.num(init_dir);
            float j = 1;
            for (float i = 1; i <= 4; i++)
            {
                if (Dm.truthy(Dm.bitand(Dm.num(init_dir), j)))
                    new_volume += UNARY_PIPE_VOLUME;
                j = Dm.shl(j, 1);
            }
            new_volume = Dm.max(new_volume, UNARY_PIPE_VOLUME * 2); // Minimum 2 directions
            set_volume(new_volume);
        }
        else
        {
            initialize_directions = ALL_CARDINALS;
            set_volume(UNARY_PIPE_VOLUME * 4);
        }
    }
}
