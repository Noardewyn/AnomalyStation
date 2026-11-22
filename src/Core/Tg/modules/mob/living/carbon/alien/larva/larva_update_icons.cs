// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/larva/larva_update_icons.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file, the larva's icon states (larvaN, _dead, _cuff, _stun, _sleep by its growth).

// tg: code/modules/mob/living/carbon/alien/larva/larva_update_icons.dm:2
public partial class mob_living_carbon_alien_larva : mob_living_carbon_alien
{
    // tg: code/modules/mob/living/carbon/alien/larva/larva_update_icons.dm:2
    public override async DmTask<object> regenerate_icons()
    {
        cut_overlays();
        await update_icons();
        return null;
    }

    // tg: code/modules/mob/living/carbon/alien/larva/larva_update_icons.dm:6
    public override async DmTask update_icons()
    {
        float state = 0;
        if (amount_grown > 80)
            state = 2;
        else if (amount_grown > 50)
            state = 1;

        if (stat == DEAD)
            icon_state = Dm.text("larva[]_dead", state);
        else if (Dm.truthy(handcuffed) || Dm.truthy(legcuffed)) //This should be an overlay. Who made this an icon_state?
            icon_state = Dm.text("larva[]_cuff", state);
        else if (HAS_TRAIT(this, TRAIT_INCAPACITATED))
            icon_state = Dm.text("larva[]_stun", state);
        else if (body_position == LYING_DOWN)
            icon_state = Dm.text("larva[]_sleep", state);
        else
            icon_state = Dm.text("larva[]", state);
    }

    // tg: code/modules/mob/living/carbon/alien/larva/larva_update_icons.dm:24
    public override async DmTask<object> update_transform(float? resize = null) //All this is handled in update_icons()
    {
        object dot = await base.update_transform(resize);
        await update_icons();
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/larva/larva_update_icons.dm:28
    public override void update_worn_handcuffs()
    {
        Waiting.NoSleep(update_icons()); //larva icon_state changes if cuffed/uncuffed.
    }

    // tg: code/modules/mob/living/carbon/alien/larva/larva_update_icons.dm:32
    public override async DmTask lying_angle_on_lying_down(object new_lying_angle = null)
    {
        return; // Larvas don't rotate on lying down, they have their own custom icons.
    }
}
