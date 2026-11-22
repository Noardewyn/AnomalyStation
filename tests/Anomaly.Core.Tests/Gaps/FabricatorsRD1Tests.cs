using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package RD1 (D333, the fabricators) in a booted toy room with Bob as a player: a protolathe linked to the station's techweb and,
/// by a multitool, to the ore silo takes iron into the silo (its insertion animation), opens tg's Fabricator window and prints a
/// researched design from the silo's iron (the printing picture and sound, the materials spent, the items out); the departmental
/// techfabs and the circuit imprinter offer only their departments' and build types' designs (with the department stripe); the
/// autolathe takes iron sheets, prints from its Autolathe window and its wires hack, disable and shock it; no fabricator offers a
/// design whose item is inert (DV384).
/// </summary>
public class FabricatorsRD1Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        // the toy room has no APC: free power
        r.In(() => (get_area(locate(5, 5, 1)) as area).requires_power = FALSE);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who, params object[] args) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(new object[] { who.loc }.Concat(args).ToArray()));
        r.Step(2);
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    private static void EmptyHands(AudioRoom r, mob_living_carbon_human who) => r.In(() =>
    {
        foreach (obj_item held in Dm.each<obj_item>(who.held_items))
            qdel(held);
    });

    // a left click after tg's click cooldown of the one before
    private static void Use(AudioRoom r, mob who, atom target)
    {
        r.Step(Second);
        Click(r, who, target);
    }

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    private static List<string> Flicks(AudioRoom r, atom a) => r.In(() => Dm.each<atom_movable_flick_visual>(a.vis_contents).Select(v => v.icon_state).ToList());

    private static List<Type> Offered(AudioRoom r, obj_machinery_rnd_production fab) => r.In(() => Dm.each<datum_design>(fab.cached_designs).Select(d => d.type).ToList());

    private static datum_techweb_science Science(AudioRoom r) => r.In(() => Dm.each<datum_techweb>(SSresearch.techwebs).OfType<datum_techweb_science>().Single());

    private static datum_material Iron(AudioRoom r) => r.In(() => (datum_material)Waiting.NoSleep(SSmaterials.get_material(typeof(datum_material_iron))));

    private static string OpenWindow(AudioRoom r, mob who, atom machine, out JsonElement statics, out JsonElement data)
    {
        Take(who);
        Use(r, who, machine);
        JsonElement payload = LastUpdate(Take(who), out string window);
        statics = payload.GetProperty("static_data");
        data = payload.GetProperty("data");
        return InterfaceOf(payload) + "|" + window;
    }

    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(obj_machinery_rnd), typeof(obj_machinery_rnd_production), typeof(obj_machinery_rnd_production_protolathe),
            typeof(obj_machinery_rnd_production_protolathe_offstation), typeof(obj_machinery_rnd_production_protolathe_department_engineering),
            typeof(obj_machinery_rnd_production_protolathe_department_science), typeof(obj_machinery_rnd_production_circuit_imprinter),
            typeof(obj_machinery_rnd_production_circuit_imprinter_offstation), typeof(obj_machinery_rnd_production_circuit_imprinter_department_science),
            typeof(obj_machinery_rnd_production_techfab), typeof(obj_machinery_rnd_production_techfab_department_cargo),
            typeof(obj_machinery_rnd_production_techfab_department_medical), typeof(obj_machinery_rnd_production_techfab_department_security),
            typeof(obj_machinery_rnd_production_techfab_department_service), typeof(obj_machinery_autolathe), typeof(obj_machinery_autolathe_hacked),
            typeof(datum_wires_autolathe), typeof(datum_wires_rnd),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void AProtolatheTakesSiloIronAndPrintsAResearchedDesignFromItsWindow()
    {
        var (r, feed, bob) = Room(7601);
        var silo = Place<obj_machinery_ore_silo>(r, 8, 6);
        var lathe = Place<obj_machinery_rnd_production_protolathe>(r, 5, 4);

        // the station's techweb: its researched protolathe designs, but not the circuit imprinter's
        Assert.Same(Science(r), r.In(() => lathe.stored_research));
        List<Type> offered = Offered(r, lathe);
        Assert.Contains(typeof(datum_design_basic_matter_bin), offered);
        Assert.Contains(typeof(datum_design_crowbar), offered);
        Assert.DoesNotContain(typeof(datum_design_board_circuit_imprinter), offered);
        Assert.Equal("protolathe", r.In(() => lathe.icon_state));
        Assert.Equal(1.0, r.In(() => lathe.efficiency_coeff), 4); // two micro servos: 1.2 - 0.2

        // a multitool carries the silo to the protolathe
        var multitool = Hold<obj_item_multitool>(r, bob);
        r.In(() => multitool.set_buffer(silo));
        Use(r, bob, lathe);
        Assert.Same(silo, r.In(() => lathe.materials.silo));

        // iron sheets go into the silo through it, with tg's insertion animation over the protolathe
        EmptyHands(r, bob);
        Hold<obj_item_stack_sheet_iron>(r, bob, 10);
        Use(r, bob, lathe);
        datum_material iron = Iron(r);
        Assert.Equal(10f * SHEET_MATERIAL_AMOUNT, r.In(() => Waiting.NoSleep(silo.materials.get_material_amount(iron))));
        List<string> flicks = Flicks(r, lathe);
        Assert.Contains("material_insertion", flicks);
        Assert.Contains("protolathe_progress", flicks);

        // tg's Fabricator window: the designs with their costs and the silo's iron
        EmptyHands(r, bob);
        string opened = OpenWindow(r, bob, lathe, out JsonElement statics, out JsonElement data);
        Assert.StartsWith("Fabricator|", opened);
        string window = opened["Fabricator|".Length..];
        Assert.Equal("protolathe", statics.GetProperty("fabName").GetString());
        JsonElement bin = statics.GetProperty("designs").GetProperty("/datum/design/basic_matter_bin");
        Assert.Equal("Basic Matter Bin", bin.GetProperty("name").GetString());
        Assert.Equal(10, bin.GetProperty("cost").GetProperty("iron").GetDouble()); // SMALL_MATERIAL_AMOUNT
        Assert.Equal("basic_matter_bin", bin.GetProperty("icon").GetString());
        Assert.Contains(data.GetProperty("materials").EnumerateArray(), m => m.GetProperty("name").GetString() == "iron" && m.GetProperty("amount").GetDouble() == 1000);
        Assert.Equal(0, data.GetProperty("busy").GetDouble());

        // two matter bins: the protolathe prints them one after the other, its printing picture and sound on
        Act(r, bob, window, "build", ("design_path", "/datum/design/basic_matter_bin"), ("amount", 2));
        r.In(() =>
        {
            Assert.True(Dm.truthy(lathe.busy));
            Assert.Equal("protolathe_n", lathe.icon_state);
            Assert.True(Dm.truthy(lathe.print_sound.is_active()));
        });
        r.Step(Second);
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("lathe_print") == true);
        for (int i = 0; i < 10 && r.In(() => Dm.truthy(lathe.busy)); i++)
            r.Step(Second);
        r.In(() =>
        {
            Assert.False(Dm.truthy(lathe.busy));
            Assert.Equal("protolathe", lathe.icon_state);
            Assert.False(Dm.truthy(lathe.print_sound.is_active()));
            Assert.Equal(2, Dm.each<obj_item_stock_parts_matter_bin>(lathe.loc).Count());
            Assert.Equal(10f * SHEET_MATERIAL_AMOUNT - 2 * 10, Waiting.NoSleep(silo.materials.get_material_amount(iron)));
        });

        // a design the protolathe cannot build is refused
        Act(r, bob, window, "build", ("design_path", "/datum/design/board/circuit_imprinter"), ("amount", 1));
        Assert.True(feed.Any("does not have the necessary manipulation systems"), "the protolathe says it cannot build an imprinter's design");
        Assert.False(r.In(() => Dm.truthy(lathe.busy)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void DepartmentalFabricatorsOfferTheirDepartmentsDesignsAndShowTheirStripe()
    {
        var (r, feed, bob) = Room(7602);
        var engineering = Place<obj_machinery_rnd_production_techfab_department_engineering>(r, 5, 4);
        var medical = Place<obj_machinery_rnd_production_techfab_department_medical>(r, 5, 5);
        var imprinter = Place<obj_machinery_rnd_production_circuit_imprinter>(r, 4, 6);
        var science_imprinter = Place<obj_machinery_rnd_production_circuit_imprinter_department_science>(r, 6, 6);

        // the matter bin is engineering's and science's; the imprinter's board science's, an imprinter's build
        Assert.Contains(typeof(datum_design_basic_matter_bin), Offered(r, engineering));
        Assert.DoesNotContain(typeof(datum_design_basic_matter_bin), Offered(r, medical));
        Assert.DoesNotContain(typeof(datum_design_board_circuit_imprinter), Offered(r, engineering));
        Assert.Contains(typeof(datum_design_board_circuit_imprinter), Offered(r, imprinter));
        Assert.Contains(typeof(datum_design_board_circuit_imprinter), Offered(r, science_imprinter));
        Assert.DoesNotContain(typeof(datum_design_basic_matter_bin), Offered(r, imprinter));
        Assert.Equal(1.0, r.In(() => imprinter.efficiency_coeff), 4); // one micro servo: 0.5 ** 0

        // the medical techfab has no key for an engineering design
        var ui = r.In(() => Dm.@new<datum_tgui>(bob, medical, "Fabricator", "test"));
        object result = r.In(() => Waiting.NoSleep(medical.ui_act("build", Dm.list(Dm.kv("design_path", "/datum/design/basic_matter_bin"), Dm.kv("amount", 1)), ui, null)));
        Assert.False(Dm.truthy(result));
        r.Step(2);
        Assert.True(feed.Any("does not have the necessary keys to decrypt this design"));

        // the department's stripe in its colour, and its open-panel picture
        Assert.Contains("protolathe_stripe", Overlays(r, engineering));
        Assert.Equal("#EFB341", r.In(() => Dm.each(engineering.overlays).OfType<IDmAppearance>().Single(o => o.icon_state == "protolathe_stripe").color as string));
        Assert.DoesNotContain(Overlays(r, imprinter), o => o?.StartsWith("protolathe_stripe", StringComparison.Ordinal) == true);
        Hold<obj_item_screwdriver>(r, bob);
        Use(r, bob, engineering);
        r.In(() =>
        {
            Assert.True(Dm.truthy(engineering.panel_open));
            Assert.Equal("protolathe_t", engineering.icon_state);
        });
        Assert.Contains("protolathe_stripe_t", Overlays(r, engineering));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void NoFabricatorOffersADesignWhoseItemIsInert()
    {
        var (r, _, _) = Room(7603);
        var lathe = Place<obj_machinery_rnd_production_techfab>(r, 5, 4);
        var autolathe = Place<obj_machinery_autolathe_hacked>(r, 6, 6);
        r.In(() =>
        {
            datum_techweb web = lathe.stored_research;
            var candidates = Dm.each<object>(web.researched_designs)
                .Select(p => SSresearch.techweb_designs[p] as datum_design)
                .Where(d => Dm.truthy(Dm.bitand(d.build_type, lathe.allowed_buildtypes)) && Dm.truthy(Dm.bitand(d.departmental_flags, lathe.allowed_department_flags)))
                .ToList();
            Assert.All(Dm.each<datum_design>(lathe.cached_designs), d => Assert.False(d.builds_inert_item(), d.type.Name));
            Assert.Equal((float)candidates.Count(d => !d.builds_inert_item()), Dm.length(lathe.cached_designs));

            // the autolathe's window, hacked designs included
            var statics = (DmList)Waiting.NoSleep(autolathe.ui_static_data(null));
            var paths = Dm.each<DmList>(statics["designs"]).Select(d => (Type)d["path"]).ToList();
            Assert.NotEmpty(paths);
            Assert.All(paths, p => Assert.False((SSresearch.techweb_designs[p] as datum_design).builds_inert_item(), p.Name));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAutolatheTakesIronSheetsPrintsAndIsHackedByItsWires()
    {
        var (r, feed, bob) = Room(7604);
        var lathe = Place<obj_machinery_autolathe>(r, 5, 4);
        Assert.Equal(1.6, r.In(() => lathe.creation_efficiency), 4); // one micro servo: 1.8 - 0.2
        Assert.IsType<datum_techweb_autounlocking_autolathe>(r.In(() => lathe.stored_research));

        // iron sheets go in, with the autolathe's insertion pictures
        Hold<obj_item_stack_sheet_iron>(r, bob, 10);
        Use(r, bob, lathe);
        datum_material iron = Iron(r);
        Assert.Equal(10f * SHEET_MATERIAL_AMOUNT, r.In(() => Waiting.NoSleep(lathe.materials.get_material_amount(iron))));
        List<string> flicks = Flicks(r, lathe);
        Assert.Contains("autolathe_mat", flicks);
        Assert.Contains("material_insertion", flicks);

        // the Autolathe window: its designs (not the hacked ones) and its materials
        EmptyHands(r, bob);
        string opened = OpenWindow(r, bob, lathe, out JsonElement statics, out JsonElement data);
        Assert.StartsWith("Autolathe|", opened);
        string window = opened["Autolathe|".Length..];
        List<JsonElement> designs = statics.GetProperty("designs").EnumerateArray().ToList();
        JsonElement crowbar = designs.Single(d => d.GetProperty("path").GetString() == "/datum/design/crowbar");
        Assert.Equal("Pocket Crowbar", crowbar.GetProperty("name").GetString());
        Assert.Equal(8, crowbar.GetProperty("cost").GetProperty("iron").GetDouble()); // SMALL_MATERIAL_AMOUNT * 0.5 * 1.6
        Assert.DoesNotContain(designs, d => d.GetProperty("path").GetString() == "/datum/design/handcuffs");
        Assert.Equal(1000, data.GetProperty("materialtotal").GetDouble());
        Assert.Equal(r.In(() => lathe.materials.max_amount), (float)data.GetProperty("materialsmax").GetDouble()); // three matter bins of 37.5 sheets

        // a crowbar: the printing picture and sound, then the crowbar on its tile
        Act(r, bob, window, "make", ("design_path", "/datum/design/crowbar"), ("multiplier", 1));
        Assert.Equal("autolathe_n", r.In(() => lathe.icon_state));
        r.Step(Second);
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("lathe_print") == true);
        for (int i = 0; i < 10 && r.In(() => Dm.truthy(lathe.busy)); i++)
            r.Step(Second);
        r.In(() =>
        {
            Assert.Equal("autolathe", lathe.icon_state);
            Assert.Single(Dm.each<obj_item_crowbar>(lathe.loc), c => c.GetType() == typeof(obj_item_crowbar));
            Assert.Equal(1000f - 8f, Waiting.NoSleep(lathe.materials.get_material_amount(iron)));
        });

        // its panel and wires: a pulse of the hack wire shows the hacked designs for six seconds
        Hold<obj_item_screwdriver>(r, bob);
        Use(r, bob, lathe);
        r.In(() => Assert.Equal("autolathe_t", lathe.icon_state));
        var wires = r.In(() => (datum_wires_autolathe)lathe.wires);
        r.In(() =>
        {
            Assert.True(wires.interactable(bob));
            Assert.Contains("The blue light is off.", Dm.each<string>(wires.get_status()));
            Waiting.NoSleep(wires.on_pulse(WIRE_HACK));
            Assert.True(Dm.truthy(lathe.hacked));
            var hacked = (DmList)Waiting.NoSleep(lathe.ui_static_data(bob));
            Assert.Contains(Dm.each<DmList>(hacked["designs"]), d => Dm.eq(d["path"], typeof(datum_design_handcuffs)));
            Assert.Contains("The blue light is on.", Dm.each<string>(wires.get_status()));
        });
        r.Step(7 * Second);
        r.In(() => Assert.False(Dm.truthy(lathe.hacked), "the hack wire's pulse wears off"));

        // a cut hack wire keeps it hacked; a cut disable wire stops its printing
        r.In(() =>
        {
            Waiting.NoSleep(wires.cut(WIRE_HACK));
            Assert.True(Dm.truthy(lathe.hacked));
            Waiting.NoSleep(wires.cut(WIRE_DISABLE));
            Assert.True(Dm.truthy(lathe.disabled));
            Waiting.NoSleep(wires.on_pulse(WIRE_SHOCK));
            Assert.True(Dm.truthy(lathe.shocked));
        });
        r.Step(7 * Second);
        r.In(() => Assert.False(Dm.truthy(lathe.shocked), "the shock wire's pulse wears off"));
        var ui = r.In(() => Dm.@new<datum_tgui>(bob, lathe, "Autolathe", "test"));
        r.In(() => Waiting.NoSleep(lathe.ui_act("make", Dm.list(Dm.kv("design_path", "/datum/design/crowbar"), Dm.kv("multiplier", 1)), ui, null)));
        r.Step(2);
        Assert.True(feed.Any("Unable to print, voltage mismatch in internal wiring."));
        Assert.False(r.In(() => Dm.truthy(lathe.busy)));
        r.AssertNoRuntimes();
    }
}
