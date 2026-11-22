// Translated from tgstation (snapshot 1d99bd7f): code/modules/library/random_books.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the random manual and get_random_manual (syndicate N2c, D316: a spent language manual turns into a random manual);
// gaps ST2 (D333): the random book, the random bookcases (fiction with its chuunibyou granter, non-fiction, religion, adult, reference
// with its random manuals, the wizard's with its cheese granter) and create_random_books. The books come from the library's database,
// which the port does not have (D76, M10+): as in tg without one, a random bookcase gets no database books and a random book read
// with none reads the database's apology. The var blocks are generated (D111).

// tg: code/modules/library/random_books.dm:1
public partial class obj_item_book_manual_random : obj_item_book_manual
{
    // tg: code/modules/library/random_books.dm:4
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        Type newtype = get_random_manual() as Type;
        Dm.@new(newtype, loc);
        return INITIALIZE_HINT_QDEL;
    }
}

public static partial class GlobalProcs
{
    /// Gets a random manuel book type excluding certain banned books
    // tg: code/modules/library/random_books.dm:11
    public static object get_random_manual()
    {
        DmList options = Dm.list();
        Dm.append(ref options, valid_subtypesof(typeof(obj_item_book_manual)));
        Dm.append(ref options, valid_subtypesof(typeof(obj_item_tgui_book_manual)));
        Dm.remove(ref options, typeof(obj_item_book_manual_random)); // no recursion
        Dm.remove(ref options, typeof(obj_item_book_manual_nuclear)); // why would nanotrasen stock instructions on how to nuke their own station?
        return pick(options);
    }
}

// tg: code/modules/library/random_books.dm:19
public partial class obj_item_book_random : obj_item_book
{
    // tg: code/modules/library/random_books.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        gen_random_icon_state();
        return dot;
    }

    // tg: code/modules/library/random_books.dm:30
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(random_loaded))
        {
            // Adult books are excluded unless explicitly set
            string loaded_category = random_category == BOOK_CATEGORY_RANDOM ? (string)pick(BOOK_CATEGORY_FICTION, BOOK_CATEGORY_NONFICTION, BOOK_CATEGORY_RELIGION, BOOK_CATEGORY_REFERENCE) : random_category;
            await create_random_books(amount: 1, location: loc as atom, fail_loud: TRUE, category: loaded_category, existing_book: this);
            random_loaded = TRUE;
        }
        return await base.attack_self(user, modifiers, __extra);
    }
}

// tg: code/modules/library/random_books.dm:38
public partial class obj_structure_bookcase_random : obj_structure_bookcase
{
    // tg: code/modules/library/random_books.dm:43
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(books_to_load) && isnum(books_to_load))
            books_to_load += Dm.num(pick(-1, -1, 0, 1, 1));
        update_appearance();
        return dot;
    }
}

public static partial class GlobalProcs
{
    /**
     * Create a random book or books.
     *
     * * amount: How many books to create.
     * * location: Where to create the books.
     * * fail_loud: If TRUE, will create a book with an error message if the database fails.
     * * category: The category of books to pick from.
     * If null or BOOK_CATEGORY_RANDOM, will pick from any category on a per-book basis.
     * * existing_book: If set, will use this book object instead of creating a new one.
     * Note passing any amount above 1 with an existing_book will still only create one book.
     */
    // tg: code/modules/library/random_books.dm:60
    public static async DmTask<object> create_random_books(float? amount = null, atom location = null, float? fail_loud = null, string category = null, obj_item_book existing_book = null)
    {
        amount ??= 1;
        fail_loud ??= FALSE;
        category ??= BOOK_CATEGORY_RANDOM;
        object dot = Dm.list();
        if (amount < 1)
            return dot;
        // if (!SSdbcore.Connect()): the port has no database (D76, M10+), so it never connects
        if (existing_book != null && (Dm.truthy(fail_loud) || prob(5)))
        {
            string error_text = I18n.T("There once was a book from Nantucket<br>But the database failed us, so f*$! it.<br>I tried to be good to you<br>Now this is an I.O.U<br>If you're feeling entitled, well, stuff it!<br><br><font color='gray'>~</font>");
            existing_book.book_data = Dm.@new<datum_book_info>(I18n.T("Strange Book"), "???", error_text);
        }
        // tg: :69-90  the query of the library table's random books (SSdbcore.NewQuery), each made a book named "Book: <title>" with
        // a random cover: unreachable without the database (M10+)
        await DmTask.CompletedTask;
        return dot;
    }
}

// tg: code/modules/library/random_books.dm:92
public partial class obj_structure_bookcase_random_fiction : obj_structure_bookcase_random
{
    ///have we spawned the chuuni granter
    // tg: code/modules/library/random_books.dm:96  var/static/chuuni_book_spawned = FALSE
    public static ref float chuuni_book_spawned => ref TgState.Current.obj_structure_bookcase_random_fiction__chuuni_book_spawned;

    // tg: code/modules/library/random_books.dm:98
    public override void after_random_load()
    {
        if (!Dm.truthy(chuuni_book_spawned) && Dm.truthy(is_station_level(z)))
        {
            chuuni_book_spawned = TRUE;
            Dm.@new<obj_item_book_granter_chuunibyou>(this);
        }
    }
}

// tg: code/modules/library/random_books.dm:115
public partial class obj_structure_bookcase_random_reference : obj_structure_bookcase_random
{
    // tg: code/modules/library/random_books.dm:121
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        while (books_to_load > 0 && prob(ref_book_prob))
        {
            books_to_load--;
            Dm.@new<obj_item_book_manual_random>(this);
        }
        return dot;
    }
}

// tg: code/modules/library/random_books.dm:127
public partial class obj_structure_bookcase_random_reference_wizard : obj_structure_bookcase_random_reference
{
    ///Whether this shelf has spawned a cheese granter
    // tg: code/modules/library/random_books.dm:130  var/static/cheese_granter_spawned = FALSE
    public static ref float cheese_granter_spawned => ref TgState.Current.obj_structure_bookcase_random_reference_wizard__cheese_granter_spawned;

    // tg: code/modules/library/random_books.dm:132
    public override void after_random_load()
    {
        if (Dm.truthy(cheese_granter_spawned))
            return;
        cheese_granter_spawned = TRUE;
        Dm.@new<obj_item_book_granter_action_spell_summon_cheese>(this);
        Dm.@new<obj_item_book_manual_ancient_parchment>(this);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/library/random_books.dm:96
    internal float obj_structure_bookcase_random_fiction__chuuni_book_spawned = FALSE;

    // tg: code/modules/library/random_books.dm:130
    internal float obj_structure_bookcase_random_reference_wizard__cheese_granter_spawned = FALSE;
}
