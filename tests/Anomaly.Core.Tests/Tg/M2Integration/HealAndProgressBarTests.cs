using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Integration;

/// <summary>
/// The M2 integration's translations on the paths a real server reaches (strict: a reached <c>Dm.NotTranslated</c> throws):
/// a full heal of a real human (the brain's trauma queries, the reagent holder's <c>clear_reagents</c> and
/// <c>update_total</c>), the progress bar of a do_after (<c>/datum/progressbar</c>, its image on the user's client) and
/// <c>get_all_contents</c>. (The hand click and the shove are M2 T2's ShoveTests.)
/// </summary>
public class HealAndProgressBarTests
{
    private static TgToyRoom StrictRoom(ulong seed)
    {
        TgToyRoom room = TgToyRoom.Boot(seed: seed);
        DmWorldState.Of(room.World).NotTranslatedThrows = true;
        return room;
    }

    private static void AssertNoRuntimes(TgToyRoom room) => Assert.True(room.Runtimes.Count == 0, string.Join("\n", room.RuntimeText));

    [Fact]
    public void AFullHealHealsEveryDamageTypeOfARealHumanAndEmptiesItsReagents()
    {
        TgToyRoom room = StrictRoom(83);
        room.In(() =>
        {
            var human = Dm.@new<mob_living_carbon_human>(TgToyRoom.T(4, 4));
            Waiting.NoSleep(human.adjust_oxy_loss(30));
            Waiting.NoSleep(human.adjust_tox_loss(20));
            Waiting.NoSleep(human.apply_damage(15, BRUTE, BODY_ZONE_CHEST));
            Waiting.NoSleep(human.apply_damage(10, BURN, BODY_ZONE_L_ARM));
            Assert.True(human.health < 50f, Dm.str(human.health));
            Waiting.NoSleep(human.fully_heal(HEAL_ALL));
            Assert.Equal(0f, Dm.num(human.get_oxy_loss()));
            Assert.Equal(0f, Dm.num(human.get_tox_loss()));
            Assert.Equal(0f, Dm.num(human.get_brute_loss()));
            Assert.Equal(0f, Dm.num(human.get_fire_loss()));
            Assert.Equal(100f, human.health);
            var holder = (datum_reagents)human.reagents;
            Assert.Equal(0f, holder.total_volume);
            Assert.Equal(CHEMICAL_NORMAL_PH, holder.ph);
            // the brain's trauma queries on a brain without traumas (brain_item.dm:606-739)
            var brain = (obj_item_organ_brain)human.get_organ_slot(ORGAN_SLOT_BRAIN);
            Assert.Equal(0f, Dm.num(brain.cure_all_traumas(TRAUMA_RESILIENCE_MAGIC)));
            Assert.Null(brain.has_trauma_type());
            Assert.Equal(0, ((DmList)brain.get_traumas_type()).len);
        });
        AssertNoRuntimes(room);
    }

    [Fact]
    public void TheBrainsTraumaQueriesFilterByTypeAndResilience()
    {
        TgToyRoom room = StrictRoom(84);
        room.In(() =>
        {
            var human = Dm.@new<mob_living_carbon_human>(TgToyRoom.T(4, 4));
            var brain = (obj_item_organ_brain)human.get_organ_slot(ORGAN_SLOT_BRAIN);
            var basic = Dm.@new<datum_brain_trauma>();
            var lobotomy = Dm.@new<datum_brain_trauma>();
            lobotomy.resilience = TRAUMA_RESILIENCE_LOBOTOMY;
            Dm.append(ref brain.traumas, basic);
            Dm.append(ref brain.traumas, lobotomy);
            Assert.Same(basic, brain.has_trauma_type(resilience: TRAUMA_RESILIENCE_BASIC));
            Assert.Equal(2, ((DmList)brain.get_traumas_type()).len); // TRAUMA_RESILIENCE_ABSOLUTE by default
            Assert.Equal(1, ((DmList)brain.get_traumas_type(resilience: TRAUMA_RESILIENCE_SURGERY)).len);
            // cure_all_traumas qdels each trauma it finds and counts them; the default resilience is BASIC
            Assert.Equal(1f, Dm.num(brain.cure_all_traumas()));
            Assert.True(QDELETED(basic));
            Assert.False(QDELETED(lobotomy));
        });
    }

    [Fact]
    public void AProgressBarPutsItsImageOnTheUsersClientAndStacksAndRestacks()
    {
        TgToyRoom room = StrictRoom(85);
        room.In(() =>
        {
            var human = Dm.@new<mob_living_carbon_human>(TgToyRoom.T(4, 4));
            var player = Dm.@new<client_test_m1_client>();
            human.client = player;
            var first = Dm.@new<datum_progressbar>(human, 10, human);
            var second = Dm.@new<datum_progressbar>(human, 10, human);
            Assert.Equal(1f, first.listindex);
            Assert.Equal(2f, second.listindex);
            Assert.Same(human, first.bar.loc);
            Assert.Equal("prog_bar_0", first.bar.icon_state);
            Assert.Equal(ABOVE_HUD_PLANE, first.bar.plane);
            Assert.Equal(APPEARANCE_UI_IGNORE_ALPHA, first.bar.appearance_flags);
            Assert.True(Dm.@in(first.bar, player.images));
            Assert.True(Dm.@in(second.bar, player.images));
            Assert.Equal(2, ((DmList)human.progressbars[human]).len);

            first.update(5);
            Assert.Equal("prog_bar_50", first.bar.icon_state);
            first.end_progress();
            Assert.Equal("prog_bar_50_fail", first.bar.icon_state);

            qdel(first);
            Assert.False(Dm.@in(first.bar, player.images));
            Assert.Equal(1f, second.listindex);
            Assert.Equal(1, ((DmList)human.progressbars[human]).len);
        });
        AssertNoRuntimes(room);
    }

    [Fact]
    public void ADoAfterOfAPlayerShowsItsProgressBarUntilItEnds()
    {
        TgToyRoom room = StrictRoom(86);
        mob_living_carbon_human human = null;
        client_test_m1_client player = null;
        room.In(() =>
        {
            human = Dm.@new<mob_living_carbon_human>(TgToyRoom.T(4, 4));
            player = Dm.@new<client_test_m1_client>();
            human.client = player;
            Waiting.Detach(do_after(human, 2 * SECONDS, human));
        });
        room.Step(2);
        room.In(() =>
        {
            var bar = Dm.each_cast<datum_progressbar>((DmList)human.progressbars[human]).Single();
            Assert.True(Dm.@in(bar.bar, player.images));
        });
        room.Step(2 * 20 + 20); // the 2 seconds, then QDEL_IN(src, PROGRESSBAR_ANIMATION_TIME) of end_progress
        room.In(() =>
        {
            Assert.Equal(0, length(player.images));
            Assert.Equal(0, length(human.progressbars?[human]));
        });
        AssertNoRuntimes(room);
    }

    [Fact]
    public void SpaceHasTgsImmutableSpaceAirAndAHumanSuffocatesThereWithoutARuntime()
    {
        TgToyRoom room = StrictRoom(88);
        mob_living_carbon_human human = null;
        room.In(() =>
        {
            var space = Dm.@new<turf_open_space>(TgToyRoom.T(4, 4)); // replaces the floor (BYOND: new /turf at a cell)
            Assert.IsType<datum_gas_mixture_immutable_space>(space.air);
            Assert.Same(turf_open_space.space_gas, space.air); // one static mixture for every space turf (space.dm:58)
            Assert.Equal(0f, Dm.num(space.air.total_moles()));
            human = Dm.@new<mob_living_carbon_human>(space);
        });
        room.Step(20 * 20); // ten SSmobs fires: breaths of nothing
        room.In(() => Assert.True(Dm.num(human.get_oxy_loss()) > 0, Dm.str(human.get_oxy_loss())));
        AssertNoRuntimes(room);
    }

    [Fact]
    public void TheBloodListsColourIsItsBloodTypesOrTheirMean()
    {
        TgToyRoom room = StrictRoom(89);
        room.In(() =>
        {
            var oPlus = (datum_blood_type)get_blood_type(typeof(datum_blood_type_human_o_plus));
            object red = Waiting.NoSleep(oPlus.get_color());
            Assert.True(Dm.eq(red, Waiting.NoSleep(get_color_from_blood_list(Dm.list())))); // none: O+'s colour (blood.dm:571-572)
            var one = Dm.list();
            one["dna-1"] = oPlus;
            Assert.True(Dm.eq(red, Waiting.NoSleep(get_color_from_blood_list(one))));
            var two = Dm.list();
            two["dna-1"] = oPlus;
            two["dna-2"] = get_blood_type(typeof(datum_blood_type_human_a_minus));
            DmList mixed = rgb2num((string)Waiting.NoSleep(get_color_from_blood_list(two)));
            DmList r = rgb2num((string)red);
            Assert.True(Dm.eq(r[1], mixed[1]) && Dm.eq(r[2], mixed[2]) && Dm.eq(r[3], mixed[3])); // two human types: the same red
            // A human with brute damage gets its blood's colour on the damage overlay without a runtime (the M2 path).
            var human = Dm.@new<mob_living_carbon_human>(TgToyRoom.T(4, 4));
            Waiting.NoSleep(human.apply_damage(30, BRUTE, BODY_ZONE_CHEST));
        });
        AssertNoRuntimes(room);
    }

    [Fact]
    public void GetAllContentsIsTheAtomAndEveryNestedContent()
    {
        TgToyRoom room = StrictRoom(87);
        room.In(() =>
        {
            var human = Dm.@new<mob_living_carbon_human>(TgToyRoom.T(4, 4));
            var all = (DmList)human.get_all_contents();
            Assert.Same(human, all[1]);
            foreach (atom_movable inside in Dm.each_cast<atom_movable>(human.contents))
            {
                Assert.True(Dm.@in(inside, all));
                foreach (atom_movable deeper in Dm.each_cast<atom_movable>(inside.contents))
                    Assert.True(Dm.@in(deeper, all));
            }
            // a flag on an atom keeps its contents out (but not the atom itself)
            var part = Dm.each_cast<obj_item_bodypart>(human.get_bodyparts()).First(p => length(p.contents) > 0);
            Dm.combine(ref part.flags_1, HOLOGRAM_1);
            var some = (DmList)human.get_all_contents(HOLOGRAM_1);
            Assert.True(Dm.@in(part, some));
            Assert.All(Dm.each_cast<atom_movable>(part.contents), a => Assert.False(Dm.@in(a, some)));
            Assert.Equal(Dm.list(Dm.kv("x", 0), Dm.kv("y", 0)).len, ((DmList)human.get_oversized_icon_offsets()).len);
            Assert.True(Dm.eq(0, ((DmList)human.get_oversized_icon_offsets())["x"]));
        });
        AssertNoRuntimes(room);
    }
}
