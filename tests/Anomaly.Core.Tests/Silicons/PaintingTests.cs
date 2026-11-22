using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Silicons;

/// <summary>
/// Silicons SI4d (D326): the paintings. A crayon paints a canvas pixel by pixel and its bucket fills; a colour the crayon does not hold is
/// refused; a finished painting is tg's PNG, which the world takes into its runtime resources (protocol 79) and the canvas shows; a framed
/// painting in a library frame is archived at the round's end and hangs in a library frame of the next round; the AI picks an archived
/// 24 x 24 painting as its portrait; the palette's colour wheel adds and picks colours.
/// </summary>
public class PaintingTests
{
    private const int Second = 20;

    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(3, 5, "Bob");
        r.Step(Second);
        return (r, bob);
    }

    private static DmList Transaction(string type, string colour, int x, int y)
    {
        DmList t = Dm.list(Dm.kv("type", type), Dm.kv("name", type), Dm.kv("layer", 1), Dm.kv("dir", "2"), Dm.kv("color", colour));
        if (type == "bucket")
            t["point"] = Dm.list(x, y);
        else
            t["points"] = Dm.list(Dm.list(x, y));
        return Dm.list(Dm.kv("command", "transaction"), Dm.kv("transaction", t));
    }

    private static void Act(AudioRoom r, mob user, obj_item_canvas canvas, string action, DmList @params) => r.In(() =>
    {
        World.Current.usr = user;
        Waiting.NoSleep(canvas.ui_act(action, @params, Dm.@new<datum_tgui>(user, canvas, "Canvas"), null));
    });

    private static string Pixel(AudioRoom r, obj_item_canvas canvas, int x, int y)
        => r.In(() => Dm.str(((DmList)canvas.workspace.get_first_layer_pixel_data(SOUTH)[y])[x]));

    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(obj_item_canvas), typeof(obj_item_canvas_nineteen_nineteen), typeof(obj_item_canvas_twentyfour_twentyfour), typeof(obj_item_canvas_fortyfive_twentyseven),
            typeof(obj_structure_easel), typeof(obj_structure_sign_painting), typeof(obj_structure_sign_painting_library), typeof(obj_structure_sign_painting_large),
            typeof(obj_item_paint_palette), typeof(obj_item_wallframe_painting), typeof(datum_sprite_editor_workspace), typeof(datum_painting),
            typeof(datum_controller_subsystem_persistent_paintings), typeof(datum_portrait_picker), typeof(datum_component_palette),
            typeof(datum_computer_file_program_portrait_printer),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void ACrayonPaintsTheCanvasAndItsBucketFills()
    {
        var (r, bob) = Room(9601);
        var canvas = r.In(() => Dm.@new<obj_item_canvas>(bob.loc));
        var crayon = r.In(() => Dm.@new<obj_item_toy_crayon_red>(bob.loc));
        r.In(() => Waiting.NoSleep(bob.put_in_hands(crayon)));
        string red = r.In(() => Dm.str(crayon.paint_color)).ToLowerInvariant() + "ff";
        Assert.Equal("#ffffffff", Pixel(r, canvas, 1, 1));

        Act(r, bob, canvas, "spriteEditorCommand", r.In(() => Transaction("pencil", red, 0, 0)));
        Assert.Equal(red, Pixel(r, canvas, 1, 1));
        Assert.Equal("#ffffffff", Pixel(r, canvas, 2, 1));
        Assert.Equal("Crayon on canvas", r.In(() => Dm.str(canvas.painting_metadata.medium)));

        // a colour the crayon does not hold is refused (validate_color)
        Act(r, bob, canvas, "spriteEditorCommand", r.In(() => Transaction("pencil", "#00ff00ff", 3, 3)));
        Assert.Equal("#ffffffff", Pixel(r, canvas, 4, 4));

        // the bucket fills every white pixel joined to the clicked one
        Act(r, bob, canvas, "spriteEditorCommand", r.In(() => Transaction("bucket", red, 5, 5)));
        Assert.Equal(red, Pixel(r, canvas, 11, 11));
        Assert.Equal(red, Pixel(r, canvas, 6, 6));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFinishedPaintingIsAPngTheWorldSendsAsARuntimeResource()
    {
        var (r, bob) = Room(9602);
        var canvas = r.In(() => Dm.@new<obj_item_canvas>(bob.loc));
        var crayon = r.In(() => Dm.@new<obj_item_toy_crayon_red>(bob.loc));
        r.In(() => Waiting.NoSleep(bob.put_in_hands(crayon)));
        string red = r.In(() => Dm.str(crayon.paint_color)).ToLowerInvariant();
        Act(r, bob, canvas, "spriteEditorCommand", r.In(() => Transaction("pencil", red + "ff", 0, 0)));
        r.In(() => Waiting.NoSleep(canvas.generate_proper_overlay()));
        r.In(() =>
        {
            Assert.True(Dm.truthy(canvas.icon_generated));
            string path = (canvas.generated_icon.dm_file as DmResource)?.Path;
            Assert.NotNull(path);
            Assert.StartsWith("rsc/", path);
            DmWorldState state = DmWorldState.Current;
            Assert.Contains(path, state.RuntimeResourceOrder);
            (int w, int h, uint[] rgba) = DmPng.Decode(state.RuntimeResources[path]);
            Assert.Equal((11, 11), (w, h));
            // the top left pixel is the crayon's, read back as BYOND's GetPixel (1, 1 is the lower left)
            Assert.Equal(red, Dm.str(canvas.generated_icon.GetPixel(1, 11)));
            Assert.Equal("#ffffff", Dm.str(canvas.generated_icon.GetPixel(2, 11)));
            Assert.True(state.Files.ReadBytes("data/paintings/temp_painting.png")?.Length > 0);
            Assert.Equal(Dm.md5(LOWER_TEXT(canvas.get_data_string())), canvas.painting_metadata.md5);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFramedPaintingIsArchivedAtTheRoundsEndAndHangsInTheNextRound()
    {
        var (r, bob) = Room(9603);
        var canvas = r.In(() => Dm.@new<obj_item_canvas_twentyfour_twentyfour>(bob.loc));
        var crayon = r.In(() => Dm.@new<obj_item_toy_crayon_red>(bob.loc));
        var frame = r.In(() => Dm.@new<obj_structure_sign_painting_library>(locate(4, 5, 1)));
        r.In(() => Waiting.NoSleep(bob.put_in_hands(crayon)));
        string red = r.In(() => Dm.str(crayon.paint_color)).ToLowerInvariant();
        Act(r, bob, canvas, "spriteEditorCommand", r.In(() => Transaction("pencil", red + "ff", 2, 3)));
        r.In(() =>
        {
            canvas.painting_metadata.title = "Red Dot";
            Waiting.NoSleep(canvas.generate_proper_overlay());
            canvas.finalized = TRUE;
            Waiting.NoSleep(bob.put_in_hands(canvas));
            Assert.Equal(TRUE, Waiting.NoSleep(frame.frame_canvas(bob, canvas)));
            Assert.Same(canvas, frame.current_canvas);
            Assert.Equal("painting - Red Dot", frame.name);
            SSpersistent_paintings.save_paintings();
        });
        string json = r.In(() => DmWorldState.Current.Files.ReadText("data/paintings.json"));
        Assert.Contains("Red Dot", json);
        Assert.Contains("library", json);

        // the next round: the subsystem loads the archive and a library frame hangs it
        var next = r.In(() => Dm.@new<obj_structure_sign_painting_library>(locate(6, 5, 1)));
        r.In(() =>
        {
            SSpersistent_paintings.paintings.Cut();
            Waiting.NoSleep(SSpersistent_paintings.Initialize());
            Assert.NotNull(next.current_canvas);
            Assert.Equal("painting - Red Dot", next.current_canvas.name);
            Assert.Equal(red, Dm.str(((DmList)next.current_canvas.workspace.get_first_layer_pixel_data(SOUTH)[4])[3]));
            Assert.True(Dm.truthy(next.current_canvas.finalized));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAiPicksAnArchivedPortrait()
    {
        var (r, bob) = Room(9604);
        r.In(() =>
        {
            (get_area(locate(5, 5, 1)) as area).requires_power = FALSE;
            Waiting.NoSleep(config.Set(typeof(datum_config_entry_flag_allow_ai), TRUE));
            SSjob.setup_occupations();
        });
        var painting = r.In(() =>
        {
            var canvas = Dm.@new<obj_item_canvas_twentyfour_twentyfour>(bob.loc);
            Waiting.NoSleep(canvas.generate_proper_overlay());
            string png = Dm.text("data/paintings/images/[].png", canvas.painting_metadata.md5);
            DmWorldState.Current.Files.WriteBytes(png, DmWorldState.Current.Files.ReadBytes("data/paintings/temp_painting.png"));
            canvas.painting_metadata.title = "Self";
            SSpersistent_paintings.paintings.Add(canvas.painting_metadata);
            SSpersistent_paintings.cache_paintings(); // as the subsystem's Initialize and save do
            return canvas.painting_metadata;
        });
        r.In(() => Waiting.Detach(bob.AIize(bob.client, FALSE)));
        r.Step(Second);
        var ai = r.In(() => Dm.each<mob_living_silicon_ai>(GLOB.ai_list).Single());
        r.In(() =>
        {
            var picker = Dm.@new<datum_portrait_picker>(ai);
            var ui = Dm.@new<datum_tgui>(ai, picker, "PortraitPicker");
            DmList data = Waiting.NoSleep(picker.ui_data(ai)) as DmList;
            Assert.Equal(1, Dm.length(data["paintings"] as DmList)); // a 24 x 24 painting is a portrait
            World.Current.usr = ai;
            Waiting.NoSleep(picker.ui_act("select", Dm.list(Dm.kv("selected", REF(painting))), ui, null));
            Assert.NotNull(ai.portrait_appearance);
            Assert.Equal(4f, ai.portrait_appearance.pixel_w);
            Assert.Equal("ai-portrait", Dm.str(ai.display_icon_override));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePalettesColourWheelAddsAndPicksColours()
    {
        var (r, bob) = Room(9605);
        var palette = r.In(() => Dm.@new<obj_item_paint_palette>(bob.loc));
        r.In(() => Waiting.NoSleep(bob.put_in_hands(palette)));
        var comp = r.In(() => palette.GetComponent(typeof(datum_component_palette)) as datum_component_palette);
        Assert.NotNull(comp);
        r.In(() =>
        {
            comp.choice_selected(bob, "add", "");
            Assert.Equal(new object[] { "#000000" }, Dm.each(comp.colors).ToArray());
            palette.set_painting_tool_color("#123456");
            comp.choice_selected(bob, "add", "");
            comp.choice_selected(bob, "1", "");
            Assert.Equal("#000000", palette.current_color);
            comp.choice_selected(bob, "1", "right=1");
            Assert.Equal(new object[] { "#123456" }, Dm.each(comp.colors).ToArray());
        });
        r.AssertNoRuntimes();
    }
}
