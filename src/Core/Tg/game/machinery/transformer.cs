// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/transformer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI8, D326: the malfunctioning AI's Robotic Factory). The var block is generated (D111).

// tg: code/game/machinery/transformer.dm:1
public partial class obj_machinery_transformer : obj_machinery
{
    // tg: code/game/machinery/transformer.dm:27
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.@new<obj_machinery_conveyor_auto>(locate(x - 1, y, z), WEST);
        Dm.@new<obj_machinery_conveyor_auto>(loc, WEST);
        Dm.@new<obj_machinery_conveyor_auto>(locate(x + 1, y, z), WEST);
        countdown = Dm.@new<obj_effect_countdown_transformer>(this);
        countdown.start();
        return dot;
    }

    // tg: code/game/machinery/transformer.dm:35
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(cooldown) && (issilicon(user) || isobserver(user)))
            Dm.append(ref dot, I18n.T("It will be ready in [].", DisplayTimeText(Dm.num(cooldown_timer) - world.time)));
        return dot;
    }

    // tg: code/game/machinery/transformer.dm:40
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref countdown);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/transformer.dm:44
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN | NOPOWER)) || cooldown == 1)
            icon_state = "separator-AO0";
        else
            icon_state = Dm.initial(GetType(), (atom a) => a.icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/transformer.dm:51
    // set waitfor = FALSE (inherited from /atom/Bumped): do_transform sleeps, so this is the wrapper of waiting-model.md 5.4
    public override object Bumped(atom_movable entering_thing = null)
    {
        return Waiting.WaitforFalse(Bumped__body(entering_thing), null);
    }

    private async DmTask<object> Bumped__body(atom_movable entering_thing)
    {
        if (Dm.truthy(cooldown))
            return null;

        // Crossed didn't like people lying down.
        if (entering_thing is mob_living_carbon_human victim)
        {
            // Only humans can enter from the west side, while lying down.
            float move_dir = get_dir(loc, entering_thing.loc);
            if ((Dm.truthy(transform_standing) || victim.body_position == LYING_DOWN) && move_dir == EAST)
            {
                await entering_thing.forceMove(drop_location() as atom);
                await do_transform(victim);
            }
        }
        return null;
    }

    // tg: code/game/machinery/transformer.dm:64
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        // Allows items to go through to stop them from blocking the conveyor belt.
        if (!ishuman(mover))
        {
            if (get_dir(this, mover) == EAST)
                return dot;
        }
        return FALSE;
    }

    // tg: code/game/machinery/transformer.dm:72
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(cooldown) && Dm.num(cooldown_timer) <= world.time)
        {
            cooldown = FALSE;
            update_appearance();
        }
        return null;
    }

    // tg: code/game/machinery/transformer.dm:77
    public virtual async DmTask do_transform(mob_living_carbon_human victim = null)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN | NOPOWER)))
            return;

        if (Dm.truthy(cooldown))
            return;

        if (!Dm.truthy(transform_dead) && victim.stat == DEAD)
        {
            playsound(loc, "sound/machines/buzz/buzz-sigh.ogg", 50, FALSE);
            return;
        }

        // Activate the cooldown
        cooldown = TRUE;
        cooldown_timer = world.time + cooldown_duration;
        update_appearance();

        playsound(loc, "sound/items/tools/welder.ogg", 50, TRUE);
        await victim.emote("scream"); // It is painful
        await victim.adjust_brute_loss(max(0, 80 - Dm.num(victim.get_brute_loss()))); // Hurt the human, don't try to kill them though.

        // Sleep for a couple of ticks to allow the human to see the pain
        await sleep(0.5f * SECONDS);

        await use_energy(active_power_usage); // Use a lot of power.
        var new_borg = await victim.Robotize() as mob_living_silicon_robot;
        new_borg.cell = Dm.@new<obj_item_stock_parts_power_store_cell_super>(new_borg);

        // So he can't jump out the gate right away.
        await new_borg.SetLockdown();
        if (master_ai != null && new_borg.connected_ai != master_ai)
        {
            await new_borg.set_connected_ai(master_ai);
            await new_borg.lawsync();
            new_borg.lawupdate = TRUE;
            log_silicon(Dm.text("[] resynced to []", key_name(new_borg), key_name(master_ai)));
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(unlock_new_robot), new_borg), 5 * SECONDS, file: "code/game/machinery/transformer.dm", line: 112);
    }

    // tg: code/game/machinery/transformer.dm:114
    public virtual async DmTask unlock_new_robot(mob_living_silicon_robot new_borg = null)
    {
        playsound(loc, "sound/machines/ping.ogg", 50, FALSE);
        await sleep(3 * SECONDS);
        if (new_borg != null)
        {
            await new_borg.SetLockdown(FALSE);
            new_borg.notify_ai(AI_NOTIFICATION_NEW_BORG);
        }
    }
}
