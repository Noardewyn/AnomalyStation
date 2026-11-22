using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using System.Linq;

namespace Anomaly.Core.Tg;

// DEVIATION(DV361): first person draws a mech's arm equipment where it draws a mob's held things. tg's pilot sees the mech from above and has no
// hands to show; here the pilot's client reads two screen objects (not drawn: no screen_loc) whose icon state names the arm and whose
// vis_contents holds the equipment on it, as it reads a hand slot's item (HeldHands.Find). Left arm: left mouse button; right arm: right button
// (on_mouseclick above).
public partial class obj_vehicle_sealed_mecha
{
    /// <summary>Icon state of the left arm's marker screen object.</summary>
    public const string ARM_VIEW_LEFT = "mecha_arm_l";
    /// <summary>Icon state of the right arm's marker screen object.</summary>
    public const string ARM_VIEW_RIGHT = "mecha_arm_r";

    private readonly Dictionary<mob, atom_movable_screen[]> arm_views = new();

    /// Brings the arm views of every occupant who controls the equipment to the equipment now on the arms, and drops those of the others.
    public void refresh_arm_views()
    {
        foreach (mob gone in arm_views.Keys.ToList())
            if (!Dm.@in(gone, occupants) || !Dm.truthy(Dm.bitand(occupants[gone], VEHICLE_CONTROL_EQUIPMENT)))
                remove_arm_views(gone);
        foreach (mob occupant in Dm.each<mob>(occupants))
        {
            if (occupant.client == null || !Dm.truthy(Dm.bitand(occupants[occupant], VEHICLE_CONTROL_EQUIPMENT)))
                continue;
            if (!arm_views.TryGetValue(occupant, out atom_movable_screen[] views))
            {
                views = new atom_movable_screen[2];
                for (int i = 0; i < 2; i++)
                {
                    views[i] = Dm.@new<atom_movable_screen>();
                    views[i].icon_state = i == 0 ? ARM_VIEW_LEFT : ARM_VIEW_RIGHT;
                    occupant.client.screen.Add(views[i]);
                }
                arm_views[occupant] = views;
            }
            for (int i = 0; i < 2; i++)
            {
                views[i].vis_contents.Cut();
                if (equip_by_category[i == 0 ? MECHA_L_ARM : MECHA_R_ARM] is obj_item_mecha_parts_mecha_equipment arm)
                    Dm.append(views[i].vis_contents, arm);
            }
        }
    }

    /// Takes the arm views from one pilot (gone from the mech, or no longer at its equipment).
    public void remove_arm_views(mob pilot)
    {
        if (!arm_views.Remove(pilot, out atom_movable_screen[] views))
            return;
        foreach (atom_movable_screen view in views)
        {
            pilot.client?.screen.Remove(view);
            qdel(view);
        }
    }
}
