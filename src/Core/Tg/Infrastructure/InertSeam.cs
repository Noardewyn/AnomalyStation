using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tg;

// Owner: the M3 contracts step (complete; frozen: docs/ai/specs/m3-ownership.md). D112, docs/ai/specs/types-converter.md
// section 7. A host seam, not tg code. M4a: the interaction guard of D132 (MissingOverride, GuardedProcs; the M4a contracts
// step's API, frozen: docs/ai/specs/m4a-ownership.md; M4a P1 implemented the bodies over the generated UndeclaredOverrides
// table, docs/ai/specs/types-converter.md section 10, and calls the guard from the chain's entry points).

/// <summary>
/// The inert seam (D112): <c>SSatoms.InitAtom</c> asks it whether an atom is inert, and then calls the atom's
/// <c>dm_inert_initialize</c> (the presentation part of tg's <c>/atom/Initialize</c>, M3 P4's engine part
/// <c>Builtin/atom.inert.builtin.cs</c>) where tg calls <c>Initialize</c>. An inert atom is a real instance of its generated
/// class whose type, or an ancestor below its nearest live ancestor, has tg procs nobody declared (M3 P1's generator marks
/// the class <c>[DmInert]</c>).
/// </summary>
public static class InertSeam
{
    /// <summary>
    /// Whether <paramref name="A"/>'s runtime type is marked inert (its own class carries M3 P1's <c>[DmInert]</c>, which is
    /// not inherited; M3 P4 switched the seam on with <c>dm_inert_initialize</c>). Pure; no world access.
    /// </summary>
    public static bool IsInert(atom A)
    {
        ArgumentNullException.ThrowIfNull(A);
        return IsInertType(A.GetType());
    }

    /// <summary>Whether the class <paramref name="type"/> is marked inert (see <see cref="IsInert"/>).</summary>
    public static bool IsInertType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type.IsDefined(typeof(DmInertAttribute), inherit: false); // M3 P4: switched on with dm_inert_initialize
    }

    // ---- M4a: the interaction guard (D132; docs/ai/specs/interaction.md section 7) ----

    /// <summary>
    /// The DM names of the interaction chain's procs that <see cref="MissingOverride"/> guards (frozen by the M4a contracts
    /// step; the generated <c>UndeclaredOverrides</c> table covers exactly these names). Each is a proc whose tg override on
    /// an item, structure or machine type does that type's own thing, so running the nearest translated ancestor's C# instead
    /// would silently give another type's behaviour.
    /// </summary>
    public static readonly System.Collections.ObjectModel.ReadOnlyCollection<string> GuardedProcs = Array.AsReadOnly(new[]
    {
        // the hand and the click (other_mobs.dm, click_alt.dm, click_ctrl.dm)
        "attack_hand", "attack_hand_secondary", "click_alt", "click_alt_secondary", "click_ctrl", "click_ctrl_shift",
        // the item chain (item_attack.dm, atom_tool_acts.dm, items.dm)
        "attack_self", "attack_self_secondary", "melee_attack_chain", "pre_attack", "pre_attack_secondary",
        "attack", "attack_secondary", "afterattack", "attackby", "attackby_secondary",
        "interact_with_atom", "interact_with_atom_secondary", "ranged_interact_with_atom", "ranged_interact_with_atom_secondary",
        "item_interaction", "item_interaction_secondary", "tool_act",
        // picking up, equipping, dropping, throwing (items.dm, atoms_movable.dm, atom_act.dm)
        "pickup", "equipped", "dropped", "throw_impact", "hitby",
        // fire reaching an atom (atom_act.dm; M4b slice 3: a hotspot burns what is on its tile)
        "fire_act",
        // the action button's click on its item (items.dm; M4c slice 3: the item action buttons)
        "ui_action_click",
        // reagents reaching an atom (_atom.dm, atom_act.dm; M4b: the fire extinguisher's water puts out, washes and wets what
        // it reaches)
        "expose_reagents", "extinguish", "wash",
        // damage reaching an atom (atom_defense.dm, obj_defense.dm, item_attack.dm; M5 slice 1: integrity, DV27's extension): a
        // type's own take_damage, armour, attack sound, break, fix, destruction or deconstruction
        "attacked_by", "take_damage", "run_atom_armor", "play_attack_sound", "repair_damage", "atom_break", "atom_fix",
        "atom_destruction", "handle_deconstruct", "atom_deconstruct",
        // a machine's own deconstruction hook (_machinery.dm:1271; M5 slice 6: /obj/machinery/handle_deconstruct translated)
        "on_deconstruction",
        // drag and drop (drag_drop.dm; protocol version 17): a type's own handler of being dragged or of receiving a drop
        "mouse_drop_dragged", "mouse_drop_receive",
        // crafting (_atom.dm:360, atoms_movable.dm:1856; M5 slice 8): a crafted result's own hook and a used component's own hook
        "on_craft_completion", "used_in_craft",
        // explosions (atom_act.dm:40, :45; M5x slice 1): a type's own reaction to a blast and the blast reaching its contents,
        // guarded in EX_ACT (__DEFINES/explosions.dm)
        "ex_act", "contents_explosion",
        // a reagent dispenser bursting (reagent_dispenser.dm:166; M5x slice 3: the fuel tank's explosion): a dispenser's own
        // burst, the water cooler's tip-over
        "boom",
        // the singularity (atom_act.dm:170, :178; M5x slice 5): a type's own reaction to being eaten or pulled
        "singularity_act", "singularity_pull",
        // a tesla zap (atom_act.dm:216; the M7 zaps slice): a type's own reaction to a bolt (a vehicle's rider, a blob, a nuke)
        "zap_act",
        // a projectile's hit (atom_act.dm:95, :119; the M7 projectiles slice): a type's own reaction to a shot (a mirror, a reflector,
        // a target, a vehicle)
        "projectile_hit", "bullet_act",
    });

    // M5 slice 1 (DV27): the procs that damage reaching an atom runs (take_damage and what it calls: the armour, the attack
    // sound, the break and the destruction with obj's deconstruction chain) and those a repair runs.
    private static readonly System.Collections.Immutable.ImmutableArray<string> DamageProcs = System.Collections.Immutable.ImmutableArray.Create("take_damage", "run_atom_armor", "play_attack_sound", "atom_break", "atom_destruction", "handle_deconstruct", "atom_deconstruct", "on_deconstruction");
    private static readonly System.Collections.Immutable.ImmutableArray<string> RepairProcs = System.Collections.Immutable.ImmutableArray.Create("repair_damage", "atom_fix");
    private static readonly System.Collections.Immutable.ImmutableArray<string> DeconstructProcs = System.Collections.Immutable.ImmutableArray.Create("handle_deconstruct", "atom_deconstruct", "on_deconstruction");

    /// <summary>
    /// Whether tg defines the proc <paramref name="proc"/> (a DM name of <see cref="GuardedProcs"/>) at the runtime type of
    /// <paramref name="A"/>, or at an ancestor below the class whose C# method would run, without a C# declaration there:
    /// the type's own tg code for that proc is not translated (the generated <c>UndeclaredOverrides</c> table). Pure; no world
    /// access. A name outside <see cref="GuardedProcs"/> is false.
    /// </summary>
    public static bool HasUndeclaredOverride(datum A, string proc)
    {
        // M4a P1: types-converter.md 10's walk. From the runtime class up to (not including) the class that declares the C#
        // method of the slot (the binder's names: proc, proc__proc; a generated live-partial stub is a declaration), any class
        // with a row in the generated table answers true. Reflection over the declared methods (the runtime caches them); no
        // world access, no allocation in the common case of a class that declares the method.
        ArgumentNullException.ThrowIfNull(A);
        if (proc == null || !GuardedProcs.Contains(proc))
            return false;
        // M5x slice 1: the answer depends on the type and the proc only (the generated table and the declared methods), so it
        // is kept per pair: an explosion asks it for every atom it reaches (EX_ACT's two guards, obj/ex_act's damage guard).
        return UndeclaredCache.GetOrAdd((A.GetType(), proc), static key => HasUndeclaredOverride(key.Item1, key.Item2));
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Type, string), bool> UndeclaredCache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Type, string), bool> StubCache = new();

    private static bool HasUndeclaredOverride(Type type, string proc)
    {
        for (Type t = type; t != null && t != typeof(datum) && t != typeof(object); t = t.BaseType)
        {
            if (UndeclaredOverrides.Has(t, proc))
                return true;
            if (DeclaresSlot(t, proc))
                return false;
        }
        return false;
    }

    /// <summary>
    /// M8y slice D (D225): the class nearest to <paramref name="A"/>'s runtime type whose tg code defines <paramref name="proc"/> (a
    /// <see cref="GuardedProcs"/> name): one that declares its C# method or has tg's own code for it untranslated (the generated
    /// table); null when none does below <see cref="datum"/>. Pure; no world access.
    /// </summary>
    public static Type TgDefiningClass(datum A, string proc)
    {
        ArgumentNullException.ThrowIfNull(A);
        for (Type t = A.GetType(); t != null && t != typeof(datum) && t != typeof(object); t = t.BaseType)
        {
            if (UndeclaredOverrides.Has(t, proc) || DeclaresSlot(t, proc))
                return t;
        }
        return null;
    }

    // Whether the class declares (DeclaredOnly) a method of the slot's DM name (rules 2.4: a C# name ending in __proc).
    private static bool DeclaresSlot(Type t, string proc)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly;
        foreach (System.Reflection.MemberInfo m in t.GetMember(proc, System.Reflection.MemberTypes.Method, flags))
            return m != null;
        foreach (System.Reflection.MemberInfo m in t.GetMember(proc + "__proc", System.Reflection.MemberTypes.Method, flags))
            return m != null;
        return false;
    }

    /// <summary>
    /// The guard of D132, called at the top of a guarded chain proc (the translated base definitions of
    /// <see cref="GuardedProcs"/>, by M4a P1, P2, P3 and P4 at the sites docs/ai/specs/interaction.md section 7 lists): when
    /// <see cref="HasUndeclaredOverride"/> holds for <paramref name="A"/>, it refuses the action as tg's own refusal would look
    /// to the player: a <c>balloon_alert(user, "not ported yet")</c> when <paramref name="user"/> is given, one host log line
    /// per type and proc (like <c>Dm.Deferred</c>'s first call at a site), and a count in the world
    /// (<c>DmWorldState.DeferredCount</c> and its sites, so tests and the soak can read it). It returns true, and the caller
    /// returns at once with the proc's "nothing happened" value (null, FALSE or the chain's continue value, per site).
    /// Otherwise it does nothing and returns false.
    /// </summary>
    public static bool MissingOverride(datum A, string proc, mob user = null)
    {
        // M4a P1 (D132, DV27; interaction.md 7). The refusal looks like tg's own: a balloon alert to the user. The log line and
        // the count are Dm.Deferred's (one host log line for the first refusal of each type and proc; DeferredCount and
        // DeferredSites count every refusal), its site naming the type and the proc.
        if (A == null || !HasUndeclaredOverride(A, proc))
            return false;
        string path = A.GetType().GetCustomAttributes(typeof(DmPathAttribute), false) is { Length: > 0 } p ? ((DmPathAttribute)p[0]).Path : A.GetType().Name;
        Dm.Deferred("inert guard: " + path + " " + proc, "not ported yet: the type's own tg " + proc + " is not translated (D132, DV27)");
        if (user != null && A is atom shown)
            shown.balloon_alert(user, I18n.T("not ported yet"));
        return true;
    }

    /// <summary>
    /// DV27 for a translated class's own code that needs what its Initialize made, reached by an inert instance of a subtype (D112: an
    /// inert atom's init runs none of its types' Initialize overrides, so a gun has no firing pin, cell or magazine, a reagent container
    /// no reagents, a computer no <c>physical</c>): when <paramref name="A"/> is inert, <see cref="MissingOverride"/>'s refusal (one host
    /// log line per type and proc, the count, tg's "not ported yet" balloon to <paramref name="user"/> when given) and true; the caller
    /// returns at once with the proc's "nothing happened" value. Otherwise false.
    /// </summary>
    public static bool MissingInitialize(atom A, string proc, mob user = null)
    {
        if (A == null || !IsInert(A))
            return false;
        string path = DmTypes.PathOf(A.GetType()) ?? A.GetType().Name;
        Dm.Deferred("inert guard: " + path + " " + proc, "not ported yet: the type's own tg code is not translated, so its Initialize, which " + proc + " needs, did not run (D112, DV27)");
        if (user != null)
            A.balloon_alert(user, I18n.T("not ported yet"));
        return true;
    }

    /// <summary>
    /// M5 slice 1 (DV27's extension): the guard of damage. True, after the refusal <see cref="MissingOverride"/> makes (the
    /// balloon "not ported yet" to <paramref name="user"/> when given, the log line and the count), when <paramref name="A"/>'s
    /// type has its own tg code for one of the procs a hit runs (<c>take_damage</c>, <c>run_atom_armor</c>,
    /// <c>play_attack_sound</c>, <c>atom_break</c>, <c>atom_destruction</c>, <c>handle_deconstruct</c>,
    /// <c>atom_deconstruct</c>) that is not translated: an undeclared override (the generated table) or a generated
    /// live-partial stub (a row of <c>live-partial.txt</c>, which does nothing). The damage then does nothing to it, where the
    /// rest of the chain would run its parent's code around the missing part (a closet destroyed without dumping what it holds).
    /// </summary>
    public static bool MissingDamageOverride(datum A, mob user = null) => MissingAny(A, DamageProcs, user);

    /// <summary>
    /// M5 slice 8 (DV27): whether a crafted result's or a used component's type has its own tg <paramref name="proc"/>
    /// (<c>on_craft_completion</c>, <c>used_in_craft</c>) that is not translated, an undeclared override or a generated
    /// live-partial stub alike; construct_item and on_craft_completion then do not call it. No user is told (the craft itself
    /// goes on).
    /// </summary>
    public static bool MissingCraftOverride(datum A, string proc)
    {
        if (A == null)
            return false;
        if (MissingOverride(A, proc))
            return true;
        if (HasStubOverride(A, proc))
        {
            Refuse(A, proc, null);
            return true;
        }
        return false;
    }

    /// <summary>The same for a repair (<c>repair_damage</c>, <c>atom_fix</c>).</summary>
    public static bool MissingRepairOverride(datum A, mob user = null) => MissingAny(A, RepairProcs, user);

    /// <summary>The same for obj's deconstruction (<c>handle_deconstruct</c>, <c>atom_deconstruct</c>, a machine's
    /// <c>on_deconstruction</c>).</summary>
    public static bool MissingDeconstructOverride(datum A, mob user = null) => MissingAny(A, DeconstructProcs, user);

    private static bool MissingAny(datum A, System.Collections.Immutable.ImmutableArray<string> procs, mob user)
    {
        if (A == null)
            return false;
        foreach (string proc in procs)
        {
            if (MissingOverride(A, proc, user))
                return true;
            if (HasStubOverride(A, proc))
            {
                Refuse(A, proc, user);
                return true;
            }
        }
        return false;
    }

    // Whether the nearest class of A's chain that declares the slot's method (the binder's names) declares a generated
    // live-partial stub ([DmGeneratedStub]): the type's own tg code for the proc is a stub that does nothing.
    private static bool HasStubOverride(datum A, string proc)
        => StubCache.GetOrAdd((A.GetType(), proc), static key => HasStubOverride(key.Item1, key.Item2)); // M5x slice 1: per type and proc

    private static bool HasStubOverride(Type type, string proc)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly;
        for (Type t = type; t != null && t != typeof(datum) && t != typeof(object); t = t.BaseType)
        {
            System.Reflection.MemberInfo[] found = t.GetMember(proc, System.Reflection.MemberTypes.Method, flags);
            if (found.Length == 0)
                found = t.GetMember(proc + "__proc", System.Reflection.MemberTypes.Method, flags);
            if (found.Length == 0)
                continue;
            foreach (System.Reflection.MemberInfo m in found)
            {
                if (m.IsDefined(typeof(DmGeneratedStubAttribute), inherit: false))
                    return true;
            }
            return false;
        }
        return false;
    }

    // MissingOverride's refusal (the log line, the count, the balloon) for a stub.
    private static void Refuse(datum A, string proc, mob user)
    {
        string path = A.GetType().GetCustomAttributes(typeof(DmPathAttribute), false) is { Length: > 0 } p ? ((DmPathAttribute)p[0]).Path : A.GetType().Name;
        Dm.Deferred("inert guard: " + path + " " + proc, "not ported yet: the type's own tg " + proc + " is a live-partial stub (D132, DV27)");
        if (user != null && A is atom shown)
            shown.balloon_alert(user, I18n.T("not ported yet"));
    }
}
