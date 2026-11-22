// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/_computer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 (the power monitor, D162): the whole file, the base of every computer: its init and power change (the screen and keyboard
// overlays, the light of the lit screen), the passage of projectiles over it, the screwdriver that takes the monitor out, the
// glass sounds, breaking, the EMP, the frame it leaves, and the three tgui hooks (the keyboard clicks and the power use of an open
// window). Var-only computers are live with it. Leaves: the emissive screen (DEVIATION DV12), the leaning component (M9) and the
// computer voice of text to speech (M9); the GPS tag of imprint_gps is a live-partial row.

// tg: code/game/machinery/computer/_computer.dm:2
public partial class obj_machinery_computer : obj_machinery
{
    // tg: code/game/machinery/computer/_computer.dm:31
    [DmParams("mapload", "C")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        power_change();
        return dot;
    }

    // tg: code/game/machinery/computer/_computer.dm:35
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        if (Dm.truthy(SStts?.tts_enabled))
            voice = SStts.computer_voice;
    }

    // tg: code/game/machinery/computer/_computer.dm:40
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        object dot = await base.mouse_drop_receive(dropped, user, @params);
        // We add the component only once here & not in Initialize() because there are tons of computers & we don't want to add to their init times
        await LoadComponent(typeof(datum_component_leanable), dropped); // gaps L1
        return dot;
    }

    // tg: code/game/machinery/computer/_computer.dm:45  allows projectiles to fly over the computer
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(projectiles_pass_chance))
            return FALSE;
        if (!isprojectile(mover))
            return FALSE;
        var proj = (obj_projectile)mover;
        if (!Dm.truthy(anchored))
            return TRUE;
        if (proj.firer != null && Dm.truthy(await Adjacent(proj.firer)))
            return TRUE;
        if (prob(projectiles_pass_chance))
            return TRUE;
        return FALSE;
    }

    // tg: code/game/machinery/computer/_computer.dm:62
    [DmParams()]
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/machinery/computer/_computer.dm:67
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        if (Dm.truthy(icon_keyboard))
        {
            if (Dm.truthy(keyboard_change_icon) && Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
                Dm.append(ref dot, Dm.text("[]_off", icon_keyboard));
            else
                Dm.append(ref dot, icon_keyboard);
        }

        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_broken", icon_state)));
            return dot; // If we don't do this broken computers glow in the dark.
        }

        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER))) // Your screen can't be on if you've got no damn charge
            return dot;

        // This lets screens ignore lighting and be visible even in the darkest room
        if (Dm.truthy(icon_screen))
        {
            Dm.append(ref dot, mutable_appearance(icon, icon_screen));
            // . += emissive_appearance(icon, icon_screen, src): the glow on the emissive plane (DEVIATION DV12)
            Dm.Deferred("code/game/machinery/computer/_computer.dm:84", "update_overlays: emissive_appearance of the screen (DEVIATION DV12)");
        }
        return dot;
    }

    // tg: code/game/machinery/computer/_computer.dm:87
    [SignalHandler]
    public override object power_change()
    {
        object dot = null;
        dot = base.power_change();
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
            Waiting.NoSleep(set_light(0));
        else
            Waiting.NoSleep(set_light(brightness_on));
        return dot;
    }

    // tg: code/game/machinery/computer/_computer.dm:94
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(await base.screwdriver_act(user, tool)))
            return TRUE;
        if (Dm.truthy(circuit))
        {
            balloon_alert(user, I18n.T("disconnecting monitor..."));
            if (Dm.truthy(await tool.use_tool(this, user, time_to_unscrew, volume: 50)))
                await deconstruct(TRUE);
        }
        return TRUE;
    }

    // tg: code/game/machinery/computer/_computer.dm:103
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        damage_flag ??= 0;
        object sw__1 = damage_type;
        if (Dm.eq(sw__1, BRUTE))
        {
            if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
                playsound(this.loc, Dm.resource("sound/effects/hit_on_shattered_glass.ogg"), 70, TRUE);
            else
                playsound(this.loc, Dm.resource("sound/effects/glass/glasshit.ogg"), 75, TRUE);
        }
        else if (Dm.eq(sw__1, BURN))
            playsound(this.loc, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
        return null;
    }

    // tg: code/game/machinery/computer/_computer.dm:113
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        if (!Dm.truthy(circuit)) //no circuit, no breaking
            return null;
        object dot = await base.atom_break(damage_flag);
        if (Dm.truthy(dot))
        {
            playsound(loc, Dm.resource("sound/effects/glass/glassbr3.ogg"), 100, TRUE);
            await set_light(0);
        }
        return dot;
    }

    // tg: code/game/machinery/computer/_computer.dm:133
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity);
        if (!Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
        {
            if (Dm.eq(severity, 1))
            {
                if (prob(50))
                    await atom_break(ENERGY);
            }
            else if (Dm.eq(severity, 2))
            {
                if (prob(10))
                    await atom_break(ENERGY);
            }
        }
        return dot;
    }

    // tg: code/game/machinery/computer/_computer.dm:144
    public override async DmTask<object> spawn_frame(object disassembled = null)
    {
        // GAP(types): the board instance is circuit_value (circuit is generated as a Type, rules 3.2 V3)
        if (QDELETED(circuit_value)) //no circuit, no computer frame
            return null;

        obj_structure_frame_computer new_frame = Dm.@new<obj_structure_frame_computer>(loc);
        await new_frame.setDir(dir);
        await new_frame.set_anchored(TRUE);
        new_frame.circuit_value = circuit_value;
        new_frame.circuit = circuit_value.GetType();
        // Circuit removal code is handled in /obj/machinery/Exited()
        Dm.remove(ref component_parts, circuit_value);
        await circuit_value.forceMove(new_frame);

        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)) || !Dm.truthy(disassembled))
        {
            atom drop_loc = drop_location() as atom;
            playsound(this, Dm.resource("sound/effects/hit_on_shattered_glass.ogg"), 70, TRUE);
            Dm.@new<obj_item_shard>(drop_loc);
            Dm.@new<obj_item_shard>(drop_loc);
            new_frame.state = FRAME_COMPUTER_STATE_WIRED;
        }
        else
            new_frame.state = FRAME_COMPUTER_STATE_GLASSED;
        new_frame.update_appearance(UPDATE_ICON_STATE);
        return null;
    }

    // tg: code/game/machinery/computer/_computer.dm:166
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        update_use_power(ACTIVE_POWER_USE);
        return dot;
    }

    // tg: code/game/machinery/computer/_computer.dm:171
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (!issilicon(ui.user))
            playsound(this, SFX_KEYBOARD_CLICKS, 10, TRUE, FALSE);
        return dot;
    }

    // tg: code/game/machinery/computer/_computer.dm:177
    public override void ui_close(mob user = null)
    {
        base.ui_close(user);
        update_use_power(IDLE_POWER_USE);
    }
}
