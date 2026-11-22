// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/screen_objects/radial_persistent.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 power helpers slice (D164): the whole file but the centre button's screen type (its Click and hover are the client's, as the
// plain radial's: DV40). The persistent menu is the plain radial of radial.cs that stays on screen and calls a callback for each
// pick (the rapid pipe cleaner layer's wiring menu). DEVIATION(DV79): the client closes its drawing at a pick as for any menu, so the server
// sends the menu again after each pick (ClientSeam.RadialPick) and the menu appears to stay open.

/*
	A derivative of radial menu which persists onscreen until closed and invokes a callback each time an element is clicked
*/

// tg: code/_onclick/hud/screen_objects/radial_persistent.dm:23
public partial class datum_radial_menu_persistent : datum_radial_menu
{
    // tg: code/_onclick/hud/screen_objects/radial_persistent.dm:27  (no ..(): the plain New would take the close button argument)
    public override async DmTask New(params object[] __extra)
    {
        // close_button = new /atom/movable/screen/radial/persistent/center; close_button.set_parent(src)
        // DEVIATION (DV40): the centre button is the client's; its pictures (radial.dm:62, :66) are kept for it.
        dm_close_button = dm_radial_picture("radial_center");
        dm_close_button_focus = dm_radial_picture("radial_center_focus");
    }

    // tg: code/_onclick/hud/screen_objects/radial_persistent.dm:32  (element_chosen(choice_id, mob/user, params))
    [DmParams("params")]
    public override async DmTask element_chosen(object choice_id = null, mob user = null, params object[] __extra)
    {
        object @params = Dm.arg(__extra, 0);
        await select_proc_callback.Invoke(choices_values[choice_id], @params);
    }

    ///Version of wait used by persistent radial menus.
    // tg: code/_onclick/hud/screen_objects/radial_persistent.dm:36
    public override async DmTask wait(atom user = null, atom anchor = null, float? require_near = null)
    {
        while (!QDELETED(this))
        {
            if (Dm.truthy(custom_check_callback) && next_check < world.time)
            {
                await custom_check_callback.Invoke();
                next_check = world.time + check_delay;
            }
            await stoplag(1);
        }
    }

    // tg: code/_onclick/hud/screen_objects/radial_persistent.dm:43
    public virtual void change_choices(DmList newchoices = null, float? tooltips = null, float? animate = null, float? keep_same_page = null)
    {
        tooltips ??= FALSE;
        animate ??= FALSE;
        keep_same_page ??= FALSE;
        if (!Dm.truthy(newchoices.len))
            return;
        button_animation_flags = NONE;
        float target_page = Dm.truthy(keep_same_page) ? current_page : 1; //Stores the current_page value before it's set back to 1 on Reset()
        Reset();
        set_choices(newchoices, tooltips, set_page: target_page);
    }

    // tg: code/_onclick/hud/screen_objects/radial_persistent.dm:51
    public override object Destroy(float? force = null)
    {
        select_proc_callback = null;
        Dm.remove(ref GLOB.radial_menus, uniqueid);
        Reset();
        hide();
        return base.Destroy(force);
    }
}

public static partial class GlobalProcs
{
    /*
        Creates a persistent radial menu and shows it to the user, anchored to anchor (or user if the anchor is currently in users screen).
        Choices should be a list where list keys are movables or text used for element names and return value
        and list values are movables/icons/images used for element icons
        Select_proc is the proc to be called each time an element on the menu is clicked, and should accept the chosen element as its final argument
        Clicking the center button will return a choice of null
    */
    // tg: code/_onclick/hud/screen_objects/radial_persistent.dm:65
    public static datum_radial_menu_persistent show_radial_menu_persistent(mob user = null, atom anchor = null, DmList choices = null, datum_callback select_proc = null, object uniqueid = null, object radius = null, float? tooltips = null, string radial_slice_icon = null, datum_callback custom_check = null)
    {
        tooltips ??= FALSE;
        radial_slice_icon ??= "radial_slice";
        if (!Dm.truthy(user) || !Dm.truthy(anchor) || !Dm.truthy(Dm.length(choices)) || !Dm.truthy(select_proc))
            return null;
        if (!Dm.truthy(uniqueid))
            uniqueid = Dm.text("defmenu_[]_[]", REF(user), REF(anchor));

        if (Dm.truthy(GLOB.radial_menus[uniqueid]))
            return null;

        datum_radial_menu_persistent menu = Dm.@new<datum_radial_menu_persistent>();
        menu.uniqueid = uniqueid;
        GLOB.radial_menus[uniqueid] = menu;
        if (Dm.truthy(radius))
            menu.radius = Dm.num(radius);
        menu.radial_slice_icon = radial_slice_icon;
        menu.select_proc_callback = select_proc;
        if (istype(custom_check, typeof(datum_callback)))
            menu.custom_check_callback = custom_check;
        menu.anchor = anchor;
        menu.check_screen_border(user); //Do what's needed to make it look good near borders or on hud
        menu.set_choices(choices, tooltips);
        menu.show_to(user);
        if (Dm.truthy(menu.custom_check_callback))
        {
            menu.next_check = world.time + menu.check_delay;
            Waiting.Detach(menu.wait()); // INVOKE_ASYNC(menu, TYPE_PROC_REF(/datum/radial_menu, wait))
        }
        return menu;
    }
}
