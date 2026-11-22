// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/dummy.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the dummy's type block, Initialize, Destroy, Life, attach_rot, set_species with harvest_organs,
// setup_human_dna and log_mob_tag, the consistent dummies and humans, get_consistent_feature_entry and
// create_consistent_human_dna translated (M2 P2: tg's unit tests allocate /mob/living/carbon/human/consistent, scope.md
// 2g). M10x slice E1: the preference preview's delete_equipment (the wardrobe's recycling deletes, SSwardrobe is not declared),
// has_equipped and wipe_state; not its apply_height (dummy.dm:102: the port's apply_height takes an image, and every mannequin is of medium height). The dummy pool (generate_or_wait_for_human_dummy and its kin) came with gaps B1 (the imaginary friend's picture).

// tg: code/modules/mob/living/carbon/human/dummy.dm:1
[DmPath("/mob/living/carbon/human/dummy")]
public partial class mob_living_carbon_human_dummy : mob_living_carbon_human
{
    // tg: code/modules/mob/living/carbon/human/dummy.dm:2-4  the var overrides (rules 10.2)
    public mob_living_carbon_human_dummy()
    {
        real_name = "Test Dummy";
        mouse_drag_pointer = MOUSE_INACTIVE_POINTER;
        visual_only_organs = TRUE;
    }

    // tg: code/modules/mob/living/carbon/human/dummy.dm:5
    public float in_use = FALSE;

    // The New() that INITIALIZE_IMMEDIATE(/mob/living/carbon/human/dummy) defines (__DEFINES/subsystems.dm:96; inline, rules
    // 11.1 (c), (d))
    // tg: code/modules/mob/living/carbon/human/dummy.dm:7
    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        object loc = Dm.arg(__extra, 0);
        await base.New(Dm.forward(__extra, loc));
        if (!Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
        {
            float previous_initialized_value = SSatoms.initialized;
            SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD;
            DmList args = Dm.args(Dm.forward(__extra, loc));
            args[1] = TRUE;
            SSatoms.InitAtom(this, FALSE, args);
            SSatoms.initialized = previous_initialized_value;
        }
    }

    // tg: code/modules/mob/living/carbon/human/dummy.dm:9
    [DmParams("mapload", "species")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_species species = Dm.arg<datum_species>(__extra, 0);
        object dot = null;
        dot = base.Initialize(mapload, Dm.forward(__extra, species));
        ADD_TRAIT(this, TRAIT_GODMODE, INNATE_TRAIT);
        ADD_TRAIT(this, TRAIT_PREVENT_BLINKING, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/dummy.dm:14
    public override object Destroy(float? force = null)
    {
        in_use = FALSE;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/carbon/human/dummy.dm:18
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        return null;
    }

    // tg: code/modules/mob/living/carbon/human/dummy.dm:21
    [DmParams("mapload")]
    public override async DmTask attach_rot(params object[] __extra)
    {
        return;
    }

    // tg: code/modules/mob/living/carbon/human/dummy.dm:24
    [DmParams("mrace", "icon_update", "pref_load", "replace_missing")]
    public override void set_species(object mrace = null, float? icon_update = null, params object[] __extra)
    {
        float? pref_load = Dm.arg<float?>(__extra, 0);
        float? replace_missing = Dm.arg<float?>(__extra, 1);
        icon_update ??= TRUE;
        pref_load ??= FALSE;
        replace_missing ??= TRUE;
        Waiting.NoSleep(harvest_organs());
        base.set_species(mrace, icon_update, Dm.forward(__extra, pref_load, replace_missing));
    }

    ///Let's extract our dummies organs and limbs for storage, to reduce the cache missed that spamming a dummy cause
    // tg: code/modules/mob/living/carbon/human/dummy.dm:29
    public virtual async DmTask harvest_organs()
    {
        foreach (object slot in Dm.each(Dm.list(ORGAN_SLOT_BRAIN, ORGAN_SLOT_HEART, ORGAN_SLOT_LUNGS, ORGAN_SLOT_APPENDIX,
            ORGAN_SLOT_EYES, ORGAN_SLOT_EARS, ORGAN_SLOT_TONGUE, ORGAN_SLOT_LIVER, ORGAN_SLOT_STOMACH)))
        {
            obj_item_organ current_organ = get_organ_slot(slot) as obj_item_organ; //Time to cache it lads
            if (current_organ != null)
            {
                await current_organ.Remove(this, special: TRUE); //Please don't somehow kill our dummy
                // GAP(lead): SSwardrobe (controllers/subsystem/wardrobe.dm) is not declared; the wardrobe's stock is the
                // preference menu's cache (M9).
                // M8 slice G: a marked leaf (the hallucinations dress dummies): the wardrobe would keep the organ for the next dummy; here it is deleted
                Dm.Deferred("code/modules/mob/living/carbon/human/dummy.dm:35", "harvest_organs: SSwardrobe.recycle_object(current_organ) (the wardrobe, M10+)");
                qdel(current_organ);
            }
        }

        datum_species current_species = dna.species as datum_species;
        foreach (object organ_path in Dm.each(current_species.mutant_organs))
        {
            obj_item_organ current_organ = get_organ_by_type(organ_path) as obj_item_organ;
            if (current_organ != null)
            {
                await current_organ.Remove(this, special: TRUE); //Please don't somehow kill our dummy
                Dm.Deferred("code/modules/mob/living/carbon/human/dummy.dm:42", "harvest_organs: SSwardrobe.recycle_object(current_organ) (the wardrobe, M10+)");
                qdel(current_organ);
            }
        }
    }

    //Instead of just deleting our equipment, we save what we can and reinsert it into SSwardrobe's store
    //Hopefully this makes preference reloading not the worst thing ever
    // tg: code/modules/mob/living/carbon/human/dummy.dm:47
    public override async DmTask delete_equipment()
    {
        DmList items_to_check = get_equipped_items(INCLUDE_POCKETS | INCLUDE_HELD | INCLUDE_PROSTHETICS | INCLUDE_ABSTRACT) as DmList;
        DmList to_nuke = Dm.list(); //List of items queued for deletion, can't qdel them before iterating their contents in case they hold something
        ///Travel to the bottom of the contents chain, expanding it out
        for (int i = 1; i <= Dm.length(items_to_check); i++) //Needs to be a c style loop since it can expand
        {
            obj_item checking = items_to_check[i] as obj_item;
            if (QDELETED(items_to_check[i])) //Nulls in the list, depressing
                continue;
            if (checking == null) //What the fuck are you on
            {
                Dm.append(ref to_nuke, items_to_check[i]);
                continue;
            }

            DmList contents = checking.contents;
            if (Dm.truthy(Dm.length(contents)))
            {
                Dm.combine(ref items_to_check, contents); //Please don't make an infinite loop somehow thx
                Dm.append(ref to_nuke, checking); //Goodbye
                continue;
            }

            //I'm making the bet that if you're empty of other items you're not going to OOM if reapplied. I assume you're here because I was wrong
            if (checking.loc is mob checkings_owner)
                await checkings_owner.temporarilyRemoveItemFromInventory(checking, TRUE); //Clear out of there yeah?
            // SSwardrobe.recycle_object(checking): GAP(lead), SSwardrobe is not declared (as harvest_organs): the item is deleted
            Dm.Deferred("code/modules/mob/living/carbon/human/dummy.dm:69", "delete_equipment: SSwardrobe.recycle_object(checking) (the wardrobe, M10+)");
            qdel(checking);
        }

        foreach (object delete in Dm.each(to_nuke))
            qdel(delete);
    }

    // tg: code/modules/mob/living/carbon/human/dummy.dm:74
    public override async DmTask<object> has_equipped(obj_item item = null, object slot = null, float? initial = null)
    {
        initial ??= FALSE;
        Dm.combine(ref item.item_flags, IN_INVENTORY);
        if (!Dm.truthy(await item.visual_equipped(this, slot, initial)))
            return FALSE;
        if (!Dm.truthy(Dm.bitand(slot, item.slot_flags))) // Things below only update if slotted in (ie: not held)
            return TRUE;
        await add_item_coverage(item);
        if (Dm.truthy(item.hair_mask))
        {
            LAZYADD(ref hair_masks, item.hair_mask);
            update_hair();
        }
        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/human/dummy.dm:87
    public virtual async DmTask wipe_state()
    {
        await delete_equipment();
        await update_lips(null, null, null, update: FALSE);
        cut_overlays(); // tg passes TRUE to a proc that takes no arguments
        clear_filters();
    }

    // tg: code/modules/mob/living/carbon/human/dummy.dm:90
    public override async DmTask setup_human_dna()
    {
        await randomize_human_normie(this, randomize_mutations: FALSE);
    }

    // tg: code/modules/mob/living/carbon/human/dummy.dm:93
    public override void log_mob_tag(object text = null, DmList data = null)
    {
        return;
    }
}

// tg: code/modules/mob/living/carbon/human/dummy.dm:107 (global procs; interleaved types, rules 1.9)
public static partial class GlobalProcs
{
    /// Takes in an accessory list and returns the first entry from that list, ensuring that we dont return SPRITE_ACCESSORY_NONE in the process.
    // tg: code/modules/mob/living/carbon/human/dummy.dm:107
    public static object get_consistent_feature_entry(DmList accessory_feature_list = null)
    {
        object consistent_entry = ((DmList)Dm.sub(accessory_feature_list, SPRITE_ACCESSORY_NONE))[1];
        if (!(consistent_entry != null)) throw Dm.CRASH(assertion_message("code/modules/mob/living/carbon/human/dummy.dm", 109, "!isnull(consistent_entry)"), "code/modules/mob/living/carbon/human/dummy.dm", 109);
        return consistent_entry;
    }

    // tg: code/modules/mob/living/carbon/human/dummy.dm:112
    public static void create_consistent_human_dna(mob_living_carbon_human target = null)
    {
        target.dna.features[FEATURE_MUTANT_COLOR] = COLOR_VIBRANT_LIME;
        target.dna.features[FEATURE_TAIL_FISH_COLOR] = "#8AC7ED";
        foreach (object feature_key in Dm.each(SSaccessories.feature_list))
            target.dna.features[feature_key] = get_consistent_feature_entry(SSaccessories.feature_list[feature_key] as DmList);
        target.dna.initialize_dna(newblood_type: get_blood_type(typeof(datum_blood_type_human_o_minus)), create_mutation_blocks: FALSE, randomize_features: FALSE);
        // UF and UI are nondeterministic, even though the features are the same some blocks will randomize slightly
        // In practice this doesn't matter, but this is for the sake of 100%(ish) consistency
        if (create_consistent_human_dna__consistent_UF == null || create_consistent_human_dna__consistent_UI == null)
        {
            create_consistent_human_dna__consistent_UF = target.dna.unique_features;
            create_consistent_human_dna__consistent_UI = target.dna.unique_identity;
        }
        else
        {
            target.dna.unique_features = create_consistent_human_dna__consistent_UF;
            target.dna.unique_identity = create_consistent_human_dna__consistent_UI;
        }
    }

    // tg: code/modules/mob/living/carbon/human/dummy.dm:121-122  var/static/consistent_UF and consistent_UI (proc-level
    // statics, rules 10.6)
    private static ref object create_consistent_human_dna__consistent_UF => ref TgState.Current.GlobalProcs__create_consistent_human_dna__consistent_UF;
    private static ref object create_consistent_human_dna__consistent_UI => ref TgState.Current.GlobalProcs__create_consistent_human_dna__consistent_UI;
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/carbon/human/dummy.dm:121  var/static/consistent_UF
    internal object GlobalProcs__create_consistent_human_dna__consistent_UF;
    // tg: code/modules/mob/living/carbon/human/dummy.dm:122  var/static/consistent_UI
    internal object GlobalProcs__create_consistent_human_dna__consistent_UI;
}

/// Provides a dummy that is consistently bald, white, naked, etc.
// tg: code/modules/mob/living/carbon/human/dummy.dm:130
[DmPath("/mob/living/carbon/human/dummy/consistent")]
public partial class mob_living_carbon_human_dummy_consistent : mob_living_carbon_human_dummy
{
    // tg: code/modules/mob/living/carbon/human/dummy.dm:132
    public override async DmTask setup_human_dna()
    {
        create_consistent_human_dna(this);
    }
}

/// Provides a dummy for unit_tests that functions like a normal human, but with a standardized appearance
/// Copies the stock dna setup from the dummy/consistent type
// tg: code/modules/mob/living/carbon/human/dummy.dm:137
[DmPath("/mob/living/carbon/human/consistent")]
public partial class mob_living_carbon_human_consistent : mob_living_carbon_human
{
    // tg: code/modules/mob/living/carbon/human/dummy.dm:139
    public override async DmTask setup_human_dna()
    {
        create_consistent_human_dna(this);
        // fully_replace_character_name(real_name, "John Doe"): names (M9). GAP(P1): /mob/proc/fully_replace_character_name
        // (mob.dm:1134) is Dm.NotTranslated (names, minds and records, M9); the consistent human keeps the type's name.
        Dm.Deferred("code/modules/mob/living/carbon/human/dummy.dm:141", "setup_human_dna: fully_replace_character_name(real_name, \"John Doe\"): the test human's name (names, M10+)");
    }

    // tg: code/modules/mob/living/carbon/human/dummy.dm:143
    public override async DmTask domutcheck()
    {
        return; // We skipped adding any mutations so this runtimes
    }
}

// tg: code/modules/mob/living/carbon/human/dummy.dm:146
// /mob/living/carbon/human/consistent/slow has no var or proc of its own outside #ifdef UNIT_TESTS (:148-160, not
// translated: inactive in production, rules 1.7).

//Inefficient pooling/caching way.
// tg: code/modules/mob/living/carbon/human/dummy.dm:162  GLOBAL_LIST_EMPTY(human_dummy_list) (gaps B1: the dummy pool, for
// get_flat_human_icon, the imaginary friend's picture)
public partial class datum_controller_global_vars
{
    // tg: code/modules/mob/living/carbon/human/dummy.dm:162  GLOBAL_LIST_EMPTY(human_dummy_list)
    public DmList human_dummy_list;

    // tg: code/modules/mob/living/carbon/human/dummy.dm:162  (GLOBAL_MANAGED(human_dummy_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalhuman_dummy_list()
    {
        human_dummy_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "human_dummy_list");
    }

    // tg: code/modules/mob/living/carbon/human/dummy.dm:163  GLOBAL_LIST_EMPTY(dummy_mob_list)
    public DmList dummy_mob_list;

    // tg: code/modules/mob/living/carbon/human/dummy.dm:163  (GLOBAL_MANAGED(dummy_mob_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldummy_mob_list()
    {
        dummy_mob_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "dummy_mob_list");
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/mob/living/carbon/human/dummy.dm:165
    public static async DmTask<object> generate_or_wait_for_human_dummy(object slotkey = null)
    {
        if (!Dm.truthy(slotkey))
            return Dm.@new<mob_living_carbon_human_dummy>();
        mob_living_carbon_human_dummy D = GLOB.human_dummy_list[slotkey] as mob_living_carbon_human_dummy;
        if (istype(D, typeof(mob_living_carbon_human_dummy)))
        {
            while (Dm.truthy(D.in_use)) // UNTIL(!D.in_use) (__DEFINES/_helpers.dm:31, expanded inline)
                await stoplag();
        }
        if (QDELETED(D))
        {
            D = Dm.@new<mob_living_carbon_human_dummy>();
            GLOB.human_dummy_list[slotkey] = D;
            Dm.append(GLOB.dummy_mob_list, D);
        }
        else
        {
            await D.regenerate_icons(); //they were cut in wipe_state()
            await D.update_body_parts(update_limb_data: TRUE);
        }
        D.in_use = TRUE;
        return D;
    }

    // tg: code/modules/mob/living/carbon/human/dummy.dm:181
    public static async DmTask<object> generate_dummy_lookalike(object slotkey = null, mob target = null)
    {
        if (!istype(target, typeof(mob)))
            return await generate_or_wait_for_human_dummy(slotkey);

        mob_living_carbon_human_dummy copycat = await generate_or_wait_for_human_dummy(slotkey) as mob_living_carbon_human_dummy;

        if (iscarbon(target))
        {
            mob_living_carbon carbon_target = (mob_living_carbon)target;
            await carbon_target.dna.copy_dna(copycat.dna, Dm.bitor(COPY_DNA_SE, COPY_DNA_SPECIES));

            if (ishuman(target))
            {
                mob_living_carbon_human human_target = (mob_living_carbon_human)target;
                human_target.copy_clothing_prefs(copycat);
            }

            await copycat.updateappearance(icon_update: TRUE, mutcolor_update: TRUE, mutations_overlay_update: TRUE);
        }
        else
        {
            //even if target isn't a carbon, if they have a client we can make the
            //dummy look like what their human would look like based on their prefs
            target?.client?.prefs?.apply_prefs_to(copycat, TRUE);
        }

        return copycat;
    }

    // tg: code/modules/mob/living/carbon/human/dummy.dm:202
    public static async DmTask unset_busy_human_dummy(object slotkey = null)
    {
        if (!Dm.truthy(slotkey))
            return;
        mob_living_carbon_human_dummy D = GLOB.human_dummy_list[slotkey] as mob_living_carbon_human_dummy;
        if (istype(D, typeof(mob_living_carbon_human_dummy)))
        {
            await D.wipe_state();
            D.in_use = FALSE;
        }
    }

    // tg: code/modules/mob/living/carbon/human/dummy.dm:210
    public static void clear_human_dummy(object slotkey = null)
    {
        if (!Dm.truthy(slotkey))
            return;

        mob_living_carbon_human_dummy dummy = GLOB.human_dummy_list[slotkey] as mob_living_carbon_human_dummy;

        Dm.remove(GLOB.human_dummy_list, slotkey);
        if (istype(dummy, typeof(mob_living_carbon_human_dummy)))
        {
            Dm.remove(GLOB.dummy_mob_list, dummy);
            qdel(dummy);
        }
    }
}
