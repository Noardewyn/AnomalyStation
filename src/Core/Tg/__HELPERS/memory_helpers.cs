// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/memory_helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package G4 (D291): the whole file. memory_helpers.dm is the earliest file of tgstation.dme that declares something on
// /datum/mind (/datum/mind/proc/_add_memory, :59), so this part carries its [DmPath] (rules 2.3). The three macros (add_memory_in_range,
// add_mob_memory, add_memory) wrap their arguments in list(...) as DM does: a named argument (Dm.named, rules 7.3) becomes an entry with a
// text key, which arglist turns back into a named argument of the memory's New.

public static partial class GlobalProcs
{
    /**
     * Adds a memory to all carbon mobs in a certain range of a certain atom.
     *
     * The third argument should be a typepath of a /datum/memory.
     *
     * Two things of note when using this:
     * * If the source atom is a mob, it will be added to that mob if possible.
     * * The protagonist of the memory is set to the memorizer by default. If sourced from a mob, you need to set the protagonist manually.
     *
     * Beyond that, can be supplied with named arguments pertaining to your memory type.
     * Common arguments include:
     * * protagonist: The main subject of the memory
     * * deuteragonist: The side subject of the memory. Doesn't necessarily have to be a mob!
     * * antagonist: The main villain of the memory. Also doesn't necessarily have to be a mob!
     */
    // tg: code/__HELPERS/memory_helpers.dm:16  #define add_memory_in_range(source, range, arguments...) _add_memory_in_range(source, range, list(##arguments))
    public static void add_memory_in_range(atom source, float range, params object[] arguments) => _add_memory_in_range(source, range, datum.DcsArgumentList(arguments));

    /// Unless you need to use this for an explicit reason, use the add_memory_in_range macro wrapper.
    // tg: code/__HELPERS/memory_helpers.dm:19
    public static void _add_memory_in_range(atom source = null, float? range = null, DmList memory_args = null)
    {
        range ??= 7;
        foreach (mob_living_carbon memorizer in Dm.each<mob_living_carbon>(hearers(range, source)))
            memorizer.mind?._add_memory(memory_args.Copy()); // One copy for each memory, since it mutates the list
    }
}

public partial class mob : atom_movable
{
    /**
     * Adds a memory to the target mob.
     *
     * The first argument should be a typepath of a /datum/memory.
     *
     * If the mob already has a memory of that type, it will be deleted.
     *
     * Beyond that, can be supplied with named arguments pertaining to your memory type.
     * Common arguments include:
     * * protagonist: The main subject of the memory
     * * deuteragonist: The side subject of the memory. Doesn't necessarily have to be a mob!
     * * antagonist: The main villain of the memory. Also doesn't necessarily have to be a mob!
     *
     * Returns the datum memory created, or null otherwise.
     */
    // tg: code/__HELPERS/memory_helpers.dm:38  #define add_mob_memory(arguments...) mind?._add_memory(list(##arguments))  (called on a mob: user.add_mob_memory(...))
    public object add_mob_memory(params object[] arguments) => mind?._add_memory(DcsArgumentList(arguments));
}

// tg: code/__HELPERS/memory_helpers.dm:59
[DmPath("/datum/mind")]
public partial class datum_mind : datum
{
    // Wrapper for _add_memory so we can used named arguments.
    /**
     * Adds a memory to the target mind.
     *
     * The first argument should be a typepath of a /datum/memory.
     *
     * If the mob already has a memory of that type, it will be deleted.
     *
     * Beyond that, can be supplied with named arguments pertaining to your memory type.
     * Common arguments include:
     * * protagonist: The main subject of the memory
     * * deuteragonist: The side subject of the memory. Doesn't necessarily have to be a mob!
     * * antagonist: The main villain of the memory. Also doesn't necessarily have to be a mob!
     *
     * Returns the datum memory created, or null otherwise.
     */
    // tg: code/__HELPERS/memory_helpers.dm:56  #define add_memory(arguments...) _add_memory(list(##arguments))
    public object add_memory(params object[] arguments) => _add_memory(DcsArgumentList(arguments));

    /// Unless you need to use this for an explicit reason, use the add_memory, add_mob_memory, or add_memory_in_range macro wrappers.
    // tg: code/__HELPERS/memory_helpers.dm:59
    public virtual object _add_memory(DmList memory_args = null)
    {
        Type memory_type = memory_args[1] as Type;
        if (!ispath(memory_type))
            throw Dm.CRASH(Dm.text("add_memory called with an invalid memory type. (Got: [])", Dm.truthy(memory_args[1]) ? memory_args[1] : "null"), "code/__HELPERS/memory_helpers.dm", 63);

        if (current != null)
        {
            float new_memory_flags = Dm.initial(memory_type, (datum_memory m) => m.memory_flags);
            if (!Dm.truthy(Dm.bitand(new_memory_flags, MEMORY_SKIP_UNCONSCIOUS)) && IS_UNCONSCIOUS(current))
                return null;
            if (Dm.truthy(Dm.bitand(new_memory_flags, MEMORY_CHECK_BLINDNESS)) && Dm.truthy(current.is_blind()))
                return null;
            if (Dm.truthy(Dm.bitand(new_memory_flags, MEMORY_CHECK_DEAFNESS)) && HAS_TRAIT(current, TRAIT_DEAF))
                return null;
        }

        datum_memory replaced_memory = memories[memory_type] as datum_memory;
        if (replaced_memory != null)
            qdel(replaced_memory);

        memory_args[1] = this;
        datum_memory created_memory = Dm.@new(memory_type, Dm.arglist(memory_args)) as datum_memory;

        memories[memory_type] = created_memory;
        return created_memory;
    }

    /**
     * Simple / sane proc for giving a mob the option to select one of their memories
     * that do not have the flags [MEMORY_FLAG_ALREADY_USED] or [MEMORY_NO_STORY].
     *
     * Arguments
     * * verbage: This is used in the tgui selection menu, explains what they're selecting a memory to do.
     *
     * Returns the memory selected, or null otherwise.
     */
    // tg: code/__HELPERS/memory_helpers.dm:93
    public virtual async DmTask<object> select_memory(string verbage = null)
    {
        verbage ??= "use";
        DmList choice_list = Dm.list();

        foreach (object key in Dm.each(memories))
        {
            datum_memory memory_iter = memories[key] as datum_memory;
            if (Dm.truthy(Dm.bitand(memory_iter.memory_flags, Dm.bitor(MEMORY_FLAG_ALREADY_USED, MEMORY_NO_STORY)))) //Can't use memories multiple times
                continue;
            choice_list[memory_iter.name] = memory_iter;
        }

        object choice = await tgui_input_list(usr, I18n.T("Select a memory to []", verbage), I18n.T("Memory Selection?"), choice_list);
        if (choice == null)
            return FALSE;
        if (choice_list[choice] == null)
            return FALSE;
        datum_memory memory_choice = choice_list[choice] as datum_memory;

        return memory_choice;
    }

    /// Small helper to clean out memories.
    // tg: code/__HELPERS/memory_helpers.dm:113
    public virtual void wipe_memory()
    {
        QDEL_LIST_ASSOC_VAL(memories);
    }

    /// Helder to wipe the passed memory type ONLY from our list of memories
    // tg: code/__HELPERS/memory_helpers.dm:117
    public virtual void wipe_memory_type(object memory_type = null)
    {
        qdel(memories[memory_type]);
        Dm.remove(ref memories, memory_type);
    }

    /// Helper to create quick copies of all of our memories
    /// Quick copies aren't full copies - just basic copies containing necessities.
    /// They cannot be used in stories.
    // tg: code/__HELPERS/memory_helpers.dm:124
    public virtual void quick_copy_all_memories(datum_mind new_memorizer = null)
    {
        foreach (object memory_path in Dm.each(memories))
        {
            datum_memory prime_memory = memories[memory_path] as datum_memory;
            new_memorizer.memories[memory_path] = prime_memory.quick_copy_memory(new_memorizer);
        }
    }
}
