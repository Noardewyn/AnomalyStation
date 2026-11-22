// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/action_group.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4c slice 3 (D146): the whole file except check_against_view (:94), which fits the groups into a changed client view
// (view_to_pixels and screen_loc_to_offset are not declared; the view size is fixed at 15 x 15 until the view, M9, and
// view_audit_buttons, its only caller, keeps the call deferred). The var blocks are generated.

// tg: code/_onclick/hud/action_group.dm:1
[DmPath("/datum/action_group")]
public partial class datum_action_group : datum
{
    // tg: code/_onclick/hud/action_group.dm:24
    [DmParams("owner")]
    public override async DmTask New(params object[] __extra)
    {
        datum_hud owner = Dm.arg<datum_hud>(__extra, 0);
        await base.New(__extra);
        actions = Dm.list();
        this.owner = owner;
    }

    // tg: code/_onclick/hud/action_group.dm:29
    public override object Destroy(float? force = null)
    {
        owner = null;
        QDEL_NULL(ref landing);
        QDEL_LIST(actions);
        return base.Destroy(force);
    }

    // tg: code/_onclick/hud/action_group.dm:35
    public virtual async DmTask insert_action(atom_movable_screen action = null, object index = null)
    {
        if (Dm.@in(action, actions))
        {
            // if(actions[index] == action) return (:37): a null or out-of-range index reads no element here (DM's list read
            // of such an index errors, and tg never inserts an action already in the group: position_action hides it first)
            if (index != null && Dm.num(index) >= 1 && Dm.num(index) <= actions.Count && actions[Dm.num(index)] == action)
                return;
            Dm.remove(ref actions, action); // Don't dupe, come on
        }
        if (!Dm.truthy(index))
            index = Dm.length(actions) + 1;
        index = Dm.min(Dm.length(actions) + 1, Dm.num(index));
        actions.Insert(Dm.num(index), action);
        refresh_actions();
    }

    // tg: code/_onclick/hud/action_group.dm:46
    public virtual async DmTask remove_action(atom_movable_screen action = null)
    {
        Dm.remove(ref actions, action);
        refresh_actions();
    }

    // tg: code/_onclick/hud/action_group.dm:50
    public virtual void refresh_actions()
    {
        // We don't use size() here because landings are not canon
        float total_rows = ROUND_UP(Dm.length(actions) / column_max);
        total_rows -= max_rows; // Lets get the amount of rows we're off from our max
        row_offset = clamp(row_offset, 0, total_rows); // You're not allowed to offset so far that we have a row of blank space

        float button_number = 0;
        foreach (atom_movable_screen button in Dm.each_cast<atom_movable_screen>(actions))
        {
            object postion = ButtonNumberToScreenCoords(button_number);
            button.screen_loc = postion as string;
            button_number++;
        }

        if (Dm.truthy(landing))
        {
            object postion = ButtonNumberToScreenCoords(button_number, landing: TRUE); // Need a good way to count buttons off screen, but allow this to display in the right place if it's being placed with no concern for dropdown
            landing.screen_loc = postion as string;
            button_number++;
        }
    }

    /// Accepts a number represeting our position in the group, indexes at 0 to make the math nicer
    // tg: code/_onclick/hud/action_group.dm:69
    public virtual object ButtonNumberToScreenCoords(object number = null, float? landing = null)
    {
        landing ??= FALSE;
        float n = Dm.num(number);
        float row = Dm.round(n / column_max);
        row -= row_offset; // If you're less then 0, you don't get to render, this lets us "scroll" rows ya feel?
        if (row < 0)
            return null;

        // Could use >= here, but I think it's worth noting that the two start at different places, since row is based on number here
        if (row > max_rows - 1)
        {
            if (!Dm.truthy(landing)) // If you're not a landing, go away please. thx
                return null;
            // We always want to render landings, even if their action button can't be displayed.
            // So we set a row equal to the max amount of rows + 1. Willing to overrun that max slightly to properly display the landing spot
            row = max_rows; // Remembering that max_rows indexes at 1, and row indexes at 0

            // We're going to need to set our column to match the first item in the last row, so let's set number properly now
            n = row * column_max;
        }

        float visual_row = row + north_offset;
        string coord_row = Dm.truthy(visual_row) ? Dm.text("-[]", visual_row) : "+0";

        float visual_column = Dm.mod(n, column_max);
        string coord_col = Dm.text("+[]", visual_column);
        float coord_col_offset = 4 + 2 * (visual_column + 1);
        return Dm.text("WEST[]:[],NORTH[]:-[]", coord_col, coord_col_offset, coord_row, pixel_north_offset);
    }

    // tg: code/_onclick/hud/action_group.dm:94
    public virtual void check_against_view()
    {
        object owner_view = owner?.mymob?.canon_client?.view;
        if (!Dm.truthy(owner_view))
            return;
        // The rest (:98-116) fits column_max into the client's view with view_to_pixels and screen_loc_to_offset
        // (__HELPERS/view.dm, screen_objs.dm), which are not declared; the view is a fixed 15 x 15 until the view (M9).
        Dm.NotTranslated("code/_onclick/hud/action_group.dm:98", "check_against_view: fitting the buttons into a changed view (view_to_pixels, screen_loc_to_offset; the view, M10+)");
    }

    /// Returns the amount of objects we're storing at the moment
    // tg: code/_onclick/hud/action_group.dm:119
    public virtual object size()
    {
        float amount = Dm.length(actions);
        if (Dm.truthy(landing))
            amount += 1;
        return amount;
    }

    // tg: code/_onclick/hud/action_group.dm:125
    public virtual object index_of(atom_movable_screen get_location = null)
    {
        return actions.Find(get_location);
    }

    /// Generates a landing object that can be dropped on to join this group
    // tg: code/_onclick/hud/action_group.dm:129
    public virtual void generate_landing()
    {
        if (Dm.truthy(landing))
            return;
        landing = Dm.@new<atom_movable_screen_action_landing>();
        landing.set_owner(this);
        refresh_actions();
    }

    /// Clears any landing objects we may currently have
    // tg: code/_onclick/hud/action_group.dm:137
    public virtual void clear_landing()
    {
        QDEL_NULL(ref landing);
    }

    // tg: code/_onclick/hud/action_group.dm:140
    public virtual void update_landing()
    {
        if (!Dm.truthy(landing))
            return;
        landing.refresh_owner();
    }

    // tg: code/_onclick/hud/action_group.dm:145
    public virtual void scroll(object amount = null)
    {
        row_offset += Dm.num(amount);
        refresh_actions();
    }
}

// tg: code/_onclick/hud/action_group.dm:149
[DmPath("/datum/action_group/palette")]
public partial class datum_action_group_palette : datum_action_group
{
    // tg: code/_onclick/hud/action_group.dm:155
    public override async DmTask insert_action(atom_movable_screen action = null, object index = null)
    {
        await base.insert_action(action, index);
        atom_movable_screen_button_palette palette = owner.screen_objects[HUD_MOB_TOGGLE_PALETTE] as atom_movable_screen_button_palette;
        await palette.play_item_added();
    }

    // tg: code/_onclick/hud/action_group.dm:160
    public override async DmTask remove_action(atom_movable_screen action = null)
    {
        await base.remove_action(action);
        atom_movable_screen_button_palette palette = owner.screen_objects[HUD_MOB_TOGGLE_PALETTE] as atom_movable_screen_button_palette;
        await palette.play_item_removed();
        if (!Dm.truthy(Dm.length(actions)))
            palette.set_expanded(FALSE);
    }

    // tg: code/_onclick/hud/action_group.dm:167
    public override void refresh_actions()
    {
        atom_movable_screen_button_palette palette = owner.screen_objects[HUD_MOB_TOGGLE_PALETTE] as atom_movable_screen_button_palette;
        atom_movable_screen_palette_scroll scroll_down = owner.screen_objects[HUD_MOB_PALETTE_DOWN] as atom_movable_screen_palette_scroll;
        atom_movable_screen_palette_scroll scroll_up = owner.screen_objects[HUD_MOB_PALETTE_UP] as atom_movable_screen_palette_scroll;

        float actions_above = Dm.round((Dm.num(owner.listed_actions.size()) - 1) / owner.listed_actions.column_max);
        north_offset = Dm.initial(this, (datum_action_group_palette p) => p.north_offset) + actions_above;

        palette.screen_loc = ui_action_palette_offset(actions_above);
        float action_count = Dm.length(owner?.mymob?.actions);
        float our_row_count = Dm.round((Dm.length(actions) - 1) / column_max);
        if (!Dm.truthy(action_count))
            palette.screen_loc = null;

        if (Dm.truthy(palette.expanded) && Dm.truthy(action_count) && our_row_count >= max_rows)
        {
            scroll_down.screen_loc = ui_palette_scroll_offset(actions_above);
            scroll_up.screen_loc = ui_palette_scroll_offset(actions_above);
        }
        else
        {
            scroll_down.screen_loc = null;
            scroll_up.screen_loc = null;
        }

        base.refresh_actions();
    }

    // tg: code/_onclick/hud/action_group.dm:190
    public override object ButtonNumberToScreenCoords(object number = null, float? landing = null)
    {
        atom_movable_screen_button_palette palette = owner.screen_objects[HUD_MOB_TOGGLE_PALETTE] as atom_movable_screen_button_palette;
        if (Dm.truthy(palette.expanded))
            return base.ButtonNumberToScreenCoords(number, landing);

        if (!Dm.truthy(landing))
            return null;

        // We only render the landing in this case, so we force it to be the second item displayed (Second rather then first since it looks nicer)
        // Remember the number var indexes at 0
        return base.ButtonNumberToScreenCoords(1 + (row_offset * column_max), landing);
    }
}

// tg: code/_onclick/hud/action_group.dm:202
[DmPath("/datum/action_group/listed")]
public partial class datum_action_group_listed : datum_action_group
{
    // tg: code/_onclick/hud/action_group.dm:207
    public override void refresh_actions()
    {
        base.refresh_actions();
        owner?.palette_actions.refresh_actions(); // We effect them, so we gotta refresh em
    }
}
