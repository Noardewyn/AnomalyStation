// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/sprite_accessories.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10x slice D1 (lobby.md 8): the whole file. The accessory datums are made one of each subtype, generated-only types included:
// tg's accessories are data with no New, so a generated-only one runs only the translated procs tg would run (not DV25's case).

/// subsystem that just holds lists of sprite accessories for accession in generating said sprites.
/// A sprite accessory is something that we add to a human sprite to make them look different. This is hair, facial hair, underwear, mutant bits, etc.
// tg: code/controllers/subsystem/sprite_accessories.dm:16 SUBSYSTEM_DEF(accessories)
[DmPath("/datum/controller/subsystem/accessories")]
public partial class datum_controller_subsystem_accessories : datum_controller_subsystem
{
    // tg: __DEFINES/MC.dm:29 NEW_SS_GLOBAL(SSaccessories), expanded inline (it reads src)
    // tg: code/controllers/subsystem/sprite_accessories.dm:16
    public override async DmTask New(params object[] __extra)
    {
        if (SSaccessories != this)
        {
            if (SSaccessories is datum_controller_subsystem_accessories)
            {
                Recover();
                qdel(SSaccessories);
            }
            SSaccessories = this;
        }
        PreInit();
    }

    // this stuff NEEDS to be set up before GLOB for preferences and stuff to work so this must go here. sorry
    // tg: code/controllers/subsystem/sprite_accessories.dm:47
    public override void PreInit()
    {
        setup_lists();
        init_hair_gradients();
        init_hair_masks();
    }

    /// Sets up all of the lists for later utilization in the round and building sprites.
    // tg: code/controllers/subsystem/sprite_accessories.dm:56
    public virtual void setup_lists()
    {
        var hair_lists = (DmList)init_sprite_accessory_subtypes(typeof(datum_sprite_accessory_hair));
        hairstyles_list = hair_lists[DEFAULT_SPRITE_LIST] as DmList;
        hairstyles_male_list = hair_lists[MALE_SPRITE_LIST] as DmList;
        hairstyles_female_list = hair_lists[FEMALE_SPRITE_LIST] as DmList;

        var facial_hair_lists = (DmList)init_sprite_accessory_subtypes(typeof(datum_sprite_accessory_facial_hair));
        facial_hairstyles_list = facial_hair_lists[DEFAULT_SPRITE_LIST] as DmList;
        facial_hairstyles_male_list = facial_hair_lists[MALE_SPRITE_LIST] as DmList;
        facial_hairstyles_female_list = facial_hair_lists[FEMALE_SPRITE_LIST] as DmList;

        var underwear_lists = (DmList)init_sprite_accessory_subtypes(typeof(datum_sprite_accessory_clothing_underwear));
        underwear_list = underwear_lists[DEFAULT_SPRITE_LIST] as DmList;
        underwear_m = underwear_lists[MALE_SPRITE_LIST] as DmList;
        underwear_f = underwear_lists[FEMALE_SPRITE_LIST] as DmList;

        var undershirt_lists = (DmList)init_sprite_accessory_subtypes(typeof(datum_sprite_accessory_clothing_undershirt));
        undershirt_list = undershirt_lists[DEFAULT_SPRITE_LIST] as DmList;
        undershirt_m = undershirt_lists[MALE_SPRITE_LIST] as DmList;
        undershirt_f = undershirt_lists[FEMALE_SPRITE_LIST] as DmList;

        socks_list = ((DmList)init_sprite_accessory_subtypes(typeof(datum_sprite_accessory_clothing_socks)))[DEFAULT_SPRITE_LIST] as DmList;

        feature_list = Dm.list();
        // felinids
        feature_list[FEATURE_TAIL_CAT] = INIT_OPTIONAL_ACCESSORY(typeof(datum_sprite_accessory_tails_felinid));
        feature_list[FEATURE_EARS] = INIT_OPTIONAL_ACCESSORY(typeof(datum_sprite_accessory_ears));
        // lizards
        feature_list[FEATURE_FRILLS] = INIT_OPTIONAL_ACCESSORY(typeof(datum_sprite_accessory_frills));
        feature_list[FEATURE_HORNS] = INIT_OPTIONAL_ACCESSORY(typeof(datum_sprite_accessory_horns));
        feature_list[FEATURE_LIZARD_MARKINGS] = INIT_OPTIONAL_ACCESSORY(typeof(datum_sprite_accessory_lizard_markings));
        feature_list[FEATURE_SNOUT] = INIT_ACCESSORY(typeof(datum_sprite_accessory_snouts));
        feature_list[FEATURE_SPINES] = INIT_OPTIONAL_ACCESSORY(typeof(datum_sprite_accessory_spines));
        feature_list[FEATURE_TAILSPINES] = INIT_OPTIONAL_ACCESSORY(typeof(datum_sprite_accessory_tail_spines));
        feature_list[FEATURE_TAIL_LIZARD] = INIT_ACCESSORY(typeof(datum_sprite_accessory_tails_lizard));
        // moths
        feature_list[FEATURE_MOTH_WINGS] = INIT_ACCESSORY(typeof(datum_sprite_accessory_moth_wings));
        feature_list[FEATURE_MOTH_ANTENNAE] = INIT_ACCESSORY(typeof(datum_sprite_accessory_moth_antennae));
        feature_list[FEATURE_MOTH_MARKINGS] = INIT_OPTIONAL_ACCESSORY(typeof(datum_sprite_accessory_moth_markings));
        // generic wings
        feature_list[FEATURE_WINGS] = INIT_OPTIONAL_ACCESSORY(typeof(datum_sprite_accessory_wings));
        feature_list[FEATURE_WINGS_OPEN] = INIT_OPTIONAL_ACCESSORY(typeof(datum_sprite_accessory_wings_open));
        // generic features
        feature_list[FEATURE_MUSH_CAP] = INIT_ACCESSORY(typeof(datum_sprite_accessory_caps));
        feature_list[FEATURE_POD_HAIR] = INIT_ACCESSORY(typeof(datum_sprite_accessory_pod_hair));
        feature_list[FEATURE_TAIL_FISH] = INIT_ACCESSORY(typeof(datum_sprite_accessory_tails_fish));
        feature_list[FEATURE_TAIL_MONKEY] = INIT_ACCESSORY(typeof(datum_sprite_accessory_tails_monkey));
        feature_list[FEATURE_TAIL_XENO] = INIT_ACCESSORY(typeof(datum_sprite_accessory_tails_xeno));
    }

    /// This proc just initializes all /datum/sprite_accessory/hair_gradient into an list indexed by gradient-style name
    // tg: code/controllers/subsystem/sprite_accessories.dm:106
    public virtual void init_hair_gradients()
    {
        hair_gradients_list = Dm.list();
        facial_hair_gradients_list = Dm.list();
        foreach (object path in Dm.each(subtypesof(typeof(datum_sprite_accessory_gradient))))
        {
            var gradient = (datum_sprite_accessory_gradient)Dm.@new((Type)path);
            if (Dm.truthy(Dm.bitand(gradient.gradient_category, GRADIENT_APPLIES_TO_HAIR)))
                hair_gradients_list[gradient.name] = gradient;
            if (Dm.truthy(Dm.bitand(gradient.gradient_category, GRADIENT_APPLIES_TO_FACIAL_HAIR)))
                facial_hair_gradients_list[gradient.name] = gradient;
        }
    }

    // tg: code/controllers/subsystem/sprite_accessories.dm:116
    public virtual void init_hair_masks()
    {
        hair_masks_list = Dm.list();
        foreach (object path in Dm.each(subtypesof(typeof(datum_hair_mask))))
            hair_masks_list[path] = Dm.@new((Type)path);
    }

    /// This reads the applicable sprite accessory datum's subtypes and adds it to the subsystem's list of sprite accessories.
    /// The boolean `add_blank` argument just adds a "None" option to the list of sprite accessories, like if a felinid doesn't want a tail or something, typically good for gated-off things.
    // tg: code/controllers/subsystem/sprite_accessories.dm:124
    public virtual object init_sprite_accessory_subtypes(object prototype = null, float? add_blank = null)
    {
        add_blank ??= FALSE;
        DmList default_list = Dm.list(), male_list = Dm.list(), female_list = Dm.list();
        DmList returnable_list = Dm.list(
            Dm.kv(DEFAULT_SPRITE_LIST, default_list),
            Dm.kv(MALE_SPRITE_LIST, male_list),
            Dm.kv(FEMALE_SPRITE_LIST, female_list));

        foreach (object path in Dm.each(subtypesof((Type)prototype)))
        {
            var accessory = (datum_sprite_accessory)Dm.@new((Type)path);

            if (Dm.truthy(accessory.icon_state))
                default_list[accessory.name] = accessory;
            else
                Dm.append(default_list, accessory.name);

            switch (accessory.gender)
            {
                case MALE:
                    Dm.append(male_list, accessory.name);
                    break;
                case FEMALE:
                    Dm.append(female_list, accessory.name);
                    break;
                default:
                    Dm.append(male_list, accessory.name);
                    Dm.append(female_list, accessory.name);
                    break;
            }
        }

        if (Dm.truthy(add_blank))
            default_list[SPRITE_ACCESSORY_NONE] = Dm.@new<datum_sprite_accessory_blank>();

        return returnable_list;
    }

    /// The non gender specific list that we get from init_sprite_accessory_subtypes()
    // tg: code/controllers/subsystem/sprite_accessories.dm:2
    private const string DEFAULT_SPRITE_LIST = "default_sprites";
    /// The male specific list that we get from init_sprite_accessory_subtypes()
    // tg: code/controllers/subsystem/sprite_accessories.dm:4
    private const string MALE_SPRITE_LIST = "male_sprites";
    /// The female specific list that we get from init_sprite_accessory_subtypes()
    // tg: code/controllers/subsystem/sprite_accessories.dm:6
    private const string FEMALE_SPRITE_LIST = "female_sprites";

    /// Use this to init a sprite accessory list for a feature where mobs are required to have one selected
    // tg: code/controllers/subsystem/sprite_accessories.dm:9
    private DmList INIT_ACCESSORY(Type sprite_accessory) => ((DmList)init_sprite_accessory_subtypes(sprite_accessory, add_blank: FALSE))[DEFAULT_SPRITE_LIST] as DmList;
    /// Use this to init a sprite accessory list for a feature where mobs can opt to not have one selected
    // tg: code/controllers/subsystem/sprite_accessories.dm:11
    private DmList INIT_OPTIONAL_ACCESSORY(Type sprite_accessory) => ((DmList)init_sprite_accessory_subtypes(sprite_accessory, add_blank: TRUE))[DEFAULT_SPRITE_LIST] as DmList;
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/sprite_accessories.dm:16  SUBSYSTEM_DEF(accessories): GLOBAL_REAL(SSaccessories, /datum/controller/subsystem/accessories)
    internal datum_controller_subsystem_accessories SSaccessories;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_accessories SSaccessories => ref TgState.Current.SSaccessories;
}
