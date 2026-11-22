// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/init_signals.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Translated in M2 by P2 (scope.md 2e "human": init_signals.dm): the human's trait signals and their handlers; the ID
// card HUD and the appearance updates are the lead's and P3's (GAPs below).

// tg: code/modules/mob/living/carbon/human/init_signals.dm:20
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/init_signals.dm:1
    public override async DmTask register_init_signals()
    {
        await base.register_init_signals();

        RegisterSignals(this, Dm.list(SIGNAL_ADDTRAIT(TRAIT_UNKNOWN_APPEARANCE), SIGNAL_REMOVETRAIT(TRAIT_UNKNOWN_APPEARANCE)), nameof(update_ID_card));
        RegisterSignals(this, Dm.list(SIGNAL_ADDTRAIT(TRAIT_DWARF), SIGNAL_REMOVETRAIT(TRAIT_DWARF)), nameof(on_dwarf_trait));
        RegisterSignals(this, Dm.list(SIGNAL_ADDTRAIT(TRAIT_TOO_TALL), SIGNAL_REMOVETRAIT(TRAIT_TOO_TALL)), nameof(on_tootall_trait));

        RegisterSignals(this, Dm.list(SIGNAL_ADDTRAIT(TRAIT_FAT), SIGNAL_REMOVETRAIT(TRAIT_FAT), SIGNAL_ADDTRAIT(TRAIT_FAT_IGNORE_SLOWDOWN), SIGNAL_REMOVETRAIT(TRAIT_FAT_IGNORE_SLOWDOWN)), nameof(on_fat));
        RegisterSignals(this, Dm.list(SIGNAL_ADDTRAIT(TRAIT_NOHUNGER), SIGNAL_REMOVETRAIT(TRAIT_NOHUNGER)), nameof(on_nohunger));

        RegisterSignal(this, COMSIG_ATOM_CONTENTS_WEIGHT_CLASS_CHANGED, nameof(check_pocket_weght));

        RegisterSignal(this, COMSIG_COMPONENT_CLEAN_FACE_ACT, nameof(clean_face));

        RegisterSignals(this, Dm.list(SIGNAL_ADDTRAIT(TRAIT_HUSK), SIGNAL_REMOVETRAIT(TRAIT_HUSK)), nameof(husk_trait_toggle));
        RegisterSignals(this, Dm.list(SIGNAL_ADDTRAIT(TRAIT_INVISIBLE_MAN), SIGNAL_REMOVETRAIT(TRAIT_INVISIBLE_MAN)), nameof(invisible_man_toggle));
        RegisterSignals(this, Dm.list(SIGNAL_ADDTRAIT(TRAIT_NO_UNDERWEAR), SIGNAL_REMOVETRAIT(TRAIT_NO_UNDERWEAR)), nameof(no_underwear_toggle));
    }

    // tg: code/modules/mob/living/carbon/human/init_signals.dm:20
    [SignalHandler]
    public virtual void on_dwarf_trait(datum source = null)
    {
        Waiting.NoSleep(update_mob_height());
        // Toggle passtable
        if (HAS_TRAIT(this, TRAIT_DWARF))
            ADD_TRAIT(this, TRAIT_PASSTABLE, TRAIT_DWARF);
        else
            REMOVE_TRAIT(this, TRAIT_PASSTABLE, TRAIT_DWARF);
    }

    // tg: code/modules/mob/living/carbon/human/init_signals.dm:31
    [SignalHandler]
    public virtual void on_tootall_trait(datum source = null)
    {
        Waiting.NoSleep(update_mob_height());
    }

    // tg: code/modules/mob/living/carbon/human/init_signals.dm:35
    [SignalHandler]
    public virtual void on_fat(datum source = null)
    {
        Waiting.NoSleep(update_nutrition());

        if (HAS_TRAIT(this, TRAIT_FAT) && !HAS_TRAIT(this, TRAIT_FAT_IGNORE_SLOWDOWN))
            Waiting.NoSleep(add_movespeed_modifier(typeof(datum_movespeed_modifier_obesity)));
        else
            remove_movespeed_modifier(typeof(datum_movespeed_modifier_obesity));
    }

    // tg: code/modules/mob/living/carbon/human/init_signals.dm:44
    [SignalHandler]
    public virtual void on_nohunger(datum source = null)
    {
        // When gaining NOHUNGER, we restore nutrition to normal levels, since we no longer interact with the hunger system
        if (HAS_TRAIT(this, TRAIT_NOHUNGER))
        {
            Waiting.NoSleep(set_nutrition(NUTRITION_LEVEL_FED, forced: TRUE));
            satiety = 0;
            overeatduration = 0;
            remove_traits(Dm.list(TRAIT_FAT, TRAIT_OFF_BALANCE_TACKLER), OBESITY);
        }
        else
            Waiting.NoSleep(update_nutrition());
    }

    // tg: code/modules/mob/living/carbon/human/init_signals.dm:56
    [SignalHandler]
    public virtual void check_pocket_weght(datum source = null, obj_item changed = null, object old_w_class = null, object new_w_class = null)
    {
        if (changed != r_store && changed != l_store)
            return;
        if (Dm.le(new_w_class, POCKET_WEIGHT_CLASS))
            return;
        if (!Dm.truthy(Waiting.NoSleep(dropItemToGround(changed, force: TRUE))))
            return;
        visible_message(
            span_warning(I18n.T("[] falls out of []'s pockets!", changed, this)),
            span_warning(I18n.T("[] falls out of your pockets!", changed)),
            vision_distance: COMBAT_MESSAGE_RANGE
        );
        playsound(this, SFX_RUSTLE, 50, TRUE, -5, frequency: 0.8f); // tg: code/modules/mob/living/carbon/human/init_signals.dm:69
    }

    // tg: code/modules/mob/living/carbon/human/init_signals.dm:71
    [SignalHandler]
    public virtual void husk_trait_toggle(datum source = null)
    {
        refresh_obscured();
        Waiting.NoSleep(update_body());
    }

    // tg: code/modules/mob/living/carbon/human/init_signals.dm:77
    [SignalHandler]
    public virtual void invisible_man_toggle(datum source = null)
    {
        refresh_obscured();
        update_visible_name();
        Waiting.NoSleep(update_body());
    }

    // tg: code/modules/mob/living/carbon/human/init_signals.dm:84
    [SignalHandler]
    public virtual void no_underwear_toggle(datum source = null)
    {
        Waiting.NoSleep(update_body());
    }
}
