// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/pipe_info.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice D172 (the rapid pipe dispenser): the whole file. The recipe datums of the RPD (and of the pipe dispenser machine):
// the atmospherics, disposal and transit tube kinds and their previews. The var blocks are generated.

///Pipe info
// tg: code/_globalvars/pipe_info.dm:2
public partial class datum_pipe_info : datum
{
    /**
     * Get preview image of an pipe
     * Arguments
     *
     * * selected_dir - the direction of the pipe to get preview of
     * * selected - is this pipe meant to be highlighted in the UI
     */
    // tg: code/_globalvars/pipe_info.dm:23
    public virtual object get_preview(object selected_dir = null, float? selected = null)
    {
        selected ??= FALSE;

        DmList dirs = null;
        object sw__1 = dirtype;
        if (Dm.eq(sw__1, PIPE_STRAIGHT) || Dm.eq(sw__1, PIPE_BENDABLE))
        {
            dirs = Dm.list(Dm.kv(Dm.text("[]", NORTH), "Vertical"), Dm.kv(Dm.text("[]", EAST), "Horizontal"));
            if (Dm.eq(dirtype, PIPE_BENDABLE))
                Dm.append(ref dirs, Dm.list(Dm.kv(Dm.text("[]", NORTHWEST), "West to North"), Dm.kv(Dm.text("[]", NORTHEAST), "North to East"),
                    Dm.kv(Dm.text("[]", SOUTHWEST), "South to West"), Dm.kv(Dm.text("[]", SOUTHEAST), "East to South")));
        }
        else if (Dm.eq(sw__1, PIPE_TRINARY))
            dirs = Dm.list(Dm.kv(Dm.text("[]", NORTH), "West South East"), Dm.kv(Dm.text("[]", SOUTH), "East North West"),
                Dm.kv(Dm.text("[]", EAST), "North West South"), Dm.kv(Dm.text("[]", WEST), "South East North"));
        else if (Dm.eq(sw__1, PIPE_TRIN_M))
            dirs = Dm.list(Dm.kv(Dm.text("[]", NORTH), "North East South"), Dm.kv(Dm.text("[]", SOUTHWEST), "North West South"),
                Dm.kv(Dm.text("[]", NORTHEAST), "South East North"), Dm.kv(Dm.text("[]", SOUTH), "South West North"),
                Dm.kv(Dm.text("[]", WEST), "West North East"), Dm.kv(Dm.text("[]", SOUTHEAST), "West South East"),
                Dm.kv(Dm.text("[]", NORTHWEST), "East North West"), Dm.kv(Dm.text("[]", EAST), "East South West"));
        else if (Dm.eq(sw__1, PIPE_UNARY))
            dirs = Dm.list(Dm.kv(Dm.text("[]", NORTH), "North"), Dm.kv(Dm.text("[]", SOUTH), "South"), Dm.kv(Dm.text("[]", WEST), "West"), Dm.kv(Dm.text("[]", EAST), "East"));
        else if (Dm.eq(sw__1, PIPE_ONEDIR))
            dirs = Dm.list(Dm.kv(Dm.text("[]", SOUTH), name));
        else if (Dm.eq(sw__1, PIPE_UNARY_FLIPPABLE))
            dirs = Dm.list(Dm.kv(Dm.text("[]", NORTH), "North"), Dm.kv(Dm.text("[]", EAST), "East"), Dm.kv(Dm.text("[]", SOUTH), "South"), Dm.kv(Dm.text("[]", WEST), "West"),
                Dm.kv(Dm.text("[]", NORTHEAST), "North Flipped"), Dm.kv(Dm.text("[]", SOUTHEAST), "East Flipped"), Dm.kv(Dm.text("[]", SOUTHWEST), "South Flipped"), Dm.kv(Dm.text("[]", NORTHWEST), "West Flipped"));
        else if (Dm.eq(sw__1, PIPE_ONEDIR_FLIPPABLE))
            dirs = Dm.list(Dm.kv(Dm.text("[]", SOUTH), name), Dm.kv(Dm.text("[]", SOUTHEAST), Dm.text("[] Flipped", name)));

        DmList rows = Dm.list();
        foreach (object dir in Dm.each(dirs))
        {
            float numdir = Dm.num(text2num(dir));
            // DM's && gives its last operand: ISDIAGONALDIR's bit (4 or 8) for a flipped variant, else the FALSE of the comparisons (rules 4.5)
            float flipped = ((Dm.eq(dirtype, PIPE_TRIN_M) && !ispath(id, typeof(obj_structure_disposalpipe))) || Dm.eq(dirtype, PIPE_UNARY_FLIPPABLE) || Dm.eq(dirtype, PIPE_ONEDIR_FLIPPABLE)) ? ISDIAGONALDIR(numdir) : FALSE;
            object is_variant_selected = Dm.truthy(selected) ? (!Dm.truthy(selected_dir) ? FALSE : (Dm.eq(dirtype, PIPE_ONEDIR) ? TRUE : Dm.num(Dm.eq(numdir, selected_dir)))) : selected;
            Dm.append(ref rows, Dm.list(Dm.list(
                Dm.kv("selected", is_variant_selected),
                Dm.kv("dir", dir2text(numdir)),
                Dm.kv("dir_name", dirs[dir]),
                Dm.kv("icon_state", icon_state),
                Dm.kv("flipped", flipped))));
        }

        return rows;
    }
}

//==============================================================================================

// tg: code/_globalvars/pipe_info.dm:74
public partial class datum_pipe_info_pipe : datum_pipe_info
{
    // tg: code/_globalvars/pipe_info.dm:77
    [DmParams("label", "path", "use_five_layers")]
    public override async DmTask New(params object[] __extra)
    {
        object label = Dm.arg(__extra, 0);
        Type path = Dm.arg(__extra, 1) as Type;
        object use_five_layers = Dm.arg(__extra, 2);
        if (name == null) // name ??= label
            name = label;
        if (id == null) // id ??= path
            id = path;
        if (all_layers == null) // all_layers ??= use_five_layers
            all_layers = use_five_layers;

        if (!ispath(id, typeof(obj_machinery_atmospherics)))
            return;

        Type id_atmos = id;
        if (icon_state == null) // icon_state ??= initial(path.pipe_state)
            icon_state = Dm.initial(id_atmos, (obj_machinery_atmospherics p) => p.pipe_state);
        if (dirtype == null) // dirtype ??= id_atmos::construction_type::RPD_type
            dirtype = Dm.num_or_null(Dm.initial(Dm.initial(id_atmos, (obj_machinery_atmospherics p) => p.construction_type), (obj_item_pipe p) => p.RPD_type));
    }
}

//==============================================================================================

///Disposal pipe info
// tg: code/_globalvars/pipe_info.dm:182
public partial class datum_pipe_info_disposal : datum_pipe_info
{
    // tg: code/_globalvars/pipe_info.dm:184
    [DmParams("label", "path", "dt")]
    public override async DmTask New(params object[] __extra)
    {
        object label = Dm.arg(__extra, 0);
        Type path = Dm.arg(__extra, 1) as Type;
        object dt = Dm.arg(__extra, 2);
        dt ??= PIPE_UNARY;
        name = label;
        id = path;

        icon_state = Dm.initial(path, (atom p) => p.icon_state);
        if (ispath(path, typeof(obj_structure_disposalpipe)))
            icon_state = Dm.text("con[]", icon_state);

        dirtype = Dm.num_or_null(dt);
    }
}

//==============================================================================================

///Transient tube pipe info
// tg: code/_globalvars/pipe_info.dm:198
public partial class datum_pipe_info_transit : datum_pipe_info
{
    // tg: code/_globalvars/pipe_info.dm:200
    [DmParams("label", "path", "dt")]
    public override async DmTask New(params object[] __extra)
    {
        object label = Dm.arg(__extra, 0);
        Type path = Dm.arg(__extra, 1) as Type;
        object dt = Dm.arg(__extra, 2);
        dt ??= PIPE_UNARY;
        name = label;
        id = path;
        dirtype = Dm.num_or_null(dt);
        icon_state = Dm.initial(path, (atom p) => p.icon_state);
        if (Dm.eq(dt, PIPE_UNARY_FLIPPABLE))
            icon_state = Dm.text("[]_preview", icon_state);
    }
}
