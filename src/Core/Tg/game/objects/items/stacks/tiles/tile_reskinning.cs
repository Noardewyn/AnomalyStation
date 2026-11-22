// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/tiles/tile_reskinning.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2 (materials and stacks): the whole file. The [DmPath] of /obj/item/stack/tile is held here (rules 2.3; written
// by package B, wave 4); its var block is generated (D111).

/////////////////////
// Tile reskinning //
/////////////////////
// Q: What is this?
// A: A simple function to allow you to change what tiles you place with a stack of tiles.
// Q: Why do it this way?
// A: This allows players more freedom to do beautiful-looking builds.
// Q: Great! Can I use this for all floors?
// A: Yep! Just change the tile stack's `tile_reskin_lists` list variable and set which variants you want to be able to convert into.

public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/tiles/tile_reskinning.dm:11  GLOBAL_LIST_EMPTY(tile_reskin_lists)
    public DmList tile_reskin_lists;

    // tg: code/game/objects/items/stacks/tiles/tile_reskinning.dm:11  (GLOBAL_MANAGED(tile_reskin_lists, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltile_reskin_lists()
    {
        tile_reskin_lists = Dm.list();
        Dm.append(ref gvars_datum_init_order, "tile_reskin_lists");
    }

    // tg: code/game/objects/items/stacks/tiles/tile_reskinning.dm:13  GLOBAL_LIST_EMPTY(tile_dir_lists)
    public DmList tile_dir_lists;

    // tg: code/game/objects/items/stacks/tiles/tile_reskinning.dm:13  (GLOBAL_MANAGED(tile_dir_lists, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltile_dir_lists()
    {
        tile_dir_lists = Dm.list();
        Dm.append(ref gvars_datum_init_order, "tile_dir_lists");
    }
}

// tg: code/game/objects/items/stacks/tiles/tile_reskinning.dm:18
[DmPath("/obj/item/stack/tile")]
public partial class obj_item_stack_tile : obj_item_stack
{
    /**
     * Caches associative lists with type path index keys and images of said type's initial icon state (typepath -> image).
     */
    // tg: code/game/objects/items/stacks/tiles/tile_reskinning.dm:18
    public virtual object tile_reskin_list(DmList values = null)
    {
        object dot = null;
        string string_id = values.Join("-");
        dot = GLOB.tile_reskin_lists[string_id];
        if (Dm.truthy(dot))
            return dot;
        foreach (object path in Dm.each(values.Copy()))
        {
            Type type_cast_path = path as Type;
            values[path] = Dm.image(icon: Dm.initial(type_cast_path, (atom a) => a.icon), icon_state: Dm.initial(type_cast_path, (atom a) => a.icon_state));
        }
        return GLOB.tile_reskin_lists[string_id] = values;
    }

    // tg: code/game/objects/items/stacks/tiles/tile_reskinning.dm:28
    // M5 slice 2 (Appendix A): type_cast_path is object, not the declared /atom: tg's caller passes the tile's turf_type, a
    // typepath (tile_types.dm:46), which initial() reads (rules 10.3).
    public virtual object tile_dir_list(DmList values = null, object type_cast_path = null)
    {
        object dot = null;
        dot = GLOB.tile_dir_lists[type];
        if (Dm.truthy(dot))
            return dot;
        foreach (object set_dir in Dm.each(values.Copy()))
        {
            image turf_image = Dm.image(icon: Dm.initial(type_cast_path, (atom a) => a.icon), icon_state: Dm.initial(type_cast_path, (atom a) => a.icon_state), dir: text2dir(set_dir));
            turf_image.transform = ((matrix)turf_image.transform).Scale(0.5f, 0.5f);
            values[set_dir] = turf_image;
        }
        return GLOB.tile_dir_lists[type] = values;
    }

    // tg: code/game/objects/items/stacks/tiles/tile_reskinning.dm:38
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        DmList radial_options = Dm.list();
        if (Dm.truthy(tile_reskin_types) && Dm.truthy(tile_rotate_dirs))
        {
            radial_options["Reskin"] = Dm.image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_reskin");
            radial_options["Rotate"] = Dm.image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_rotate");
            object radial_result = await show_radial_menu(user, this, radial_options, require_near: TRUE, tooltips: TRUE);
            object sw__1 = radial_result;
            if (Dm.eq(sw__1, "Reskin"))
            {
                await tile_reskin(user);
                return null;
            }
            else if (Dm.eq(sw__1, "Rotate"))
            {
                await tile_rotate(user);
                return null;
            }
            return null;
        }
        if (Dm.truthy(tile_reskin_types))
        {
            await tile_reskin(user);
            return null;
        }
        if (Dm.truthy(tile_rotate_dirs))
        {
            await tile_rotate(user);
            return null;
        }
        return await base.attack_self(user, modifiers, __extra);
    }

    // tg: code/game/objects/items/stacks/tiles/tile_reskinning.dm:55
    public virtual async DmTask tile_reskin(mob user = null)
    {
        object choice = await show_radial_menu(user, this, tile_reskin_types, radius: 48, require_near: TRUE);
        if (!Dm.truthy(choice) || Dm.eq(choice, type))
            return;
        choice = Dm.@new((Type)choice, user.drop_location(), amount);
        await moveToNullspace();
        if (!QDELETED(choice)) // Tile could have merged with stuff on the ground. The user will have to pick it up if so.
            await user.put_in_active_hand(choice as obj_item);
        qdel(this);
    }

    // tg: code/game/objects/items/stacks/tiles/tile_reskinning.dm:65
    public virtual async DmTask tile_rotate(mob user = null)
    {
        object choice = await show_radial_menu(user, this, tile_rotate_dirs, radius: 56, require_near: TRUE);
        if (!Dm.truthy(choice))
            return;
        turf_dir = Dm.num(text2dir(choice));
    }
}
