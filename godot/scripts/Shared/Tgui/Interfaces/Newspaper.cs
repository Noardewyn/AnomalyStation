using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Newspaper.tsx (silicons SI6b, D326: the printed
// newspaper): the previous and next page buttons, the title page with its table of contents, a channel's page with its stories, the
// last page's wanted issue, and a page's scribble. The stories' HTML is drawn as tg's processed text; the photos' images are not
// shown (the server renders no icons, D104). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Newspaper
{
    // tg: Newspaper.tsx:38
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> channels = data.Get("channels")?.Items ?? Array.Empty<UiValue>();
        double currentPage = data.Num("current_page");
        string scribble = data.Str("scribble_message");

        var content = new UiNode(UiKind.Box) { Key = "paper", Background = "#858387" }.With(
            new UiNode(UiKind.Section) { Key = "pages" }.With(
                new UiNode(UiKind.Button) { Key = "prev", Icon = "arrow-left", Width = "49%", Disabled = currentPage == 0, Text = "Previous Page", OnClick = () => c.Act("prev_page") },
                new UiNode(UiKind.Button) { Key = "next", Icon = "arrow-right", Width = "50%", Disabled = currentPage == channels.Count + 1, Text = "Next Page", OnClick = () => c.Act("next_page") }),
            currentPage == channels.Count + 1 ? Ending(data) : currentPage != 0 ? Channel(data) : Intro(data, channels));
        if (!string.IsNullOrEmpty(scribble))
            content.With(new UiNode(UiKind.Box) { Key = "scribble", Rule = true, Text = scribble });
        return c.Window(300, 400, null, content);
    }

    // tg: Newspaper.tsx:85
    private static UiNode Intro(UiValue data, IReadOnlyList<UiValue> channels)
    {
        var section = new UiNode(UiKind.Section) { Key = "intro" }.With(
            new UiNode(UiKind.Box) { Key = "title", Bold = true, FontSize = "30px", Text = "The Griffon" },
            new UiNode(UiKind.Box) { Key = "subtitle", Bold = true, FontSize = "15px", Text = "For use on Space Facilities only!" },
            new UiNode(UiKind.Box) { Key = "contents", FontSize = "12px", Text = "Table of Contents:" });
        foreach (UiValue channel in channels)
            section.With(new UiNode(UiKind.Box) { Key = "page:" + TguiFormat.JsNumber(channel.Num("page_number")), Text = "Page " + TguiFormat.JsNumber(channel.Num("page_number")) + ": " + channel.Str("name") });
        if (data.Truthy("wanted_criminal"))
            section.With(new UiNode(UiKind.Box) { Key = "wanted", Bold = true, Text = "Last Page: Important Security Announcement" });
        return section;
    }

    // tg: Newspaper.tsx:110
    private static UiNode Channel(UiValue data)
    {
        bool hasMessages = data.Truthy("channel_has_messages");
        var section = new UiNode(UiKind.Section) { Key = "channel" };
        foreach (UiValue channel in data.Get("channel_data")?.Items ?? Array.Empty<UiValue>())
        {
            var box = new UiNode(UiKind.Box) { Key = "channel:" + channel.Str("channel_name") }.With(
                new UiNode(UiKind.Box) { Key = "name", Bold = true, FontSize = "15px", Text = channel.Str("channel_name") },
                new UiNode(UiKind.Box) { Key = "author", FontSize = "12px", Text = "Channel made by: " + channel.Str("author_name") });
            if (hasMessages)
            {
                int index = 0;
                foreach (UiValue message in channel.Get("channel_messages")?.Items ?? Array.Empty<UiValue>())
                {
                    // tg: {!!message.photo && <Image src={message.photo} />}: the story's photo (no picture has an image here, D104)
                    box.With(
                        new UiNode(UiKind.Box) { Key = "message:" + index }.With(
                            new UiNode(UiKind.Html) { Key = "text", TextValue = message.Str("message") ?? "" },
                            new UiNode(UiKind.Box) { Key = "writer", Text = "Written by: " + message.Str("author") }),
                        new UiNode(UiKind.Divider) { Key = "divider:" + index });
                    index++;
                }
            }
            else
                box.With(new UiNode(UiKind.Box) { Key = "empty", Text = "No feed stories stem from this channel..." });
            section.With(box);
        }
        return section;
    }

    // tg: Newspaper.tsx:144
    private static UiNode Ending(UiValue data)
    {
        string criminal = data.Str("wanted_criminal");
        if (string.IsNullOrEmpty(criminal))
            return new UiNode(UiKind.Section) { Key = "ending" }.With(new UiNode(UiKind.Box) { Key = "ads", Text = "Apart from some uninteresting classified ads, theres nothing in this page..." });
        // tg: {!!wanted_photo && <Image src={wanted_photo} />}: the wanted photo (no picture has an image here, D104)
        return new UiNode(UiKind.Section) { Key = "ending" }.With(
            new UiNode(UiKind.Box) { Key = "wanted", Bold = true, FontSize = "15px", Text = "Wanted Issue" },
            new UiNode(UiKind.Box) { Key = "criminal", FontSize = "12px", Text = "Criminal Name: " + criminal },
            new UiNode(UiKind.Box) { Key = "body", Text = "Description: " + data.Str("wanted_body") });
    }
}
