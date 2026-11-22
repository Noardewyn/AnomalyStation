// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/_wires.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the wires a machine holds (M3 P3: the airlock's requiresID, power and bolts read them): New, Destroy, add_duds,
// on_holder_qdel, is_wire_tool and the empty on_pulse (M5x slice 4), the cut state (is_cut, is_all_cut, cut, cut_all (M5 slice 6: a broken airlock), on_cut) and the colour lookups. The colours themselves are the
// hacking interface assigns colors per round and exposes cut/mend/pulse through tg's tgui actions. Pulsing and the EMP pulse
// are translated; attaching assemblies too (gaps F1: the chem grenade's trigger). Studying blueprint photos remains deferred.
// The var block is generated (D111).

// tg: code/datums/wires/_wires.dm:32
[DmPath("/datum/wires")]
public partial class datum_wires : datum
{
    // tg: code/datums/wires/_wires.dm:1  #define MAXIMUM_EMP_WIRES 3 (file-local, #undef at :420)
    private const float MAXIMUM_EMP_WIRES = 3;

    // tg: code/datums/wires/_wires.dm:81
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        await base.New(Dm.forward(__extra, holder));
        if (!istype(holder, holder_type))
            throw Dm.CRASH("Wire holder is not of the expected type!", "code/datums/wires/_wires.dm", 84);

        this.holder = holder;

        // If there is a dictionary key set, we'll want to use that. Otherwise, use the holder type.
        Type key = (Dm.truthy(dictionary_key) ? dictionary_key : holder_type);

        RegisterSignal(holder, COMSIG_QDELETING, nameof(on_holder_qdel));
        // if(randomize) randomize() else the shared colours of the key in GLOB.wire_color_directory (:92-100)
        AssignColors(key);
    }

    // tg: code/datums/wires/_wires.dm:102
    public override object Destroy(float? force = null)
    {
        holder = null;
        //properly clear refs to avoid harddels & other problems
        foreach (object color in Dm.each(assemblies))
        {
            obj_item_assembly assembly = LAZYACCESS(assemblies, color) as obj_item_assembly;
            assembly.holder = null;
            assembly.connected = null;
        }
        LAZYNULL(ref assemblies);
        return base.Destroy(force);
    }

    /// Adds a number of wires which do absolutely nothing.
    // tg: code/datums/wires/_wires.dm:113
    public virtual void add_duds(object duds = null)
    {
        while (Dm.truthy(duds))
        {
            string dud = WIRE_DUD_PREFIX + Dm.text("[]", Dm.predec(ref duds));
            if (Dm.@in(dud, wires))
                continue;
            LAZYADD(ref wires, dud);
        }
    }

    ///Called when holder is qdeleted for us to clean ourselves as not to leave any unlawful references.
    // tg: code/datums/wires/_wires.dm:121
    [SignalHandler]
    public virtual void on_holder_qdel(atom source = null, object force = null)
    {
        qdel(this);
    }

    // tg: code/datums/wires/_wires.dm:145
    public virtual object get_wire(object color = null)
    {
        return LAZYACCESS(colors, color);
    }

    // tg: code/datums/wires/_wires.dm:140
    public virtual async DmTask repair()
    {
        foreach (object wire in Dm.each(cut_wires?.Copy()))
            await cut(wire);
    }

    // tg: code/datums/wires/_wires.dm:148
    public virtual object get_color_of_wire(object wire_type = null)
    {
        foreach (object color in Dm.each(colors))
        {
            object other_type = colors[color];
            if (Dm.eq(wire_type, other_type))
                return color;
        }
        return null;
    }

    // tg: code/datums/wires/_wires.dm:154
    public virtual object get_attached(object color = null)
    {
        if (Dm.truthy(LAZYACCESS(assemblies, color)))
            return LAZYACCESS(assemblies, color);
        return null;
    }

    // tg: code/datums/wires/_wires.dm:159
    public virtual object is_attached(object color = null)
    {
        if (Dm.truthy(LAZYACCESS(assemblies, color)))
            return TRUE;
        return null;
    }

    // tg: code/datums/wires/_wires.dm:163
    public virtual object is_cut(object wire = null)
    {
        return Dm.num(Dm.@in(wire, cut_wires));
    }

    // tg: code/datums/wires/_wires.dm:166
    public virtual object is_color_cut(object color = null)
    {
        return is_cut(get_wire(color));
    }

    // tg: code/datums/wires/_wires.dm:169
    public virtual object is_all_cut()
    {
        if (LAZYLEN(cut_wires) == LAZYLEN(wires))
            return TRUE;
        return null;
    }

    // tg: code/datums/wires/_wires.dm:173
    public virtual object is_dud(object wire = null)
    {
        return findtext(wire, WIRE_DUD_PREFIX, 1, length(WIRE_DUD_PREFIX) + 1);
    }

    // tg: code/datums/wires/_wires.dm:179
    public virtual async DmTask cut(object wire = null, mob_living source = null)
    {
        if (Dm.truthy(is_cut(wire)))
        {
            LAZYREMOVE(ref cut_wires, wire);
            // SEND_SIGNAL(src, COMSIG_MEND_WIRE(wire), wire) (__DEFINES/dcs/helpers.dm:7), inline (rules 11.1 (e))
            if (Dm.truthy(this._listen_lookup?[COMSIG_MEND_WIRE(wire)]))
                this._SendSignal(COMSIG_MEND_WIRE(wire), Dm.list(this, wire));
            await on_cut(wire, mend: TRUE, source: source);
        }
        else
        {
            LAZYADD(ref cut_wires, wire);
            // SEND_SIGNAL(src, COMSIG_CUT_WIRE(wire), wire) (__DEFINES/dcs/helpers.dm:7), inline (rules 11.1 (e))
            if (Dm.truthy(this._listen_lookup?[COMSIG_CUT_WIRE(wire)]))
                this._SendSignal(COMSIG_CUT_WIRE(wire), Dm.list(this, wire));
            await on_cut(wire, mend: FALSE, source: source);
        }
    }

    // tg: code/datums/wires/_wires.dm:195
    public virtual async DmTask cut_all(mob_living source = null)
    {
        foreach (object wire in Dm.each(wires))
            await cut(wire, source);
    }

    // tg: code/datums/wires/_wires.dm:199
    public virtual async DmTask pulse(object wire = null, mob_living user = null, float? force = null)
    {
        force ??= FALSE;
        if (!Dm.truthy(force) && Dm.truthy(is_cut(wire)))
            return;
        if (Dm.truthy(this._listen_lookup?[COMSIG_PULSE_WIRE]))
            this._SendSignal(COMSIG_PULSE_WIRE, Dm.list(this, wire, user));
        await on_pulse(wire, user);
    }

    // tg: code/datums/wires/_wires.dm:205  (gaps F1: the chem grenade's trigger assembly)
    public virtual async DmTask pulse_color(object color = null, mob_living user = null, float? force = null)
    {
        force ??= FALSE;
        await pulse(get_wire(color), user, force);
    }

    // tg: code/datums/wires/_wires.dm:208  (gaps F1)
    public virtual async DmTask<object> pulse_assembly(obj_item_assembly assembly = null)
    {
        foreach (object color in Dm.each(assemblies))
        {
            if (assembly == assemblies[color])
            {
                await pulse_color(color, force: TRUE);
                return TRUE;
            }
        }
        return null;
    }

    // tg: code/datums/wires/_wires.dm:214  (gaps F1)
    public virtual async DmTask<object> attach_assembly(object color = null, obj_item_assembly assembly = null)
    {
        if (assembly != null && istype(assembly, typeof(obj_item_assembly)) && Dm.truthy(assembly.assembly_behavior) && !Dm.truthy(is_attached(color))
            && !Dm.truthy(Dm.bitand(assembly._listen_lookup?[COMSIG_ASSEMBLY_PRE_ATTACH] != null ? assembly._SendSignal(COMSIG_ASSEMBLY_PRE_ATTACH, Dm.list(assembly, holder)) : null, COMPONENT_CANCEL_ATTACH)))
        {
            LAZYSET(ref assemblies, color, assembly);
            await assembly.forceMove(holder);
            assembly.connected = this;
            await assembly.on_attach(); // Notify assembly that it is attached
            return assembly;
        }
        return null;
    }

    // tg: code/datums/wires/_wires.dm:222  (gaps F1)
    public virtual async DmTask<object> detach_assembly(object color = null)
    {
        obj_item_assembly assembly = get_attached(color) as obj_item_assembly;
        if (assembly != null && istype(assembly, typeof(obj_item_assembly)))
        {
            LAZYREMOVE(ref assemblies, color);
            await assembly.on_detach(); // Notify the assembly.  This should remove the reference to our holder
            return assembly;
        }
        return null;
    }

    /// Called from [/atom/proc/emp_act]
    // tg: code/datums/wires/_wires.dm:230
    public virtual async DmTask emp_pulse()
    {
        DmList possible_wires = shuffle(wires) as DmList;
        float remaining_pulses = MAXIMUM_EMP_WIRES;

        foreach (object wire in Dm.each(possible_wires))
        {
            if (prob(33))
            {
                await pulse(wire);
                remaining_pulses--;
                if (!Dm.truthy(remaining_pulses))
                    break;
            }
        }
    }

    // tg: code/datums/wires/_wires.dm:251
    public virtual async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        mend ??= FALSE;
        return;
    }

    // tg: code/datums/wires/_wires.dm:254  (M5x slice 4: the root of the explosive wires')
    public virtual async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        return;
    }
}

// tg: code/datums/wires/_wires.dm:13 (M5x slice 4: C4's wire tools)
public static partial class GlobalProcs
{
    // tg: code/datums/wires/_wires.dm:13
    public static object is_wire_tool(obj_item tool = null, float? check_secured = null)
    {
        check_secured ??= FALSE;
        if (!istype(tool, typeof(obj_item)))
            return FALSE;
        if (Dm.eq(tool.tool_behaviour, TOOL_WIRECUTTER) || Dm.eq(tool.tool_behaviour, TOOL_MULTITOOL))
            return TRUE;
        if (istype(tool, typeof(obj_item_assembly))) // isassembly
        {
            obj_item_assembly assembly = (obj_item_assembly)tool;
            if (!Dm.truthy(check_secured) || !Dm.truthy(assembly.secured))
                return TRUE;
        }
        return FALSE;
    }
}

public partial class atom
{
    // tg: code/datums/wires/_wires.dm:26
    public virtual async DmTask<object> attempt_wire_interaction(mob user = null)
    {
        if (wires == null || user == null || !Dm.truthy(await IsReachableBy(user)))
            return WIRE_INTERACTION_FAIL;
        Waiting.Detach(wires.interact(user));
        return WIRE_INTERACTION_BLOCK;
    }
}

public partial class datum_wires
{
    // tg: code/datums/wires/_wires.dm:92-100  the shared assignments of the round in GLOB.wire_color_directory, the names in
    // GLOB.wire_name_directory (the station blueprints' legend reads both, blueprints.dm:75); each holder keeps a copy of the colours
    private void AssignColors(Type key)
    {
        if (!Dm.truthy(randomize) && GLOB.wire_color_directory[key] is DmList shared)
        {
            colors = shared.Copy();
            return;
        }
        DmList available = Dm.list("blue", "brown", "crimson", "cyan", "gold", "green", "grey", "lime", "magenta", "orange", "pink", "purple", "red", "silver", "violet", "white", "yellow");
        colors = Dm.list();
        foreach (object wire in Dm.each(shuffle(wires)))
        {
            if (available.Count == 0)
                break;
            object color = pick_n_take(available);
            colors[color] = wire;
        }
        if (!Dm.truthy(randomize))
        {
            GLOB.wire_color_directory[key] = colors.Copy();
            GLOB.wire_name_directory[key] = proper_name;
        }
    }

    // tg: code/datums/wires/_wires.dm:241
    public virtual bool interactable(mob user)
    {
        if (user == null || QDELETED(holder))
            return false;
        if (Dm.truthy(user._listen_lookup?[COMSIG_TRY_WIRES_INTERACT]) && Dm.truthy(Dm.bitand(user._SendSignal(COMSIG_TRY_WIRES_INTERACT, Dm.list(user, holder)), COMPONENT_CANT_INTERACT_WIRES)))
            return false;
        return true;
    }

    // tg: code/datums/wires/_wires.dm:259
    public virtual async DmTask<object> interact(mob user)
    {
        if (!interactable(user))
            return FALSE;
        await ui_interact(user);
        return TRUE;
    }

    // tg: code/datums/wires/_wires.dm:332
    public override object ui_host(mob user = null) => holder;
    public override object ui_state(mob user = null) => GLOB.physical_state;

    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
        => interactable(user) ? await base.ui_status(user, state) : UI_CLOSE;

    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "Wires", Dm.str(holder.name) + " Wires");
            await ui.open();
        }
        return null;
    }

    public virtual DmList get_status() => Dm.list();

    public virtual bool can_reveal_wires(mob user) => Dm.truthy(user?.is_holding_item_of_type(typeof(obj_item_multitool_abductor)))
        || !Dm.truthy(randomize) && Dm.truthy(user?.is_holding_item_of_type(typeof(obj_item_blueprints)));

    public virtual bool always_reveal_wire(object color) => false;

    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList payload = Dm.list();
        foreach (object color in Dm.each(colors))
            Dm.append(payload, Dm.list(Dm.list(Dm.kv("color", color), Dm.kv("shownColor", color),
                Dm.kv("wire", (can_reveal_wires(user) || always_reveal_wire(color)) && !Dm.truthy(is_dud(get_wire(color))) ? get_wire(color) : null),
                Dm.kv("cut", is_color_cut(color)), Dm.kv("attached", is_attached(color)))));
        return Dm.list(Dm.kv("wires", payload), Dm.kv("status", get_status()), Dm.kv("proper_name", proper_name));
    }

    // tg: code/datums/wires/_wires.dm:376
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        if (Dm.truthy(await base.ui_act(action, @params, ui, state)) || ui?.user is not mob_living user || !interactable(user))
            return FALSE;
        object color = @params?["wire"];
        if (Dm.eq(action, "attach")) // gaps F1: the attach branch (_wires.dm:401-418)
        {
            obj_item I;
            if (Dm.truthy(is_attached(color)))
            {
                I = await detach_assembly(color) as obj_item;
                if (I != null)
                {
                    await user.put_in_hands(I);
                    return TRUE;
                }
            }
            else
            {
                I = user.get_active_held_item() as obj_item;
                if (isassembly(I))
                {
                    obj_item_assembly A = I as obj_item_assembly;
                    if (Dm.truthy(Dm.bitand(A.assembly_behavior, wire_behavior)))
                    {
                        if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(A)))
                            return null;
                        if (!Dm.truthy(await attach_assembly(color, A)))
                            await A.forceMove(user.drop_location() as atom);
                        return TRUE;
                    }
                    to_chat(user, span_warning("You cannot attach this assembly to these wires!"));
                }
            }
            return null;
        }
        object wire = get_wire(color);
        if (wire == null || action is not ("cut" or "pulse"))
            return FALSE;
        object quality = Dm.eq(action, "cut") ? TOOL_WIRECUTTER : TOOL_MULTITOOL;
        if (user.is_holding_tool_quality(quality) is not obj_item tool)
        {
            to_chat(user, span_warning(Dm.eq(action, "cut") ? "You need wirecutters!" : "You need a multitool!"));
            return FALSE;
        }
        tool.play_tool_sound(holder, 20);
        if (Dm.eq(action, "cut"))
            await cut(wire, user);
        else
            await pulse(wire, user);
        return TRUE;
    }
}

// The syndicate campaign G2 (D316): randomize and shuffle_wires, which the syndicate bomb's wires and the training bomb's reset
// use to deal new colours (the port's New deals them through AssignColors).
public partial class datum_wires : datum
{
    // tg: code/datums/wires/_wires.dm:61  var/static/list/default_possible_colors (rules 10.6)
    private static ref DmList default_possible_colors => ref TgState.Current.datum_wires__default_possible_colors;

    // tg: code/datums/wires/_wires.dm:126
    public virtual void randomize__proc()
    {
        if (LAZYLEN(wires) > Dm.length(default_possible_colors))
            stack_trace(Dm.text("Wire type [] has more wires than possible colors, consider adding more colors or removing wires.", type), "code/datums/wires/_wires.dm", 128);

        DmList possible_colors = default_possible_colors.Copy();

        foreach (object wire in Dm.each(shuffle(wires)))
        {
            if (!Dm.truthy(Dm.length(possible_colors)))
                possible_colors = default_possible_colors.Copy();
            LAZYSET(ref colors, pick_n_take(possible_colors), wire);
        }
    }

    // tg: code/datums/wires/_wires.dm:137
    public virtual void shuffle_wires()
    {
        LAZYCLEARLIST(colors);
        randomize__proc();
    }
}

public sealed partial class TgState
{
    // tg: code/datums/wires/_wires.dm:61
    internal DmList datum_wires__default_possible_colors = Dm.list(
        Dm.kv("blue", "dim grey"),
        Dm.kv("brown", "dim grey"),
        Dm.kv("crimson", "grey"),
        Dm.kv("cyan", "light grey"),
        Dm.kv("gold", "grey"),
        Dm.kv("green", "grey"),
        Dm.kv("grey", "grey"),
        Dm.kv("lime", "light grey"),
        Dm.kv("magenta", "grey"),
        Dm.kv("orange", "grey"),
        Dm.kv("pink", "grey"),
        Dm.kv("purple", "grey"),
        Dm.kv("red", "grey"),
        Dm.kv("silver", "light grey"),
        Dm.kv("violet", "grey"),
        Dm.kv("white", "white"),
        Dm.kv("yellow", "light grey"));
}
