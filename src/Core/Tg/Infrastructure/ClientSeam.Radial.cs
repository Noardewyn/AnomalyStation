using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

// Protocol version 15 (M4c slice 4, DV40): the client's answer to a radial menu. tg's menu is a set of screen objects in the
// player's client.images; a click on a slice or on the centre runs their Click (radial.dm:53, :72). The client draws the menu
// itself (the replicator sends client.dm_radial_menus) and sends which element it picked; this is BYOND's side of that click.

public static partial class ClientSeam
{
    /// <summary>
    /// The player picked element <paramref name="element"/> (1 based, the current page's order) of the radial menu
    /// <paramref name="menuId"/> shown to them, or the centre button (<paramref name="element"/> 0): tg's
    /// <c>/atom/movable/screen/radial/slice/Click</c> (<c>radial.dm:53</c>: the next page, or <c>element_chosen</c>) or
    /// <c>/atom/movable/screen/radial/center/Click</c> (<c>:72</c>: <c>finished</c>), with <c>usr</c> the client's mob. A menu that
    /// is no longer shown to the player (answered, closed or deleted meanwhile), an element past the page or a centre the menu
    /// does not have is a click on nothing: ignored. Detached, as BYOND starts a click. A persistent menu (the rapid pipe cleaner
    /// layer's) takes its centre as a pick of null and goes on being shown (DV79).
    /// </summary>
    public static void RadialPick(client player, long menuId, int element)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null)
            return;
        datum_radial_menu parent = ShownRadial(player, menuId);
        if (parent == null)
            return;
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            mob user = player.mob;
            world.usr = user;
            if (element == 0)
            {
                // tg: code/_onclick/hud/screen_objects/radial.dm:72  /atom/movable/screen/radial/center/Click
                if (parent.dm_close_button == null)
                    return; // display_close_button FALSE: tg makes no centre button
                if (user?.client == parent.current_user)
                {
                    if (parent is datum_radial_menu_persistent)
                    {
                        // tg: code/_onclick/hud/screen_objects/radial_persistent.dm:10  /atom/movable/screen/radial/persistent/center/Click:
                        // parent.element_chosen(null, usr, params); the menu goes on (DEVIATION(DV79): sent again below)
                        Waiting.Detach(parent.element_chosen(null, user, ""));
                        parent.dm_serial++;
                    }
                    else
                        parent.finished = Dm.TRUE;
                }
                return;
            }
            if (element < 1 || element > parent.dm_elements.Count)
                return;
            RadialElement slice = parent.dm_elements[element - 1];
            // tg: code/_onclick/hud/screen_objects/radial.dm:53  /atom/movable/screen/radial/slice/Click
            if (user?.client == parent.current_user)
            {
                if (slice.NextPage)
                    parent.next_page();
                else
                {
                    Waiting.Detach(parent.element_chosen(slice.Choice, user, ""));
                    // DEVIATION(DV79): a persistent menu stays; the client closed its drawing at the pick, so the menu is sent again
                    if (parent is datum_radial_menu_persistent)
                        parent.dm_serial++;
                }
            }
        }
        finally
        {
            world.usr = caller_usr;
        }
    }

    /// <summary>The radial menu <paramref name="menuId"/> among those shown to <paramref name="player"/> (<c>client.dm_radial_menus</c>), or null.</summary>
    public static datum_radial_menu ShownRadial(client player, long menuId)
    {
        DmList shown = player?.dm_radial_menus;
        if (shown == null || menuId <= 0)
            return null;
        for (int i = 1; i <= shown.Count; i++)
        {
            if (shown[(float)i] is datum_radial_menu menu && menu.dm_id == menuId && menu.gc_destroyed == null)
                return menu;
        }
        return null;
    }
}
