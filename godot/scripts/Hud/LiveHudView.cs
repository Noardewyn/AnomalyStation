using Godot;
using Anomaly.Client.Shared;

namespace Anomaly.Client.Hud;

/// <summary>
/// The live HUD's native boxes (ui-overhaul.md section 6, wireframes Mockup 1): the doll panel's background, and the combat pill under it (a
/// key chip with the key bound to combat mode and "Combat" or "Peaceful", red when on). It only draws: <see cref="HudLayer"/> places it first
/// in its root, so tg's pictures (the doll) stand over it and it hides with the interface, and reports the pill's rectangle to the HUD pick
/// under the combat toggle's screen entry.
/// </summary>
public partial class LiveHudView : Control
{
    private LiveHudLayout _layout;
    private float _unit = 1f;
    private bool _panel;
    private bool? _combat;
    private string _key;
    private readonly StyleBoxFlat _panelStyle = new();
    private readonly StyleBoxFlat _pillStyle = new();

    public LiveHudView()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    /// <summary>What to draw this frame: the panel when a doll is shown, the pill when a combat toggle is shown (null: none) and the key chip's label (null: unbound).</summary>
    public void Show(in LiveHudLayout layout, float unit, bool panel, bool? combat, string key)
    {
        if (layout == _layout && unit == _unit && panel == _panel && combat == _combat && key == _key)
            return;
        (_layout, _unit, _panel, _combat, _key) = (layout, unit, panel, combat, key);
        QueueRedraw();
    }

    private static Rect2 R(HudRect r) => new(r.Left, r.Top, r.Width, r.Height);

    private static int FontPx(float referencePixels, float u) => Math.Max(1, (int)Math.Round(referencePixels * u));

    public override void _Draw()
    {
        Font font = ThemeDB.FallbackFont;
        float u = _unit;
        if (_panel)
        {
            _panelStyle.BgColor = ChatLines.Rgba(UiTokens.DollPanelBack);
            _panelStyle.SetCornerRadiusAll(Math.Max(1, (int)Math.Round(UiTokens.DollPanelRadius * u)));
            DrawStyleBox(_panelStyle, R(_layout.Panel));
        }
        if (_combat is not bool on)
            return;
        HudRect pill = _layout.Pill;
        _pillStyle.BgColor = ChatLines.Rgba(on ? UiTokens.PillCombat : UiTokens.ChipBack);
        _pillStyle.BorderColor = ChatLines.Rgba(UiTokens.ChipRim);
        _pillStyle.SetBorderWidthAll(Math.Max(1, (int)Math.Round(UiTokens.ChipRimWidth * u)));
        _pillStyle.SetCornerRadiusAll(Math.Max(1, (int)(pill.Height / 2f)));
        DrawStyleBox(_pillStyle, R(pill));
        string text = LiveHud.CombatText(on);
        int textSize = FontPx(UiTokens.PillFont, u), chipSize = FontPx(UiTokens.ChipFont, u);
        float textWidth = font.GetStringSize(text, HorizontalAlignment.Left, -1, textSize).X;
        float chipWidth = _key == null ? 0f : font.GetStringSize(_key, HorizontalAlignment.Left, -1, chipSize).X + 2 * UiTokens.ChipPadX * u;
        float gap = _key == null ? 0f : UiTokens.PromptChipGap * u;
        float left = pill.Left + Math.Max(0f, (pill.Width - chipWidth - gap - textWidth) / 2f);
        if (_key != null)
        {
            float chipHeight = UiTokens.ChipHeight * u;
            ChatLines.DrawChip(this, font, new Rect2(left, pill.Top + (pill.Height - chipHeight) / 2f, chipWidth, chipHeight), _key, chipSize, u, 1f);
        }
        float textLeft = left + chipWidth + gap;
        float textBaseline = pill.Top + (pill.Height + font.GetAscent(textSize) - font.GetDescent(textSize)) / 2f;
        DrawString(font, new Vector2(textLeft, textBaseline), text, HorizontalAlignment.Left, Math.Max(1f, pill.Right - textLeft), textSize, ChatLines.Rgba(UiTokens.LocalText));
    }
}
