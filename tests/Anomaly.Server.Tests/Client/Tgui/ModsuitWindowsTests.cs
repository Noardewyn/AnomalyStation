using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps MO1: the client's MODsuit window (tg's MODsuit.tsx) shows the suit's status, charge, lock and MODlink, the user's status
/// from the status readout, the modules with their select, configure and pin buttons and their configuration entries, and the
/// parts with their deploy buttons; an unresponsive interface shows tg's error instead. The MODpaint window (MODpaint.jsx) shows
/// the colour matrix sliders, the presets and the suit's preview; NtosMODsuit (NtosMODsuit.jsx) shows the synced suit's panel or
/// tg's notice. All three are registered under their names.
/// </summary>
public class ModsuitWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string iface, Func<TguiContext, UiNode> render, string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, iface, "w", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private static bool HasKey(UiNode tree, string key) => tree.Walk().Any(n => n.Key == key);

    // The shape of mod/mod_ui.dm's ui_data and ui_static_data: a standard suit, active, with a status readout, a jetpack (one bool
    // configuration entry), a flashlight (no configuration, cooling down) and a passive module.
    private const string SuitStatus =
        "\"suit_status\":{\"core_name\":\"MOD standard core\",\"charge_current\":5000,\"charge_max\":10000,\"chargebar_color\":\"average\","
        + "\"chargebar_string\":\"5 kJ of 10 kJ (50%)\",\"active\":1,\"ai_name\":null,\"has_pai\":0,\"is_ai\":0,\"link_id\":\"123\",\"link_freq\":\"NT\","
        + "\"link_call\":null,\"open\":1,\"seconds_electrified\":0,\"malfunctioning\":0,\"locked\":1,\"interface_break\":0,\"complexity\":6}";

    private const string UserAndCustom =
        "\"user_status\":{\"user_name\":\"Jane Doe\",\"user_assignment\":\"Station Engineer\"},"
        + "\"module_custom_status\":{\"display_time\":1,\"shift_time\":\"00:12:34\",\"shift_id\":\"7\",\"health\":80,\"health_max\":100,"
        + "\"loss_brute\":20,\"loss_fire\":0,\"loss_tox\":0,\"loss_oxy\":0,\"body_temperature\":310.15,\"nutrition\":400.4,\"viruses\":null},";

    private const string Modules =
        "\"module_info\":["
        + "{\"module_name\":\"MOD ion jetpack module\",\"description\":\"Thrusters.\",\"module_type\":2,\"module_active\":0,\"pinned\":null,"
        + "\"idle_power\":0,\"active_power\":2.5,\"use_energy\":50,\"module_complexity\":3,\"cooldown_time\":0,\"cooldown\":0,\"id\":null,"
        + "\"ref\":\"[0x2001]\",\"configuration_data\":{\"stabilizers\":{\"display_name\":\"Stabilizers\",\"type\":\"bool\",\"value\":1,\"values\":null}}},"
        + "{\"module_name\":\"MOD flashlight module\",\"description\":\"Lights.\",\"module_type\":2,\"module_active\":1,\"pinned\":\"MOD flashlight module\","
        + "\"idle_power\":0,\"active_power\":15,\"use_energy\":0,\"module_complexity\":1,\"cooldown_time\":0,\"cooldown\":10,\"id\":null,"
        + "\"ref\":\"[0x2002]\",\"configuration_data\":[]},"
        + "{\"module_name\":\"MOD storage module\",\"description\":\"Pockets.\",\"module_type\":0,\"module_active\":0,\"pinned\":null,"
        + "\"idle_power\":0,\"active_power\":0,\"use_energy\":0,\"module_complexity\":1,\"cooldown_time\":0,\"cooldown\":0,\"id\":null,"
        + "\"ref\":\"[0x2003]\",\"configuration_data\":[]}],";

    private const string Hardware =
        "\"control\":\"standard MOD control unit\","
        + "\"parts\":[{\"slot\":\"back\",\"name\":\"standard MOD control unit\",\"deployed\":1,\"ref\":\"[0x1001]\"},"
        + "{\"slot\":\"head\",\"name\":\"standard MOD helmet\",\"deployed\":0,\"ref\":\"[0x1002]\"}],"
        + "\"ui_theme\":\"ntos\",\"complexity_max\":15";

    private static string Suit(string suitStatus = SuitStatus, string modules = Modules) => "{" + suitStatus + "," + UserAndCustom + modules + Hardware + "}";

    [Fact]
    public void TheModsuitWindowShowsTheSuitTheUserTheModulesAndTheParts()
    {
        Assert.NotNull(TguiInterfaces.Find("MODsuit"));
        var (context, tree, acts) = Render("MODsuit", MODsuit.Render, Suit());
        Assert.Equal((600.0, 600.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("MOD Interface Panel", context.WindowTitle);
        Assert.Equal("ntos", context.WindowTheme);
        Assert.True(ByKey(tree, "content").Scrollable);

        // the suit status
        Assert.Equal("Active", ByKey(tree, "activate").Text);
        Assert.Equal("good", ByKey(tree, "activate").Color);
        Assert.Equal(0.5, ByKey(tree, "charge").Value);
        Assert.Equal("average", ByKey(tree, "charge").Color);
        Assert.Equal("5 kJ of 10 kJ (50%)", ByKey(tree, "charge").Text);
        Assert.Equal("Locked", ByKey(tree, "lock").Text);
        Assert.Equal("lock", ByKey(tree, "lock").Icon);
        Assert.Equal("Call (123)", ByKey(tree, "call").Text);
        Assert.False(ByKey(tree, "call").Disabled);
        Assert.Equal("Open", ByKey(tree, "cover").Text);
        Assert.False(HasKey(tree, "circuits"));
        Assert.False(HasKey(tree, "eject_pai"));
        Assert.Equal("00:12:34", ByKey(tree, "time").Text);
        Assert.Equal("7", ByKey(tree, "number").Text);

        // the user status (the status readout's vitals)
        Assert.False(HasKey(tree, "unpowered"));
        Assert.Equal(0.8, ByKey(tree, "health").Value, 3);
        Assert.Equal("80", ByKey(tree, "health").Text);
        Assert.Equal(0.2, ByKey(tree, "brute").Value, 3);
        Assert.Equal("310 K", ByKey(tree, "temp").Text);
        Assert.Equal("400", ByKey(tree, "nutrition").Text);
        Assert.Equal("Jane Doe", ByKey(tree, "user_name").Text);
        Assert.Equal("Station Engineer", ByKey(tree, "user_assignment").Text);
        Assert.False(HasKey(tree, "fingerprints_row"));
        Assert.False(HasKey(tree, "radiation_row"));
        Assert.False(HasKey(tree, "diseases"));

        // the modules
        Assert.Equal("6 of 15 complexity used", ByKey(tree, "complexity").Text);
        Assert.Equal("MOD ion jetpack module", ByKey(tree, "module:[0x2001]").Title);
        Assert.Equal("green", ByKey(tree, "module:[0x2002]").Color);
        Assert.Equal("Toggle", ByKey(tree, "select:[0x2001]").Tooltip);
        Assert.Equal("power-off", ByKey(tree, "select:[0x2001]").Icon);
        Assert.True(ByKey(tree, "select:[0x2002]").Disabled); // cooling down
        Assert.True(ByKey(tree, "select:[0x2003]").Disabled); // passive
        Assert.True(ByKey(tree, "pin:[0x2003]").Disabled);
        Assert.True(ByKey(tree, "pin:[0x2002]").Selected);
        Assert.False(ByKey(tree, "configure:[0x2001]").Disabled);
        Assert.True(ByKey(tree, "configure:[0x2002]").Disabled);
        Assert.Equal("2.50", ByKey(tree, "active:[0x2001]").Text);
        Assert.Equal("15.00", ByKey(tree, "active:[0x2002]").Text);
        Assert.Equal("3", ByKey(tree, "complexity:[0x2001]").Text);
        Assert.False(HasKey(tree, "configure_screen:[0x2001]"));

        // the hardware
        Assert.Equal("standard MOD control unit", ByKey(tree, "control").Text);
        Assert.Equal("MOD standard core", ByKey(tree, "core").Text);
        Assert.Equal("back Slot", ByKey(tree, "part:back").Label);
        Assert.Equal("Retract", ByKey(tree, "deploy:back").Text);
        Assert.Equal("Deploy", ByKey(tree, "deploy:head").Text);
        Assert.Equal("No AI Detected", ByKey(tree, "ai").Text);

        ByKey(tree, "activate").OnClick();
        ByKey(tree, "lock").OnClick();
        ByKey(tree, "call").OnClick();
        ByKey(tree, "select:[0x2001]").OnClick();
        ByKey(tree, "pin:[0x2001]").OnClick();
        ByKey(tree, "deploy:head").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("activate", null), ("lock", null), ("call", null), ("select", "{\"ref\":\"[0x2001]\"}"), ("pin", "{\"ref\":\"[0x2001]\"}"),
            ("deploy", "{\"ref\":\"[0x1002]\"}"),
        }, acts);
    }

    [Fact]
    public void TheModsuitWindowConfiguresAModule()
    {
        var local = new TguiLocalState();
        var (_, tree, _) = Render("MODsuit", MODsuit.Render, Suit(), local);
        ByKey(tree, "configure:[0x2001]").OnClick();
        var (_, opened, acts) = Render("MODsuit", MODsuit.Render, Suit(), local);
        Assert.True(ByKey(opened, "configure:[0x2001]").Selected);
        UiNode entry = ByKey(opened, "config:[0x2001]:stabilizers");
        Assert.Equal("Stabilizers", ByKey(opened, "config:[0x2001]:stabilizers:row").Label);
        Assert.True(entry.Checkbox);
        entry.OnClick();
        Assert.Equal(new (string, string)[] { ("configure", "{\"key\":\"stabilizers\",\"value\":false,\"ref\":\"[0x2001]\"}") }, acts);

        // a second click closes it again
        ByKey(opened, "configure:[0x2001]").OnClick();
        var (_, closed, _) = Render("MODsuit", MODsuit.Render, Suit(), local);
        Assert.False(HasKey(closed, "configure_screen:[0x2001]"));
    }

    [Fact]
    public void TheModsuitWindowShowsAnInactiveSuitAndItsOtherStates()
    {
        string inactive = SuitStatus.Replace("\"active\":1", "\"active\":0").Replace("\"link_freq\":\"NT\"", "\"link_freq\":null")
            .Replace("\"locked\":1", "\"locked\":0").Replace("\"seconds_electrified\":0", "\"seconds_electrified\":30")
            .Replace("\"ai_name\":null,\"has_pai\":0", "\"ai_name\":\"pAI\",\"has_pai\":1");
        var (_, tree, acts) = Render("MODsuit", MODsuit.Render, Suit(inactive));
        Assert.Equal("Inactive", ByKey(tree, "activate").Text);
        Assert.Equal("default", ByKey(tree, "activate").Color);
        Assert.Equal("SUIT UNPOWERED", ByKey(tree, "unpowered").Text);
        Assert.Equal(0.0, ByKey(tree, "health").Value);
        Assert.Equal("0", ByKey(tree, "health").Text);
        Assert.Equal("00:00:00", ByKey(tree, "time").Text);
        Assert.Equal("???", ByKey(tree, "number").Text);
        Assert.Equal("Frequency Unset", ByKey(tree, "call").Text);
        Assert.True(ByKey(tree, "call").Disabled);
        Assert.Equal("Unlocked", ByKey(tree, "lock").Text);
        Assert.Equal("Shorted", ByKey(tree, "circuits").Text);
        Assert.Equal("pAI", ByKey(tree, "ai").Text);
        ByKey(tree, "eject_pai").OnClick();
        Assert.Equal(new (string, string)[] { ("eject_pai", null) }, acts);

        string calling = SuitStatus.Replace("\"link_call\":null", "\"link_call\":\"456\"");
        var (_, call, _) = Render("MODsuit", MODsuit.Render, Suit(calling));
        Assert.Equal("Calling (456)", ByKey(call, "call").Text);
        Assert.Equal("good", ByKey(call, "call").Color);

        string broken = SuitStatus.Replace("\"interface_break\":0", "\"interface_break\":1");
        var (_, locked, _) = Render("MODsuit", MODsuit.Render, Suit(broken));
        Assert.Equal("ERROR: INTERFACE UNRESPONSIVE", ByKey(locked, "locked_text").Text);
        Assert.False(ByKey(locked, "content").Scrollable);
        Assert.False(HasKey(locked, "suit_status"));

        // no modules: tg's notice
        var (_, empty, _) = Render("MODsuit", MODsuit.Render, Suit(modules: "\"module_info\":[],"));
        Assert.Equal("No Modules Detected", ByKey(empty, "no_modules").Text);
    }

    [Fact]
    public void TheModsuitWindowShowsTheStatusReadoutsDnaAndDiseases()
    {
        string json = Suit().Replace("\"viruses\":null", "\"dna_unique_identity\":\"abc123\",\"dna_unique_enzymes\":\"def456\","
            + "\"viruses\":[{\"name\":\"Cold\",\"type\":\"Airborne\",\"stage\":2,\"maxstage\":3,\"cure\":\"Rest\"}]");
        var (_, tree, _) = Render("MODsuit", MODsuit.Render, json);
        Assert.Equal("abc123", ByKey(tree, "fingerprints").Text);
        Assert.Equal("def456", ByKey(tree, "enzymes").Text);
        Assert.Equal("Cold", ByKey(tree, "virus:Cold").Title);
        Assert.Equal("2/3", ByKey(tree, "stage:Cold").Text);
    }

    [Fact]
    public void TheModpaintWindowShowsTheMatrixThePresetsAndThePreview()
    {
        Assert.NotNull(TguiInterfaces.Find("MODpaint"));
        var (context, tree, acts) = Render("MODpaint", MODpaint.Render,
            "{\"mapRef\":\"color_matrix_proxy_[0x3001]\",\"currentColor\":[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1,0,0,0,0]}");
        Assert.Equal((600.0, 365.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Red turns to:", ByKey(tree, "row:0").Title);
        Assert.Equal(100.0, ByKey(tree, "slider:0").Value);
        Assert.Equal(0.0, ByKey(tree, "slider:1").Value);
        Assert.Equal(100.0, ByKey(tree, "slider:5").Value);
        Assert.Equal(125.0, ByKey(tree, "slider:0").MaxValue);
        Assert.Equal("40%", ByKey(tree, "slider:0").Format(40));
        UiNode map = ByKey(tree, "map:color_matrix_proxy_[0x3001]");
        Assert.Equal("color_matrix_proxy_[0x3001]", map.MapRef);
        Assert.True(map.MapPicture);
        Assert.Equal("Purple", ByKey(tree, "preset:purple").Tooltip);

        ByKey(tree, "slider:1").OnChange(50);
        ByKey(tree, "preset:red").OnClick();
        ByKey(tree, "confirm").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("transition_color", "{\"color\":[1,0.5,0,0,0,1,0,0,0,0,1,0,0,0,0,1,0,0,0,0]}"),
            ("transition_color", "{\"color\":[1,0,0,0,0.25,0.5,0,0,0.25,0,0.5,0,0,0,0,1,0,0,0,0]}"),
            ("confirm", null),
        }, acts);
    }

    [Fact]
    public void TheNtosModsuitWindowShowsTheSyncedSuitOrANotice()
    {
        Assert.NotNull(TguiInterfaces.Find("NtosMODsuit"));
        var (context, none, _) = Render("NtosMODsuit", NtosMODsuit.Render, "{\"has_suit\":0,\"PC_device_theme\":\"ntos\"}");
        Assert.Equal((575.0, 700.0), (context.WindowWidth, context.WindowHeight));
        Assert.StartsWith("No Modular suit connected", ByKey(none, "no_suit").Text);
        Assert.False(HasKey(none, "suit_status"));

        var (_, tree, acts) = Render("NtosMODsuit", NtosMODsuit.Render, Suit().Replace("{\"suit_status\"", "{\"has_suit\":1,\"PC_device_theme\":\"ntos\",\"suit_status\""));
        Assert.False(HasKey(tree, "no_suit"));
        Assert.Equal("Active", ByKey(tree, "activate").Text);
        ByKey(tree, "deploy:back").OnClick();
        Assert.Equal(new (string, string)[] { ("deploy", "{\"ref\":\"[0x1001]\"}") }, acts);
    }
}
