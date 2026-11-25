using System.Linq;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui;

// M5 slice 3 (D148): the window manager's engine-free half. It keeps the client's open tgui windows (from ReplicaWorld.UiWindows), each
// with its interface's local state, renders each through its translated interface, places it on the screen (the position the player
// dragged that window type to last, from the options file, else centred and cascaded), keeps it on the screen while dragged, scales it
// with the HUD, and turns the player's clicks, Esc and close button into tg's act and close messages (PlaySession.UiAct, UiClose).
// The Godot host (Hud/Tgui/TguiHost.cs) draws what it says.

/// <summary>The translated interfaces by tg's interface name.</summary>
public static class TguiInterfaces
{
    private static readonly Dictionary<string, TguiInterface> Known = new(StringComparer.Ordinal)
    {
        ["Canister"] = Canister.Render,
        ["MechBayPowerConsole"] = MechBayPowerConsole.Render, // D321
        ["ExosuitControlConsole"] = ExosuitControlConsole.Render, // D321
        ["Mecha"] = Mecha.Render, // D321
        ["MarkdownViewer"] = MarkdownViewer.Render, // package J2 (docs/ai/specs/gaps.md)
        ["PaperSheet"] = PaperSheet.Render, // gaps S3
        ["Folder"] = Folder.Render, // gaps S3
        ["Clipboard"] = Clipboard.Render, // gaps S3
        ["FilingCabinet"] = FilingCabinet.Render, // gaps S3
        ["NoticeBoard"] = NoticeBoard.Render, // gaps S3
        ["Photocopier"] = Photocopier.Render, // gaps S3
        ["Fax"] = Fax.Render, // gaps S3
        ["NtosFaxBond"] = NtosFaxBond.Render, // gaps S3
        ["SyndicateContractor"] = SyndicateContractor.Render, // syndicate X1a
        ["PortablePump"] = PortablePump.Render, // package E3 (docs/ai/specs/engineering.md)
        ["PortableScrubber"] = PortableScrubber.Render, // package E3
        ["PipeScrubber"] = PipeScrubber.Render, // package E3
        ["Tank"] = Tank.Render, // package E3
        ["NumberInputModal"] = NumberInputModal.Render,
        ["TextInputModal"] = TextInputModal.Render, // M8z (D241)
        ["ColorPickerModal"] = ColorPickerModal.Render, // M10x slice D5 (DV209)
        [Limits.BrowseUiInterface] = Browser.Render, // M10x slice G6 (DV216): tg's browse() pages
        ["Adminhelp"] = Adminhelp.Render, // M10x slice G6
        ["AlertModal"] = AlertModal.Render,
        ["ListInputWindow"] = ListInputWindow.Render,
        ["StackCrafting"] = StackCrafting.Render,
        ["AirlockElectronics"] = AirlockElectronics.Render,
        ["Wires"] = Wires.Render,
        ["PersonalCrafting"] = PersonalCrafting.Render,
        ["AirAlarm"] = AirAlarm.Render,
        ["Smes"] = Smes.Render,
        ["PortableGenerator"] = PortableGenerator.Render,
        ["Apc"] = Apc.Render,
        ["SolarControl"] = SolarControl.Render,
        ["ThermoMachine"] = ThermoMachine.Render,
        ["Reflector"] = Reflector.Render,
        ["AtmosPump"] = AtmosPump.Render,
        ["Jukebox"] = Jukebox.Render, // gaps H3
        ["BloodFilter"] = BloodFilter.Render, // gaps J5
        ["SlimeBodySwapper"] = SlimeBodySwapper.Render, // gaps SP2c
        ["CrewConsole"] = CrewConsole.Render, // gaps J5
        ["Electropack"] = Electropack.Render, // gaps J5
        ["Crayon"] = Crayon.Render,
        ["ForensicScanner"] = ForensicScanner.Render, // gaps package J4
        ["ForensicsSpoofer"] = ForensicsSpoofer.Render, // gaps package J4
        ["DetectiveBoard"] = DetectiveBoard.Render, // gaps package J4
        ["KeycardAuth"] = KeycardAuth.Render, // gaps package J4
        ["Teleporter"] = Teleporter.Render, // gaps package TP1
        ["BluespaceLocator"] = BluespaceLocator.Render, // gaps package TP1
        ["ImplantPad"] = ImplantPad.Render, // gaps DV2
        ["HighLuminosityEyesMenu"] = HighLuminosityEyesMenu.Render, // gaps OG1
        ["TacVisorEyesMenu"] = TacVisorEyesMenu.Render, // gaps OG1
        ["ImplantChair"] = ImplantChair.Render, // gaps DV2
        ["MODsuit"] = MODsuit.Render, // gaps MO1
        ["MODpaint"] = MODpaint.Render, // gaps MO1
        ["NtosMODsuit"] = NtosMODsuit.Render, // gaps MO1
        ["SkillStation"] = SkillStation.Render, // gaps SL1
        ["SkillPanel"] = SkillPanel.Render, // gaps SL1
        ["NtosSkillTracker"] = NtosSkillTracker.Render, // gaps SL1
        ["RequestsConsole"] = RequestsConsole.Render, // gaps SC1
        ["IntegratedCircuit"] = IntegratedCircuit.Render, // gaps CI1
        ["ComponentPrinter"] = ComponentPrinter.Render, // gaps CI1
        ["CircuitModule"] = CircuitModule.Render, // gaps CI1
        ["CircuitAdminPanel"] = CircuitAdminPanel.Render, // gaps CI1
        ["ScannerGate"] = ScannerGate.Render, // gaps SG1
        ["HypnoChair"] = HypnoChair.Render, // gaps SG1
        ["VotePanel"] = VotePanel.Render, // the owner, 2026-11-08: votes
        ["AtmosFilter"] = AtmosFilter.Render,
        ["AtmosMixer"] = AtmosMixer.Render,
        ["AtmosControlConsole"] = AtmosControlConsole.Render, // package E5
        ["AirlockController"] = AirlockController.Render, // package E8
        ["AirlockButtonController"] = AirlockButtonController.Render, // package E8
        ["ElevatorPanel"] = ElevatorPanel.Render, // D325: multi-z elevators
        ["TramControl"] = TramControl.Render, // the tram port
        ["TramController"] = TramController.Render, // the tram port
        ["TramPlaque"] = TramPlaque.Render, // the tram port
        ["DisposalUnit"] = DisposalUnit.Render, // D325: the disposal system
        ["DestinationTagger"] = DestinationTagger.Render, // D325: the disposal mail
        ["AtmosTempGate"] = AtmosTempGate.Render,
        ["AtmosTempPump"] = AtmosTempPump.Render,
        ["GasAnalyzer"] = GasAnalyzer.Render,
        ["RapidPipeDispenser"] = RapidPipeDispenser.Render,
        ["RapidConstructionDevice"] = RapidConstructionDevice.Render, // engineering package E13
        ["RapidTilingDevice"] = RapidTilingDevice.Render, // engineering package E13
        ["RapidDecorationDevice"] = RapidDecorationDevice.Render, // engineering package E13
        ["RapidPlumbingDevice"] = RapidPlumbingDevice.Render, // engineering package E15
        ["ChemAcclimator"] = ChemAcclimator.Render, // engineering package E15
        ["ChemAutomaticBuffer"] = ChemAutomaticBuffer.Render, // engineering package E15
        ["ChemDisposer"] = ChemDisposer.Render, // engineering package E15
        ["ChemFilter"] = ChemFilter.Render, // engineering package E15
        ["ChemMixingChamber"] = ChemMixingChamber.Render, // engineering package E15
        ["ChemPress"] = ChemPress.Render, // engineering package E15
        ["ChemReactionChamber"] = ChemReactionChamber.Render, // engineering package E15
        ["ChemSplitter"] = ChemSplitter.Render, // engineering package E15
        ["ChemSynthesizer"] = ChemSynthesizer.Render, // engineering package E15
        ["PowerMonitor"] = PowerMonitor.Render,
        ["Cryo"] = Cryo.Render,
        ["CommunicationsConsole"] = CommunicationsConsole.Render,
        ["LockedSafe"] = LockedSafe.Render,
        ["BrigTimer"] = BrigTimer.Render, // syndicate brig timers (D316)
        ["NuclearBomb"] = NuclearBomb.Render,
        ["EmergencyShuttleConsole"] = EmergencyShuttleConsole.Render,
        ["ShuttleConsole"] = ShuttleConsole.Render,
        ["Cargo"] = Cargo.Render,
        ["StripMenu"] = StripMenu.Render,
        ["EmotePanel"] = EmotePanel.Render, // M9 slice B (D256)
        ["Changelog"] = Changelog.Render, // M10x slice H (D281)
        ["Radio"] = Radio.Render, // M9 slice D
        ["Signaler"] = Signaler.Render, // M9 slice D
        ["Telecomms"] = Telecomms.Render, // M9 slice D
        ["TelecommsMonitor"] = TelecommsMonitor.Render, // M9 slice D2
        ["ServerMonitor"] = ServerMonitor.Render, // M9 slice D2
        ["NtosMain"] = NtosMain.Render, // M9 slice E
        ["NtosMessenger"] = NtosMessenger.Render, // M9 slice E
        ["NtosNotepad"] = NtosNotepad.Render, // M9 slice E
        ["NtosCrewManifest"] = NtosCrewManifest.Render, // M9 slice E
        ["NtosCard"] = NtosCard.Render, // gaps S4a
        ["NtosPay"] = NtosPay.Render, // gaps P4a
        ["NtosCargo"] = NtosCargo.Render, // gaps P4b
        ["NtosCouponMaster"] = NtosCouponMaster.Render, // gaps P4b
        ["CivCargoHoldTerminal"] = CivCargoHoldTerminal.Render, // gaps P4c
        ["NtosCivCargoHoldTerminal"] = NtosCivCargoHoldTerminal.Render, // gaps P4c
        ["CargoExpress"] = CargoExpress.Render, // gaps P4d
        ["NtosShipping"] = NtosShipping.Render, // gaps P4d
        ["LTSRBT"] = LTSRBT.Render, // gaps P4d
        ["BlackMarketUplink"] = BlackMarketUplink.Render, // gaps P4d
        ["NtosDeptOrder"] = NtosDeptOrder.Render, // gaps P4d
        ["MatMarket"] = MatMarket.Render, // gaps P4d
        ["ProduceConsole"] = ProduceConsole.Render, // gaps P4d
        ["LanguageMenu"] = LanguageMenu.Render, // gaps L2
        ["PhysicalNewscaster"] = PhysicalNewscaster.Render, // silicons SI6b
        ["NtosNewscaster"] = NtosNewscaster.Render, // silicons SI6b
        ["Newspaper"] = Newspaper.Render, // silicons SI6b
        ["BountyBoard"] = BountyBoard.Render, // silicons SI6b
        ["NtosBountyBoard"] = NtosBountyBoard.Render, // silicons SI6b
        ["BankMachine"] = BankMachine.Render, // gaps P4a
        ["HoloPay"] = HoloPay.Render, // gaps P4a
        ["AccountingConsole"] = AccountingConsole.Render, // gaps P4a
        ["NtosJobManager"] = NtosJobManager.Render, // gaps S4a
        ["NtosRecords"] = NtosRecords.Render, // gaps S4a
        ["NtosStatus"] = NtosStatus.Render, // gaps S4a
        ["NtosPowerMonitor"] = NtosPowerMonitor.Render, // gaps S4b
        ["NtosSignaler"] = NtosSignaler.Render, // gaps S4b
        ["NtosRoboControl"] = NtosRoboControl.Render, // gaps S4b
        ["NtosCyborgRemoteMonitor"] = NtosCyborgRemoteMonitor.Render, // gaps S4b
        ["NtosNetMonitor"] = NtosNetMonitor.Render, // gaps S4b
        ["NtosNetChat"] = NtosNetChat.Render, // gaps S4b
        ["NtosEmojipedia"] = NtosEmojipedia.Render, // gaps S4c
        ["NtosPhysScanner"] = NtosPhysScanner.Render, // gaps S4c
        ["NtosBotanicalEncyclopedia"] = NtosBotanicalEncyclopedia.Render, // gaps S4c
        ["NtosCursor"] = NtosCursor.Render, // gaps S4c
        ["NtosNetDos"] = NtosNetDos.Render, // gaps S4c
        ["NtosRevelation"] = NtosRevelation.Render, // gaps S4c
        ["NtosArcade"] = NtosArcade.Render, // gaps S4c
        ["NtosSpectreMeter"] = NtosSpectreMeter.Render, // gaps S4c
        ["NtosVirtualPet"] = NtosVirtualPet.Render, // gaps S4d
        ["NtosMafiaPanel"] = NtosMafiaPanel.Render, // gaps S4d
        ["MafiaPanel"] = MafiaPanel.Render, // gaps S4d
        ["DnaConsole"] = DnaConsole.Render, // gaps P3
        ["ExperimentConfigure"] = ExperimentConfigure.Render, // gaps RD3
        ["DestructiveAnalyzer"] = DestructiveAnalyzer.Render, // gaps RD3
        ["Experimentor"] = Experimentor.Render, // gaps RD4
        ["InfuserBook"] = InfuserBook.Render, // gaps P3b
        ["MinigamesMenu"] = MinigamesMenu.Render, // gaps S4d
        ["NtosRadar"] = NtosRadar.Render, // syndicate N1b
        ["NtosRadarSyndicate"] = NtosRadar.RenderSyndicate, // syndicate N1b: the Fission360
        ["NtosFileManager"] = NtosFileManager.Render, // M9 slice E
        ["NtosNetDownloader"] = NtosNetDownloader.Render, // M9 slice E
        ["NtosThemeConfigure"] = NtosThemeConfigure.Render, // M9 slice E
        ["CrewManifest"] = CrewManifest.Render, // M9 slice E
        ["JobSelection"] = JobSelection.Render, // M10 slice F
        ["PreferencesMenu"] = PreferencesMenu.Render, // M10 slice F (D267)
        ["MessageMonitor"] = MessageMonitor.Render, // M9 slice E
        ["NtnetRelay"] = NtnetRelay.Render, // M9 slice E
        ["AutomatedAnnouncement"] = AutomatedAnnouncement.Render, // M9 slice D
        ["CameraConsole"] = CameraConsole.Render, // M10 slice D (D268)
        ["Intellicard"] = Intellicard.Render, // silicons SI4 (D326)
        ["AiRestorer"] = AiRestorer.Render, // silicons SI4
        ["PaiCard"] = PaiWindows.RenderCard, // silicons SI6
        ["PaiSubmit"] = PaiWindows.RenderSubmit, // silicons SI6
        ["PaiInterface"] = PaiWindows.RenderInterface, // silicons SI6
        ["GhostMenu"] = GhostMenu.Render, // silicons SI6
        ["SpawnersMenu"] = SpawnersMenu.Render, // silicons SI7
        ["MalfunctionModulePicker"] = MalfunctionModulePicker.Render, // silicons SI8
        ["AntagInfoMalf"] = AntagInfoMalf.Render, // silicons SI8
        ["AiVoiceChanger"] = AiVoiceChanger.Render, // silicons SI8
        ["InstrumentEditor"] = InstrumentEditor.Render, // silicons SI6c
        ["NtosAiRestorer"] = AiRestorer.RenderNtos, // silicons SI4
        ["Canvas"] = Canvas.Render, // silicons SI4d
        ["PortraitPicker"] = PaintingBrowsers.RenderPortraitPicker, // silicons SI4d
        ["NtosPortraitPrinter"] = PaintingBrowsers.RenderPortraitPrinter, // silicons SI4d
        ["Holopad"] = Holopad.Render, // silicons SI4
        ["RemoteRobotControl"] = RemoteRobotControl.Render, // silicons SI4
        ["AiCoreDisplayPicker"] = AiDisplayPickers.RenderCore, // silicons SI4
        ["AiStatusDisplayPicker"] = AiDisplayPickers.RenderStatus, // silicons SI4
        ["NtosSecurEye"] = NtosSecurEye.Render, // syndicate G3d (D316)
        ["EightBallVote"] = EightBallVote.Render, // syndicate G4a (D316)
        ["TransferValve"] = TransferValve.Render, // syndicate G4b (D316)
        ["Orbit"] = Orbit.Render, // M10x slice C
        ["Supermatter"] = Supermatter.Render, // E2 (D287)
        ["NtosSupermatter"] = NtosSupermatter.Render, // E2 (D287)
        ["GravityGenerator"] = GravityGenerator.Render, // engineering package E4
        ["StationAlertConsole"] = StationAlertConsole.Render, // E6 (D287)
        ["NtosStationAlertConsole"] = NtosStationAlertConsole.Render, // E6 (D287)
        ["AtmosAlertConsole"] = AtmosAlertConsole.Render, // E6 (D287)
        ["ApcControl"] = ApcControl.Render, // E6 (D287)
        ["SpaceHeater"] = SpaceHeater.Render, // engineering package E9
        ["TurbineComputer"] = TurbineComputer.Render, // engineering package E11
        ["ThermoElectricGenerator"] = ThermoElectricGenerator.Render, // engineering package E11
        ["ModularShieldGen"] = ModularShieldGen.Render, // engineering package E10
        ["ModularShieldConsole"] = ModularShieldConsole.Render, // engineering package E10
        ["Crystallizer"] = Crystallizer.Render, // E12a (D287)
        ["Electrolyzer"] = Electrolyzer.Render, // E12a (D287)
        ["Hypertorus"] = Hypertorus.Render, // engineering package E12b
        ["Blueprints"] = Blueprints.Render, // engineering package E14
        ["DecalPainter"] = DecalPainter.Render, // engineering package E14
        ["BigManipulator"] = BigManipulator.Render, // engineering package E16
        ["ManufacturingSorter"] = ManufacturingSorter.Render, // engineering package E16
        ["Portagrav"] = Portagrav.Render, // engineering package E16
        ["AnomalousWeatherTower"] = AnomalousWeatherTower.Render, // engineering package E16
        ["IVDrip"] = IVDrip.Render, // gaps package G1
        ["Safe"] = Safe.Render, // gaps package G1
        ["StateLawUi"] = StateLawUi.Render, // silicons SI1
        ["LawRack"] = LawRack.Render, // silicons SI1
        ["Gps"] = Gps.Render, // silicons SI2
        ["NtosGasAnalyzer"] = NtosGasAnalyzer.Render, // silicons SI2
        ["RoboticsControlConsole"] = RoboticsControlConsole.Render, // silicons SI2
        ["AiAirlock"] = AiAirlock.Render, // silicons SI2
        ["CyborgBootDebug"] = CyborgBootDebug.Render, // silicons SI2
        ["BorgHypo"] = BorgHypo.Render, // silicons SI2
        ["BorgShaker"] = BorgShaker.Render, // silicons SI2
        ["NtosRobotact"] = NtosRobotact.Render, // silicons SI2
        ["LootPanel"] = LootPanel.Render, // gaps package G1
        ["MemoryPanel"] = MemoryPanel.Render, // gaps package G4
        ["Vending"] = Vending.Render, // gaps package S2
        ["SmartVend"] = SmartVend.Render, // gaps package H2
        ["SimpleBot"] = SimpleBot.Render, // gaps package C3
        ["RepairBot"] = RepairBot.Render, // gaps package C3
        ["RepairbotResources"] = RepairbotResources.Render, // gaps package C3
        ["Mule"] = Mule.Render, // gaps package C3
        ["NavBeacon"] = NavBeacon.Render, // gaps package C3
        ["BotAnnouncement"] = BotAnnouncement.Render, // gaps package C3
        ["ProximitySensor"] = ProximitySensor.Render, // gaps package C3
        ["Timer"] = Interfaces.Timer.Render, // gaps package AS1
        ["HealthSensor"] = HealthSensor.Render, // gaps package AS1
        ["InfraredEmitter"] = InfraredEmitter.Render, // gaps package AS1
        ["SmokeMachine"] = SmokeMachine.Render, // gaps package F1
        ["ChemDispenser"] = ChemDispenser.Render, // gaps package P5
        ["ChemDebugSynthesizer"] = ChemDebugSynthesizer.Render, // gaps package P5
        ["ChemMaster"] = ChemMaster.Render, // gaps package P5
        ["ChemHeater"] = ChemHeater.Render, // gaps package P5
        ["ChemSeparator"] = ChemSeparator.Render, // gaps package P5
        ["MassSpec"] = MassSpec.Render, // gaps package P5
        ["PortableChemMixer"] = PortableChemMixer.Render, // gaps package P5
        ["Thermometer"] = Thermometer.Render, // gaps package P5
        ["Pandemic"] = Pandemic.Render, // gaps package VI1
        ["Reagents"] = Reagents.Render, // gaps package P5
        ["OperatingComputer"] = OperatingComputer.Render, // gaps package P1
        ["OreBox"] = OreBox.Render, // Lavaland L2a
        ["OreRedemptionMachine"] = OreRedemptionMachine.Render, // Lavaland L2a
        ["OreSilo"] = OreSilo.Render, // Lavaland L2a
        ["StackingConsole"] = StackingConsole.Render, // Lavaland L2b
        ["ProcessingConsole"] = ProcessingConsole.Render, // Lavaland L2b
        ["LaborClaimConsole"] = LaborClaimConsole.Render, // Lavaland L2b
        ["GulagItemReclaimer"] = GulagItemReclaimer.Render, // gaps LC1
        ["GulagTeleporterConsole"] = GulagTeleporterConsole.Render, // gaps LC1
        ["PrisonerManagement"] = PrisonerManagement.Render, // gaps LC1
        ["SeedExtractor"] = SeedExtractor.Render, // gaps package P2
        ["MedicalKiosk"] = MedicalKiosk.Render, // gaps MD1
        ["Biogenerator"] = Biogenerator.Render, // gaps package P2
        ["ExosuitFabricator"] = ExosuitFabricator.Render, // silicons SI3
        ["Fabricator"] = Fabricator.Render, // gaps RD1
        ["Autolathe"] = Autolathe.Render, // gaps RD1
        ["ServerControl"] = ServerControl.Render, // gaps RD2
        ["Techweb"] = Techweb.Render, // gaps RD2
        ["NtosTechweb"] = Techweb.RenderNtos, // gaps RD2
        ["Limbgrower"] = Limbgrower.Render, // gaps MD3
        ["PlantAnalyzer"] = PlantAnalyzer.Render, // gaps package P2
        ["AntagInfoGeneric"] = AntagInfoGeneric.Render, // syndicate package A1
        ["LaunchpadRemote"] = LaunchpadRemote.Render, // syndicate campaign G2 (D316)
        ["RadioactiveMicrolaser"] = RadioactiveMicrolaser.Render, // syndicate campaign G2 (D316)
        ["AntagInfoTraitor"] = AntagInfoTraitor.Render, // syndicate package A2
        ["AntagInfoBrother"] = AntagInfoBrother.Render, // syndicate package X4
        ["AntagInfoRevolution"] = AntagInfoRevolution.Render, // gaps V1
        ["AntagInfoChangeling"] = AntagInfoChangeling.Render, // gaps K1
        ["CellularEmporium"] = CellularEmporium.Render, // gaps K1
        ["LingMMITalk"] = LingMMITalk.Render, // gaps K1
        ["AntagInfoSentient"] = AntagInfoSentient.Render, // syndicate G3c
        ["Uplink"] = Uplink.Render, // syndicate package A2
        ["DynamicAdmin"] = DynamicAdmin.Render, // syndicate package A3
        ["DynamicTester"] = DynamicTester.Render, // syndicate package A3
        ["Secrets"] = Secrets.Render, // the station's power and lights (DV351)
        ["StationTraitsPanel"] = StationTraitsPanel.Render, // gaps R2: the admins' Modify Station Traits
        ["PortableTurret"] = PortableTurret.Render, // syndicate package G4d
        ["TurretControl"] = TurretControl.Render, // syndicate package G4d
        ["ChameleonCard"] = ChameleonCard.Render, // syndicate package G1b
        ["BluespaceArtillery"] = BluespaceArtillery.Render, // gaps R1
        ["DnaVault"] = DnaVault.Render, // gaps R1
        ["SatelliteControl"] = SatelliteControl.Render, // gaps R1
        ["MedicalRecords"] = MedicalRecords.Render, // gaps SC2
        ["SecurityRecords"] = SecurityRecords.Render, // gaps SC2
        ["WarrantConsole"] = WarrantConsole.Render, // gaps SC2
        ["Trophycase"] = Trophycase.Render, // gaps ST2
        ["Vendatray"] = Vendatray.Render, // gaps ST2
        ["AbandonedCrate"] = AbandonedCrate.Render, // gaps ST3
        ["TrainingMachine"] = TrainingMachine.Render, // gaps ST4
        ["OreContainer"] = OreContainer.Render, // gaps ST5
        ["QuantumConsole"] = QuantumConsole.Render, // gaps BR1
        ["NetpodOutfits"] = NetpodOutfits.Render, // gaps BR1
        ["AvatarHelp"] = AvatarHelp.Render, // gaps BR1
    };

    /// <summary>The interface of that name, or null when it is not translated yet.</summary>
    public static TguiInterface Find(string name) => name != null && Known.TryGetValue(name, out TguiInterface f) ? f : null;

    public static IReadOnlyCollection<string> Names => Known.Keys;

    /// <summary>
    /// What a window shows for an interface that is not translated yet: a notice with its name. An NtOS program's (Ntos*) is drawn in
    /// NtosWindow, so its header's Close and Minimize lead back to the computer's main screen.
    /// </summary>
    public static UiNode Missing(TguiContext c)
    {
        var notice = new UiNode(UiKind.Section) { Fill = true }.With(
            new UiNode(UiKind.NoticeBox) { Text = "The window \"" + c.Interface + "\" is not ported yet." });
        return c.Interface != null && c.Interface.StartsWith("Ntos", StringComparison.Ordinal)
            ? Interfaces.NtosWindow.Render(c, 420, 180, null, notice)
            : c.Window(320, 140, null, notice);
    }
}

/// <summary>A rectangle on the screen in pixels.</summary>
public readonly record struct UiRect(double X, double Y, double Width, double Height)
{
    public bool Contains(double x, double y) => x >= X && y >= Y && x < X + Width && y < Y + Height;
}

/// <summary>One open window as the manager keeps it.</summary>
public sealed class TguiWindow
{
    internal TguiWindow(int index, string @interface)
    {
        Index = index;
        Interface = @interface;
    }

    public int Index { get; }

    public string Interface { get; }

    public TguiLocalState Local { get; } = new();

    /// <summary>The last render: the content tree, the context (title, size, the modals' Esc and Enter).</summary>
    public UiNode Content { get; internal set; }

    public TguiContext Context { get; internal set; }

    /// <summary>The window's top left on the screen in pixels (its size is <see cref="Size"/>).</summary>
    public double X { get; internal set; }

    public double Y { get; internal set; }

    public bool Placed { get; internal set; }

    /// <summary>The top left's share of the screen where it was placed or dragged: a resized game window puts it back there (the owner, 2026-10-28).</summary>
    internal double ShareX = double.NaN, ShareY = double.NaN;

    /// <summary>The server's state serial and the local state serial the content was rendered from.</summary>
    internal int RenderedServer = -1;
    internal int RenderedLocal = -1;
    internal double RenderedScale;

    /// <summary>Counts the renders (the host rebuilds the controls when it changes).</summary>
    public int Version { get; internal set; }

    /// <summary>tg's ui status (2 interactive, 1 update only, 0 disabled).</summary>
    public int Status { get; internal set; }

    public string Title => Context?.WindowTitle ?? Interface;

    /// <summary>The size the player dragged the window to (pixels at 100 %), or null: tgui's own.</summary>
    public (double Width, double Height)? UserSize { get; internal set; }

    /// <summary>The window's size on the screen: the player's, else tgui's Window width and height (plus the title bar), times the HUD scale.</summary>
    public (double Width, double Height) Size(double scale)
        => UserSize is (double w, double h) ? (w * scale, h * scale) : ((Context?.WindowWidth ?? 400) * scale, (Context?.WindowHeight ?? 300) * scale);

    public UiRect Rect(double scale)
    {
        (double w, double h) = Size(scale);
        return new UiRect(X, Y, w, h);
    }
}

/// <summary>The client's tgui windows (engine-free; the Godot host draws them).</summary>
public sealed class TguiWindows
{
    private readonly SortedDictionary<int, TguiWindow> _windows = new();
    private readonly List<int> _order = new(); // stacking order, the top last

    /// <summary>Sends tg's act: window, action, params (PlaySession.UiAct).</summary>
    public Action<int, string, UiValue> SendAct { get; set; }

    /// <summary>Sends tg's close: window (PlaySession.UiClose).</summary>
    public Action<int> SendClose { get; set; }

    /// <summary>M10x slice G6: sends a click on a link of a browse() page (PlaySession.Topic, tg's chat links).</summary>
    public Action<string> SendTopic { get; set; }

    /// <summary>The window sizes the player chose, per interface (the options file's <c>window_size.NAME=w,h</c>, pixels at 100 %).</summary>
    public Dictionary<string, (int Width, int Height)> Sizes { get; }

    /// <summary>Raised when a resized window's new size should be saved (the options file).</summary>
    public event Action PositionsChanged;

    public TguiWindows(Dictionary<string, (int Width, int Height)> sizes = null)
    {
        Sizes = sizes ?? new Dictionary<string, (int Width, int Height)>(StringComparer.Ordinal);
    }

    /// <summary>The smallest a window is resized to (pixels at 100 %): its title bar and a few lines; its content scrolls.</summary>
    public const double MinWidth = 200, MinHeight = 120;

    /// <summary>The open windows, bottom first.</summary>
    public IEnumerable<TguiWindow> Open => _order.Select(i => _windows[i]);

    public int Count => _windows.Count;

    public bool AnyOpen => _windows.Count > 0;

    public TguiWindow Top => _order.Count > 0 ? _windows[_order[^1]] : null;

    public TguiWindow Get(int index) => _windows.TryGetValue(index, out TguiWindow w) ? w : null;

    /// <summary>
    /// Brings the windows in line with the replica's (<see cref="ReplicaWorld.UiWindows"/>) and renders the ones whose server or local state
    /// changed. A window that closed is dropped with its local state; a new one is placed. Returns true when anything changed.
    /// </summary>
    public bool Sync(IReadOnlyDictionary<int, UiWindowState> states, double screenWidth, double screenHeight, double scale)
    {
        bool changed = false;
        foreach (int gone in _windows.Keys.Where(k => !states.ContainsKey(k) || states[k].Interface != _windows[k].Interface).ToList())
        {
            _windows.Remove(gone);
            _order.Remove(gone);
            changed = true;
        }
        foreach ((int index, UiWindowState state) in states)
        {
            if (state.Interface == Limits.EscapeMenuUiInterface)
                continue; // G4: the Esc menu draws it (GameScene), it is no window
            if (!_windows.TryGetValue(index, out TguiWindow window))
            {
                window = new TguiWindow(index, state.Interface);
                _windows[index] = window;
                if (state.Interface == Limits.LobbyUiInterface)
                    _order.Insert(0, index); // M10 slice F: tg's lobby is the screen under every window
                else
                    _order.Add(index);
                changed = true;
            }
            if (window.RenderedServer != state.Serial || window.RenderedLocal != window.Local.Serial || window.RenderedScale != scale
                || TguiClock.Seconds >= window.Local.WakeAt) // a timer of the interface (X1a)
            {
                Render(window, state);
                window.RenderedScale = scale;
                if (!window.Placed)
                    Place(window, screenWidth, screenHeight, scale);
                else
                    Clamp(window, screenWidth, screenHeight, scale);
                changed = true;
            }
        }
        return changed;
    }

    private void Render(TguiWindow window, UiWindowState state)
    {
        var context = new TguiContext(window.Index, state.Interface, state.Title, state.Status, state.Merged,
            (action, parameters) => Act(window.Index, action, parameters), window.Local) { Topic = SendTopic };
        TguiInterface view = TguiInterfaces.Find(state.Interface) ?? TguiInterfaces.Missing;
        window.Local.WakeAt = double.PositiveInfinity; // the render asks again for what it still waits for
        window.Content = view(context);
        window.Context = context;
        window.Status = state.Status;
        window.RenderedServer = state.Serial;
        window.RenderedLocal = window.Local.Serial;
        window.Version++;
    }

    // tgui's act: a window that only updates or is disabled takes no input (tgui's Window dims it; the server would refuse it anyway).
    private void Act(int index, string action, UiValue parameters)
    {
        if (_windows.TryGetValue(index, out TguiWindow window) && window.Status < 2)
            return;
        SendAct?.Invoke(index, action, parameters);
    }

    /// <summary>The cascade step of new windows of the same type with no saved position, in pixels at 100 %.</summary>
    public const double Cascade = 24;

    /// <summary>The height of the title bar in pixels at 100 % (tgui's TitleBar is 32 px).</summary>
    public const double TitleBarHeight = 22;

    /// <summary>
    /// Places a new window in the middle of the screen, a cascade step down and right of each other window already there, all of it on the
    /// screen when it fits (the owner, 2026-10-29: always in the middle; a machine's window opened where one was once dragged, often below the screen).
    /// </summary>
    public void Place(TguiWindow window, double screenWidth, double screenHeight, double scale)
    {
        if (Sizes.TryGetValue(window.Interface, out (int Width, int Height) size))
            window.UserSize = (size.Width, size.Height);
        (double w, double h) = window.Size(scale);
        int others = _windows.Values.Count(o => o != window && o.Placed);
        window.X = Math.Clamp(Math.Round((screenWidth - w) / 2) + others * Cascade * scale, 0, Math.Max(0, screenWidth - w));
        window.Y = Math.Clamp(Math.Round((screenHeight - h) / 2) + others * Cascade * scale, 0, Math.Max(0, screenHeight - h));
        window.Placed = true;
        Remember(window, screenWidth, screenHeight);
        Clamp(window, screenWidth, screenHeight, scale);
    }

    private void Remember(TguiWindow window, double screenWidth, double screenHeight)
    {
        window.ShareX = window.X / Math.Max(1, screenWidth);
        window.ShareY = window.Y / Math.Max(1, screenHeight);
    }

    /// <summary>Keeps the window's title bar on the screen (at least 48 px of it inside horizontally, all of it vertically).</summary>
    public static void Clamp(TguiWindow window, double screenWidth, double screenHeight, double scale)
    {
        (double w, _) = window.Size(scale);
        (window.X, window.Y) = KeepOnScreen(window.X, window.Y, w, screenWidth, screenHeight, TitleBarHeight * scale, scale);
    }

    /// <summary>The top left of a window <paramref name="width"/> wide with a title bar <paramref name="bar"/> high, kept so that the bar is on the
    /// screen (at least 48 px of it inside horizontally, all of it vertically); the game's other windows keep to it too (D282).</summary>
    public static (double X, double Y) KeepOnScreen(double x, double y, double width, double screenWidth, double screenHeight, double bar, double scale)
    {
        double keep = Math.Min(48 * scale, width);
        return (Math.Clamp(x, keep - width, Math.Max(keep - width, screenWidth - keep)), Math.Clamp(y, 0, Math.Max(0, screenHeight - bar)));
    }

    /// <summary>
    /// The game's window was resized from (fromWidth, fromHeight) to (toWidth, toHeight) pixels: each placed window's top left goes back to its
    /// share of the screen where it was placed or dragged (its size follows the HUD scale), so going back to a size brings it back where it
    /// was; it stays on the screen.
    /// </summary>
    public void Rescreen(double fromWidth, double fromHeight, double toWidth, double toHeight, double scale)
    {
        if (fromWidth <= 0 || fromHeight <= 0)
            return;
        foreach (TguiWindow window in _windows.Values)
        {
            if (!window.Placed)
                continue;
            if (double.IsNaN(window.ShareX))
                Remember(window, fromWidth, fromHeight);
            window.X = window.ShareX * toWidth;
            window.Y = window.ShareY * toHeight;
            Clamp(window, toWidth, toHeight, scale);
        }
    }

    /// <summary>A drag of the title bar moved the window by (dx, dy) pixels; it stays on the screen.</summary>
    public void Drag(TguiWindow window, double dx, double dy, double screenWidth, double screenHeight, double scale)
    {
        window.X += dx;
        window.Y += dy;
        Clamp(window, screenWidth, screenHeight, scale);
        Remember(window, screenWidth, screenHeight);
        Raise(window);
    }

    /// <summary>
    /// A drag of the window's corner grew it by (dx, dy) screen pixels (the owner, 2026-10-20: the game's windows can be resized): no smaller than
    /// <see cref="MinWidth"/> x <see cref="MinHeight"/>, no larger than the screen; its content scrolls when it is smaller than the content.
    /// </summary>
    public void Resize(TguiWindow window, double dx, double dy, double screenWidth, double screenHeight, double scale)
    {
        (double w, double h) = window.Size(1);
        window.UserSize = (Math.Clamp(w + dx / scale, MinWidth, Math.Max(MinWidth, screenWidth / scale)),
            Math.Clamp(h + dy / scale, MinHeight, Math.Max(MinHeight, screenHeight / scale)));
        Raise(window);
    }

    /// <summary>A resize ended: the window type's size is remembered (the options file).</summary>
    public void ResizeEnded(TguiWindow window)
    {
        if (window.UserSize is not (double w, double h))
            return;
        Sizes[window.Interface] = ((int)Math.Round(w), (int)Math.Round(h));
        PositionsChanged?.Invoke();
    }

    /// <summary>Puts the window on top (a click on it).</summary>
    public void Raise(TguiWindow window)
    {
        if (window.Interface == Limits.LobbyUiInterface)
            return;
        if (_order.Remove(window.Index))
            _order.Add(window.Index);
    }

    /// <summary>D292: Esc closes the top window (tg's lobby screen is no window to close); false when none is open.</summary>
    public bool CloseTop()
    {
        for (int i = _order.Count - 1; i >= 0; i--)
        {
            TguiWindow window = _windows[_order[i]];
            if (window.Interface == Limits.LobbyUiInterface)
                continue;
            Close(window);
            return true;
        }
        return false;
    }

    /// <summary>D292: the close-windows key closes every window but tg's lobby screen; false when none was open.</summary>
    public bool CloseAll()
    {
        bool any = false;
        foreach (TguiWindow window in Open.Where(w => w.Interface != Limits.LobbyUiInterface).ToList())
        {
            Close(window);
            any = true;
        }
        return any;
    }

    /// <summary>The close button: tg's "close" message (the server closes the ui; the window goes when the server says so).</summary>
    public void Close(TguiWindow window) => SendClose?.Invoke(window.Index);

    /// <summary>Enter: the top window's own Enter (the modals submit). False when it has none.</summary>
    public bool Enter()
    {
        TguiWindow top = Top;
        if (top?.Context?.OnEnter == null)
            return false;
        top.Context.OnEnter();
        return true;
    }

    /// <summary>Up (-1) or Down (+1): the top window's own arrow keys (the list input). False when it has none.</summary>
    public bool Arrow(int step)
    {
        TguiWindow top = Top;
        if (top?.Context?.OnArrow == null)
            return false;
        top.Context.OnArrow(step);
        return true;
    }

    /// <summary>The topmost window under a screen point, or null.</summary>
    public TguiWindow At(double x, double y, double scale)
    {
        for (int i = _order.Count - 1; i >= 0; i--)
        {
            TguiWindow w = _windows[_order[i]];
            if (w.Rect(scale).Contains(x, y))
                return w;
        }
        return null;
    }

    /// <summary>The windows of a join forgotten (the replica's MapGrid cleared them; the server sends them again).</summary>
    public void Clear()
    {
        _windows.Clear();
        _order.Clear();
    }

    /// <summary>A window position option's name: <c>window.</c> and tg's interface name (letters, digits and _ only).</summary>
    public static bool IsValidName(string name) => !string.IsNullOrEmpty(name) && name.Length <= 64 && name.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_');
}
