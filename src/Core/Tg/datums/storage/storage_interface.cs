// Translated from tgstation (snapshot 1d99bd7f): code/datums/storage/storage_interface.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P3: the storage UI tracker (docs/ai/specs/m4a-ownership.md, P3's list). Its screen objects (/atom/movable/screen/close,
// storage_up and the /atom/movable/screen/storage cells, corners and row joins) are P2's screen_objects.cs; tg's
// show_contents puts them and the stored items into the viewer's client.screen and the HUD's storage group (D131: the
// client's container window lays them out). The silicon subtype (the cyborg's modules tray) since silicons SI2.

/// Datum tracker for storage UI
// tg: code/datums/storage/storage_interface.dm:2
public partial class datum_storage_interface : datum
{
    // tg: code/datums/storage/storage_interface.dm:17
    [DmParams("ui_style", "parent_storage", "user")]
    public override async DmTask New(params object[] __extra)
    {
        object ui_style = Dm.arg(__extra, 0);
        datum_storage parent_storage = Dm.arg<datum_storage>(__extra, 1);
        mob user = Dm.arg<mob>(__extra, 2);
        await base.New(Dm.forward(__extra, ui_style, parent_storage, user));
        this.parent_storage = parent_storage;
        datum_hud owner_hud = user.hud_used;
        closer = Dm.@new<atom_movable_screen_close>(null, owner_hud, parent_storage);
        upper = Dm.@new<atom_movable_screen_storage_up>(null, owner_hud, parent_storage);
        cells = Dm.@new<atom_movable_screen_storage_cell>(null, owner_hud, parent_storage);
        corner_top_left = Dm.@new<atom_movable_screen_storage_corner>(null, owner_hud, parent_storage);
        corner_top_right = Dm.@new<atom_movable_screen_storage_corner_top_right>(null, owner_hud, parent_storage);
        corner_bottom_left = Dm.@new<atom_movable_screen_storage_corner_bottom_left>(null, owner_hud, parent_storage);
        corner_bottom_right = Dm.@new<atom_movable_screen_storage_corner_bottom_right>(null, owner_hud, parent_storage);
        rowjoin_left = Dm.@new<atom_movable_screen_storage_rowjoin>(null, owner_hud, parent_storage);
        rowjoin_right = Dm.@new<atom_movable_screen_storage_rowjoin_right>(null, owner_hud, parent_storage);
        foreach (atom_movable_screen ui_elem in Dm.each_cast<atom_movable_screen>(list_ui_elements(initializing: TRUE)))
            ui_elem.icon = ui_style;
    }

    /// Returns all UI elements under this theme
    // tg: code/datums/storage/storage_interface.dm:34
    public virtual object list_ui_elements(float? initializing = null)
    {
        initializing ??= FALSE;
        return Dm.list(cells, corner_top_left, corner_top_right, corner_bottom_left, corner_bottom_right, rowjoin_left, rowjoin_right, upper, closer);
    }

    // tg: code/datums/storage/storage_interface.dm:37
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref closer);
        QDEL_NULL(ref cells);
        QDEL_NULL(ref corner_top_left);
        QDEL_NULL(ref corner_top_right);
        QDEL_NULL(ref corner_bottom_left);
        QDEL_NULL(ref corner_bottom_right);
        QDEL_NULL(ref rowjoin_left);
        QDEL_NULL(ref rowjoin_right);
        parent_storage = null;
        return base.Destroy(force);
    }

    /// Updates position of all UI elements
    // tg: code/datums/storage/storage_interface.dm:50
    public virtual void update_position(object screen_start_x = null, object screen_pixel_x = null, object screen_start_y = null, object screen_pixel_y = null, object columns = null, object rows = null, mob user_looking = null, atom real_location = null, DmList numbered_contents = null, params object[] __extra)
    {
        float number_of_hands = user_looking.held_items.len;
        while (number_of_hands > user_looking.default_hand_amount)
        {
            number_of_hands /= user_looking.default_hand_amount;
            screen_start_y = Dm.num(screen_start_y) + 1;
        }
        float start_pixel_x = Dm.num(screen_start_x) * 32 + Dm.num(screen_pixel_x);
        float start_pixel_y = Dm.num(screen_start_y) * 32 + Dm.num(screen_pixel_y);
        float end_pixel_x = start_pixel_x + (Dm.num(columns) - 1) * 32;
        float end_pixel_y = start_pixel_y + (Dm.num(rows) - 1) * 32;

        cells.screen_loc = spanning_screen_loc__gap(start_pixel_x, start_pixel_y, end_pixel_x, end_pixel_y);
        string left_edge_loc = spanning_screen_loc__gap(min(start_pixel_x + 32, end_pixel_x), start_pixel_y, end_pixel_x, end_pixel_y);
        string right_edge_loc = spanning_screen_loc__gap(start_pixel_x, start_pixel_y, max(start_pixel_x, end_pixel_x - 32), end_pixel_y);
        corner_top_left.screen_loc = left_edge_loc;
        corner_bottom_left.screen_loc = left_edge_loc;
        corner_top_right.screen_loc = right_edge_loc;
        corner_bottom_right.screen_loc = right_edge_loc;

        string row_left_loc = spanning_screen_loc__gap(start_pixel_x, start_pixel_y + 27, start_pixel_x, start_pixel_y + 27 + max(0, Dm.num(rows) - 2) * 32);
        rowjoin_left.screen_loc = row_left_loc;
        rowjoin_left.alpha = (Dm.num(rows) > 1 ? 1 : 0) * 255;

        string row_right_loc = spanning_screen_loc__gap(end_pixel_x, start_pixel_y + 27, end_pixel_x, start_pixel_y + 27 + max(0, Dm.num(rows) - 2) * 32);
        rowjoin_right.screen_loc = row_right_loc;
        rowjoin_right.alpha = (Dm.num(rows) > 1 ? 1 : 0) * 255;

        upper.screen_loc = Dm.text("[]:[],[]:[]", Dm.num(screen_start_x) + Dm.num(columns), Dm.num(screen_pixel_x) + 8, screen_start_y, screen_pixel_y);
        upper.invisibility = Dm.truthy(parent_storage.parent.loc?.atom_storage) ? 0 : INVISIBILITY_MAXIMUM;

        closer.screen_loc = Dm.text("[]:[],[]:[]", Dm.num(screen_start_x) + Dm.num(columns), Dm.num(screen_pixel_x) - 5, screen_start_y, screen_pixel_y);

        DmList args = Dm.args(screen_start_x, screen_pixel_x, screen_start_y, screen_pixel_y, columns, rows, user_looking, real_location, numbered_contents, __extra);
        Waiting.NoSleep(Dm.Call(this, nameof(add_items), Dm.arglist(args)));
    }

    // tg: code/datums/storage/storage_interface.dm:93
    public virtual void add_items(object screen_start_x = null, object screen_pixel_x = null, object screen_start_y = null, object screen_pixel_y = null, object columns = null, object rows = null, mob user_looking = null, atom real_location = null, DmList numbered_contents = null)
    {
        float current_x = Dm.num(screen_start_x);
        float current_y = Dm.num(screen_start_y);
        turf our_turf = get_turf(real_location);

        DmList storage_contents = Dm.list();
        if (islist(numbered_contents))
        {
            foreach (object content_type in Dm.each(numbered_contents))
            {
                datum_numbered_display numberdisplay = numbered_contents[content_type] as datum_numbered_display;
                storage_contents[numberdisplay.sample_object] = MAPTEXT(Dm.text("<font color='white'>[]</font>", (Dm.num(numberdisplay.number) > 1) ? Dm.text("[]", numberdisplay.number) : ""));
            }
        }
        else
        {
            foreach (obj item in Dm.each_cast<obj>(real_location))
                storage_contents[item] = "";
        }

        foreach (obj item in Dm.each_cast<obj>(storage_contents))
        {
            item.mouse_opacity = MOUSE_OPACITY_OPAQUE;
            item.screen_loc = Dm.text("[]:[],[]:[]", current_x, Dm.num(screen_pixel_x) + item.base_pixel_x, current_y, Dm.num(screen_pixel_y) + item.base_pixel_y);
            if (Dm.truthy(parent_storage.numerical_stacking))
                item.maptext = storage_contents[item] as string;
            SET_PLANE(item, ABOVE_HUD_PLANE, our_turf);
            current_x++;
            if (current_x - Dm.num(screen_start_x) < Dm.num(columns))
                continue;
            current_x = Dm.num(screen_start_x);

            current_y++;
            if (current_y - Dm.num(screen_start_y) >= Dm.num(rows))
                break;
        }
    }

    /// Returns a screen_loc format for a tiling screen objects from start and end positions. Start should be bottom left corner, and end top right corner.
    // GAP(lead): tg's global proc spanning_screen_loc (__HELPERS/screen_objs.dm:106) has no GlobalProcs member yet; a local
    // copy for the I1 cleanup.
    // tg: code/__HELPERS/screen_objs.dm:106
    private static string spanning_screen_loc__gap(float start_px, float start_py, float end_px, float end_py)
    {
        float starting_tile_x = round(start_px / ICON_SIZE_X);
        start_px -= starting_tile_x * ICON_SIZE_X;
        float starting_tile_y = round(start_py / ICON_SIZE_Y);
        start_py -= starting_tile_y * ICON_SIZE_Y;
        float ending_tile_x = round(end_px / ICON_SIZE_X);
        end_px -= ending_tile_x * ICON_SIZE_X;
        float ending_tile_y = round(end_py / ICON_SIZE_Y);
        end_py -= ending_tile_y * ICON_SIZE_Y;
        return Dm.text("[]:[],[]:[] to []:[],[]:[]", starting_tile_x, start_px, starting_tile_y, start_py, ending_tile_x, end_px, ending_tile_y, end_py);
    }
}

// tg: code/datums/storage/storage_interface.dm:134
public partial class datum_storage_interface_silicon : datum_storage_interface
{
    // tg: code/datums/storage/storage_interface.dm:137
    [DmParams("ui_style", "parent_storage", "user")]
    public override async DmTask New(params object[] __extra)
    {
        datum_storage parent_storage = Dm.arg<datum_storage>(__extra, 1);
        await base.New(__extra);
        robot_model = parent_storage.real_location as obj_item_robot_model;
    }

    // tg: code/datums/storage/storage_interface.dm:141
    public override void add_items(object screen_start_x = null, object screen_pixel_x = null, object screen_start_y = null, object screen_pixel_y = null, object columns = null, object rows = null, mob user_looking = null, atom real_location = null, DmList numbered_contents = null)
    {
        DmList usable_modules = robot_model.get_usable_modules() as DmList;

        float current_x = Dm.num(screen_start_x);
        float current_y = Dm.num(screen_start_y);
        turf our_turf = get_turf(real_location);

        foreach (obj_item item in Dm.each<obj_item>(usable_modules))
        {
            //Only for non active modules
            if (Dm.truthy(Dm.bitand(item.item_flags, IN_STORAGE)))
            {
                item.mouse_opacity = MOUSE_OPACITY_OPAQUE;
                SET_PLANE(item, ABOVE_HUD_PLANE, our_turf);
                item.screen_loc = Dm.text("[]:[],[]:[]", current_x, Dm.num(screen_pixel_x) + item.base_pixel_x, current_y, Dm.num(screen_pixel_y) + item.base_pixel_y);
            }

            current_x++;
            if (current_x - Dm.num(screen_start_x) < Dm.num(columns))
                continue;
            current_x = Dm.num(screen_start_x);

            current_y++;
            if (current_y - Dm.num(screen_start_y) >= Dm.num(rows))
                break;
        }
    }
}
