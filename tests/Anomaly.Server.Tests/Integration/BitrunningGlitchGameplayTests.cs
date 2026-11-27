using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The bitrunning glitch through the real server on MetaStation: a bitrunner deploys the xeno nest from Meta's quantum console and
/// enters it from a netpod as an avatar; the glitch event picks the running server and its poll throws its alert on Gina, an
/// observer, who clicks it and signs up. When the poll ends her client is a cyber police agent in the domain, with the server's
/// glitch sound, her AntagInfoGlitch window and a name of the cyber authority; the domain's xenomorphs leave her be, and she meets
/// the avatar and punches it.
/// </summary>
public class BitrunningGlitchGameplayTests
{
    private const int Second = 20;

    private readonly ITestOutputHelper _output;

    public BitrunningGlitchGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action f)
    {
        using (w.World.Enter())
            f();
    }

    private static object Obj(RealHeadlessWorld w, long id) => In(w, () => w.World.Objects.TryGet(id, out object o) ? o : null);

    [MetaServerFact]
    public void A_ghost_takes_the_glitch_poll_and_hunts_the_avatar_as_the_cyber_police()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 10572, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player runner = w.Join("Bitrunner");
        RealHeadlessWorld.Player gina = w.Join("Gina");
        var me = (mob_living_carbon_human)w.MobOf(runner);
        var ginaTexts = new List<string>();
        gina.Client.ServerTextReceived += t => ginaTexts.Add(t.Html);
        w.World.RuntimeErrorSink = e => _output.WriteLine("RUNTIME: " + e.Message.Split('\n')[0]);
        w.Run(Second);
        mob_living ginaBody = w.MobOf(gina);
        In(w, () => ginaBody.ghostize(Dm.TRUE)); // Gina is an observer

        // Meta's den: the xeno nest deployed from the console, then a netpod
        var server = In(w, () => Dm.each<obj_machinery_quantum_server>(Dm.world.contents).Single());
        var console = In(w, () => Dm.each<obj_machinery_computer_quantum_console>(Dm.world.contents).Single());
        var pod = In(w, () => Dm.each<obj_machinery_netpod>(Dm.world.contents).First(n => n.find_server() == server));
        In(w, () => server.points = 10); // the nest costs points the den has yet to earn
        In(w, () => Waiting.NoSleep(me.forceMove(get_turf(pod))));
        w.Run(5);
        In(w, () => Waiting.Detach(console.ui_act("set_domain", Dm.list(Dm.kv("id", "xeno_nest")),
            Dm.@new<datum_tgui>(me, console, "QuantumConsole"), null)));
        for (int i = 0; i < 40 && In(w, () => server.generated_domain == null || !Dm.truthy(server.is_ready)); i++)
            w.Run(Second / 2);
        Assert.Equal("xeno_nest", In(w, () => Dm.str(server.generated_domain?.key)));
        In(w, () => Waiting.Detach(pod.mouse_drop_receive(me, me)));
        mob avatar = null;
        for (int i = 0; i < 40 && (avatar == null || avatar == me); i++)
        {
            w.Run(Second / 2);
            avatar = w.MobOf(runner);
        }
        Assert.True(In(w, () => is_area_virtual(get_area(avatar))), "the avatar is not in the domain");

        // the glitch event: it finds the running server with mobs to mutate, and the server polls the ghosts
        var control = In(w, () => Dm.each<datum_round_event_control_bitrunning_glitch>(SSevents.control).Single());
        Assert.Equal(Dm.TRUE, In(w, () => control.validate_servers()));
        In(w, () =>
        {
            var ev = Dm.@new<datum_round_event_ghost_role_bitrunning_glitch>(Dm.TRUE, control);
            ev.forced_role = typeof(datum_antagonist_bitrunning_glitch_cyber_police);
            Waiting.Detach(ev.spawn_role());
        });
        w.Run(Second / 2);
        long alertId = gina.Session.Replica.Screen.Values.Select(e => e.Id).FirstOrDefault(id => Obj(w, id) is atom_movable_screen_alert_poll_alert);
        Assert.True(alertId != 0, "no poll alert on the observer's HUD\n" + w.Err);
        Assert.True(gina.Session.Click(new ClickTarget(TargetKind.Screen, alertId, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        Assert.Contains(ginaTexts, t => t.Contains("You have signed up", StringComparison.Ordinal));

        // the poll ends: Gina is the cyber police in the domain, her window open
        w.Run(21 * Second);
        var police = Obj(w, gina.Session.Replica.OwnEntity) as mob_living_carbon_human;
        Assert.True(police != null, "Gina is not the glitch\n" + w.Err);
        Assert.True(In(w, () => is_area_virtual(get_area(police))), "the glitch is not in the domain");
        Assert.NotNull(In(w, () => police.mind.has_antag_datum(typeof(datum_antagonist_bitrunning_glitch_cyber_police))));
        Assert.True(In(w, () => Dm.@in(police.real_name, GLOB.cyberauth_names)), In(w, () => Dm.str(police.real_name)));
        UiWindowState info = gina.Session.Replica.UiWindows.Values.SingleOrDefault(v => v.Interface == "AntagInfoGlitch");
        Assert.True(info != null, "no AntagInfoGlitch: " + string.Join(", ", gina.Session.Replica.UiWindows.Values.Select(v => v.Interface)));
        Assert.Equal("Cyber Police", info.Merged.Str("antag_name"));
        var context = new TguiContext(info.Window, info.Interface, info.Title, 2, info.Merged, (_, _) => { }, new TguiLocalState());
        Assert.Contains(Anomaly.Client.Shared.Tgui.Interfaces.AntagInfoGlitch.Render(context).Walk(), n => n.Key == "objective");

        // the domain's xenomorphs leave a glitch be
        var xeno = In(w, () => Dm.each<mob_living_basic_alien>(Dm.world.contents).First(a => is_area_virtual(get_area(a))));
        Assert.Equal(Dm.TRUE, In(w, () => Dm.num(police.GetComponent(typeof(datum_component_npc_friendly)) != null)));
        Assert.True(In(w, () => Dm.@in(Defines.ROLE_GLITCH, police.faction)));

        // the glitch meets the avatar: next to it, in combat mode, Gina's click is a punch
        In(w, () => Waiting.NoSleep(police.forceMove(get_step(avatar, Dm.EAST))));
        In(w, () => police.set_combat_mode(Dm.TRUE));
        w.Run(Second);
        Assert.True(gina.Session.Replica.TryGet(In(w, () => avatar.dm_id), out _), "Gina's client does not see the avatar");
        float before = In(w, () => Dm.num(((mob_living)avatar).get_brute_loss()));
        for (int i = 0; i < 5; i++)
        {
            Assert.True(gina.Session.Click(new ClickTarget(TargetKind.Entity, In(w, () => avatar.dm_id), 0, 0, 0, 16, 18), ClickButton.Left, ClickModifiers.None));
            w.Run(Second);
        }
        Assert.True(In(w, () => Dm.num(((mob_living)avatar).get_brute_loss())) > before, "the avatar took no blow\n" + w.Err);
        _output.WriteLine($"xeno {In(w, () => xeno.name)}, avatar brute {In(w, () => ((mob_living)avatar).get_brute_loss())}");
    }
}
