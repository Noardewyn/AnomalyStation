// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/screen_objects/human.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P2, D126: the human's inventory toggle (the button that shows the toggleable slots). Gaps K1 (D327): the changeling's chemical
// display (its hover shows the chemical storage's cap) and the current sting's button (a click retracts the sting); the whole file.

// tg: code/_onclick/hud/screen_objects/human.dm:4
public partial class atom_movable_screen_human_toggle : atom_movable_screen_human
{
    // tg: code/_onclick/hud/screen_objects/human.dm:11
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        mob targetmob = usr;

        if (isobserver(usr))
        {
            if (ishuman(usr.client.eye) && (usr.client.eye != usr))
            {
                mob M = usr.client.eye as mob;
                targetmob = M;
            }
        }

        if (Dm.truthy(usr.hud_used.inventory_shown) && Dm.truthy(targetmob.hud_used))
        {
            usr.hud_used.inventory_shown = FALSE;
            Dm.remove(usr.client.screen, targetmob.hud_used.screen_groups[HUD_GROUP_TOGGLEABLE_INVENTORY]);
        }
        else
        {
            usr.hud_used.inventory_shown = TRUE;
            Dm.append(usr.client.screen, targetmob.hud_used.screen_groups[HUD_GROUP_TOGGLEABLE_INVENTORY]);
        }

        update_appearance();
        return null;
    }

    // tg: code/_onclick/hud/screen_objects/human.dm:28
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(hud?.inventory_shown) ? "_active" : "");
        return await base.update_icon_state(__extra);
    }
}

// tg: code/_onclick/hud/screen_objects/human.dm:35
public partial class atom_movable_screen_ling_chems : atom_movable_screen_ling
{
    // tg: code/_onclick/hud/screen_objects/human.dm:42
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = null;
        dot = await base.Click(location, control, @params);
        to_chat(usr, span_notice(I18n.T("Shows you how many chemicals you have. While hovering over this, it will show the max amount of chemicals you can hold.")));
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/human.dm:46
    public override async DmTask MouseEntered(object location = null, object control = null, object @params = null)
    {
        if (!Dm.eq(usr, get_mob()))
            return;
        datum_antagonist_changeling antagonist_datum = IS_CHANGELING(hud.mymob) as datum_antagonist_changeling;
        if (antagonist_datum == null)
            return;
        await base.MouseEntered(location, control, @params);
        hovering = TRUE;
        antagonist_datum.update_chemical_hud();
    }

    // tg: code/_onclick/hud/screen_objects/human.dm:56
    public override void MouseExited(object location = null, object control = null, object @params = null)
    {
        if (!Dm.eq(usr, get_mob()))
            return;
        datum_antagonist_changeling antagonist_datum = IS_CHANGELING(hud.mymob) as datum_antagonist_changeling;
        if (antagonist_datum == null)
            return;
        base.MouseExited(location, control, @params);
        hovering = FALSE;
        antagonist_datum.update_chemical_hud(antagonist_datum.chem_charges);
    }
}

// tg: code/_onclick/hud/screen_objects/human.dm:66
public partial class atom_movable_screen_ling_sting : atom_movable_screen_ling
{
    // tg: code/_onclick/hud/screen_objects/human.dm:72
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (isobserver(usr))
            return null;
        mob_living_carbon carbon_user = usr as mob_living_carbon;
        carbon_user.unset_sting();
        await DmTask.CompletedTask;
        return null;
    }
}
