using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package J2 (D298): every profession takeable with its full starting kit. The sweep dresses a human in every joinable job's outfit,
/// with every backpack preference, through tg's equip; the hooks: the officer's department, the clown's clumsiness, the mime's vow and
/// spells, the chaplain's bible.
/// </summary>
public class JobKitTests(ITestOutputHelper output)
{
    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(24, 9, seed: seed);
        r.In(() => TgStrings.Mount(DmWorldState.Of(r.World).Files, TgClone.Root));
        return r;
    }

    private static mob_living_carbon_human Crewman(AudioRoom r, int x, int y, string key, Type job)
    {
        mob_living_carbon_human who = r.Join(x, y, key);
        r.In(() => Waiting.Detach(SSjob.equip_rank(who, SSjob.get_job_type(job) as datum_job, who.client)));
        r.Step(5);
        return who;
    }

    private static void Slot(List<string> wrong, datum_job job, mob_living_carbon_human H, Type wanted, float slot, string name)
    {
        object worn = H.get_item_by_slot(slot);
        if (wanted == null ? worn != null : worn?.GetType() != wanted)
            wrong.Add($"{job.title} {name}: wanted {wanted?.Name ?? "nothing"}, wears {worn?.GetType().Name ?? "nothing"}");
    }

    [Fact]
    public void EveryJobDressesInItsOutfitWithEveryBackpack()
    {
        AudioRoom r = Room(9901);
        var wrong = new List<string>();
        var inert = new SortedDictionary<string, SortedSet<string>>();
        string[] bags = { GBACKPACK, GSATCHEL, GDUFFELBAG, LSATCHEL, GMESSENGER, DBACKPACK, DSATCHEL, DDUFFELBAG, DMESSENGER };
        int dressed = 0;
        r.In(() =>
        {
            foreach (datum_job job in Dm.each<datum_job>(SSjob.joinable_occupations))
            {
                if (job.get_outfit() == null)
                {
                    output.WriteLine($"no outfit: {job.title}");
                    continue; // the silicons have no outfit
                }
                foreach (string bag in bags)
                {
                    mob_living_carbon_human H = Dm.@new<mob_living_carbon_human>(locate(4, 4, 1));
                    H.real_name = "Test Person";
                    H.backpack = bag;
                    datum_outfit_job outfit = Dm.@new(job.get_outfit() as Type) as datum_outfit_job;
                    Waiting.NoSleep(((datum_species)H.dna.species).pre_equip_species_outfit(job, H, FALSE));
                    Waiting.NoSleep(H.equip_outfit_and_loadout(outfit));
                    dressed++;
                    if (outfit.uniform != typeof(obj_item_clothing_under_color_random)) // tg's random jumpsuit deletes itself for a colour
                        Slot(wrong, job, H, outfit.uniform, ITEM_SLOT_ICLOTHING, "uniform");
                    Slot(wrong, job, H, outfit.suit, ITEM_SLOT_OCLOTHING, "suit");
                    Slot(wrong, job, H, outfit.gloves, ITEM_SLOT_GLOVES, "gloves");
                    Slot(wrong, job, H, outfit.shoes, ITEM_SLOT_FEET, "shoes");
                    Slot(wrong, job, H, outfit.head, ITEM_SLOT_HEAD, "head");
                    Slot(wrong, job, H, outfit.mask, ITEM_SLOT_MASK, "mask");
                    Slot(wrong, job, H, outfit.neck, ITEM_SLOT_NECK, "neck");
                    Slot(wrong, job, H, outfit.ears, ITEM_SLOT_EARS, "ears");
                    Slot(wrong, job, H, outfit.glasses, ITEM_SLOT_EYES, "glasses");
                    Slot(wrong, job, H, outfit.back, ITEM_SLOT_BACK, "back");
                    Slot(wrong, job, H, outfit.l_pocket, ITEM_SLOT_LPOCKET, "l_pocket");
                    Slot(wrong, job, H, outfit.r_pocket, ITEM_SLOT_RPOCKET, "r_pocket");
                    Slot(wrong, job, H, outfit.suit_store, ITEM_SLOT_SUITSTORE, "suit_store");

                    atom back = H.back;
                    if (outfit.box != null && Dm.locate_type(outfit.box, back?.contents) == null)
                        wrong.Add($"{job.title} ({bag}): no {outfit.box.Name} in {back?.GetType().Name}");
                    foreach (Type content in Dm.each_cast<Type>(outfit.backpack_contents))
                    {
                        if (Dm.locate_type(content, back?.contents) == null)
                            wrong.Add($"{job.title} ({bag}): no {content.Name} in {back?.GetType().Name}");
                    }

                    obj_item_card_id card = H.wear_id as obj_item_card_id;
                    if (card == null || card.GetType() != outfit.id)
                        wrong.Add($"{job.title}: the id is {H.wear_id?.GetType().Name}, wanted {outfit.id?.Name}");
                    else if (card.trim?.GetType() != outfit.id_trim)
                        wrong.Add($"{job.title}: the trim is {card.trim?.GetType().Name}, wanted {outfit.id_trim?.Name}");
                    if (Dm.truthy(outfit.pda_slot))
                    {
                        // tg's post_equip looks in the slot's item too (a PDA in a backpack, a belt)
                        atom holder = H.get_item_by_slot(Dm.num(outfit.pda_slot)) as atom;
                        if (holder is not obj_item_modular_computer_pda && Dm.locate<obj_item_modular_computer_pda>(holder?.contents) == null)
                            wrong.Add($"{job.title}: no PDA in its slot");
                    }

                    if (bag == GBACKPACK)
                    {
                        // the job's own after_spawn runs clean for an unconnected body
                        Waiting.NoSleep(H.mind_initialize());
                        H.mind.set_assigned_role(job);
                        Waiting.NoSleep(job.after_spawn(H, null));

                        // which of the job's items are still inert props
                        var worn = new List<atom_movable>();
                        foreach (obj_item I in Dm.each<obj_item>(H.get_equipped_items(INCLUDE_POCKETS)))
                        {
                            worn.Add(I);
                            foreach (atom_movable inside in Dm.each<atom_movable>(I.contents))
                                worn.Add(inside);
                        }
                        foreach (atom_movable I in worn.Where(I => InertSeam.IsInert((atom)I)))
                        {
                            if (!inert.TryGetValue(job.title, out SortedSet<string> set))
                                inert[job.title] = set = new SortedSet<string>();
                            set.Add(DmTypes.PathOf(I.GetType()));
                        }
                    }
                    qdel(H);
                }
            }
        });
        foreach (var (job, items) in inert)
            output.WriteLine($"{job}: {string.Join(", ", items)}");
        output.WriteLine($"dressed {dressed}");
        r.Step(3);
        Assert.True(dressed > 200, dressed.ToString());
        Assert.True(wrong.Count == 0, string.Join("\n", wrong.Distinct()));
        r.AssertNoRuntimes();
    }

    // tg's own security_officer_distribution.dm
    [Fact]
    public void TheOfficersDepartmentsAreDistributedAsTgsUnitTestSays()
    {
        AudioRoom r = Room(9902);
        r.In(() =>
        {
            void Test(string[] preferences, string[] expected)
            {
                DmList outcome = get_officer_departments(Dm.list(preferences.Cast<object>().ToArray()), Dm.list("a", "b", "c", "d"));
                Assert.Equal(expected, Enumerable.Range(1, (int)outcome.len).Select(i => Dm.str(outcome[(float)i])).ToArray());
            }
            Test(new[] { "a" }, new[] { "a" });
            Test(new[] { "a", "b" }, new[] { "a", "a" });
            Test(new[] { "a", "b", "c" }, new[] { "a", "a", "a" });
            Test(new[] { "a", "a", "b" }, new[] { "a", "a", "a" });
            Test(new[] { "a", "a", "b", "b" }, new[] { "a", "a", "b", "b" });
            Test(new[] { "a", "a", "a", "b" }, new[] { "a", "a", "b", "b" });
            Test(new[] { "a", "b", "c", "d" }, new[] { "a", "b", "b", "a" });
            Test(new[] { SEC_DEPT_NONE }, new[] { "d" });
            Test(new[] { "a", SEC_DEPT_NONE }, new[] { "a", "a" });
            Test(new[] { SEC_DEPT_NONE, SEC_DEPT_NONE, SEC_DEPT_NONE, SEC_DEPT_NONE }, new[] { "d", "d", "c", "c" });

            // the late joiner: a lone officer's department first, else the least manned, the preference first
            Assert.Equal("b", get_new_officer_distribution_from_late_join(SEC_DEPT_NONE, Dm.list("a", "b", "c"), Dm.list(Dm.kv("x", "b"))));
            Assert.Equal("c", get_new_officer_distribution_from_late_join("c", Dm.list("a", "b", "c"), Dm.list()));
            Assert.Equal("a", get_new_officer_distribution_from_late_join(SEC_DEPT_NONE, Dm.list("a", "b", "c"), Dm.list()));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheLateJoiningOfficerGetsHisDepartmentsHeadsetArmbandAndAccess()
    {
        AudioRoom r = Room(9903);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() =>
        {
            bob.client.prefs.write_preference(GLOB.preference_entries[typeof(datum_preference_choiced_security_department)] as datum_preference, SEC_DEPT_MEDICAL);
            datum_job job = SSjob.get_job_type(typeof(datum_job_security_officer)) as datum_job;
            Waiting.Detach(SSjob.equip_rank(bob, job, bob.client));
        });
        r.Step(3);
        r.In(() => Waiting.Detach(((datum_job)SSjob.get_job_type(typeof(datum_job_security_officer))).after_latejoin_spawn(bob)));
        r.Step(3);
        r.In(() =>
        {
            Assert.Equal(SEC_DEPT_MEDICAL, GLOB.security_officer_distribution[REF(bob)]);
            Assert.IsType<obj_item_radio_headset_headset_sec_alt_department_med>(bob.ears);
            Assert.NotNull(Dm.locate<obj_item_clothing_accessory_armband_medblue>(((obj_item_clothing_under)bob.w_uniform).attached_accessories));
            obj_item_card_id card = bob.wear_id as obj_item_card_id;
            Assert.IsType<datum_id_trim_job_security_officer_medical>(card.trim);
            Assert.True(Dm.@in(ACCESS_MEDICAL, card.access));
            Assert.True(Dm.@in(ACCESS_SECURITY, card.access));
            Assert.Equal(JOB_SECURITY_OFFICER_MEDICAL, Dm.str(card.assignment));
            obj_item_modular_computer_pda pda = bob.get_item_by_slot(ITEM_SLOT_BELT) as obj_item_modular_computer_pda;
            Assert.Equal(JOB_SECURITY_OFFICER_MEDICAL, Dm.str(pda.saved_job));
        });
        Assert.True(feed.Any("You have been assigned to Medical!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheClownIsClumsyRenamedAndHonks()
    {
        AudioRoom r = Room(9904);
        mob_living_carbon_human clown = Crewman(r, 4, 4, "Bob", typeof(datum_job_clown));
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(clown, TRAIT_CLUMSY));
            Assert.True(HAS_TRAIT(clown, TRAIT_CLOWN_ENJOYER));
            Assert.True(Dm.@in(clown.real_name, GLOB.clown_names) || Dm.truthy(clown.real_name));
            Assert.True(HAS_TRAIT(clown, TRAIT_WADDLING));
            Assert.IsType<obj_item_clothing_shoes_clown_shoes>(clown.shoes);
            Assert.IsType<obj_item_clothing_mask_gas_clown_hat>(clown.wear_mask);
            Assert.True(HAS_TRAIT(clown.get_organ_slot(ORGAN_SLOT_LIVER) as datum, TRAIT_COMEDY_METABOLISM));
        });

        // the horn in the pocket honks in the hand
        r.In(() =>
        {
            obj_item_bikehorn horn = clown.l_store as obj_item_bikehorn;
            Assert.NotNull(horn);
            Waiting.Detach(clown.put_in_active_hand(horn));
        });
        r.Step(1);
        r.Command(() => Waiting.Detach(((obj_item)clown.get_active_held_item()).attack_self(clown)));
        r.Step(2);
        Assert.NotEmpty(r.Sounds.Of(clown, "sound/items/bikehorn.ogg"));

        // the clown shoes squeak on a walk (the squeak component's own 50% chance per step, the steps counted)
        for (int i = 0; i < 6; i++)
        {
            r.In(() => Waiting.Detach(clown.Move(locate(5 + i, 4, 1), EAST)));
            r.Step(10);
        }
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("footstep/clownstep") == true);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBananaHasItsReagentsAndItsPeelSlips()
    {
        AudioRoom r = Room(9905);
        r.In(() =>
        {
            obj_item_food_grown_banana banana = Dm.@new<obj_item_food_grown_banana>(locate(4, 4, 1));
            Assert.True(Dm.truthy(((datum_reagents)banana.reagents).has_reagent(typeof(datum_reagent_consumable_banana))));
            Assert.IsType<obj_item_seeds_banana>(banana.seed);
            obj_item_grown_bananapeel peel = Dm.@new<obj_item_grown_bananapeel>(locate(5, 4, 1));
            Assert.NotNull(peel.GetComponent(typeof(datum_component_slippery)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMimesVowSilencesHimAndHisSpellsConjure()
    {
        AudioRoom r = Room(9906);
        Feed feed = Listen(r);
        mob_living_carbon_human mime = Crewman(r, 4, 4, "Bob", typeof(datum_job_mime));
        datum_action_cooldown_spell_vow_of_silence vow = null;
        r.In(() =>
        {
            vow = Dm.locate<datum_action_cooldown_spell_vow_of_silence>(mime.actions);
            Assert.NotNull(vow);
            Assert.True(HAS_MIND_TRAIT(mime, TRAIT_MIMING));
            Assert.False(Dm.truthy(mime.can_speak__proc()));
            Assert.True(HAS_TRAIT(mime, TRAIT_MIME_FAN));
        });

        // speaking is refused with tg's line
        r.Command(() => Waiting.Detach(mime.say("hello")));
        r.Step(2);
        Assert.True(feed.Any("Your vow of silence prevents you from speaking!"));

        // the guide: the wall is learned, the book gone; the wall stands dense and invisible on the mime's tile
        datum_action_cooldown_spell_conjure_invisible_wall wall = null;
        r.In(() =>
        {
            obj_item_book_granter_action_spell_mime_mimery guide = Dm.@new<obj_item_book_granter_action_spell_mime_mimery>(locate(4, 4, 1));
            guide.granted_action = typeof(datum_action_cooldown_spell_conjure_invisible_wall);
            Assert.True(Dm.truthy(guide.can_learn(mime)));
            Waiting.Detach(guide.on_reading_finished(mime));
            wall = Dm.locate<datum_action_cooldown_spell_conjure_invisible_wall>(mime.actions);
            Assert.NotNull(wall);
            Assert.True(QDELETED(guide));
            Assert.True(Dm.truthy(Waiting.NoSleep(wall.IsAvailable())));
        });
        r.Command(() => Waiting.Detach(wall.Trigger(mime)));
        r.Step(2);
        obj_effect_forcefield_mime field = r.In(() => Dm.locate<obj_effect_forcefield_mime>(locate(4, 4, 1)));
        Assert.NotNull(field);
        r.In(() =>
        {
            Assert.True(Dm.truthy(field.density));
            Assert.Equal(0, field.alpha);
            Assert.False(Dm.truthy(Waiting.NoSleep(wall.IsAvailable())));
        });
        r.Step(31 * Second);
        Assert.True(r.In(() => QDELETED(field))); // the wall's 30 seconds
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(wall.IsAvailable()))));

        // the chair seats the mime
        r.In(() =>
        {
            var chair = Dm.@new<datum_action_cooldown_spell_conjure_invisible_chair>(mime.mind);
            Waiting.Detach(chair.Grant(mime));
            Waiting.Detach(chair.Trigger(mime));
        });
        r.Step(2);
        Assert.True(r.In(() => mime.buckled is obj_structure_chair_mime));

        // breaking the vow (the cast, past tg's confirmation window) frees the voice and reopens the job
        float positions = r.In(() => SSjob.get_job(JOB_MIME) is datum_job_mime job ? job.total_positions : -1);
        r.In(() => Waiting.Detach(vow.cast(mime)));
        r.Step(2);
        r.In(() =>
        {
            Assert.False(HAS_MIND_TRAIT(mime, TRAIT_MIMING));
            Assert.True(Dm.truthy(mime.can_speak__proc()));
            Assert.Equal(positions + 1, ((datum_job_mime)SSjob.get_job(JOB_MIME)).total_positions);
            Assert.True(QDELETED(vow));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheChaplainsBibleHealsInHolyHandsAndTheFirstChaplainFixesTheReligion()
    {
        AudioRoom r = Room(9907);
        Feed feed = Listen(r);
        mob_living_carbon_human chaplain = Crewman(r, 4, 4, "Bob", typeof(datum_job_chaplain));
        mob_living_carbon_human patient = r.Join(5, 4, "Alice");
        obj_item_book_bible bible = null;
        r.In(() =>
        {
            Assert.Equal(HOLY_ROLE_HIGHPRIEST, Dm.num(chaplain.mind.holy_role));
            Assert.True(HAS_TRAIT(chaplain.mind, TRAIT_SPIRITUAL));
            Assert.Equal(DEFAULT_DEITY, Dm.str(GLOB.deity));
            bible = Dm.locate<obj_item_book_bible_booze>(chaplain.back.contents);
            Assert.NotNull(bible);
            Assert.Equal(DEFAULT_DEITY, Dm.str(bible.deity_name));
            Assert.Equal(Dm.str(GLOB.bible_name), bible.name);
            Assert.NotNull(Dm.locate<obj_item_reagent_containers_cup_glass_bottle_whiskey>(bible.contents)); // carved, with the whiskey
            Assert.True(Dm.truthy(bible.can_set_bible_skin(chaplain)));
            Waiting.NoSleep(patient.apply_damage(30, BRUTE, BODY_ZONE_CHEST));
        });

        float before = r.In(() => Dm.num(patient.get_brute_loss()));
        r.In(() => Assert.Equal(BLESSING_SUCCESS, Waiting.NoSleep(bible.bless(patient, chaplain))));
        Assert.Equal(before - 10, r.In(() => Dm.num(patient.get_brute_loss())));
        Assert.True(feed.Any("compel you to be healed"));

        // unholy hands burn on the book
        r.In(() =>
        {
            float burn = Dm.num(patient.get_fire_loss());
            Waiting.NoSleep(bible.attack__proc(chaplain, patient));
            Assert.Equal(burn + 10, Dm.num(patient.get_fire_loss()));
        });
        Assert.True(feed.Any("The book sizzles in your hands."));

        // the second chaplain is an acolyte with the null rod in hand; the rod's menu is tg's
        mob_living_carbon_human acolyte = Crewman(r, 7, 4, "Carl", typeof(datum_job_chaplain));
        r.In(() =>
        {
            Assert.Equal(HOLY_ROLE_PRIEST, Dm.num(acolyte.mind.holy_role));
            obj_item_nullrod rod = acolyte.get_active_held_item() as obj_item_nullrod ?? acolyte.get_inactive_held_item() as obj_item_nullrod;
            Assert.NotNull(rod);
            Assert.NotNull(rod.GetComponent(typeof(datum_component_subtype_picker)));
            Assert.NotNull(rod.GetComponent(typeof(datum_component_anti_magic)));
            Assert.True(Dm.@in(typeof(obj_item_nullrod_claymore), GLOB.nullrod_variants));
            Assert.True(HAS_TRAIT(rod, TRAIT_NULLROD_ITEM));
            rod.on_holy_weapon_picked(rod, acolyte);
            Assert.Equal(typeof(obj_item_nullrod), GLOB.holy_weapon_type);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheFrenchBeretAndTheChefsMoustacheChangeSpeech()
    {
        AudioRoom r = Room(9908);
        mob_living_carbon_human cook = Crewman(r, 4, 4, "Bob", typeof(datum_job_cook));
        r.In(() =>
        {
            Assert.IsType<obj_item_clothing_mask_fakemoustache_italian>(cook.wear_mask);
            Assert.IsType<datum_id_trim_job_cook_chef>(((obj_item_card_id)cook.wear_id).trim);
            DmList args = Dm.list_n(SPEECH_MESSAGE);
            args[SPEECH_MESSAGE] = "hello my friend";
            cook._SendSignal(COMSIG_MOB_SAY, Dm.list(cook, args));
            Assert.NotEqual("hello my friend", Dm.str(args[SPEECH_MESSAGE]));
        });
        // the second cook is a cook, not the chef
        mob_living_carbon_human second = Crewman(r, 6, 4, "Carl", typeof(datum_job_cook));
        r.In(() =>
        {
            Assert.IsType<datum_id_trim_job_cook>(((obj_item_card_id)second.wear_id).trim);
            Assert.IsType<obj_item_clothing_suit_apron_chef>(second.wear_suit);
            Assert.IsType<obj_item_clothing_head_soft_mime>(second.head);
        });
        r.AssertNoRuntimes();
    }
}
