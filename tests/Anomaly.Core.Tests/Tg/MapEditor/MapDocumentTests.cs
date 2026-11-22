using System.IO;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit;

namespace Anomaly.Core.Tests.Tg.MapEditor;

/// <summary>The map editor's stage 1 (docs/ai/specs/map-editor.md 4): the editable document, its undoable steps and the file it writes.</summary>
public class MapDocumentTests
{
    private const string Space = "/turf/open/space/basic | /area/space";
    private const string Floor = "/turf/open/floor/iron | /area/station/hallway";

    private static string Fixture() => File.ReadAllText(Path.Combine(MapFixtures.FixtureFolder(), "p2_small.as3dmap"));

    private static string Header(int width, int height, int levels)
        => "anomaly-map 1\nsize " + width + " " + height + " " + levels + "\nchunk 32\ndefault " + Space + "\n";

    [Fact]
    public void A_map_read_and_not_edited_is_written_as_it_was_read()
    {
        string text = Fixture();
        MapDocument document = MapDocument.Parse(text, "small");
        Assert.Equal(text, document.ToText());
        Assert.False(document.IsDirty);
    }

    [Fact]
    public void The_converted_meta_is_written_as_it_was_read()
    {
        string meta = MapFixtures.ConvertedMeta();
        if (meta == null) return; // skipped without a converted map (tools/convert-maps.sh)
        string text = File.ReadAllText(meta);
        Assert.True(text == MapDocument.Parse(text, "meta").ToText(), "MetaStation's file changed on a read and a write");
    }

    [Fact]
    public void An_edit_of_one_tile_changes_one_line()
    {
        string text = Fixture();
        MapDocument document = MapDocument.Parse(text, "small");
        MapModel before = document[3, 3, 1];
        var members = new List<MapMember> { new("/obj/item/wrench", Array.Empty<MapOverride>()) };
        members.AddRange(before.Members);
        Assert.True(document.Edit("Place a wrench", new[] { (3, 3, 1, document.Model(members)) }));

        string[] was = text.Split('\n'), now = document.ToText().Split('\n');
        Assert.Equal(was.Length, now.Length);
        int line = Array.IndexOf(now, "3,3: /obj/item/wrench | " + before.Text);
        Assert.True(line >= 0, "the edited tile's line is the wrench in front of what the tile had");
        Assert.Equal(1, Enumerable.Range(0, was.Length).Count(i => was[i] != now[i]));
        Assert.Equal("/obj/item/wrench", MapDocument.Parse(document.ToText(), "again")[3, 3, 1].Members[0].Path);
    }

    [Fact]
    public void A_tile_set_to_the_default_loses_its_line_and_an_empty_chunk_its_section()
    {
        MapDocument document = MapDocument.Create(40, 3, 1, Space);
        document.Edit("Floor", new[] { (40, 2, 1, document.Model(Floor)) });
        Assert.Equal(Header(40, 3, 1) + "\n[chunk 33 1 1]\n40,2: " + Floor + "\n", document.ToText());
        document.Edit("Space", new[] { (40, 2, 1, document.Default) });
        Assert.Equal(Header(40, 3, 1), document.ToText());
    }

    [Fact]
    public void Chunks_are_written_by_level_then_y_then_x_and_lines_by_y_down_then_x()
    {
        MapDocument document = MapDocument.Create(40, 40, 2, Space);
        MapModel floor = document.Model(Floor);
        document.Edit("Floors", new[] { (1, 1, 2, floor), (40, 40, 1, floor), (2, 5, 1, floor), (1, 5, 1, floor), (1, 6, 1, floor), (33, 1, 1, floor), (1, 33, 1, floor) });
        string expected = Header(40, 40, 2)
            + "\n[chunk 1 1 1]\n1,6: " + Floor + "\n1,5: " + Floor + "\n2,5: " + Floor + "\n"
            + "\n[chunk 33 1 1]\n33,1: " + Floor + "\n"
            + "\n[chunk 1 33 1]\n1,33: " + Floor + "\n"
            + "\n[chunk 33 33 1]\n40,40: " + Floor + "\n"
            + "\n[chunk 1 1 2]\n1,1: " + Floor + "\n";
        Assert.Equal(expected, document.ToText());
        Assert.Equal(7, MapText.Parse(document.ToText(), "written").Lines.Count()); // the reader takes what the document writes
    }

    [Fact]
    public void Undo_and_redo_restore_the_file_and_the_saved_state()
    {
        string text = Fixture();
        MapDocument document = MapDocument.Parse(text, "small");
        Assert.False(document.CanUndo);
        document.Edit("Clear a tile", new[] { (2, 4, 1, document.Default) });
        document.Edit("Floor two tiles", new[] { (1, 1, 1, document.Model(Floor)), (5, 1, 1, document.Model(Floor)) });
        string edited = document.ToText();
        Assert.NotEqual(text, edited);
        Assert.True(document.IsDirty);
        Assert.Equal("Floor two tiles", document.UndoLabel);

        Assert.True(document.Undo());
        Assert.True(document.Undo());
        Assert.False(document.Undo());
        Assert.Equal(text, document.ToText());
        Assert.False(document.IsDirty);
        Assert.Equal("Clear a tile", document.RedoLabel);

        Assert.True(document.Redo());
        Assert.True(document.Redo());
        Assert.False(document.Redo());
        Assert.Equal(edited, document.ToText());

        document.MarkSaved();
        Assert.False(document.IsDirty);
        document.Undo();
        Assert.True(document.IsDirty);
        document.Edit("Another", new[] { (3, 1, 1, document.Model(Floor)) }); // a new step forgets the undone one
        Assert.False(document.CanRedo);
    }

    [Fact]
    public void An_open_step_reports_each_tile_and_is_undone_as_one()
    {
        MapDocument document = MapDocument.Create(8, 8, 1, Space);
        MapModel floor = document.Model(Floor);
        var seen = new List<MapTileChange>();
        document.Changed += changes => seen.AddRange(changes);

        document.Begin("Drag");
        Assert.True(document.InStep);
        Assert.True(document.Set(1, 1, 1, floor));
        Assert.False(document.Set(1, 1, 1, floor)); // it already has it
        Assert.True(document.Set(2, 1, 1, floor));
        Assert.True(document.Set(2, 1, 1, document.Default)); // back to what it was: no change of that tile in the end
        Assert.Equal(3, seen.Count);
        Assert.True(document.End());

        Assert.True(document.Undo());
        Assert.Same(document.Default, document[1, 1, 1]);
        Assert.Equal(4, seen.Count); // the undo reports only the tile the step really changed
        Assert.Equal(new MapTileChange(1, 1, 1, floor, document.Default), seen[^1]);

        document.Begin("Nothing");
        document.Set(3, 3, 1, floor);
        document.Set(3, 3, 1, document.Default);
        Assert.False(document.End()); // a step with no change in the end leaves no trace
        Assert.False(document.CanUndo);
        Assert.Equal("Drag", document.RedoLabel);
    }

    [Fact]
    public void An_edit_with_a_tile_outside_the_map_changes_nothing()
    {
        MapDocument document = MapDocument.Create(4, 4, 1, Space);
        MapModel floor = document.Model(Floor);
        int raised = 0;
        document.Changed += _ => raised++;
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Edit("Bad", new[] { (1, 1, 1, floor), (5, 1, 1, floor) }));
        Assert.Same(document.Default, document[1, 1, 1]);
        Assert.False(document.InStep);
        Assert.False(document.CanUndo);
        Assert.False(document.IsDirty);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void Models_are_shared_by_their_text_and_a_bad_member_list_is_refused()
    {
        MapDocument document = MapDocument.Create(4, 4, 1, Space);
        Assert.Same(document.Model(Floor), document.Model(Floor));
        Assert.Same(document.Default, document.Model(Space));
        Assert.Equal(2, document.Model(Floor).Members.Count);
        Assert.Throws<MapTextException>(() => document.Model("floor | /area/space"));
        Assert.Throws<MapTextException>(() => document.Model(Floor + " "));
        Assert.Throws<MapTextException>(() => document.Model("/obj/item/crowbar{name = \"a\nb\"} | " + Floor));
        Assert.Throws<MapTextException>(() => document.Model(""));
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(256, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 256, 1)]
    [InlineData(1, 1, 0)]
    [InlineData(1, 1, 65)]
    public void A_new_map_keeps_to_the_formats_sizes(int width, int height, int levels)
        => Assert.Throws<ArgumentOutOfRangeException>(() => MapDocument.Create(width, height, levels, Space));

    [Fact]
    public void A_new_map_is_a_header_the_reader_takes()
    {
        string text = MapDocument.Create(255, 255, 16, Space).ToText();
        Assert.Equal(Header(255, 255, 16), text);
        ParsedMapText parsed = MapText.Parse(text, "new");
        Assert.Equal((255, 255, 16), (parsed.Width, parsed.Height, parsed.Levels));
        Assert.Null(parsed.Source);
    }

    [Fact]
    public void The_history_forgets_its_oldest_step_past_the_limit()
    {
        MapDocument document = MapDocument.Create(4, 4, 1, Space);
        MapModel floor = document.Model(Floor);
        for (int i = 0; i <= MapDocument.HistoryLimit; i++)
            document.Edit("Step " + i, new[] { (1, 1, 1, i % 2 == 0 ? floor : document.Default) });
        int undone = 0;
        while (document.Undo()) undone++;
        Assert.Equal(MapDocument.HistoryLimit, undone);
        Assert.Same(floor, document[1, 1, 1]); // the first step can no longer be taken back
        Assert.True(document.IsDirty);
    }

    // What is kept beside the tiles (the map's settings) has its steps in the same history (docs/ai/specs/map-editor.md 5.10).
    [Fact]
    public void A_recorded_step_is_taken_back_and_done_again_among_the_tiles_steps()
    {
        MapDocument document = MapDocument.Create(4, 4, 1, Space);
        MapModel floor = document.Model(Floor);
        int sun = 1;
        document.Edit("Floor", new[] { (1, 1, 1, floor) });
        sun = 2;
        document.Record("Sun", () => sun = 1, () => sun = 2);
        document.Edit("Floor 2", new[] { (2, 1, 1, floor) });
        Assert.True(document.IsDirty);
        Assert.Equal("Floor 2", document.UndoLabel);

        Assert.True(document.Undo()); // the second floor
        Assert.Equal(2, sun);
        Assert.Equal("Sun", document.UndoLabel);
        Assert.True(document.Undo()); // the setting
        Assert.Equal(1, sun);
        Assert.Same(floor, document[1, 1, 1]);
        Assert.True(document.Redo());
        Assert.Equal(2, sun);
        Assert.True(document.Undo() && document.Undo()); // the setting and the first floor
        Assert.False(document.IsDirty);

        // a recorded step alone makes the document one to save, and saving it one that is saved
        Assert.True(document.Redo() && document.Redo());
        document.MarkSaved();
        Assert.False(document.IsDirty);
        Assert.True(document.Undo());
        Assert.True(document.IsDirty);
    }

    [Fact]
    public void Steps_of_one_key_that_follow_closely_are_one_step()
    {
        MapDocument document = MapDocument.Create(4, 4, 1, Space);
        double energy = 1;
        void Set(double to, double at)
        {
            double was = energy;
            energy = to;
            document.Record("Energy", () => energy = was, () => energy = to, "energy", at);
        }

        // a slider dragged from 1 to 4 within a second, then moved again later, then another setting
        Set(2, 10.0);
        Set(3, 10.3);
        Set(4, 10.9);
        Set(5, 20.0);
        int size = 1;
        document.Record("Size", () => size = 1, () => size = 2, "size", 20.1);
        size = 2;

        Assert.True(document.Undo());
        Assert.Equal(1, size);
        Assert.True(document.Undo());
        Assert.Equal(4, energy); // the later move
        Assert.True(document.Undo());
        Assert.Equal(1, energy); // the whole drag
        Assert.False(document.Undo());
        Assert.True(document.Redo());
        Assert.Equal(4, energy);

        // a step after a save does not join the saved one: the document must say it has changed
        document.MarkSaved();
        Set(6, 30.0);
        Set(7, 30.1);
        Assert.True(document.IsDirty);
        Assert.True(document.Undo());
        Assert.Equal(4, energy);
        Assert.False(document.IsDirty);
    }
}
