namespace Anomaly.Core.DmShim;

// Owner: contracts. output_to, animate, flick, sound and (W4.0) the minimal image are complete; icon waits for the
// decision on where the /icon class lives (docs/ai/specs/shim-ownership.md, open item).
public static partial class Dm
{
    /// <summary>
    /// DM <c>target &lt;&lt; value</c> (tg <c>DIRECT_OUTPUT</c>, <c>SEND_SOUND</c>, <c>WRITE_FILE</c>), rules section 12:
    /// <c>world.log</c> goes to the host log (<see cref="DmWorldState.WorldLog"/>), a <see cref="sound"/> to
    /// <see cref="ISoundSink.Play"/>, anything else to <see cref="IMessageSink.Output"/> as text (rendered like
    /// <c>"[value]"</c>). A null target or value sends nothing; an unregistered object target is skipped
    /// (presentation-seam.md section 1). A <see cref="DmFileRef"/> target (<c>file(path) &lt;&lt; text</c>, tg's
    /// <c>WRITE_FILE</c>) appends the text and a line break to that file of the world's file seam. Named
    /// <c>output_to</c> because BYOND's <c>output(msg, control)</c> builtin keeps the name <c>output</c> (the UI seam,
    /// rules Appendix C); no tg <c>#define</c> or proc is named <c>output_to</c>.
    /// </summary>
    public static void output_to(object target, object value)
    {
        if (target == null) return;
        if (target is DmWorldLog)
        {
            Action<string> log = DmWorldState.Current.WorldLog;
            log?.Invoke(value as string ?? (value == null ? "" : str(value)));
            return;
        }
        if (target is DmFileRef file)
        {
            // F << text (tg WRITE_FILE): the text and a line break appended to the file of the world's file seam.
            if (value == null) return;
            DmWorldState.Current.Files.AppendText(file.Path, (value as string ?? str(value)) + "\n");
            return;
        }
        if (value == null || !PresentationSinks.Reportable(target)) return;
        PresentationSinks sinks = DmWorldState.Current.Sinks;
        if (value is sound s)
        {
            sinks.Sounds.Play(target, s);
            return;
        }
        sinks.Messages.Output(target, value as string ?? str(value));
    }

    /// <summary>
    /// DM <c>animate(Object, var1=new_value1, ..., time, loop, easing, flags, delay, tag, command)</c> (BYOND ref
    /// <c>/proc/animate</c>): named arguments are <c>Dm.named(...)</c> items. Reports the call to
    /// <see cref="IAppearanceSink.AnimateStep"/> (whose default is <see cref="IAppearanceSink.Animate"/>) with the first positional argument as target and the other arguments
    /// as given (named ones as text keys; the positional ones after the object as BYOND's parameters time, loop, easing, flags and delay,
    /// one past delay, or whose parameter is already named, as an item: M4v slice 4). A call with only named arguments continues
    /// the previous animation (target null). An <c>arglist(L)</c> argument is expanded (text keys with associated
    /// values become named arguments).
    /// <para>
    /// The object's vars take the step's values at once (BYOND: only the client shows the transition; dm-conformance.md):
    /// every named var the object declares is assigned, added to its current value (a matrix multiplied) when the step's
    /// <c>flags</c> hold <c>ANIMATION_RELATIVE</c>. A step with no object applies to the object of the last <c>animate</c>
    /// call that named one, in this world (M2 integration: the SHIM-GAP of M2 P3).
    /// </para>
    /// </summary>
    public static void animate(params object[] args)
    {
        args ??= OneNull();
        object target = null;
        bool haveTarget = false;
        int positional = 0;
        var arguments = new DmList();
        foreach (object arg in args)
        {
            if (arg is DmArgList arglist)
            {
                if (arglist.List is not DmList expanded) throw new DmRuntimeException("arglist() needs a list, got " + KindOf(arglist.List));
                for (int i = 1; i <= expanded.Count; i++)
                {
                    object item = expanded._items[i - 1]; // by position, also in an alist (its indexer takes a number as a key)
                    if (item is string key && expanded.TryGetAssociated(item, out object value)) arguments[key] = value;
                    else AddAnimateArgument(arguments, item, ref target, ref haveTarget, ref positional);
                }
            }
            else
            {
                AddAnimateArgument(arguments, arg, ref target, ref haveTarget, ref positional);
            }
        }
        DmWorldState state = DmWorldState.Current;
        object subject = target;
        if (haveTarget)
            state.LastAnimateTarget = target == null ? null : new WeakReference<object>(target);
        else if (state.LastAnimateTarget == null || !state.LastAnimateTarget.TryGetTarget(out subject))
            subject = null;
        DmList before = null;
        if (subject != null)
        {
            before = AnimateBefore(subject, arguments);
            ApplyAnimateVars(subject, arguments);
        }
        if (!PresentationSinks.Reportable(target)) return;
        state.Sinks.Appearance.AnimateStep(target, subject, arguments, before);
    }

    // The values the step's named vars of the subject hold before it applies (BYOND animates from them): IAppearanceSink.AnimateStep.
    private static DmList AnimateBefore(object subject, DmList arguments)
    {
        var before = new DmList();
        foreach ((object key, object _) in pairs(arguments))
        {
            if (key is not string name || IsAnimateControl(name))
                continue;
            if (subject is dm_filter filtered) // M6 slice 7: a filter's vars are its parameters
            {
                before[name] = filtered.Get(name);
                continue;
            }
            DmVars.Accessor accessor = DmVars.Find(subject.GetType(), DmBinder.DmName(name));
            if (accessor.Get == null)
                continue;
            object value = accessor.Get(subject);
            before[name] = value is matrix m ? m.Copy() : value;
        }
        return before;
    }

    // The arguments of animate that are no var of the object (BYOND ref /proc/animate).
    private static bool IsAnimateControl(string name) => name is "time" or "loop" or "easing" or "flags" or "delay" or "tag" or "command";

    private static void ApplyAnimateVars(object subject, DmList arguments)
    {
        bool relative = arguments.TryGetAssociated("flags", out object flags) && TryGetNumber(flags, out float f) && ((int)f & (int)ANIMATION_RELATIVE) != 0;
        foreach ((object key, object value) in pairs(arguments))
        {
            if (key is not string name || IsAnimateControl(name))
                continue;
            if (subject is dm_filter filtered) // M6 slice 7: a filter's parameter takes the step's value (relative: added)
            {
                object now = filtered.Get(name);
                filtered.Set(name, relative && TryGetNumber(now, out _) && TryGetNumber(value, out _) ? add(now, value) : value);
                continue;
            }
            DmVars.Accessor accessor = DmVars.Find(subject.GetType(), DmBinder.DmName(name));
            if (accessor.Set == null)
                continue; // not a var of this object: nothing to assign
            object final = value;
            if (relative)
            {
                object current = accessor.Get(subject);
                if (current is matrix m)
                    final = mul(m, value);
                else if (TryGetNumber(current, out _) && TryGetNumber(value, out _))
                    final = add(current, value);
            }
            accessor.Set(subject, final);
        }
    }

    // BYOND's positional parameters of animate() after Object (OpenDream's DMStandard: animate(Object, time, loop, easing, flags,
    // delay, pixel_x, ...)): M4v slice 4, tg's animate(src, 0.5 SECONDS, transform = ...) of a broken canister.
    // A positional argument whose parameter is already named (or past delay) stays an item, as given.
    private static string AnimatePositional(int position) => position switch
    {
        0 => "time",
        1 => "loop",
        2 => "easing",
        3 => "flags",
        4 => "delay",
        _ => null,
    };

    private static void AddAnimateArgument(DmList arguments, object arg, ref object target, ref bool haveTarget, ref int positional)
    {
        if (arg is DmNamedArg named)
        {
            arguments[named.Name] = named.Value;
        }
        else if (!haveTarget)
        {
            target = arg;
            haveTarget = true;
        }
        else if (AnimatePositional(positional++) is string name && !@in(name, arguments))
        {
            arguments[name] = arg;
        }
        else
        {
            arguments.AppendItem(arg);
        }
    }

    /// <summary>
    /// DM <c>flick(Icon, Object)</c> (BYOND ref <c>/proc/flick</c>): plays an icon state or icon once on
    /// <paramref name="Object"/>. Reported to <see cref="IAppearanceSink.Flick"/>; nothing for a null or
    /// unregistered object.
    /// </summary>
    public static void flick(object Icon, object Object)
    {
        if (Object == null || !PresentationSinks.Reportable(Object)) return;
        DmWorldState.Current.Sinks.Appearance.Flick(Object, Icon);
    }

    /// <summary>
    /// DM <c>sound(file, repeat=0, wait, channel, volume)</c> (BYOND ref <c>/proc/sound</c>, supports named
    /// arguments): a new <see cref="sound"/> datum. A null argument keeps the var's default (repeat 0, wait 0,
    /// channel 0, volume 100).
    /// </summary>
    public static sound sound(object file = null, float? repeat = null, float? wait = null, float? channel = null, float? volume = null)
        => new sound
        {
            file = file,
            repeat = repeat ?? 0f,
            wait = wait ?? 0f,
            channel = channel ?? 0f,
            volume = volume ?? 100f,
        };

    /// <summary>
    /// DM <c>image(icon, loc, icon_state, layer, dir, pixel_x, pixel_y)</c> (BYOND ref <c>/proc/image</c>, supports named
    /// arguments; OpenDream adds the two pixel offsets): a new <see cref="DmShim.image"/>. The first argument gives the
    /// appearance: null keeps the image type's default, an atom or another image gives its appearance (icon,
    /// icon_state, layer, plane, dir, pixel offsets, appearance_flags, alpha, color, invisibility), text is the icon. The
    /// second is the location when it is null or an atom; text or a number there is the icon_state, and the later
    /// arguments shift down one place (BYOND: "A text string will be interpreted as the icon_state argument if one is not
    /// present"; OpenDream <c>DreamObjectImage.Initialize</c>). Then a non-null icon_state, layer, dir, pixel_x and
    /// pixel_y override the appearance. Anything else (a number or a list as the icon, a datum that is no atom as the
    /// location, a non-text icon_state) is a runtime error: tg's M0 callers never pass one (wave 4, decision D3).
    /// </summary>
    // RULE-GAP: a resource literal ('icons/x.dmi') is text in the port until resources are placed, so text in the icon
    // position is the icon; BYOND's reading of real text there as an icon_state is not supported (tg passes resources).
    public static image image(object icon = null, object loc = null, object icon_state = null, object layer = null, object dir = null, object pixel_x = null, object pixel_y = null)
    {
        var made = new image();
        // P1 (surgery): a typepath is the image of the type's initial appearance (BYOND ref image: the icon "can be an icon, an
        // object prototype, an object instance, or another image"; tg's operations make their radial pictures from item types)
        if (icon is Type prototypePath && DmTypes.PathOf(prototypePath) != null && DmPrototypes.Prototype(prototypePath) is IDmAtom prototypeAtom)
            icon = prototypeAtom;
        switch (icon)
        {
            case null:
                break;
            case DmResource resource:
                made.icon = resource;
                break;
            case string resource:
                made.icon = resource;
                break;
            case DmShim.icon dynamic:
                made.icon = dynamic; // M4c slice 4: an icon object is the image's icon (its file and state, icon.cs)
                break;
            case image other:
                CopyAppearance(made, other.icon, other.icon_state, other.layer, other.plane, other.dir, other.pixel_x,
                    other.pixel_y, other.appearance_flags, other.alpha, other.color, other.invisibility);
                made.pixel_w = other.pixel_w;
                made.pixel_z = other.pixel_z;
                made.transform = ((IDmAppearance)other).transform;
                made.blend_mode = other.blend_mode; // M7 husks: a masked leg's husk blood stays inset (carbon_update_icons.dm:538)
                made.overlays = other.overlays?.Copy() ?? new DmList(); // M8 slice G: the copy keeps the overlays and underlays (BYOND copies the appearance)
                made.underlays = other.underlays?.Copy() ?? new DmList();
                break;
            case IDmAtom atom:
                CopyAppearance(made, get_var(atom, "icon"), (string)get_var(atom, "icon_state"), num(get_var(atom, "layer")),
                    num(get_var(atom, "plane")), num(get_var(atom, "dir")), num(get_var(atom, "pixel_x")),
                    num(get_var(atom, "pixel_y")), num(get_var(atom, "appearance_flags")), num(get_var(atom, "alpha")),
                    get_var(atom, "color"), num(get_var(atom, "invisibility")));
                // M4v slice 3: the rest of the template's look (OpenDream's DreamObjectImage copies the whole appearance):
                // tg's pickup image of an item (image(icon = src), items.dm:1514) keeps its offsets and transform.
                if (atom is IDmAppearance look)
                {
                    made.pixel_w = look.pixel_w;
                    made.pixel_z = look.pixel_z;
                    made.transform = look.transform;
                    made.blend_mode = look.blend_mode;
                    // silicons SI4: and its overlays and underlays (BYOND copies the whole appearance; tg's holorecord caller image,
                    // holocall.dm:211, is the caller as seen, clothes included)
                    made.overlays = look.overlays?.Copy() ?? new DmList();
                    made.underlays = look.underlays?.Copy() ?? new DmList();
                }
                break;
            case DmAppearance snapshot:
                // M8 slice G: an appearance snapshot (BYOND's /image of a mutable appearance, tg's get_dynamic_human_appearance): the whole look
                // with its overlays and underlays
                CopyAppearance(made, snapshot.icon, snapshot.icon_state, snapshot.layer, snapshot.plane, snapshot.dir, snapshot.pixel_x, snapshot.pixel_y,
                    snapshot.appearance_flags, snapshot.alpha, snapshot.color, snapshot.invisibility);
                made.pixel_w = snapshot.pixel_w;
                made.pixel_z = snapshot.pixel_z;
                made.transform = snapshot.transform;
                made.blend_mode = snapshot.blend_mode;
                made.overlays = snapshot.overlays?.Copy() ?? new DmList();
                made.underlays = snapshot.underlays?.Copy() ?? new DmList();
                break;
            default:
                throw new DmRuntimeException("image(): cannot make an image from " + KindOf(icon));
        }
        if (loc == null || loc is IDmAtom)
        {
            made.loc = loc;
        }
        else if (loc is string || TryGetNumber(loc, out _))
        {
            // The location is left out: the arguments shift down one place.
            pixel_y = pixel_x;
            pixel_x = dir;
            dir = layer;
            layer = icon_state;
            icon_state = loc;
        }
        else
        {
            throw new DmRuntimeException("image(): the location must be an atom, not " + KindOf(loc));
        }
        if (icon_state != null)
            made.icon_state = icon_state as string ?? throw new DmRuntimeException("image(): the icon_state must be text, not " + KindOf(icon_state));
        if (layer != null)
            made.layer = num(layer);
        if (dir != null)
            made.dir = num(dir);
        if (pixel_x != null)
            made.pixel_x = num(pixel_x);
        if (pixel_y != null)
            made.pixel_y = num(pixel_y);
        return made;
    }

    // The appearance vars an image takes from an atom or another image.
    private static void CopyAppearance(image made, object icon, string icon_state, float layer, float plane, float dir,
        float pixel_x, float pixel_y, float appearance_flags, float alpha, object color, float invisibility)
    {
        made.icon = icon;
        made.icon_state = icon_state;
        made.layer = layer;
        made.plane = plane;
        made.dir = dir;
        made.pixel_x = pixel_x;
        made.pixel_y = pixel_y;
        made.appearance_flags = appearance_flags;
        made.alpha = alpha;
        made.color = color;
        made.invisibility = invisibility;
    }

    // DM icon(icon, state, dir, frame, moving): DmShim/icon.cs (M4c slice 4).
}
