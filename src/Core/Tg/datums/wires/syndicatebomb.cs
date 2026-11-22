// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/syndicatebomb.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G2 (D316): the syndicate bomb's wires: activate, delay, proceed, unbolt, the boom wires its timer
// adds and the duds. The bombs' pictures in the chat lines are chat icons (not-translated section 76); the admin message is a
// row.

// tg: code/datums/wires/syndicatebomb.dm:1
public partial class datum_wires_syndicatebomb : datum_wires
{
    // tg: code/datums/wires/syndicatebomb.dm:6
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        setup_wires();
        await base.New(__extra);
    }

    /**
     * Handles setting up the wires list.
     *
     * * num_booms: The number of boom wires to add.
     * * num_duds: The number of dud wires to add.
     */
    // tg: code/datums/wires/syndicatebomb.dm:16
    public virtual void setup_wires(float? num_booms = null, float? num_duds = null)
    {
        num_booms ??= 2;
        num_duds ??= 0;
        wires = Dm.list(
            WIRE_ACTIVATE,
            WIRE_DELAY,
            WIRE_PROCEED,
            WIRE_UNBOLT);
        add_booms(num_booms);
        add_duds(num_duds);
        shuffle_wires();
    }

    /// Adds a number of wires which will explode the bomb if pulse/cut
    // tg: code/datums/wires/syndicatebomb.dm:28
    public virtual void add_booms(float? booms = null)
    {
        booms ??= 2;
        for (float i = 1; i <= booms; i++)
            wires.Add(Dm.text("[] []", WIRE_BOOM, i));
    }

    // tg: code/datums/wires/syndicatebomb.dm:32
    public override bool interactable(mob user)
    {
        obj_machinery_syndicatebomb bomb = (obj_machinery_syndicatebomb)holder;
        return base.interactable(user) && Dm.truthy(bomb.open_panel);
    }

    /// Translates numbered boom wires into WIRE_BOOM.
    // tg: code/datums/wires/syndicatebomb.dm:37
    public virtual object parse_wire(object wire = null)
    {
        return Dm.truthy(findtext(wire, WIRE_BOOM)) ? WIRE_BOOM : wire;
    }

    /// Checks if the bomb, if detonated, is dangerous to the user.
    // tg: code/datums/wires/syndicatebomb.dm:41
    public virtual object is_dangerous()
    {
        obj_machinery_syndicatebomb bomb = (obj_machinery_syndicatebomb)holder;
        if (bomb.payload == null)
            return FALSE;
        if (istype(bomb.payload, typeof(obj_item_bombcore_training)))
            return FALSE;
        return TRUE;
    }

    // tg: code/datums/wires/syndicatebomb.dm:49
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        obj_machinery_syndicatebomb bomb = (obj_machinery_syndicatebomb)holder;
        object sw__1 = parse_wire(wire);
        // [icon2html(bomb, viewers(holder))] before every line: the bomb's picture in the chat line (chat icons)
        if (Dm.eq(sw__1, WIRE_BOOM))
        {
            if (!Dm.truthy(bomb.active))
            {
                holder.visible_message(span_notice(I18n.T("Nothing happens.")));
                return;
            }

            holder.visible_message(span_danger(I18n.T("An alarm sounds! It's go-")));
            bomb.explode_now = TRUE;
            if (Dm.truthy(is_dangerous()))
            {
                tell_admins(bomb, user, "detonated via boom wire");
                if (isliving(user))
                    add_memory_in_range(bomb, 7, typeof(datum_memory_bomb_defuse_failure), Dm.named("protagonist", user), Dm.named("antagonist", bomb));
            }
        }
        else if (Dm.eq(sw__1, WIRE_UNBOLT))
        {
            holder.visible_message(span_notice(I18n.T("The bolts spin in place for a moment.")));
        }
        else if (Dm.eq(sw__1, WIRE_DELAY))
        {
            if (Dm.truthy(bomb.delayedbig))
            {
                holder.visible_message(span_notice(I18n.T("Nothing happens.")));
                return;
            }

            holder.visible_message(span_notice(I18n.T("The bomb chirps.")));
            playsound(bomb, Dm.resource("sound/machines/chime.ogg"), 30, TRUE);
            bomb.detonation_timer = Dm.num(bomb.detonation_timer) + 30 * SECONDS;
            if (Dm.truthy(bomb.active))
                bomb.delayedbig = TRUE;
        }
        else if (Dm.eq(sw__1, WIRE_PROCEED))
        {
            holder.visible_message(span_danger(I18n.T("The bomb buzzes ominously!")));
            playsound(bomb, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 30, TRUE);
            float seconds = Dm.num(bomb.seconds_remaining());
            if (seconds >= 61) // Long fuse bombs can suddenly become more dangerous if you tinker with them.
                bomb.detonation_timer = world.time + 60 * SECONDS;
            else if (seconds >= 21)
                bomb.detonation_timer = Dm.num(bomb.detonation_timer) - 10 * SECONDS;
            else if (seconds >= 11) // Both to prevent negative timers and to have a little mercy.
                bomb.detonation_timer = world.time + 10 * SECONDS;
        }
        else if (Dm.eq(sw__1, WIRE_ACTIVATE))
        {
            if (!Dm.truthy(bomb.active))
            {
                holder.visible_message(span_danger(I18n.T("You hear the bomb start ticking!")));
                bomb.activate();
                bomb.update_appearance();
            }
            else if (Dm.truthy(bomb.delayedlittle))
            {
                holder.visible_message(span_notice(I18n.T("Nothing happens.")));
            }
            else
            {
                holder.visible_message(span_notice(I18n.T("The bomb seems to hesitate for a moment.")));
                bomb.detonation_timer = Dm.num(bomb.detonation_timer) + 100;
                bomb.delayedlittle = TRUE;
            }
        }
    }

    // tg: code/datums/wires/syndicatebomb.dm:101
    public override async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        obj_machinery_syndicatebomb bomb = (obj_machinery_syndicatebomb)holder;
        object sw__1 = parse_wire(wire);
        // [icon2html(bomb, viewers(holder))] before every line: the bomb's picture in the chat line (chat icons)
        if (Dm.eq(sw__1, WIRE_BOOM))
        {
            if (Dm.truthy(mend) || !Dm.truthy(bomb.active))
                return;
            holder.visible_message(span_danger(I18n.T("An alarm sounds! It's go-")));
            bomb.explode_now = TRUE;
            if (Dm.truthy(is_dangerous()))
            {
                tell_admins(bomb, source, "detonated via boom wire");
                if (isliving(source))
                    add_memory_in_range(bomb, 7, typeof(datum_memory_bomb_defuse_failure), Dm.named("protagonist", source), Dm.named("antagonist", bomb));
            }
        }
        else if (Dm.eq(sw__1, WIRE_UNBOLT))
        {
            if (Dm.truthy(mend) || !Dm.truthy(bomb.anchored))
                return;
            holder.visible_message(span_notice(I18n.T("The bolts lift out of the ground!")));
            playsound(bomb, Dm.resource("sound/effects/stealthoff.ogg"), 30, TRUE);
            await bomb.set_anchored(FALSE);
        }
        else if (Dm.eq(sw__1, WIRE_PROCEED))
        {
            if (Dm.truthy(mend) || !Dm.truthy(bomb.active))
                return;
            holder.visible_message(span_danger(I18n.T("The digital display on the device deactivates.")));
            bomb.examinable_countdown = FALSE;
        }
        else if (Dm.eq(sw__1, WIRE_ACTIVATE))
        {
            if (Dm.truthy(mend) || !Dm.truthy(bomb.active))
                return;
            object bomb_time_left = bomb.seconds_remaining();
            holder.visible_message(span_notice(I18n.T("The timer stops! The bomb has been defused!")));
            bomb.defuse();
            if (Dm.truthy(is_dangerous()))
            {
                tell_admins(bomb, source, "defused");
                if (isliving(source))
                    add_memory_in_range(bomb, 7, typeof(datum_memory_bomb_defuse_success), Dm.named("protagonist", source), Dm.named("antagonist", bomb), Dm.named("bomb_time_left", bomb_time_left));
            }
        }
    }

    // tg: code/datums/wires/syndicatebomb.dm:138
    public virtual void tell_admins(obj_machinery_syndicatebomb bomb = null, atom source = null, object what_happened = null)
    {
        turf bombloc = get_turf(bomb);
        log_game(Dm.text("\\A [] was [] at [] by [].", bomb, what_happened, AREACOORD(bombloc), (object)source ?? "nothing(?)"));
        // message_admins("\A [bomb] was [what_happened] at [ADMIN_VERBOSEJMP(bombloc)] by [source ? ADMIN_LOOKUPFLW(source) : "nothing(?)"]."):
        // the admin message (admin tooling, M10+)
        if (isliving(source))
            log_combat(source, bomb, what_happened);
        return;
    }
}
