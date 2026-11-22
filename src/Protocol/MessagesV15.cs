namespace Anomaly.Protocol;

// Protocol version 15 (M4c slice 4: tg's radial menu, show_radial_menu; docs/ai/specs/protocol.md section 24; DV40). tg shows a radial
// menu as screen objects (the slices and the centre button) around the anchor in one client's images, and waits until the player
// clicks one. The server keeps the menu (the datum, its choices, its pages, the wait, require_near and custom_check) and sends the
// current page to the client in a RadialMenu message; the client draws it at its crosshair and answers with a RadialPick action
// (ActionKind.RadialPick, MessagesV2.cs): an element of the page, or the centre (close).

// The last action kind the codec takes is ActionKinds.Last (MessagesV17.cs since version 17).

/// <summary>What a <see cref="RadialMenuMessage"/> does.</summary>
public enum RadialMenuKind : byte
{
    /// <summary>The menu is shown, or its page changed (tg's <c>show_to</c>, <c>next_page</c>): the client draws these elements.</summary>
    Open = 0,

    /// <summary>The menu is gone (answered, closed by a repeat, out of reach, deleted): the client stops drawing it.</summary>
    Close = 1,
}

/// <summary>Bits of a <see cref="RadialMenuMessage"/>'s flags.</summary>
[Flags]
public enum RadialMenuFlags : byte
{
    None = 0,

    /// <summary>tg's <c>tooltips</c>: the slices' names show as tooltips while hovered.</summary>
    Tooltips = 1,

    /// <summary>tg's <c>display_close_button</c>: the menu has a centre button (<see cref="RadialMenuMessage.Center"/>), a pick of element 0.</summary>
    CloseButton = 2,

    /// <summary>tg's <c>BUTTON_SLIDE_IN</c>: the slices slide out from the centre one after another.</summary>
    SlideIn = 4,

    /// <summary>tg's <c>BUTTON_FADE_IN</c>.</summary>
    FadeIn = 8,

    /// <summary>tg's <c>BUTTON_FADE_OUT</c>: the slices fade out once the menu is answered.</summary>
    FadeOut = 16,

    /// <summary>Every valid bit.</summary>
    All = Tooltips | CloseButton | SlideIn | FadeIn | FadeOut,
}

/// <summary>Bits of a <see cref="RadialSlice"/>'s flags.</summary>
[Flags]
public enum RadialElementFlags : byte
{
    None = 0,

    /// <summary>tg's <c>next_page</c>: the slice turns the page (the server sends the next one).</summary>
    NextPage = 1,

    /// <summary>tg's <c>click_on_hover</c>: hovering the slice picks it.</summary>
    ClickOnHover = 2,

    /// <summary>Every valid bit.</summary>
    All = NextPage | ClickOnHover,
}

/// <summary>
/// One slice of the page, as tg's <c>SetElement</c> sets it up: its name (the slice's <c>name</c>), its picture (the choice's image, the
/// slice's overlay; none for a choice without one), and the slice itself, normal and hovered (<c>radial.dmi</c>'s <c>radial_slice</c> or
/// the menu's slice icon, and its <c>_focus</c> state). The appearances are entries of the session's tables (private or public).
/// </summary>
public readonly record struct RadialSlice(string Name, RadialElementFlags Flags, TableRef Icon, TableRef Background, TableRef BackgroundFocus);

/// <summary>
/// Version 15, server to client on the State channel after the tick's Delta (whose private block carries the appearances it names):
/// a radial menu of this session opened or turned its page (<see cref="RadialMenuKind.Open"/>, the page's elements in tg's order,
/// clockwise from the top), or closed. A session may have several menus (tg's unique ids); the newest open one is drawn.
/// </summary>
public sealed class RadialMenuMessage
{
    public RadialMenuKind Kind { get; set; }

    /// <summary>The menu's id (the server's world id of the <c>/datum/radial_menu</c>), above 0: what a <see cref="ActionKind.RadialPick"/> names.</summary>
    public long MenuId { get; set; }

    public RadialMenuFlags Flags { get; set; }

    /// <summary>The page shown, 1 to <see cref="Pages"/>.</summary>
    public byte Page { get; set; }

    public byte Pages { get; set; }

    /// <summary>The centre button and its hovered look (<c>radial_center</c>, <c>radial_center_focus</c>), with <see cref="RadialMenuFlags.CloseButton"/> only.</summary>
    public TableRef Center { get; set; }

    public TableRef CenterFocus { get; set; }

    /// <summary>The page's slices, 1 to <see cref="Limits.MaxRadialElements"/>, for an open; none for a close.</summary>
    public List<RadialSlice> Elements { get; } = new();
}
