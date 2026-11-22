// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/verbs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10x slice G6: the admin help's verb; silicons SI4, D326: a mob's verbs): add_verb and remove_verb on a mob's or a client's
// verb list and their signals. Not translated: the stat panel's and the tgui panel's messages (the port's stat panel is the replicated
// command verb list, Infrastructure/ClientSeam.Verbs.cs, which reads the lists) and the verb sorting helpers below them.

public static partial class GlobalProcs
{
    /**
     * handles adding verbs and updating the stat panel browser
     *
     * pass the verb type path to this instead of adding it directly to verbs so the statpanel can update
     * Arguments:
     * * target - Who the verb is being added to, client or mob typepath
     * * verb - typepath to a verb, or a list of verbs, supports lists of lists
     */
    // tg: code/__HELPERS/verbs.dm:9
    public static void add_verb(object target = null, object verb_or_list_to_add = null)
    {
        if (target == null)
            throw Dm.CRASH("add_verb called without a target", "code/__HELPERS/verbs.dm", 11);
        if (Dm.truthy(IsAdminAdvancedProcCall()))
            return;
        mob mob_target = target as mob;
        client client_target = target as client ?? mob_target?.client;
        if (client_target == null && mob_target == null)
            throw Dm.CRASH("add_verb called on a non-mob and non-client", "code/__HELPERS/verbs.dm", 19);
        DmList verbs_list = flatten_verbs(verb_or_list_to_add);
        if (mob_target != null)
        {
            mob_target.verbs.Add(verbs_list);
            if (client_target == null)
                return; //Our work is done.
        }
        else
            client_target.verbs.Add(verbs_list);
        // the stat panel's "add_verb_list" and the tgui panel's "verbs/add": no stat panel (above)
        if (Dm.truthy(client_target._listen_lookup?[COMSIG_CLIENT_VERB_ADDED]))
            client_target._SendSignal(COMSIG_CLIENT_VERB_ADDED, Dm.list(client_target, verbs_list));
    }

    /**
     * handles removing verb and sending it to browser to update, use this for removing verbs
     *
     * pass the verb type path to this instead of removing it from verbs so the statpanel can update
     * Arguments:
     * * target - Who the verb is being removed from, client or mob typepath
     * * verb - typepath to a verb, or a list of verbs, supports lists of lists
     */
    // tg: code/__HELPERS/verbs.dm:67
    public static void remove_verb(object target = null, object verb_or_list_to_remove = null)
    {
        if (Dm.truthy(IsAdminAdvancedProcCall()))
            return;
        mob mob_target = target as mob;
        client client_target = target as client ?? mob_target?.client;
        if (client_target == null && mob_target == null)
            throw Dm.CRASH("remove_verb called on a non-mob and non-client", "code/__HELPERS/verbs.dm", 75);
        DmList verbs_list = flatten_verbs(verb_or_list_to_remove);
        if (mob_target != null)
        {
            mob_target.verbs.Remove(verbs_list);
            if (client_target == null)
                return; //Our work is done.
        }
        else
            client_target.verbs.Remove(verbs_list);
        if (Dm.truthy(client_target._listen_lookup?[COMSIG_CLIENT_VERB_REMOVED]))
            client_target._SendSignal(COMSIG_CLIENT_VERB_REMOVED, Dm.list(client_target, verbs_list));
    }

    // tg: code/__HELPERS/verbs.dm:21-32 and :77-88 (the same loop in both procs): a verb, or the verbs of a list of lists
    private static DmList flatten_verbs(object verb_or_list)
    {
        DmList verbs_list = Dm.list();
        if (verb_or_list is not DmList verb_listref)
        {
            verbs_list.Add(verb_or_list);
            return verbs_list;
        }
        DmList elements_to_process = verb_listref.Copy();
        while (Dm.length(elements_to_process) > 0)
        {
            object element_or_list = elements_to_process[elements_to_process.len]; //Last element
            elements_to_process.len--;
            if (element_or_list is DmList)
                elements_to_process.Add(element_or_list); //list/a += list/b adds the contents of b into a, not the reference to the list itself
            else
                verbs_list.Add(element_or_list);
        }
        return verbs_list;
    }
}
