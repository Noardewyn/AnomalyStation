// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/airlock.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only New (M3 P3: the airlock's wire set), on_cut (M5 slice 6: on_break cuts every wire) and on_pulse (M7 EMP: an EMP
// pulses the wires, wires/_wires.dm:230) with the airlock's reset_ai_wire. The hacking (interact,
// interactable, get_status and the UI) uses the shared wire interface. The var block is generated (D111).

// tg: code/datums/wires/airlock.dm:1
[DmPath("/datum/wires/airlock")]
public partial class datum_wires_airlock : datum_wires
{
    // tg: code/datums/wires/airlock.dm:45
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        wires = Dm.list(
            WIRE_AI,
            WIRE_BACKUP1,
            WIRE_BACKUP2,
            WIRE_BOLTS,
            WIRE_IDSCAN,
            WIRE_FEEDBACK,
            WIRE_OPEN,
            WIRE_POWER1,
            WIRE_POWER2,
            WIRE_SAFETY,
            WIRE_SHOCK,
            WIRE_TIMING,
            WIRE_UNRESTRICTED_EXIT,
            WIRE_ZAP1,
            WIRE_ZAP2
            );
        add_duds(2);
        await base.New(Dm.forward(__extra, holder));
    }
    // tg: code/datums/wires/airlock.dm:104  (set waitfor = FALSE, inherited from /datum/wires/on_pulse's overrides: the body may
    // sleep through run_animation, so this is the wrapper of waiting-model.md 5.4)
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        Waiting.WaitforFalse(on_pulse__body(wire));
    }

    private async DmTask on_pulse__body(object wire)
    {
        obj_machinery_door_airlock A = (obj_machinery_door_airlock)holder;
        object sw = wire;
        if (Dm.eq(sw, WIRE_POWER1) || Dm.eq(sw, WIRE_POWER2)) // Pulse to lose power.
        {
            A.loseMainPower();
        }
        else if (Dm.eq(sw, WIRE_BACKUP1) || Dm.eq(sw, WIRE_BACKUP2)) // Pulse to lose backup power.
        {
            A.loseBackupPower();
        }
        else if (Dm.eq(sw, WIRE_OPEN)) // Pulse to open door (only works not emagged and ID wire is cut or no access is required).
        {
            if (Dm.truthy(Dm.bitand(A.obj_flags, EMAGGED)))
                return;
            if (!Dm.truthy(A.requiresID()) || Dm.truthy(A.check_access(null)))
            {
                if (Dm.truthy(A.density))
                    Waiting.Detach(A.open(1)); // INVOKE_ASYNC(A, TYPE_PROC_REF(/obj/machinery/door/airlock, open), 1)
                else
                    Waiting.Detach(A.close(1)); // INVOKE_ASYNC(A, TYPE_PROC_REF(/obj/machinery/door/airlock, close), 1)
            }
        }
        else if (Dm.eq(sw, WIRE_BOLTS)) // Pulse to toggle bolts (but only raises if power is on).
        {
            if (!Dm.truthy(A.locked))
            {
                A.bolt();
            }
            else
            {
                if (Dm.truthy(A.hasPower()))
                    A.unbolt();
            }
            A.update_appearance();
        }
        else if (Dm.eq(sw, WIRE_IDSCAN)) // Pulse to disable emergency access and flash the red lights.
        {
            if (Dm.truthy(A.hasPower()) && Dm.truthy(A.density))
            {
                await A.run_animation(DOOR_DENY_ANIMATION);
                if (Dm.truthy(A.emergency))
                {
                    A.emergency = FALSE;
                    A.update_appearance();
                }
            }
        }
        else if (Dm.eq(sw, WIRE_AI)) // Pulse to disable WIRE_AI control for 10 ticks (follows same rules as cutting).
        {
            if (Dm.eq(A.aiControlDisabled, AI_WIRE_NORMAL))
                A.aiControlDisabled = AI_WIRE_DISABLED;
            else if (Dm.eq(A.aiControlDisabled, AI_WIRE_DISABLED_HACKED))
                A.aiControlDisabled = AI_WIRE_HACKED;
            _addtimer(Dm.@new<datum_callback>(A, nameof(obj_machinery_door_airlock.reset_ai_wire)), 1 * SECONDS, file: "code/datums/wires/airlock.dm", line: 138);
        }
        else if (Dm.eq(sw, WIRE_SHOCK)) // Pulse to shock the door for 10 ticks.
        {
            if (!Dm.truthy(A.secondsElectrified))
                await A.set_electrified(MACHINE_DEFAULT_ELECTRIFY_TIME, usr);
            await A.shock__proc(usr as mob_living, 100);
        }
        else if (Dm.eq(sw, WIRE_SAFETY))
        {
            A.safe = !Dm.truthy(A.safe) ? TRUE : FALSE;
            if (!Dm.truthy(A.density))
                await A.close();
        }
        else if (Dm.eq(sw, WIRE_TIMING))
        {
            A.normalspeed = !Dm.truthy(A.normalspeed) ? TRUE : FALSE;
        }
        else if (Dm.eq(sw, WIRE_FEEDBACK))
        {
            A.feedback = !Dm.truthy(A.feedback) ? TRUE : FALSE;
            A.update_appearance();
        }
        else if (Dm.eq(sw, WIRE_UNRESTRICTED_EXIT)) // Pulse to switch the direction around by 180 degrees (North goes to South, East goes to West, vice-versa)
        {
            if (!Dm.truthy(A.unres_latch)) //only works if the "latch" is installed (a variable that we assign to the door either upon creation of a door with unrestricted directions or if an unrestricted helper is added to a door in mapping)
                return;
            A.unres_sides = REVERSE_DIR(A.unres_sides);
            A.update_appearance();
        }
    }

    // tg: code/datums/wires/airlock.dm:164
    public override async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        mend ??= FALSE;
        obj_machinery_door_airlock A = (obj_machinery_door_airlock)holder;
        object sw = wire;
        if (Dm.eq(sw, WIRE_POWER1) || Dm.eq(sw, WIRE_POWER2)) // Cut to lose power, repair all to gain power.
        {
            if (Dm.truthy(mend) && !Dm.truthy(is_cut(WIRE_POWER1)) && !Dm.truthy(is_cut(WIRE_POWER2)))
                A.regainMainPower();
            else
                A.loseMainPower();
            await A.shock__proc(usr as mob_living, 50);
        }
        else if (Dm.eq(sw, WIRE_BACKUP1) || Dm.eq(sw, WIRE_BACKUP2)) // Cut to lose backup power, repair all to gain backup power.
        {
            if (Dm.truthy(mend) && !Dm.truthy(is_cut(WIRE_BACKUP1)) && !Dm.truthy(is_cut(WIRE_BACKUP2)))
                A.regainBackupPower();
            else
                A.loseBackupPower();
            await A.shock__proc(usr as mob_living, 50);
        }
        else if (Dm.eq(sw, WIRE_BOLTS)) // Cut to engage bolts, mend does nothing.
        {
            if (!Dm.truthy(mend))
                A.bolt();
        }
        else if (Dm.eq(sw, WIRE_AI)) // Cut to disable WIRE_AI control, mend to re-enable.
        {
            if (Dm.truthy(mend))
            {
                if (Dm.eq(A.aiControlDisabled, AI_WIRE_DISABLED)) // 0 = normal, 1 = locked out, 2 = overridden by WIRE_AI, -1 = previously overridden by WIRE_AI
                    A.aiControlDisabled = AI_WIRE_NORMAL;
                else if (Dm.eq(A.aiControlDisabled, AI_WIRE_HACKED))
                    A.aiControlDisabled = AI_WIRE_DISABLED_HACKED;
            }
            else
            {
                if (Dm.eq(A.aiControlDisabled, AI_WIRE_NORMAL))
                    A.aiControlDisabled = AI_WIRE_DISABLED;
                else if (Dm.eq(A.aiControlDisabled, AI_WIRE_DISABLED_HACKED))
                    A.aiControlDisabled = AI_WIRE_HACKED;
            }
        }
        else if (Dm.eq(sw, WIRE_SHOCK)) // Cut to shock the door, mend to unshock.
        {
            if (source != null)
                log_combat(source, A, Dm.text("[] shocking for", Dm.truthy(mend) ? "disabled" : "enabled"));
            if (Dm.truthy(mend))
            {
                if (Dm.truthy(A.secondsElectrified))
                    await A.set_electrified(MACHINE_NOT_ELECTRIFIED, usr);
            }
            else
            {
                if (!Dm.eq(A.secondsElectrified, MACHINE_ELECTRIFIED_PERMANENT))
                    await A.set_electrified(MACHINE_ELECTRIFIED_PERMANENT, usr);
                await A.shock__proc(usr as mob_living, 100);
            }
        }
        else if (Dm.eq(sw, WIRE_SAFETY)) // Cut to disable safeties, mend to re-enable.
        {
            A.safe = mend.Value;
            if (source != null)
                log_combat(source, A, Dm.text("[] door safeties for", Dm.truthy(mend) ? "enabled" : "disabled"));
        }
        else if (Dm.eq(sw, WIRE_TIMING)) // Cut to disable auto-close, mend to re-enable.
        {
            A.autoclose = mend.Value;
            if (Dm.truthy(A.autoclose) && !Dm.truthy(A.density))
                Waiting.Detach(A.close());
        }
        else if (Dm.eq(sw, WIRE_FEEDBACK)) // Cut to disable lights and sounds, mend to re-enable.
        {
            A.feedback = mend.Value;
            A.update_appearance();
        }
        else if (Dm.eq(sw, WIRE_ZAP1) || Dm.eq(sw, WIRE_ZAP2)) // Ouch.
        {
            await A.shock__proc(usr as mob_living, 50);
        }
        else if (Dm.eq(sw, WIRE_UNRESTRICTED_EXIT)) // If this wire is cut, the unrestricted helper goes away. If you mend it, it'll go "haywire" and pick a new direction at random. Might have to cut/mend a time or two to get the direction you want.
        {
            if (!Dm.truthy(A.unres_latch)) //only works if the "latch" is installed (a variable that we assign to the door either upon creation of a door with unrestricted directions, or if an unrestricted helper is added to a door in mapping)
                return;
            if (Dm.truthy(mend))
            {
                A.unres_sides = Dm.num(pick(NORTH, SOUTH, EAST, WEST));
                A.update_appearance();
            }
            else
            {
                A.unres_sides = NONE;
                A.update_appearance();
            }
        }
    }
}

// tg: code/datums/wires/airlock.dm:158 (interleaved types, rules 1.9; M7 EMP)
public partial class obj_machinery_door_airlock
{
    // tg: code/datums/wires/airlock.dm:158
    public virtual void reset_ai_wire()
    {
        if (Dm.eq(aiControlDisabled, AI_WIRE_DISABLED))
            aiControlDisabled = AI_WIRE_NORMAL;
        else if (Dm.eq(aiControlDisabled, AI_WIRE_HACKED))
            aiControlDisabled = AI_WIRE_DISABLED_HACKED;
    }
}

public partial class datum_wires_airlock
{
    public override bool interactable(mob user)
        => base.interactable(user) && holder is obj_machinery_door_airlock door && Dm.truthy(door.panel_open) && !Dm.truthy(door.is_secure())
            && (HAS_TRAIT(user, TRAIT_SILICON_ACCESS) || !Dm.truthy(door.isElectrified()) || !Dm.truthy(door.hasPower())
                || user is mob_living_carbon carbon && !Dm.truthy(carbon.should_electrocute(get_area(door))));

    public override async DmTask<object> interact(mob user)
    {
        var door = (obj_machinery_door_airlock)holder;
        if (!HAS_TRAIT(user, TRAIT_SILICON_ACCESS) && Dm.truthy(door.isElectrified()) && Dm.truthy(await door.shock__proc(user as mob_living, 100)))
            return FALSE;
        return await base.interact(user);
    }

    public override DmList get_status()
    {
        var door = (obj_machinery_door_airlock)holder;
        return Dm.list(Dm.truthy(door.locked) ? "The door bolts have engaged!" : "The door bolts have disengaged.",
            Dm.truthy(door.hasPower()) ? "The test light is on." : "The test light is off.",
            Dm.truthy(door.safe) ? "The check wiring light is off." : "The check wiring light is on.");
    }
}
