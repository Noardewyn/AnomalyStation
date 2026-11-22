// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/prisongate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J4, D298): the sentence timer of the prisoner's ID starts here. Meta places no gate; a map or a builder does.

// tg: code/game/machinery/prisongate.dm:6
public partial class obj_machinery_prisongate : obj_machinery
{
    private const float SPAM_CD = 3 * SECONDS;
    public const string ALLOW_EXIT = "allow_exit";
    public const string BLOCK_EXIT = "block_exit";

    private bool SpamFinished() => spam_cooldown_time <= Dm.num(world.time); // COOLDOWN_FINISHED, inline
    private void SpamStart() => spam_cooldown_time = Dm.num(world.time) + SPAM_CD; // COOLDOWN_START, inline

    // tg: code/game/machinery/prisongate.dm:34
    public override object power_change()
    {
        object dot = base.power_change();
        if (!Dm.truthy(get_area(this) is area powered_area ? powered_area.powered(power_channel) : null))
        {
            visible_message(span_notice(I18n.T("[] momentarily flickers before the hardlight barrier loses cohesion and dissipates into thin air!", this)));
            gate_active = FALSE;
            flick("prisongate_turningoff", this);
            icon_state = "prisongate_off";
            update_use_power(IDLE_POWER_USE);
        }
        else
        {
            gate_active = TRUE;
            visible_message(span_notice(I18n.T("[] whirrs back to life as its hardlight barrier fills the space between it.", this)));
            flick("prisongate_turningon", this);
            icon_state = "prisongate_on";
            update_use_power(ACTIVE_POWER_USE);
        }
        return dot;
    }

    // tg: code/game/machinery/prisongate.dm:49
    public override async DmTask<object> CanAllowThrough(atom_movable gate_toucher = null, object border_dir = null)
    {
        await base.CanAllowThrough(gate_toucher, border_dir);
        if (!iscarbon(gate_toucher))
        {
            if (!isstructure(gate_toucher))
                return TRUE;
            foreach (mob_living stowaway in Dm.each<mob_living>(gate_toucher.contents)) //nice try bub
            {
                if (SpamFinished())
                {
                    await say(I18n.T("Stowaway detected in internal contents. Access denied."));
                    playsound(this, Dm.resource("sound/machines/buzz/buzz-two.ogg"), 50, FALSE);
                    SpamStart();
                }
                return FALSE;
            }
        }
        mob_living_carbon the_toucher = gate_toucher as mob_living_carbon;
        if (!Dm.truthy(gate_active))
            return TRUE;
        // a structure with no stowaway falls through to here in tg, where allowed() and the rest read a structure as a mob; with the barrier up a crate is stopped by the last line, kept
        if (Dm.truthy(await allowed__proc(gate_toucher as mob)))
        {
            if (SpamFinished())
            {
                await say(I18n.T("Brig clearance detected. Access granted."));
                playsound(this, Dm.resource("sound/machines/chime.ogg"), 50, FALSE);
                SpamStart();
            }
            return TRUE;
        }
        else if (the_toucher?.pulledby != null && Dm.truthy(await allowed__proc(the_toucher.pulledby as mob)))
        {
            if (SpamFinished())
            {
                await say(I18n.T("Brig escort detected. Access granted."));
                playsound(this, Dm.resource("sound/machines/chime.ogg"), 50, FALSE);
                SpamStart();
            }
            return TRUE;
        }

        if (the_toucher == null)
            return FALSE;

        foreach (obj_item_card_id_advanced_prisoner prison_id in Dm.each<obj_item_card_id_advanced_prisoner>(the_toucher.get_all_contents()))
        {
            switch (await allow_prisoner_id(prison_id))
            {
                case ALLOW_EXIT:
                    return TRUE;
                case BLOCK_EXIT:
                    return FALSE;
            }
        }

        if (SpamFinished())
        {
            to_chat(the_toucher, span_warning(I18n.T("You try to push through the hardlight barrier with little effect.")));
            SpamStart();
        }
        return FALSE;
    }

    // tg: code/game/machinery/prisongate.dm:90
    public virtual async DmTask<string> allow_prisoner_id(obj_item_card_id_advanced_prisoner prison_id = null)
    {
        if (!Dm.truthy(prison_id.timed))
            return null;
        if (Dm.truthy(prison_id.time_to_assign))
        {
            await say(I18n.T("Prison ID with active sentence detected. Please enjoy your stay in our corporate rehabilitation center, []!", prison_id.registered_name));
            playsound(this, Dm.resource("sound/machines/chime.ogg"), 50, FALSE);
            prison_id.time_left = Dm.num(prison_id.time_to_assign);
            prison_id.time_to_assign = null;
            await prison_id.start_timer();
            return ALLOW_EXIT;
        }
        if (Dm.num(prison_id.time_left) <= 0)
        {
            await say(I18n.T("Prison ID with served sentence detected. Access granted."));
            prison_id.timed = FALSE; //disables the id check from earlier so you can't just throw it back into perma for mass escapes
            playsound(this, Dm.resource("sound/machines/chime.ogg"), 50, FALSE);
            return ALLOW_EXIT;
        }
        if (SpamFinished())
        {
            await say(I18n.T("Prison ID with ongoing sentence detected. Access denied."));
            playsound(this, Dm.resource("sound/machines/buzz/buzz-two.ogg"), 50, FALSE);
            SpamStart();
        }
        return BLOCK_EXIT;
    }
}

// tg: code/game/machinery/prisongate.dm:112
public partial class obj_machinery_prisongate_labour : obj_machinery_prisongate
{
    // tg: code/game/machinery/prisongate.dm:115
    public override async DmTask<string> allow_prisoner_id(obj_item_card_id_advanced_prisoner prison_id = null)
    {
        if (!Dm.truthy(prison_id.goal))
            return null;
        if (Dm.num(prison_id.points) >= Dm.num(prison_id.goal))
        {
            if (spam_cooldown_time <= Dm.num(world.time))
            {
                await say(I18n.T("All points served. Access granted."));
                playsound(this, Dm.resource("sound/machines/chime.ogg"), 50, FALSE);
                spam_cooldown_time = Dm.num(world.time) + 3 * SECONDS;
            }
            return ALLOW_EXIT;
        }
        if (spam_cooldown_time <= Dm.num(world.time))
        {
            await say(I18n.T("[] point\\s remaining on sentence. Access denied.", Dm.num(prison_id.goal) - Dm.num(prison_id.points)));
            playsound(this, Dm.resource("sound/machines/buzz/buzz-two.ogg"), 50, FALSE);
            spam_cooldown_time = Dm.num(world.time) + 3 * SECONDS;
        }
        return BLOCK_EXIT;
    }
}
