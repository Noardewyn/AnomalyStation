// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/access.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 P3. Until M4a's ID cards a mob's access is its dev access list (D117, DV23: Builtin/mob.access.builtin.cs and the
// human's get_access in human_helpers.cs).

// tg: code/modules/jobs/access.dm:6
public partial class atom_movable : atom
{
    /**
     * Returns TRUE if this mob has sufficient access to use this object
     *
     * * accessor - mob trying to access this object, !!CAN BE NULL!! because of telekiesis because we're in hell
     */
    // tg: code/modules/jobs/access.dm:6
    public virtual async DmTask<object> allowed__proc(mob accessor = null)
    {
        // GAP(lead): /proc/isAdminGhostAI (modules/mob/mob_helpers.dm:357) is not declared; it needs an admin observer with
        // admin rights (check_rights_for, M10), so no mob of M3 is one.
        bool isAdminGhostAI__gap = false;
        //check if it doesn't require any access at all, or the user is an Adminghost
        if (Dm.truthy(check_access(null)) || isAdminGhostAI__gap)
            return TRUE;
        if (accessor == null) //likely a TK user, and we checked for free access above.
            return FALSE;

        //If the mob has the simple_access component with the requried access, we let them in.
        object attempted_access = (!Dm.truthy(accessor._listen_lookup?[COMSIG_MOB_TRIED_ACCESS]) ? NONE : accessor._SendSignal(COMSIG_MOB_TRIED_ACCESS, Dm.list(accessor, this)));
        if (Dm.truthy(Dm.bitand(attempted_access, ACCESS_ALLOWED)))
            return TRUE;
        if (Dm.truthy(Dm.bitand(attempted_access, ACCESS_DISALLOWED)))
            return FALSE;

        DmList player_access = accessor.get_access() as DmList;

        //now let's check access we got from the signal.
        if (Dm.truthy(check_access_list(player_access)))
            return TRUE;

        // HAS_SILICON_ACCESS(accessor) (__DEFINES/is_helpers.dm:178), with isAdminGhostAI as above
        if (HAS_TRAIT(accessor, TRAIT_SILICON_ACCESS) || isAdminGhostAI__gap)
        {
            if (!Dm.truthy(accessor.has_faction(ROLE_SYNDICATE)))
            {
                if (Dm.@in(ACCESS_SYNDICATE, req_access) || Dm.@in(ACCESS_SYNDICATE_LEADER, req_access) || Dm.@in(ACCESS_SYNDICATE, req_one_access) || Dm.@in(ACCESS_SYNDICATE_LEADER, req_one_access))
                    return FALSE;
                if (Dm.truthy(onSyndieBase()) && loc != accessor)
                    return FALSE;
            }
            return TRUE; //AI can do whatever it wants
        }
        return FALSE;
    }

    // Check if an item has access to this object
    // tg: code/modules/jobs/access.dm:36
    public virtual object check_access(obj_item I = null)
    {
        return check_access_list(Dm.truthy(I) ? I.GetAccess() as DmList : null);
    }

    // tg: code/modules/jobs/access.dm:39
    public virtual object check_access_list(DmList access_list = null)
    {
        if (!Dm.truthy(length(req_access)) && !Dm.truthy(length(req_one_access)))
            return TRUE;

        if (!Dm.truthy(length(access_list)) || !islist(access_list))
            return FALSE;

        foreach (object req in Dm.each(req_access))
        {
            if (!Dm.@in(req, access_list)) //doesn't have this access
                return FALSE;
        }

        if (Dm.truthy(length(req_one_access)))
        {
            foreach (object req in Dm.each(req_one_access))
            {
                if (Dm.@in(req, access_list)) //has an access from the single access list
                    return TRUE;
            }
            return FALSE;
        }
        return TRUE;
    }
}

// tg: code/modules/jobs/access.dm:57
public partial class obj_item : obj
{
    // tg: code/modules/jobs/access.dm:57
    public virtual object GetAccess()
    {
        return Dm.list();
    }

    // tg: code/modules/jobs/access.dm:60
    public virtual object GetID()
    {
        return null;
    }

    // tg: code/modules/jobs/access.dm:63
    public virtual async DmTask<object> remove_id(params object[] __extra)
    {
        return null;
    }

    // tg: code/modules/jobs/access.dm:66
    public virtual async DmTask<object> insert_id(params object[] __extra)
    {
        return FALSE;
    }
}
