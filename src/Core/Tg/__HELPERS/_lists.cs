// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/_lists.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/*
 * Holds procs to help with list operations
 * Contains groups:
 * Misc
 * Sorting
 */

/*
 * Misc
 */

// The function-like macros of this file are GlobalProcs members (rules 11.1) unless noted as inline. Their list
// argument is a `ref DmList` where the macro assigns it (rules 6.4); callers with a list var of another C# type get
// an overload when one is needed.
public static partial class GlobalProcs
{
    // Generic listoflist safe add and removal macros:
    // tg: code/__HELPERS/_lists.dm:14
    ///If value is a list, wrap it in a list so it can be used with list add/remove operations
    public static object LIST_VALUE_WRAP_LISTS(object value)
    {
        return (islist(value) ? (object)Dm.list(value) : value);
    }

    // tg: code/__HELPERS/_lists.dm:16
    ///Add an untyped item to a list, taking care to handle list items by wrapping them in a list to remove the footgun
    public static void UNTYPED_LIST_ADD(ref DmList list, object item)
    {
        Dm.append(ref list, LIST_VALUE_WRAP_LISTS(item));
    }

    // tg: code/__HELPERS/_lists.dm:18
    ///Remove an untyped item to a list, taking care to handle list items by wrapping them in a list to remove the footgun
    public static void UNTYPED_LIST_REMOVE(ref DmList list, object item)
    {
        Dm.remove(ref list, LIST_VALUE_WRAP_LISTS(item));
    }

    /*
     * ## Lazylists
     *
     * * What is a lazylist?
     *
     * True to its name a lazylist is a lazy instantiated list.
     * It is a list that is only created when necessary (when it has elements) and is null when empty.
     *
     * * Why use a lazylist?
     *
     * Lazylists save memory - an empty list that is never used takes up more memory than just `null`.
     *
     * * When to use a lazylist?
     *
     * Lazylists are best used on hot types when making lists that are not always used.
     *
     * For example, if you were adding a list to all atoms that tracks the names of people who touched it,
     * you would want to use a lazylist because most atoms will never be touched by anyone.
     *
     * * How do I use a lazylist?
     *
     * A lazylist is just a list you defined as `null` rather than `list()`.
     * Then, you use the LAZY* macros to interact with it, which are essentially null-safe ways to interact with a list.
     *
     * Note that you probably should not be using these macros if your list is not a lazylist.
     * This will obfuscate the code and make it a bit harder to read and debug.
     *
     * Generally speaking you shouldn't be checking if your lazylist is `null` yourself, the macros will do that for you.
     * Remember that LAZYLEN (and by extension, length) will return 0 if the list is null.
     */

    // tg: code/__HELPERS/_lists.dm:52
    ///Initialize the lazylist
    public static void LAZYINITLIST(ref DmList L)
    {
        if (!Dm.truthy(L))
            L = Dm.list();
    }

    // tg: code/__HELPERS/_lists.dm:54
    ///If the provided list is empty, set it to null
    public static void UNSETEMPTY(ref DmList L)
    {
        if (Dm.truthy(L) && !Dm.truthy(length(L)))
            L = null;
    }

    // tg: code/__HELPERS/_lists.dm:56
    ///If the provided key -> list is empty, remove it from the list
    public static void ASSOC_UNSETEMPTY(ref DmList L, object K)
    {
        if (!Dm.truthy(length(L[K])))
            Dm.remove(ref L, K);
    }

    // tg: code/__HELPERS/_lists.dm:58
    ///Like LAZYCOPY - copies an input list if the list has entries, If it doesn't the assigned list is nulled
    public static DmList LAZYLISTDUPLICATE(object L)
    {
        return (Dm.truthy(L) ? ((DmList)L).Copy() : null);
    }

    // tg: code/__HELPERS/_lists.dm:60
    ///Remove an item from the list, set the list to null if empty
    public static void LAZYREMOVE(ref DmList L, object I)
    {
        if (Dm.truthy(L))
        {
            Dm.remove(ref L, I);
            if (!Dm.truthy(length(L)))
                L = null;
        }
    }

    // tg: code/__HELPERS/_lists.dm:62
    ///Add an item to the list, if the list is null it will initialize it
    public static void LAZYADD(ref DmList L, object I)
    {
        if (!Dm.truthy(L))
            L = Dm.list();
        Dm.append(ref L, I);
    }

    // tg: code/__HELPERS/_lists.dm:64
    ///Add an item to the list if not already present, if the list is null it will initialize it
    public static void LAZYOR(ref DmList L, object I)
    {
        if (!Dm.truthy(L))
            L = Dm.list();
        Dm.combine(ref L, I);
    }

    // tg: code/__HELPERS/_lists.dm:66
    ///Returns the key of the submitted item in the list
    public static float? LAZYFIND(object L, object V)
    {
        return ((DmList)L)?.Find(V);
    }

    // tg: code/__HELPERS/_lists.dm:68
    ///returns L[I] if L exists and I is a valid index of L, runtimes if L is not a list
    public static object LAZYACCESS(object L, object I)
    {
        return (Dm.truthy(L) ? (isnum(I) ? (Dm.gt(I, 0) && Dm.le(I, length(L)) ? ((DmList)L)[I] : null) : ((DmList)L)[I]) : null);
    }

    // tg: code/__HELPERS/_lists.dm:70
    ///Sets the item K to the value V, if the list is null it will initialize it
    public static void LAZYSET(ref DmList L, object K, object V)
    {
        if (!Dm.truthy(L))
            L = Dm.list();
        L[K] = V;
    }

    // tg: code/__HELPERS/_lists.dm:72
    ///Sets the length of a lazylist
    public static void LAZYSETLEN(ref DmList L, float V)
    {
        if (!Dm.truthy(L))
            L = Dm.list();
        L.len = V;
    }

    // tg: code/__HELPERS/_lists.dm:74
    ///Returns the length of the list
    public static float LAZYLEN(object L)
    {
        return length(L);
    }

    // tg: code/__HELPERS/_lists.dm:76
    ///Sets a list to null
    public static void LAZYNULL<T>(ref T L) where T : class
    {
        L = null;
    }

    // tg: code/__HELPERS/_lists.dm:78
    ///Adds to the item K the value V, if the list is null it will initialize it
    public static void LAZYADDASSOC(ref DmList L, object K, object V)
    {
        if (!Dm.truthy(L))
            L = Dm.list();
        Dm.append_at(L, K, V);
    }

    // tg: code/__HELPERS/_lists.dm:80
    ///This is used to add onto lazy assoc list when the value you're adding is a /list/. This one has extra safety over lazyaddassoc because the value could be null (and thus cant be used to += objects)
    public static void LAZYADDASSOCLIST(ref DmList L, object K, object V)
    {
        if (!Dm.truthy(L))
            L = Dm.list();
        Dm.append_at(L, K, Dm.list(V));
    }

    // tg: code/__HELPERS/_lists.dm:82
    ///Removes the value V from the item K, if the item K is empty will remove it from the list, if the list is empty will set the list to null
    public static void LAZYREMOVEASSOC(ref DmList L, object K, object V)
    {
        if (Dm.truthy(L?[K]))
        {
            Dm.remove_at(L, K, V);
            if (!Dm.truthy(length(L[K])))
                Dm.remove(ref L, K);
            if (!Dm.truthy(length(L)))
                L = null;
        }
    }

    // tg: code/__HELPERS/_lists.dm:84
    ///Accesses an associative list, returns null if nothing is found
    public static object LAZYACCESSASSOC(object L, object I, object K)
    {
        return ((DmList)((DmList)L)?[I])?[K];
    }

    // tg: code/__HELPERS/_lists.dm:86
    ///Qdel every item in the list before setting the list to null
    public static void QDEL_LAZYLIST(ref DmList L)
    {
        foreach (object I in Dm.each(L))
            qdel(I);
        L = null;
    }

    //These methods don't null the list
    // tg: code/__HELPERS/_lists.dm:89
    ///Use LAZYLISTDUPLICATE instead if you want it to null with no entries
    public static DmList LAZYCOPY(object L)
    {
        return (((DmList)L)?.Copy() is var or__1 && Dm.truthy(or__1) ? or__1 : Dm.list());
    }

    // tg: code/__HELPERS/_lists.dm:91
    /// Consider LAZYNULL instead
    public static void LAZYCLEARLIST(object L)
    {
        ((DmList)L)?.Cut();
    }

    // tg: code/__HELPERS/_lists.dm:93
    ///Returns the list if it's actually a valid list, otherwise will initialize it
    public static object SANITIZE_LIST(object L)
    {
        return (islist(L) ? L : (object)Dm.list());
    }

    // tg: code/__HELPERS/_lists.dm:95
    /// Performs an insertion on the given lazy list with the given key and value. If the value already exists, a new one will not be made.
    public static void LAZYORASSOCLIST(ref DmList lazy_list, object key, object value)
    {
        LAZYINITLIST(ref lazy_list);
        // LAZYINITLIST(lazy_list[key]): a list element cannot be passed by ref; its expansion `lazy_list[key] ||= list();` (rules 4.5)
        if (!Dm.truthy(lazy_list[key]))
            lazy_list[key] = Dm.list();
        Dm.combine_at(lazy_list, key, value);
    }

    // tg: code/__HELPERS/_lists.dm:100
    /// Calls Insert on the lazy list if it exists, otherwise initializes it with the value
    public static void LAZYINSERT(ref DmList lazylist, float index, object value)
    {
        if (!Dm.truthy(lazylist))
            lazylist = Dm.list(value);
        else if (index == 0 && index > length(lazylist))
            Dm.append(ref lazylist, value);
        else
            lazylist.Insert(index, value);
    }

    // tg: code/__HELPERS/_lists.dm:109
    /// Returns the smallest element in the lazy list, or default if the list is null
    public static object LAZYMAX(object lazylist, object @default)
    {
        return (lazylist == null ? @default : max(lazylist));
    }

    // tg: code/__HELPERS/_lists.dm:111
    /// Returns the largest element in the lazy list, or default if the list is null
    public static object LAZYMIN(object lazylist, object @default)
    {
        return (lazylist == null ? @default : min(lazylist));
    }

    // tg: code/__HELPERS/_lists.dm:114  #define LISTASSERTLEN(L, I, V...)
    ///Ensures the length of a list is at least I, prefilling it with V if needed. if V is a proc call, it is repeated for each new index so that list() can just make a new list for each item.
    // Inline at each use: it declares locals and evaluates V once per new index (and once more for its `if`), so a
    // member that receives V evaluated once would change it (rules 11.1 (e), a lazy-argument macro).

    // tg: code/__HELPERS/_lists.dm:126
    public static object reverseList(object L)
    {
        return reverse_range(((DmList)L).Copy());
    }

    // tg: code/__HELPERS/_lists.dm:129  #define COMPARE_KEY __BIN_LIST[__BIN_MID]
    /// Passed into BINARY_INSERT to compare keys
    // tg: code/__HELPERS/_lists.dm:131  #define COMPARE_VALUE __BIN_LIST[__BIN_LIST[__BIN_MID]]
    /// Passed into BINARY_INSERT to compare values
    // Both name the locals of a BINARY_INSERT* expansion (rules 11.1 (d)): inline inside it.

    /****
     * Binary search sorted insert
     * INPUT: Object to be inserted
     * LIST: List to insert object into
     * TYPECONT: The typepath of the contents of the list
     * COMPARE: The object to compare against, usualy the same as INPUT
     * COMPARISON: The variable on the objects to compare
     * COMPTYPE: How should the values be compared? Either COMPARE_KEY or COMPARE_VALUE.
     */
    // tg: code/__HELPERS/_lists.dm:142  #define BINARY_INSERT(INPUT, LIST, TYPECONT, COMPARE, COMPARISON, COMPTYPE)
    // Inline at each use: it pastes TYPECONT into a declaration and COMPARISON into member accesses (rules 11.1 (a), (b)).
    // Its tie rule: an item equal to INPUT stays before it (`<=` moves right).

    /**
     * Custom binary search sorted insert utilising comparison procs instead of vars.
     * INPUT: Object to be inserted
     * LIST: List to insert object into
     * TYPECONT: The typepath of the contents of the list
     * COMPARE: The object to compare against, usualy the same as INPUT
     * COMPARISON: The plaintext name of a proc on INPUT that takes a single argument to accept a single element from LIST and returns a positive, negative or zero number to perform a comparison.
     * COMPTYPE: How should the values be compared? Either COMPARE_KEY or COMPARE_VALUE.
     */
    // tg: code/__HELPERS/_lists.dm:177  #define BINARY_INSERT_PROC_COMPARE(INPUT, LIST, TYPECONT, COMPARE, COMPARISON, COMPTYPE)
    // Inline at each use (rules 11.1 (a), (b)), like BINARY_INSERT.

    // tg: code/__HELPERS/_lists.dm:203
    public static object SORT_FIRST_INDEX(object list)
    {
        return ((DmList)list)[1];
    }

    // tg: code/__HELPERS/_lists.dm:204
    public static object SORT_PRIORITY_INDEX(object list)
    {
        return ((DmList)list)["priority"];
    }

    // tg: code/__HELPERS/_lists.dm:205
    public static object SORT_COMPARE_DIRECTLY(object thing)
    {
        return thing;
    }

    // tg: code/__HELPERS/_lists.dm:206  #define SORT_VAR_NO_TYPE(varname) var/varname
    // A declaration: inline (rules 11.1 (c)), only as the TYPECONT argument of BINARY_INSERT_DEFINE.

    /****
     * Even more custom binary search sorted insert, using defines instead of vars
     * INPUT: Item to be inserted
     * LIST: List to insert INPUT into
     * TYPECONT: A define setting the var to the typepath of the contents of the list
     * COMPARE: The item to compare against, usualy the same as INPUT
     * COMPARISON: A define that takes an item to compare as input, and returns their comparable value
     * COMPTYPE: How should the list be compared? Either COMPARE_KEY or COMPARE_VALUE.
     */
    // tg: code/__HELPERS/_lists.dm:216  #define BINARY_INSERT_DEFINE(INPUT, LIST, TYPECONT, COMPARE, COMPARISON, COMPTYPE)
    // Inline at each use (rules 11.1 (b)): it pastes TYPECONT and COMPARISON into its text.

    // tg: code/__HELPERS/_lists.dm:243
    // The default separator and filler words are plain literals: data that reaches a player only through a caller's
    // message (rules 5.3).
    ///Returns a list in plain english as a string
    public static object english_list(DmList input = null, string nothing_text = null, string and_text = null, string comma_text = null, string final_comma_text = null)
    {
        nothing_text ??= "nothing";
        and_text ??= " and ";
        comma_text ??= ", ";
        final_comma_text ??= "";
        float total = length(input);
        switch (total)
        {
            case 0:
                return Dm.text("[]", nothing_text);
            case 1:
                return Dm.text("[]", input[1]);
            case 2:
                return Dm.text("[][][]", input[1], and_text, input[2]);
            default:
                string output = "";
                float index = 1;
                while (index < total)
                {
                    if (index == total - 1)
                        comma_text = final_comma_text;

                    output += Dm.text("[][]", input[index], comma_text);
                    index++;
                }

                return Dm.text("[][][]", output, and_text, input[index]);
        }
    }

    // tg: code/__HELPERS/_lists.dm:265
    ///Returns a list of atom types in plain english as a string of each type name
    public static object type_english_list(DmList input = null, string nothing_text = null, string and_text = null, string comma_text = null, string final_comma_text = null)
    {
        nothing_text ??= "nothing";
        and_text ??= " and ";
        comma_text ??= ", ";
        final_comma_text ??= "";
        DmList english_input = Dm.list();
        foreach (Type type in Dm.each_cast<Type>(input))
            Dm.append(ref english_input, Dm.text("[]", Dm.initial(type, (atom p) => p.name)));
        return english_list(english_input, nothing_text, and_text, comma_text, final_comma_text);
    }

    /**
     * Checks for specific types in a list.
     *
     * If using zebra mode the list should be an assoc list with truthy/falsey values.
     * The check short circuits so earlier entries in the input list will take priority.
     * Ergo, subtypes should come before parent types.
     * Notice that this is the opposite priority of [/proc/typecacheof].
     *
     * Arguments:
     * - [type_to_check][/datum]: An instance to check.
     * - [list_to_check][/list]: A list of typepaths to check the type_to_check against.
     * - zebra: Whether to use the value of the matching type (closest to our type) in the list instead of just returning true when a match is found.
     * - return_first_match: If zebra is true, this will return the first match found, instead of the value of closest type
     */
    // tg: code/__HELPERS/_lists.dm:285
    public static object is_type_in_list(datum type_to_check = null, DmList list_to_check = null, float? zebra = null, float? return_first_match = null)
    {
        zebra ??= FALSE;
        return_first_match ??= FALSE;
        object dot = null;
        if (!Dm.truthy(LAZYLEN(list_to_check)) || !Dm.truthy(type_to_check))
            return FALSE;
        dot = FALSE;
        object highest_matched_type = null;
        foreach (object type in Dm.each(list_to_check))
        {
            if (!istype(type_to_check, type as Type))
                continue;
            if (!Dm.truthy(zebra))
                return TRUE;
            if (Dm.truthy(return_first_match) || Dm.eq(type, type_to_check.type))
                return list_to_check[type];
            if (!Dm.truthy(highest_matched_type) || ispath(type, highest_matched_type as Type))
            {
                dot = list_to_check[type];
                highest_matched_type = type;
            }
        }
        return dot;
    }

    /**
     * Checks for specific paths in a list.
     *
     * If using zebra mode the list should be an assoc list with truthy/falsey values.
     * The check short circuits so earlier entries in the input list will take priority.
     * Ergo, subpaths should come before parent paths.
     * Notice that this is the opposite priority of [/proc/typecacheof].
     *
     * Arguments:
     * - path_to_check: A typepath to check.
     * - [list_to_check][/list]: A list of typepaths to check the path_to_check against.
     * - zebra: Whether to use the value of the mathing path in the list instead of just returning true when a match is found.
     */
    // tg: code/__HELPERS/_lists.dm:314
    public static object is_path_in_list(object path_to_check = null, DmList list_to_check = null, float? zebra = null)
    {
        zebra ??= FALSE;
        if (!Dm.truthy(LAZYLEN(list_to_check)) || !Dm.truthy(path_to_check))
            return FALSE;
        foreach (object path in Dm.each(list_to_check))
        {
            if (ispath(path_to_check, path as Type))
                return (Dm.not(zebra) is var or__1 && Dm.truthy(or__1) ? or__1 : list_to_check[path]);
        }
        return FALSE;
    }

    // tg: code/__HELPERS/_lists.dm:323
    ///Checks for specific types in specifically structured (Assoc "type" = TRUE|FALSE) lists ('typecaches')
    public static object is_type_in_typecache(object A, object L)
    {
        return ((A is var and__1 && !Dm.truthy(and__1) ? and__1 : (object)length(L)) is var and__2 && !Dm.truthy(and__2) ? and__2 : ((DmList)L)[(ispath(A) ? A : Dm.get_var(A, "type"))]);
    }

    // tg: code/__HELPERS/_lists.dm:326
    ///returns a new list with only atoms that are in the typecache list
    public static object typecache_filter_list(DmList atoms = null, DmList typecache = null)
    {
        DmList dot = null;
        dot = Dm.list();
        foreach (atom atom_checked in Dm.each_cast<atom>(atoms))
        {
            if (Dm.truthy(typecache[atom_checked.type]))
                Dm.append(ref dot, atom_checked);
        }
        return dot;
    }

    // tg: code/__HELPERS/_lists.dm:334
    ///return a new list with atoms that are not in the typecache list
    public static object typecache_filter_list_reverse(DmList atoms = null, DmList typecache = null)
    {
        DmList dot = null;
        dot = Dm.list();
        foreach (atom atom_checked in Dm.each_cast<atom>(atoms))
        {
            if (!Dm.truthy(typecache[atom_checked.type]))
                Dm.append(ref dot, atom_checked);
        }
        return dot;
    }

    // tg: code/__HELPERS/_lists.dm:342
    ///similar to typecache_filter_list and typecache_filter_list_reverse but it supports an inclusion list and and exclusion list
    public static object typecache_filter_multi_list_exclusion(DmList atoms = null, DmList typecache_include = null, DmList typecache_exclude = null)
    {
        DmList dot = null;
        dot = Dm.list();
        foreach (atom atom_checked in Dm.each_cast<atom>(atoms))
        {
            if (Dm.truthy(typecache_include[atom_checked.type]) && !Dm.truthy(typecache_exclude[atom_checked.type]))
                Dm.append(ref dot, atom_checked);
        }
        return dot;
    }

    /**
     * Like typesof() or subtypesof(), but returns a typecache instead of a list.
     *
     * Arguments:
     * - path: A typepath or list of typepaths.
     * - only_root_path: Whether the typecache should be specifically of the passed types.
     * - ignore_root_path: Whether to ignore the root path when caching subtypes.
     */
    // tg: code/__HELPERS/_lists.dm:356
    public static object typecacheof(object path = null, float? only_root_path = null, float? ignore_root_path = null)
    {
        only_root_path ??= FALSE;
        ignore_root_path ??= FALSE;
        DmList dot = null;
        if (path == null)
            return dot;

        if (ispath(path))
        {
            dot = Dm.list();
            if (Dm.truthy(only_root_path))
            {
                dot[path] = TRUE;
                return dot;
            }

            foreach (object subtype in Dm.each((Dm.truthy(ignore_root_path) ? subtypesof(path) : typesof(path))))
                dot[subtype] = TRUE;
            return dot;
        }

        if (!islist(path))
            throw Dm.CRASH(Dm.text("Tried to create a typecache of [] which is neither a typepath nor a list.", path), "code/__HELPERS/_lists.dm", 371);

        dot = Dm.list();
        DmList pathlist = path as DmList;
        if (Dm.truthy(only_root_path))
        {
            foreach (object current_path in Dm.each(pathlist))
                dot[current_path] = TRUE;
        }
        else if (Dm.truthy(ignore_root_path))
        {
            foreach (object current_path in Dm.each(pathlist))
            {
                foreach (object subtype in Dm.each(subtypesof(current_path)))
                    dot[subtype] = TRUE;
            }
        }
        else
        {
            foreach (object current_path in Dm.each(pathlist))
            {
                foreach (object subpath in Dm.each(typesof(current_path)))
                    dot[subpath] = TRUE;
            }
        }
        return dot;
    }

    /**
     * Like typesof() or subtypesof(), but returns a typecache instead of a list.
     * This time it also uses the associated values given by the input list for the values of the subtypes.
     *
     * Latter values from the input list override earlier values.
     * Thus subtypes should come _after_ parent types in the input list.
     * Notice that this is the opposite priority of [/proc/is_type_in_list] and [/proc/is_path_in_list].
     *
     * Arguments:
     * - path: A typepath or list of typepaths with associated values.
     * - single_value: The assoc value used if only a single path is passed as the first variable.
     * - only_root_path: Whether the typecache should be specifically of the passed types.
     * - ignore_root_path: Whether to ignore the root path when caching subtypes.
     * - clear_nulls: Whether to remove keys with null assoc values from the typecache after generating it.
     */
    // tg: code/__HELPERS/_lists.dm:402
    public static object zebra_typecacheof(object path = null, float? single_value = null, float? only_root_path = null, float? ignore_root_path = null, float? clear_nulls = null)
    {
        single_value ??= TRUE;
        only_root_path ??= FALSE;
        ignore_root_path ??= FALSE;
        clear_nulls ??= FALSE;
        DmList dot = null;
        if (path == null)
            return dot;

        if (ispath(path))
        {
            if (single_value == null)
                return dot;

            dot = Dm.list();
            if (Dm.truthy(only_root_path))
            {
                dot[path] = single_value;
                return dot;
            }

            foreach (object subtype in Dm.each((Dm.truthy(ignore_root_path) ? subtypesof(path) : typesof(path))))
                dot[subtype] = single_value;
            return dot;
        }

        if (!islist(path))
            throw Dm.CRASH(Dm.text("Tried to create a typecache of [] which is neither a typepath nor a list.", path), "code/__HELPERS/_lists.dm", 420);

        dot = Dm.list();
        DmList pathlist = path as DmList;
        if (Dm.truthy(only_root_path))
        {
            foreach (object current_path in Dm.each(pathlist))
                dot[current_path] = pathlist[current_path];
        }
        else if (Dm.truthy(ignore_root_path))
        {
            foreach (object current_path in Dm.each(pathlist))
            {
                foreach (object subtype in Dm.each(subtypesof(current_path)))
                    dot[subtype] = pathlist[current_path];
            }
        }
        else
        {
            foreach (object current_path in Dm.each(pathlist))
            {
                foreach (object subpath in Dm.each(typesof(current_path)))
                    dot[subpath] = pathlist[current_path];
            }
        }

        if (!Dm.truthy(clear_nulls))
            return dot;

        foreach (object cached_path in Dm.each(dot))
        {
            if (dot[cached_path] == null)
                Dm.remove(ref dot, cached_path);
        }
        return dot;
    }

    /**
     * Removes any null entries from the list
     * Returns TRUE if the list had nulls, FALSE otherwise
    **/
    // tg: code/__HELPERS/_lists.dm:448
    public static object list_clear_nulls(DmList list_to_clear = null)
    {
        return Dm.num(list_to_clear.RemoveAll(null) > 0);
    }

    /**
     * Removes any empty weakrefs from the list
     * Returns TRUE if the list had empty refs, FALSE otherwise
    **/
    // tg: code/__HELPERS/_lists.dm:456
    public static object list_clear_empty_weakrefs(DmList list_to_clear = null)
    {
        float start_len = list_to_clear.len;
        foreach (datum_weakref entry in Dm.each<datum_weakref>(list_to_clear))
        {
            if (!Dm.truthy(entry.resolve()))
                Dm.remove(ref list_to_clear, entry);
        }
        return Dm.num(list_to_clear.len < start_len);
    }

    /*
     * Returns list containing all the entries from first list that are not present in second.
     * If skiprep = 1, repeated elements are treated as one.
     * If either of arguments is not a list, returns null
     */
    // tg: code/__HELPERS/_lists.dm:468
    public static object difflist(DmList first = null, DmList second = null, float? skiprep = null)
    {
        skiprep ??= 0;
        if (!islist(first) || !islist(second))
            return null;
        DmList result = Dm.list();
        if (Dm.truthy(skiprep))
        {
            foreach (object entry in Dm.each(first))
            {
                if (!Dm.@in(entry, result) && !Dm.@in(entry, second))
                    UNTYPED_LIST_ADD(ref result, entry);
            }
        }
        else
        {
            result = Dm.sub(first, second) as DmList;
        }
        return result;
    }

    /*
     * Returns list containing entries that are in either list but not both.
     * If skipref = 1, repeated elements are treated as one.
     * If either of arguments is not a list, returns null
     */
    // tg: code/__HELPERS/_lists.dm:485
    public static object unique_merge_list(DmList first = null, DmList second = null, float? skiprep = null)
    {
        skiprep ??= 0;
        if (!islist(first) || !islist(second))
            return null;
        DmList result = Dm.list();
        if (Dm.truthy(skiprep))
            result = Dm.add(difflist(first, second, skiprep), difflist(second, first, skiprep)) as DmList;
        else
            result = Dm.bitxor(first, second) as DmList;
        return result;
    }

    /**
     * Picks a random element from a list based on a weighting system.
     * For example, given the following list:
     * A = 6, B = 3, C = 1, D = 0
     * A would have a 60% chance of being picked,
     * B would have a 30% chance of being picked,
     * C would have a 10% chance of being picked,
     * and D would have a 0% chance of being picked.
     * You should only pass integers in.
     */
    // tg: code/__HELPERS/_lists.dm:505
    public static object pick_weight(DmList list_to_pick = null)
    {
        if (length(list_to_pick) == 0)
            return null;

        float total = 0;
        foreach (object item in Dm.each(list_to_pick))
        {
            if (!Dm.truthy(list_to_pick[item]))
                list_to_pick[item] = 0;
            // tg: TEST_ONLY_ASSERT(...) (:513-516) expands to nothing in production (__DEFINES/unit_tests.dm:19, UNIT_TESTS undefined, rules 1.7)
            total += Dm.num(list_to_pick[item]);
        }

        total = rand(1, total);
        foreach (object item in Dm.each(list_to_pick))
        {
            object item_weight = list_to_pick[item];
            if (Dm.eq(item_weight, 0))
                continue;

            total -= Dm.num(item_weight);
            if (total <= 0)
                return item;
        }

        return null;
    }

    /**
     * Like pick_weight, but allowing for nested lists.
     *
     * For example, given the following list:
     * list(A = 1, list(B = 1, C = 1))
     * A would have a 50% chance of being picked,
     * and list(B, C) would have a 50% chance of being picked.
     * If list(B, C) was picked, B and C would then each have a 50% chance of being picked.
     * So the final probabilities would be 50% for A, 25% for B, and 25% for C.
     *
     * Weights should be integers. Entries without weights are assigned weight 1 (so unweighted lists can be used as well)
     */
    // tg: code/__HELPERS/_lists.dm:543
    public static object pick_weight_recursive(DmList list_to_pick = null)
    {
        object result = pick_weight(fill_with_ones(list_to_pick) as DmList);
        while (islist(result))
            result = pick_weight(fill_with_ones(result) as DmList);
        return result;
    }

    /**
    * Like pick_weight, but decreases the value of the picked element by 1
     * For example, given the following list:
     * A = 6, B = 3, C = 1, D = 0
     * A would have a 60% chance of being picked, after which it would decrease by one and the new list would be
     * A = 5, B = 3, C = 1, D = 0
     * Tt would then have a 55.55...% to be picked, rinse and repeat
    */
    // tg: code/__HELPERS/_lists.dm:557
    public static object pick_weight_take(DmList list_to_pick = null)
    {
        object dot = null;
        dot = pick_weight(list_to_pick);
        Dm.dec_at(list_to_pick, dot);
        return dot;
    }

    /**
    * Picks n items from a list. The same index will not be chosen more than once.
    * e.g. pick_n(list_of_stuff, 10) would return a list of 10 items from the list, chosen randomly.
    */
    // tg: code/__HELPERS/_lists.dm:565
    public static object pick_n(DmList list_to_pick = null, object n = null)
    {
        float list_to_pick_length = 0;
        if (islist(list_to_pick))
            list_to_pick_length = length(list_to_pick);

        if (!Dm.truthy(list_to_pick_length) || Dm.le(n, 0))
            return Dm.list();

        /// length of our list_to_pick
        n = min(n, list_to_pick_length);

        // Shuffle and slice the first n indices
        DmList copy_to_shuffle = list_to_pick.Copy();
        shuffle_inplace(copy_to_shuffle);

        return copy_to_shuffle.Copy(1, Dm.num(Dm.add(n, 1)));
    }

    /**
     * Given a list, return a copy where values without defined weights are given weight 1.
     * For example, fill_with_ones(list(A, B=2, C)) = list(A=1, B=2, C=1)
     * Useful for weighted random choices (loot tables, syllables in languages, etc.)
     */
    // tg: code/__HELPERS/_lists.dm:587
    public static object fill_with_ones(object list_to_pad = null)
    {
        if (!islist(list_to_pad))
            return list_to_pad;

        DmList final_list = Dm.list();

        foreach (object key in Dm.each(list_to_pad))
        {
            if (Dm.truthy(((DmList)list_to_pad)[key]))
                final_list[key] = ((DmList)list_to_pad)[key];
            else
                final_list[key] = 1;
        }

        return final_list;
    }

    /// Takes a weighted list (see above) and expands it into raw entries
    /// This eats more memory, but saves time when actually picking from it
    // tg: code/__HELPERS/_lists.dm:603
    public static object expand_weights(DmList list_to_pick = null)
    {
        DmList values = Dm.list();
        foreach (object item in Dm.each(list_to_pick))
        {
            object value = list_to_pick[item];
            if (!Dm.truthy(value))
                continue;
            Dm.append(ref values, value);
        }

        object gcf = greatest_common_factor(values);

        DmList output = Dm.list();
        foreach (object item in Dm.each(list_to_pick))
        {
            object value = list_to_pick[item];
            if (!Dm.truthy(value))
                continue;
            for (float i = 1, i__end = Dm.num(Dm.div(value, gcf)); i <= i__end; i++)
                UNTYPED_LIST_ADD(ref output, item);
        }
        return output;
    }

    /// Takes a list of numbers as input, returns the highest value that is cleanly divides them all
    /// Note: this implementation is expensive as heck for large numbers, I only use it because most of my usecase
    /// Is < 10 ints
    // tg: code/__HELPERS/_lists.dm:625
    public static object greatest_common_factor(DmList values = null)
    {
        object smallest = min(Dm.arglist(values));
        for (float i = Dm.num(smallest), i__end = 1; i >= i__end; i += -1)
        {
            float safe = TRUE;
            foreach (object entry in Dm.each(values))
            {
                if (Dm.mod(entry, i) != 0)
                {
                    safe = FALSE;
                    break;
                }
            }
            if (Dm.truthy(safe))
                return i;
        }
        return null;
    }

    /// Pick a random element from the list and remove it from the list.
    // tg: code/__HELPERS/_lists.dm:637  (RETURN_TYPE(list_to_pick[_].type): dropped, rules 7.9)
    public static object pick_n_take(DmList list_to_pick = null)
    {
        object dot = null;
        if (Dm.truthy(list_to_pick.len))
        {
            float picked = rand(1, list_to_pick.len);
            dot = list_to_pick[picked];
            list_to_pick.Cut(picked, picked + 1); //Cut is far more efficient that Remove()
        }
        return dot;
    }

    ///Returns the top(last) element from the list and removes it from the list (typical stack function)
    // tg: code/__HELPERS/_lists.dm:645
    public static object pop(DmList L = null)
    {
        object dot = null;
        if (Dm.truthy(L.len))
        {
            dot = L[L.len];
            L.len--;
        }
        return dot;
    }

    /// Returns the top (last) element from the list, does not remove it from the list. Stack functionality.
    // tg: code/__HELPERS/_lists.dm:651
    public static object peek(DmList target_list = null)
    {
        float list_length = length(target_list);
        if (list_length != 0)
            return target_list[list_length];
        return null;
    }

    // tg: code/__HELPERS/_lists.dm:656
    public static object popleft(DmList L = null)
    {
        object dot = null;
        if (Dm.truthy(L.len))
        {
            dot = L[1];
            L.Cut(1, 2);
        }
        return dot;
    }

    // tg: code/__HELPERS/_lists.dm:661
    public static void sorted_insert(DmList L = null, object thing = null, object comparator = null)
    {
        float pos = L.len;
        while (pos > 0 && Dm.gt(Waiting.NoSleep(Dm.Call(null, (string)comparator, thing, L[pos])), 0))
            pos--;
        L.Insert(pos + 1, thing);
    }

    /// Returns the next item in a list
    // tg: code/__HELPERS/_lists.dm:668
    public static object next_list_item(object item = null, DmList inserted_list = null)
    {
        float i = 0;
        i = inserted_list.Find(item);
        if (i == inserted_list.len)
            i = 1;
        else
            i++;
        return inserted_list[i];
    }

    /// Returns the previous item in a list
    // tg: code/__HELPERS/_lists.dm:678
    public static object previous_list_item(object item = null, DmList inserted_list = null)
    {
        float i = 0;
        i = inserted_list.Find(item);
        if (i == 1)
            i = inserted_list.len;
        else
            i--;
        return inserted_list[i];
    }

    ///Randomize: Return the list in a random order
    // tg: code/__HELPERS/_lists.dm:688
    public static object shuffle(DmList inserted_list = null)
    {
        if (!Dm.truthy(inserted_list))
            return null;
        inserted_list = inserted_list.Copy();

        for (float i = 1, i__end = inserted_list.len - 1; i <= i__end; i++)
            inserted_list.Swap(i, rand(i, inserted_list.len));

        return inserted_list;
    }

    ///same as shuffle, but acts on list in place, and returns same list
    // tg: code/__HELPERS/_lists.dm:699
    public static object shuffle_inplace(DmList inserted_list = null)
    {
        if (!Dm.truthy(inserted_list))
            return null;

        for (float i = 1, i__end = inserted_list.len - 1; i <= i__end; i++)
            inserted_list.Swap(i, rand(i, inserted_list.len));

        return inserted_list;
    }

    ///Return a list with no duplicate entries
    // tg: code/__HELPERS/_lists.dm:709
    public static object unique_list(DmList inserted_list = null)
    {
        DmList dot = null;
        dot = Dm.list();
        foreach (object i in Dm.each(inserted_list))
            Dm.combine(ref dot, LIST_VALUE_WRAP_LISTS(i));
        return dot;
    }

    ///same as unique_list, but returns nothing and acts on list in place (also handles associated values properly)
    // tg: code/__HELPERS/_lists.dm:715
    public static void unique_list_in_place(DmList inserted_list = null)
    {
        object temp = inserted_list.Copy();
        inserted_list.len = 0;
        foreach (object key in Dm.each(temp))
        {
            if (isnum(key))
                Dm.combine(ref inserted_list, key);
            else
                inserted_list[key] = ((DmList)temp)[key];
        }
    }

    ///for sorting clients or mobs by ckey
    // tg: code/__HELPERS/_lists.dm:725
    public static object sort_key(DmList ckey_list = null, float? order = null)
    {
        order ??= 1;
        return sortTim(ckey_list, (Dm.num(order) >= 0 ? "/proc/" + nameof(GlobalProcs.cmp_ckey_asc) : "/proc/" + nameof(GlobalProcs.cmp_ckey_dsc)));
    }

    ///Specifically for record datums in a list.
    // tg: code/__HELPERS/_lists.dm:729
    public static object sort_record(DmList record_list = null, float? order = null)
    {
        order ??= 1;
        return sortTim(record_list, (Dm.num(order) >= 0 ? "/proc/" + nameof(GlobalProcs.cmp_records_asc) : "/proc/" + nameof(GlobalProcs.cmp_records_dsc)));
    }

    ///sort any value in a list
    // tg: code/__HELPERS/_lists.dm:733
    public static object sort_list(DmList list_to_sort = null, string cmp = null)
    {
        cmp ??= "/proc/" + nameof(GlobalProcs.cmp_text_asc);
        return sortTim(list_to_sort.Copy(), cmp);
    }

    ///uses sort_list() but uses the var's name specifically. This should probably be using mergeAtom() instead
    // tg: code/__HELPERS/_lists.dm:737
    public static object sort_names(DmList list_to_sort = null, float? order = null)
    {
        order ??= 1;
        return sortTim(list_to_sort.Copy(), (Dm.num(order) >= 0 ? "/proc/" + nameof(GlobalProcs.cmp_name_asc) : "/proc/" + nameof(GlobalProcs.cmp_name_dsc)));
    }

    ///Converts a bitfield to a list of numbers (or words if a wordlist is provided)
    // tg: code/__HELPERS/_lists.dm:741
    public static object bitfield_to_list(float? bitfield = null, DmList wordlist = null)
    {
        bitfield ??= 0;
        DmList return_list = Dm.list();
        if (islist(wordlist))
        {
            float max = min(wordlist.len, 24);
            float bit = 1;
            for (float i = 1, i__end = max; i <= i__end; i++)
            {
                if (Dm.truthy(Dm.bitand(Dm.num(bitfield), bit)))
                    Dm.append(ref return_list, wordlist[i]);
                bit = Dm.shl(bit, 1);
            }
        }
        else
        {
            for (float bit_number = 0, bit_number__end = 23; bit_number <= bit_number__end; bit_number++)
            {
                float bit = Dm.shl(1, bit_number);
                if (Dm.truthy(Dm.bitand(Dm.num(bitfield), bit)))
                    Dm.append(ref return_list, bit);
            }
        }

        return return_list;
    }

    // tg: code/__HELPERS/_lists.dm:759
    /// Returns the key based on the index
    public static object KEYBYINDEX(object L, float index)
    {
        return (((index <= length(L)) && (index > 0)) ? ((DmList)L)[index] : null);
    }

    ///return the amount of items of the same type inside a list
    // tg: code/__HELPERS/_lists.dm:762
    public static object count_by_type(DmList inserted_list = null, object type = null)
    {
        float i = 0;
        foreach (object item_type in Dm.each(inserted_list))
        {
            if (istype(item_type, type as Type))
                i++;
        }
        return i;
    }

    /**
     * Returns the first record in the list that matches the name
     *
     * If locked_only is TRUE, locked records will be checked
     *
     * If locked_only is FALSE, crew records will be checked
     *
     * If no record is found, returns null
     */
    // tg: code/__HELPERS/_lists.dm:778  (`as /datum/record`: a return type hint, dropped like RETURN_TYPE, rules 7.9)
    public static object find_record(object value = null, float? locked_only = null)
    {
        locked_only ??= FALSE;
        if (Dm.truthy(locked_only))
        {
            foreach (datum_record_locked target in Dm.each<datum_record_locked>(GLOB.manifest.locked))
            {
                if (!Dm.eq(target.name, value))
                    continue;
                return target;
            }
            return null;
        }

        foreach (datum_record_crew target in Dm.each<datum_record_crew>(GLOB.manifest.general))
        {
            if (!Dm.eq(target.name, value))
                continue;
            return target;
        }
        return null;
    }

    /**
     * Move a single element from position from_index within a list, to position to_index
     * All elements in the range [1,to_index) before the move will be before the pivot afterwards
     * All elements in the range [to_index, L.len+1) before the move will be after the pivot afterwards
     * In other words, it's as if the range [from_index,to_index) have been rotated using a <<< operation common to other languages.
     * from_index and to_index must be in the range [1,L.len+1]
     * This will preserve associations ~Carnie
    **/
    // tg: code/__HELPERS/_lists.dm:801
    public static void move_element(DmList inserted_list = null, object from_index = null, object to_index = null)
    {
        if (Dm.eq(from_index, to_index) || Dm.eq(Dm.add(from_index, 1), to_index)) //no need to move
            return;
        if (Dm.gt(from_index, to_index))
            Dm.inc(ref from_index); //since a null will be inserted before from_index, the index needs to be nudged right by one

        inserted_list.Insert(Dm.num(to_index), null);
        inserted_list.Swap(Dm.num(from_index), Dm.num(to_index));
        inserted_list.Cut(Dm.num(from_index), Dm.num(Dm.add(from_index, 1)));
    }

    /**
     * Move elements [from_index,from_index+len) to [to_index-len, to_index)
     * Same as moveElement but for ranges of elements
     * This will preserve associations ~Carnie
    **/
    // tg: code/__HELPERS/_lists.dm:817
    public static void move_range(DmList inserted_list = null, object from_index = null, object to_index = null, float? len = null)
    {
        len ??= 1;
        float distance = abs(Dm.num(Dm.sub(to_index, from_index)));
        if (Dm.num(len) >= distance) //there are more elements to be moved than the distance to be moved. Therefore the same result can be achieved (with fewer operations) by moving elements between where we are and where we are going. The result being, our range we are moving is shifted left or right by dist elements
        {
            if (Dm.le(from_index, to_index))
                return; //no need to move
            Dm.append(ref from_index, len); //we want to shift left instead of right

            for (float i = 1, i__end = distance; i <= i__end; i++)
            {
                inserted_list.Insert(Dm.num(from_index), null);
                inserted_list.Swap(Dm.num(from_index), Dm.num(to_index));
                inserted_list.Cut(Dm.num(to_index), Dm.num(Dm.add(to_index, 1)));
            }
        }
        else
        {
            if (Dm.gt(from_index, to_index))
                Dm.append(ref from_index, len);

            for (float i = 1, i__end = Dm.num(len); i <= i__end; i++)
            {
                inserted_list.Insert(Dm.num(to_index), null);
                inserted_list.Swap(Dm.num(from_index), Dm.num(to_index));
                inserted_list.Cut(Dm.num(from_index), Dm.num(Dm.add(from_index, 1)));
            }
        }
    }

    ///Move elements from [from_index, from_index+len) to [to_index, to_index+len)
    ///Move any elements being overwritten by the move to the now-empty elements, preserving order
    ///Note: if the two ranges overlap, only the destination order will be preserved fully, since some elements will be within both ranges ~Carnie
    // tg: code/__HELPERS/_lists.dm:840
    public static void swap_range(DmList inserted_list = null, object from_index = null, object to_index = null, float? len = null)
    {
        len ??= 1;
        float distance = abs(Dm.num(Dm.sub(to_index, from_index)));
        if (Dm.num(len) > distance) //there is an overlap, therefore swapping each element will require more swaps than inserting new elements
        {
            if (Dm.lt(from_index, to_index))
                Dm.append(ref to_index, len);
            else
                Dm.append(ref from_index, len);

            for (float i = 1, i__end = distance; i <= i__end; i++)
            {
                inserted_list.Insert(Dm.num(from_index), null);
                inserted_list.Swap(Dm.num(from_index), Dm.num(to_index));
                inserted_list.Cut(Dm.num(to_index), Dm.num(Dm.add(to_index, 1)));
            }
        }
        else
        {
            if (Dm.gt(to_index, from_index))
            {
                object temp = to_index;
                to_index = from_index;
                from_index = temp;
            }

            for (float i = 1, i__end = Dm.num(len); i <= i__end; i++)
                inserted_list.Swap(Dm.postinc(ref from_index), Dm.postinc(ref to_index));
        }
    }

    ///replaces reverseList ~Carnie
    // tg: code/__HELPERS/_lists.dm:862
    public static object reverse_range(DmList inserted_list = null, float? start = null, float? end = null)
    {
        start ??= 1;
        end ??= 0;
        if (Dm.truthy(inserted_list.len))
        {
            start = Dm.mod(Dm.num(start), inserted_list.len);
            end = Dm.mod(Dm.num(end), inserted_list.len + 1);
            if (Dm.num(start) <= 0)
                Dm.append(ref start, inserted_list.len);
            if (Dm.num(end) <= 0)
                Dm.append(ref end, inserted_list.len + 1);

            Dm.dec(ref end);
            while (Dm.num(start) < Dm.num(end))
                inserted_list.Swap(Dm.postinc(ref start), Dm.postdec(ref end));
        }

        return inserted_list;
    }

    ///return first thing in L which has var/varname == value
    ///this is typecaste as list/L, but you could actually feed it an atom instead.
    ///completely safe to use
    // tg: code/__HELPERS/_lists.dm:881
    public static object get_element_by_var(DmList inserted_list = null, object varname = null, object value = null)
    {
        Dm.NotTranslated("code/__HELPERS/_lists.dm:881", "get_element_by_var: reads checked_datum.vars; the vars reflection is not translated (rules 10.7)");
        return default;
    }

    ///Copies a list, and all lists inside it recusively
    ///Does not copy any other reference type
    // tg: code/__HELPERS/_lists.dm:892
    public static object deep_copy_list(object inserted_list = null)
    {
        DmList dot = null;
        if (!islist(inserted_list))
            return inserted_list;
        dot = ((DmList)inserted_list).Copy();
        for (float i = 1, i__end = ((DmList)inserted_list).len; i <= i__end; i++)
        {
            object key = dot[i];
            if (isnum(key))
                // numbers cannot ever be associative keys
                continue;
            object value = dot[key];
            if (islist(value))
            {
                value = deep_copy_list(value);
                dot[key] = value;
            }
            if (islist(key))
            {
                key = deep_copy_list(key);
                dot[i] = key;
                dot[key] = value;
            }
        }
        return dot;
    }

    /// A version of deep_copy_list that actually supports associative list nesting: list(list(list("a" = "b"))) will actually copy correctly.
    // tg: code/__HELPERS/_lists.dm:911
    public static object deep_copy_list_alt(object inserted_list = null)
    {
        object dot = null;
        if (!islist(inserted_list))
            return inserted_list;
        object copied_list = ((DmList)inserted_list).Copy();
        dot = copied_list;
        foreach (object key_or_value in Dm.each(inserted_list))
        {
            if (isnum(key_or_value) || !Dm.truthy(((DmList)inserted_list)[key_or_value]))
                continue;
            object value = ((DmList)inserted_list)[key_or_value];
            object new_value = value;
            if (islist(value))
                new_value = deep_copy_list_alt(value);
            ((DmList)copied_list)[key_or_value] = new_value;
        }
        return dot;
    }

    ///takes an input_key, as text, and the list of keys already used, outputting a replacement key in the format of "[input_key] ([number_of_duplicates])" if it finds a duplicate
    ///use this for lists of things that might have the same name, like mobs or objects, that you plan on giving to a player as input
    // tg: code/__HELPERS/_lists.dm:927
    public static object avoid_assoc_duplicate_keys(object input_key = null, DmList used_key_list = null)
    {
        if (!Dm.truthy(input_key) || !(used_key_list is DmList))
            return null;
        if (Dm.truthy(used_key_list[input_key]))
        {
            Dm.inc_at(used_key_list, input_key);
            input_key = Dm.text("[] ([])", input_key, used_key_list[input_key]);
        }
        else
        {
            used_key_list[input_key] = 1;
        }
        return input_key;
    }

    ///Make a normal list an associative one
    // tg: code/__HELPERS/_lists.dm:938
    public static object make_associative(DmList flat_list = null)
    {
        DmList dot = null;
        dot = Dm.list();
        foreach (object thing in Dm.each(flat_list))
            dot[thing] = TRUE;
        return dot;
    }

    // tg: code/__HELPERS/_lists.dm:944
    ///Picks from the list, with some safeties, and returns the "default" arg if it fails
    public static object DEFAULTPICK(object L, object @default)
    {
        return ((islist(L) && Dm.truthy(length(L))) ? pick(L) : @default);
    }

    /* Definining a counter as a series of key -> numeric value entries

     * All these procs modify in place.
    */

    // tg: code/__HELPERS/_lists.dm:951
    public static object counterlist_scale(DmList L = null, object scalar = null)
    {
        DmList dot = null;
        DmList @out = Dm.list();
        foreach (object key in Dm.each(L))
            @out[key] = Dm.mul(L[key], scalar);
        dot = @out;
        return dot;
    }

    // tg: code/__HELPERS/_lists.dm:957
    public static object counterlist_sum(DmList L = null)
    {
        object dot = null;
        dot = 0;
        foreach (object key in Dm.each(L))
            Dm.append(ref dot, L[key]);
        return dot;
    }

    // tg: code/__HELPERS/_lists.dm:962
    public static object counterlist_normalise(DmList L = null)
    {
        object dot = null;
        object avg = counterlist_sum(L);
        if (!Dm.eq(avg, 0))
            dot = counterlist_scale(L, Dm.div(1, avg));
        else
            dot = L;
        return dot;
    }

    // tg: code/__HELPERS/_lists.dm:969
    public static void counterlist_combine(DmList L1 = null, DmList L2 = null)
    {
        foreach (object key in Dm.each(L2))
        {
            object other_value = L2[key];
            if (Dm.@in(key, L1))
                Dm.append_at(L1, key, other_value);
            else
                L1[key] = other_value;
        }
    }

    /// Turns an associative list into a flat list of keys
    // tg: code/__HELPERS/_lists.dm:978
    public static object assoc_to_keys(DmList input = null)
    {
        DmList keys = Dm.list();
        foreach (object key in Dm.each(input))
            UNTYPED_LIST_ADD(ref keys, key);
        return keys;
    }

    /// Turns an associative list into a flat list of keys, but for sprite accessories, respecting the locked variable
    // tg: code/__HELPERS/_lists.dm:985
    public static object assoc_to_keys_features(DmList input = null)
    {
        DmList keys = Dm.list();
        foreach (object key in Dm.each(input))
        {
            datum_sprite_accessory value = (datum_sprite_accessory)input[key];
            if (Dm.truthy(value?.locked))
                continue;
            UNTYPED_LIST_ADD(ref keys, key);
        }
        return keys;
    }

    /// Turns an associative list into a flat list of values
    // tg: code/__HELPERS/_lists.dm:995
    public static object assoc_to_values(DmList key_list = null)
    {
        DmList dot = null;
        if (!islist(key_list))
            return null;
        dot = Dm.list();
        foreach (object key in Dm.each(key_list))
            Dm.combine(ref dot, LIST_VALUE_WRAP_LISTS(key_list[key]));
        return dot;
    }

    ///compare two lists, returns TRUE if they are the same
    // tg: code/__HELPERS/_lists.dm:1003
    public static object compare_list(DmList l = null, DmList d = null)
    {
        if (!islist(l) || !islist(d))
            return FALSE;

        if (l.len != d.len)
            return FALSE;

        for (float i = 1, i__end = l.len; i <= i__end; i++)
        {
            if (!Dm.eq(l[i], d[i]))
                return FALSE;
        }

        return TRUE;
    }

    // tg: code/__HELPERS/_lists.dm:1016
    public static object LAZY_LISTS_OR(object left_list, object right_list)
    {
        return (Dm.truthy(length(left_list))
            ? (Dm.truthy(length(right_list))
                ? Dm.bitor(left_list, right_list)
                : (object)((DmList)left_list).Copy())
            : (Dm.truthy(length(right_list))
                ? ((DmList)right_list).Copy()
                : null));
    }

    ///Returns a list with items filtered from a list that can call callback
    // tg: code/__HELPERS/_lists.dm:1027
    public static async DmTask<object> special_list_filter(DmList list_to_filter = null, datum_callback condition = null)
    {
        DmList dot = null;
        if (!islist(list_to_filter) || !Dm.truthy(length(list_to_filter)) || !(condition is datum_callback))
            return Dm.list();
        dot = Dm.list();
        foreach (object i in Dm.each(list_to_filter))
        {
            if (Dm.truthy(await condition.Invoke(i)))
                Dm.combine(ref dot, LIST_VALUE_WRAP_LISTS(i));
        }
        return dot;
    }

    ///Returns a list with all weakrefs resolved
    // tg: code/__HELPERS/_lists.dm:1036
    public static object recursive_list_resolve(DmList list_to_resolve = null)
    {
        DmList dot = null;
        dot = Dm.list();
        foreach (object element in Dm.each(list_to_resolve))
        {
            if (istext(element))
            {
                Dm.append(ref dot, element);
                object possible_assoc_value = list_to_resolve[element];
                if (Dm.truthy(possible_assoc_value))
                    dot[element] = recursive_list_resolve_element(possible_assoc_value);
            }
            else
            {
                Dm.append(ref dot, Dm.list(recursive_list_resolve_element(element)));
            }
        }
        return dot;
    }

    ///Helper for recursive_list_resolve()
    // tg: code/__HELPERS/_lists.dm:1048
    public static object recursive_list_resolve_element(object element = null)
    {
        if (islist(element))
        {
            DmList inner_list = element as DmList;
            return recursive_list_resolve(inner_list);
        }
        else if (isweakref(element))
        {
            datum_weakref @ref = element as datum_weakref;
            return @ref.resolve();
        }
        else
        {
            return element;
        }
    }

    /**
     * Intermediate step for preparing lists to be passed into the lua editor tgui.
     * Resolves weakrefs, converts some values without a standard textual representation to text,
     * and can handle self-referential lists and potential duplicate output keys.
     */
    // tg: code/__HELPERS/_lists.dm:1063
    public static object prepare_lua_editor_list(DmList target_list = null, DmList visited = null)
    {
        Dm.NotTranslated("code/__HELPERS/_lists.dm:1063", "prepare_lua_editor_list: a helper of the Lua editor only (modules/admin/verbs/lua; the DREAMLUAU admin scripting is not ported, rules 1.7, 13); it also needs CHECK_TICK (the MC) and compares ref() of any value, which the port's ref() rejects for text and numbers (object-model.md section 3)");
        return default;
    }

    /**
     * Converts a list into a list of assoc lists of the form ("key" = key, "value" = value)
     * so that list keys that are themselves lists can be fully json-encoded
     * and that unique objects with the same string representation do not
     * produce duplicate keys that are clobbered by the standard JavaScript JSON.parse function
     */
    // tg: code/__HELPERS/_lists.dm:1120
    public static object kvpify_list(DmList target_list = null, float? depth = null, DmList visited = null)
    {
        Dm.NotTranslated("code/__HELPERS/_lists.dm:1120", "kvpify_list: a helper of the Lua editor only (modules/admin/verbs/lua, not ported: rules 1.7, 13); it also needs CHECK_TICK (__DEFINES/_tick.dm, the MC of the controller wave)");
        return default;
    }

    /// Compares 2 lists, returns TRUE if they are the same
    // tg: code/__HELPERS/_lists.dm:1150
    public static object deep_compare_list(DmList list_1 = null, DmList list_2 = null)
    {
        if (list_1 == list_2)
            return TRUE;

        if (!islist(list_1) || !islist(list_2))
            return FALSE;

        if (list_1.len != list_2.len)
            return FALSE;

        for (float i = 1, i__end = list_1.len; i <= i__end; i++)
        {
            object key_1 = list_1[i];
            object key_2 = list_2[i];
            if (islist(key_1) && islist(key_2))
            {
                if (!Dm.truthy(deep_compare_list(key_1 as DmList, key_2 as DmList)))
                    return FALSE;
            }
            else if (!Dm.eq(key_1, key_2))
            {
                return FALSE;
            }
            if (istext(key_1) || islist(key_1) || ispath(key_1) || isdatum(key_1) || Dm.eq(key_1, world))
            {
                object value_1 = list_1[key_1];
                object value_2 = list_2[key_1];
                if (islist(value_1) && islist(value_2))
                {
                    if (!Dm.truthy(deep_compare_list(value_1 as DmList, value_2 as DmList)))
                        return FALSE;
                }
                else if (!Dm.eq(value_1, value_2))
                {
                    return FALSE;
                }
            }
        }
        return TRUE;
    }

    /// Returns a copy of the list where any element that is a datum is converted into a weakref
    // tg: code/__HELPERS/_lists.dm:1179
    public static object weakrefify_list(DmList target_list = null, DmList visited = null)
    {
        Dm.NotTranslated("code/__HELPERS/_lists.dm:1179", "weakrefify_list: a helper of the Lua state only (modules/admin/verbs/lua, not ported: rules 1.7, 13); it also needs CHECK_TICK (the MC of the controller wave)");
        return default;
    }

    /// Runtimes if the passed in list is not sorted
    // tg: code/__HELPERS/_lists.dm:1213
    public static void assert_sorted(DmList list = null, object name = null, string cmp = null)
    {
        cmp ??= "/proc/" + nameof(GlobalProcs.cmp_numeric_asc);
        object last_value = list[1];

        for (float index = 2, index__end = list.len; index <= index__end; index++)
        {
            object value = list[index];

            if (Dm.lt(Waiting.NoSleep(Dm.Call(null, cmp, value, last_value)), 0))
                stack_trace(Dm.text("[] is not sorted. value at [] ([]) is in the wrong place compared to the previous value of [] (when compared to by [])", name, index, value, last_value, cmp), "code/__HELPERS/_lists.dm", 1220);

            last_value = value;
        }
    }

    /**
     * Converts a list of coordinates, or an assosciative list if passed, into a turf by calling locate(x, y, z) based on the values in the list
     */
    // tg: code/__HELPERS/_lists.dm:1227
    public static object coords2turf(DmList coords = null)
    {
        Dm.NotTranslated("code/__HELPERS/_lists.dm:1227", "coords2turf: needs locate(x, y, z), a Builtins member of the atom wave (planned, rules Appendix C.1)");
        return default;
    }

    /**
     * Given a list and a list of its variant hints, appends variants that aren't explicitly required by dreamluau,
     * but are required by the lua editor tgui.
     */
    // tg: code/__HELPERS/_lists.dm:1236
    public static void add_lua_editor_variants(DmList values = null, DmList variants = null, DmList visited = null, string path = null)
    {
        Dm.NotTranslated("code/__HELPERS/_lists.dm:1236", "add_lua_editor_variants: a helper of the Lua state only (modules/admin/verbs/lua, not ported: rules 1.7, 13); it also needs CHECK_TICK (the MC) and compares ref() of any value, which the port's ref() rejects for text and numbers (object-model.md section 3)");
    }

    // tg: code/__HELPERS/_lists.dm:1286
    public static void add_lua_return_value_variants(DmList values = null, DmList variants = null)
    {
        Dm.NotTranslated("code/__HELPERS/_lists.dm:1286", "add_lua_return_value_variants: a helper of the Lua state only (modules/admin/verbs/lua, not ported: rules 1.7, 13); it compares ref() of any value, which the port's ref() rejects for text and numbers (object-model.md section 3)");
    }

    // tg: code/__HELPERS/_lists.dm:1298
    public static object deep_copy_without_cycles(object values = null, DmList visited = null)
    {
        Dm.NotTranslated("code/__HELPERS/_lists.dm:1298", "deep_copy_without_cycles: a helper of the Lua editor only (modules/admin/verbs/lua, not ported: rules 1.7, 13); it also needs CHECK_TICK (__DEFINES/_tick.dm, the MC of the controller wave)");
        return default;
    }

    /**
     * Given a list and a list of its variant hints, removes any list key/values that are represent lua values that could not be directly converted to DM.
     */
    // tg: code/__HELPERS/_lists.dm:1341
    public static void remove_non_dm_variants(DmList return_values = null, DmList variants = null, DmList visited = null)
    {
        if (!islist(visited))
            visited = Dm.list();
        if (!islist(return_values) || !islist(variants) || Dm.truthy(visited[return_values]))
            return;
        visited[return_values] = TRUE;
        if (return_values.len != variants.len)
            throw Dm.CRASH("return_values and variants must be the same length", "code/__HELPERS/_lists.dm", 1348);
        for (float i = 1, i__end = variants.len; i <= i__end; i++)
        {
            object pair = variants[i];
            if (!islist(variants))
                continue;
            object key = return_values[i];
            if (Dm.truthy(((DmList)pair)["key"]))
            {
                if (!islist(((DmList)pair)["key"]))
                {
                    return_values[i] = null;
                    continue;
                }
                remove_non_dm_variants(key as DmList, ((DmList)pair)["key"] as DmList, visited);
            }
            if (Dm.truthy(((DmList)pair)["value"]))
            {
                if (!islist(((DmList)pair)["value"]))
                {
                    return_values[key] = null;
                    continue;
                }
                remove_non_dm_variants(return_values[key] as DmList, ((DmList)pair)["value"] as DmList, visited);
            }
        }
    }

    // tg: code/__HELPERS/_lists.dm:1365
    public static object compare_lua_logs(DmList log_1 = null, DmList log_2 = null)
    {
        if (log_1 == log_2)
            return TRUE;
        foreach (object field in Dm.each(Dm.list("status", "name", "message", "chunk")))
        {
            if (!Dm.eq(log_1[field], log_2[field]))
                return FALSE;
        }
        object sw__1 = log_1["status"];
        if (Dm.eq(sw__1, "finished") || Dm.eq(sw__1, "yield"))
        {
            return (deep_compare_list(
                    recursive_list_resolve(log_1["return_values"] as DmList) as DmList,
                    recursive_list_resolve(log_2["return_values"] as DmList) as DmList
                    ) is var and__1 && !Dm.truthy(and__1) ? and__1 : deep_compare_list(log_1["variants"] as DmList, log_2["variants"] as DmList));
        }
        else if (Dm.eq(sw__1, "runtime"))
        {
            return (Dm.num(Dm.eq(log_1["file"], log_2["file"])
                && Dm.eq(log_1["line"], log_2["line"])) is var and__2 && !Dm.truthy(and__2) ? (object)and__2 : deep_compare_list(log_1["stack"] as DmList, log_2["stack"] as DmList));
        }
        else
        {
            return TRUE;
        }
    }

    /**
     * Similar to pick_weight_recursive, except without the weight part, meaning it should hopefully not take
     * up as much computing power for things that don't +need+ weights.
     * * * Able to handle cases such as:
     * * pick_recursive(list(a), list(b), list(c))
     * * pick_recursive(list(list(a), list(b)))
     * * pick_recursive(a, list(b), list(list(c), list(d)))
     * * pick_recusrive(list(a, b, c), d, e)
     * Really any combination of lists & vars, as long as the passed lists aren't empty
     */
    // tg: code/__HELPERS/_lists.dm:1395
    public static object pick_recursive(params object[] __extra)
    {
        DmList args = Dm.args(__extra);
        object result = pick(args);
        while (islist(result))
            result = pick(result);
        return result;
    }

    /** Takes in two weighted lists and outputs a third list containing the elements of both inputs with their weights blended according to a given proportion.
     * Not exact and may have rounding errors, will round to nearest 1/1000.
     * */
    // tg: code/__HELPERS/_lists.dm:1404
    public static object blend_weighted_lists(DmList listA = null, DmList listB = null, object blend = null)
    {
        DmList joined_list = Dm.bitor(listA, listB) as DmList;

        listA = counterlist_normalise(listA) as DmList;
        listB = counterlist_normalise(listB) as DmList;

        foreach (object element in Dm.each(joined_list))
            joined_list[element] = round(Dm.num(Dm.mul(Dm.add(Dm.mul(listA[element], Dm.sub(1, blend)), Dm.mul(listB[element], blend)), 1000)));

        return joined_list;
    }
}
