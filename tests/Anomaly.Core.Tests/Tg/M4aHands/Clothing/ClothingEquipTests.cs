using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4aHands.Clothing;

/// <summary>
/// MetaStation's clothing (M4a clothing): the clothing types the map places (from tg's MetaStation.dmm) and the closet and
/// locker types it places, whose PopulateContents put in the rest.
/// </summary>
internal static class MetaClothing
{
    internal static readonly string[] MetaMapped =
    {
        "/obj/item/clothing/accessory/armband/hydro", "/obj/item/clothing/accessory/waistcoat",
        "/obj/item/clothing/ears/earmuffs", "/obj/item/clothing/glasses/blindfold", "/obj/item/clothing/glasses/hud/health",
        "/obj/item/clothing/glasses/hud/security/sunglasses", "/obj/item/clothing/glasses/hud/security/sunglasses/gars",
        "/obj/item/clothing/glasses/meson", "/obj/item/clothing/glasses/meson/engine", "/obj/item/clothing/glasses/monocle",
        "/obj/item/clothing/glasses/regular/hipster", "/obj/item/clothing/glasses/science",
        "/obj/item/clothing/glasses/sunglasses", "/obj/item/clothing/glasses/sunglasses/big",
        "/obj/item/clothing/glasses/welding", "/obj/item/clothing/gloves/cargo_gauntlet",
        "/obj/item/clothing/gloves/color/black", "/obj/item/clothing/gloves/color/blue",
        "/obj/item/clothing/gloves/color/fyellow", "/obj/item/clothing/gloves/color/orange",
        "/obj/item/clothing/gloves/color/yellow", "/obj/item/clothing/gloves/latex",
        "/obj/item/clothing/gloves/latex/nitrile", "/obj/item/clothing/head/beret/sec/navyofficer",
        "/obj/item/clothing/head/beret/sec/navywarden", "/obj/item/clothing/head/collectable/hop",
        "/obj/item/clothing/head/collectable/hos", "/obj/item/clothing/head/cone",
        "/obj/item/clothing/head/costume/festive", "/obj/item/clothing/head/costume/snowman",
        "/obj/item/clothing/head/costume/sombrero/green", "/obj/item/clothing/head/fedora",
        "/obj/item/clothing/head/hats/hos/beret/navyhos", "/obj/item/clothing/head/hats/tophat",
        "/obj/item/clothing/head/soft/green", "/obj/item/clothing/head/soft/grey", "/obj/item/clothing/head/soft/mime",
        "/obj/item/clothing/head/soft/red", "/obj/item/clothing/head/utility/chefhat",
        "/obj/item/clothing/head/utility/hardhat/orange", "/obj/item/clothing/head/utility/welding",
        "/obj/item/clothing/mask/animal/horsehead", "/obj/item/clothing/mask/animal/pig",
        "/obj/item/clothing/mask/balaclava", "/obj/item/clothing/mask/breath", "/obj/item/clothing/mask/breath/muzzle",
        "/obj/item/clothing/mask/fakemoustache", "/obj/item/clothing/mask/gas", "/obj/item/clothing/mask/gas/cyborg",
        "/obj/item/clothing/mask/gas/sechailer", "/obj/item/clothing/mask/muzzle", "/obj/item/clothing/mask/surgical",
        "/obj/item/clothing/mask/whistle", "/obj/item/clothing/neck/stethoscope", "/obj/item/clothing/neck/tie/black",
        "/obj/item/clothing/neck/tie/blue", "/obj/item/clothing/neck/tie/red", "/obj/item/clothing/shoes/cowboy/black",
        "/obj/item/clothing/shoes/laceup", "/obj/item/clothing/shoes/magboots", "/obj/item/clothing/shoes/sneakers/orange",
        "/obj/item/clothing/shoes/winterboots", "/obj/item/clothing/suit/apron", "/obj/item/clothing/suit/apron/chef",
        "/obj/item/clothing/suit/apron/surgical", "/obj/item/clothing/suit/costume/cyborg_suit",
        "/obj/item/clothing/suit/costume/snowman", "/obj/item/clothing/suit/costume/wellworn_shirt/messy/graphic/gamer",
        "/obj/item/clothing/suit/hazardvest", "/obj/item/clothing/suit/hooded/ablative",
        "/obj/item/clothing/suit/hooded/wintercoat", "/obj/item/clothing/suit/hooded/wintercoat/engineering",
        "/obj/item/clothing/suit/hooded/wintercoat/miner", "/obj/item/clothing/suit/hooded/wintercoat/science",
        "/obj/item/clothing/suit/jacket/hos/blue", "/obj/item/clothing/suit/jacket/officer/blue",
        "/obj/item/clothing/suit/jacket/straight_jacket", "/obj/item/clothing/suit/jacket/warden/blue",
        "/obj/item/clothing/suit/toggle/lawyer/black", "/obj/item/clothing/under/color/blue",
        "/obj/item/clothing/under/color/red", "/obj/item/clothing/under/color/white",
        "/obj/item/clothing/under/costume/buttondown/slacks/service", "/obj/item/clothing/under/misc/assistantformal",
        "/obj/item/clothing/under/misc/burial", "/obj/item/clothing/under/misc/mailman",
        "/obj/item/clothing/under/misc/vice_officer", "/obj/item/clothing/under/rank/medical/scrubs/purple",
        "/obj/item/clothing/under/rank/prisoner", "/obj/item/clothing/under/rank/prisoner/skirt",
        "/obj/item/clothing/under/rank/security/head_of_security/formal",
        "/obj/item/clothing/under/rank/security/officer/formal", "/obj/item/clothing/under/rank/security/warden/formal",
        "/obj/item/clothing/under/suit/black_really", "/obj/item/clothing/under/suit/burgundy",
        "/obj/item/clothing/under/suit/navy", "/obj/item/clothing/under/suit/red", "/obj/item/clothing/under/suit/tan",
    };

    internal static readonly string[] MetaClosets =
    {
        "/obj/structure/closet", "/obj/structure/closet/athletic_mixed", "/obj/structure/closet/bombcloset",
        "/obj/structure/closet/bombcloset/security", "/obj/structure/closet/boxinggloves",
        "/obj/structure/closet/cardboard", "/obj/structure/closet/crate", "/obj/structure/closet/crate/bin",
        "/obj/structure/closet/crate/coffin", "/obj/structure/closet/crate/engineering",
        "/obj/structure/closet/crate/engineering/electrical", "/obj/structure/closet/crate/freezer",
        "/obj/structure/closet/crate/freezer/blood", "/obj/structure/closet/crate/freezer/surplus_limbs",
        "/obj/structure/closet/crate/goldcrate/stocked", "/obj/structure/closet/crate/hydroponics",
        "/obj/structure/closet/crate/medical", "/obj/structure/closet/crate/mod", "/obj/structure/closet/crate/necropolis",
        "/obj/structure/closet/crate/preopen", "/obj/structure/closet/crate/secure",
        "/obj/structure/closet/crate/secure/weapon", "/obj/structure/closet/crate/silvercrate",
        "/obj/structure/closet/crate/solarpanel_small", "/obj/structure/closet/crate/trashcart",
        "/obj/structure/closet/crate/trashcart/laundry", "/obj/structure/closet/crate/wooden/toy",
        "/obj/structure/closet/emcloset", "/obj/structure/closet/emcloset/anchored", "/obj/structure/closet/firecloset",
        "/obj/structure/closet/l3closet", "/obj/structure/closet/l3closet/janitor",
        "/obj/structure/closet/l3closet/scientist", "/obj/structure/closet/l3closet/security",
        "/obj/structure/closet/l3closet/virology", "/obj/structure/closet/lasertag/blue",
        "/obj/structure/closet/lasertag/red", "/obj/structure/closet/masks", "/obj/structure/closet/radiation",
        "/obj/structure/closet/secure_closet", "/obj/structure/closet/secure_closet/atmospherics",
        "/obj/structure/closet/secure_closet/bar", "/obj/structure/closet/secure_closet/bar/all_access",
        "/obj/structure/closet/secure_closet/brig", "/obj/structure/closet/secure_closet/captains",
        "/obj/structure/closet/secure_closet/chemical", "/obj/structure/closet/secure_closet/chief_medical",
        "/obj/structure/closet/secure_closet/courtroom", "/obj/structure/closet/secure_closet/cytology",
        "/obj/structure/closet/secure_closet/detective", "/obj/structure/closet/secure_closet/engineering_chief",
        "/obj/structure/closet/secure_closet/engineering_electrical",
        "/obj/structure/closet/secure_closet/engineering_personal",
        "/obj/structure/closet/secure_closet/engineering_welding", "/obj/structure/closet/secure_closet/evidence",
        "/obj/structure/closet/secure_closet/exile", "/obj/structure/closet/secure_closet/freezer/empty/open",
        "/obj/structure/closet/secure_closet/freezer/fridge", "/obj/structure/closet/secure_closet/freezer/kitchen",
        "/obj/structure/closet/secure_closet/freezer/kitchen/maintenance",
        "/obj/structure/closet/secure_closet/freezer/meat", "/obj/structure/closet/secure_closet/hop",
        "/obj/structure/closet/secure_closet/hos", "/obj/structure/closet/secure_closet/hydroponics",
        "/obj/structure/closet/secure_closet/injection", "/obj/structure/closet/secure_closet/medical1",
        "/obj/structure/closet/secure_closet/medical2", "/obj/structure/closet/secure_closet/miner",
        "/obj/structure/closet/secure_closet/paramedic", "/obj/structure/closet/secure_closet/personal",
        "/obj/structure/closet/secure_closet/psychology", "/obj/structure/closet/secure_closet/quartermaster",
        "/obj/structure/closet/secure_closet/research_director", "/obj/structure/closet/secure_closet/security",
        "/obj/structure/closet/secure_closet/security/cargo", "/obj/structure/closet/secure_closet/security/engine",
        "/obj/structure/closet/secure_closet/security/med", "/obj/structure/closet/secure_closet/security/science",
        "/obj/structure/closet/secure_closet/security/sec", "/obj/structure/closet/secure_closet/warden",
        "/obj/structure/closet/toolcloset", "/obj/structure/closet/wardrobe/black", "/obj/structure/closet/wardrobe/green",
        "/obj/structure/closet/wardrobe/grey", "/obj/structure/closet/wardrobe/miner",
        "/obj/structure/closet/wardrobe/mixed", "/obj/structure/closet/wardrobe/pjs",
        "/obj/structure/closet/wardrobe/white",
    };

    /// <summary>
    /// The clothing classes the translated closet and storage code names (game/objects/structures/crates_lockers and
    /// game/objects/items/storage: closets, lockers, crates and the boxes in them, some of which populate only when opened): a
    /// superset of what Meta's closets can hold.
    /// </summary>
    internal static readonly string[] ClosetCodeClasses =
    {
        "obj_item_clothing_accessory_armband_cargo", "obj_item_clothing_accessory_armband_deputy",
        "obj_item_clothing_accessory_armband_engine", "obj_item_clothing_accessory_armband_medblue",
        "obj_item_clothing_accessory_armband_science", "obj_item_clothing_accessory_medal_bronze_heart",
        "obj_item_clothing_accessory_medal_conduct", "obj_item_clothing_accessory_medal_gold_captain",
        "obj_item_clothing_accessory_medal_plasma_nobel_science", "obj_item_clothing_accessory_medal_silver_security",
        "obj_item_clothing_accessory_medal_silver_valor", "obj_item_clothing_accessory_pocketprotector",
        "obj_item_clothing_ears_earmuffs", "obj_item_clothing_glasses_blindfold",
        "obj_item_clothing_glasses_hud_security_sunglasses", "obj_item_clothing_glasses_meson_engine",
        "obj_item_clothing_glasses_meson_engine_tray", "obj_item_clothing_glasses_regular",
        "obj_item_clothing_gloves_atmos", "obj_item_clothing_gloves_boxing", "obj_item_clothing_gloves_boxing_blue",
        "obj_item_clothing_gloves_boxing_green", "obj_item_clothing_gloves_boxing_yellow", "obj_item_clothing_gloves_latex",
        "obj_item_clothing_gloves_tackler", "obj_item_clothing_head_bio_hood_cmo",
        "obj_item_clothing_head_bio_hood_general", "obj_item_clothing_head_bio_hood_janitor",
        "obj_item_clothing_head_bio_hood_scientist", "obj_item_clothing_head_bio_hood_security",
        "obj_item_clothing_head_bio_hood_virology", "obj_item_clothing_head_costume_nightcap_blue",
        "obj_item_clothing_head_costume_nightcap_red", "obj_item_clothing_head_costume_powdered_wig",
        "obj_item_clothing_head_fedora_inspector_hat", "obj_item_clothing_head_hats_tophat",
        "obj_item_clothing_head_helmet_sec", "obj_item_clothing_head_helmet_taghelm_blue",
        "obj_item_clothing_head_helmet_taghelm_red", "obj_item_clothing_head_soft_black",
        "obj_item_clothing_head_soft_grey", "obj_item_clothing_head_soft_mime", "obj_item_clothing_head_utility_bomb_hood",
        "obj_item_clothing_head_utility_bomb_hood_security", "obj_item_clothing_head_utility_hardhat",
        "obj_item_clothing_head_utility_hardhat_red", "obj_item_clothing_head_utility_hardhat_welding_atmos",
        "obj_item_clothing_head_utility_radiation", "obj_item_clothing_head_utility_welding",
        "obj_item_clothing_mask_balaclava", "obj_item_clothing_mask_bandana_black", "obj_item_clothing_mask_bandana_blue",
        "obj_item_clothing_mask_bandana_gold", "obj_item_clothing_mask_bandana_green", "obj_item_clothing_mask_bandana_red",
        "obj_item_clothing_mask_bandana_skull_black", "obj_item_clothing_mask_bandana_white",
        "obj_item_clothing_mask_breath", "obj_item_clothing_mask_breath_muzzle", "obj_item_clothing_mask_gas",
        "obj_item_clothing_mask_gas_atmos", "obj_item_clothing_mask_luchador", "obj_item_clothing_mask_luchador_rudos",
        "obj_item_clothing_mask_luchador_tecnicos", "obj_item_clothing_mask_muzzle", "obj_item_clothing_mask_surgical",
        "obj_item_clothing_neck_link_scryer_loaded", "obj_item_clothing_neck_petcollar", "obj_item_clothing_neck_tie_black",
        "obj_item_clothing_neck_tie_red", "obj_item_clothing_shoes_jackboots", "obj_item_clothing_shoes_laceup",
        "obj_item_clothing_shoes_sneakers_black", "obj_item_clothing_shoes_sneakers_brown",
        "obj_item_clothing_shoes_sneakers_orange", "obj_item_clothing_shoes_sneakers_white",
        "obj_item_clothing_shoes_winterboots", "obj_item_clothing_suit_armor_vest",
        "obj_item_clothing_suit_armor_vest_alt_sec", "obj_item_clothing_suit_armor_vest_det_suit",
        "obj_item_clothing_suit_bio_suit_cmo", "obj_item_clothing_suit_bio_suit_general",
        "obj_item_clothing_suit_bio_suit_janitor", "obj_item_clothing_suit_bio_suit_scientist",
        "obj_item_clothing_suit_bio_suit_security", "obj_item_clothing_suit_bio_suit_virology",
        "obj_item_clothing_suit_bluetag", "obj_item_clothing_suit_costume_judgerobe", "obj_item_clothing_suit_hazardvest",
        "obj_item_clothing_suit_hooded_wintercoat", "obj_item_clothing_suit_jacket_bomber",
        "obj_item_clothing_suit_jacket_leather", "obj_item_clothing_suit_jacket_leather_biker",
        "obj_item_clothing_suit_jacket_straight_jacket", "obj_item_clothing_suit_redtag",
        "obj_item_clothing_suit_utility_bomb_suit", "obj_item_clothing_suit_utility_bomb_suit_security",
        "obj_item_clothing_suit_utility_fire_atmos", "obj_item_clothing_suit_utility_fire_firefighter",
        "obj_item_clothing_suit_utility_radiation", "obj_item_clothing_under_color_black",
        "obj_item_clothing_under_color_blue", "obj_item_clothing_under_color_darkblue",
        "obj_item_clothing_under_color_green", "obj_item_clothing_under_color_grey",
        "obj_item_clothing_under_color_jumpskirt_black", "obj_item_clothing_under_color_jumpskirt_blue",
        "obj_item_clothing_under_color_jumpskirt_darkblue", "obj_item_clothing_under_color_jumpskirt_green",
        "obj_item_clothing_under_color_jumpskirt_grey", "obj_item_clothing_under_color_jumpskirt_lightpurple",
        "obj_item_clothing_under_color_jumpskirt_orange", "obj_item_clothing_under_color_jumpskirt_pink",
        "obj_item_clothing_under_color_jumpskirt_red", "obj_item_clothing_under_color_jumpskirt_teal",
        "obj_item_clothing_under_color_jumpskirt_white", "obj_item_clothing_under_color_jumpskirt_yellow",
        "obj_item_clothing_under_color_lightpurple", "obj_item_clothing_under_color_orange",
        "obj_item_clothing_under_color_pink", "obj_item_clothing_under_color_red", "obj_item_clothing_under_color_teal",
        "obj_item_clothing_under_color_white", "obj_item_clothing_under_color_yellow",
        "obj_item_clothing_under_costume_buttondown_skirt_service",
        "obj_item_clothing_under_costume_buttondown_slacks_service", "obj_item_clothing_under_costume_jabroni",
        "obj_item_clothing_under_misc_assistantformal", "obj_item_clothing_under_misc_pj_blue",
        "obj_item_clothing_under_misc_pj_red", "obj_item_clothing_under_rank_prisoner",
        "obj_item_clothing_under_rank_prisoner_skirt", "obj_item_clothing_under_rank_security_officer",
        "obj_item_clothing_under_shorts_black", "obj_item_clothing_under_shorts_blue",
        "obj_item_clothing_under_shorts_green", "obj_item_clothing_under_shorts_grey",
        "obj_item_clothing_under_shorts_purple", "obj_item_clothing_under_shorts_red",
        "obj_item_clothing_under_suit_black_really",
    };

    /// <summary>The worn slots a human offers, in the order the sweep tries them.</summary>
    internal static readonly float[] Slots =
    {
        ITEM_SLOT_ICLOTHING, ITEM_SLOT_OCLOTHING, ITEM_SLOT_GLOVES, ITEM_SLOT_FEET, ITEM_SLOT_HEAD, ITEM_SLOT_MASK,
        ITEM_SLOT_NECK, ITEM_SLOT_EARS, ITEM_SLOT_EYES, ITEM_SLOT_BACK, ITEM_SLOT_BELT, ITEM_SLOT_ID, ITEM_SLOT_LPOCKET,
    };

    /// <summary>The slots tg only opens over a jumpsuit (human mob_can_equip / can_equip: belt, ID, pockets).</summary>
    internal static bool NeedsJumpsuit(float slot) => slot == ITEM_SLOT_BELT || slot == ITEM_SLOT_ID || slot == ITEM_SLOT_LPOCKET;

    /// <summary>The inert guard's refusals so far (InertSeam.MissingOverride's Dm.Deferred sites).</summary>
    internal static List<string> Refusals(World world) => DmWorldState.Of(world).DeferredSites.Where(s => s.StartsWith("inert guard:", StringComparison.Ordinal)).ToList();

    /// <summary>
    /// The clothing types Meta's closets put in: every Meta closet type made on <paramref name="spot"/> and populated with
    /// its translated PopulateContents, its contents walked (boxes in it too). A closet whose PopulateContents reaches an
    /// untranslated proc is reported in <paramref name="skipped"/>.
    /// </summary>
    internal static SortedSet<string> ClosetContents(turf spot, List<string> skipped)
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        void Walk(atom a)
        {
            foreach (atom_movable inner in Dm.each<atom_movable>(a))
            {
                if (inner is obj_item_clothing)
                    found.Add(DmTypes.PathOf(inner.GetType()));
                Walk(inner);
            }
        }
        foreach (string path in MetaClosets)
        {
            Type type = DmTypes.FromPath(path);
            if (type == null)
            {
                skipped.Add(path + ": no class");
                continue;
            }
            try
            {
                obj_structure_closet closet = Dm.@new(type, spot) as obj_structure_closet;
                if (closet == null)
                {
                    skipped.Add(path + ": not a closet");
                    continue;
                }
                if (!Dm.truthy(closet.contents_initialized))
                {
                    closet.contents_initialized = TRUE;
                    Waiting.NoSleep(closet.PopulateContents());
                }
                Walk(closet);
                qdel(closet);
            }
            catch (Exception e)
            {
                skipped.Add(path + ": " + e.GetType().Name + " " + e.Message);
            }
        }
        return found;
    }
}

/// <summary>
/// M4a clothing: tg's own equipped and dropped of /obj/item/clothing and its families (clothing.dm, _gloves.dm, _masks.dm,
/// _shoes.dm, _glasses.dm, _head.dm, _neck.dm, _suits.dm, _under.dm and Meta's subtypes) let a player put on, take off and
/// drop every piece of Meta's clothing without the inert guard's "not ported yet" (DV27), and the worn item is drawn on
/// the wearer (tg's update_worn_* and the families' worn_overlays, D142). A reached Dm.NotTranslated throws.
/// </summary>
public class ClothingEquipTests
{
    private readonly ITestOutputHelper _out;

    public ClothingEquipTests(ITestOutputHelper output) => _out = output;

    private static atom_movable_screen Screen(T2Room r, mob who, string key) => r.In(() => who.hud_used.screen_objects[key] as atom_movable_screen);

    public static IEnumerable<object[]> Families() => new[]
    {
        new object[] { "/obj/item/clothing/gloves/color/black", ITEM_SLOT_GLOVES, "gloves", GLOVES_LAYER },
        new object[] { "/obj/item/clothing/gloves/color/yellow", ITEM_SLOT_GLOVES, "gloves", GLOVES_LAYER },
        new object[] { "/obj/item/clothing/gloves/latex/nitrile", ITEM_SLOT_GLOVES, "gloves", GLOVES_LAYER },
        new object[] { "/obj/item/clothing/under/color/blue", ITEM_SLOT_ICLOTHING, "w_uniform", UNIFORM_LAYER },
        new object[] { "/obj/item/clothing/suit/hazardvest", ITEM_SLOT_OCLOTHING, "wear_suit", SUIT_LAYER },
        new object[] { "/obj/item/clothing/head/fedora", ITEM_SLOT_HEAD, "head", HEAD_LAYER },
        new object[] { "/obj/item/clothing/head/soft/red", ITEM_SLOT_HEAD, "head", HEAD_LAYER },
        new object[] { "/obj/item/clothing/mask/breath", ITEM_SLOT_MASK, "wear_mask", FACEMASK_LAYER },
        new object[] { "/obj/item/clothing/mask/gas/sechailer", ITEM_SLOT_MASK, "wear_mask", FACEMASK_LAYER },
        new object[] { "/obj/item/clothing/glasses/meson", ITEM_SLOT_EYES, "glasses", GLASSES_LAYER },
        new object[] { "/obj/item/clothing/glasses/hud/health", ITEM_SLOT_EYES, "glasses", GLASSES_LAYER },
        new object[] { "/obj/item/clothing/shoes/laceup", ITEM_SLOT_FEET, "shoes", SHOES_LAYER },
        new object[] { "/obj/item/clothing/neck/tie/black", ITEM_SLOT_NECK, "wear_neck", NECK_LAYER },
        new object[] { "/obj/item/clothing/ears/earmuffs", ITEM_SLOT_EARS, "ears", EARS_LAYER },
    };

    [Theory]
    [MemberData(nameof(Families))]
    public void APlayerPicksUpPutsOnTakesOffAndDropsItAndTheWornItemIsDrawnMeanwhile(string path, float slot, string field, int layer)
    {
        var r = new T2Room(4501);
        mob_living_carbon_human bob = r.Join(8, 8, "Bob", name: "bob");
        obj_item it = r.In(() => Dm.@new(DmTypes.FromPath(path), bob.loc) as obj_item);
        Assert.NotNull(it);
        atom_movable_screen toggle = Screen(r, bob, HUD_HUMAN_TOGGLE_INVENTORY);
        atom_movable_screen slotScreen = Screen(r, bob, "item_slot:" + (int)slot);
        r.Command(() => ClientSeam.Click(bob.client, toggle, ""));
        r.Step(3);

        // picking it up: the empty hand's click on the floor item (attack_hand -> attempt_pickup -> put_in_active_hand)
        r.Command(() => ClientSeam.Click(bob.client, it, "left=1;icon-x=16;icon-y=16"));
        r.Step(3);
        r.In(() => Assert.Same(it, bob.get_active_held_item()));

        // putting it on: the click on its HUD slot with the item in hand (equip_to_slot_if_possible)
        r.Command(() => ClientSeam.Click(bob.client, slotScreen, ""));
        r.Step(3);
        IDmAppearance worn = null;
        r.In(() =>
        {
            Assert.Same(it, Dm.get_var(bob, field));
            Assert.Null(bob.get_active_held_item());
            // tg's update_worn_<slot> built the overlay on the slot's layer (build_worn_icon, human_update_icons.dm:570)
            var standing = bob.overlays_standing[layer] as mutable_appearance;
            Assert.NotNull(standing);
            string state = (it.worn_icon_state is string w && w.Length > 0) ? w : it.icon_state;
            float drawn = it.alternate_worn_layer != null ? Dm.num(it.alternate_worn_layer) : layer;
            worn = Dm.each(bob.overlays).Cast<IDmAppearance>().FirstOrDefault(a => a.icon_state == state && Dm.eq(a.layer, -drawn));
            Assert.True(worn != null, $"the worn {state} overlay on layer {-drawn}: " + string.Join(", ", Dm.each(bob.overlays).Cast<IDmAppearance>().Select(a => $"{a.icon}:{a.icon_state}@{a.layer}")));
        });

        _out.WriteLine($"{path}: worn {worn.icon}:{worn.icon_state} layer {worn.layer}");

        // taking it off: the empty hand's click on the worn item's slot (attack_ui -> attack_hand -> attempt_pickup)
        r.Command(() => ClientSeam.Click(bob.client, slotScreen, ""));
        r.Step(3);
        r.In(() =>
        {
            Assert.Null(Dm.get_var(bob, field));
            Assert.Same(it, bob.get_active_held_item());
            Assert.Null(bob.overlays_standing[layer]);
            Assert.DoesNotContain(Dm.each(bob.overlays).Cast<IDmAppearance>(), a => ReferenceEquals(a, worn));
        });

        // dropping it: the drop binding (drop_item)
        r.Command(() => ClientSeam.Keybinding(bob.client, 5, true));
        r.Step();
        r.In(() =>
        {
            Assert.Null(bob.get_active_held_item());
            Assert.Same(bob.loc, it.loc);
        });
        Assert.Empty(MetaClothing.Refusals(r.World));
        r.AssertNoRuntimes();
    }

    [Theory]
    [InlineData("/obj/item/clothing/glasses/welding", "glasses")]
    [InlineData("/obj/item/clothing/head/utility/welding", "head")]
    public void WeldingGearDarkensTheView(string path, string field)
    {
        // tg's welding goggles and helmet: tint 2 (TINT_DARKENED), so the wearer's update_tint shows the "tint" fullscreen at severity 2
        var r = new T2Room(4503);
        mob_living_carbon_human bob = r.Join(8, 8, "Bob", name: "bob");
        r.In(() =>
        {
            obj_item_clothing it = Dm.@new(DmTypes.FromPath(path), bob.loc) as obj_item_clothing;
            Assert.True(Dm.truthy(bob.equip_to_appropriate_slot(it)));
            Assert.Same(it, Dm.get_var(bob, field));
            _out.WriteLine($"{path}: tint {it.tint}, screens {string.Join(", ", Dm.each(bob.screens).Select(k => k?.ToString()))}");
            Assert.True(Dm.@in("tint", bob.screens), "the tint fullscreen");
        });
        r.AssertNoRuntimes();
    }

    [Theory]
    [InlineData("/obj/item/clothing/accessory/armband/hydro")]
    [InlineData("/obj/item/clothing/accessory/waistcoat")]
    public void AnAccessoryIsPinnedOnTheWornJumpsuitDrawnWithItAndPoppedOffAgain(string path)
    {
        var r = new T2Room(4502);
        mob_living_carbon_human bob = r.Join(8, 8, "Bob", name: "bob");
        obj_item_clothing_under suit = null;
        obj_item_clothing_accessory acc = null;
        r.In(() =>
        {
            suit = Dm.@new<obj_item_clothing_under_color_blue>(bob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(suit, ITEM_SLOT_ICLOTHING))));
            acc = Dm.@new(DmTypes.FromPath(path), bob.loc) as obj_item_clothing_accessory;
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_active_hand(acc))));
        });
        atom_movable_screen toggle = Screen(r, bob, HUD_HUMAN_TOGGLE_INVENTORY);
        atom_movable_screen uniformSlot = Screen(r, bob, "item_slot:" + (int)ITEM_SLOT_ICLOTHING);
        r.Command(() => ClientSeam.Click(bob.client, toggle, ""));
        r.Step(3);

        // pinning it: the click on the worn jumpsuit's slot with the accessory in hand (the slot clicks the jumpsuit:
        // melee_attack_chain -> the jumpsuit's item_interaction -> attach_accessory)
        r.Command(() => ClientSeam.Click(bob.client, uniformSlot, "left=1;icon-x=16;icon-y=16"));
        r.Step(3);
        string state = r.In(() => Dm.truthy(acc.icon_state_is_worn) ? acc.icon_state : (string)acc.worn_icon_state);
        r.In(() =>
        {
            Assert.Same(suit, acc.loc);
            Assert.True(Dm.@in(acc, suit.attached_accessories));
            Assert.Null(bob.get_active_held_item());
            // the jumpsuit's worn overlay carries it (under/worn_overlays -> get_accessory_overlays -> generate_accessory_overlay)
            var standing = (mutable_appearance)bob.overlays_standing[UNIFORM_LAYER];
            Assert.True(Dm.each(standing.overlays).Cast<IDmAppearance>().Any(a => a.icon_state == state),
                $"the {state} overlay on the jumpsuit: " + string.Join(", ", Dm.each(standing.overlays).Cast<IDmAppearance>().Select(a => $"{a.icon}:{a.icon_state}")));
            Assert.Contains(Dm.each(bob.overlays).Cast<IDmAppearance>(), a => a.icon_state == standing.icon_state && Dm.eq(a.layer, -UNIFORM_LAYER));
        });

        // popping it off: alt + right click on the worn jumpsuit (click_alt_secondary -> pop_accessory -> put_in_hands)
        r.Step(2);
        r.Command(() => ClientSeam.Click(bob.client, uniformSlot, "right=1;alt=1;icon-x=16;icon-y=16"));
        r.Step(3);
        r.In(() =>
        {
            Assert.False(Dm.@in(acc, suit.attached_accessories));
            Assert.True(Dm.@in(acc, bob.held_items), $"the accessory is in a hand (loc {acc.loc})");
            var standing = (mutable_appearance)bob.overlays_standing[UNIFORM_LAYER];
            Assert.DoesNotContain(Dm.each(standing.overlays).Cast<IDmAppearance>(), a => a.icon_state == state);
        });
        Assert.Empty(MetaClothing.Refusals(r.World));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EveryMetaClothingTypeGoesOnAHumanAndComesOffWithoutARefusal()
    {
        // the closets are populated in a room of their own: what their other contents' code reaches is not this test's
        var closets = new T2Room(4510);
        var skippedClosets = new List<string>();
        SortedSet<string> fromClosets = closets.In(() => MetaClothing.ClosetContents(locate(8, 8, 1) as turf, skippedClosets));
        foreach (string s in skippedClosets)
            _out.WriteLine("closet not populated: " + s);
        foreach (string s in closets.Room.RuntimeText)
            _out.WriteLine("closet population runtime: " + string.Join(" / ", s.Split('\n').Take(8)));

        var r = new T2Room(4511);
        mob_living_carbon_human bare = r.Dummy(4, 4, name: "bare");
        mob_living_carbon_human suited = r.Dummy(10, 10, name: "suited");
        _out.WriteLine($"closet clothing: {fromClosets.Count} types");
        var types = new SortedSet<string>(MetaClothing.MetaMapped, StringComparer.Ordinal);
        types.UnionWith(fromClosets);
        foreach (string cls in MetaClothing.ClosetCodeClasses)
        {
            Type t = typeof(obj_item).Assembly.GetType("Anomaly.Core.Tg." + cls);
            Assert.True(t != null, cls);
            types.Add(DmTypes.PathOf(t));
        }
        _out.WriteLine($"swept: {types.Count} types");
        Assert.True(fromClosets.Count > 20, "the closets put clothing in: " + string.Join(", ", fromClosets));

        r.In(() =>
        {
            obj_item suit = Dm.@new<obj_item_clothing_under_color_grey>(suited.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(suited.equip_to_slot_if_possible(suit, ITEM_SLOT_ICLOTHING))));
        });
        int before = r.In(() => MetaClothing.Refusals(r.World).Count);
        var failures = new List<string>();
        var worn = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in types)
        {
            string failure = r.In(() =>
            {
                try
                {
                    return Wear(bare, suited, path, worn);
                }
                catch (Exception e)
                {
                    return e.GetType().Name + ": " + e.Message;
                }
            });
            if (failure != null)
                failures.Add(path + ": " + failure);
        }
        foreach (var kv in worn)
            _out.WriteLine($"{kv.Key} -> {kv.Value}");
        Assert.True(failures.Count == 0, string.Join("\n", failures));
        Assert.Empty(MetaClothing.Refusals(r.World).Skip(before));
        r.AssertNoRuntimes();
    }

    // tg's own mob_can_equip refuses these on a human (the pet collar: TRAIT_LESSER_HUMANOID or human_wearable, _neck.dm:506).
    private static readonly HashSet<string> RefusedByTg = new(StringComparer.Ordinal) { "/obj/item/clothing/neck/petcollar" };

    // Worn, these restrain the wearer's arms, so the wearer cannot take them off by hand (tg).
    private static readonly HashSet<string> StraitJackets = new(StringComparer.Ordinal) { "/obj/item/clothing/suit/jacket/straight_jacket" };

    // One item through the inventory path: picked up, put into its first slot the human accepts, taken off with the empty
    // hand's click on it, dropped. Returns null when it went through, else what failed.
    private static string Wear(mob_living_carbon_human bare, mob_living_carbon_human suited, string path, IDictionary<string, string> worn)
    {
        Type type = DmTypes.FromPath(path);
        if (type == null)
            return "no class";
        obj_item it = Dm.@new(type, bare.loc) as obj_item;
        if (it == null)
            return "not an item";
        foreach (float slot in MetaClothing.Slots)
        {
            if (!Dm.truthy(Dm.bitand(it.slot_flags, slot)))
                continue;
            mob_living_carbon_human who = MetaClothing.NeedsJumpsuit(slot) ? suited : bare;
            Waiting.NoSleep(it.forceMove(who.loc));
            if (!Dm.truthy(Waiting.NoSleep(who.put_in_hands(it))))
                return "put_in_hands refused it";
            // the self-equip delay (equip_delay_self's do_after, items.dm mob_can_equip) is the player's wait; bypassed here
            if (!Dm.truthy(Waiting.NoSleep(who.equip_to_slot_if_possible(it, slot, disable_warning: TRUE, bypass_equip_delay_self: TRUE))))
            {
                Waiting.NoSleep(who.dropItemToGround(it));
                continue;
            }
            if (!Dm.eq(who.get_item_by_slot(slot), it))
                return $"not in slot {slot} after equipping";
            worn[path] = "slot " + slot;
            if (StraitJackets.Contains(path))
            {
                // tg: the jacket restrains its wearer's arms (human equip_to_slot, inventory.dm: a suit with a breakouttime), so the wearer's own
                // hand cannot take it off; someone else strips it (strippable, M9). Here it is dropped by force.
                Waiting.NoSleep(it.attack_hand(who));
                if (!Dm.eq(who.get_item_by_slot(slot), it))
                    return "the wearer took off the strait jacket with restrained arms";
                Waiting.NoSleep(who.dropItemToGround(it, force: TRUE));
                if (it.loc != who.loc || HAS_TRAIT(who, TRAIT_RESTRAINED))
                    return "the strait jacket did not come off by force";
                qdel(it);
                return null;
            }
            if (it is obj_item_clothing_head_utility_chefhat)
            {
                // tg: the chef's hat's pocket has attack_hand_interact (pockets.dm:55), so the wearer's click opens the mouse's pocket; the hat is
                // taken off by a drag to the hand (the HUD).
                Waiting.NoSleep(who.dropItemToGround(it));
                qdel(it);
                return null;
            }
            Waiting.NoSleep(it.attack_hand(who));
            if (who.get_active_held_item() != it)
                return $"did not come off slot {slot} into the hand (loc {it.loc})";
            Waiting.NoSleep(who.dropItemToGround(it));
            if (it.loc != who.loc)
                return "not dropped";
            qdel(it);
            return null;
        }
        // an accessory (slot_flags NONE) is pinned onto the worn jumpsuit (item_interaction -> attach_accessory) and popped
        // off again into the hand (click_alt_secondary -> pop_accessory)
        if (it is obj_item_clothing_accessory accessory)
        {
            var uniform = suited.w_uniform as obj_item_clothing_under;
            Waiting.NoSleep(it.forceMove(suited.loc));
            if (!Dm.truthy(Waiting.NoSleep(suited.put_in_hands(it))))
                return "put_in_hands refused it";
            object attached = Waiting.NoSleep(uniform.item_interaction(suited, accessory));
            if (!Dm.eq(attached, ITEM_INTERACT_SUCCESS) || accessory.loc != uniform || !Dm.@in(accessory, uniform.attached_accessories))
                return $"not attached to the jumpsuit ({attached})";
            worn[path] = "pinned on the jumpsuit";
            Waiting.NoSleep(uniform.click_alt_secondary(suited));
            if (suited.get_active_held_item() != it && !Dm.@in(it, suited.held_items))
                return $"not popped off into the hand (loc {it.loc})";
            Waiting.NoSleep(suited.dropItemToGround(it));
            if (it.loc != suited.loc)
                return "not dropped";
            qdel(it);
            return null;
        }
        // no worn slot: tg's own mob_can_equip refuses it on a human
        if (!Dm.truthy(Waiting.NoSleep(bare.put_in_hands(it))))
            return "put_in_hands refused it";
        Waiting.NoSleep(bare.dropItemToGround(it));
        if (it.loc != bare.loc)
            return "not dropped";
        if (Dm.truthy(it.slot_flags) && !RefusedByTg.Contains(path))
            return "no slot took it (slot_flags " + it.slot_flags + ")";
        worn[path] = Dm.truthy(it.slot_flags) ? "refused by tg's own mob_can_equip" : "held only (no worn slot)";
        qdel(it);
        return null;
    }
}
