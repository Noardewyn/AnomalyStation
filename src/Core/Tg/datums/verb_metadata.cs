// Translated from tgstation (snapshot 1d99bd7f): code/datums/verb_metadata.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI4 (D326): the whole file. The var blocks are generated. The VERB_ARG statics that call ____register_verb_arg from the
// verb bodies stay out (rules 10.6, living.cs's __gvb_succumb), so a verb's arguments are empty and invoke_verb prompts for none.

// tg: code/datums/verb_metadata.dm:1
public partial class datum_verb_metadata : datum
{
    // tg: code/datums/verb_metadata.dm:10
    public virtual void assign_to(object target = null)
    {
        add_verb(target, verb_path);
    }

    // tg: code/datums/verb_metadata.dm:13
    public virtual void unassign_from(object target = null)
    {
        remove_verb(target, verb_path);
    }
}

// tg: code/datums/verb_metadata.dm:16
public partial class datum_verb_arg_metadata : datum
{
    // tg: code/datums/verb_metadata.dm:24
    public virtual async DmTask<object> prompt(client user = null, object verb_name = null)
    {
        if (Dm.eq(source, VERB_ARG_SOURCE_LIST) && Dm.truthy(Dm.length(options)))
            return await tgui_input_list(user, name, verb_name, options);
        float flags = Dm.num(arg_type);
        if (Dm.truthy(Dm.bitand(flags, VERB_ARG_TYPE_NUM)))
            return await tgui_input_number(user, name, verb_name);
        if (Dm.truthy(Dm.bitand(flags, VERB_ARG_TYPE_TEXT)))
            return await tgui_input_text(user, name, verb_name);
        if (Dm.truthy(Dm.bitand(flags, VERB_ARG_TYPE_MESSAGE)))
            return await tgui_input_text(user, name, verb_name, multiline: TRUE);
        if (Dm.truthy(Dm.bitand(flags, VERB_ARG_TYPE_SOUND)))
            return await Dm.input(user, name, verb_name); // as null|sound
        if (Dm.truthy(Dm.bitand(flags, VERB_ARG_TYPE_TYPEPATH)))
        {
            if (!Dm.truthy(type_path))
                return "";
            return await Dm.input(user, name, verb_name, typesof(type_path)); // as null|anything in typesof(type_path)
        }
        DmList targets = get_targets(user);
        if (Dm.truthy(Dm.length(targets)))
            return await tgui_input_list(user, name, verb_name, sort_names(targets) as DmList);
        return null;
    }

    // tg: code/datums/verb_metadata.dm:77
    public override async DmTask New(params object[] __extra)
    {
        name = Dm.arg(__extra, 0);
        arg_type = Dm.arg(__extra, 1);
        type_path = Dm.arg(__extra, 2);
        source = Dm.arg(__extra, 3);
        options = Dm.arg(__extra, 4) as DmList;
        view_range = Dm.arg(__extra, 5);
        await DmTask.CompletedTask;
    }

    // tg: code/datums/verb_metadata.dm:85
    public virtual DmList get_targets(client viewer = null)
    {
        if (Dm.eq(source, VERB_ARG_SOURCE_WORLD))
            return get_world_targets(viewer);
        if (Dm.eq(source, VERB_ARG_SOURCE_VIEW))
            return get_view_targets(viewer);
        return Dm.list();
    }

    // tg: code/datums/verb_metadata.dm:93
    public virtual DmList get_world_targets(client viewer = null)
    {
        float flags = Dm.num(arg_type);
        if (Dm.truthy(Dm.bitand(flags, VERB_ARG_TYPE_MOB)))
            return GLOB.mob_list;
        if (Dm.truthy(Dm.bitand(flags, VERB_ARG_TYPE_AREA)))
            return get_sorted_areas() as DmList;
        if (Dm.truthy(Dm.bitand(flags, VERB_ARG_TYPE_TURF)))
        {
            DmList turfs = Dm.list();
            if (viewer.mob != null)
            {
                turf admin_turf = get_turf(viewer.mob);
                if (admin_turf != null)
                    turfs.Add(admin_turf);
            }
            foreach (mob player in Dm.each<mob>(GLOB.player_list))
            {
                turf player_turf = get_turf(player);
                if (player_turf != null)
                    Dm.combine(ref turfs, player_turf);
            }
            return turfs;
        }
        if (Dm.truthy(Dm.bitand(flags, VERB_ARG_TYPE_OBJ | VERB_ARG_TYPE_DATUM | VERB_ARG_TYPE_ATOM)))
        {
            if (viewer.mob != null)
                return view(viewer.view, viewer.mob);
        }
        return Dm.list();
    }

    // tg: code/datums/verb_metadata.dm:114
    public virtual DmList get_view_targets(client viewer = null)
    {
        if (viewer.mob == null)
            return Dm.list();
        DmList visible = Dm.truthy(view_range) ? oview(view_range, viewer.mob) : view(viewer.view, viewer.mob);
        float flags = Dm.num(arg_type);
        if (!Dm.truthy(Dm.bitand(flags, VERB_ARG_TYPE_MOB | VERB_ARG_TYPE_OBJ | VERB_ARG_TYPE_TURF)))
            return visible;
        DmList filtered = Dm.list();
        if (Dm.truthy(Dm.bitand(flags, VERB_ARG_TYPE_MOB)))
        {
            foreach (mob target in Dm.each<mob>(visible))
                filtered.Add(target);
        }
        if (Dm.truthy(Dm.bitand(flags, VERB_ARG_TYPE_OBJ)))
        {
            foreach (obj target in Dm.each<obj>(visible))
                filtered.Add(target);
        }
        if (Dm.truthy(Dm.bitand(flags, VERB_ARG_TYPE_TURF)))
        {
            foreach (turf target in Dm.each<turf>(visible))
                filtered.Add(target);
        }
        return filtered;
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/datums/verb_metadata.dm:132  GLOBAL_LIST_INIT(____pending_verb_args, list())
    public DmList ____pending_verb_args;

    // tg: code/datums/verb_metadata.dm:132  (GLOBAL_MANAGED(____pending_verb_args, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobal____pending_verb_args()
    {
        ____pending_verb_args = Dm.list();
        Dm.append(ref gvars_datum_init_order, "____pending_verb_args");
    }
}

public static partial class GlobalProcs
{
    // tg: code/datums/verb_metadata.dm:44
    public static async DmTask<DmList> ____collect_verb_args(client user = null, object verb_name = null, DmList arguments = null, DmList positional_args = null)
    {
        DmList structured_args = Dm.list();

        object context_target = null;
        if (Dm.truthy(Dm.length(positional_args)) && Dm.@in(VERB_ARG_CONTEXT_TARGET_KEY, positional_args))
        {
            context_target = positional_args[VERB_ARG_CONTEXT_TARGET_KEY];
            Dm.remove(ref positional_args, VERB_ARG_CONTEXT_TARGET_KEY);
        }

        if (Dm.truthy(Dm.length(positional_args)) && Dm.truthy(Dm.length(arguments)))
        {
            if (positional_args.IsAlist) // istype(positional_args, /alist)
            {
                foreach (datum_verb_arg_metadata arg in Dm.each<datum_verb_arg_metadata>(arguments))
                {
                    if (positional_args[arg.name] != null)
                        structured_args[arg.name] = positional_args[arg.name];
                }
            }
            else
            {
                for (int i = 1; i <= Math.Min(Dm.length(positional_args), Dm.length(arguments)); i++)
                {
                    var arg = arguments[i] as datum_verb_arg_metadata;
                    structured_args[arg.name] = positional_args[i];
                }
            }
        }

        if (context_target != null && Dm.truthy(Dm.length(arguments)))
        {
            foreach (datum_verb_arg_metadata arg in Dm.each<datum_verb_arg_metadata>(arguments))
            {
                if (structured_args[arg.name] == null && (Dm.eq(arg.source, VERB_ARG_SOURCE_WORLD) || Dm.eq(arg.source, VERB_ARG_SOURCE_VIEW)))
                {
                    structured_args[arg.name] = context_target;
                    break;
                }
            }
        }

        foreach (datum_verb_arg_metadata arg in Dm.each<datum_verb_arg_metadata>(arguments))
        {
            if (structured_args[arg.name] != null)
                continue;
            object value = await arg.prompt(user, verb_name);
            if (value == null)
                return null;
            structured_args[arg.name] = value;
        }
        return structured_args;
    }

    // tg: code/datums/verb_metadata.dm:134
    public static object ____register_verb_arg(object owner_type = null, object proc_path = null, object arg_name = null, object arg_type = null, object arg_type_path = null, object arg_source = null, object arg_view_range = null)
    {
        object verb_key;
        if (ispath(owner_type, typeof(datum_admin_verb)))
            verb_key = owner_type;
        else
            verb_key = proc_path;

        if (!Dm.truthy(GLOB.____pending_verb_args[verb_key]))
            GLOB.____pending_verb_args[verb_key] = Dm.list();
        ((DmList)GLOB.____pending_verb_args[verb_key]).Add(Dm.list(Dm.@new<datum_verb_arg_metadata>(arg_name, arg_type, arg_type_path, arg_source, null, arg_view_range)));
        return TRUE;
    }

    // tg: code/datums/verb_metadata.dm:146
    public static object ____register_verb_arg_list(object owner_type = null, object proc_path = null, object arg_name = null, DmList options = null)
    {
        object verb_key;
        if (ispath(owner_type, typeof(datum_admin_verb)))
            verb_key = owner_type;
        else
            verb_key = proc_path;

        if (!Dm.truthy(GLOB.____pending_verb_args[verb_key]))
            GLOB.____pending_verb_args[verb_key] = Dm.list();
        ((DmList)GLOB.____pending_verb_args[verb_key]).Add(Dm.list(Dm.@new<datum_verb_arg_metadata>(arg_name, VERB_ARG_TYPE_TEXT, null, VERB_ARG_SOURCE_LIST, options)));
        return TRUE;
    }
}
