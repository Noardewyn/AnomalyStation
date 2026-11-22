// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/spawns.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4c, D316: the supply pods).

public static partial class GlobalProcs
{
    /**
     * One proc for easy spawning of pods in the code to drop off items before whizzling (please don't proc call this in game, it will destroy you)
     *
     * Arguments:
     * * specifications: special mods to the pod, see non var edit specifications for details on what you should fill this with
     * Non var edit specifications:
     * * target = where you want the pod to drop
     * * path = a special specific pod path if you want, this can save you a lot of var edits
     * * style = style of the pod, defaults to the normal pod
     * * spawn = spawned path or a list of the paths spawned, what you're sending basically
     * Returns the pod spawned, in case you want to spawn items yourself and modify them before putting them in.
     */
    // tg: code/__HELPERS/spawns.dm:13
    public static async DmTask<object> podspawn(DmList specifications)
    {
        //get non var edit specifications
        atom landing_location = specifications["target"] as atom;
        object spawn_type = specifications["path"];
        object style = specifications["style"];
        object paths_to_spawn = specifications["spawn"];

        //setup pod, add contents
        if (!isturf(landing_location))
            landing_location = get_turf(landing_location);
        if (!Dm.truthy(spawn_type))
            spawn_type = typeof(obj_structure_closet_supplypod_podspawn);
        obj_structure_closet_supplypod pod = Dm.@new((Type)spawn_type, null, style) as obj_structure_closet_supplypod;
        if (Dm.truthy(paths_to_spawn) && !islist(paths_to_spawn))
            paths_to_spawn = Dm.list(paths_to_spawn);
        DmList spawn_list = paths_to_spawn as DmList;
        foreach (object path in Dm.each(spawn_list))
        {
            if (!ispath(path))
                await ((atom_movable)path).forceMove(pod);
            else
            {
                object amount_to_spawn = spawn_list[path] ?? 1;
                if (!Dm.truthy(amount_to_spawn))
                    amount_to_spawn = 1;
                if (!isnum(amount_to_spawn))
                {
                    stack_trace(Dm.text("amount to spawn for path \"[]\" is not a number, defaulting to 1", path), "code/__HELPERS/spawns.dm", 37);
                    amount_to_spawn = 1;
                }

                for (int item_number = 1; item_number <= Dm.num(amount_to_spawn); item_number++)
                    Dm.@new((Type)path, pod);
            }
        }

        //remove non var edits from specifications
        specifications.Remove("target");
        specifications.Remove("style");
        specifications.Remove("path");
        specifications.Remove("spawn"); //list, we remove the key

        //rest of specificiations are edits on the pod
        foreach (object variable_name in Dm.each(specifications.Copy()))
        {
            object variable_value = specifications[variable_name];
            if (!Dm.truthy(await pod.vv_edit_var(variable_name, variable_value)))
                stack_trace(Dm.text("WARNING! podspawn vareditting \"[]\" to \"[]\" was rejected by the pod!", variable_name, variable_value), "code/__HELPERS/spawns.dm", 52);
        }
        Dm.@new<obj_effect_pod_landingzone>(landing_location, pod);
        return pod;
    }
}
