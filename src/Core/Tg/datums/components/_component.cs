// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/_component.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * # Component
 *
 * The component datum
 *
 * A component should be a single standalone unit
 * of functionality, that works by receiving signals from its parent
 * object to provide some single functionality (i.e a slippery component)
 * that makes the object it's attached to cause people to slip over.
 * Useful when you want shared behaviour independent of type inheritance
 */
// tg: code/datums/components/_component.dm:12
[DmPath("/datum/component")]
public partial class datum_component : datum
{
    /**
      * Defines how duplicate existing components are handled when added to a datum
      *
      * See [COMPONENT_DUPE_*][COMPONENT_DUPE_ALLOWED] definitions for available options
      */
    // tg: code/datums/components/_component.dm:18
    public float dupe_mode = COMPONENT_DUPE_HIGHLANDER;

    /// The datum this components belongs to
    // tg: code/datums/components/_component.dm:21
    public datum parent;

    /**
      * Only set to true if you are able to properly transfer this component
      *
      * At a minimum [RegisterWithParent][/datum/component/proc/RegisterWithParent] and [UnregisterFromParent][/datum/component/proc/UnregisterFromParent] should be used
      *
      * Make sure you also implement [PostTransfer][/datum/component/proc/PostTransfer] for any post transfer handling
      */
    // tg: code/datums/components/_component.dm:30
    public float can_transfer = FALSE;

    /// A lazy list of the sources for this component
    // tg: code/datums/components/_component.dm:33
    public DmList sources;

    /**
     * Create a new component.
     *
     * Additional arguments are passed to [Initialize()][/datum/component/proc/Initialize]
     *
     * Arguments:
     * * datum/P the parent datum this component reacts to signals from
     */
    // tg: code/datums/components/_component.dm:43
    [DmParams("raw_args")]
    public override async DmTask New(params object[] __extra)
    {
        DmList raw_args = Dm.arg<DmList>(__extra, 0);
        parent = raw_args[1] as datum;
        DmList arguments = raw_args.Copy(2);

        object result = await Dm.Call(this, nameof(Initialize), Dm.arglist(arguments));

        if (Dm.eq(result, COMPONENT_INCOMPATIBLE))
        {
            stack_trace(Dm.text("Incompatible [] assigned to a []! args: []", type, parent.type, json_encode(arguments)), "code/datums/components/_component.dm", 50);
            // tg passes a third argument (TRUE) that qdel does not declare; it is dropped (rules 7.3).
            qdel(this, TRUE);
            return;
        }

        if (Dm.eq(result, COMPONENT_REDUNDANT))
        {
            // tg passes a third argument (TRUE) that qdel does not declare; it is dropped (rules 7.3).
            qdel(this, TRUE);
            return;
        }

        if (QDELETED(this) || QDELETED(parent))
            throw Dm.CRASH(Dm.text("Component [] was created with a deleted parent or was deleted itself before it could be added to a parent", type), "code/datums/components/_component.dm", 59);

        await _JoinParent();
    }

    /**
     * Called during component creation with the same arguments as in new excluding parent.
     *
     * Do not call `qdel(src)` from this function, `return COMPONENT_INCOMPATIBLE` instead
     */
    // tg: code/datums/components/_component.dm:68
    public virtual async DmTask<object> Initialize(params object[] __extra)
    {
        return null;
    }

    /**
     * Properly removes the component from `parent` and cleans up references
     *
     * Arguments:
     * * force - makes it not check for and remove the component from the parent
     */
    // tg: code/datums/components/_component.dm:77
    public override object Destroy(float? force = null)
    {
        force ??= FALSE;
        if (!Dm.truthy(parent))
            return base.Destroy(force);
        if (!Dm.truthy(force))
            Waiting.NoSleep(_RemoveFromParent());
        if (Dm.truthy(parent._listen_lookup?[COMSIG_COMPONENT_REMOVING]))
            parent._SendSignal(COMSIG_COMPONENT_REMOVING, Dm.list(parent, this));
        parent = null;
        return base.Destroy(force);
    }

    /**
     * Internal proc to handle behaviour of components when joining a parent
     */
    // tg: code/datums/components/_component.dm:89
    public virtual async DmTask _JoinParent()
    {
        datum P = parent;
        //lazy init the parent's dc list
        DmList dc = P._datum_components;
        if (!Dm.truthy(dc))
            P._datum_components = dc = Dm.list();

        //set up the typecache
        object our_type = type;
        foreach (object I in Dm.each(_GetInverseTypeList(our_type)))
        {
            object test = dc[I];
            if (Dm.truthy(test)) //already another component of this type here
            {
                DmList components_of_type = null;
                if (!Dm.truthy(length(test)))
                {
                    components_of_type = Dm.list(test);
                    dc[I] = components_of_type;
                }
                else
                    components_of_type = test as DmList;
                if (Dm.eq(I, our_type)) //exact match, take priority
                {
                    float inserted = FALSE;
                    for (float J = 1, J__end = components_of_type.len; J <= J__end; J++)
                    {
                        datum_component C = components_of_type[J] as datum_component;
                        if (!Dm.eq(C.type, our_type)) //but not over other exact matches
                        {
                            // DEVIATION(DV8): tg inserts I, the type key, which leaves a typepath in the parent's
                            // component list where this component belongs (a tg bug); the port inserts src.
                            // tg: code/datums/components/_component.dm:112  components_of_type.Insert(J, I)
                            components_of_type.Insert(J, this);
                            inserted = TRUE;
                            break;
                        }
                    }
                    if (!Dm.truthy(inserted))
                        Dm.append(ref components_of_type, this);
                }
                else //indirect match, back of the line with ya
                    Dm.append(ref components_of_type, this);
            }
            else //only component of this type, no list
                dc[I] = this;
        }

        await RegisterWithParent();
    }

    /**
     * Internal proc to handle behaviour when being removed from a parent
     */
    // tg: code/datums/components/_component.dm:127
    public virtual async DmTask _RemoveFromParent()
    {
        datum parent = this.parent;
        DmList parents_components = parent._datum_components;
        foreach (object I in Dm.each(_GetInverseTypeList()))
        {
            DmList components_of_type = parents_components[I] as DmList;

            if (Dm.truthy(length(components_of_type))) //
            {
                DmList subtracted = Dm.sub(components_of_type, this) as DmList;

                if (subtracted.len == 1) //only 1 guy left
                    parents_components[I] = subtracted[1]; //make him special
                else
                    parents_components[I] = subtracted;
            }

            else //just us
                Dm.remove(ref parents_components, I);
        }

        if (!Dm.truthy(parents_components.len))
            parent._datum_components = null;

        await UnregisterFromParent();
    }

    /**
     * Register the component with the parent object
     *
     * Use this proc to register with your parent object
     *
     * Overridable proc that's called when added to a new parent
     */
    // tg: code/datums/components/_component.dm:156
    public virtual async DmTask RegisterWithParent()
    {
        return;
    }

    /**
     * Unregister from our parent object
     *
     * Use this proc to unregister from your parent object
     *
     * Overridable proc that's called when removed from a parent
     * *
     */
    // tg: code/datums/components/_component.dm:167
    public virtual async DmTask UnregisterFromParent(params object[] __extra)
    {
        return;
    }

    /**
     * Called when the component has a new source registered.
     * Return COMPONENT_INCOMPATIBLE to signal that the source is incompatible and should not be added
     */
    // tg: code/datums/components/_component.dm:174
    [ShouldCallParent]
    public virtual async DmTask<object> on_source_add(object source = null, params object[] __extra)
    {
        if (dupe_mode != COMPONENT_DUPE_SOURCES)
            return COMPONENT_INCOMPATIBLE;
        LAZYOR(ref sources, source);
        return null;
    }

    /**
     * Called when the component has a source removed.
     * You probably want to call parent after you do your logic because at the end of this we qdel if we have no sources remaining!
     */
    // tg: code/datums/components/_component.dm:184
    [ShouldCallParent]
    public virtual void on_source_remove(object source = null)
    {
        if (dupe_mode != COMPONENT_DUPE_SOURCES)
            throw Dm.CRASH(Dm.text("Component '[]' does not use sources but is trying to remove a source", type), "code/datums/components/_component.dm", 187);
        LAZYREMOVE(ref sources, source);
        if (!Dm.truthy(LAZYLEN(sources)))
            qdel(this);
    }

    /**
     * Called on a component when a component of the same type was added to the same parent
     *
     * See [/datum/component/var/dupe_mode]
     *
     * `C`'s type will always be the same of the called component
     */
    // tg: code/datums/components/_component.dm:199
    public virtual async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        return;
    }

    /**
     * Called on a component when a component of the same type was added to the same parent with [COMPONENT_DUPE_SELECTIVE]
     *
     * See [/datum/component/var/dupe_mode]
     *
     * `C`'s type will always be the same of the called component
     *
     * return TRUE if you are absorbing the component, otherwise FALSE if you are fine having it exist as a duplicate component
     */
    // tg: code/datums/components/_component.dm:212
    public virtual async DmTask<object> CheckDupeComponent(datum_component C = null, params object[] __extra)
    {
        return null;
    }

    /**
     * Callback Just before this component is transferred
     *
     * Use this to do any special cleanup you might need to do before being deregged from an object
     */
    // tg: code/datums/components/_component.dm:221
    public virtual async DmTask PreTransfer(datum new_parent = null)
    {
        return;
    }

    /**
     * Callback Just after a component is transferred
     *
     * Use this to do any special setup you need to do after being moved to a new object
     *
     * Do not call `qdel(src)` from this function, `return COMPONENT_INCOMPATIBLE` instead
     */
    // tg: code/datums/components/_component.dm:231
    public virtual async DmTask<object> PostTransfer(datum new_parent = null)
    {
        return COMPONENT_INCOMPATIBLE; //Do not support transfer by default as you must properly support it
    }

    /**
     * Internal proc to create a list of our type and all parent types
     */
    // tg: code/datums/components/_component.dm:237
    public virtual object _GetInverseTypeList(object our_type = null)
    {
        our_type ??= type;
        DmList dot = null;
        //we can do this one simple trick
        dot = Dm.list(our_type);
        Type current_type = Dm.parent_type(GetType());
        //and since most components are root level + 1, this won't even have to run
        while (current_type != typeof(datum_component))
        {
            Dm.append(ref dot, current_type);
            current_type = Dm.parent_type(current_type);
        }
        return dot;
    }
}

public partial class datum
{
    // The type arg is casted so initial works, you shouldn't be passing a real instance into this
    /**
     * Return any component assigned to this datum of the given type
     *
     * This will throw an error if it's possible to have more than one component of that type on the parent
     *
     * Arguments:
     * * datum/component/c_type The typepath of the component you want to get a reference to
     */
    // tg: code/datums/components/_component.dm:255
    // Per-slot decision (rules 3.3 P1, 7.1; reported to the lead for Appendix A): slots.tsv lists c_type as undecided
    // (declared datum/component, read by initial()); it is Type: tg passes a typepath at every call site and says so above.
    public virtual object GetComponent(Type c_type = null)
    {
        object dot = null;
        if (Dm.initial(c_type, (datum_component p) => p.dupe_mode) == COMPONENT_DUPE_ALLOWED || Dm.initial(c_type, (datum_component p) => p.dupe_mode) == COMPONENT_DUPE_SELECTIVE)
            stack_trace(Dm.text("GetComponent was called to get a component of which multiple copies could be on an object. This can easily break and should be changed. Type: \\[[]\\]", c_type), "code/datums/components/_component.dm", 258);
        DmList dc = _datum_components;
        if (!Dm.truthy(dc))
            return null;
        dot = dc[c_type];
        if (Dm.truthy(length(dot)))
            return ((DmList)dot)[1];
        return dot;
    }

    // The type arg is casted so initial works, you shouldn't be passing a real instance into this
    /**
     * Return any component assigned to this datum of the exact given type
     *
     * This will throw an error if it's possible to have more than one component of that type on the parent
     *
     * Arguments:
     * * datum/component/c_type The typepath of the component you want to get a reference to
     */
    // tg: code/datums/components/_component.dm:275
    // Per-slot decision (rules 3.3 P1, 7.1; reported to the lead for Appendix A): c_type is Type, as for GetComponent.
    public virtual object GetExactComponent(Type c_type = null)
    {
        float initial_type_mode = Dm.initial(c_type, (datum_component p) => p.dupe_mode);
        if (initial_type_mode == COMPONENT_DUPE_ALLOWED || initial_type_mode == COMPONENT_DUPE_SELECTIVE)
            stack_trace(Dm.text("GetComponent was called to get a component of which multiple copies could be on an object. This can easily break and should be changed. Type: \\[[]\\]", c_type), "code/datums/components/_component.dm", 279);
        DmList all_components = _datum_components;
        if (!Dm.truthy(all_components))
            return null;
        datum_component potential_component = null;
        if (Dm.truthy(length(all_components)))
            potential_component = all_components[c_type] as datum_component;
        if (potential_component?.type == c_type)
            return potential_component;
        return null;
    }

    /**
     * Get all components of a given type that are attached to this datum
     *
     * Arguments:
     * * c_type The component type path
     */
    // tg: code/datums/components/_component.dm:296
    public virtual object GetComponents(object c_type = null)
    {
        // var/list/components holds one component or a list of them (islist below): object (rules 3.4).
        object components = _datum_components?[c_type];
        if (!Dm.truthy(components))
            return Dm.list();
        return (islist(components) ? components : (object)Dm.list(components));
    }

    /**
     * Creates an instance of `new_type` in the datum and attaches to it as parent
     *
     * Sends the [COMSIG_COMPONENT_ADDED] signal to the datum
     *
     * Returns the component that was created. Or the old component in a dupe situation where [COMPONENT_DUPE_UNIQUE] was set
     *
     * If this tries to add a component to an incompatible type, the component will be deleted and the result will be `null`. This is very unperformant, try not to do it
     *
     * Properly handles duplicate situations based on the `dupe_mode` var
     */
    // tg: code/datums/components/_component.dm:313
    public virtual async DmTask<object> _AddComponent(DmList raw_args = null, object source = null)
    {
        object original_type = raw_args[1];
        // var/datum/component/component_type holds a typepath or a component (ispath and istype below): object (rules 3.4).
        object component_type = original_type;

        if (QDELING(this))
            throw Dm.CRASH(Dm.text("Attempted to add a new component of type \\[[]\\] to a qdeleting parent of type \\[[]\\]!", component_type, type), "code/datums/components/_component.dm", 318);

        datum_component new_component = null;

        if (!ispath(component_type, typeof(datum_component)))
        {
            if (!(component_type is datum_component))
                throw Dm.CRASH(Dm.text("Attempted to instantiate \\[[]\\] as a component added to parent of type \\[[]\\]!", component_type, type), "code/datums/components/_component.dm", 324);
            else
            {
                new_component = component_type as datum_component;
                component_type = new_component.type;
            }
        }
        else if (Dm.eq(component_type, typeof(datum_component)))
            throw Dm.CRASH(Dm.text("[] attempted instantiation!", component_type), "code/datums/components/_component.dm", 329);

        float dupe_mode = Dm.initial(component_type, (datum_component p) => p.dupe_mode);
        float uses_sources = Dm.num(dupe_mode == COMPONENT_DUPE_SOURCES);
        if (Dm.truthy(uses_sources) && !Dm.truthy(source))
            throw Dm.CRASH(Dm.text("Attempted to add a sourced component of type '[]' to '[]' without a source!", component_type, type), "code/datums/components/_component.dm", 334);
        else if (!Dm.truthy(uses_sources) && Dm.truthy(source))
            throw Dm.CRASH(Dm.text("Attempted to add a normal component of type '[]' to '[]' with a source!", component_type, type), "code/datums/components/_component.dm", 336);

        datum_component old_component = null;

        raw_args[1] = this;
        if (dupe_mode != COMPONENT_DUPE_ALLOWED && dupe_mode != COMPONENT_DUPE_SELECTIVE)
        {
            old_component = GetComponent(component_type as Type) as datum_component;

            if (Dm.truthy(old_component))
            {
                switch (dupe_mode)
                {
                    case COMPONENT_DUPE_UNIQUE:
                        if (!Dm.truthy(new_component))
                            new_component = await Dm.new_async(component_type as Type, raw_args) as datum_component;
                        if (!QDELETED(new_component))
                        {
                            await old_component.InheritComponent(new_component, TRUE);
                            QDEL_NULL(ref new_component);
                        }
                        break;

                    case COMPONENT_DUPE_HIGHLANDER:
                        if (!Dm.truthy(new_component))
                            new_component = await Dm.new_async(component_type as Type, raw_args) as datum_component;
                        if (!QDELETED(new_component))
                        {
                            await new_component.InheritComponent(old_component, FALSE);
                            QDEL_NULL(ref old_component);
                        }
                        break;

                    case COMPONENT_DUPE_UNIQUE_PASSARGS:
                        if (!Dm.truthy(new_component))
                        {
                            DmList arguments = raw_args.Copy(2);
                            arguments.Insert(1, null, TRUE);
                            await Dm.Call(old_component, nameof(datum_component.InheritComponent), Dm.arglist(arguments));
                        }
                        else
                        {
                            await old_component.InheritComponent(new_component, TRUE);
                            QDEL_NULL(ref new_component);
                        }
                        break;

                    case COMPONENT_DUPE_SOURCES:
                        if (Dm.@in(source, old_component.sources) && !Dm.truthy(old_component.allow_source_update(source)))
                            return old_component; // source already registered, no work to do

                        object source_result = await Dm.Call(old_component, nameof(datum_component.on_source_add), Dm.arglist(Dm.add(Dm.list(source), raw_args.Copy(2))));
                        if (Dm.eq(source_result, COMPONENT_INCOMPATIBLE))
                        {
                            stack_trace(Dm.text("incompatible source added to a []. Args: []", old_component.type, json_encode(raw_args)), "code/datums/components/_component.dm", 375);
                            return null;
                        }
                        if (Dm.eq(source_result, COMPONENT_REDUNDANT))
                            return null;
                        break;
                }
            }

            else if (!Dm.truthy(new_component))
                new_component = await Dm.new_async(component_type as Type, raw_args) as datum_component; // There's a valid dupe mode but there's no old component, act like normal
        }

        else if (dupe_mode == COMPONENT_DUPE_SELECTIVE)
        {
            DmList arguments = raw_args.Copy();
            arguments[1] = new_component;
            float make_new_component = TRUE;
            foreach (datum_component existing_component in Dm.each_cast<datum_component>(GetComponents(original_type)))
            {
                if (Dm.truthy(await Dm.Call(existing_component, nameof(datum_component.CheckDupeComponent), Dm.arglist(arguments))))
                {
                    make_new_component = FALSE;
                    QDEL_NULL(ref new_component);
                    break;
                }
            }
            if (!Dm.truthy(new_component) && Dm.truthy(make_new_component))
                new_component = await Dm.new_async(component_type as Type, raw_args) as datum_component;
        }

        else if (!Dm.truthy(new_component))
            new_component = await Dm.new_async(component_type as Type, raw_args) as datum_component; // Dupes are allowed, act like normal

        if (!Dm.truthy(old_component) && !QDELETED(new_component)) // Nothing related to duplicate components happened and the new component is healthy
        {
            if (Dm.truthy(source) && Dm.eq(await Dm.Call(new_component, nameof(datum_component.on_source_add), Dm.arglist(Dm.add(Dm.list(source), raw_args.Copy(2)))), COMPONENT_INCOMPATIBLE))
            {
                stack_trace(Dm.text("incompatible source added to a []. Args: []", new_component.type, json_encode(raw_args)), "code/datums/components/_component.dm", 400);
                return null;
            }
            if (Dm.truthy(this._listen_lookup?[COMSIG_COMPONENT_ADDED]))
                this._SendSignal(COMSIG_COMPONENT_ADDED, Dm.list(this, new_component));
            return new_component;
        }

        return old_component;
    }

    /**
     * Removes a component source from this datum
     */
    // tg: code/datums/components/_component.dm:410
    public virtual void RemoveComponentSource(object source = null, object component_type = null)
    {
        if (ispath(component_type))
            component_type = GetExactComponent(component_type as Type);
        if (!Dm.truthy(component_type))
            return;
        ((datum_component)component_type).on_source_remove(source);
    }

    /**
     * Get existing component of type, or create it and return a reference to it
     *
     * Use this if the item needs to exist at the time of this call, but may not have been created before now
     *
     * Arguments:
     * * component_type The typepath of the component to create or return
     * * ... additional arguments to be passed when creating the component if it does not exist
     */
    // tg: code/datums/components/_component.dm:426
    public virtual async DmTask<object> _LoadComponent(DmList arguments = null)
    {
        object dot = null;
        dot = GetComponent(arguments[1] as Type);
        if (!Dm.truthy(dot))
            return await _AddComponent(arguments);
        return dot;
    }
}

public partial class datum_component : datum
{
    /**
     * Removes the component from parent, ends up with a null parent
     * Used as a helper proc by the component transfer proc, does not clean up the component like Destroy does
     */
    // tg: code/datums/components/_component.dm:435
    public virtual async DmTask ClearFromParent(datum new_parent = null)
    {
        if (!Dm.truthy(parent))
            return;
        datum old_parent = parent;
        await PreTransfer(new_parent);
        await _RemoveFromParent();
        parent = null;
        if (Dm.truthy(old_parent._listen_lookup?[COMSIG_COMPONENT_REMOVING]))
            old_parent._SendSignal(COMSIG_COMPONENT_REMOVING, Dm.list(old_parent, this));
    }
}

public partial class datum
{
    /**
     * Transfer this component to another parent
     *
     * Component is taken from source datum
     *
     * Arguments:
     * * datum/component/target Target datum to transfer to
     */
    // tg: code/datums/components/_component.dm:452
    public virtual async DmTask TakeComponent(datum_component target = null)
    {
        if (!Dm.truthy(target) || target.parent == this)
            return;
        if (Dm.truthy(target.parent))
            await target.ClearFromParent(this);
        object result = await target.PostTransfer(this);
        object sw__1 = result;
        if (Dm.eq(sw__1, COMPONENT_INCOMPATIBLE))
        {
            object c_type = target.type;
            qdel(target);
            throw Dm.CRASH(Dm.text("Incompatible [] transfer attempt to a []!", c_type, type), "code/datums/components/_component.dm", 462);
        }
        else if (Dm.eq(sw__1, COMPONENT_NOTRANSFER))
        {
            qdel(target);
            return;
        }

        await AddComponent(target);
        if (!QDELETED(target))
        {
            target.parent = this;
            await target._JoinParent();
        }
    }

    /**
     * Transfer all components to target
     *
     * All components from source datum are taken
     *
     * Arguments:
     * * /datum/target the target to move the components to
     */
    // tg: code/datums/components/_component.dm:480
    public virtual async DmTask TransferComponents(datum target = null)
    {
        DmList dc = _datum_components;
        if (!Dm.truthy(dc))
            return;
        foreach (object component_key in Dm.each(dc))
        {
            object component_or_list = dc[component_key];
            if (islist(component_or_list))
            {
                foreach (datum_component component in Dm.each<datum_component>(component_or_list))
                {
                    if (Dm.truthy(component.can_transfer))
                        await target.TakeComponent(component);
                }
            }
            else
            {
                datum_component component = component_or_list as datum_component;
                if (!QDELETED(component) && Dm.truthy(component.can_transfer))
                    await target.TakeComponent(component);
            }
        }
    }
}

public partial class datum_component : datum
{
    /**
     * Return the object that is the host of any UI's that this component has
     */
    // tg: code/datums/components/_component.dm:498
    [DmParams()]
    public override object ui_host(mob user = null)
    {
        return parent;
    }

    ///Whether the component is allowed to call on_source_add() on a source that's already present
    // tg: code/datums/components/_component.dm:502
    public virtual object allow_source_update(object source = null)
    {
        return FALSE;
    }
}
