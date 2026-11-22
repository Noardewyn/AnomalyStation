// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/netpod/utils.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the netpod's link: the stasis healing, entering the matrix (a new or the kept avatar, the
// connection component), the server lookup, the sever and the disconnection's effects (the static, the flash, the brain damage).

// tg: code/modules/bitrunning/netpod/utils.dm:1
public partial class obj_machinery_netpod : obj_machinery
{
    /// Puts the occupant in netpod stasis, basically short-circuiting environmental conditions
    // tg: code/modules/bitrunning/netpod/utils.dm:2
    public virtual async DmTask add_healing(mob_living target = null)
    {
        if (target != occupant)
            return;

        await target.AddComponent(typeof(datum_component_netpod_healing), Dm.named("pod", this));
        target.playsound_local(this, Dm.resource("sound/effects/submerge.ogg"), 20, vary: TRUE);
        target.extinguish_mob();
        update_use_power(ACTIVE_POWER_USE);
    }

    /// Disconnects the occupant after a certain time so they aren't just hibernating in netpod stasis. A balance change
    // tg: code/modules/bitrunning/netpod/utils.dm:13
    public virtual async DmTask auto_disconnect()
    {
        if (occupant == null || Dm.truthy(state_open) || Dm.truthy(connected))
            return;

        mob player = occupant as mob;
        player.playsound_local(this, Dm.resource("sound/effects/splash.ogg"), 60, TRUE);
        to_chat(player, span_notice(I18n.T("The machine disconnects itself and begins to drain.")));
        await open_machine();
    }

    /// Handles occupant post-disconnection effects like damage, sounds, etc
    // tg: code/modules/bitrunning/netpod/utils.dm:24
    public virtual async DmTask disconnect_occupant(float? cause_damage = null)
    {
        cause_damage ??= FALSE;
        connected = FALSE;

        mob_living mob_occupant = occupant as mob_living;
        if (occupant == null || mob_occupant.stat == DEAD)
        {
            await open_machine();
            return;
        }

        mob_occupant.playsound_local(this, Dm.resource("sound/effects/magic/blink.ogg"), 25, TRUE);
        await mob_occupant.set_static_vision(2 * SECONDS);
        await mob_occupant.set_temp_blindness(1 * SECONDS);
        await mob_occupant.Paralyze(2 * SECONDS);

        if (!Dm.truthy(is_operational))
        {
            await open_machine();
            return;
        }

        float heal_time = 1;
        if (mob_occupant.health < mob_occupant.maxHealth)
            heal_time = (mob_occupant.stat + 2) * 5;
        _addtimer(Dm.@new<datum_callback>(this, nameof(auto_disconnect)), heal_time * SECONDS, Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_STOPPABLE), TIMER_DELETE_ME), file: "code/modules/bitrunning/netpod/utils.dm", line: 46);

        if (!Dm.truthy(cause_damage))
            return;

        await mob_occupant.flash_act(override_blindness_check: TRUE, visual: TRUE);
        await mob_occupant.adjust_organ_loss(ORGAN_SLOT_BRAIN, disconnect_damage);
        Waiting.Detach(mob_occupant.emote("scream"));
        to_chat(mob_occupant, span_danger(I18n.T("You've been forcefully disconnected from your avatar! Your thoughts feel scrambled!")));
    }

    /**
     * ### Enter Matrix
     * Finds any current avatars from this chair - or generates a new one
     *
     * New avatars cost 1 attempt, and this will eject if there's none left
     *
     * Connects the mind to the avatar if everything is ok
     */
    // tg: code/modules/bitrunning/netpod/utils.dm:63
    public virtual async DmTask enter_matrix()
    {
        mob_living_carbon_human neo = occupant as mob_living_carbon_human;
        if (!ishuman(neo) || neo.stat == DEAD || neo.mind == null)
        {
            balloon_alert(neo, I18n.T("invalid occupant."));
            return;
        }

        obj_machinery_quantum_server server = find_server() as obj_machinery_quantum_server;
        if (server == null)
        {
            balloon_alert(neo, I18n.T("no server connected!"));
            return;
        }

        datum_lazy_template_virtual_domain generated_domain = server.generated_domain;
        if (generated_domain == null || !Dm.truthy(server.is_ready))
        {
            balloon_alert(neo, I18n.T("nothing loaded!"));
            return;
        }

        balloon_alert(neo, I18n.T("establishing connection..."));

        // Prevent hand interactions during loading to stop smuggling exploits into virtual domain
        ADD_TRAIT(neo, TRAIT_HANDS_BLOCKED, TRAIT_GENERIC);

        object connection_successful = await do_after(neo, 2 * SECONDS, this);

        // Re-enable hand interactions after loading attempt
        REMOVE_TRAIT(neo, TRAIT_HANDS_BLOCKED, TRAIT_GENERIC);

        if (!Dm.truthy(connection_successful))
        {
            await open_machine();
            return;
        }

        mob_living_carbon current_avatar = avatar_ref?.resolve() as mob_living_carbon;
        if (current_avatar == null || IS_UNCONSCIOUS_OR_CRIT(current_avatar)) // We need a viable avatar
        {
            current_avatar = await server.start_new_connection(neo, netsuit) as mob_living_carbon;
            if (current_avatar == null)
            {
                balloon_alert(neo, I18n.T("out of bandwidth!"));
                return;
            }
        }

        await neo.set_static_vision(2 * SECONDS);
        await add_healing(occupant as mob_living);

        if (!Dm.truthy(validate_entry(neo, current_avatar)))
        {
            await open_machine();
            return;
        }

        await current_avatar.AddComponent(
            typeof(datum_component_avatar_connection),
            Dm.named("old_mind", neo.mind),
            Dm.named("old_body", neo),
            Dm.named("server", server),
            Dm.named("pod", this),
            Dm.named("help_text", generated_domain.help_text),
            Dm.named("copy_body", copy_body));

        connected = TRUE;
    }

    /// Finds a server and sets the server_ref
    // tg: code/modules/bitrunning/netpod/utils.dm:121
    public virtual object find_server()
    {
        obj_machinery_quantum_server server = server_ref?.resolve() as obj_machinery_quantum_server;
        if (server != null)
            return server;

        server = Dm.locate<obj_machinery_quantum_server>(oview(4, this));
        if (server == null)
            return null;

        server_ref = WEAKREF(server) as datum_weakref;
        RegisterSignal(server, COMSIG_MACHINERY_REFRESH_PARTS, nameof(on_server_upgraded));
        RegisterSignal(server, COMSIG_BITRUNNER_DOMAIN_COMPLETE, nameof(on_domain_complete));
        RegisterSignal(server, COMSIG_BITRUNNER_DOMAIN_SCRUBBED, nameof(on_domain_scrubbed));

        return server;
    }

    /// Severs the connection with the current avatar
    // tg: code/modules/bitrunning/netpod/utils.dm:139
    public virtual void sever_connection()
    {
        if (occupant == null || !Dm.truthy(connected))
            return;

        SEND_SIGNAL(COMSIG_BITRUNNER_NETPOD_SEVER, Dm.list(this));
    }

    /// Checks for cases to eject/fail connecting an avatar
    // tg: code/modules/bitrunning/netpod/utils.dm:147
    public virtual object validate_entry(mob_living neo = null, mob_living avatar = null)
    {
        // Very invalid
        if (QDELETED(neo) || QDELETED(avatar) || QDELETED(this) || !Dm.truthy(is_operational))
            return FALSE;

        // Invalid
        if (occupant != neo || neo.mind == null || IS_UNCONSCIOUS(neo) || avatar.stat == DEAD)
            return FALSE;

        return TRUE;
    }
}
