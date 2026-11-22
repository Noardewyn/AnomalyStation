// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/lighting/light_wallframes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 lighting slice L1 (D168): the whole file. The tube and bulb fixture frames are mounted on a wall (or on a window) from a
// hand, the small one is also attached to the floor (the floor bulb frame). The var blocks are generated.

// tg: code/modules/power/lighting/light_wallframes.dm:1
public partial class obj_item_wallframe_light_fixture : obj_item_wallframe
{
    // tg: code/modules/power/lighting/light_wallframes.dm:9
    public override object find_support_structure(atom structure = null)
    {
        return istype(structure, typeof(obj_structure_window)) ? structure : base.find_support_structure(structure);
    }

    // tg: code/modules/power/lighting/light_wallframes.dm:12
    // try_build is defined twice at this type (:12 and :25): the earlier one is try_build__d0 and the later one calls it
    // through `..()` (rules 2.5).
    public virtual async DmTask<object> try_build__d0(atom support = null, mob user = null)
    {
        area A = get_area(user) as area;
        if (Dm.truthy(A.always_unpowered))
        {
            balloon_alert(user, I18n.T("cannot place in this area!"));
            return FALSE;
        }
        return await base.try_build(support, user);
    }

    // tg: code/modules/power/lighting/light_wallframes.dm:25  (`turf/on_wall` is only a type annotation of the parameter)
    public override async DmTask<object> try_build(atom on_wall = null, mob user = null)
    {
        if (!Dm.truthy(await try_build__d0(on_wall, user)))
            return null;
        area local_area = get_area(user) as area;
        if (!Dm.truthy(local_area.static_lighting))
        {
            to_chat(user, span_warning(I18n.T("You cannot place [] in this area!", this)));
            return null;
        }
        return TRUE;
    }
}

// tg: code/modules/power/lighting/light_wallframes.dm:19
public partial class obj_item_wallframe_light_fixture_small : obj_item_wallframe_light_fixture
{
    // tg: code/modules/power/lighting/light_wallframes.dm:34
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        turf local_turf = get_turf(user) as turf;
        area local_area = get_area(user) as area;
        if (!isturf(user.loc) || !isfloorturf(local_turf))
        {
            balloon_alert(user, I18n.T("cannot place here!"));
            return null;
        }
        if (Dm.truthy(local_area.always_unpowered) || !Dm.truthy(local_area.static_lighting))
        {
            balloon_alert(user, I18n.T("cannot place in this area!"));
            return null;
        }
        foreach (obj @object in Dm.each<obj>(local_turf))
        {
            if ((Dm.truthy(@object.density) && !Dm.truthy(Dm.bitand(@object.obj_flags, IGNORE_DENSITY))) || Dm.truthy(Dm.bitand(@object.obj_flags, BLOCKS_CONSTRUCTION)))
            {
                balloon_alert(user, I18n.T("something is in the way!"));
                return null;
            }
        }
        if (local_turf.underfloor_accessibility < UNDERFLOOR_INTERACTABLE)
        {
            balloon_alert(user, I18n.T("remove the floor plating!"));
            return null;
        }
        if (Dm.locate<obj_structure_light_construct_floor>(local_turf) != null)
        {
            balloon_alert(user, I18n.T("already has a light!"));
            return null;
        }
        if (Dm.locate<obj_machinery_light_floor>(local_turf) != null)
        {
            balloon_alert(user, I18n.T("already has a light!"));
            return null;
        }

        playsound(loc, Dm.resource("sound/machines/click.ogg"), 75, TRUE); // tg: code/modules/power/lighting/light_wallframes.dm:57
        user.visible_message(span_notice(I18n.T("[] attaches [] to the floor.", user.name, this)),
            span_notice(I18n.T("You attach [] to the floor.", this)),
            span_hear(I18n.T("You hear clicking.")));

        Dm.@new<obj_structure_light_construct_floor>(local_turf);
        qdel(this);
        return null;
    }
}
