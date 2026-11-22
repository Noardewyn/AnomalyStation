// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/chameleon/generic_chameleon_clothing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G1b (D316): the chameleon clothing (jumpsuit, suit, glasses, gloves, hat, mask with its voice changer,
// shoes and the no-slips, backpack, belt, headset, PDA, stamp, neck) and their broken variants (stuck randomising, EMP-proof).
// PARTIAL: the drones' camogear hat and mask (generic_chameleon_clothing.dm:159-170, :250-264) stay inert with the drones
// (not-translated.md section 134). The var blocks are generated (D111).

public static partial class GlobalProcs
{
    // tg: code/modules/clothing/chameleon/generic_chameleon_clothing.dm:1  #define BREAK_CHAMELEON_ACTION(item) (file-local)
    public static void BREAK_CHAMELEON_ACTION(obj_item item)
    {
        var _action = Dm.locate<datum_action_item_action_chameleon_change>(item.actions) as datum_action_item_action_chameleon_change;
        if (_action != null)
            Waiting.NoSleep(_action.emp_randomise(float.PositiveInfinity));
        Waiting.NoSleep(item.AddElement(typeof(datum_element_empprotection), Dm.bitor(EMP_PROTECT_SELF, EMP_NO_EXAMINE)));
    }
}

public partial class obj_item_clothing_under_chameleon_broken : obj_item_clothing_under_chameleon
{
    // tg: code/modules/clothing/chameleon/generic_chameleon_clothing.dm:41
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        BREAK_CHAMELEON_ACTION(this);
        return dot;
    }
}

public partial class obj_item_clothing_suit_chameleon : obj_item_clothing_suit
{
    // tg: code/modules/clothing/chameleon/generic_chameleon_clothing.dm:68
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        allowed = GLOB.security_vest_allowed; //should at least act like a vest
        return dot;
    }
}

public partial class obj_item_clothing_suit_chameleon_broken : obj_item_clothing_suit_chameleon
{
    // tg: code/modules/clothing/chameleon/generic_chameleon_clothing.dm:74
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        BREAK_CHAMELEON_ACTION(this);
        return dot;
    }
}

public partial class obj_item_clothing_glasses_chameleon_broken : obj_item_clothing_glasses_chameleon
{
    // tg: code/modules/clothing/chameleon/generic_chameleon_clothing.dm:100
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        BREAK_CHAMELEON_ACTION(this);
        return dot;
    }
}

public partial class obj_item_clothing_gloves_chameleon_broken : obj_item_clothing_gloves_chameleon
{
    // tg: code/modules/clothing/chameleon/generic_chameleon_clothing.dm:129
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        BREAK_CHAMELEON_ACTION(this);
        return dot;
    }
}

public partial class obj_item_clothing_head_chameleon_broken : obj_item_clothing_head_chameleon
{
    // tg: code/modules/clothing/chameleon/generic_chameleon_clothing.dm:155
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        BREAK_CHAMELEON_ACTION(this);
        return dot;
    }
}

// Cham mask, voice changer included
public partial class obj_item_clothing_mask_chameleon : obj_item_clothing_mask
{
    // tg: code/modules/clothing/chameleon/generic_chameleon_clothing.dm:196
    public virtual async DmTask<object> after_input_check(mob user = null)
    {
        if (QDELETED(user) || QDELETED(this) || user.client == null || !Dm.truthy(await user.can_perform_action(this, Dm.bitor(NEED_DEXTERITY, FORBID_TELEKINESIS_REACH))))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/clothing/chameleon/generic_chameleon_clothing.dm:201
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        bool on = Dm.@in(TRAIT_VOICE_MATCHES_ID, clothing_traits);
        if (on)
        {
            voice_override = null;
            detach_clothing_traits(TRAIT_VOICE_MATCHES_ID);
        }
        else
        {
            if (Dm.truthy(SStts.tts_enabled))
            {
                object popup_input = await tgui_input_list(user, "Choose Action", "Chameleon Mask", Dm.list("Spoof Crew Manifest Voice", "Spoof Any Voice", "Cancel"));
                if (!Dm.truthy(popup_input) || !Dm.truthy(await after_input_check(user)))
                    return null;
                if (Dm.eq(popup_input, "Spoof Crew Manifest Voice"))
                {
                    DmList possible_voices = Dm.list();
                    foreach (datum_record_crew target in Dm.each<datum_record_crew>(GLOB.manifest.general))
                    {
                        if (Dm.truthy(target.voice) && Dm.@in(target.voice, SStts.available_speakers))
                            Dm.append(possible_voices, target.name);
                        CHECK_TICK();
                    }
                    object voice_choice = await tgui_input_list(user, "Choose what voice to use as a disguise", "Voice Selection", possible_voices);
                    if (voice_choice == null || !Dm.truthy(await after_input_check(user)))
                    {
                        to_chat(user, span_warning(I18n.T("No choice selected, audible voice changing disabled.")));
                        voice_override = null;
                        return null;
                    }
                    datum_record_crew crew_record = find_record(voice_choice) as datum_record_crew;
                    if (Dm.truthy(crew_record.voice) && Dm.@in(crew_record.voice, SStts.available_speakers))
                    {
                        voice_override = voice_choice;
                        return null;
                    }
                    else
                    {
                        to_chat(user, span_warning(I18n.T("Crewmember's record's voice has been changed, please select another.")));
                        return null;
                    }
                }
                else if (Dm.eq(popup_input, "Spoof Any Voice"))
                {
                    object voice_choice = await tgui_input_list(user, "Choose what voice to use as a disguise", "Voice Selection", SStts.available_speakers);
                    if (voice_choice == null || !Dm.truthy(await after_input_check(user)))
                    {
                        to_chat(user, span_warning(I18n.T("No choice selected, audible voice changing disabled.")));
                        voice_override = null;
                        return null;
                    }
                    voice_override = voice_choice;
                }
            }
            else
                voice_override = null;
            attach_clothing_traits(TRAIT_VOICE_MATCHES_ID);
        }
        on = !on;
        to_chat(user, span_notice(I18n.T("The voice changer is now []!", on ? "on" : "off")));
        return null;
    }
}

public partial class obj_item_clothing_mask_chameleon_broken : obj_item_clothing_mask_chameleon
{
    // tg: code/modules/clothing/chameleon/generic_chameleon_clothing.dm:246
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        BREAK_CHAMELEON_ACTION(this);
        return dot;
    }
}

// Cham shoes, including chameleon noslips
public partial class obj_item_clothing_shoes_chameleon : obj_item_clothing_shoes
{
    // tg: code/modules/clothing/chameleon/generic_chameleon_clothing.dm:294
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_pockets_shoes)));
        return dot;
    }
}

public partial class obj_item_clothing_shoes_chameleon_broken : obj_item_clothing_shoes_chameleon
{
    // tg: code/modules/clothing/chameleon/generic_chameleon_clothing.dm:301
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        BREAK_CHAMELEON_ACTION(this);
        return dot;
    }
}

public partial class obj_item_clothing_shoes_chameleon_noslip_broken : obj_item_clothing_shoes_chameleon_noslip
{
    // tg: code/modules/clothing/chameleon/generic_chameleon_clothing.dm:312
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        BREAK_CHAMELEON_ACTION(this);
        return dot;
    }
}

// Cham backpack
public partial class obj_item_storage_backpack_chameleon : obj_item_storage_backpack
{
    // tg: code/modules/clothing/chameleon/generic_chameleon_clothing.dm:321
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_CONTRABAND_BLOCKER, INNATE_TRAIT);
        return dot;
    }
}

public partial class obj_item_storage_backpack_chameleon_broken : obj_item_storage_backpack_chameleon
{
    // tg: code/modules/clothing/chameleon/generic_chameleon_clothing.dm:328
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        BREAK_CHAMELEON_ACTION(this);
        return dot;
    }
}

// Cham belt (bonus: is silent)
public partial class obj_item_storage_belt_chameleon : obj_item_storage_belt
{
    // tg: code/modules/clothing/chameleon/generic_chameleon_clothing.dm:338
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        atom_storage.silent = TRUE;
        return dot;
    }
}

public partial class obj_item_storage_belt_chameleon_broken : obj_item_storage_belt_chameleon
{
    // tg: code/modules/clothing/chameleon/generic_chameleon_clothing.dm:344
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        BREAK_CHAMELEON_ACTION(this);
        return dot;
    }
}

public partial class obj_item_radio_headset_chameleon_broken : obj_item_radio_headset_chameleon
{
    // tg: code/modules/clothing/chameleon/generic_chameleon_clothing.dm:356
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        BREAK_CHAMELEON_ACTION(this);
        return dot;
    }
}

public partial class obj_item_modular_computer_pda_chameleon_broken : obj_item_modular_computer_pda_chameleon
{
    // tg: code/modules/clothing/chameleon/generic_chameleon_clothing.dm:369
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        BREAK_CHAMELEON_ACTION(this);
        return dot;
    }
}

public partial class obj_item_stamp_chameleon_broken : obj_item_stamp_chameleon
{
    // tg: code/modules/clothing/chameleon/generic_chameleon_clothing.dm:381
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        BREAK_CHAMELEON_ACTION(this);
        return dot;
    }
}

public partial class obj_item_clothing_neck_chameleon_broken : obj_item_clothing_neck_chameleon
{
    // tg: code/modules/clothing/chameleon/generic_chameleon_clothing.dm:402
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        BREAK_CHAMELEON_ACTION(this);
        return dot;
    }
}
