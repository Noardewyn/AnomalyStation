// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/masks/animal_masks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// /obj/item/clothing/mask/animal's equipped (both definitions: equipped__proc__d0 and equipped__proc), dropped and handle_speech
// (M4a clothing: animal masks are put on and taken off; worn, they turn the wearer's speech into animal sounds). Gaps CL1 (D333):
// the rest of the file: GLOB.cursed_animal_masks, the curse (make_cursed, clear_curse: the mask melds onto the face with tg's curse
// sound), the voice box toggle (alt-click and its examine line), vv_edit_var and the frog, small, bear and tribal masks (with their fishing
// difficulty elements).

public partial class datum_controller_global_vars
{
    // tg: code/modules/clothing/masks/animal_masks.dm:2  GLOBAL_LIST_INIT(cursed_animal_masks, list(...))
    public DmList cursed_animal_masks;

    // tg: code/modules/clothing/masks/animal_masks.dm:2
    public virtual void InitGlobalcursed_animal_masks()
    {
        cursed_animal_masks = Dm.list(
            typeof(obj_item_clothing_mask_animal_pig_cursed),
            typeof(obj_item_clothing_mask_animal_frog_cursed),
            typeof(obj_item_clothing_mask_animal_cowmask_cursed),
            typeof(obj_item_clothing_mask_animal_horsehead_cursed),
            typeof(obj_item_clothing_mask_animal_small_rat_cursed),
            typeof(obj_item_clothing_mask_animal_small_fox_cursed),
            typeof(obj_item_clothing_mask_animal_small_bee_cursed),
            typeof(obj_item_clothing_mask_animal_small_bear_cursed),
            typeof(obj_item_clothing_mask_animal_small_bat_cursed),
            typeof(obj_item_clothing_mask_animal_small_raven_cursed),
            typeof(obj_item_clothing_mask_animal_small_jackal_cursed)
        );
        Dm.append(ref gvars_datum_init_order, "cursed_animal_masks");
    }
}

// tg: code/modules/clothing/masks/animal_masks.dm:16
public partial class obj_item_clothing_mask_animal : obj_item_clothing_mask
{
    // tg: code/modules/clothing/masks/animal_masks.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(cursed))
            Waiting.NoSleep(make_cursed());
        return dot;
    }

    // The earlier of the type's two equipped definitions (rules 7.5: the later one's ..() calls it).
    // tg: code/modules/clothing/masks/animal_masks.dm:36
    public async DmTask equipped__proc__d0(mob M = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(M, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_MASK)) && Dm.truthy(modifies_speech))
            RegisterSignal(M, COMSIG_MOB_SAY, nameof(handle_speech));
        else
            UnregisterSignal(M, COMSIG_MOB_SAY);
    }

    // tg: code/modules/clothing/masks/animal_masks.dm:43
    [DmParams("M")]
    public override async DmTask<object> dropped(mob M = null, float? silent = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.dropped(M, silent, __extra);
        UnregisterSignal(M, COMSIG_MOB_SAY);
        return dot;
    }

    // tg: code/modules/clothing/masks/animal_masks.dm:47
    [DmParams("vname", "vval")]
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        object vname = var_name;
        object vval = var_value;
        if (Dm.eq(vname, nameof(cursed)))
        {
            if (Dm.truthy(vval))
            {
                if (!Dm.truthy(cursed))
                    await make_cursed();
            }
            else if (Dm.truthy(cursed))
                await clear_curse();
        }
        if (Dm.eq(vname, nameof(modifies_speech)) && ismob(loc))
        {
            mob M = (mob)loc;
            if (M.get_item_by_slot(ITEM_SLOT_MASK) == this)
            {
                if (Dm.truthy(vval))
                {
                    if (!Dm.truthy(modifies_speech))
                        RegisterSignal(M, COMSIG_MOB_SAY, nameof(handle_speech));
                }
                else if (Dm.truthy(modifies_speech))
                    UnregisterSignal(M, COMSIG_MOB_SAY);
            }
        }
        return await base.vv_edit_var(vname, vval);
    }

    // tg: code/modules/clothing/masks/animal_masks.dm:64
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(Dm.bitand(clothing_flags, VOICEBOX_TOGGLABLE)))
            Dm.append(ref dot, span_notice(I18n.T("Its voicebox is currently []. <b>Alt-click</b> to toggle it.", Dm.truthy(Dm.bitand(clothing_flags, VOICEBOX_DISABLED)) ? I18n.T("disabled") : I18n.T("enabled"))));
        return dot;
    }

    // tg: code/modules/clothing/masks/animal_masks.dm:69
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(Dm.bitand(clothing_flags, VOICEBOX_TOGGLABLE)))
            return NONE;
        clothing_flags = Dm.bitxor(clothing_flags, VOICEBOX_DISABLED);
        to_chat(user, span_notice(I18n.T("You [] []'s voicebox.", Dm.truthy(Dm.bitand(clothing_flags, VOICEBOX_DISABLED)) ? I18n.T("disabled") : I18n.T("enabled"), this)));
        await DmTask.CompletedTask;
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/clothing/masks/animal_masks.dm:76
    public virtual async DmTask make_cursed() //apply cursed effects.
    {
        ADD_TRAIT(this, TRAIT_NODROP, CURSED_MASK_TRAIT);
        clothing_flags = NONE; //force animal sounds to always on.
        if (Dm.eq(flags_inv, Dm.initial(this, (obj_item_clothing_mask_animal p) => p.flags_inv)))
            flags_inv = HIDEFACIALHAIR;
        name = I18n.T("[] face", animal_type);
        desc = I18n.T("It looks like a [] mask, but closer inspection reveals it's melded onto this person's face!", animal_type);
        if (Dm.truthy(curse_spawn_sound))
            playsound(this, curse_spawn_sound, 50, TRUE);
        bool update_speech_mod = !Dm.truthy(modifies_speech) && Dm.truthy(LAZYLEN(animal_sounds));
        if (update_speech_mod)
            modifies_speech = TRUE;
        if (ismob(loc))
        {
            mob M = (mob)loc;
            if (M.get_item_by_slot(ITEM_SLOT_MASK) == this)
            {
                if (update_speech_mod)
                    RegisterSignal(M, COMSIG_MOB_SAY, nameof(handle_speech));
                to_chat(M, span_userdanger(I18n.T("[] was cursed!", this)));
                await M.update_worn_mask();
                M.refresh_obscured();
            }
        }
    }

    // tg: code/modules/clothing/masks/animal_masks.dm:97
    public virtual async DmTask clear_curse()
    {
        REMOVE_TRAIT(this, TRAIT_NODROP, CURSED_MASK_TRAIT);
        clothing_flags = Dm.num(Dm.initial(this, (obj_item_clothing_mask_animal p) => p.clothing_flags));
        flags_inv = Dm.num(Dm.initial(this, (obj_item_clothing_mask_animal p) => p.flags_inv));
        name = Dm.initial(this, (obj_item_clothing_mask_animal p) => p.name);
        desc = Dm.initial(this, (obj_item_clothing_mask_animal p) => p.desc);
        bool update_speech_mod = Dm.truthy(modifies_speech) && !Dm.truthy(Dm.initial(this, (obj_item_clothing_mask_animal p) => p.modifies_speech));
        if (update_speech_mod)
            modifies_speech = FALSE;
        if (ismob(loc))
        {
            mob M = (mob)loc;
            if (M.get_item_by_slot(ITEM_SLOT_MASK) == this)
            {
                to_chat(M, span_notice(I18n.T("[]'s curse has been lifted!", this)));
                if (update_speech_mod)
                    UnregisterSignal(M, COMSIG_MOB_SAY);
                await M.update_worn_mask();
            }
        }
    }

    // tg: code/modules/clothing/masks/animal_masks.dm:114
    [SignalHandler]
    public virtual void handle_speech(datum source = null, DmList speech_args = null)
    {
        if (Dm.truthy(Dm.bitand(clothing_flags, VOICEBOX_DISABLED)))
            return;
        if (!Dm.truthy(modifies_speech) || !Dm.truthy(LAZYLEN(animal_sounds)))
            return;
        speech_args[SPEECH_MESSAGE] = pick((prob(Dm.num(animal_sounds_alt_probability)) && Dm.truthy(LAZYLEN(animal_sounds_alt))) ? animal_sounds_alt : animal_sounds);
    }

    // tg: code/modules/clothing/masks/animal_masks.dm:123
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        if (!iscarbon(user))
        {
            await equipped__proc__d0(user, slot, initial);
            return;
        }
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_MASK)) && HAS_TRAIT_FROM(this, TRAIT_NODROP, CURSED_MASK_TRAIT))
            to_chat(user, span_userdanger(I18n.T("[] was cursed!", this)));
        await equipped__proc__d0(user, slot, initial);
    }
}

// tg: code/modules/clothing/masks/animal_masks.dm:145
public partial class obj_item_clothing_mask_animal_frog : obj_item_clothing_mask_animal
{
    // tg: code/modules/clothing/masks/animal_masks.dm:155
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), Dm.truthy(cursed) ? 4 : -4));
        return dot;
    }

    // tg: code/modules/clothing/masks/animal_masks.dm:159
    public override async DmTask make_cursed()
    {
        await base.make_cursed();
        RemoveElement(typeof(datum_element_adjust_fishing_difficulty), -4);
        await AddElement(typeof(datum_element_adjust_fishing_difficulty), 4);
    }

    // tg: code/modules/clothing/masks/animal_masks.dm:164
    public override async DmTask clear_curse()
    {
        await base.clear_curse();
        RemoveElement(typeof(datum_element_adjust_fishing_difficulty), 4);
        await AddElement(typeof(datum_element_adjust_fishing_difficulty), -4);
    }
}

// tg: code/modules/clothing/masks/animal_masks.dm:196
public partial class obj_item_clothing_mask_animal_small : obj_item_clothing_mask_animal
{
    // tg: code/modules/clothing/masks/animal_masks.dm:202
    public override async DmTask make_cursed()
    {
        flags_inv = NONE;
        await base.make_cursed();
    }
}

// tg: code/modules/clothing/masks/animal_masks.dm:239
public partial class obj_item_clothing_mask_animal_small_bear : obj_item_clothing_mask_animal_small
{
    // tg: code/modules/clothing/masks/animal_masks.dm:247
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), Dm.truthy(cursed) ? 4 : -4));
        return dot;
    }

    // tg: code/modules/clothing/masks/animal_masks.dm:251
    public override async DmTask make_cursed()
    {
        await base.make_cursed();
        RemoveElement(typeof(datum_element_adjust_fishing_difficulty), -4);
        await AddElement(typeof(datum_element_adjust_fishing_difficulty), 4);
    }

    // tg: code/modules/clothing/masks/animal_masks.dm:256
    public override async DmTask clear_curse()
    {
        await base.clear_curse();
        RemoveElement(typeof(datum_element_adjust_fishing_difficulty), 4);
        await AddElement(typeof(datum_element_adjust_fishing_difficulty), -4);
    }
}

// tg: code/modules/clothing/masks/animal_masks.dm:299
public partial class obj_item_clothing_mask_animal_small_tribal : obj_item_clothing_mask_animal_small
{
    // tg: code/modules/clothing/masks/animal_masks.dm:309
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), Dm.truthy(cursed) ? 5 : -5));
        return dot;
    }

    // tg: code/modules/clothing/masks/animal_masks.dm:313
    public override async DmTask make_cursed()
    {
        await base.make_cursed();
        RemoveElement(typeof(datum_element_adjust_fishing_difficulty), -5);
        await AddElement(typeof(datum_element_adjust_fishing_difficulty), 5);
    }

    // tg: code/modules/clothing/masks/animal_masks.dm:318
    public override async DmTask clear_curse()
    {
        await base.clear_curse();
        RemoveElement(typeof(datum_element_adjust_fishing_difficulty), 5);
        await AddElement(typeof(datum_element_adjust_fishing_difficulty), -5);
    }
}
