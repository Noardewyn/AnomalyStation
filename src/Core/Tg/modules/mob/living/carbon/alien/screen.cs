// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/screen.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file, the queen sense of the alien HUD.

// tg: code/modules/mob/living/carbon/alien/screen.dm:2
public partial class mob_living_carbon_alien : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/alien/screen.dm:2
    public virtual void findQueen()
    {
        if (hud_used == null)
            return;
        atom_movable_screen_alien_alien_queen_finder finder = hud_used.screen_objects[HUD_ALIEN_QUEEN_FINDER] as atom_movable_screen_alien_alien_queen_finder;
        if (finder == null)
            return;
        finder.cut_overlays();
        mob queen = get_alien_type(typeof(mob_living_carbon_alien_adult_royal_queen)) as mob;
        if (queen == null)
            return;
        turf Q = get_turf(queen);
        turf A = get_turf(this);
        if (Q.z != A.z) //The queen is on a different Z level, we cannot sense that far.
            return;
        object Qdir = get_dir(this, Q);
        float Qdist = Dm.num(get_dist(this, Q));
        string finder_icon = "finder_center"; //Overlay showed when adjacent to or on top of the queen!
        if (Qdist >= 2 && Qdist <= 7)
            finder_icon = "finder_near";
        else if (Qdist >= 8 && Qdist <= 20)
            finder_icon = "finder_med";
        else if (Qdist >= 21 && Qdist <= INFINITY)
            finder_icon = "finder_far";
        image finder_eye = Dm.image(Dm.resource("icons/hud/screen_alien.dmi"), icon_state: finder_icon, dir: Qdir);
        finder.add_overlay(finder_eye);
    }
}

// tg: code/modules/mob/living/carbon/alien/screen.dm:29
public partial class mob_living_carbon_alien_adult_royal_queen : mob_living_carbon_alien_adult_royal
{
    // tg: code/modules/mob/living/carbon/alien/screen.dm:29
    public override void findQueen()
    {
        return; //Queen already knows where she is. Hopefully.
    }
}
