// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/species_types/shadowpeople.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327, with Mafia: the nightmare is revealed as a shadowperson): the shadowpeople, burnt and confused by a
// flash. Their organs are generated (D111); the darkness healing of their brain is the nightmare's (inert with the nightmare).

// tg: code/modules/mob/living/carbon/human/species_types/shadowpeople.dm:1
public partial class datum_species_shadow : datum_species
{
    // tg: code/modules/mob/living/carbon/human/species_types/shadowpeople.dm:36
    public override async DmTask on_species_gain(mob_living_carbon_human human_who_gained_species = null, datum_species old_species = null, object pref_load = null, float? regenerate_icons = null, float? replace_missing = null)
    {
        await base.on_species_gain(human_who_gained_species, old_species, pref_load, regenerate_icons, replace_missing);
        RegisterSignal(human_who_gained_species, COMSIG_MOB_FLASH_OVERRIDE_CHECK, nameof(on_flashed));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/shadowpeople.dm:40
    public override async DmTask on_species_loss(mob_living_carbon_human human = null, datum_species new_species = null, object pref_load = null, params object[] __extra)
    {
        await base.on_species_loss(human, new_species, pref_load, __extra);
        UnregisterSignal(human, COMSIG_MOB_FLASH_OVERRIDE_CHECK);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/shadowpeople.dm:44
    public override object check_roundstart_eligible()
    {
        if (Dm.truthy(check_holidays(HALLOWEEN)))
            return TRUE;
        return base.check_roundstart_eligible();
    }

    // tg: code/modules/mob/living/carbon/human/species_types/shadowpeople.dm:49
    public override object get_physical_attributes()
        => "These cursed creatures heal in the dark, but suffer in the light much more heavily. Their eyes let them see in the dark as though it were day.";

    // tg: code/modules/mob/living/carbon/human/species_types/shadowpeople.dm:52
    public override object get_species_description()
        => "Victims of a long extinct space alien. Their flesh is a sickly seethrough filament, their tangled insides in clear view. Their form is a mockery of life, leaving them mostly unable to work with others under normal circumstances.";

    // tg: code/modules/mob/living/carbon/human/species_types/shadowpeople.dm:58
    public override object get_species_lore() => Dm.list(
        "Long ago, the Spinward Sector used to be inhabited by terrifying aliens aptly named \"Shadowlings\" after their control over darkness, and tendancy to kidnap victims into the dark maintenance shafts. Around 2558, the long campaign Nanotrasen waged against the space terrors ended with the full extinction of the Shadowlings.",
        "Victims of their kidnappings would become brainless thralls, and via surgery they could be freed from the Shadowling's control. Those more unlucky would have their entire body transformed by the Shadowlings to better serve in kidnappings. Unlike the brain tumors of lesser control, these greater thralls could not be reverted.",
        "With Shadowlings long gone, their will is their own again. But their bodies have not reverted, burning in exposure to light. Nanotrasen has assured the victims that they are searching for a cure. No further information has been given, even years later. Most shadowpeople now assume Nanotrasen has long since shelfed the project.");

    // tg: code/modules/mob/living/carbon/human/species_types/shadowpeople.dm:73
    public override object create_pref_unique_perks()
    {
        DmList to_add = Dm.list();

        Dm.append(ref to_add, Dm.list(
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "moon"),
                Dm.kv(SPECIES_PERK_NAME, "Shadowborn"),
                Dm.kv(SPECIES_PERK_DESC, "Their skin blooms in the darkness. All kinds of damage, no matter how extreme, will heal over time as long as there is no light.")),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "eye"),
                Dm.kv(SPECIES_PERK_NAME, "Nightvision"),
                Dm.kv(SPECIES_PERK_DESC, "Their eyes are adapted to the night, and can see in the dark with no problems.")),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "sun"),
                Dm.kv(SPECIES_PERK_NAME, "Lightburn"),
                Dm.kv(SPECIES_PERK_DESC, "Their flesh withers in the light. Any exposure to light is incredibly painful for the shadowperson, charring their skin."))));

        return to_add;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/shadowpeople.dm:117
    [SignalHandler]
    public virtual object on_flashed(object source = null, mob_living_carbon flashed = null, object flash = null, object deviation = null)
    {
        if (Dm.eq(deviation, DEVIATION_FULL)) //If no deviation, we can assume it's a non-assembly flash and should do max flash damage.
        {
            Waiting.NoSleep(flashed.apply_damage(16, BURN, attacking_item: flash));
            Waiting.NoSleep(flashed.adjust_confusion_up_to(3 * SECONDS, 6 * SECONDS));
        }
        else //If it's anything less than a full hit, it does less than stellar damage. Bear in mind that this damage is dished out much faster since flashes have a quicker cooldown on clicks.
        {
            Waiting.NoSleep(flashed.apply_damage(8, BURN, attacking_item: flash));
            Waiting.NoSleep(flashed.adjust_confusion_up_to(1 * SECONDS, 3 * SECONDS));
        }

        Waiting.Detach(flashed.emote("scream"));
        flashed.visible_message(span_danger(I18n.T("[] wails in pain as a burst of light singes their flesh!", flashed)),
            span_danger(I18n.T("You wail in pain as the sudden burst of light singes your flesh!")),
            span_danger(I18n.T("Something wails in pain! It sounds like a terrifying monster! Good thing you can't see it, or you'd probably be freaking out right now.")));

        return FLASH_OVERRIDDEN;
    }
}
