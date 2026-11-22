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
/// Gaps package C2 on MetaStation through the real server and a client's own stack: every pet Meta maps is alive with its tree (Ian as the puppy he is without a save), Ian
/// wanders and the replica sees him move, a crewman clicks him with an empty hand and the client gets tg's pet line and heart, a word makes him heel; Poly, Runtime and the
/// others run on with no new runtime. Prints the rough cost of all the pets on an idle round.
/// </summary>
public class PetsGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static T Find<T>(RealHeadlessWorld w) where T : class => In(w, () => Dm.each<T>(world).FirstOrDefault());

    [MetaServerFact]
    public void Metas_pets_live_Ian_wanders_is_petted_and_obeys_and_the_client_sees_it()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 97, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Crewman");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var chat = new List<ServerText>();
        p.Client.ServerTextReceived += chat.Add;
        w.Run(Second);
        // Meta's boot reaches code of later milestones (strict: false)
        var errors = new List<Exception>();
        Action<Exception> sink = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e =>
        {
            sink?.Invoke(e);
            // the rig's table of sound lengths (RigSoundLengths) lacks some of the rooms' ambience: not a pet's
            if (!e.Message.Contains("rustg_sound_length", StringComparison.Ordinal) && !e.Message.Contains("play_ambience", StringComparison.Ordinal))
                errors.Add(e);
        };

        // every pet Meta places: live, with its own controller running
        var pets = new (Type Path, string Tree)[]
        {
            (typeof(mob_living_basic_pet_dog_corgi_puppy_ian), "dog"),
            (typeof(mob_living_basic_pet_dog_corgi_puppy_void), "dog"),
            (typeof(mob_living_basic_pet_dog_corgi_puppy_slime), "dog"),
            (typeof(mob_living_basic_pet_dog_pug_mcgriff), "dog"),
            (typeof(mob_living_basic_pet_cat_runtime), "cat"),
            (typeof(mob_living_basic_parrot_poly), "parrot"),
            (typeof(mob_living_basic_pet_fox_renault), "fox"),
            (typeof(mob_living_basic_sloth_citrus), "sloth"),
            (typeof(mob_living_basic_lizard_wags_his_tail), "lizard"),
            (typeof(mob_living_basic_goose_vomit), "goose"),
            (typeof(mob_living_basic_pet_penguin_baby_permanent), "penguin"),
            (typeof(mob_living_basic_spider_giant_sgt_araneus), "spider"),
        };
        var alive = new List<mob_living>();
        foreach (var (path, tree) in pets)
        {
            var pet = In(w, () => Dm.each<mob_living>(world).FirstOrDefault(m => m.GetType() == path));
            Assert.True(pet != null, path.Name + " is on the map");
            Assert.False(In(w, () => InertSeam.IsInert(pet)), path.Name + " is live");
            var brain = In(w, () => pet.ai_controller as datum_ai_controller);
            Assert.True(brain != null, path.Name + " has its controller");
            Assert.Contains(tree, In(w, () => brain.behavior_tree_json as string));
            Assert.Equal(AI_STATUS_ON, In(w, () => brain.ai_status as string));
            alive.Add(pet);
        }
        Assert.Null(Find<mob_living_basic_pet_dog_corgi_ian>(w)); // without a save he is his puppy (corgi.dm:393)
        var ian = (mob_living_basic_pet_dog_corgi_puppy_ian)alive[0];
        Assert.Equal("Ian", In(w, () => ian.name));
        Assert.True(In(w, () => Dm.@in(typeof(obj_structure_bed_dogbed_ian), Dm.list(typeof(obj_structure_bed_dogbed_ian)))));

        // he wanders on his own and the client sees it (idle_dog: 5% a second to step)
        Do(w, () => Waiting.NoSleep(me.forceMove(get_turf(ian))));
        w.Run(Second);
        var start = In(w, () => (ian.x, ian.y));
        ReplicaEntity seen = replica.Entities.Single(e => e.Id == ian.dm_id);
        Assert.True(replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look));
        Assert.Equal("puppy", replica.ResolveString(look.IconState));
        float firstX = seen.State.X, firstY = seen.State.Y;
        bool moved = false;
        // beside the cabinet and the painter only 3 of pick(alldirs) are free
        for (int i = 0; i < 400 * Second && !moved; i++)
        {
            w.Run(1);
            moved = In(w, () => (ian.x, ian.y)) != start;
        }
        Assert.True(moved, "Ian wandered");
        w.Run(Second);
        seen = replica.Entities.Single(e => e.Id == ian.dm_id);
        Assert.True(seen.State.X != firstX || seen.State.Y != firstY, "the replica followed him");

        // a crewman pets him: tg's line in the chat, the heart on his turf reaches the client
        Do(w, () => Waiting.NoSleep(me.forceMove(get_step(ian, NORTH))));
        Do(w, () => { me.combat_mode = FALSE; });
        w.Run(4);
        chat.Clear();
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, ian.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        Assert.Contains(chat, t => t.Html.Contains("pet", StringComparison.Ordinal));
        var heart = In(w, () => Dm.each<obj_effect_temp_visual_heart>(world).FirstOrDefault());
        Assert.NotNull(heart);
        Assert.Contains(replica.Entities, e => e.Id == heart.dm_id);

        // a friend's word: "heel" and he follows; he turns toward the commander's new place
        Do(w, () => Waiting.NoSleep(ian.befriend(me)));
        w.Run(2);
        var brainIan = In(w, () => ian.ai_controller as datum_ai_controller);
        Do(w, () => Waiting.NoSleep(me.say("Ian, heel")));
        w.Run(2);
        Assert.IsType<datum_pet_command_follow_dog>(In(w, () => brainIan.blackboard[BB_ACTIVE_PET_COMMAND]));
        Assert.Same(me, In(w, () => brainIan.blackboard[BB_CURRENT_PET_TARGET]));

        // tg's way to the commands: hold the key and hover him: his radial opens; "Stay" picked there makes him sit, with the commander's emote
        ClickTarget onIan = new ClickTarget(TargetKind.Entity, ian.dm_id, 0, 0, 0, 16, 16);
        p.Session.Keybinding(KeybindingCode.ViewPetCommands);
        w.Run(2);
        Assert.True(p.Session.Hover(onIan, 1000));
        w.Run(4);
        RadialMenuMessage menu = replica.TopRadial;
        Assert.NotNull(menu);
        output.WriteLine("the radial: " + string.Join(", ", menu.Elements.Select(e => e.Name)));
        Assert.Contains(menu.Elements, e => e.Name == "Stay");
        Assert.Contains(menu.Elements, e => e.Name == "Fetch");
        Assert.DoesNotContain(menu.Elements, e => e.Name == "Good Boy"); // hidden
        Assert.True(replica.TryGetAppearance(menu.Elements[0].Icon, out AppearanceEntry halt), "tg's radial_pets picture");
        Assert.Equal("halt", replica.ResolveString(halt.IconState));
        int stay = menu.Elements.ToList().FindIndex(e => e.Name == "Stay");
        Assert.True(p.Session.RadialPick(menu.MenuId, stay + 1));
        w.Run(Second);
        Assert.IsType<datum_pet_command_idle>(In(w, () => brainIan.blackboard[BB_ACTIVE_PET_COMMAND]));
        p.Session.Keybinding(KeybindingCode.ViewPetCommands, up: true);

        // the others move too, with no runtime, a minute on the station
        var before = In(w, () => alive.Select(m => (m.x, m.y)).ToList());
        var movers = new HashSet<string>();
        for (int i = 0; i < 60; i++)
        {
            w.Run(Second);
            foreach (var (pet, index) in alive.Select((m, n) => (m, n)))
                if (In(w, () => (pet.x, pet.y)) != before[index])
                    movers.Add(pet.GetType().Name);
        }
        output.WriteLine("moved in a minute: " + string.Join(", ", movers));
        Assert.True(movers.Count >= 3, "the pets wander: " + string.Join(", ", movers));
        Assert.True(errors.Count == 0, string.Join("\n----\n", errors.Select(e => e.ToString())));

        // the rough cost of all of Meta's pets on an idle round: their plans, 4 a second each
        Do(w, () =>
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 400; i++)
                foreach (mob_living pet in alive)
                    (pet.ai_controller as datum_ai_controller).SelectBehaviors(0.25f);
            double perSecond = clock.Elapsed.TotalMilliseconds * 1000 / 100; // 4 plans a second: 400 rounds of plans are 100 seconds
            output.WriteLine($"Meta's {alive.Count} pets' SelectBehaviors: {perSecond:F1} us per second of round ({clock.Elapsed.TotalMilliseconds * 1000 / 400 / alive.Count:F1} us a plan)");
        });
        Assert.True(errors.Count == 0, string.Join("\n----\n", errors.Select(e => e.ToString())));
        w.World.RuntimeErrorSink = sink;
    }
}
