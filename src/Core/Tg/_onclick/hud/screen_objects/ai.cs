// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/screen_objects/ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2 and SI4, D326): the AI's HUD buttons (the Take Image one the cyborg's HUD shares). The var blocks are
// generated (D111).

// tg: code/_onclick/hud/screen_objects/ai.dm:1
public partial class atom_movable_screen_ai : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/ai.dm:5
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (isobserver(usr) || Dm.truthy(usr.incapacitated))
            return TRUE;
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/ai.dm:131
public partial class atom_movable_screen_ai_image_take : atom_movable_screen_ai
{
    // tg: code/_onclick/hud/screen_objects/ai.dm:136
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(await base.Click(location, control, @params)))
            return null;
        if (isAI(usr))
        {
            mob_living_silicon_ai AI = usr as mob_living_silicon_ai;
            AI.aicamera.toggle_camera_mode(usr);
        }
        else if (iscyborg(usr))
        {
            mob_living_silicon_robot R = usr as mob_living_silicon_robot;
            R.aicamera.toggle_camera_mode(usr);
        }
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/ai.dm:9
public partial class atom_movable_screen_ai_aicore : atom_movable_screen_ai
{
    // tg: code/_onclick/hud/screen_objects/ai.dm:14
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (!isAI(usr))
            return null;

        mob_living_silicon_ai AI = usr as mob_living_silicon_ai;
        await AI.view_core();
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/ai.dm:21
public partial class atom_movable_screen_ai_camera_list : atom_movable_screen_ai
{
    // tg: code/_onclick/hud/screen_objects/ai.dm:26
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(await base.Click(location, control, @params)))
            return null;
        mob_living_silicon_ai AI = usr as mob_living_silicon_ai;
        await AI.show_camera_list();
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/ai.dm:32
public partial class atom_movable_screen_ai_camera_track : atom_movable_screen_ai
{
    // tg: code/_onclick/hud/screen_objects/ai.dm:37
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(await base.Click(location, control, @params)))
            return null;
        mob_living_silicon_ai AI = usr as mob_living_silicon_ai;
        await AI.__gvb_ai_camera_track(Dm.list()); // AI.ai_camera_track(): the verb called as a proc runs its body
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/ai.dm:43
public partial class atom_movable_screen_ai_camera_light : atom_movable_screen_ai
{
    // tg: code/_onclick/hud/screen_objects/ai.dm:48
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(await base.Click(location, control, @params)))
            return null;
        mob_living_silicon_ai AI = usr as mob_living_silicon_ai;
        AI.toggle_camera_light();
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/ai.dm:54
public partial class atom_movable_screen_ai_modpc : atom_movable_screen_ai
{
    // tg: code/_onclick/hud/screen_objects/ai.dm:59
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(await base.Click(location, control, @params)))
            return null;
        mob_living_silicon_ai AI = usr as mob_living_silicon_ai;
        if (AI.modularInterface != null)
            await AI.modularInterface.interact(AI);
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/ai.dm:65
public partial class atom_movable_screen_ai_crew_monitor : atom_movable_screen_ai
{
    // tg: code/_onclick/hud/screen_objects/ai.dm:70
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(await base.Click(location, control, @params)))
            return null;
        mob_living_silicon_ai AI = usr as mob_living_silicon_ai;
        await GLOB.crewmonitor.show(AI, AI);
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/ai.dm:76
public partial class atom_movable_screen_ai_crew_manifest : atom_movable_screen_ai
{
    // tg: code/_onclick/hud/screen_objects/ai.dm:81
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(await base.Click(location, control, @params)))
            return null;
        mob_living_silicon_ai AI = usr as mob_living_silicon_ai;
        await AI.ai_roster();
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/ai.dm:87
public partial class atom_movable_screen_ai_alerts : atom_movable_screen_ai
{
    // tg: code/_onclick/hud/screen_objects/ai.dm:92
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(await base.Click(location, control, @params)))
            return null;
        mob_living_silicon_ai AI = usr as mob_living_silicon_ai;
        await AI.alert_control.ui_interact(AI);
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/ai.dm:98
public partial class atom_movable_screen_ai_announcement : atom_movable_screen_ai
{
    // tg: code/_onclick/hud/screen_objects/ai.dm:103
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(await base.Click(location, control, @params)))
            return null;
        mob_living_silicon_ai AI = usr as mob_living_silicon_ai;
        await AI.announcement();
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/ai.dm:109
public partial class atom_movable_screen_ai_call_shuttle : atom_movable_screen_ai
{
    // tg: code/_onclick/hud/screen_objects/ai.dm:114
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(await base.Click(location, control, @params)))
            return null;
        mob_living_silicon_ai AI = usr as mob_living_silicon_ai;
        await AI.ai_call_shuttle();
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/ai.dm:120
public partial class atom_movable_screen_ai_state_laws : atom_movable_screen_ai
{
    // tg: code/_onclick/hud/screen_objects/ai.dm:125
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(await base.Click(location, control, @params)))
            return null;
        mob_living_silicon_ai AI = usr as mob_living_silicon_ai;
        await AI.checklaws();
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/ai.dm:145
public partial class atom_movable_screen_ai_image_view : atom_movable_screen_ai
{
    // tg: code/_onclick/hud/screen_objects/ai.dm:150
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(await base.Click(location, control, @params)))
            return null;
        if (isAI(usr))
        {
            mob_living_silicon_ai AI = usr as mob_living_silicon_ai;
            await AI.aicamera.viewpictures(usr);
        }
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/ai.dm:157
public partial class atom_movable_screen_ai_sensors : atom_movable_screen_ai
{
    // tg: code/_onclick/hud/screen_objects/ai.dm:162
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(await base.Click(location, control, @params)))
            return null;
        mob_living_silicon S = usr as mob_living_silicon;
        S.toggle_sensors();
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/ai.dm:168
public partial class atom_movable_screen_ai_multicam : atom_movable_screen_ai
{
    // tg: code/_onclick/hud/screen_objects/ai.dm:173
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(await base.Click(location, control, @params)))
            return null;
        mob_living_silicon_ai AI = usr as mob_living_silicon_ai;
        AI.toggle_multicam();
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/ai.dm:179
public partial class atom_movable_screen_ai_add_multicam : atom_movable_screen_ai
{
    // tg: code/_onclick/hud/screen_objects/ai.dm:184
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(await base.Click(location, control, @params)))
            return null;
        mob_living_silicon_ai AI = usr as mob_living_silicon_ai;
        AI.drop_new_multicam();
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/ai.dm:190
public partial class atom_movable_screen_ai_floor_indicator : atom_movable_screen_ai
{
    // tg: code/_onclick/hud/screen_objects/ai.dm:194
    [DmParams("mapload", "hud_owner")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_hud hud_owner = Dm.arg<datum_hud>(__extra, 0);
        if (hud_owner != null)
        {
            RegisterSignal(hud_owner, COMSIG_HUD_Z_CHANGED, nameof(update_z));
            update_z();
        }
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/ai.dm:200
    public virtual void update_z(datum_hud source = null)
    {
        mob_living_silicon_ai ai = get_mob() as mob_living_silicon_ai; //if you use this for anyone else i will find you
        if (ai == null)
            return;
        turf locturf = isturf(ai.loc) ? get_turf(ai.eyeobj) : get_turf(ai); //must be a var cuz error
        float ai_z = locturf.z;
        string text = Dm.text("Level<br/>[]", ai_z);
        if (Dm.truthy(SSmapping.level_trait(ai_z, ZTRAIT_STATION)))
            text = Dm.text("Floor<br/>[]", ai_z - 1);
        else if (Dm.truthy(SSmapping.level_trait(ai_z, ZTRAIT_NOPHASE)))
            text = "ERROR";
        maptext = MAPTEXT_TINY_UNICODE(Dm.text("<div align='center' valign='middle' style='position:relative; top:0px; left:0px'>[]</div>", text));
    }
}

// tg: code/_onclick/hud/screen_objects/ai.dm:214
public partial class atom_movable_screen_ai_go_up : atom_movable_screen_ai
{
    // tg: code/_onclick/hud/screen_objects/ai.dm:219
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/ai.dm:223
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        base.add_context(source, context, held_item, user);
        context[SCREENTIP_CONTEXT_LMB] = "Go up a floor";
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/_onclick/hud/screen_objects/ai.dm:228
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        mob ai = get_mob() as mob; //the core
        flick("uppressed", this);
        if (!isturf(ai.loc) || usr != ai) //aicard and stuff
            return null;
        await ai.up();
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/ai.dm:235
public partial class atom_movable_screen_ai_go_up_down : atom_movable_screen_ai_go_up
{
    // tg: code/_onclick/hud/screen_objects/ai.dm:239
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        base.add_context(source, context, held_item, user);
        context[SCREENTIP_CONTEXT_LMB] = "Go down a floor";
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/_onclick/hud/screen_objects/ai.dm:244
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        mob ai = get_mob() as mob; //the core
        flick("downpressed", this);
        if (!isturf(ai.loc) || usr != ai) //aicard and stuff
            return null;
        await ai.down();
        return null;
    }
}

