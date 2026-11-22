using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps S4d (D327): the Virtual Pet app (virtual_pet.dm) and its Orbie (pets/orbie/*.dm): the hologram made at install and sleeping in the
/// PDA, released and recalled, customized, levelled up with its new ability, taught a trick, the feed's chocolate, and its picture's parts.
/// </summary>
public class VirtualPetTests
{
    private static (AudioRoom r, mob_living_carbon_human alice, obj_item_modular_computer pda, datum_computer_file_program_virtual_pet app) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        mob_living_carbon_human alice = r.Join(4, 4, "alice");
        var pda = r.In(() => Dm.@new<obj_item_modular_computer_pda_crew_assistant>(locate(4, 4, 1)));
        var app = r.In(() =>
        {
            var pet = Dm.@new<datum_computer_file_program_virtual_pet>();
            Assert.True(Dm.truthy(pda.store_file(pet))); // on_install runs through COMSIG_COMPUTER_FILE_STORE
            return pet;
        });
        return (r, alice, pda, app);
    }

    private static void Act(AudioRoom r, mob user, datum_computer_file_program_virtual_pet app, obj_item_modular_computer pda, string action, DmList @params = null) => r.In(() =>
    {
        World w = World.Current;
        w.usr = user;
        Waiting.NoSleep(app.ui_act(action, @params ?? Dm.list(), Dm.@new<datum_tgui>(user, pda, "NtosVirtualPet")));
        w.usr = null;
    });

    [Fact]
    public void The_orbie_sleeps_in_the_pda_comes_out_and_goes_back()
    {
        var (r, alice, pda, app) = Room(4401);
        r.In(() =>
        {
            Assert.IsType<mob_living_basic_orbie>(app.pet);
            Assert.Same(pda, app.pet.loc);
            Assert.Contains(app, Dm.each<object>(GLOB.virtual_pets_list));
            Assert.Equal("asleep", app.get_pet_state());
            Assert.Equal("pet_preview", Dm.str(app.profile_picture_parts["icon_state"]));
        });
        Act(r, alice, app, pda, "summon_pet");
        r.In(() => Assert.IsAssignableFrom<turf>(app.pet.loc));
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.Contains("orbie_send_out", StringComparison.Ordinal));
        r.Step(30); // the summon's cooldown is 10 s
        r.In(() => app.summon_cooldown = 0);
        Act(r, alice, app, pda, "summon_pet");
        r.Step(40); // the recall's shrink, then the move back
        r.In(() => Assert.Same(pda, app.pet.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_customization_renames_colours_and_hats_the_pet()
    {
        var (r, alice, pda, app) = Room(4402);
        Act(r, alice, app, pda, "apply_customization", Dm.list(
            Dm.kv("chosen_hat", "/obj/item/clothing/head/hats/tophat"),
            Dm.kv("chosen_name", "Sparky"),
            Dm.kv("chosen_color", "#c3ecf3"),
            Dm.kv("chosen_gender", "female")));
        r.In(() =>
        {
            Assert.Equal("Sparky", app.pet.name);
            Assert.Equal(FEMALE, Dm.str(app.pet.gender));
            Assert.Same(typeof(obj_item_clothing_head_hats_tophat), app.selected_hat["type"]);
            Assert.NotNull(app.profile_picture_parts["hat_icon"]);
            DmList data = Waiting.NoSleep(app.ui_data(alice)) as DmList;
            Assert.NotNull(LAZYACCESS(app.pet.atom_colours, FIXED_COLOUR_PRIORITY)); // the colour is on the pet
            Assert.Equal("", Dm.str(data["pet_color"])); // tg's quirk: atom_colours holds list(colour, type), so the name is never found
            Assert.Equal("Sparky", Dm.str(data["pet_name"]));
            Assert.Equal(0f, Dm.num(data["can_alter_appearance"])); // a 10 s cooldown
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_level_up_grants_the_lights_and_announces_it_on_the_feed()
    {
        var (r, alice, pda, app) = Room(4403);
        r.In(() =>
        {
            app.current_level_progress = app.to_next_level - 1;
            app.increment_exp();
            Assert.Equal(2f, Dm.num(app.level));
            datum_ai_controller controller = (app.pet as mob_living_basic).ai_controller as datum_ai_controller;
            Assert.IsType<datum_action_cooldown_mob_cooldown_lights>(controller.blackboard[BB_LIGHTS_ABILITY]);
            Assert.Equal(2f, Dm.num(controller.blackboard[BB_VIRTUAL_PET_LEVEL]));
            Assert.Equal(200f, Dm.num(app.pet.maxHealth)); // Orbie's level up adds 100 health
            DmList update = Dm.each<DmList>(GLOB.global_pet_updates).Last();
            Assert.Equal("has reached level 2!", Dm.str(update["message"]));
            Assert.Same(app.profile_picture_parts, update["pet_picture"]);
        });
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.Contains("orbie_level_up", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_trick_is_taught_and_the_feed_area_drops_a_chocolate()
    {
        var (r, alice, pda, app) = Room(4404);
        r.In(() => GLOB.emote_list ??= init_emote_list() as DmList); // made at the world's boot (global_lists.dm), not in a test room
        Act(r, alice, app, pda, "teach_tricks", Dm.list(Dm.kv("trick_name", "Dance"), Dm.kv("tricks", Dm.list("flip", "nonsense", "spin", "none"))));
        r.In(() =>
        {
            datum_ai_controller controller = (app.pet as mob_living_basic).ai_controller as datum_ai_controller;
            Assert.Equal("Dance", Dm.str(controller.blackboard[BB_TRICK_NAME]));
            Assert.Equal(new[] { "flip", "spin" }, Dm.each<object>(controller.blackboard[BB_TRICK_SEQUENCE] as DmList).Select(Dm.str));
        });
        r.In(() =>
        {
            app.selected_area = get_area(pda) as area; // the dropzone is where the PDA is
            app.drop_feed();
            Assert.Single(Dm.each<obj_item_food_virtual_chocolate>(get_turf(pda).contents));
            Assert.Null(app.selected_area);
        });
        r.AssertNoRuntimes();
    }
}
