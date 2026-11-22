// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/screen_objects/alert.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M2 P3 (D107): the alert procs, the base alert's procs on the M2 paths and the gas alerts that the lungs and the carbon's
// breath throw (_lungs.dm, carbon/life.dm), the succumb alert, and the HUD's placement; the buckled alert's click (M8y slice A); the alert's tooltips (MouseEntered,
// MouseExited, protocol version 19); the base alert's Click and the embedded object's alert (M6 slice 3b). Not translated
// here: the succumb prompt (tgui_input_text: M9); examine is X3's; the other alert types (gravity, restraints, status
// effects, antagonists, ...) come with their milestones. The give alert and its subtypes (high-five, hand) since M9 slice F; the
// ghosts' poll alert since the syndicate campaign X2; the ghosts' notify alert and the cell alerts (the cyborgs' and mechs' low and empty charge) since silicons SI2.

//A system to manage and display alerts on screen without needing you to do it yourself

//PUBLIC -  call these wherever you want

// tg: code/_onclick/hud/screen_objects/alert.dm:15
public partial class mob : atom_movable
{
    /**
     *Proc to create or update an alert. Returns the alert if the alert is new or updated, 0 if it was thrown already
     *category is a text string. Each mob may only have one alert per category; the previous one will be replaced
     *path is a type path of the actual alert type to throw
     *severity is an optional number that will be placed at the end of the icon_state for this alert
     *for example, high pressure's icon_state is "highpressure" and can be serverity 1 or 2 to get "highpressure1" or "highpressure2"
     *new_master is optional and sets the alert's icon state to "template" in the ui_style icons with the master as an overlay.
     *flicks are forwarded to master
     *override makes it so the alert is not replaced until cleared by a clear_alert with clear_override, and it's used for hallucinations.
     */
    // tg: code/_onclick/hud/screen_objects/alert.dm:15
    public virtual object throw_alert(object category = null, object type = null, object severity = null, atom new_master = null, float? @override = null, object timeout_override = null, float? no_anim = null)
    {
        @override ??= FALSE;
        no_anim ??= FALSE;

        if (!Dm.truthy(category) || QDELETED(this))
            return null;

        datum_weakref master_ref = null;
        if (isatom(new_master))
            master_ref = WEAKREF(new_master) as datum_weakref;
        atom_movable_screen_alert thealert = null;
        if (Dm.truthy(alerts[category]))
        {
            thealert = alerts[category] as atom_movable_screen_alert;
            if (Dm.truthy(thealert.override_alerts))
                return thealert;
            if (Dm.truthy(master_ref) && Dm.truthy(thealert.master_ref) && master_ref != thealert.master_ref)
            {
                datum current_master = thealert.master_ref.resolve() as datum;
                // WARNING(msg) (__DEFINES/logging.dm): warning("[MSG] in [__FILE__] at line [__LINE__] src: [src] usr: [usr].")
                warning(Dm.text("[] in [] at line [] src: [] usr: [].", Dm.text("[] threw alert [] with new_master [] while already having that alert with master []", this, category, new_master, current_master), "code/_onclick/hud/screen_objects/alert.dm", 30, this, usr));

                clear_alert(category);
                return throw_alert(category, type, severity, new_master, @override, timeout_override, no_anim); // .()
            }
            else if (thealert.type != (type as Type))
            {
                clear_alert(category);
                return throw_alert(category, type, severity, new_master, @override, timeout_override, no_anim); // .()
            }
            else if (!Dm.truthy(severity) || Dm.eq(severity, thealert.severity))
            {
                if (!Dm.truthy(thealert.timeout))
                {
                    // No need to update existing alert
                    return thealert;
                }
                // Reset timeout of existing alert
                object timeout = (timeout_override is var or__1 && Dm.truthy(or__1) ? or__1 : Dm.initial(thealert, (atom_movable_screen_alert p) => p.timeout));
                _addtimer(Dm.@new<datum_callback>(this, nameof(alert_timeout), thealert, category), Dm.num(timeout), file: "code/_onclick/hud/screen_objects/alert.dm", line: 42);
                thealert.timeout = world.time + Dm.num(timeout) - world.tick_lag;
                return thealert;
            }
        }
        else
        {
            thealert = Dm.@new((Type)type) as atom_movable_screen_alert;
            thealert.override_alerts = Dm.num(@override);
            if (Dm.truthy(@override))
                thealert.timeout = null;
        }
        thealert.owner = this;

        if (Dm.truthy(new_master))
        {
            thealert.master_ref = master_ref;
            thealert.RegisterSignal(new_master, COMSIG_ATOM_UPDATE_APPEARANCE, nameof(atom_movable_screen_alert.on_master_update_appearance));
            thealert.update_appearance();
        }
        else if (Dm.truthy(severity))
        {
            thealert.icon_state = Dm.text("[][]", Dm.initial(thealert, (atom p) => p.icon_state), severity);
            thealert.severity = Dm.num(severity);
        }

        alerts[category] = thealert;
        if (Dm.truthy(client) && Dm.truthy(hud_used))
            hud_used.reorganize_alerts();
        if (!Dm.truthy(no_anim))
        {
            thealert.transform = Dm.matrix(32, 0, MATRIX_TRANSLATE);
            animate(thealert, Dm.named("transform", Dm.matrix()), Dm.named("time", 1 * SECONDS), Dm.named("easing", ELASTIC_EASING));
        }
        if (Dm.truthy(timeout_override))
            thealert.timeout = Dm.num_or_null(timeout_override);
        if (Dm.truthy(thealert.timeout))
        {
            _addtimer(Dm.@new<datum_callback>(this, nameof(alert_timeout), thealert, category), Dm.num(thealert.timeout), file: "code/_onclick/hud/screen_objects/alert.dm", line: 69);
            thealert.timeout = world.time + Dm.num(thealert.timeout) - world.tick_lag;
        }
        return thealert;
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:74
    public virtual void alert_timeout(atom_movable_screen_alert alert = null, object category = null)
    {
        if (Dm.truthy(alert.timeout) && Dm.eq(alerts[category], alert) && world.time >= Dm.num(alert.timeout))
            clear_alert(category);
    }

    // Proc to clear an existing alert.
    // tg: code/_onclick/hud/screen_objects/alert.dm:79
    public virtual object clear_alert(object category = null, float? clear_override = null)
    {
        clear_override ??= FALSE;
        atom_movable_screen_alert alert = alerts[category] as atom_movable_screen_alert;
        if (!Dm.truthy(alert))
            return 0;
        if (Dm.truthy(alert.override_alerts) && !Dm.truthy(clear_override))
            return 0;

        Dm.remove(ref alerts, category);
        if (Dm.truthy(client) && Dm.truthy(hud_used))
        {
            hud_used.reorganize_alerts();
            foreach (mob viewer in Dm.each_cast<mob>(observers))
            {
                if (viewer.client != null)
                    Dm.remove(viewer.client.screen, alert);
            }
            Dm.remove(client.screen, alert);
        }
        qdel(alert);
        return null;
    }

    // Proc to check for an alert
    // tg: code/_onclick/hud/screen_objects/alert.dm:95
    public virtual object has_alert(object category = null)
    {
        return Dm.num(!(alerts[category] == null));
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:98
[DmPath("/atom/movable/screen/alert")]
public partial class atom_movable_screen_alert : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:99-103
    public atom_movable_screen_alert()
    {
        icon = Dm.resource("icons/hud/screen_alert.dmi");
        icon_state = "template";
        name = "Alert";
        desc = "Something seems to have gone wrong with this alert, so report this bug please";
        mouse_opacity = MOUSE_OPACITY_ICON;
    }

    /// do we glow to represent we do stuff when clicked
    // tg: code/_onclick/hud/screen_objects/alert.dm:105
    public float clickable_glow = FALSE;
    // tg: code/_onclick/hud/screen_objects/alert.dm:106 (float?: tg assigns thealert.timeout = null, alert.dm:50)
    public float? timeout = 0; //If set to a number, this alert will clear itself after that many deciseconds
    // tg: code/_onclick/hud/screen_objects/alert.dm:107
    public float severity = 0;
    // tg: code/_onclick/hud/screen_objects/alert.dm:108
    public string alerttooltipstyle = "";
    // tg: code/_onclick/hud/screen_objects/alert.dm:109
    public float override_alerts = FALSE; //If it is overriding other alerts of the same type
    // tg: code/_onclick/hud/screen_objects/alert.dm:110
    public mob owner; //Alert owner

    /// Boolean. If TRUE, the Click() proc will attempt to Click() on the master first if there is a master.
    // tg: code/_onclick/hud/screen_objects/alert.dm:113
    public float click_master = TRUE;

    ///If set true, instead of using the default icon file for screen alerts, it will use the hud's ui style
    // tg: code/_onclick/hud/screen_objects/alert.dm:116
    public float use_user_hud_icon = USER_HUD_STYLE_IGNORE;
    ///If set, this overlay will be added to the icon.
    // tg: code/_onclick/hud/screen_objects/alert.dm:118
    public object overlay_state;
    ///The file to fetch the overlay from
    // tg: code/_onclick/hud/screen_objects/alert.dm:120 (a resource, rules 3.7)
    public object overlay_icon = Dm.resource("icons/hud/screen_alert.dmi");
}

// tg: code/_onclick/hud/screen_objects/alert.dm:122
public partial class atom_movable_screen_alert : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:122
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        // if(PERFORM_ALL_TESTS(focus_only/screen_alert_overlay) && ...) (:124): FALSE in production (rules 1.7)
        if (Dm.truthy(FALSE))
            Dm.NotTranslated("code/_onclick/hud/screen_objects/alert.dm:124", "Initialize: the screen_alert_overlay focus test (CI only)");
        if (Dm.truthy(clickable_glow))
        {
            add_filter("clickglow", 2, outline_filter(color: COLOR_GOLD, size: 1) as DmList); // M6 slice 7 (D155): filters reach the client
            mouse_over_pointer = MOUSE_HAND_POINTER;
        }
        if (Dm.truthy(overlay_state))
            update_appearance();
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:132
    public override async DmTask MouseEntered(object location = null, object control = null, object @params = null)
    {
        await base.MouseEntered(location, control, @params);
        if (!QDELETED(this))
            openToolTip(usr, this, @params, title: name, content: desc, theme: alerttooltipstyle);
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:137
    public override void MouseExited(object location = null, object control = null, object @params = null)
    {
        closeToolTip(usr);
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:165
    [ShouldCallParent]
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        await base.Click(location, control, @params);
        if (usr == null || !Dm.truthy(GET_CLIENT(usr)) || usr != owner)
            return FALSE;
        DmList modifiers = params2list(@params);
        if (Dm.truthy(LAZYACCESS(modifiers, SHIFT_CLICK))) // screen objects don't do the normal Click() stuff so we'll cheat
        {
            // boxed_message(str) (__DEFINES/chat.dm:48), inline
            to_chat(usr, "<div class='boxed_message'>" + jointext(await examine(usr), "\n") + "</div>");
            return FALSE;
        }
        if (!Dm.truthy(click_master))
            return TRUE;
        datum our_master = master_ref?.resolve() as datum;
        if (our_master != null)
        {
            await usr.client.Click(our_master as atom, location as atom, control, @params);
            return null; // /client/Click returns nothing
        }

        return TRUE;
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:140
    [SignalHandler]
    public virtual void on_master_update_appearance(datum source = null)
    {
        update_appearance();
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:144
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        atom our_master = master_ref?.resolve() as atom;
        if (our_master is atom && !QDELETED(our_master))
            Dm.append(ref dot, add_atom_icon(our_master));
        if (Dm.truthy(overlay_state))
            Dm.append(ref dot, mutable_appearance(overlay_icon, (string)overlay_state));
        return dot;
    }

    ///Returns a copy of the appearance of the atom, with its base pixel coordinates. Useful for overlays
    // tg: code/_onclick/hud/screen_objects/alert.dm:153
    public virtual object add_atom_icon(atom atom = null)
    {
        mutable_appearance atom_appearance = Dm.@new<mutable_appearance>(atom);
        atom_appearance.appearance_flags = KEEP_TOGETHER;
        atom_appearance.layer = FLOAT_LAYER;
        atom_appearance.plane = FLOAT_PLANE;
        atom_appearance.dir = SOUTH;
        atom_appearance.pixel_x = atom.base_pixel_x;
        atom_appearance.pixel_y = atom.base_pixel_y;
        atom_appearance.pixel_w = atom.base_pixel_w;
        atom_appearance.pixel_z = atom.base_pixel_z;
        // return strip_appearance_underlays(atom_appearance) (__HELPERS/icons.dm:1353): GAP(lead, __HELPERS/icons.cs): no
        // mirror yet; its body (drop the underlays on another true plane than the appearance's) is written here.
        object base_plane = PLANE_TO_TRUE(atom_appearance.plane);
        foreach (object underlay in Dm.each(atom_appearance.underlays))
        {
            if (underlay == null)
                continue;
            if (!Dm.eq(PLANE_TO_TRUE(((IDmAppearance)underlay).plane), base_plane))
                Dm.remove(atom_appearance.underlays, underlay);
        }
        return atom_appearance;
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:183
    public override object Destroy(float? force = null)
    {
        object dot = null;
        dot = base.Destroy(force);
        severity = 0;
        master_ref = null;
        owner = null;
        screen_loc = "";
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:190
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        return Dm.list(
            span_boldnotice(name),
            span_info(desc)
        );
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:204
[DmPath("/atom/movable/screen/alert/not_enough_oxy")]
public partial class atom_movable_screen_alert_not_enough_oxy : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:205-207
    public atom_movable_screen_alert_not_enough_oxy()
    {
        name = "Choking (No O2)";
        desc = "You're not getting enough oxygen. Find some good air before you pass out! The box in your backpack has an oxygen tank and breath mask in it.";
        icon_state = ALERT_NOT_ENOUGH_OXYGEN;
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:209
[DmPath("/atom/movable/screen/alert/too_much_oxy")]
public partial class atom_movable_screen_alert_too_much_oxy : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:210-212
    public atom_movable_screen_alert_too_much_oxy()
    {
        name = "Choking (O2)";
        desc = "There's too much oxygen in the air, and you're breathing it in! Find some good air before you pass out!";
        icon_state = ALERT_TOO_MUCH_OXYGEN;
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:214
[DmPath("/atom/movable/screen/alert/not_enough_nitro")]
public partial class atom_movable_screen_alert_not_enough_nitro : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:215-217
    public atom_movable_screen_alert_not_enough_nitro()
    {
        name = "Choking (No N2)";
        desc = "You're not getting enough nitrogen. Find some good air before you pass out!";
        icon_state = ALERT_NOT_ENOUGH_NITRO;
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:219
[DmPath("/atom/movable/screen/alert/too_much_nitro")]
public partial class atom_movable_screen_alert_too_much_nitro : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:220-222
    public atom_movable_screen_alert_too_much_nitro()
    {
        name = "Choking (N2)";
        desc = "There's too much nitrogen in the air, and you're breathing it in! Find some good air before you pass out!";
        icon_state = ALERT_TOO_MUCH_NITRO;
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:224
[DmPath("/atom/movable/screen/alert/not_enough_co2")]
public partial class atom_movable_screen_alert_not_enough_co2 : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:225-227
    public atom_movable_screen_alert_not_enough_co2()
    {
        name = "Choking (No CO2)";
        desc = "You're not getting enough carbon dioxide. Find some good air before you pass out!";
        icon_state = ALERT_NOT_ENOUGH_CO2;
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:229
[DmPath("/atom/movable/screen/alert/too_much_co2")]
public partial class atom_movable_screen_alert_too_much_co2 : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:230-232
    public atom_movable_screen_alert_too_much_co2()
    {
        name = "Choking (CO2)";
        desc = "There's too much carbon dioxide in the air, and you're breathing it in! Find some good air before you pass out!";
        icon_state = ALERT_TOO_MUCH_CO2;
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:234
[DmPath("/atom/movable/screen/alert/not_enough_plas")]
public partial class atom_movable_screen_alert_not_enough_plas : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:235-237
    public atom_movable_screen_alert_not_enough_plas()
    {
        name = "Choking (No Plasma)";
        desc = "You're not getting enough plasma. Find some good air before you pass out! The box in your backpack has a spare plasma tank.";
        icon_state = ALERT_NOT_ENOUGH_PLASMA;
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:239
[DmPath("/atom/movable/screen/alert/too_much_plas")]
public partial class atom_movable_screen_alert_too_much_plas : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:240-242
    public atom_movable_screen_alert_too_much_plas()
    {
        name = "Choking (Plasma)";
        desc = "There's highly flammable, toxic plasma in the air and you're breathing it in. Find some fresh air. The box in your backpack has an oxygen tank and breath mask in it.";
        icon_state = ALERT_TOO_MUCH_PLASMA;
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:244
[DmPath("/atom/movable/screen/alert/not_enough_n2o")]
public partial class atom_movable_screen_alert_not_enough_n2o : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:245-247
    public atom_movable_screen_alert_not_enough_n2o()
    {
        name = "Choking (No N2O)";
        desc = "You're not getting enough N2O. Find some good air before you pass out!";
        icon_state = ALERT_NOT_ENOUGH_N2O;
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:249
[DmPath("/atom/movable/screen/alert/too_much_n2o")]
public partial class atom_movable_screen_alert_too_much_n2o : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:250-252
    public atom_movable_screen_alert_too_much_n2o()
    {
        name = "Choking (N2O)";
        desc = "There's sleeping gas in the air and you're breathing it in. Find some fresh air. The box in your backpack has an oxygen tank and breath mask in it.";
        icon_state = ALERT_TOO_MUCH_N2O;
    }
}

//End gas alerts

// tg: code/_onclick/hud/screen_objects/alert.dm:350 (the gravity alerts, M2 integration: /mob/living/update_gravity throws them)
[DmPath("/atom/movable/screen/alert/negative")]
public partial class atom_movable_screen_alert_negative : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:351-354
    public atom_movable_screen_alert_negative()
    {
        name = "Negative Gravity";
        desc = "You're getting pulled upwards. While you won't have to worry about falling down anymore, you may accidentally fall upwards!";
        use_user_hud_icon = USER_HUD_STYLE_INHERIT;
        overlay_state = "negative";
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:356
[DmPath("/atom/movable/screen/alert/weightless")]
public partial class atom_movable_screen_alert_weightless : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:357-363
    public atom_movable_screen_alert_weightless()
    {
        name = "Weightless";
        desc = "Gravity has ceased affecting you, and you're floating around aimlessly. You'll need something large and heavy, like a wall or lattice, to push yourself off if you want to move. A jetpack would enable free range of motion. A pair of magboots would let you walk around normally on the floor. Barring those, you can throw things, use a fire extinguisher, or shoot a gun to move around via Newton's 3rd Law of Motion.";
        use_user_hud_icon = USER_HUD_STYLE_INHERIT;
        overlay_state = "weightless";
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:365
[DmPath("/atom/movable/screen/alert/highgravity")]
public partial class atom_movable_screen_alert_highgravity : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:366-369
    public atom_movable_screen_alert_highgravity()
    {
        name = "High Gravity";
        desc = "You're getting crushed by high gravity, picking up items and movement will be slowed.";
        use_user_hud_icon = USER_HUD_STYLE_INHERIT;
        overlay_state = "paralysis";
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:371
[DmPath("/atom/movable/screen/alert/veryhighgravity")]
public partial class atom_movable_screen_alert_veryhighgravity : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:372-375
    public atom_movable_screen_alert_veryhighgravity()
    {
        name = "Crushing Gravity";
        desc = "You're getting crushed by high gravity, picking up items and movement will be slowed. You'll also accumulate brute damage!";
        use_user_hud_icon = USER_HUD_STYLE_INHERIT;
        overlay_state = "paralysis";
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:334
public partial class atom_movable_screen_alert_embeddedobject : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:342
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (!Dm.truthy(dot))
            return dot;

        mob_living_carbon carbon_owner = owner as mob_living_carbon;
        return carbon_owner.check_self_for_injuries();
    }
}

// M8 slice G: the fire alert's click (stop, drop and roll) and the fake fire alert of the fire hallucination.
// tg: code/_onclick/hud/screen_objects/alert.dm:377
public partial class atom_movable_screen_alert_fire : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:383
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (!Dm.truthy(dot))
            return FALSE;

        mob_living living_owner = owner as mob_living;
        if (!Dm.truthy(living_owner.can_resist()))
            return FALSE;

        living_owner.changeNext_move(CLICK_CD_RESIST);
        if (!Dm.truthy(Dm.bitand(living_owner.mobility_flags, MOBILITY_MOVE)))
            return FALSE;

        return await handle_stop_drop_roll(living_owner);
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:398
    public virtual async DmTask<object> handle_stop_drop_roll(mob_living roller = null)
    {
        return await roller.resist_fire();
    }
}

// tg: code/modules/hallucination/on_fire.dm:146  This alert is thrown when hallucinating fire
public partial class atom_movable_screen_alert_fire_fake : atom_movable_screen_alert_fire
{
    // tg: code/modules/hallucination/on_fire.dm:148
    public override async DmTask<object> handle_stop_drop_roll(mob_living roller = null)
    {
        return Dm.num(Dm.truthy(await roller.apply_status_effect(typeof(datum_status_effect_stop_drop_roll_hallucinating), hallucination_weakref)));
    }
}

// M8y slice A: the buckled alert: clicking it resists the buckle (unbuckles, or the cuffed breakout).
// tg: code/_onclick/hud/screen_objects/alert.dm:1104
public partial class atom_movable_screen_alert_buckled : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:1113
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (!Dm.truthy(dot))
            return dot;

        mob_living living_owner = owner as mob_living;

        if (!Dm.truthy(living_owner.can_resist()))
            return null;
        living_owner.changeNext_move(CLICK_CD_RESIST);
        if (living_owner.last_special <= world.time)
            await living_owner.resist_buckle();
        return null;
    }
}

// M7x slice E: the restraints' alert (handcuffed and legcuffed): clicking it resists the restraints.
// tg: code/_onclick/hud/screen_objects/alert.dm:1143
public partial class atom_movable_screen_alert_restrained : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:1141
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (!Dm.truthy(dot))
            return dot;

        mob_living living_owner = owner as mob_living;

        if (!Dm.truthy(living_owner.can_resist()))
            return null;

        living_owner.changeNext_move(CLICK_CD_RESIST);
        if (Dm.truthy(Dm.bitand(living_owner.mobility_flags, MOBILITY_MOVE)) && living_owner.last_special <= world.time)
            await living_owner.resist_restraints();
        return null;
    }
}

/// Gives the player the option to succumb while in critical condition
// tg: code/_onclick/hud/screen_objects/alert.dm:578
[DmPath("/atom/movable/screen/alert/succumb")]
public partial class atom_movable_screen_alert_succumb : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:579-584
    public atom_movable_screen_alert_succumb()
    {
        name = "Succumb";
        desc = "Shuffle off this mortal coil.";
        use_user_hud_icon = USER_HUD_STYLE_INHERIT;
        overlay_icon = Dm.resource("icons/mob/simple/mob.dmi");
        overlay_state = "ghost";
        clickable_glow = TRUE;
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:585  var/static/list/death_titles (rules 10.6: a TgState field, at
    // the end of this file)
    public static ref DmList death_titles => ref TgState.Current.atom_movable_screen_alert_succumb__death_titles;

    // tg: code/_onclick/hud/screen_objects/alert.dm:596  (M8z, D241)
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (!Dm.truthy(dot))
            return dot;
        mob_living living_owner = owner as mob_living;
        if (!CAN_SUCCUMB(living_owner) && !HAS_TRAIT(living_owner, TRAIT_SUCCUMB_OVERRIDE)) //checked again in [mob/living/verb/succumb()]
            return dot;

        object title = pick(death_titles);

        //Succumbing with a message
        object last_whisper = await tgui_input_text(usr, I18n.T("Do you have any last words?"), title, max_length: CHAT_MESSAGE_MAX_LENGTH, encode: FALSE); // saycode already handles sanitization
        if (last_whisper == null)
            return dot;
        if (Dm.truthy(length(last_whisper)))
            await living_owner.say(Dm.text("#[]", last_whisper));
        // INVOKE_GAME_VERB(living_owner, usr, /mob/living, succumb, whisper = length(last_whisper) > 0) (__DEFINES/verb.dm:172):
        // SSverbs.invoke calls the body with the alist as its structured_args (controllers/subsystem/verbs.dm:24-34), usr the invoker.
        // The key is "whisper" while the body reads "whispered", so the body sees no whisper, as in tg.
        await living_owner.__gvb_succumb(Dm.list(Dm.kv("whisper", Dm.truthy(length(last_whisper)) ? TRUE : FALSE)));
        return dot;
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:1155  (M8z slice B: the shoelaces the strip menu knots)
public partial class atom_movable_screen_alert_shoes : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:1161
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (!Dm.truthy(dot))
            return dot;

        obj_item_clothing_shoes shoes = owner.get_item_by_slot(ITEM_SLOT_FEET) as obj_item_clothing_shoes;
        if (!Dm.truthy(owner.can_resist()) || shoes == null)
            return null;

        owner.changeNext_move(CLICK_CD_RESIST);
        await shoes.handle_tying(owner as mob_living);
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:1201
public partial class datum_hud : datum
{
    // PRIVATE = only edit, use, or override these if you're editing the system as a whole

    /// Gets the placement for the alert based on its index
    // tg: code/_onclick/hud/screen_objects/alert.dm:1201
    public virtual object get_ui_alert_placement(object index = null)
    {
        // Only has support for 5 slots currently
        if (Dm.num(index) > 5)
            return "";

        return Dm.text("EAST-1:28,CENTER+[]:[]", 6 - Dm.num(index), 29 - (Dm.num(index) * 2));
    }

    // Re-render all alerts - also called in /datum/hud/show_hud() because it's needed there
    // tg: code/_onclick/hud/screen_objects/alert.dm:1209
    public virtual object reorganize_alerts(mob viewmob = null)
    {
        mob screenmob = (viewmob is var or__1 && Dm.truthy(or__1) ? or__1 : mymob) as mob;
        if (!Dm.truthy(screenmob.client))
            return FALSE;
        DmList alerts = mymob.alerts;
        if (hud_version != HUD_STYLE_STANDARD)
        {
            for (float i = 1, i__end = alerts.len; i <= i__end; i++)
                Dm.remove(screenmob.client.screen, alerts[alerts[i]]);
            return TRUE;
        }

        object user_pref_hud = ui_style2icon(mymob.client?.prefs?.read_preference(typeof(datum_preference_choiced_ui_style)));
        for (float i = 1, i__end = length(alerts); i <= i__end; i++)
        {
            atom_movable_screen_alert alert = alerts[alerts[i]] as atom_movable_screen_alert;
            float sw__1 = alert.use_user_hud_icon;
            if (sw__1 == USER_HUD_STYLE_INHERIT)
                alert.icon = ui_style;
            else if (sw__1 == USER_HUD_STYLE_PREFS)
            {
                if (Dm.truthy(user_pref_hud))
                    alert.icon = user_pref_hud;
            }
            alert.screen_loc = (string)get_ui_alert_placement(i);
            Dm.combine(screenmob.client.screen, alert);
        }
        if (!Dm.truthy(viewmob))
        {
            foreach (object viewer in Dm.each(mymob.observers))
                reorganize_alerts(viewer as mob);
        }
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:585  var/static/list/death_titles = list(...)
    internal DmList atom_movable_screen_alert_succumb__death_titles = Dm.list(
        "Goodnight, Sweet Prince",
        "Game Over, Man",
        "End Of The Road",
        "Live Long And Prosper",
        "See You Space Cowboy...",
        "It's Been An Honor",
        "The Curtains Close",
        "All Good Things Must End"
    );
}

// tg: code/_onclick/hud/screen_objects/alert.dm:401  (M9 slice F: the offering's alert)
public partial class atom_movable_screen_alert_give : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:416
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:420
    public override object Destroy(float? force = null)
    {
        offer = null;
        return base.Destroy(force);
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:424
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        context[SCREENTIP_CONTEXT_LMB] = Dm.truthy(screentip_override_text) ? screentip_override_text : Dm.text("Take []", offer.offered_item.name);
        context[SCREENTIP_CONTEXT_SHIFT_LMB] = "Examine";
        return CONTEXTUAL_SCREENTIP_SET;
    }

    /**
     * Handles assigning most of the variables for the alert that pops up when an item is offered
     *
     * Handles setting the name, description and icon of the alert and tracking the living mob giving
     * and the item being offered.
     * Arguments:
     * * taker - The living mob receiving the alert
     * * offer - The status effect connected to the offer being made
     */
    // tg: code/_onclick/hud/screen_objects/alert.dm:438
    public virtual void setup(mob_living taker = null, datum_status_effect_offering offer = null)
    {
        this.offer = offer;

        mob_living offerer = offer.owner;
        obj_item receiving = offer.offered_item;
        object receiving_name = get_receiving_name(taker, offerer, receiving);
        name = I18n.T("[] is offering []", offerer, receiving_name);
        desc = I18n.T("[] is offering []. []", offerer, receiving_name, additional_desc_text);
        icon_state = "template";
        cut_overlays();
        add_overlay(receiving);
    }

    /**
     * Called right before `setup()`, to do any sort of logic to change the name of
     * what's displayed as the name of what's being offered in the alert. Use this to
     * add pronouns and the like, or to totally override the displayed name!
     * Also the best place to make changes to `additional_desc_text` before `setup()`
     * without having to override `setup()` entirely.
     *
     * Arguments:
     * * taker - The person receiving the alert
     * * offerer - The person giving the alert and item
     * * receiving - The item being given by the offerer
     *
     * Returns a string that will be displayed in the alert, which is `receiving.name`
     * by default.
     */
    // tg: code/_onclick/hud/screen_objects/alert.dm:465
    public virtual object get_receiving_name(mob_living taker = null, mob_living offerer = null, obj_item receiving = null)
    {
        return receiving.name;
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:468
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (!Dm.truthy(dot))
            return dot;

        if (!isliving(usr))
            throw Dm.CRASH(Dm.text("User for [] is of type ", this) + "[" + Dm.text("[]", usr.type) + "]. This should never happen.", "code/_onclick/hud/screen_objects/alert.dm", 474);

        await handle_transfer();
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:478
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(examinable))
            return await base.examine(user, __extra);

        return Dm.list(
            span_boldnotice(name),
            span_info(I18n.T("[] is offering you the following item (click the alert to take it!):", offer.owner)),
            Dm.text("<hr>[]", jointext(await offer.offered_item.examine(user), "\n")));
    }

    /// An overrideable proc used simply to hand over the item when claimed, this is a proc so that high-fives can override them since nothing is actually transferred
    // tg: code/_onclick/hud/screen_objects/alert.dm:489
    public virtual async DmTask handle_transfer()
    {
        mob_living taker = owner as mob_living;
        mob_living offerer = offer.owner;
        obj_item receiving = offer.offered_item;
        await taker.take(offerer, receiving, bypass_active_hand);
        if (Dm.truthy(offerer._listen_lookup?[COMSIG_LIVING_ITEM_GIVEN]))
            offerer._SendSignal(COMSIG_LIVING_ITEM_GIVEN, Dm.list(offerer, taker, receiving));
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:500
public partial class atom_movable_screen_alert_give_highfive : atom_movable_screen_alert_give
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:507
    public override object get_receiving_name(mob_living taker = null, mob_living offerer = null, obj_item receiving = null)
    {
        return I18n.T("a high-five");
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:510
    public override void setup(mob_living taker = null, datum_status_effect_offering offer = null)
    {
        base.setup(taker, offer);
        RegisterSignal(offer.owner, COMSIG_ATOM_EXAMINE_MORE, nameof(check_fake_out));
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:514
    public override async DmTask handle_transfer()
    {
        if (Dm.truthy(too_slowing_this_guy))
            return;

        mob_living taker = owner as mob_living;
        mob_living offerer = offer.owner;
        obj_item receiving = offer.offered_item;
        if (!QDELETED(receiving) && Dm.truthy(offerer.is_holding(receiving)))
        {
            await receiving.on_offer_taken(offerer, taker);
            return;
        }

        too_slow_p1();
    }

    /// If the person who offered the high five no longer has it when we try to accept it, we get pranked hard
    // tg: code/_onclick/hud/screen_objects/alert.dm:528
    public virtual void too_slow_p1()
    {
        mob_living rube = owner as mob_living;
        mob_living offerer = offer?.owner;
        if (QDELETED(rube) || QDELETED(offerer))
        {
            qdel(this);
            return;
        }

        too_slowing_this_guy = TRUE;
        offerer.visible_message(span_notice(I18n.T("[] rushes in to high-five [], but-", rube, offerer)), span_nicegreen(I18n.T("[] falls for your trick just as planned, lunging for a high-five that no longer exists! Classic!", rube)), ignored_mobs: rube);
        to_chat(rube, span_nicegreen(I18n.T("You go in for []'s high-five, but-", offerer)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(too_slow_p2), offerer, rube), 0.5f * SECONDS, file: "code/_onclick/hud/screen_objects/alert.dm", line: 540);
    }

    /// Part two of the ultimate prank
    // tg: code/_onclick/hud/screen_objects/alert.dm:541
    public virtual async DmTask too_slow_p2()
    {
        mob_living rube = owner as mob_living;
        mob_living offerer = offer?.owner;
        if (!QDELETED(rube) && !QDELETED(offerer))
        {
            offerer.visible_message(span_danger(I18n.T("[] pulls away from []'s slap at the last second, dodging the high-five entirely!", offerer, rube)), span_nicegreen(I18n.T("[] fails to make contact with your hand, making an utter fool of []self!", rube, rube.p_them())), span_hear(I18n.T("You hear a disappointing sound of flesh not hitting flesh!")), ignored_mobs: rube);
            to_chat(rube, span_userdanger(uppertext(I18n.T("NO! [] PULLS [] HAND AWAY FROM YOURS! YOU'RE TOO SLOW!", offerer, offerer.p_their()))));
            playsound(offerer, Dm.resource("sound/items/weapons/thudswoosh.ogg"), 100, TRUE, 1);
            await rube.Knockdown(1 * SECONDS);
            await offerer.add_mood_event("high_five", typeof(datum_mood_event_down_low));
            await rube.add_mood_event("high_five", typeof(datum_mood_event_too_slow));
            offerer.remove_status_effect(typeof(datum_status_effect_offering_no_item_received_high_five));
            // offerer.client?.give_award(/datum/award/achievement/misc/down_low_too_slow, offerer): the achievements (M10)
        }

        qdel(this);
    }

    /// If someone examine_more's the offerer while they're trying to pull a too-slow, it'll tip them off to the offerer's trickster ways
    // tg: code/_onclick/hud/screen_objects/alert.dm:557
    [SignalHandler]
    public virtual void check_fake_out(mob source = null, mob user = null, DmList examine_list = null)
    {
        if (QDELETED(offer.offered_item))
            Dm.append(ref examine_list, span_warning(I18n.T("[]'s arm appears tensed up, as if [] plan[] on pulling it back suddenly...", source, source.p_they(), source.p_s())));
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:563
public partial class atom_movable_screen_alert_give_hand : atom_movable_screen_alert_give
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:567
    public override object get_receiving_name(mob_living taker = null, mob_living offerer = null, obj_item receiving = null)
    {
        additional_desc_text = I18n.T("Click this alert to take it and let [] pull you around!", offerer.p_them());
        return I18n.T("[] []", offerer.p_their(), receiving.name);
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:571
public partial class atom_movable_screen_alert_give_hand_helping : atom_movable_screen_alert_give_hand
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:573
    public override object get_receiving_name(mob_living taker = null, mob_living offerer = null, obj_item receiving = null)
    {
        object dot = base.get_receiving_name(taker, offerer, receiving);
        additional_desc_text = I18n.T("Click this alert to let them help you up!");
        return dot;
    }
}


// The syndicate campaign X2 (D316): the ghosts' poll alert (SSpolling throws it on every polled ghost); its screentips (add_context)
// stay a live-partial row with the other screentips.
// tg: code/_onclick/hud/screen_objects/alert.dm:954
public partial class atom_movable_screen_alert_poll_alert : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:974
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        signed_up_overlay = mutable_appearance(Dm.resource("icons/hud/screen_gen.dmi"), icon_state: "selector") as mutable_appearance;
        register_context();
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:979
    public virtual void set_role_overlay()
    {
        object role_or_only_question = Dm.truthy(poll.role) ? poll.role : "?";
        role_overlay = Dm.@new<mutable_appearance>();
        role_overlay.screen_loc = screen_loc;
        role_overlay.maptext = MAPTEXT(Dm.text("<span style='text-align: right; color: #B3E3FC'>[]</span>", full_capitalize(role_or_only_question)));
        role_overlay.maptext_width = 128;
        role_overlay.transform = ((matrix)role_overlay.transform).Translate(-128, 0);
        add_overlay(role_overlay);
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:988
    public override object Destroy(float? force = null)
    {
        role_overlay = null; // QDEL_NULL on an appearance (not a datum): qdel ignores it
        time_left_overlay = null;
        stacks_overlay = null;
        candidates_num_overlay = null;
        signed_up_overlay = null;
        if (poll != null)
            Dm.remove(poll.alert_buttons, this);
        poll = null;
        return base.Destroy(force);
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:1017
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(show_time_left))
        {
            float timeleft = Dm.num(timeout) - world.time;
            if (timeleft <= 0)
                return PROCESS_KILL;
            cut_overlay(time_left_overlay);
            time_left_overlay = Dm.@new<mutable_appearance>();
            time_left_overlay.maptext = MAPTEXT(Dm.text("<span style='color: []'><b>[]</b></span>", (timeleft <= 10 * SECONDS) ? "red" : "white", ceil(timeleft / (1 * SECONDS))));
            time_left_overlay.transform = ((matrix)time_left_overlay.transform).Translate(4, 19);
            add_overlay(time_left_overlay);
        }
        return null;
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:1028
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (!Dm.truthy(dot) || poll == null)
            return dot;
        DmList modifiers = params2list(@params);
        if (Dm.truthy(LAZYACCESS(modifiers, ALT_CLICK)) && Dm.truthy(poll.ignoring_category))
        {
            set_never_round();
            return dot;
        }
        if (Dm.truthy(LAZYACCESS(modifiers, CTRL_CLICK)) && poll.jump_to_me != null)
        {
            await jump_to_jump_target();
            return dot;
        }
        handle_sign_up();
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:1041
    public virtual void handle_sign_up()
    {
        if (Dm.@in(owner, poll.signed_up))
            poll.remove_candidate(owner);
        else if (!Dm.@in(owner.ckey, GLOB.poll_ignore[poll.ignoring_category]))
            poll.sign_up(owner);
        update_signed_up_overlay();
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:1048
    public virtual void set_never_round()
    {
        if (!Dm.@in(owner.ckey, GLOB.poll_ignore[poll.ignoring_category]))
        {
            poll.do_never_for_this_round(owner);
            color = "red";
            update_signed_up_overlay();
            return;
        }
        poll.undo_never_for_this_round(owner);
        color = Dm.initial(this, (atom p) => p.color);
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:1057
    public virtual async DmTask jump_to_jump_target()
    {
        if (poll?.jump_to_me == null || !isobserver(owner))
            return;
        turf target_turf = get_turf(poll.jump_to_me) as turf;
        if (target_turf != null && isturf(target_turf))
            await owner.abstract_move(target_turf);
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:1064
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        if (Dm.truthy(href_list["never"]))
        {
            set_never_round();
            return null;
        }
        if (Dm.truthy(href_list["signup"]))
            handle_sign_up();
        if (Dm.truthy(href_list["jump"]))
        {
            await jump_to_jump_target();
            return null;
        }
        return null;
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:1074
    public virtual void update_signed_up_overlay()
    {
        if (Dm.@in(owner, poll.signed_up))
            add_overlay(signed_up_overlay);
        else
            cut_overlay(signed_up_overlay);
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:1080
    public virtual void update_candidates_number_overlay()
    {
        cut_overlay(candidates_num_overlay);
        if (!Dm.truthy(length(poll.signed_up)))
            return;
        candidates_num_overlay = Dm.@new<mutable_appearance>();
        candidates_num_overlay.maptext = MAPTEXT(Dm.text("<span style='text-align: right; color: aqua'>[]</span>", length(poll.signed_up)));
        candidates_num_overlay.transform = ((matrix)candidates_num_overlay.transform).Translate(-4, 2);
        add_overlay(candidates_num_overlay);
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:1089
    public virtual void update_stacks_overlay()
    {
        cut_overlay(stacks_overlay);
        float stack_number = 1;
        foreach (datum_candidate_poll other_poll in Dm.each_cast<datum_candidate_poll>(SSpolling.currently_polling))
        {
            if (other_poll != poll && Dm.eq(other_poll.poll_key, poll.poll_key) && !Dm.truthy(other_poll.finished))
                stack_number++;
        }
        if (stack_number <= 1)
            return;
        stacks_overlay = Dm.@new<mutable_appearance>();
        stacks_overlay.maptext = MAPTEXT(Dm.text("<span style='color: yellow'>[]x</span>", stack_number));
        stacks_overlay.transform = ((matrix)stacks_overlay.transform).Translate(3, 2);
        stacks_overlay.layer = layer;
        // tg never adds the stacks overlay (alert.dm:1098 ends here): kept
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:924
public partial class atom_movable_screen_alert_notify_action : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:936
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (!Dm.truthy(dot))
            return dot;

        atom target = target_ref?.resolve() as atom;
        if (target == null || !isobserver(owner) || target == owner)
            return dot;

        mob_dead_observer ghost_owner = owner as mob_dead_observer;

        if (Dm.truthy(click_interact))
        {
            await ghost_owner.jump_to_interact(target);
            return dot;
        }

        await ghost_owner.observer_view(target);
        return dot;
    }
}

//SILICONS

// tg: code/_onclick/hud/screen_objects/alert.dm:779
public partial class atom_movable_screen_alert_emptycell : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:784
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance(UPDATE_DESC);
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:788
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        desc = Dm.initial(this, (atom_movable_screen_alert_emptycell p) => p.desc);
        if (Dm.truthy(length(GLOB.roundstart_station_borgcharger_areas)))
            desc += I18n.T(" Recharging stations are available in [].", english_list(GLOB.roundstart_station_borgcharger_areas));
        return dot;
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:794
public partial class atom_movable_screen_alert_lowcell : atom_movable_screen_alert
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:799
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance(UPDATE_DESC);
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/alert.dm:803
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        desc = Dm.initial(this, (atom_movable_screen_alert_lowcell p) => p.desc);
        if (Dm.truthy(length(GLOB.roundstart_station_borgcharger_areas)))
            desc += I18n.T(" Recharging stations are available in [].", english_list(GLOB.roundstart_station_borgcharger_areas));
        return dot;
    }
}

//MECH

// tg: code/_onclick/hud/screen_objects/alert.dm:811
public partial class atom_movable_screen_alert_lowcell_mech : atom_movable_screen_alert_lowcell
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:811
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        desc = Dm.initial(this, (atom_movable_screen_alert_lowcell_mech p) => p.desc);
        if (Dm.truthy(length(GLOB.roundstart_station_mechcharger_areas)))
            desc += I18n.T(" Power ports are available in [].", english_list(GLOB.roundstart_station_mechcharger_areas));
        return dot;
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:817
public partial class atom_movable_screen_alert_emptycell_mech : atom_movable_screen_alert_emptycell
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:817
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        desc = Dm.initial(this, (atom_movable_screen_alert_emptycell_mech p) => p.desc);
        if (Dm.truthy(length(GLOB.roundstart_station_mechcharger_areas)))
            desc += I18n.T(" Power ports are available in [].", english_list(GLOB.roundstart_station_mechcharger_areas));
        return dot;
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:844
public partial class atom_movable_screen_alert_emptycell_plasma : atom_movable_screen_alert_emptycell
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:849
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        desc = Dm.initial(this, (atom_movable_screen_alert_emptycell_plasma p) => p.desc);
        return dot;
    }
}

// tg: code/_onclick/hud/screen_objects/alert.dm:853
public partial class atom_movable_screen_alert_lowcell_plasma : atom_movable_screen_alert_lowcell
{
    // tg: code/_onclick/hud/screen_objects/alert.dm:857
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        desc = Dm.initial(this, (atom_movable_screen_alert_lowcell_plasma p) => p.desc);
        return dot;
    }
}
