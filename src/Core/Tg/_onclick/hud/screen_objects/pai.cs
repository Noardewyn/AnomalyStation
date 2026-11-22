// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/screen_objects/pai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI6, D326): the pAI's HUD buttons. The var blocks are generated (D111).

// tg: code/_onclick/hud/screen_objects/pai.dm:3
public partial class atom_movable_screen_pai : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/pai.dm:1
    public static string PAI_MISSING_SOFTWARE_MESSAGE => span_warning(I18n.T("You must download the required software to use this."));

    // tg: code/_onclick/hud/screen_objects/pai.dm:8
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        await DmTask.CompletedTask;
        if (isobserver(usr) || Dm.truthy(usr.incapacitated))
            return FALSE;
        var user = usr as mob_living_silicon_pai;
        if (Dm.truthy(required_software) && !Dm.truthy(user.installed_software.Find(required_software)))
        {
            to_chat(user, PAI_MISSING_SOFTWARE_MESSAGE);
            return FALSE;
        }
        return TRUE;
    }
}

// tg: code/_onclick/hud/screen_objects/pai.dm:17
public partial class atom_movable_screen_pai_software : atom_movable_screen_pai
{
    // tg: code/_onclick/hud/screen_objects/pai.dm:22
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (!Dm.truthy(await base.Click(location, control, @params)))
            return null;
        var pAI = (mob_living_silicon_pai)usr;
        await pAI.ui_interact(pAI);
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/pai.dm:28
public partial class atom_movable_screen_pai_shell : atom_movable_screen_pai
{
    // tg: code/_onclick/hud/screen_objects/pai.dm:33
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (!Dm.truthy(await base.Click(location, control, @params)))
            return null;
        var pAI = (mob_living_silicon_pai)usr;
        if (Dm.truthy(pAI.holoform))
            await pAI.fold_in(0);
        else
            await pAI.fold_out();
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/pai.dm:42
public partial class atom_movable_screen_pai_chassis : atom_movable_screen_pai
{
    // tg: code/_onclick/hud/screen_objects/pai.dm:47
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (!Dm.truthy(await base.Click(location, control, @params)))
            return null;
        var pAI = (mob_living_silicon_pai)usr;
        await pAI.choose_chassis();
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/pai.dm:53
public partial class atom_movable_screen_pai_rest : atom_movable_screen_pai
{
    // tg: code/_onclick/hud/screen_objects/pai.dm:58
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (!Dm.truthy(await base.Click(location, control, @params)))
            return null;
        var pAI = (mob_living_silicon_pai)usr;
        await pAI.toggle_resting();
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/pai.dm:64
public partial class atom_movable_screen_pai_light : atom_movable_screen_pai
{
    // tg: code/_onclick/hud/screen_objects/pai.dm:69
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (!Dm.truthy(await base.Click(location, control, @params)))
            return null;
        var pAI = (mob_living_silicon_pai)usr;
        pAI.toggle_integrated_light();
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/pai.dm:75
public partial class atom_movable_screen_pai_newscaster : atom_movable_screen_pai
{
    // tg: code/_onclick/hud/screen_objects/pai.dm:81
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (!Dm.truthy(await base.Click(location, control, @params)))
            return null;
        var pAI = (mob_living_silicon_pai)usr;
        await pAI.newscaster.ui_interact(usr);
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/pai.dm:87
public partial class atom_movable_screen_pai_host_monitor : atom_movable_screen_pai
{
    // tg: code/_onclick/hud/screen_objects/pai.dm:93
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (!Dm.truthy(dot))
            return dot;
        var pAI = (mob_living_silicon_pai)usr;
        DmList modifiers = params2list(@params);
        if (Dm.truthy(LAZYACCESS(modifiers, LEFT_CLICK)))
        {
            await pAI.host_scan(PAI_SCAN_TARGET);
            return TRUE;
        }
        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
        {
            await pAI.host_scan(PAI_SCAN_MASTER);
            return TRUE;
        }
        return dot;
    }
}

// tg: code/_onclick/hud/screen_objects/pai.dm:106
public partial class atom_movable_screen_pai_crew_manifest : atom_movable_screen_pai
{
    // tg: code/_onclick/hud/screen_objects/pai.dm:112
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (!Dm.truthy(await base.Click(location, control, @params)))
            return null;
        var pAI = (mob_living_silicon_pai)usr;
        await pAI.ai_roster();
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/pai.dm:118
public partial class atom_movable_screen_pai_state_laws : atom_movable_screen_pai
{
    // tg: code/_onclick/hud/screen_objects/pai.dm:123
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (!Dm.truthy(await base.Click(location, control, @params)))
            return null;
        var pAI = (mob_living_silicon_pai)usr;
        await pAI.checklaws();
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/pai.dm:129
public partial class atom_movable_screen_pai_modpc : atom_movable_screen_pai
{
    // tg: code/_onclick/hud/screen_objects/pai.dm:135
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (!Dm.truthy(dot)) // this works for some reason.
            return dot;
        if (usr is mob_living_silicon_pai pAI && pAI.modularInterface != null)
            await pAI.modularInterface.interact(pAI);
        return dot;
    }
}

// tg: code/_onclick/hud/screen_objects/pai.dm:143
public partial class atom_movable_screen_pai_internal_gps : atom_movable_screen_pai
{
    // tg: code/_onclick/hud/screen_objects/pai.dm:149
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (!Dm.truthy(dot))
            return dot;
        var pAI = (mob_living_silicon_pai)usr;
        pAI.internal_gps ??= Dm.@new<obj_item_gps_pai>(pAI);
        await pAI.internal_gps.attack_self(pAI);
        return dot;
    }
}

// tg: code/_onclick/hud/screen_objects/pai.dm:158
public partial class atom_movable_screen_pai_image_take : atom_movable_screen_pai
{
    // tg: code/_onclick/hud/screen_objects/pai.dm:164
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (!Dm.truthy(dot))
            return dot;
        var pAI = (mob_living_silicon_pai)usr;
        pAI.aicamera.toggle_camera_mode(usr);
        return dot;
    }
}

// tg: code/_onclick/hud/screen_objects/pai.dm:171
public partial class atom_movable_screen_pai_image_view : atom_movable_screen_pai
{
    // tg: code/_onclick/hud/screen_objects/pai.dm:177
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (!Dm.truthy(await base.Click(location, control, @params)))
            return null;
        var pAI = (mob_living_silicon_pai)usr;
        await pAI.aicamera.viewpictures(usr);
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/pai.dm:183
public partial class atom_movable_screen_pai_radio : atom_movable_screen_pai
{
    // tg: code/_onclick/hud/screen_objects/pai.dm:189
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (!Dm.truthy(await base.Click(location, control, @params)))
            return null;
        var pAI = (mob_living_silicon_pai)usr;
        await ((obj_item_radio)pAI.radio).interact(usr);
        return null;
    }
}
