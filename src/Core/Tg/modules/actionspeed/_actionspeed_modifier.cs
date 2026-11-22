// Translated from tgstation (snapshot 1d99bd7f): code/modules/actionspeed/_actionspeed_modifier.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/*! Actionspeed modification datums.

	How action speed for mobs works

Action speed is now calculated by using modifier datums which are added to mobs. Some of them (nonvariable ones) are globally cached, the variable ones are instanced and changed based on need.

This gives us the ability to have multiple sources of actionspeed, reliabily keep them applied and remove them when they should be

THey can have unique sources and a bunch of extra fancy flags that control behaviour

Previously trying to update action speed was a shot in the dark that usually meant mobs got stuck going faster or slower

Actionspeed modification list is a simple key = datum system. Key will be the datum's ID if it is overridden to not be null, or type if it is not.

DO NOT override datum IDs unless you are going to have multiple types that must overwrite each other. It's more efficient to use types, ID functionality is only kept for cases where dynamic creation of modifiers need to be done.

When update actionspeed is called, the list of items is iterated, according to flags priority and a bunch of conditions
this spits out a final calculated value which is used as a modifer to last_move + modifier for calculating when a mob
can next move

*/

// tg: code/modules/actionspeed/_actionspeed_modifier.dm:23
[DmPath("/datum/actionspeed_modifier")]
public partial class datum_actionspeed_modifier : datum
{
    /// Whether or not this is a variable modifier. Variable modifiers can NOT be ever auto-cached. ONLY CHECKED VIA INITIAL(), EFFECTIVELY READ ONLY (and for very good reason)
    // tg: code/modules/actionspeed/_actionspeed_modifier.dm:25
    public float? variable = FALSE;

    /// Unique ID. You can never have different modifications with the same ID. By default, this SHOULD NOT be set. Only set it for cases where you're dynamically making modifiers/need to have two types overwrite each other. If unset, uses path (converted to text) as ID.
    // tg: code/modules/actionspeed/_actionspeed_modifier.dm:28
    // (object: the V3 grep matches other vars named id; tg stores the text "[type]" here, New)
    public object id;

    /// Higher ones override lower priorities. This is NOT used for ID, ID must be unique, if it isn't unique the newer one overwrites automatically if overriding.
    // tg: code/modules/actionspeed/_actionspeed_modifier.dm:31
    public float priority = 0;
    // tg: code/modules/actionspeed/_actionspeed_modifier.dm:32
    public float? flags = NONE;

    /// Multiplicative slowdown
    // tg: code/modules/actionspeed/_actionspeed_modifier.dm:35
    public float? multiplicative_slowdown = 0;

    /// Other modification datums this conflicts with.
    // tg: code/modules/actionspeed/_actionspeed_modifier.dm:38
    public object conflicts_with;

    // tg: code/modules/actionspeed/_actionspeed_modifier.dm:40
    [DmParams("init_id")]
    public override async DmTask New(params object[] __extra)
    {
        object init_id = Dm.arg(__extra, 0);
        await base.New(Dm.forward(__extra, init_id));

        if (Dm.truthy(init_id))
            id = init_id;

        if (!Dm.truthy(id))
            id = Dm.text("[]", type); //We turn the path into a string.
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/modules/actionspeed/_actionspeed_modifier.dm:49  GLOBAL_LIST_EMPTY(actionspeed_modification_cache)
    public DmList actionspeed_modification_cache;

    // tg: code/modules/actionspeed/_actionspeed_modifier.dm:49  (GLOBAL_MANAGED(actionspeed_modification_cache, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalactionspeed_modification_cache()
    {
        actionspeed_modification_cache = Dm.list();
        Dm.append(ref gvars_datum_init_order, "actionspeed_modification_cache");
    }
}

public static partial class GlobalProcs
{
    /// Grabs a STATIC MODIFIER datum from cache. YOU MUST NEVER EDIT THESE DATUMS, OR IT WILL AFFECT ANYTHING ELSE USING IT TOO!
    // tg: code/modules/actionspeed/_actionspeed_modifier.dm:52
    public static async DmTask<object> get_cached_actionspeed_modifier(object modtype = null)
    {
        if (!ispath(modtype, typeof(datum_actionspeed_modifier)))
            throw Dm.CRASH(Dm.text("[] is not a actionspeed modification typepath.", modtype), "code/modules/actionspeed/_actionspeed_modifier.dm", 54);
        // var/datum/actionspeed_modifier/actionspeed_mod holds the typepath first, then the cached datum: object (rules 3.4).
        object actionspeed_mod = modtype;
        if (Dm.truthy(Dm.initial(actionspeed_mod, (datum_actionspeed_modifier p) => p.variable)))
            throw Dm.CRASH(Dm.text("[] is a variable modifier, and can never be cached.", modtype), "code/modules/actionspeed/_actionspeed_modifier.dm", 57);
        actionspeed_mod = GLOB.actionspeed_modification_cache[modtype];
        if (!Dm.truthy(actionspeed_mod))
            actionspeed_mod = GLOB.actionspeed_modification_cache[modtype] = await Dm.new_async((Type)modtype);
        return actionspeed_mod;
    }
}

// tg: code/modules/actionspeed/_actionspeed_modifier.dm:64
public partial class mob : atom_movable
{
    ///Add a action speed modifier to a mob. If a variable subtype is passed in as the first argument, it will make a new datum. If ID conflicts, it will overwrite the old ID.
    // tg: code/modules/actionspeed/_actionspeed_modifier.dm:64
    public virtual async DmTask<object> add_actionspeed_modifier(object type_or_datum = null, float? update = null)
    {
        update ??= TRUE;
        if (ispath(type_or_datum))
        {
            if (!Dm.truthy(Dm.initial(type_or_datum, (datum_actionspeed_modifier p) => p.variable)))
                type_or_datum = await get_cached_actionspeed_modifier(type_or_datum);
            else
                type_or_datum = await Dm.new_async((Type)type_or_datum);
        }
        datum_actionspeed_modifier existing = LAZYACCESS(actionspeed_modification, ((datum_actionspeed_modifier)type_or_datum).id) as datum_actionspeed_modifier;
        if (Dm.truthy(existing))
        {
            if (Dm.eq(existing, type_or_datum)) //same thing don't need to touch
                return TRUE;
            remove_actionspeed_modifier(existing, FALSE);
        }
        if (Dm.truthy(length(actionspeed_modification)))
        {
            // BINARY_INSERT(type_or_datum.id, actionspeed_modification, /datum/actionspeed_modifier, type_or_datum, priority,
            // COMPARE_VALUE) (__HELPERS/_lists.dm:142, inline by rules 11.1 (a), (b)): the id goes in at the place its
            // priority sorts to (an equal priority stays before it); LAZYSET below gives it its value.
            do
            {
                DmList __BIN_LIST = actionspeed_modification;
                float __BIN_CTTL = length(__BIN_LIST);
                if (!Dm.truthy(__BIN_CTTL))
                    Dm.append(ref __BIN_LIST, ((datum_actionspeed_modifier)type_or_datum).id);
                else
                {
                    float __BIN_LEFT = 1;
                    float __BIN_RIGHT = __BIN_CTTL;
                    float __BIN_MID = Dm.shr(__BIN_LEFT + __BIN_RIGHT, 1);
                    datum_actionspeed_modifier __BIN_ITEM = null;
                    while (__BIN_LEFT < __BIN_RIGHT)
                    {
                        __BIN_ITEM = __BIN_LIST[__BIN_LIST[__BIN_MID]] as datum_actionspeed_modifier;
                        if (__BIN_ITEM.priority <= ((datum_actionspeed_modifier)type_or_datum).priority)
                            __BIN_LEFT = __BIN_MID + 1;
                        else
                            __BIN_RIGHT = __BIN_MID;
                        __BIN_MID = Dm.shr(__BIN_LEFT + __BIN_RIGHT, 1);
                    }
                    __BIN_ITEM = __BIN_LIST[__BIN_LIST[__BIN_MID]] as datum_actionspeed_modifier;
                    __BIN_MID = (__BIN_ITEM.priority > ((datum_actionspeed_modifier)type_or_datum).priority ? __BIN_MID : __BIN_MID + 1);
                    __BIN_LIST.Insert(__BIN_MID, ((datum_actionspeed_modifier)type_or_datum).id);
                }
            }
            while (Dm.truthy(FALSE));
        }
        LAZYSET(ref actionspeed_modification, ((datum_actionspeed_modifier)type_or_datum).id, type_or_datum);
        if (Dm.truthy(update))
            update_actionspeed();
        return TRUE;
    }

    /// Remove a action speed modifier from a mob, whether static or variable.
    // tg: code/modules/actionspeed/_actionspeed_modifier.dm:83
    public virtual object remove_actionspeed_modifier(object type_id_datum = null, float? update = null)
    {
        update ??= TRUE;
        object key = null;
        if (ispath(type_id_datum))
            key = (Dm.initial(type_id_datum, (datum_actionspeed_modifier p) => p.id) is var or__1 && Dm.truthy(or__1) ? or__1 : (object)Dm.text("[]", type_id_datum)); //id if set, path set to string if not.
        else if (!istext(type_id_datum)) //if it isn't text it has to be a datum, as it isn't a type.
            key = ((datum_actionspeed_modifier)type_id_datum).id;
        else //assume it's an id
            key = type_id_datum;
        if (!Dm.truthy(LAZYACCESS(actionspeed_modification, key)))
            return FALSE;
        LAZYREMOVE(ref actionspeed_modification, key);
        if (Dm.truthy(update))
            update_actionspeed(); // DM passes FALSE, which update_actionspeed does not declare (dropped, rules 7.3)
        return TRUE;
    }

    /*! Used for variable slowdowns like hunger/health loss/etc, works somewhat like the old list-based modification adds. Returns the modifier datum if successful
        How this SHOULD work is:
        1. Ensures type_id_datum one way or another refers to a /variable datum. This makes sure it can't be cached. This includes if it's already in the modification list.
        2. Instantiate a new datum if type_id_datum isn't already instantiated + in the list, using the type. Obviously, wouldn't work for ID only.
        3. Add the datum if necessary using the regular add proc
        4. If any of the rest of the args are not null (see: multiplicative slowdown), modify the datum
        5. Update if necessary
    */
    // tg: code/modules/actionspeed/_actionspeed_modifier.dm:106
    public virtual async DmTask<object> add_or_update_variable_actionspeed_modifier(object type_id_datum = null, float? update = null, object multiplicative_slowdown = null)
    {
        update ??= TRUE;
        float modified = FALSE;
        float inject = FALSE;
        datum_actionspeed_modifier final = null;
        if (istext(type_id_datum))
        {
            final = LAZYACCESS(actionspeed_modification, type_id_datum) as datum_actionspeed_modifier;
            if (!Dm.truthy(final))
                throw Dm.CRASH("Couldn't find existing modification when provided a text ID.", "code/modules/actionspeed/_actionspeed_modifier.dm", 113);
        }
        else if (ispath(type_id_datum))
        {
            if (!Dm.truthy(Dm.initial(type_id_datum, (datum_actionspeed_modifier p) => p.variable)))
                throw Dm.CRASH("Not a variable modifier", "code/modules/actionspeed/_actionspeed_modifier.dm", 116);
            final = LAZYACCESS(actionspeed_modification, (Dm.initial(type_id_datum, (datum_actionspeed_modifier p) => p.id) is var or__1 && Dm.truthy(or__1) ? or__1 : (object)Dm.text("[]", type_id_datum))) as datum_actionspeed_modifier;
            if (!Dm.truthy(final))
            {
                final = await Dm.new_async((Type)type_id_datum) as datum_actionspeed_modifier;
                inject = TRUE;
                modified = TRUE;
            }
        }
        else
        {
            if (!Dm.truthy(Dm.initial(type_id_datum, (datum_actionspeed_modifier p) => p.variable)))
                throw Dm.CRASH("Not a variable modifier", "code/modules/actionspeed/_actionspeed_modifier.dm", 124);
            final = type_id_datum as datum_actionspeed_modifier;
            if (!Dm.truthy(LAZYACCESS(actionspeed_modification, final.id)))
            {
                inject = TRUE;
                modified = TRUE;
            }
        }
        if (multiplicative_slowdown != null)
        {
            final.multiplicative_slowdown = Dm.num_or_null(multiplicative_slowdown);
            modified = TRUE;
        }
        if (Dm.truthy(inject))
            await add_actionspeed_modifier(final, FALSE);
        if (Dm.truthy(update) && Dm.truthy(modified))
            update_actionspeed(); // DM passes TRUE, which update_actionspeed does not declare (dropped, rules 7.3)
        return final;
    }

    ///Is there a actionspeed modifier for this mob
    // tg: code/modules/actionspeed/_actionspeed_modifier.dm:139
    public virtual object has_actionspeed_modifier(object datum_type_id = null)
    {
        object key = null;
        if (ispath(datum_type_id))
            key = (Dm.initial(datum_type_id, (datum_actionspeed_modifier p) => p.id) is var or__1 && Dm.truthy(or__1) ? or__1 : (object)Dm.text("[]", datum_type_id));
        else if (istext(datum_type_id))
            key = datum_type_id;
        else
            key = ((datum_actionspeed_modifier)datum_type_id).id;
        return LAZYACCESS(actionspeed_modification, key);
    }

    /// Go through the list of actionspeed modifiers and calculate a final actionspeed. ANY ADD/REMOVE DONE IN UPDATE_actionspeed MUST HAVE THE UPDATE ARGUMENT SET AS FALSE!
    // tg: code/modules/actionspeed/_actionspeed_modifier.dm:150
    public virtual object update_actionspeed()
    {
        object dot = null;
        dot = 0;
        DmList conflict_tracker = Dm.list();
        foreach (object key in Dm.each(get_actionspeed_modifiers()))
        {
            datum_actionspeed_modifier M = actionspeed_modification[key] as datum_actionspeed_modifier;
            object conflict = M.conflicts_with;
            object amt = M.multiplicative_slowdown;
            if (Dm.truthy(conflict))
            {
                // Conflicting modifiers prioritize the larger slowdown or the larger speedup
                // We purposefully don't handle mixing speedups and slowdowns on the same id
                if (abs(Dm.num(conflict_tracker[conflict])) < abs(Dm.num(amt)))
                    conflict_tracker[conflict] = amt;
                else
                    continue;
            }
            Dm.append(ref dot, amt);
        }
        cached_multiplicative_actions_slowdown = dot;
        return dot;
    }

    ///Adds a default action speed
    // tg: code/modules/actionspeed/_actionspeed_modifier.dm:168
    public virtual async DmTask initialize_actionspeed()
    {
        await add_or_update_variable_actionspeed_modifier(typeof(datum_actionspeed_modifier_base), multiplicative_slowdown: 1);
    }

    /// Get the action speed modifiers list of the mob
    // tg: code/modules/actionspeed/_actionspeed_modifier.dm:172
    public virtual object get_actionspeed_modifiers()
    {
        DmList dot = null;
        dot = LAZYCOPY(actionspeed_modification);
        foreach (object id in Dm.each(actionspeed_mod_immunities))
            Dm.remove(ref dot, id);
        return dot;
    }
}

public static partial class GlobalProcs
{
    /// Checks if a action speed modifier is valid and not missing any data
    // tg: code/modules/actionspeed/_actionspeed_modifier.dm:178
    public static object actionspeed_data_null_check(datum_actionspeed_modifier M = null) //Determines if a data list is not meaningful and should be discarded.
    {
        object dot = null;
        dot = Dm.not(M.multiplicative_slowdown);
        return dot;
    }
}
