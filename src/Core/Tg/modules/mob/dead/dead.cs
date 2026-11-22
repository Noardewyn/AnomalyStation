// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/dead/dead.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the whole file but the Server Hop verb (the ghosts, 2026-10-16, D151): server_hop (:36-77, a jump to another
// server of the cross-server config) is a recorded leaf, the port runs one server (docs/ai/specs/not-translated.md section 16).

//Dead mobs can exist whenever. This is needful

// tg: code/modules/mob/dead/dead.dm:5
[DmPath("/mob/dead")]
public partial class mob_dead : mob
{
    // INITIALIZE_IMMEDIATE(/mob/dead) (dead.dm:3; __DEFINES/subsystems.dm:96, expanded inline, rules 11.1 (b), (d)): the type
    // and its subtypes always call Initialize in New
    // tg: code/modules/mob/dead/dead.dm:3
    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        object loc = Dm.arg(__extra, 0);
        await base.New(Dm.forward(__extra, loc));
        DmList args = Dm.args(Dm.forward(__extra, loc));
        if (!Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
        {
            float previous_initialized_value = SSatoms.initialized;
            SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD;
            args[1] = TRUE;
            SSatoms.InitAtom(this, FALSE, args);
            SSatoms.initialized = previous_initialized_value;
        }
    }

    // tg: code/modules/mob/dead/dead.dm:11  (SHOULD_CALL_PARENT(FALSE) is dropped, rules 7.9)
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
            stack_trace(Dm.text("Warning: []([]) initialized multiple times!", this, type), "code/modules/mob/dead/dead.dm", 14);
        Dm.combine(ref flags_1, INITIALIZED_1);
        if (Dm.truthy(LAZYLEN(faction)))
            faction = string_list(faction) as DmList;
        // Initial is non standard here, but ghosts move before they get here so it's needed. this is a cold path too so it's ok
        SET_PLANE_IMPLICIT(this, Dm.num(Dm.initial(this, (atom p) => p.plane)));
        add_to_mob_list();

        prepare_huds();

        if (Dm.truthy(length(config.Get(typeof(datum_config_entry_keyed_list_cross_server)))))
        {
            // tg: :24  ASSIGN_GAME_VERB(src, /mob/dead, server_hop)
            Dm.NotTranslated("code/modules/mob/dead/dead.dm:24", "Initialize: ASSIGN_GAME_VERB(src, /mob/dead, server_hop), the Server Hop verb of a cross-server config (one server, after the core version)");
        }
        Waiting.NoSleep(set_focus(this));
        become_hearing_sensitive();
        log_mob_tag(Dm.text("TAG: [] CREATED: [] \\[[]\\]", tag, key_name(this), type));
        return INITIALIZE_HINT_NORMAL;
    }

    // tg: code/modules/mob/dead/dead.dm:31
    public override object canUseStorage()
    {
        return FALSE;
    }

    /**
     * updates the Z level for dead players
     * If they don't have a new z, we'll keep the old one, preventing bugs from ghosting and re-entering, among others
     */
    // tg: code/modules/mob/dead/dead.dm:81
    public virtual void update_z(object new_z = null) // 1+ to register, null to unregister
    {
        if (Dm.eq(registered_z, new_z))
            return;
        if (Dm.truthy(registered_z))
            Dm.remove((DmList)datum_controller_subsystem_mobs.dead_players_by_zlevel[registered_z], this);
        if (client == null)
        {
            registered_z = null;
            return;
        }
        registered_z = new_z;
        Dm.append((DmList)datum_controller_subsystem_mobs.dead_players_by_zlevel[new_z], this);
    }

    // tg: code/modules/mob/dead/dead.dm:92
    public override async DmTask<object> Login()
    {
        object dot = null;
        dot = await base.Login();
        if (!Dm.truthy(dot) || !Dm.truthy(client))
            return FALSE;
        turf T = get_turf(this);
        if (isturf(T))
            update_z(T.z);
        return dot;
    }

    // tg: code/modules/mob/dead/dead.dm:100
    public override object auto_deadmin_on_login()
    {
        return null;
    }

    // tg: code/modules/mob/dead/dead.dm:103
    public override async DmTask<object> Logout()
    {
        update_z(null);
        return await base.Logout();
    }

    // tg: code/modules/mob/dead/dead.dm:107
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
        update_z(new_turf?.z);
    }
}
