// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/netpod/_netpod.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the netpod's machine (its server link, states, observer jump and the scan toggle). The rest of
// the netpod is in this folder's other files. The var block is generated (D111).

// tg: code/modules/bitrunning/netpod/_netpod.dm:4
public partial class obj_machinery_netpod : obj_machinery
{
    // tg: code/modules/bitrunning/netpod/_netpod.dm:1  (#undef at :168)
    private const float BASE_DISCONNECT_DAMAGE = 40;
    // tg: code/modules/bitrunning/netpod/_netpod.dm:2  (#undef at :169)
    private const float SCANNING_TOGGLE_COOLDOWN = 5;

    // tg: code/modules/bitrunning/netpod/_netpod.dm:35
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();

        disconnect_damage = BASE_DISCONNECT_DAMAGE;
        find_server();

        RegisterSignal(this, COMSIG_ATOM_TAKE_DAMAGE, nameof(on_damage_taken));
        RegisterSignal(this, COMSIG_MACHINERY_POWER_LOST, nameof(on_power_loss));
        RegisterSignals(this, Dm.list(COMSIG_QDELETING, COMSIG_MACHINERY_BROKEN), nameof(on_broken));

        register_context();
        update_appearance();
    }

    // tg: code/modules/bitrunning/netpod/_netpod.dm:49
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);

        QDEL_LIST(cached_outfits);
        return dot;
    }

    // tg: code/modules/bitrunning/netpod/_netpod.dm:55
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        base.add_context(source, context, held_item, user);

        if (held_item == null)
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Select Outfit");
        else
        {
            if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER) && occupant == null && !Dm.truthy(state_open))
                context[SCREENTIP_CONTEXT_LMB] = Dm.truthy(panel_open) ? I18n.T("Close Panel") : I18n.T("Open Panel");

            if (Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR))
            {
                if (occupant == null)
                {
                    if (Dm.truthy(panel_open))
                        context[SCREENTIP_CONTEXT_LMB] = I18n.T("Deconstruct");
                    else
                        context[SCREENTIP_CONTEXT_LMB] = Dm.truthy(state_open) ? I18n.T("Close Cover") : I18n.T("Open Cover");
                }
                else
                    context[SCREENTIP_CONTEXT_LMB] = I18n.T("Break out");
            }
        }

        context[SCREENTIP_CONTEXT_ALT_LMB] = Dm.truthy(copy_body) ? I18n.T("Disable Scan") : I18n.T("Enable Scan");
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/modules/bitrunning/netpod/_netpod.dm:76
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;

        Dm.append(ref dot, span_notice(I18n.T("Its maintenance panel can be [] [].", EXAMINE_HINT(I18n.T("screwed")), Dm.truthy(panel_open) ? I18n.T("close") : I18n.T("open"))));
        if (occupant == null)
        {
            if (Dm.truthy(panel_open))
                Dm.append(ref dot, span_notice(I18n.T("It can be [] apart.", EXAMINE_HINT(I18n.T("pried")))));
            else
                Dm.append(ref dot, span_notice(I18n.T("Its hatch can be [] [].", EXAMINE_HINT(I18n.T("pried")), Dm.truthy(state_open) ? I18n.T("closed") : I18n.T("open"))));
        }

        if (server_ref?.resolve() == null)
        {
            Dm.append(ref dot, span_infoplain(I18n.T("It's not connected to anything.")));
            Dm.append(ref dot, span_infoplain(I18n.T("Netpods must be built within 4 tiles of a server.")));
            return dot;
        }

        if (!isobserver(user))
        {
            Dm.append(ref dot, span_infoplain(I18n.T("Drag yourself into the pod to engage the link.")));
            Dm.append(ref dot, span_infoplain(I18n.T("It has limited resuscitation capabilities. Remaining in the pod can heal some injuries.")));
            Dm.append(ref dot, span_infoplain(I18n.T("It has a security system that will alert the occupant if it is tampered with.")));
            if (Dm.truthy(copy_body))
                Dm.append(ref dot, span_infoplain(I18n.T("Occupant scanning is currently enabled, which will cause bit avatars to look like the occupant when first created.")));
            Dm.append(ref dot, span_infoplain(I18n.T("Alt-click to [] occupant scanning.", Dm.truthy(copy_body) ? I18n.T("disable") : I18n.T("enable"))));
        }

        if (occupant == null)
        {
            Dm.append(ref dot, span_infoplain(I18n.T("It's currently unoccupied.")));
            return dot;
        }

        Dm.append(ref dot, span_infoplain(I18n.T("It's currently occupied by [].", occupant)));

        if (isobserver(user))
        {
            Dm.append(ref dot, span_notice(I18n.T("As an observer, you can click this netpod to jump to its avatar.")));
            return dot;
        }

        Dm.append(ref dot, span_notice(I18n.T("It can be pried open with a crowbar, but its safety mechanisms will alert the occupant.")));
        return dot;
    }

    // tg: code/modules/bitrunning/netpod/_netpod.dm:112
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (!Dm.truthy(is_operational))
        {
            icon_state = Dm.str(base_icon_state);
            return await base.update_icon_state(__extra);
        }

        if (Dm.truthy(state_open))
        {
            icon_state = Dm.add(base_icon_state, "_open_active") as string;
            return await base.update_icon_state(__extra);
        }

        if (Dm.truthy(panel_open))
        {
            icon_state = Dm.add(base_icon_state, "_panel") as string;
            return await base.update_icon_state(__extra);
        }

        icon_state = Dm.add(base_icon_state, "_closed") as string;
        if (occupant != null)
            icon_state += "_active";

        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/bitrunning/netpod/_netpod.dm:132
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        atom target = dropped;
        mob_living_carbon player = user as mob_living_carbon;

        if (!iscarbon(player) || !Dm.truthy(is_operational) || !Dm.truthy(state_open) || player.buckled != null)
            return null;

        await close_machine(target as atom_movable);
        return null;
    }

    // tg: code/modules/bitrunning/netpod/_netpod.dm:141
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (!Dm.truthy(state_open) && user == occupant)
            await container_resist_act(user as mob_living);
        return dot;
    }

    // tg: code/modules/bitrunning/netpod/_netpod.dm:147
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        mob_dead_observer our_observer = user;
        atom our_target = avatar_ref?.resolve() as atom;
        if (our_target == null || !Dm.truthy(await our_observer.orbit(our_target)))
            return await base.attack_ghost(user);
        return null;
    }

    /// When the server is upgraded, drops brain damage a little
    // tg: code/modules/bitrunning/netpod/_netpod.dm:154
    [SignalHandler]
    public virtual void on_server_upgraded(obj_machinery_quantum_server source = null)
    {
        disconnect_damage = BASE_DISCONNECT_DAMAGE * (1 - source.servo_bonus);
    }

    // tg: code/modules/bitrunning/netpod/_netpod.dm:159
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        if (world.time < scanning_can_toggle)
            return CLICK_ACTION_BLOCKING;
        copy_body = Dm.not(copy_body);
        scanning_can_toggle = world.time + SCANNING_TOGGLE_COOLDOWN;
        playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        // As tg: the user is passed as the viewers' message, the text is the user's own (balloon_alert.dm's message, self_message)
        user.balloon_alert_to_viewers(user, I18n.T("scanning []", Dm.truthy(copy_body) ? I18n.T("enabled") : I18n.T("disabled")));
        return CLICK_ACTION_SUCCESS;
    }
}
