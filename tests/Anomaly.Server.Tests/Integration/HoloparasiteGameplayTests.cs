using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Syndicate package X2 (D316) through the real server and two clients: Bob holds a holoparasite injector and activates it in hand
/// (Z): tg's radial of the guardian types reaches his client and he picks the charger; the ghosts' poll throws its alert on Gina, an
/// observer: her client draws it on her HUD with tg's prompt sound, she clicks it and signs up. When the poll ends her client is moved
/// into the charger bound to Bob; she names and colours it through tg's text and colour prompts, then clicks her HUD's Manifest button
/// and the guardian stands beside Bob, in his client's view, with the phase-in effect.
/// </summary>
public sealed class HoloparasiteGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static ServerConfig Config() => new()
    {
        Seed = 11, RoomWidth = 24, RoomHeight = 24, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f, DevCommands = false,
    };

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static object Obj(RealHeadlessWorld w, long id) => In(w, () => w.World.Objects.TryGet(id, out object o) ? o : null);

    private static UiWindowState Window(RealHeadlessWorld.Player p, string @interface) => p.Session.Replica.UiWindows.Values.SingleOrDefault(v => v.Interface == @interface);

    private static void Submit(RealHeadlessWorld w, RealHeadlessWorld.Player p, string @interface, string entry)
    {
        UiWindowState input = Window(p, @interface);
        Assert.True(input != null, "no " + @interface + ": " + string.Join(", ", p.Session.Replica.UiWindows.Values.Select(v => v.Interface)) + "\n" + w.Err);
        Assert.True(p.Session.UiAct(input.Window, "submit", UiValue.ObjectOf(("entry", UiValue.Of(entry)))));
        w.Run(Second);
    }

    [Fact]
    public void Bob_injects_a_holoparasite_and_the_observer_who_takes_the_poll_becomes_it_and_manifests_beside_him()
    {
        using var w = new RealHeadlessWorld(config: Config(), strict: false);
        RealHeadlessWorld.Player bob = w.Join("Bob");
        RealHeadlessWorld.Player gina = w.Join("Gina");
        var ginaTexts = new List<string>();
        var bobTexts = new List<string>();
        gina.Client.ServerTextReceived += t => ginaTexts.Add(t.Html);
        bob.Client.ServerTextReceived += t => bobTexts.Add(t.Html);
        w.World.RuntimeErrorSink = e => output.WriteLine("RUNTIME: " + e.Message.Split('\n')[0]);
        w.Run(4 * Second);
        if (!In(w, () => Dm.truthy(GlobalProcs.SSticker.HasRoundStarted())))
            In(w, () => GlobalProcs.SSticker.current_state = Defines.GAME_STATE_PLAYING); // the room's world has no lobby: its round is the room

        var bobBody = (mob_living_carbon_human)w.MobOf(bob);
        mob_living ginaBody = w.MobOf(gina);
        In(w, () => ginaBody.ghostize(Dm.TRUE)); // Gina is an observer
        In(w, () =>
        {
            var injector = Dm.@new<obj_item_guardian_creator_tech>(bobBody.loc);
            Waiting.NoSleep(bobBody.put_in_hands(injector));
        });
        w.Run(Second);
        Assert.Equal("/mob/dead/observer", gina.Session.Replica.TryGet(gina.Session.Replica.OwnEntity, out var ghostEntity) ? ghostEntity.TypePath : null);

        // Z: the injector's attack_self shows tg's radial of the types; Bob picks the charger
        bob.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second / 2);
        RadialMenuMessage menu = bob.Session.Replica.TopRadial;
        Assert.True(menu != null, "no radial\n" + w.Err);
        int element = menu.Elements.FindIndex(e => e.Name == "Charger") + 1;
        output.WriteLine("radial: " + string.Join(", ", menu.Elements.Select(e => e.Name)));
        Assert.True(element > 0);
        Assert.True(bob.Session.RadialPick(menu.MenuId, element));
        w.Run(Second / 2);
        Assert.Contains(bobTexts, t => t.Contains("You start to power on the injector...", StringComparison.Ordinal));

        // the poll alert on Gina's HUD (and the boxed chat line with its sign-up link); she clicks it
        long alertId = gina.Session.Replica.Screen.Values.Select(e => e.Id).FirstOrDefault(id => Obj(w, id) is atom_movable_screen_alert_poll_alert);
        Assert.True(alertId != 0, "no poll alert on the observer's HUD\n" + w.Err);
        Assert.Contains(ginaTexts, t => t.Contains("[Sign Up]", StringComparison.Ordinal));
        Assert.True(gina.Session.Click(new ClickTarget(TargetKind.Screen, alertId, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        Assert.Contains(ginaTexts, t => t.Contains("You have signed up for Charger!", StringComparison.Ordinal));

        // the poll ends: Gina's client is moved into the charger bound to Bob, which asks her its name and colour
        w.Run(12 * Second);
        var guardian = Obj(w, gina.Session.Replica.OwnEntity) as mob_living_basic_guardian_charger;
        Assert.True(guardian != null, "Gina is not the guardian\n" + w.Err);
        Assert.Same(bobBody, In(w, () => guardian.summoner));
        Assert.Contains(bobTexts, t => t.Contains("Overclocking motive engines", StringComparison.Ordinal));
        Submit(w, gina, "TextInputModal", "Stardust");
        Submit(w, gina, "ColorPickerModal", "#33cc33");
        Assert.Equal("Stardust", In(w, () => guardian.real_name));

        // her HUD's Manifest button: the charger stands on Bob's tile and Bob's client sees it
        long manifestButton = In(w, () => ((atom)Dm.each_cast<datum_action>(guardian.actions).First(a => a is datum_action_cooldown_guardian_manifest).viewers[guardian.hud_used]).dm_id);
        Assert.Contains(gina.Session.Replica.Screen.Values, e => e.Id == manifestButton);
        Assert.True(gina.Session.Click(new ClickTarget(TargetKind.Screen, manifestButton, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        Assert.Same(In(w, () => bobBody.loc), In(w, () => guardian.loc));
        Assert.True(bob.Session.Replica.TryGet(In(w, () => guardian.dm_id), out var seen), "Bob's client does not see the guardian");
        Assert.Equal("/mob/living/basic/guardian/charger", seen.TypePath);
    }
}
