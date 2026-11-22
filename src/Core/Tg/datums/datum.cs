// Translated from tgstation (snapshot 1d99bd7f): code/datums/datum.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * The absolute base class for everything
 *
 * A datum instantiated has no physical world presence, use an atom if you want something
 * that actually lives in the world
 *
 * Be very mindful about adding variables to this class, they are inherited by every single
 * thing in the entire game, and so you can easily cause memory usage to rise a lot with careless
 * use of variables at this level
 */
// tg: code/datums/datum.dm:11
public partial class datum
{
    /**
      * Tick count time when this object was destroyed.
      *
      * If this is non zero then the object has been garbage collected and is awaiting either
      * a hard del by the GC subsystme, or to be autocollected (if it has no references)
      */
    // tg: code/datums/datum.dm:18
    public object gc_destroyed;

    /// Open uis owned by this datum
    /// Lazy, since this case is semi rare
    // tg: code/datums/datum.dm:22
    public DmList open_uis;

    /// Active timers with this datum as the target
    // tg: code/datums/datum.dm:25
    public DmList _active_timers;
    /// Status traits attached to this datum. associative list of the form: list(trait name (string) = list(source1, source2, source3,...))
    // tg: code/datums/datum.dm:27
    public DmList _status_traits;

    /**
      * Components attached to this datum
      *
      * Lazy associated list in the structure of `type -> component/list of components`
      */
    // tg: code/datums/datum.dm:34
    public DmList _datum_components;
    /**
      * Any datum registered to receive signals from this datum is in this list
      *
      * Lazy associated list in the structure of `signal -> registree/list of registrees`
      */
    // tg: code/datums/datum.dm:40
    public DmList _listen_lookup;
    /// Lazy associated list in the structure of `target -> list(signal -> proctype)` that are run when the datum receives that signal
    // tg: code/datums/datum.dm:42
    public DmList _signal_procs;

    /// Datum level flags
    // tg: code/datums/datum.dm:45
    public float datum_flags = NONE;

    /// A weak reference to another datum
    // tg: code/datums/datum.dm:48
    public datum_weakref weak_reference;

    /*
    * Lazy associative list of currently active cooldowns.
    *
    * cooldowns [ COOLDOWN_INDEX ] = add_timer()
    * add_timer() returns the truthy value of -1 when not stoppable, and else a truthy numeric index
    */
    // tg: code/datums/datum.dm:56
    public DmList cooldowns;

    /// List for handling persistent filters.
    // tg: code/datums/datum.dm:59
    public DmList filter_data;
    /// An accursed beast of a list that contains our filters. Why? Because var/list/filters on atoms/images isn't actually a list
    /// but a snowflaked skinwalker pretending to be one, which doesn't support half the list procs/operations and the other half behaves weirdly
    /// so we cut down on filter creation and appearance update costs by editing *this* list, and then assigning ours to it
    // tg: code/datums/datum.dm:63
    public DmList filter_cache;

    // tg: #ifdef REFERENCE_TRACKING ... #endif not translated (inactive in production)

    // If we have called dump_harddel_info already. Used to avoid duped calls (since we call it immediately in some cases on failure to process)
    // Create and destroy is weird and I wanna cover my bases
    // tg: code/datums/datum.dm:80
    public float harddel_deets_dumped = FALSE;

    // tg: #ifdef DATUMVAR_DEBUGGING_MODE ... #endif not translated (inactive in production)
    ///The layout pref we take from the player looking at this datum's UI to know what layout to give.
    // tg: code/datums/datum.dm:86
    public Type layout_prefs_used = typeof(datum_preference_choiced_tgui_layout);

    /**
     * Parent types.
     *
     * Abstract-ness is a meta-property of a class that is used to indicate
     * that the class is intended to be used as a base class for others, and
     * should not (or cannot) be instantiated.
     * We have no such language concept in DM, and so we provide a datum member
     * that can be used to hint at abstractness for circumstances where we would
     * like that to be the case, such as base behavior providers.
     */
    // tg: code/datums/datum.dm:98
    public Type abstract_type = typeof(datum);

    /**
     * Called when a href for this datum is clicked
     *
     * Sends a [COMSIG_TOPIC] signal
     */
    // tg: code/datums/datum.dm:105
    public virtual async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        Topic__d0(href, href_list); // D154: a chat link reaches this through ClientSeam.Topic
        if (Dm.truthy(this._listen_lookup?[COMSIG_TOPIC]))
            this._SendSignal(COMSIG_TOPIC, Dm.list(this, usr, href_list));
        return null;
    }

    /**
     * Default implementation of clean-up code.
     *
     * This should be overridden to remove all references pointing to the object being destroyed, if
     * you do override it, make sure to call the parent and return its return value by default
     *
     * Return an appropriate [QDEL_HINT][QDEL_HINT_QUEUE] to modify handling of your deletion;
     * in most cases this is [QDEL_HINT_QUEUE].
     *
     * The base case is responsible for doing the following
     * * Erasing timers pointing to this datum
     * * Erasing compenents on this datum
     * * Notifying datums listening to signals from this datum that we are going away
     *
     * Returns [QDEL_HINT_QUEUE]
     */
    // tg: code/datums/datum.dm:125
    [DmProtected, ShouldCallParent, ShouldNotSleep]
    public virtual object Destroy(float? force = null)
    {
        force ??= FALSE;
        tag = null;
        Dm.mask(ref datum_flags, Dm.bitnot(DF_USE_TAG)); //In case something tries to REF us
        weak_reference = null; //ensure prompt GCing of weakref.

        if (Dm.truthy(_active_timers))
        {
            DmList timers = _active_timers;
            _active_timers = null;
            foreach (datum_timedevent timer in Dm.each_cast<datum_timedevent>(timers))
            {
                if (Dm.truthy(timer.spent) && !Dm.truthy(Dm.bitand(timer.flags, TIMER_DELETE_ME)))
                    continue;
                qdel(timer);
            }
        }

        // tg: #ifdef REFERENCE_TRACKING ... #endif not translated (inactive in production)

        //BEGIN: ECS SHIT
        DmList dc = _datum_components;
        if (Dm.truthy(dc))
        {
            foreach (object component_key in Dm.each(dc))
            {
                object component_or_list = dc[component_key];
                if (islist(component_or_list))
                {
                    foreach (datum_component component in Dm.each_cast<datum_component>(component_or_list))
                        qdel(component, FALSE);
                }
                else
                {
                    datum_component C = component_or_list as datum_component;
                    qdel(C, FALSE);
                }
            }
            dc.Cut();
        }

        _clear_signal_refs();
        //END: ECS SHIT

        // tg: #ifndef DISABLE_DREAMLUAU ... #endif not translated (inactive in production)

        return QDEL_HINT_QUEUE;
    }

    ///Only override this if you know what you're doing. You do not know what you're doing
    ///This is a threat
    // tg: code/datums/datum.dm:173
    public virtual void _clear_signal_refs()
    {
        DmList lookup = _listen_lookup;
        if (Dm.truthy(lookup))
        {
            foreach (object sig in Dm.each(lookup))
            {
                object comps = lookup[sig];
                if (Dm.truthy(length(comps)))
                {
                    foreach (object comp in Dm.each(comps))
                        ((datum)comp).UnregisterSignal(this, sig);
                }
                else
                {
                    object comp = comps;
                    ((datum)comp).UnregisterSignal(this, sig);
                }
            }
            _listen_lookup = lookup = null;
        }

        foreach (object target in Dm.each(_signal_procs))
            UnregisterSignal(target as datum, _signal_procs[target]);
    }

    // tg: #ifdef DATUMVAR_DEBUGGING_MODE ... #endif not translated (inactive in production)

    /// Return a list of data which can be used to investigate the datum, also ensure that you set the semver in the options list
    // tg: code/datums/datum.dm:217
    [ShouldCallParent]
    public virtual object serialize_list(DmList options = null, DmList semvers = null)
    {
        Dm.NotTranslated("code/datums/datum.dm:217", "serialize_list: datum serialization has no M0 consumer");
        return default;
    }

    ///Accepts a LIST from deserialize_datum. Should return whether or not the deserialization was successful.
    // tg: code/datums/datum.dm:227
    [ShouldCallParent]
    public virtual object deserialize_list(object json = null, DmList options = null)
    {
        Dm.NotTranslated("code/datums/datum.dm:227", "deserialize_list: datum serialization has no M0 consumer");
        return default;
    }

    ///Serializes into JSON. Does not encode type.
    // tg: code/datums/datum.dm:232
    public virtual object serialize_json(DmList options = null)
    {
        Dm.NotTranslated("code/datums/datum.dm:232", "serialize_json: datum serialization has no M0 consumer");
        return default;
    }

    ///Deserializes from JSON. Does not parse type.
    // tg: code/datums/datum.dm:240
    public virtual object deserialize_json(DmList input = null, DmList options = null)
    {
        Dm.NotTranslated("code/datums/datum.dm:240", "deserialize_json: datum serialization has no M0 consumer");
        return default;
    }

    ///Generate a tag for this /datum, if it implements one
    ///Should be called as early as possible, best would be in New, to avoid weakref mistargets
    ///Really just don't use this, you don't need it, global lists will do just fine MOST of the time
    ///We really only use it for mobs to make id'ing people easier
    // tg: code/datums/datum.dm:321
    public virtual void GenerateTag()
    {
        Dm.combine(ref datum_flags, DF_USE_TAG);
    }

    /** Add a filter to the datum.
     * This is on datum level, despite being most commonly / primarily used on atoms, so that filters can be applied to images / mutable appearances.
     * Can also be used to assert a filter's existence. I.E. update a filter regardless if it exists or not.
     *
     * Arguments:
     * * name - Filter name
     * * priority - Priority used when sorting the filter.
     * * params - Parameters of the filter.
     * * update - If we should update our actual filters list, or wait until something updates it later
     */
    // tg: code/datums/datum.dm:334  (M6 slice 7, D155: filters reach the client, protocol version 25)
    public virtual void add_filter(object name = null, object priority = null, DmList @params = null, float? update = null)
    {
        update ??= TRUE;
        if (!(isatom(this) || isimage(this))) throw Dm.CRASH(assertion_message("code/datums/datum.dm", 335, "isatom(src) || isimage(src)"), "code/datums/datum.dm", 335);
        var atom_cast = (atom)this; // filters only work with images or atoms.
        LAZYINITLIST(ref filter_data);
        LAZYINITLIST(ref filter_cache);
        DmList copied_parameters = @params.Copy();
        copied_parameters["name"] = name;
        copied_parameters["priority"] = priority;
        for (float index = 1; index <= length(filter_data); index++)
        {
            var filter_info = filter_data[index] as DmList;
            if (!Dm.eq(filter_info["name"], name))
                continue;
            Dm.remove(ref filter_data, Dm.list(filter_info));
            Dm.remove(ref filter_cache, filter_cache[index]);
            break;
        }

        // BINARY_INSERT_DEFINE(list(copied_parameters), filter_data, SORT_VAR_NO_TYPE, copied_parameters, SORT_PRIORITY_INDEX, COMPARE_KEY)
        // (__HELPERS/_lists.dm:216, inline by rules 11.1 (a), (b)): SORT_VAR_NO_TYPE declares an untyped item, SORT_PRIORITY_INDEX
        // compares the items' "priority", COMPARE_KEY reads __BIN_LIST[__BIN_MID].
        do
        {
            DmList __BIN_LIST = filter_data;
            float __BIN_CTTL = length(__BIN_LIST);
            if (!Dm.truthy(__BIN_CTTL))
                Dm.append(ref __BIN_LIST, Dm.list(copied_parameters));
            else
            {
                float __BIN_LEFT = 1;
                float __BIN_RIGHT = __BIN_CTTL;
                float __BIN_MID = Dm.shr(__BIN_LEFT + __BIN_RIGHT, 1);
                object __BIN_ITEM = null;
                while (__BIN_LEFT < __BIN_RIGHT)
                {
                    __BIN_ITEM = __BIN_LIST[__BIN_MID];
                    if (Dm.le(SORT_PRIORITY_INDEX(__BIN_ITEM), SORT_PRIORITY_INDEX(copied_parameters)))
                        __BIN_LEFT = __BIN_MID + 1;
                    else
                        __BIN_RIGHT = __BIN_MID;
                    __BIN_MID = Dm.shr(__BIN_LEFT + __BIN_RIGHT, 1);
                }
                __BIN_ITEM = __BIN_LIST[__BIN_MID];
                __BIN_MID = Dm.gt(SORT_PRIORITY_INDEX(__BIN_ITEM), SORT_PRIORITY_INDEX(copied_parameters)) ? __BIN_MID : __BIN_MID + 1;
                __BIN_LIST.Insert(__BIN_MID, Dm.list(copied_parameters));
            }
        }
        while (Dm.truthy(FALSE));

        for (float index = 1; index <= length(filter_data); index++)
        {
            var filter_info = filter_data[index] as DmList;
            if (!Dm.eq(filter_info["name"], name))
                continue;
            DmList arguments = filter_info.Copy();
            Dm.remove(ref arguments, "priority");
            filter_cache.Insert(index, Dm.filter(Dm.arglist(arguments)));
            break;
        }

        if (Dm.truthy(update))
            atom_cast.filters = filter_cache;
    }

    /// A version of add_filter that takes a list of filters to add rather than being individual, to limit appearance updates
    // tg: code/datums/datum.dm:365
    public virtual void add_filters(DmList filters = null, float? update = null)
    {
        update ??= TRUE;
        if (!(isatom(this) || isimage(this))) throw Dm.CRASH(assertion_message("code/datums/datum.dm", 366, "isatom(src) || isimage(src)"), "code/datums/datum.dm", 366);
        var atom_cast = (atom)this; // filters only work with images or atoms.
        foreach (DmList individual_filter in Dm.each_cast<DmList>(filters))
            add_filter(individual_filter["name"], individual_filter["priority"], individual_filter["params"] as DmList, update: FALSE);
        if (Dm.truthy(update))
            atom_cast.filters = filter_cache;
    }

    /// Reapplies all the filters. If start_index is passed, only a portion of all filters are reapplied starting from said index
    // tg: code/datums/datum.dm:374
    public virtual async DmTask update_filters(object start_index = null)
    {
        if (!(isatom(this) || isimage(this))) throw Dm.CRASH(assertion_message("code/datums/datum.dm", 375, "isatom(src) || isimage(src)"), "code/datums/datum.dm", 375);
        var atom_cast = (atom)this; // filters only work with images or atoms.
        if (Dm.truthy(start_index))
            filter_cache.Cut(Dm.num(start_index));
        else
        {
            atom_cast.filters = null;
            filter_cache.Cut();
        }

        for (float index = Dm.truthy(start_index) ? Dm.num(start_index) : 1; index <= length(filter_data); index++)
        {
            var filter_info = filter_data[index] as DmList;
            DmList arguments = filter_info.Copy();
            Dm.remove(ref arguments, "priority");
            if (Dm.truthy(start_index)) // See https://www.byond.com/forum/post/2980598 as to why we cannot just override the existing filter
            {
                object current = atom_cast.filters;
                Dm.remove(ref current, filter_info["name"]); // We're trapped in the belly of this horrible machine
                atom_cast.filters = current;
            }
            Dm.append(ref filter_cache, Dm.filter(Dm.arglist(arguments))); // And the machine is bleeding to death
        }

        atom_cast.filters = filter_cache;
        UNSETEMPTY(ref filter_data);
    }
}

// tg: code/datums/datum.dm:394
public partial class obj_item : obj
{
    // tg: code/datums/datum.dm:394
    public override async DmTask update_filters(object start_index = null)
    {
        await base.update_filters(start_index);
        await update_item_action_buttons();
    }
}

public partial class datum
{
    /** Update a filter's parameter to the new one. If the filter doesn't exist we won't do anything.
     *
     * Arguments:
     * * name - Filter name
     * * new_params - New parameters of the filter
     * * overwrite - TRUE means we replace the parameter list completely. FALSE means we only replace the things on new_params.
     * * update - If we should apply our filter cache to our actual filters
     */
    // tg: code/datums/datum.dm:406
    public virtual void modify_filter(object name = null, DmList new_params = null, float? overwrite = null, float? update = null)
    {
        overwrite ??= FALSE;
        update ??= TRUE;
        if (!(isatom(this) || isimage(this))) throw Dm.CRASH(assertion_message("code/datums/datum.dm", 407, "isatom(src) || isimage(src)"), "code/datums/datum.dm", 407);
        var atom_cast = (atom)this; // filters only work with images or atoms.
        for (float index = 1; index <= length(filter_data); index++)
        {
            var filter_info = filter_data[index] as DmList;
            if (!Dm.eq(filter_info["name"], name))
                continue;

            if (Dm.truthy(overwrite))
                filter_data[index] = new_params;
            else
            {
                foreach (object thing in Dm.each(new_params))
                    filter_info[thing] = new_params[thing];
            }

            DmList arguments = filter_info.Copy();
            Dm.remove(ref arguments, "priority");
            filter_cache[index] = Dm.filter(Dm.arglist(arguments));

            if (Dm.truthy(update))
                atom_cast.filters = filter_cache;
            return;
        }
    }

    /** Update a filter's parameter and animate this change. If the filter doesn't exist we won't do anything.
     * Basically a [datum/proc/modify_filter] call but with animations. Unmodified filter parameters are kept.
     *
     * Arguments:
     * * name - Filter name
     * * new_params - New parameters of the filter
     * * time - time arg of the BYOND animate() proc.
     * * easing - easing arg of the BYOND animate() proc.
     * * loop - loop arg of the BYOND animate() proc.
     */
    // tg: code/datums/datum.dm:438
    public virtual void transition_filter(object name = null, DmList new_params = null, object time = null, object easing = null, object loop = null)
    {
        object filter = get_filter(name);
        if (!Dm.truthy(filter))
            return;
        // This can get injected by the filter procs, we want to support them so bye byeeeee
        Dm.remove(ref new_params, "type");
        Dm.animate(filter, Dm.arglist(new_params), Dm.named("time", time), Dm.named("easing", easing), Dm.named("loop", loop));
        modify_filter(name, new_params);
    }

    /** Similar to transition_filter(), except it creates an animation chain that moves between a list of states.
     * Arguments:
     * * name - Filter name
     * * num_loops - Amount of times the chain loops. INDEFINITE = Infinite
     * * ... - a list of each link in the animation chain. Use filter_chain_step(params, duration, easing) for each link
     * Example use:
     * * add_filter("blue_pulse", 1, color_matrix_filter(COLOR_WHITE))
     * * transition_filter_chain(src, "blue_pulse", INDEFINITE,\
     * *	filter_chain_step(color_matrix_filter(COLOR_BLUE), 10 SECONDS, CUBIC_EASING),\
     * *	filter_chain_step(color_matrix_filter(COLOR_WHITE), 10 SECONDS, CUBIC_EASING))
     * The above code would edit a color_matrix_filter() to slowly turn blue over 10 seconds before returning back to white 10 seconds after, repeating this chain forever.
     */
    // tg: code/datums/datum.dm:469
    public virtual void transition_filter_chain(object name = null, object num_loops = null, params object[] __extra)
    {
        DmList transition_steps = Dm.list(__extra);
        object filter = get_filter(name);
        if (!Dm.truthy(filter))
            return;
        var first_step = transition_steps[1] as DmList;
        Dm.animate(filter, Dm.arglist(first_step["params"]), Dm.named("time", first_step["duration"]), Dm.named("easing", first_step["easing"]), Dm.named("flags", first_step["flags"]), Dm.named("loop", num_loops));
        for (float transition_step = 2; transition_step <= length(transition_steps); transition_step++)
        {
            var this_step = transition_steps[transition_step] as DmList;
            Dm.animate(Dm.arglist(this_step["params"]), Dm.named("time", this_step["duration"]), Dm.named("easing", this_step["easing"]), Dm.named("flags", this_step["flags"]));
        }
    }

    /// Updates the priority of the passed filter key
    // tg: code/datums/datum.dm:481
    public virtual void change_filter_priority(object name = null, object new_priority = null)
    {
        foreach (DmList filter_info in Dm.each_cast<DmList>(filter_data))
        {
            if (!Dm.eq(filter_info["name"], name))
                continue;

            remove_filter(name, update: FALSE);
            add_filter(name, new_priority, filter_info);
            return;
        }
    }

    /// Returns the filter associated with the passed key
    // tg: code/datums/datum.dm:491
    public virtual object get_filter(object name = null)
    {
        if (!(isatom(this) || isimage(this))) throw Dm.CRASH(assertion_message("code/datums/datum.dm", 492, "isatom(src) || isimage(src)"), "code/datums/datum.dm", 492);
        var atom_cast = (atom)this; // filters only work with images or atoms.
        // BYOND's filters list of an atom without filters is empty (a lookup gives null); the port keeps null for none.
        return (atom_cast.filters as DmList)?[name];
    }

    /// Returns filter data associated with the passed key
    // tg: code/datums/datum.dm:497
    public virtual object get_filter_data(object name = null)
    {
        foreach (DmList filter_info in Dm.each_cast<DmList>(filter_data))
        {
            if (Dm.eq(filter_info["name"], name))
                return filter_info.Copy();
        }
        return null;
    }

    /// Removes the passed filter, or multiple filters, if supplied with a list.
    // tg: code/datums/datum.dm:503
    public virtual object remove_filter(object name_or_names = null, float? update = null)
    {
        update ??= TRUE;
        if (!(isatom(this) || isimage(this))) throw Dm.CRASH(assertion_message("code/datums/datum.dm", 504, "isatom(src) || isimage(src)"), "code/datums/datum.dm", 504);
        if (!Dm.truthy(filter_data))
            return null;
        var atom_cast = (atom)this; // filters only work with images or atoms.
        DmList names = islist(name_or_names) ? name_or_names as DmList : Dm.list(name_or_names);
        object dot = FALSE;
        DmList new_data = Dm.list();
        DmList new_cache = Dm.list();
        for (float index = 1; index <= length(filter_data); index++)
        {
            var filter_info = filter_data[index] as DmList;
            if (!Dm.@in(filter_info["name"], names))
            {
                Dm.append(ref new_data, Dm.list(filter_info));
                Dm.append(ref new_cache, filter_cache[index]);
            }
        }
        filter_data = new_data;
        filter_cache = new_cache;
        if (Dm.truthy(update))
            atom_cast.filters = filter_cache;
        return dot;
    }

    // tg: code/datums/datum.dm:523
    public virtual void clear_filters()
    {
        if (!(isatom(this) || isimage(this))) throw Dm.CRASH(assertion_message("code/datums/datum.dm", 524, "isatom(src) || isimage(src)"), "code/datums/datum.dm", 524);
        var atom_cast = (atom)this; // filters only work with images or atoms.
        filter_data = null;
        filter_cache = null;
        atom_cast.filters = null;
    }

    /// Calls qdel on itself, because signals dont allow callbacks
    // tg: code/datums/datum.dm:531
    [SignalHandler]
    public virtual void selfdelete()
    {
        qdel(this);
    }

    /// Return text from this proc to provide extra context to hard deletes that happen to it
    /// Optional, you should use this for cases where replication is difficult and extra context is required
    /// Can be called more then once per object, use harddel_deets_dumped to avoid duplicate calls (I am so sorry)
    // tg: code/datums/datum.dm:538
    public virtual object dump_harddel_info()
    {
        return null;
    }

    // tg: code/datums/datum.dm:542 /image/dump_harddel_info is not translated here: where /image lives is an open item
    // (docs/ai/specs/shim-ownership.md), so there is no class to override in; it gets its Dm.NotTranslated stub with that
    // class (presentation, M3).
}

public static partial class GlobalProcs
{
    ///Convert a datum into a json blob
    // tg: code/datums/datum.dm:247
    public static object json_serialize_datum(datum D = null, DmList options = null)
    {
        Dm.NotTranslated("code/datums/datum.dm:247", "json_serialize_datum: datum serialization has no M0 consumer");
        return default;
    }

    /// Convert a list of json to datum
    // tg: code/datums/datum.dm:256
    public static async DmTask<object> json_deserialize_datum(object jsonlist = null, DmList options = null, object target_type = null, float? strict_target_type = null)
    {
        Dm.NotTranslated("code/datums/datum.dm:256", "json_deserialize_datum: datum serialization has no M0 consumer");
        return default;
    }

    /**
     * Callback called by a timer to end an associative-list-indexed cooldown.
     *
     * Arguments:
     * * source - datum storing the cooldown
     * * index - string index storing the cooldown on the cooldowns associative list
     *
     * This sends a signal reporting the cooldown end.
     */
    // tg: code/datums/datum.dm:295
    public static void end_cooldown(datum source = null, object index = null)
    {
        if (QDELETED(source))
            return;
        string t__1 = COMSIG_CD_STOP(index);
        if (Dm.truthy(source._listen_lookup?[t__1]))
            source._SendSignal(t__1, Dm.list(source));
        TIMER_COOLDOWN_END(source, index);
    }

    /**
     * Proc used by stoppable timers to end a cooldown before the time has ran out.
     *
     * Arguments:
     * * source - datum storing the cooldown
     * * index - string index storing the cooldown on the cooldowns associative list
     *
     * This sends a signal reporting the cooldown end, passing the time left as an argument.
     */
    // tg: code/datums/datum.dm:311
    public static void reset_cooldown(datum source = null, object index = null)
    {
        if (QDELETED(source))
            return;
        string t__1 = COMSIG_CD_RESET(index);
        if (Dm.truthy(source._listen_lookup?[t__1]))
            source._SendSignal(t__1, Dm.list(source, S_TIMER_COOLDOWN_TIMELEFT(source, index)));
        TIMER_COOLDOWN_END(source, index);
    }

    /** Keeps the steps in the correct order.
    * Arguments:
    * * params - the parameters you want this step to animate to
    * * duration - the time it takes to animate this step
    * * easing - the type of easing this step has
    */
    // tg: code/datums/datum.dm:453
    public static object filter_chain_step(object @params = null, object duration = null, object easing = null, object flags = null)
    {
        Dm.remove(ref @params, "type");
        return Dm.list(Dm.kv("params", @params), Dm.kv("duration", duration), Dm.kv("easing", easing), Dm.kv("flags", flags));
    }
}
