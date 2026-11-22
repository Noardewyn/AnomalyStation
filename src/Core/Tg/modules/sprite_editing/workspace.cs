// Translated from tgstation (snapshot 1d99bd7f): code/modules/sprite_editing/workspace.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI4d (D326): the whole file but to_icon (the NanoPaint program's image files, a live-partial row: the modular computers' image
// data is not ported). The var block is generated.

// tg: code/modules/sprite_editing/workspace.dm:1
public partial class datum_sprite_editor_workspace : datum
{
    // tg: code/modules/sprite_editing/workspace.dm:19
    [DmParams("width", "height", "dirs", "backdrop", "color_mode", "config_flags", "tool_flags", "initial_layer_color")]
    public override async DmTask New(params object[] __extra)
    {
        object width = Dm.arg(__extra, 0) ?? 32;
        object height = Dm.arg(__extra, 1) ?? 32;
        object dirs = Dm.arg(__extra, 2) ?? 1;
        object backdrop = Dm.arg(__extra, 3);
        object color_mode = Dm.arg(__extra, 4) ?? SPRITE_EDITOR_COLOR_MODE_RGBA;
        object config_flags = Dm.arg(__extra, 5) ?? ALL;
        object tool_flags = Dm.arg(__extra, 6) ?? ALL;
        object initial_layer_color = Dm.arg(__extra, 7);
        await base.New(__extra);
        this.width = width;
        this.height = height;
        this.dirs = dirs;
        this.color_mode = Dm.str(color_mode);
        this.config_flags = Dm.num(config_flags);
        this.tool_flags = Dm.num(tool_flags);
        this.backdrop = backdrop;
        layers = Dm.list(Dm.list(Dm.kv("name", "Background"), Dm.kv("visible", TRUE), Dm.kv("data", create_layer_data(initial_layer_color))));
    }

    // tg: code/modules/sprite_editing/workspace.dm:37
    public virtual datum_sprite_editor_workspace copy(object preserve_history = null)
    {
        // tg's positional call puts the colour mode in the backdrop's place (and every later argument one place on), as here
        var new_workspace = Dm.@new<datum_sprite_editor_workspace>(width, height, dirs, color_mode, config_flags, tool_flags);
        new_workspace.layers = deep_copy_list_alt(layers) as DmList;
        if (Dm.truthy(preserve_history))
        {
            new_workspace.undo_names = undo_names.Copy();
            new_workspace.undo_stack = deep_copy_list_alt(undo_stack) as DmList;
            new_workspace.redo_names = redo_names.Copy();
            new_workspace.redo_stack = deep_copy_list_alt(redo_stack) as DmList;
        }
        return new_workspace;
    }

    // tg: code/modules/sprite_editing/workspace.dm:47
    public virtual DmList create_layer_data(object color = null)
    {
        color ??= "#00000000";
        DmList @out = Dm.list();
        for (int i = 1; i <= Dm.num(dirs); i++)
        {
            DmList layer = Dm.list();
            for (int y = 1; y <= Dm.num(height); y++)
            {
                DmList row = Dm.list();
                for (int x = 1; x <= Dm.num(width); x++)
                    row.Add(color);
                layer.Add(Dm.list(row));
            }
            @out[Dm.text("[]", GLOB.alldirs_dmi_order[i])] = layer;
        }
        return @out;
    }

    /**
     * Take a new transaction, perform it, and optionally add it to the undo history.
     * Returns TRUE if the transaction was valid.
     */
    // tg: code/modules/sprite_editing/workspace.dm:63
    public virtual object new_transaction(object transaction = null)
    {
        var list = transaction as DmList;
        if (!Dm.truthy(can_transact(list)))
            return null;
        preprocess_new_transaction(list);
        transact(list);
        if (!Dm.truthy(Dm.bitand(config_flags ?? 0, SPRITE_EDITOR_ALLOW_UNDO)))
            return TRUE;
        redo_stack.Cut();
        redo_names.Cut();
        undo_stack.Add(Dm.list(list));
        undo_names.Add(list["name"]);
        return TRUE;
    }

    // tg: code/modules/sprite_editing/workspace.dm:76
    public virtual void undo()
    {
        if (!Dm.truthy(Dm.bitand(config_flags ?? 0, SPRITE_EDITOR_ALLOW_UNDO)))
            return;
        if (Dm.truthy(Dm.length(undo_stack)))
        {
            pop(undo_names);
            var transaction = pop(undo_stack) as DmList;
            reverse_transact(transaction);
            redo_stack.Add(Dm.list(transaction));
            redo_names.Add(transaction["name"]);
        }
    }

    // tg: code/modules/sprite_editing/workspace.dm:86
    public virtual void redo()
    {
        if (!Dm.truthy(Dm.bitand(config_flags ?? 0, SPRITE_EDITOR_ALLOW_UNDO)))
            return;
        if (Dm.truthy(Dm.length(redo_stack)))
        {
            pop(redo_names);
            var transaction = pop(redo_stack) as DmList;
            transact(transaction);
            undo_stack.Add(Dm.list(transaction));
            undo_names.Add(transaction["name"]);
        }
    }

    // tg: code/modules/sprite_editing/workspace.dm:96
    public virtual void toggle_layer_visible(object layer = null)
    {
        if (!Dm.truthy(Dm.bitand(config_flags ?? 0, SPRITE_EDITOR_ALLOW_LAYERS)))
            return;
        if (!isnum(layer))
            return;
        if (Dm.num(layer) < 1 || Dm.num(layer) > Dm.length(layers))
            return;
        var entry = (DmList)layers[layer];
        entry["visible"] = !Dm.truthy(entry["visible"]) ? TRUE : FALSE;
    }

    // tg: code/modules/sprite_editing/workspace.dm:105
    public virtual object is_valid_color(object color = null)
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_SPRITE_EDITOR_VALIDATE_COLOR]) && Dm.truthy(_SendSignal(COMSIG_SPRITE_EDITOR_VALIDATE_COLOR, Dm.list(this, color))))
            return FALSE;
        DmList rgb_color = split_color(color);
        switch (color_mode)
        {
            case SPRITE_EDITOR_COLOR_MODE_RGBA:
                return TRUE;
            case SPRITE_EDITOR_COLOR_MODE_RGB:
                return Dm.eq(rgb_color[4], 255) ? TRUE : FALSE;
            case SPRITE_EDITOR_COLOR_MODE_GREYSCALE:
                return Dm.eq(rgb_color[1], rgb_color[2]) && Dm.eq(rgb_color[2], rgb_color[3]) ? TRUE : FALSE;
            default:
                return TRUE;
        }
    }

    // tg: code/modules/sprite_editing/workspace.dm:120
    public virtual object can_transact(DmList transaction = null)
    {
        switch (Dm.str(transaction?["type"]))
        {
            case "pencil":
                return Dm.truthy(Dm.bitand(tool_flags, SPRITE_EDITOR_TOOL_PENCIL)) && Dm.truthy(is_valid_color(transaction["color"])) ? TRUE : FALSE;
            case "eraser":
                return Dm.bitand(tool_flags, SPRITE_EDITOR_TOOL_ERASER);
            case "bucket":
                return Dm.truthy(Dm.bitand(tool_flags, SPRITE_EDITOR_TOOL_BUCKET)) && Dm.truthy(is_valid_color(transaction["color"])) ? TRUE : FALSE;
            case "renameLayer":
            case "moveLayerUp":
            case "moveLayerDown":
            case "flattenLayer":
            case "addLayer":
            case "deleteLayer":
                return Dm.bitand(config_flags ?? 0, SPRITE_EDITOR_ALLOW_LAYERS);
            default: // Invalid transaction type, probably from href exploitation
                return FALSE;
        }
    }

    // The frame (a dir's rows) of a layer a transaction names.
    private DmList frame_of(DmList transaction) => (DmList)((DmList)((DmList)layers[transaction["layer"]])["data"])[transaction["dir"]];

    // tg: code/modules/sprite_editing/workspace.dm:133
    public virtual void preprocess_new_transaction(DmList transaction = null)
    {
        switch (Dm.str(transaction["type"]))
        {
            case "pencil":
            case "eraser":
            {
                DmList affected_frame = frame_of(transaction);
                foreach (DmList point in Dm.each<DmList>(transaction["points"] as DmList))
                {
                    object x = Dm.num(point[1]) + 1;
                    object y = Dm.num(point[2]) + 1;
                    point.Add(((DmList)affected_frame[y])[x]);
                }
                break;
            }
            case "bucket":
            {
                DmList affected_frame = frame_of(transaction);
                var point = (DmList)transaction["point"];
                object x = Dm.num(point[1]) + 1;
                object y = Dm.num(point[2]) + 1;
                transaction["points"] = flood_fill(affected_frame, x, y, width, height);
                Dm.remove(ref transaction, "point");
                break;
            }
            case "flattenLayer":
            {
                object layer = transaction["layer"];
                var top_layer = (DmList)layers[layer];
                var bottom_layer = (DmList)layers[Dm.num(layer) - 1];
                transaction["oldTop"] = top_layer;
                transaction["oldBottom"] = deep_copy_list(bottom_layer);
                break;
            }
            case "deleteLayer":
            {
                object layer = transaction["layer"];
                transaction["oldLayer"] = layers[layer];
                break;
            }
        }
    }

    // tg: code/modules/sprite_editing/workspace.dm:162
    public virtual void transact(DmList transaction = null)
    {
        switch (Dm.str(transaction["type"]))
        {
            case "pencil":
            case "bucket":
            {
                object color = transaction["color"];
                DmList affected_frame = frame_of(transaction);
                foreach (DmList point in Dm.each<DmList>(transaction["points"] as DmList))
                {
                    var row = (DmList)affected_frame[Dm.num(point[2]) + 1];
                    object x = Dm.num(point[1]) + 1;
                    row[x] = blend_color(row[x], color);
                }
                break;
            }
            case "eraser":
            {
                DmList affected_frame = frame_of(transaction);
                foreach (DmList point in Dm.each<DmList>(transaction["points"] as DmList))
                    ((DmList)affected_frame[Dm.num(point[2]) + 1])[Dm.num(point[1]) + 1] = "#00000000";
                break;
            }
            case "renameLayer":
                ((DmList)layers[transaction["layer"]])["name"] = transaction["newName"];
                break;
            case "moveLayerUp":
            {
                float layer = Dm.num(transaction["layer"]);
                layers.Swap(layer, layer + 1);
                break;
            }
            case "moveLayerDown":
            {
                float layer = Dm.num(transaction["layer"]);
                layers.Swap(layer, layer - 1);
                break;
            }
            case "flattenLayer":
            {
                float layer = Dm.num(transaction["layer"]);
                var top_layer = (DmList)layers[layer];
                var bottom_layer = (DmList)layers[layer - 1];
                for (int dir = 1; dir <= Dm.num(dirs); dir++)
                {
                    // tg indexes the layers' data by "[dir]" here (1, 2, ...), not by the dmi order's dirs
                    var bottom = (DmList)((DmList)bottom_layer[Dm.text("[]", dir)]);
                    var top = (DmList)((DmList)top_layer[Dm.text("[]", dir)]);
                    for (int y = 1; y <= Dm.num(height); y++)
                        for (int x = 1; x <= Dm.num(width); x++)
                            ((DmList)bottom[y])[x] = blend_color(((DmList)bottom[y])[x], ((DmList)top[y])[x]);
                }
                layers.Cut(layer, layer + 1);
                break;
            }
            case "addLayer":
            {
                string layer_name = "New Layer";
                int dupe_count = 1;
                foreach (DmList layer in Dm.each<DmList>(layers))
                {
                    if (Dm.eq(layer["name"], layer_name))
                        layer_name = Dm.text("New Layer []", ++dupe_count);
                }
                layers.Add(Dm.list(Dm.list(Dm.kv("name", layer_name), Dm.kv("visible", TRUE), Dm.kv("data", create_layer_data()))));
                break;
            }
            case "deleteLayer":
            {
                float layer = Dm.num(transaction["layer"]);
                layers.Cut(layer, layer + 1);
                break;
            }
        }
    }

    // tg: code/modules/sprite_editing/workspace.dm:211
    public virtual void reverse_transact(DmList transaction = null)
    {
        switch (Dm.str(transaction["type"]))
        {
            case "pencil":
            case "eraser":
            case "bucket":
            {
                DmList affected_frame = frame_of(transaction);
                foreach (DmList point in Dm.each<DmList>(transaction["points"] as DmList))
                    ((DmList)affected_frame[Dm.num(point[2]) + 1])[Dm.num(point[1]) + 1] = point[3];
                break;
            }
            case "renameLayer":
                ((DmList)layers[transaction["layer"]])["name"] = transaction["oldName"];
                break;
            case "moveLayerUp":
            {
                float layer = Dm.num(transaction["layer"]);
                layers.Swap(layer, layer + 1);
                break;
            }
            case "moveLayerDown":
            {
                float layer = Dm.num(transaction["layer"]);
                layers.Swap(layer, layer - 1);
                break;
            }
            case "flattenLayer":
            {
                float layer = Dm.num(transaction["layer"]);
                object top_layer = transaction["oldTop"];
                var bottom_layer = (DmList)transaction["oldBottom"];
                float bottom_layer_index = layer - 1;
                object old_visibility = ((DmList)layers[bottom_layer_index])["visible"];
                layers[bottom_layer_index] = bottom_layer;
                bottom_layer["visible"] = old_visibility;
                layers.Insert(layer, top_layer);
                break;
            }
            case "addLayer":
                pop(layers);
                break;
            case "deleteLayer":
                layers.Insert(Dm.num(transaction["layer"]), transaction["oldLayer"]);
                break;
        }
    }

    // tg: code/modules/sprite_editing/workspace.dm:250
    public virtual DmList sprite_editor_ui_data()
    {
        return Dm.list(
            Dm.kv("colorMode", color_mode),
            Dm.kv("toolFlags", tool_flags),
            Dm.kv("undoStack", undo_names),
            Dm.kv("redoStack", redo_names),
            Dm.kv("sprite", Dm.list(
                Dm.kv("width", width),
                Dm.kv("height", height),
                Dm.kv("dirs", dirs),
                Dm.kv("backdrop", backdrop),
                Dm.kv("layers", layers)
            ))
        );
    }

    /// Get a reference to the pixel data for the first layer of the given dir
    // tg: code/modules/sprite_editing/workspace.dm:265
    public virtual DmList get_first_layer_pixel_data(object dir = null)
    {
        dir ??= SOUTH;
        return ((DmList)((DmList)layers[1])["data"])[Dm.text("[]", dir)] as DmList;
    }
}
