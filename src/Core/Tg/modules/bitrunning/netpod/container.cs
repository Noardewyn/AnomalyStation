// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/netpod/container.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the netpod as a container: its hatch's opening and closing (the tram doors' sounds, the
// opening and closing flicks), the connection on close, and the pry.

// tg: code/modules/bitrunning/netpod/container.dm:1
public partial class obj_machinery_netpod : obj_machinery
{
    // tg: code/modules/bitrunning/netpod/container.dm:1
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (!Dm.truthy(state_open) && gone == occupant)
            await container_resist_act(gone as mob_living);
        return dot;
    }

    // tg: code/modules/bitrunning/netpod/container.dm:7
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (!Dm.truthy(state_open))
            await container_resist_act(user);
        return null;
    }

    // tg: code/modules/bitrunning/netpod/container.dm:12
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        user.visible_message(span_notice(I18n.T("[] emerges from []!", occupant, this)),
            span_notice(I18n.T("You climb out of []!", this)),
            span_notice(I18n.T("With a hiss, you hear a machine opening.")));
        await open_machine();
        return null;
    }

    // tg: code/modules/bitrunning/netpod/container.dm:19
    public override async DmTask<object> open_machine(float? drop = null, float? density_to_set = null)
    {
        drop ??= TRUE;
        density_to_set ??= FALSE;
        playsound(this, Dm.resource("sound/machines/tram/tramopen.ogg"), 60, TRUE, frequency: 65000);
        flick(Dm.text("[]_opening", base_icon_state), this);
        SEND_SIGNAL(COMSIG_BITRUNNER_NETPOD_OPENED, Dm.list(this));
        update_use_power(IDLE_POWER_USE);

        return await base.open_machine(drop, density_to_set);
    }

    // tg: code/modules/bitrunning/netpod/container.dm:28
    public override async DmTask<object> close_machine(atom_movable target = null, float? density_to_set = null)
    {
        mob user = target as mob;
        density_to_set ??= TRUE;
        if (!Dm.truthy(state_open) || Dm.truthy(panel_open) || !Dm.truthy(is_operational) || !iscarbon(user))
            return null;

        playsound(this, Dm.resource("sound/machines/tram/tramclose.ogg"), 60, TRUE, frequency: 65000);
        flick(Dm.text("[]_closing", base_icon_state), this);
        await base.close_machine(target, density_to_set);

        await enter_matrix();
        return null;
    }

    // tg: code/modules/bitrunning/netpod/container.dm:39
    public override async DmTask<object> default_pry_open(mob_living user = null, obj_item crowbar = null, float? close_after_pry = null, float? open_density = null, float? closed_density = null, float? deconstruct_on_fail = null)
    {
        close_after_pry ??= FALSE;
        open_density ??= FALSE;
        closed_density ??= TRUE;
        deconstruct_on_fail ??= FALSE;
        if (!iscarbon(occupant))
        {
            if (!Dm.truthy(state_open))
            {
                if (Dm.truthy(panel_open))
                    return Dm.truthy(deconstruct_on_fail) ? await default_deconstruction_crowbar(user, crowbar) : NONE;
                await open_machine();
            }
            else
                shut_pod();

            return ITEM_INTERACT_SUCCESS;
        }

        user.visible_message(
            span_danger(I18n.T("[] starts prying open []!", user, this)),
            span_notice(I18n.T("You start to pry open [].", this)),
            span_notice(I18n.T("You hear loud prying on metal.")));
        playsound(this, Dm.resource("sound/machines/airlock/airlock_alien_prying.ogg"), 100, TRUE);

        SEND_SIGNAL(COMSIG_BITRUNNER_CROWBAR_ALERT, Dm.list(this, user));

        if (Dm.truthy(await crowbar.use_tool(this, user, 15 * SECONDS, volume: 50)))
        {
            if (!Dm.truthy(state_open))
            {
                sever_connection();
                await open_machine();
            }
        }

        return ITEM_INTERACT_SUCCESS;
    }

    /// Closes the machine without shoving in an occupant
    // tg: code/modules/bitrunning/netpod/container.dm:68
    public virtual void shut_pod()
    {
        state_open = FALSE;
        playsound(this, Dm.resource("sound/machines/tram/tramclose.ogg"), 60, TRUE, frequency: 65000);
        flick(Dm.text("[]_closing", base_icon_state), this);
        set_density(TRUE);

        update_appearance();
    }
}
