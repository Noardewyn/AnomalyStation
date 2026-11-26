using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>ui-overhaul.md section 6, slice A: key chip labels from the bindings, the prompt lines beside the crosshair and where they go.</summary>
public class KeyChipsTests
{
    [Theory]
    [InlineData("MouseLeft", "LMB")]
    [InlineData("MouseRight", "RMB")]
    [InlineData("MouseMiddle", "MMB")]
    [InlineData("Ctrl_Q", "Ctrl+Q")]
    [InlineData("Shift_MouseLeft", "Shift+LMB")]
    [InlineData("Ctrl_Shift_T", "Ctrl+Shift+T")]
    [InlineData("Key1", "1")]
    [InlineData("Kp1", "1")]
    [InlineData("Semicolon", ";")]
    [InlineData("Apostrophe", "'")]
    [InlineData("Shift_Apostrophe", "Shift+'")]
    [InlineData("Bracketleft", "[")]
    [InlineData("Quoteleft", "`")]
    [InlineData("Backslash", "\\")]
    [InlineData("Escape", "Esc")]
    [InlineData("Shift_Escape", "Shift+Esc")]
    [InlineData("F3", "F3")]
    [InlineData("Delete", "Delete")]
    [InlineData("Ctrl", "Ctrl")]
    [InlineData("Kp", "Kp")]
    [InlineData("KpAdd", "KpAdd")]
    [InlineData("Keyboard", "Keyboard")]
    public void A_binding_name_reads_as_a_player_reads_it(string binding, string label)
    {
        Assert.Equal(label, KeyChips.Label(binding));
    }

    [Fact]
    public void An_actions_chip_is_its_first_key_and_nothing_when_unbound()
    {
        KeyBindings b = KeyBindings.Defaults();
        Assert.Equal("H", KeyChips.KeyOf(b, PlayAction.StopPulling)); // H and Delete: the first
        Assert.Equal("Ctrl+Q", KeyChips.KeyOf(b, PlayAction.ThrowMode));
        b.Set(PlayAction.ThrowMode);
        Assert.Null(KeyChips.KeyOf(b, PlayAction.ThrowMode));
    }

    private static ContextHintsMessage Hints(params ContextHint[] hints)
    {
        var message = new ContextHintsMessage { TargetKind = TargetKind.Entity, Target = 7, Name = "toolbox" };
        message.Hints.AddRange(hints);
        return message;
    }

    [Fact]
    public void The_prompt_names_a_thing_only_from_the_servers_answer_for_it()
    {
        // DV589: on a real server nothing shows until the answer for this very target has come
        var thing = new ClickTarget(TargetKind.Entity, 7, 0, 0, 0);
        var cell = new ClickTarget(TargetKind.Turf, 0, 3, 4, 1);
        Assert.Null(KeyChips.PromptName(KeyChips.AnswerFor(null, thing), realServer: true, "toolbox"));
        Assert.Equal("toolbox", KeyChips.PromptName(null, realServer: false, "toolbox")); // the fake server: the client's name, as before

        // an answer for another target (an older hover) is no answer for this one
        var other = new ContextHintsMessage { TargetKind = TargetKind.Entity, Target = 8, Name = "crowbar" };
        Assert.Null(KeyChips.AnswerFor(other, thing));
        Assert.Null(KeyChips.PromptName(KeyChips.AnswerFor(other, thing), realServer: true, "toolbox"));
        Assert.Null(KeyChips.AnswerFor(new ContextHintsMessage { TargetKind = TargetKind.Turf, TurfX = 3, TurfY = 5, TurfZ = 1 }, cell));
        Assert.Null(KeyChips.AnswerFor(new ContextHintsMessage { TargetKind = TargetKind.Entity, Target = 7 }, cell)); // same numbers, another kind

        // tg's "no screentip" (every floor): a null name, and the client names the floor itself
        var floor = new ContextHintsMessage { TargetKind = TargetKind.Turf, TurfX = 3, TurfY = 4, TurfZ = 1 };
        Assert.Same(floor, KeyChips.AnswerFor(floor, cell));
        Assert.Equal("floor (iron)", KeyChips.PromptName(KeyChips.AnswerFor(floor, cell), realServer: true, "floor (iron)"));

        // out of reach: a zero-length name and no hints: bare, no lines
        var far = new ContextHintsMessage { TargetKind = TargetKind.Entity, Target = 7, Name = "" };
        Assert.Equal("", KeyChips.PromptName(KeyChips.AnswerFor(far, thing), realServer: true, "toolbox"));
        Assert.Empty(KeyChips.Prompts(far, KeyBindings.Defaults()));

        // in reach: the answer for it gives its name (the client's name for it takes the server's)
        Assert.Equal("toolbox", KeyChips.PromptName(KeyChips.AnswerFor(Hints(), thing), realServer: true, "toolbox"));
    }

    [Fact]
    public void Out_of_reach_only_the_prompt_goes_bare_and_the_radial_and_dev_tools_keep_a_name()
    {
        // DV589: the server's zero-length name is "out of reach": no name given, so the radial's title and the dev tools use the client's own;
        // tg's null ("no screentip") the same; a name is the server's
        Assert.Null(KeyChips.ServerName(new ContextHintsMessage { TargetKind = TargetKind.Entity, Target = 7, Name = "" }));
        Assert.Null(KeyChips.ServerName(new ContextHintsMessage { TargetKind = TargetKind.Entity, Target = 7 }));
        Assert.Null(KeyChips.ServerName(null));
        Assert.Equal("toolbox", KeyChips.ServerName(Hints()));
        // the prompt alone stays bare for it, whatever the client would call the thing
        Assert.Equal("", KeyChips.PromptName(new ContextHintsMessage { TargetKind = TargetKind.Entity, Target = 7, Name = "" }, realServer: true, "pen"));
    }

    [Fact]
    public void A_left_and_a_right_click_hint_give_two_lines_with_the_click_keys()
    {
        List<PromptLine> lines = KeyChips.Prompts(Hints(new ContextHint(ClickButton.Left, ClickModifiers.None, "Pick up"), new ContextHint(ClickButton.Right, ClickModifiers.None, "Open")), KeyBindings.Defaults());
        Assert.Equal(new[] { new PromptLine("LMB", "Pick up"), new PromptLine("RMB", "Open") }, lines);
    }

    [Fact]
    public void The_keys_come_from_the_bindings_not_from_the_hints_buttons()
    {
        KeyBindings b = KeyBindings.Defaults();
        b.Set(PlayAction.PrimaryClick, "MouseRight");
        b.Set(PlayAction.SecondaryClick, "MouseLeft");
        List<PromptLine> lines = KeyChips.Prompts(Hints(new ContextHint(ClickButton.Left, ClickModifiers.None, "Pick up"), new ContextHint(ClickButton.Right, ClickModifiers.None, "Open")), b);
        Assert.Equal(new[] { new PromptLine("RMB", "Pick up"), new PromptLine("LMB", "Open") }, lines);
    }

    [Fact]
    public void Rebinding_pull_changes_its_chip_and_drops_examine_that_lost_its_key()
    {
        ContextHintsMessage hints = Hints(new ContextHint(ClickButton.Left, ClickModifiers.Shift, "Examine"), new ContextHint(ClickButton.Left, ClickModifiers.Ctrl, "Pull"));
        Assert.Equal(new[] { new PromptLine("Shift+LMB", "Examine"), new PromptLine("Ctrl+LMB", "Pull") }, KeyChips.Prompts(hints, KeyBindings.Defaults()));
        KeyBindings b = KeyBindings.Defaults();
        Assert.Equal(PlayAction.Examine, b.Bind(PlayAction.Pull, "Shift_MouseLeft"));
        Assert.Equal(new[] { new PromptLine("Shift+LMB", "Pull") }, KeyChips.Prompts(hints, b));
    }

    [Fact]
    public void An_unbound_action_drops_its_line()
    {
        KeyBindings b = KeyBindings.Defaults();
        b.Set(PlayAction.SecondaryClick);
        List<PromptLine> lines = KeyChips.Prompts(Hints(new ContextHint(ClickButton.Left, ClickModifiers.None, "Pick up"), new ContextHint(ClickButton.Right, ClickModifiers.None, "Open")), b);
        Assert.Equal(new[] { new PromptLine("LMB", "Pick up") }, lines);
    }

    [Fact]
    public void Chords_without_an_action_of_their_own_are_dropped()
    {
        ContextHintsMessage hints = Hints(
            new ContextHint(ClickButton.Left, ClickModifiers.Alt, "Rotate"),
            new ContextHint(ClickButton.Right, ClickModifiers.Alt, "Eject"),
            new ContextHint(ClickButton.Right, ClickModifiers.Ctrl, "Toggle"),
            new ContextHint(ClickButton.Left, ClickModifiers.Ctrl | ClickModifiers.Shift, "Wrench"),
            new ContextHint(ClickButton.Left, ClickModifiers.None, "Buckle", ContextHintAction.DropSelf),
            new ContextHint(ClickButton.Left, ClickModifiers.None, "Strip", ContextHintAction.DropOntoSelf),
            new ContextHint(ClickButton.Left, ClickModifiers.None, "Flip", ContextHintAction.Extra),
            new ContextHint(ClickButton.Right, ClickModifiers.None, "Open"));
        Assert.Equal(new[] { new PromptLine("RMB", "Open") }, KeyChips.Prompts(hints, KeyBindings.Defaults()));
    }

    [Fact]
    public void Stop_pulling_shows_its_own_key()
    {
        ContextHintsMessage hints = Hints(new ContextHint(ClickButton.Left, ClickModifiers.None, "Stop pulling", ContextHintAction.StopPulling));
        Assert.Equal(new[] { new PromptLine("H", "Stop pulling") }, KeyChips.Prompts(hints, KeyBindings.Defaults()));
        KeyBindings b = KeyBindings.Defaults();
        b.Set(PlayAction.StopPulling, "Delete");
        Assert.Equal(new[] { new PromptLine("Delete", "Stop pulling") }, KeyChips.Prompts(hints, b));
    }

    [Fact]
    public void At_most_three_click_lines_in_tgs_order_the_first_hint_of_a_chord_winning()
    {
        ContextHintsMessage hints = Hints(
            new ContextHint(ClickButton.Left, ClickModifiers.Ctrl, "Pull"),
            new ContextHint(ClickButton.Left, ClickModifiers.Shift, "Examine"),
            new ContextHint(ClickButton.Right, ClickModifiers.None, "Open"),
            new ContextHint(ClickButton.Left, ClickModifiers.None, "Pick up"),
            new ContextHint(ClickButton.Left, ClickModifiers.None, "Unbuckle"));
        List<PromptLine> lines = KeyChips.Prompts(hints, KeyBindings.Defaults());
        Assert.Equal(new[] { new PromptLine("LMB", "Pick up"), new PromptLine("RMB", "Open"), new PromptLine("Shift+LMB", "Examine") }, lines);
        Assert.Equal(KeyChips.MaxClickLines, lines.Count);
    }

    [Fact]
    public void Stop_pulling_is_never_the_line_cut()
    {
        // what the server sends while the player pulls a mob: tg's LMB and RMB, the port's Pull and Stop pulling (ServerHost.World.cs)
        ContextHintsMessage hints = Hints(
            new ContextHint(ClickButton.Left, ClickModifiers.None, "Hug"),
            new ContextHint(ClickButton.Right, ClickModifiers.None, "Shove"),
            new ContextHint(ClickButton.Left, ClickModifiers.Shift, "Examine"),
            new ContextHint(ClickButton.Left, ClickModifiers.Ctrl, "Grab"),
            new ContextHint(ClickButton.Left, ClickModifiers.None, "Stop pulling", ContextHintAction.StopPulling));
        List<PromptLine> lines = KeyChips.Prompts(hints, KeyBindings.Defaults());
        Assert.Equal(new[] { new PromptLine("LMB", "Hug"), new PromptLine("RMB", "Shove"), new PromptLine("Shift+LMB", "Examine"), new PromptLine("H", "Stop pulling") }, lines);
        Assert.Equal(KeyChips.MaxPromptLines, lines.Count);
    }

    [Fact]
    public void A_click_line_shows_the_mouse_button_bound_to_that_click()
    {
        ContextHintsMessage hints = Hints(new ContextHint(ClickButton.Left, ClickModifiers.None, "Hug"), new ContextHint(ClickButton.Right, ClickModifiers.None, "Shove"));
        // --bind=secondary_click=Z: the game acts on a click only from the mouse, so a Z chip would name a key that does nothing
        KeyBindings b = KeyBindings.Defaults();
        b.Set(PlayAction.SecondaryClick, "Z");
        Assert.Equal(new[] { new PromptLine("LMB", "Hug") }, KeyChips.Prompts(hints, b));
        // a key and a mouse button: the mouse button, wherever it stands in the list
        b.Set(PlayAction.SecondaryClick, "Z", "MouseMiddle");
        Assert.Equal(new[] { new PromptLine("LMB", "Hug"), new PromptLine("MMB", "Shove") }, KeyChips.Prompts(hints, b));
        Assert.Equal("MMB", KeyChips.MouseButtonOf(b, PlayAction.SecondaryClick));
        Assert.Null(KeyChips.MouseButtonOf(KeyBindings.Defaults(), PlayAction.DropItem));
    }

    [Fact]
    public void No_hints_give_no_lines()
    {
        Assert.Empty(KeyChips.Prompts(null, KeyBindings.Defaults()));
        Assert.Empty(KeyChips.Prompts(Hints(), KeyBindings.Defaults()));
    }

    public static IEnumerable<object[]> Screens()
    {
        foreach ((float w, float h) in new[] { (1920f, 1080f), (1280f, 720f), (3840f, 2160f), (1024f, 768f) })
        {
            foreach (float scale in new[] { 0.5f, 1f, 1.5f, 2f, 2.5f, 3f })
                yield return new object[] { w, h, scale };
        }
    }

    [Theory]
    [MemberData(nameof(Screens))]
    public void The_prompt_block_stays_on_screen_right_of_the_crosshair_and_above_the_balloon(float width, float height, float scale)
    {
        var metrics = new HudMetrics(width, height, scale);
        HudRect balloon = KeyChips.BalloonRow(metrics);
        foreach (float crosshair in new[] { 0f, 8.5f, 34f, 68f }) // the crosshair's extent at its 50, 200 and 400 per cent
        {
            foreach (int lines in new[] { 1, KeyChips.MaxClickLines, KeyChips.MaxPromptLines })
            {
                foreach (float wanted in new[] { 0f, 200f, 5000f })
                {
                    HudRect block = KeyChips.PromptBlock(metrics, lines, wanted, width / 2f, height / 2f, crosshair);
                    Assert.True(block.Left > width / 2f + crosshair, "right of the crosshair's arms");
                    Assert.True(block.Top >= 0 && block.Right <= width && block.Bottom <= height, "on screen");
                    Assert.True(block.Bottom < balloon.Top, $"clear of the balloon row: {block.Bottom} >= {balloon.Top}");
                    Assert.True(block.Width <= wanted, "no wider than asked");
                }
            }
            Assert.True(KeyChips.PromptBlock(metrics, 3, 5000f, width / 2f, height / 2f, crosshair).Right <= width - UiTokens.ScreenMargin * UiTokens.Unit(metrics) + 0.01f, "the margin is kept");
        }
    }

    [Fact]
    public void The_block_is_as_wide_as_its_widest_line_and_grows_with_its_lines()
    {
        var metrics = new HudMetrics(1920, 1080, 1f);
        Assert.Equal(300f, KeyChips.PromptBlock(metrics, 2, 300f, 960, 540, 34f).Width);
        Assert.True(KeyChips.PromptBlock(metrics, 4, 300f, 960, 540, 34f).Height > KeyChips.PromptBlock(metrics, 3, 300f, 960, 540, 34f).Height);
        Assert.Equal(KeyChips.PromptBlock(metrics, 4, 300f, 960, 540, 34f).Height, KeyChips.PromptBlock(metrics, 9, 300f, 960, 540, 34f).Height);
        Assert.Equal(KeyChips.PromptBlock(metrics, 1, 300f, 960, 540, 0f).Left + 34f, KeyChips.PromptBlock(metrics, 1, 300f, 960, 540, 34f).Left);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(2f)]
    [InlineData(4f)]
    public void The_plain_crosshair_reaches_to_its_arms_end_at_its_size(float size)
    {
        Assert.Equal((GunSightGap.ArmEndPixels + GunSightGap.ShadowPixels) * size, KeyChips.SightReach(false, 0f, 75f, 1080f, size));
    }

    [Theory]
    [InlineData(1920f, 1080f, 1f, 4f)]   // a buckshot-wide cone
    [InlineData(1920f, 1080f, 2f, 4f)]
    [InlineData(1920f, 1080f, 2f, 0f)]   // a gun with no spread
    [InlineData(1280f, 720f, 1f, 12f)]
    [InlineData(1920f, 1080f, 4f, 30f)]
    public void With_a_gun_the_block_starts_past_the_sights_line_even_right_after_a_shot(float width, float height, float size, float cone)
    {
        const float fov = 75f;
        var metrics = new HudMetrics(width, height, 1f);
        float reach = KeyChips.SightReach(true, cone, fov, height, size);
        HudRect block = KeyChips.PromptBlock(metrics, 2, 200f, width / 2f, height / 2f, reach);
        foreach (double since in new[] { 0.0, 0.1, 5.0 }) // a shot's bloom, half way, at rest
        {
            // Crosshair.DrawGun: the right line runs from the gap to the gap plus its length times the crosshair's size
            float lineEnd = width / 2f + GunSightGap.Pixels(cone, fov, height, since) + GunSightGap.LinePixels * size;
            Assert.True(block.Left > lineEnd, $"the block at {block.Left} starts on the sight's line ending at {lineEnd}");
        }
        Assert.True(reach > KeyChips.SightReach(false, cone, fov, height, size) || cone == 0f, "a gun's sight reaches past the plain arms");
        Assert.True(block.Right <= width, "on screen");
    }

    public static IEnumerable<object[]> Pointers()
    {
        foreach ((float x, float y) in new[] { (100f, 100f), (1800f, 100f), (1900f, 1060f), (20f, 1070f), (960f, 540f), (1500f, 900f) })
        {
            foreach (float scale in new[] { 0.5f, 1f, 3f })
                yield return new object[] { x, y, scale };
        }
    }

    [Theory]
    [MemberData(nameof(Pointers))]
    public void In_cursor_mode_the_block_stands_beside_the_pointer_on_the_screen(float x, float y, float scale)
    {
        var metrics = new HudMetrics(1920, 1080, scale);
        float u = UiTokens.Unit(metrics);
        foreach (float wanted in new[] { 120f, 400f, 5000f })
        {
            HudRect block = KeyChips.PromptBlock(metrics, KeyChips.MaxPromptLines, wanted, x, y, KeyChips.PointerReach);
            Assert.True(block.Left >= 0 && block.Top >= 0 && block.Right <= 1920 && block.Bottom <= 1080, $"on screen: {block}");
            // beside the pointer: left of it or right of it, clear of its arrow, never across it
            Assert.True(block.Left >= x + KeyChips.PointerReach || block.Right <= x - KeyChips.PointerReach, $"clear of the pointer at {x}: {block}");
            // and near it: the block's top at the pointer's offset unless the screen's edge moved it
            float wantedTop = y + UiTokens.PromptOffsetY * u;
            if (wantedTop >= UiTokens.ScreenMargin * u && wantedTop + block.Height <= 1080 - UiTokens.ScreenMargin * u)
                Assert.Equal(wantedTop, block.Top, 3);
        }
    }

    [Fact]
    public void A_pointer_with_room_on_its_right_gets_the_block_there_and_one_at_the_right_edge_gets_it_mirrored()
    {
        var metrics = new HudMetrics(1920, 1080, 1f);
        float gap = KeyChips.PointerReach + UiTokens.PromptOffsetX;
        HudRect right = KeyChips.PromptBlock(metrics, 2, 300f, 400f, 300f, KeyChips.PointerReach);
        Assert.Equal(400f + gap, right.Left);
        Assert.Equal(300f + UiTokens.PromptOffsetY, right.Top);
        HudRect mirrored = KeyChips.PromptBlock(metrics, 2, 300f, 1800f, 300f, KeyChips.PointerReach);
        Assert.Equal(1800f - gap, mirrored.Right, 3);
        Assert.Equal(300f, mirrored.Width);
    }

    [Fact]
    public void A_right_edge_narrows_a_wide_block_beside_the_crosshair_instead_of_mirroring_it()
    {
        var metrics = new HudMetrics(1920, 1080, 2f);
        float reach = 30f;
        // without an edge a block too wide for the right goes left of the crosshair; with the doll panel's edge it stays right and ends there
        HudRect free = KeyChips.PromptBlock(metrics, 1, 1000f, 960f, 540f, reach);
        HudRect limited = KeyChips.PromptBlock(metrics, 1, 1000f, 960f, 540f, reach, 1284f);
        Assert.Equal(960f + reach + UiTokens.PromptOffsetX * 2f, limited.Left, 3);
        Assert.Equal(1284f, limited.Right, 3);
        Assert.True(free.Width > limited.Width);
        // with less than the least width left before the edge, the block goes left of the crosshair as it would without the panel
        HudRect noRoom = KeyChips.PromptBlock(metrics, 1, 300f, 960f, 540f, reach, 1010f + UiTokens.PromptMinWidth * 2f - 1f);
        Assert.Equal(960f - reach - UiTokens.PromptOffsetX * 2f, noRoom.Right, 3);
        Assert.Equal(300f, noRoom.Width);
        HudRect justRoom = KeyChips.PromptBlock(metrics, 1, 300f, 960f, 540f, reach, 1010f + UiTokens.PromptMinWidth * 2f);
        Assert.Equal(1010f, justRoom.Left, 3);
        Assert.Equal(UiTokens.PromptMinWidth * 2f, justRoom.Width, 3);
    }

    // one pixel per character, the mark two
    private static float Measure(string s) => s.Replace(KeyChips.Ellipsis, "..").Length;

    [Fact]
    public void A_text_too_long_for_its_room_is_cut_with_an_ellipsis()
    {
        Assert.Equal("reinforced wall", KeyChips.Ellipsize("reinforced wall", 15f, Measure));
        Assert.Equal("reinforced" + KeyChips.Ellipsis, KeyChips.Ellipsize("reinforced wall (wall/r wall)", 13f, Measure));
        Assert.True(Measure(KeyChips.Ellipsize("reinforced wall (wall/r wall)", 13f, Measure)) <= 13f);
        // a space before the cut is not kept before the mark
        Assert.Equal("reinforced" + KeyChips.Ellipsis, KeyChips.Ellipsize("reinforced wall", 12f, Measure));
        Assert.Equal(KeyChips.Ellipsis, KeyChips.Ellipsize("reinforced", 2f, Measure));
        Assert.Equal(string.Empty, KeyChips.Ellipsize("reinforced", 1f, Measure));
        Assert.Equal(string.Empty, KeyChips.Ellipsize(null, 10f, Measure));
    }
}
