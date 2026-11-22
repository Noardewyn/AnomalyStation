using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces.Common;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/common/EditableText.tsx and CharacterPreview.tsx (gaps
// SC2, D333: the medical and security records). Engine-free. The text to click is a transparent button (the kit's boxes take no
// clicks; its text keeps the button's colour, not tg's grey, white or given colour), and Escape is the field losing the focus (the
// kit's input has no onEscape): either leaves the field without sending.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class EditableText
{
    /**
     * Expects a referrence to the thing you're modifying,
     * then attempts to set the field with the value.
     *
     * When user presses ENTER, this is called to Byond:
     * act('edit_field', { field: field, ref: target_ref, value: value })
     */
    // tg: common/EditableText.tsx:26
    public static UiNode Render(TguiContext c, string field, string target_ref, string text, string color = null)
    {
        if (string.IsNullOrEmpty(field))
            return null;

        string editingKey = "editable:" + field + ":" + target_ref;
        string typedKey = "input:" + editingKey; // the field's typed text (the kit keeps it under its key)
        bool editing = c.UseState(editingKey, false);

        if (editing)
        {
            return new UiNode(UiKind.Input)
            {
                Key = editingKey,
                AutoFocus = true,
                Width = "50%",
                MaxLength = 512,
                TextValue = text,
                OnCommit = _ => c.SetState(editingKey, false), // onEscape (the focus leaving) and the Enter below
                OnEnter = () =>
                {
                    c.SetState(editingKey, false);
                    c.Act("edit_field", ("field", field), ("ref", target_ref), ("value", c.Local.Get(typedKey, text ?? "")));
                },
            };
        }

        return new UiNode(UiKind.Stack) { Key = editingKey }.With(
            new UiNode(UiKind.StackItem) { Key = "text" }.With(new UiNode(UiKind.Button)
            {
                Key = "edit",
                Color = "transparent",
                Text = string.IsNullOrEmpty(text) ? "(none)" : text,
                OnClick = () =>
                {
                    c.Local.SetQuiet(typedKey, text ?? "");
                    c.SetState(editingKey, true);
                },
            }),
            new UiNode(UiKind.StackItem) { Key = "clearItem" }.With(new UiNode(UiKind.Button)
            {
                Key = "clear",
                Color = "transparent",
                Icon = "backspace",
                Tooltip = "Clear",
                OnClick = () => c.Act("edit_field", ("field", field), ("ref", target_ref), ("value", "")),
            }));
    }
}

public static class CharacterPreview
{
    // tg: common/CharacterPreview.tsx:3  a ByondUi map, drawn as a 2D picture (D284) as the preferences' preview
    public static UiNode Render(string height, string id) => new UiNode(UiKind.ByondUi)
    {
        Key = "map:" + id,
        MapRef = id,
        MapPicture = true,
        Width = "220px",
        Height = height,
    };
}
