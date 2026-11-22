// Translated from tgstation (snapshot 1d99bd7f): code/modules/library/book_info.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

///A datum which contains all the metadata of a book
// tg: code/modules/library/book_info.dm:8
public partial class datum_book_info : datum
{
    // tg: code/modules/library/book_info.dm:15
    [DmParams("_title", "_author", "_content")]
    public override async DmTask New(params object[] __extra)
    {
        object _title = Dm.arg(__extra, 0);
        object _author = Dm.arg(__extra, 1);
        object _content = Dm.arg(__extra, 2);
        await base.New(__extra);
        title = _title;
        author = _author;
        content = _content;
    }

    // tg: code/modules/library/book_info.dm:20
    public virtual void set_title(object _title = null, float? trusted = null) //Trusted should only be used for books read from the db, or in cases that we can be sure the info has already been sanitized
    {
        if (Dm.truthy(trusted))
        {
            title = _title;
            return;
        }
        title = reject_bad_text(trim(html_encode(_title), 30));
    }

    // tg: code/modules/library/book_info.dm:26
    public virtual object get_title(object @default = null) //Loads in an html decoded version of the title. Only use this for tgui menus, absolutely nothing else.
    {
        object decoded = html_decode(title);
        return Dm.truthy(decoded) ? decoded : "N/A";
    }

    // tg: code/modules/library/book_info.dm:29
    public virtual void set_author(object _author = null, float? trusted = null)
    {
        if (Dm.truthy(trusted))
        {
            author = _author;
            return;
        }
        author = trim(html_encode(_author), MAX_NAME_LEN);
    }

    // tg: code/modules/library/book_info.dm:35
    public virtual object get_author(object @default = null)
    {
        object decoded = html_decode(author);
        return Dm.truthy(decoded) ? decoded : "N/A";
    }

    // tg: code/modules/library/book_info.dm:38
    public virtual void set_content(object _content = null, float? trusted = null)
    {
        if (Dm.truthy(trusted))
        {
            content = _content;
            return;
        }
        content = trim(html_encode(trim(_content, MAX_PAPER_LENGTH)), MAX_BOOK_LENGTH);
    }

    // tg: code/modules/library/book_info.dm:44
    public virtual void set_content_using_paper(obj_item_paper paper = null)
    {
        // Just the paper's raw data. (gaps S3)
        string raw_content = "";
        foreach (datum_paper_input text_input in Dm.each<datum_paper_input>(paper.raw_text_inputs))
        {
            raw_content += Dm.str(text_input.to_raw_html());
            raw_content += Dm.eq(Dm.textchar(text_input.raw_text, length(text_input.raw_text)), "\n") ? "\n" : "\n\n";
        }

        // Paper raw inputs should already be trimmed to maximum paper length,
        // so we don't trim here again to avoid cutting into the expanded html.
        // We still trim to a more lenient length to avoid abuse.
        content = trim(html_encode(raw_content), MAX_BOOK_LENGTH) as string;
    }

    // tg: code/modules/library/book_info.dm:56
    public virtual object get_content(object @default = null)
    {
        object decoded = html_decode(content);
        return Dm.truthy(decoded) ? decoded : "N/A";
    }

    ///Returns a copy of the book_info datum
    // tg: code/modules/library/book_info.dm:60
    public virtual object return_copy()
    {
        return Dm.@new<datum_book_info>(title, author, content);
    }

    ///Modify an existing book_info datum to match your data
    // tg: code/modules/library/book_info.dm:65
    public virtual object copy_into(datum_book_info copycat = null)
    {
        copycat.set_title(title, trusted: TRUE);
        copycat.set_author(author, trusted: TRUE);
        copycat.set_content(content, trusted: TRUE);
        return copycat;
    }

    // tg: code/modules/library/book_info.dm:71
    public virtual object compare(datum_book_info cmp_with = null)
    {
        if (!Dm.eq(author, cmp_with.author))
            return FALSE;
        if (!Dm.eq(title, cmp_with.title))
            return FALSE;
        if (!Dm.eq(content, cmp_with.content))
            return FALSE;
        return TRUE;
    }
}
