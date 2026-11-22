using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Engineering;

/// <summary>
/// Engineering package E14: the airlock painter (airlock_painter.dm) with its toner, the airlock's and the closet's paint jobs
/// (airlock.dm:1497, closets.dm:699), the decal painter (decal_painter.dm) with tg's categories (paintable_decals.dm) and its window's
/// data and acts, in a booted toy room. A click with the painter on an airlock asks tg's paint job list and gives the airlock that
/// job's icon, overlays and assembly for one charge of ink; a glass airlock refuses a job with no glass version; an empty painter says so;
/// the alt-click takes the cartridge out and a cartridge goes back in. The decal painter paints its chosen decal on a floor as the
/// decal element's overlay (tg's colour and alpha rules: the RGBA tile colours, the warning stripes' state suffix, the 8-dir states).
/// </summary>
public class PaintersTests
{
    private const int Second = 20;

    private sealed class Feed : IMessageSink
    {
        public List<(object Target, string Html)> Chat { get; } = new();

        void IMessageSink.Chat(object target, DmList message) => Chat.Add((target, Dm.str(message?["html"])));

        public void Output(object target, string text) { }

        public void Balloon(object viewer, object atom, string text) => Chat.Add((viewer, "balloon: " + text));

        public bool Any(string text) => Chat.Any(c => c.Html != null && c.Html.Contains(text, StringComparison.Ordinal));
    }

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Bob, Feed Feed);

    private static Rig Room(ulong seed)
    {
        var r = new AudioRoom(11, 11, seed: seed);
        var feed = new Feed();
        PresentationSinks.Of(r.World).Messages = feed;
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return new Rig(r, bob, feed);
    }

    private static T Hold<T>(Rig g) where T : obj_item => g.R.In(() =>
    {
        var item = Dm.@new<T>(g.Bob.loc);
        Waiting.NoSleep(g.Bob.put_in_hands(item));
        return item;
    });

    private static void Click(Rig g, atom target, string mods = "icon-x=16;icon-y=16;left=1;button=left")
    {
        g.R.Command(() => ClientSeam.Click(g.Bob.client, target, mods));
        g.R.Step(4);
    }

    private static void Answer(Rig g, string iface, object entry)
    {
        string window = g.R.In(() => Dm.str(Dm.each_cast<datum_tgui>(g.Bob.tgui_open_uis).Last(u => Dm.str(u.@interface) == iface).window.id));
        g.R.Command(() => ClientSeam.TguiMessage(g.Bob.client, window, "act/submit", Dm.list(Dm.kv("entry", entry))));
        g.R.Step(4);
    }

    private static object Act(Rig g, obj_item_airlock_painter_decal p, string action, params (string Key, object Value)[] args) => g.R.In(() =>
    {
        usr = g.Bob;
        datum_tgui ui = (datum_tgui)SStgui.get_open_ui(g.Bob, p);
        if (ui == null)
        {
            Waiting.NoSleep(p.ui_interact(g.Bob));
            ui = (datum_tgui)SStgui.get_open_ui(g.Bob, p);
        }
        DmList parameters = Dm.list();
        foreach ((string key, object value) in args)
            parameters[key] = value;
        return Waiting.NoSleep(p.ui_act(action, parameters, ui, null));
    });

    // the decal overlays of a turf: (icon_state, dir, color, alpha)
    private static List<(string State, float Dir, string Color, float Alpha)> Decals(Rig g, turf t) => g.R.In(() =>
        Dm.each(t.overlays).OfType<IDmAppearance>().Where(o => Dm.eq(o.icon, Dm.resource("icons/turf/decals.dmi")))
            .Select(o => (o.icon_state, o.dir, Dm.str(o.color), o.alpha)).ToList());

    [Fact]
    public void AnAirlockTakesThePaintJobChosenFromTgsListForOneCharge()
    {
        Rig g = Room(14101);
        var airlock = g.R.In(() => Dm.@new<obj_machinery_door_airlock>(locate(5, 4, 1)));
        g.R.Step(Second);
        var painter = Hold<obj_item_airlock_painter>(g);
        g.R.In(() =>
        {
            Assert.IsType<obj_item_toner>(painter.ink);
            Assert.Same(painter, painter.ink.loc);
            Assert.Equal(5f, painter.ink.charges);
        });

        Click(g, airlock);
        DmList items = g.R.In(() => ((datum_tgui_list_input)Dm.each_cast<datum_tgui>(g.Bob.tgui_open_uis).Last(u => Dm.str(u.@interface) == "ListInputWindow").src_object).items);
        string[] jobs = g.R.In(() => Dm.each(items).Select(Dm.str).ToArray());
        Assert.Equal(16, jobs.Length);
        Assert.Equal(jobs.OrderBy(j => j, StringComparer.Ordinal).ToArray(), jobs); // sort_list
        Answer(g, "ListInputWindow", "Engineering");
        g.R.In(() =>
        {
            Assert.Equal(Dm.resource("icons/obj/doors/airlocks/station/engineering.dmi"), airlock.icon);
            Assert.Equal(Dm.resource("icons/obj/doors/airlocks/station/overlays.dmi"), airlock.overlays_file);
            Assert.Equal(typeof(obj_structure_door_assembly_door_assembly_eng), airlock.assemblytype);
            Assert.Equal(4f, painter.ink.charges);
        });
        Assert.NotEmpty(g.R.Sounds.Of(g.Bob, "sound/effects/spray2.ogg"));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AGlassAirlockRefusesAJobWithoutGlassAndAnEmptyPainterSaysSo()
    {
        Rig g = Room(14102);
        var airlock = g.R.In(() => Dm.@new<obj_machinery_door_airlock_glass>(locate(5, 4, 1)));
        g.R.Step(Second);
        var painter = Hold<obj_item_airlock_painter>(g);
        Assert.Equal("glass", g.R.In(() => Dm.str(airlock.airlock_material)));
        Click(g, airlock);
        Answer(g, "ListInputWindow", "Freezer");
        Assert.True(g.Feed.Any("This paint job can only be applied to non-glass airlocks."));
        g.R.In(() =>
        {
            Assert.Equal(Dm.initial(typeof(obj_machinery_door_airlock_glass), (obj_machinery_door_airlock p) => p.icon), airlock.icon);
            Assert.Equal(5f, painter.ink.charges);
        });

        // the alt-click takes the cartridge out into the other hand; the painter then cannot paint
        Click(g, painter, "icon-x=16;icon-y=16;alt=1;button=left");
        obj_item_toner ink = g.R.In(() =>
        {
            Assert.Null(painter.ink);
            var held = Dm.each_cast<obj_item>(g.Bob.held_items).OfType<obj_item_toner>().Single();
            return held;
        });
        Assert.True(g.Feed.Any("You remove the toner cartridge from the airlock painter."));
        g.R.In(() => Assert.Equal(FALSE, Dm.num(painter.can_use(g.Bob))));
        Assert.True(g.Feed.Any("balloon: no cartridge!"));

        // a used-up cartridge: out of ink
        g.R.In(() =>
        {
            ink.charges = 0;
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(painter.item_interaction(g.Bob, ink, Dm.list())));
            Assert.Same(ink, painter.ink);
            Assert.Equal(FALSE, Dm.num(painter.can_use(g.Bob)));
        });
        Assert.True(g.Feed.Any("You install the toner cartridge into the airlock painter."));
        Assert.True(g.Feed.Any("balloon: out of ink!"));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AClosetTakesItsPaintJob()
    {
        Rig g = Room(14103);
        var closet = g.R.In(() => Dm.@new<obj_structure_closet>(locate(5, 4, 1)));
        g.R.Step(Second);
        var painter = Hold<obj_item_airlock_painter>(g);
        Click(g, closet);
        Answer(g, "ListInputWindow", "Radiation");
        g.R.In(() =>
        {
            Assert.Equal(("eng", "eng", "eng_rad"), (closet.icon_state, Dm.str(closet.base_icon_state), Dm.str(closet.icon_door)));
            Assert.Equal(4f, painter.ink.charges);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheDecalPainterPaintsItsChosenDecalAsTheFloorsOverlay()
    {
        Rig g = Room(14104);
        turf floor = g.R.In(() =>
        {
            var t = (turf)locate(5, 4, 1);
            Waiting.NoSleep(t.ChangeTurf(typeof(turf_open_floor_iron)));
            return (turf)locate(5, 4, 1);
        });
        g.R.Step(Second);
        var painter = Hold<obj_item_airlock_painter_decal>(g);
        g.R.In(() =>
        {
            Assert.IsType<obj_item_toner_large>(painter.ink);
            // GLOB.paintable_decals holds tg's six categories; the painter starts in the first, on its first colour and decal
            Assert.Equal(new[] { "Tiles", "Trimlines", "Warning Stripes", "Colored Sidings", "Normal Sidings", "Plating Sidings" },
                Dm.each_cast<datum_paintable_decal_category>(GLOB.paintable_decals).Select(c => c.category).ToArray());
        });

        // the window's data, as DecalPainter.tsx reads it
        DmList statics = g.R.In(() => (DmList)Waiting.NoSleep(painter.ui_static_data(g.Bob)));
        Assert.Equal("paintable_decals32x32", statics["icon_prefix"]);
        DmList tiles = (DmList)((DmList)statics["categories"])[1];
        Assert.Equal("Tiles", tiles["category"]);
        DmList first = (DmList)((DmList)tiles["decal_list"])[1];
        Assert.Equal(("4 Corners", "tile_fourcorners", 0f), (Dm.str(first["name"]), Dm.str(first["icon_state"]), Dm.num(first["directional"])));
        Assert.Equal(14f, Dm.length(tiles["color_list"]));
        Assert.Equal(4f, Dm.length(tiles["dir_list"]));
        DmList data = g.R.In(() => (DmList)Waiting.NoSleep(painter.ui_data(g.Bob)));
        Assert.Equal(("tile_fourcorners", "#d4d4d432", 2f, "Tiles"), (Dm.str(data["current_decal"]), Dm.str(data["current_color"]), Dm.num(data["current_dir"]), Dm.str(data["active_category"])));

        // the neutral tile colour carries its alpha (get_decal_info's RGBA rule): #d4d4d4 at 0x32
        Click(g, floor);
        Assert.Contains(("tile_fourcorners", 2f, "#d4d4d4", 50f), Decals(g, floor));
        Assert.Equal(24f, g.R.In(() => painter.ink.charges));

        // the warning stripes: a red stripe facing east is the "_red" state, uncoloured, opaque
        Assert.Equal(TRUE, Dm.num(Act(g, painter, "select_category", ("category", "Warning Stripes"))));
        DmList warn = g.R.In(() => (DmList)Waiting.NoSleep(painter.ui_data(g.Bob)));
        Assert.Equal(("warningline", "yellow"), (Dm.str(warn["current_decal"]), Dm.str(warn["current_color"])));
        Act(g, painter, "select_color", ("color", "red"));
        Act(g, painter, "select_decal", ("decal", "warningline"), ("dir", "4"));
        Act(g, painter, "select_color", ("color", "#123456")); // not one of the category's colours: ignored
        Act(g, painter, "select_decal", ("decal", "tile_full"), ("dir", "5")); // neither the state nor the dir is the category's
        Click(g, floor);
        Assert.Contains(("warningline_red", 4f, "red", 255f), Decals(g, floor));

        // an 8-dir trimline: the "__8" state turns its dir by 45 degrees (paintable_decals.dm:149)
        g.R.In(() =>
        {
            var trim = Dm.each_cast<datum_paintable_decal_category>(GLOB.paintable_decals).Single(c => c.category == "Trimlines");
            DmList info = trim.get_decal_info("trimline_fill__8", "#FFFFFF", NORTH) as DmList;
            Assert.Equal(("trimline_fill", 9f, "#FFFFFF", 110f), (Dm.str(info["icon_state"]), Dm.num(info["dir"]), Dm.str(info["color"]), Dm.num(info["alpha"])));
            Assert.Equal("trimline_fill__8_1_FFFFFF", trim.spritesheet_key(NORTH, "trimline_fill__8", "#FFFFFF"));
        });
        g.R.AssertNoRuntimes();
    }
}
