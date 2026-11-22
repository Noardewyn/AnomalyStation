using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps package P1 (D298) through the real server on MetaStation: a medical doctor in tg's outfit takes the tools out of a Meta surgery tray
/// one by one and operates on a patient buckled to one of Meta's operating tables (tg's free operation, his clicks aimed at the chest): the
/// incision, the retraction, the sawing of the ribs, the organ incision and, from tg's radial of the hemostat's operations, the heart taken out.
/// The client sees the tray lose each tool's overlay, the do-after's bar during the sawing, hears tg's scalpel sounds, reads the operating
/// computer's window with the patient's opened chest in it and sees the heart lying on the table.
/// </summary>
public class SurgeryGameplayTests
{
    private const int Second = 20; // ticks per second

    private readonly ITestOutputHelper _output;

    public SurgeryGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action f)
    {
        using (w.World.Enter())
            f();
    }

    private static List<string> States(ReplicaWorld replica, RealHeadlessWorld w, atom thing)
    {
        Assert.True(replica.TryGet(In(w, () => thing.dm_id), out ReplicaEntity seen), "the client does not see " + In(w, () => thing.type));
        return CrewOutfitTests.Icons(replica, seen.State.Appearance).Select(i => i.State ?? "").ToList();
    }

    private static bool Free(turf t) => t is turf_open open && open is not turf_open_space && !Dm.truthy(open.density) && !Dm.each<atom_movable>(open).Any(a => Dm.truthy(a.density));

    [MetaServerFact]
    public void A_doctor_takes_a_heart_out_on_medbays_operating_table_with_the_trays_tools_and_the_client_sees_it()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 29, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Doctor");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var texts = new List<string>();
        p.Client.ServerTextReceived += t => texts.Add(t.Html);
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        w.Run(2 * Second);

        // tg's medical doctor: his outfit's latex gloves and surgical mask
        Do(w, () =>
        {
            foreach (obj_item worn in Dm.each<obj_item>(me.get_equipped_items(INCLUDE_POCKETS)))
                qdel(worn);
            Waiting.NoSleep(me.equip_outfit_and_loadout(Dm.@new<datum_outfit_job_doctor>()));
        });
        w.Run(Second);

        // one of Meta's operating tables linked to its computer, a full tray of Meta's spawners near it
        var (table, computer, tray) = In(w, () =>
        {
            foreach (obj_structure_table_optable t in Dm.each<obj_structure_table_optable>(world))
            {
                if (t.computer is not obj_machinery_computer_operating c)
                    continue;
                obj_item_surgery_tray near = Dm.each<obj_item_surgery_tray>(world)
                    .Where(x => x.z == t.z && get_turf(x) != null && Dm.num(get_dist(x, t)) <= 8 && Dm.each<obj_item_scalpel>(x.contents).Any() && Dm.each<obj_item_circular_saw>(x.contents).Any())
                    .OrderBy(x => Dm.num(get_dist(x, t))).FirstOrDefault();
                if (near != null)
                    return (t, c, near);
            }
            return ((obj_structure_table_optable)null, (obj_machinery_computer_operating)null, (obj_item_surgery_tray)null);
        });
        Assert.NotNull(table);
        _output.WriteLine(In(w, () => $"table at {table.x},{table.y},{table.z}, computer at {computer.x},{computer.y}, tray {tray.type} at {tray.x},{tray.y} in {Dm.str(((area)get_area(table)).name)}"));

        // the patient lies buckled on the table; the doctor stands beside it, next to the computer when a tile is
        mob_living_carbon_human pat = In(w, () => Dm.@new<mob_living_carbon_human>(table.loc));
        w.Run(Second);
        Do(w, () => Waiting.NoSleep(table.buckle_mob(pat, force: TRUE)));
        turf beside = In(w, () =>
        {
            var free = new[] { NORTH, SOUTH, EAST, WEST, NORTHEAST, NORTHWEST, SOUTHEAST, SOUTHWEST }.Select(d => get_step(table, d)).Where(Free).ToList();
            return free.FirstOrDefault(t => Dm.num(get_dist(t, computer)) <= 1) ?? free.First();
        });
        Do(w, () => Waiting.NoSleep(me.forceMove(beside)));
        w.Run(Second);
        Assert.True(In(w, () => HAS_TRAIT(pat, TRAIT_READY_TO_OPERATE)), "the table's patient is ready to operate on");
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        var runtimes = new List<string>();
        Action<Exception> sink = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.Message); sink?.Invoke(e); };

        // the tray's overlays on the client: tg's tool overlays of what lies in it
        List<string> trayLooks = States(replica, w, tray);
        _output.WriteLine("tray: " + string.Join(", ", trayLooks));
        Assert.Contains("scalpel_normal", trayLooks);
        Assert.Contains("saw_normal", trayLooks);

        ClickTarget Chest() => new ClickTarget(TargetKind.Entity, In(w, () => pat.dm_id), 0, 0, 0, 16, 16, AimZone.Chest);
        string Diag() => "chat: " + string.Join(" | ", texts.TakeLast(6)) + " runtimes: " + string.Join("\n", runtimes.TakeLast(3));
        obj_item_bodypart chest = In(w, () => pat.get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart);

        void Take<T>() where T : obj_item => Do(w, () =>
        {
            if (me.get_active_held_item() is obj_item old && !Dm.truthy(tray.atom_storage.attempt_insert(old, me, messages: FALSE)))
                Waiting.NoSleep(me.dropItemToGround(old)); // the outfit's surgery medkit in his hand
            T tool = Dm.each<T>(tray.contents).First();
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(tool))), "the doctor takes " + tool.type);
        });

        void Operate(float seconds, float state)
        {
            Assert.True(p.Session.Click(Chest(), ClickButton.Left, ClickModifiers.None));
            w.Run((int)(seconds * Second) + Second);
            Assert.True(In(w, () => LIMB_HAS_SURGERY_STATE(chest, state)), "chest state " + In(w, () => chest.surgery_state) + "; " + Diag());
        }

        // 1. the scalpel: the incision, tg's scalpel sounds on the client; the tray's scalpel overlay is gone
        Take<obj_item_scalpel>();
        w.Run(2);
        Assert.DoesNotContain("scalpel_normal", States(replica, w, tray));
        Operate(1.6f, Dm.bitor(SURGERY_SKIN_CUT, SURGERY_VESSELS_UNCLAMPED));
        Assert.Contains(texts, t => t.Contains("You begin to make an incision in"));
        Assert.Contains(heard, h => h.File == "sound/items/handling/surgery/scalpel1.ogg");
        Assert.Contains(heard, h => h.File == "sound/items/handling/surgery/scalpel2.ogg");

        // 2. the retractor, 3. the saw, with the do-after's bar over the patient on the client while it runs
        Take<obj_item_retractor>();
        Operate(2.4f, SURGERY_SKIN_OPEN);
        Take<obj_item_circular_saw>();
        Assert.True(p.Session.Click(Chest(), ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second);
        Assert.Contains(replica.Images.Values, i => CrewOutfitTests.Icons(replica, i.Appearance).Any(l => (l.State ?? "").StartsWith("prog_bar_", StringComparison.Ordinal)));
        w.Run(4 * Second + Second / 2);
        Assert.True(In(w, () => LIMB_HAS_SURGERY_STATE(chest, SURGERY_BONE_SAWED)), "the ribs are sawed; " + Diag());

        // 4. the scalpel again: the organs cut
        Take<obj_item_scalpel>();
        Operate(2.4f, SURGERY_ORGANS_CUT);

        // the operating computer's window: the patient's opened chest and the next steps with the tool in hand
        if (In(w, () => Dm.num(get_dist(me, computer))) > 1)
        {
            Do(w, () => Waiting.NoSleep(me.forceMove(get_step(computer, get_dir(computer, table)) is turf t && Free(t) ? t : beside)));
            w.Run(Second);
        }
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, In(w, () => computer.dm_id), 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui(), x => x.Interface == "OperatingComputer");
        UiValue patient = window.Merged.Get("patient");
        Assert.False(patient.IsNull, "the computer sees its table's patient");
        List<string> readable = patient.Get("surgery_state").Items.Select(i => i.Text).ToList();
        _output.WriteLine("surgery state: " + string.Join(", ", readable));
        Assert.Contains("Organs are cut", readable);
        Assert.Contains("Bone is sawed", readable);
        Assert.Contains(window.Merged.Get("surgeries").Items, s => s.Get("name").Text != null);
        p.Session.UiClose(window.Window);
        w.Run(Second / 2);
        Do(w, () => Waiting.NoSleep(me.forceMove(beside)));
        w.Run(Second / 2);

        // 5. the hemostat: tg's radial of its operations on the client, "remove heart" picked; the heart lies on the table
        obj_item_organ_heart heart = In(w, () => pat.get_organ_slot(ORGAN_SLOT_HEART) as obj_item_organ_heart);
        Take<obj_item_hemostat>();
        Assert.True(p.Session.Click(Chest(), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        RadialMenuMessage menu = replica.TopRadial;
        Assert.True(menu != null, "no radial; " + Diag());
        _output.WriteLine("radial: " + string.Join(", ", menu.Elements.Select(e => e.Name)));
        int slice = menu.Elements.FindIndex(e => (e.Name ?? "").Contains("remove heart", StringComparison.OrdinalIgnoreCase));
        Assert.True(slice >= 0, "no remove heart slice");
        Assert.True(p.Session.RadialPick(menu.MenuId, slice + 1));
        w.Run(3 * Second);
        Assert.Null(In(w, () => pat.get_organ_slot(ORGAN_SLOT_HEART)));
        Assert.Same(In(w, () => table.loc), In(w, () => heart.loc));
        Assert.Contains(texts, t => t.Contains("You successfully extract"));
        List<string> heartLooks = States(replica, w, heart);
        _output.WriteLine("heart: " + string.Join(", ", heartLooks));
        Assert.Contains(heartLooks, s => s.StartsWith("heart", StringComparison.Ordinal));

        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, string.Join("\n", runtimes));
    }
}
