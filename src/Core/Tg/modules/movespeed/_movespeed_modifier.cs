// Translated from tgstation (snapshot 1d99bd7f): code/modules/movespeed/_movespeed_modifier.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/*! Movespeed modification datums.

	How move speed for mobs works

Move speed is now calculated by using modifier datums which are added to mobs. Some of them (nonvariable ones) are globally cached, the variable ones are instanced and changed based on need.

This gives us the ability to have multiple sources of movespeed, reliabily keep them applied and remove them when they should be

THey can have unique sources and a bunch of extra fancy flags that control behaviour

Previously trying to update move speed was a shot in the dark that usually meant mobs got stuck going faster or slower

Movespeed modification list is a simple key = datum system. Key will be the datum's ID if it is overridden to not be null, or type if it is not.

DO NOT override datum IDs unless you are going to have multiple types that must overwrite each other. It's more efficient to use types, ID functionality is only kept for cases where dynamic creation of modifiers need to be done.

When update movespeed is called, the list of items is iterated, according to flags priority and a bunch of conditions
this spits out a final calculated value which is used as a modifer to last_move + modifier for calculating when a mob
can next move

Key procs
* [add_movespeed_modifier](mob.html#proc/add_movespeed_modifier)
* [remove_movespeed_modifier](mob.html#proc/remove_movespeed_modifier)
* [has_movespeed_modifier](mob.html#proc/has_movespeed_modifier)
* [update_movespeed](mob.html#proc/update_movespeed)
*/

// tg: code/modules/movespeed/_movespeed_modifier.dm:28
[DmPath("/datum/movespeed_modifier")]
public partial class datum_movespeed_modifier : datum
{
    /// Whether or not this is a variable modifier. Variable modifiers can NOT be ever auto-cached. ONLY CHECKED VIA INITIAL(), EFFECTIVELY READ ONLY (and for very good reason)
    // tg: code/modules/movespeed/_movespeed_modifier.dm:30
    public float? variable = FALSE;

    /// Unique ID. You can never have different modifications with the same ID. By default, this SHOULD NOT be set. Only set it for cases where you're dynamically making modifiers/need to have two types overwrite each other. If unset, uses path (converted to text) as ID.
    // tg: code/modules/movespeed/_movespeed_modifier.dm:33
    // (object: the V3 grep matches other vars named id; tg stores the text "[type]" here, New)
    public object id;

    /// Higher ones override lower priorities. This is NOT used for ID, ID must be unique, if it isn't unique the newer one overwrites automatically if overriding.
    // tg: code/modules/movespeed/_movespeed_modifier.dm:36
    public float priority = 0;
    // tg: code/modules/movespeed/_movespeed_modifier.dm:37
    public float? flags = NONE;

    /// Multiplicative slowdown
    // tg: code/modules/movespeed/_movespeed_modifier.dm:40
    public float? multiplicative_slowdown = 0;

    /// Movetypes this applies to
    // tg: code/modules/movespeed/_movespeed_modifier.dm:43
    public float movetypes = ALL;

    /// Movetypes this never applies to
    // tg: code/modules/movespeed/_movespeed_modifier.dm:46
    public float blacklisted_movetypes = NONE;

    /// Other modification datums this conflicts with.
    // tg: code/modules/movespeed/_movespeed_modifier.dm:49
    public object conflicts_with;

    // tg: code/modules/movespeed/_movespeed_modifier.dm:51
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (!Dm.truthy(id))
            id = Dm.text("[]", type); //We turn the path into a string.
    }

    // tg: code/modules/movespeed/_movespeed_modifier.dm:56  /datum/movespeed_modifier/vv_edit_var(var_name, var_value):
    // not translated with the file: the slot's root, /datum/proc/vv_edit_var (code/datums/datumvars.dm:16), is admin
    // variable editing, which the port does not translate (rules 13); this override comes with it.
}

public partial class datum_controller_global_vars
{
    // tg: code/modules/movespeed/_movespeed_modifier.dm:62  GLOBAL_LIST_EMPTY(movespeed_modification_cache)
    public DmList movespeed_modification_cache;

    // tg: code/modules/movespeed/_movespeed_modifier.dm:62  (GLOBAL_MANAGED(movespeed_modification_cache, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmovespeed_modification_cache()
    {
        movespeed_modification_cache = Dm.list();
        Dm.append(ref gvars_datum_init_order, "movespeed_modification_cache");
    }
}

public static partial class GlobalProcs
{
    /// Grabs a STATIC MODIFIER datum from cache. YOU MUST NEVER EDIT THESE DATUMS, OR IT WILL AFFECT ANYTHING ELSE USING IT TOO!
    // tg: code/modules/movespeed/_movespeed_modifier.dm:65
    public static async DmTask<object> get_cached_movespeed_modifier(object modtype = null)
    {
        if (!ispath(modtype, typeof(datum_movespeed_modifier)))
            throw Dm.CRASH(Dm.text("[] is not a movespeed modification typepath.", modtype), "code/modules/movespeed/_movespeed_modifier.dm", 67);
        // var/datum/movespeed_modifier/M holds the typepath first, then the cached datum: object (rules 3.4).
        object M = modtype;
        if (Dm.truthy(Dm.initial(M, (datum_movespeed_modifier p) => p.variable)))
            throw Dm.CRASH(Dm.text("[] is a variable modifier, and can never be cached.", modtype), "code/modules/movespeed/_movespeed_modifier.dm", 70);
        M = GLOB.movespeed_modification_cache[modtype];
        if (!Dm.truthy(M))
            M = GLOB.movespeed_modification_cache[modtype] = await Dm.new_async((Type)modtype);
        return M;
    }
}

// tg: code/modules/movespeed/_movespeed_modifier.dm:77
public partial class mob : atom_movable
{
    ///Add a move speed modifier to a mob. If a variable subtype is passed in as the first argument, it will make a new datum. If ID conflicts, it will overwrite the old ID.
    // tg: code/modules/movespeed/_movespeed_modifier.dm:77
    public virtual async DmTask<object> add_movespeed_modifier(object type_or_datum = null, float? update = null)
    {
        update ??= TRUE;
        if (ispath(type_or_datum))
        {
            if (!Dm.truthy(Dm.initial(type_or_datum, (datum_movespeed_modifier p) => p.variable)))
                type_or_datum = await get_cached_movespeed_modifier(type_or_datum);
            else
                type_or_datum = await Dm.new_async((Type)type_or_datum);
        }
        datum_movespeed_modifier existing = LAZYACCESS(movespeed_modification, ((datum_movespeed_modifier)type_or_datum).id) as datum_movespeed_modifier;
        if (Dm.truthy(existing))
        {
            if (Dm.eq(existing, type_or_datum)) //same thing don't need to touch
                return TRUE;
            remove_movespeed_modifier(existing, FALSE);
        }
        if (Dm.truthy(length(movespeed_modification)))
        {
            // BINARY_INSERT(type_or_datum.id, movespeed_modification, /datum/movespeed_modifier, type_or_datum, priority,
            // COMPARE_VALUE) (__HELPERS/_lists.dm:142, inline by rules 11.1 (a), (b)): the id goes in at the place its
            // priority sorts to (an equal priority stays before it); LAZYSET below gives it its value.
            do
            {
                DmList __BIN_LIST = movespeed_modification;
                float __BIN_CTTL = length(__BIN_LIST);
                if (!Dm.truthy(__BIN_CTTL))
                    Dm.append(ref __BIN_LIST, ((datum_movespeed_modifier)type_or_datum).id);
                else
                {
                    float __BIN_LEFT = 1;
                    float __BIN_RIGHT = __BIN_CTTL;
                    float __BIN_MID = Dm.shr(__BIN_LEFT + __BIN_RIGHT, 1);
                    datum_movespeed_modifier __BIN_ITEM = null;
                    while (__BIN_LEFT < __BIN_RIGHT)
                    {
                        __BIN_ITEM = __BIN_LIST[__BIN_LIST[__BIN_MID]] as datum_movespeed_modifier;
                        if (__BIN_ITEM.priority <= ((datum_movespeed_modifier)type_or_datum).priority)
                            __BIN_LEFT = __BIN_MID + 1;
                        else
                            __BIN_RIGHT = __BIN_MID;
                        __BIN_MID = Dm.shr(__BIN_LEFT + __BIN_RIGHT, 1);
                    }
                    __BIN_ITEM = __BIN_LIST[__BIN_LIST[__BIN_MID]] as datum_movespeed_modifier;
                    __BIN_MID = (__BIN_ITEM.priority > ((datum_movespeed_modifier)type_or_datum).priority ? __BIN_MID : __BIN_MID + 1);
                    __BIN_LIST.Insert(__BIN_MID, ((datum_movespeed_modifier)type_or_datum).id);
                }
            }
            while (Dm.truthy(FALSE));
        }
        LAZYSET(ref movespeed_modification, ((datum_movespeed_modifier)type_or_datum).id, type_or_datum);
        if (Dm.truthy(update))
            update_movespeed();
        return TRUE;
    }

    /// Remove a move speed modifier from a mob, whether static or variable.
    // tg: code/modules/movespeed/_movespeed_modifier.dm:96
    public virtual object remove_movespeed_modifier(object type_id_datum = null, float? update = null)
    {
        update ??= TRUE;
        object key = null;
        if (ispath(type_id_datum))
            key = (Dm.initial(type_id_datum, (datum_movespeed_modifier p) => p.id) is var or__1 && Dm.truthy(or__1) ? or__1 : (object)Dm.text("[]", type_id_datum)); //id if set, path set to string if not.
        else if (!istext(type_id_datum)) //if it isn't text it has to be a datum, as it isn't a type.
            key = ((datum_movespeed_modifier)type_id_datum).id;
        else //assume it's an id
            key = type_id_datum;
        if (!Dm.truthy(LAZYACCESS(movespeed_modification, key)))
            return FALSE;
        LAZYREMOVE(ref movespeed_modification, key);
        if (Dm.truthy(update))
            update_movespeed(); // DM passes FALSE, which update_movespeed does not declare (dropped, rules 7.3)
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
    // tg: code/modules/movespeed/_movespeed_modifier.dm:119
    public virtual async DmTask<object> add_or_update_variable_movespeed_modifier(object type_id_datum = null, float? update = null, object multiplicative_slowdown = null)
    {
        update ??= TRUE;
        float modified = FALSE;
        float inject = FALSE;
        datum_movespeed_modifier final = null;
        if (istext(type_id_datum))
        {
            final = LAZYACCESS(movespeed_modification, type_id_datum) as datum_movespeed_modifier;
            if (!Dm.truthy(final))
                throw Dm.CRASH("Couldn't find existing modification when provided a text ID.", "code/modules/movespeed/_movespeed_modifier.dm", 126);
        }
        else if (ispath(type_id_datum))
        {
            if (!Dm.truthy(Dm.initial(type_id_datum, (datum_movespeed_modifier p) => p.variable)))
                throw Dm.CRASH("Not a variable modifier", "code/modules/movespeed/_movespeed_modifier.dm", 129);
            final = LAZYACCESS(movespeed_modification, (Dm.initial(type_id_datum, (datum_movespeed_modifier p) => p.id) is var or__1 && Dm.truthy(or__1) ? or__1 : (object)Dm.text("[]", type_id_datum))) as datum_movespeed_modifier;
            if (!Dm.truthy(final))
            {
                final = await Dm.new_async((Type)type_id_datum) as datum_movespeed_modifier;
                inject = TRUE;
                modified = TRUE;
            }
        }
        else
        {
            if (!Dm.truthy(Dm.initial(type_id_datum, (datum_movespeed_modifier p) => p.variable)))
                throw Dm.CRASH("Not a variable modifier", "code/modules/movespeed/_movespeed_modifier.dm", 137);
            final = type_id_datum as datum_movespeed_modifier;
            if (!Dm.truthy(LAZYACCESS(movespeed_modification, final.id)))
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
            await add_movespeed_modifier(final, FALSE);
        if (Dm.truthy(update) && Dm.truthy(modified))
            update_movespeed(); // DM passes TRUE, which update_movespeed does not declare (dropped, rules 7.3)
        return final;
    }

    ///Is there a movespeed modifier for this mob
    // tg: code/modules/movespeed/_movespeed_modifier.dm:153
    public virtual object has_movespeed_modifier(object datum_type_id = null)
    {
        object key = null;
        if (ispath(datum_type_id))
            key = (Dm.initial(datum_type_id, (datum_movespeed_modifier p) => p.id) is var or__1 && Dm.truthy(or__1) ? or__1 : (object)Dm.text("[]", datum_type_id));
        else if (istext(datum_type_id))
            key = datum_type_id;
        else
            key = ((datum_movespeed_modifier)datum_type_id).id;
        return LAZYACCESS(movespeed_modification, key);
    }

    /// Set or update the global movespeed config on a mob
    // tg: code/modules/movespeed/_movespeed_modifier.dm:164
    public virtual async DmTask update_config_movespeed()
    {
        await add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_mob_config_speedmod), multiplicative_slowdown: get_config_multiplicative_speed());
    }

    /// Get the global config movespeed of a mob by type
    // tg: code/modules/movespeed/_movespeed_modifier.dm:168
    public virtual object get_config_multiplicative_speed()
    {
        if (!islist(GLOB.mob_config_movespeed_type_lookup) || !Dm.truthy(GLOB.mob_config_movespeed_type_lookup[type]))
            return 0;
        else
            return GLOB.mob_config_movespeed_type_lookup[type];
    }

    /// Go through the list of movespeed modifiers and calculate a final movespeed. ANY ADD/REMOVE DONE IN UPDATE_MOVESPEED MUST HAVE THE UPDATE ARGUMENT SET AS FALSE!
    // tg: code/modules/movespeed/_movespeed_modifier.dm:175
    public virtual object update_movespeed()
    {
        object dot = null;
        dot = 0;
        DmList conflict_tracker = Dm.list();
        foreach (object key in Dm.each(get_movespeed_modifiers()))
        {
            datum_movespeed_modifier M = movespeed_modification[key] as datum_movespeed_modifier;
            if (!Dm.truthy(Dm.bitand(M.movetypes, movement_type))) // We don't affect any of these move types, skip
                continue;
            if (Dm.truthy(Dm.bitand(M.blacklisted_movetypes, movement_type))) // There's a movetype here that disables this modifier, skip
                continue;
            object conflict = M.conflicts_with;
            object amt = M.multiplicative_slowdown;
            if (Dm.truthy(conflict))
            {
                // Conflicting modifiers prioritize the larger slowdown or the larger speedup
                // We purposefuly don't handle mixing speedups and slowdowns on the same id
                if (abs(Dm.num(conflict_tracker[conflict])) < abs(Dm.num(amt)))
                    conflict_tracker[conflict] = amt;
                else
                    continue;
            }
            Dm.append(ref dot, amt);
        }
        cached_multiplicative_slowdown = dot;
        if (Dm.truthy(this._listen_lookup?[COMSIG_MOB_MOVESPEED_UPDATED]))
            this._SendSignal(COMSIG_MOB_MOVESPEED_UPDATED, Dm.list(this));
        return dot;
    }

    /// Get the move speed modifiers list of the mob
    // tg: code/modules/movespeed/_movespeed_modifier.dm:198
    public virtual object get_movespeed_modifiers()
    {
        DmList dot = null;
        dot = LAZYCOPY(movespeed_modification);
        foreach (object id in Dm.each(movespeed_mod_immunities))
            Dm.remove(ref dot, id);
        return dot;
    }
}

public static partial class GlobalProcs
{
    /// Checks if a move speed modifier is valid and not missing any data
    // tg: code/modules/movespeed/_movespeed_modifier.dm:204
    public static object movespeed_data_null_check(datum_movespeed_modifier M = null) //Determines if a data list is not meaningful and should be discarded.
    {
        object dot = null;
        dot = TRUE;
        if (Dm.truthy(M.multiplicative_slowdown))
            dot = FALSE;
        return dot;
    }
}
