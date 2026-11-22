// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/dna_probe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R1 (D327): the DNA sampler (/obj/item/dna_probe): linked to a DNA vault, it samples harvestable plants in hydroponics trays,
// the crew and the animals, and uploads the new samples to the vault. Gaps OD2 (D333): the file whole, with the carp DNA sampler
// (:155-195), which samples a space carp's DNA; its self-mutation into a space dragon (:189-193) is a not-translated leaf, as the
// space dragon (an antagonist, D333) is not ported (not-translated.md 218); no tg code or map makes the sampler. The var blocks are
// generated (D111).

/**
 * DNA Probe
 *
 * Used for scanning DNA, and can be uploaded to a DNA vault.
 */
// tg: code/game/objects/items/dna_probe.dm:11
public partial class obj_item_dna_probe : obj_item
{
    // tg: code/game/objects/items/dna_probe.dm:1-3  (file-local, #undef at :197-199)
    private const int DNA_PROBE_SCAN_PLANTS = 1 << 0;
    private const int DNA_PROBE_SCAN_ANIMALS = 1 << 1;
    private const int DNA_PROBE_SCAN_HUMANS = 1 << 2;

    /// Things we consider to be animals
    // tg: code/game/objects/items/dna_probe.dm:31  var/static/list/animal_typecache (a type static, rules 10.6)
    private static DmList animal_typecache => TgState.Current.obj_item_dna_probe__animal_typecache ??= typecacheof(Dm.list(
        typeof(mob_living_basic),
        typeof(mob_living_carbon_alien),
        typeof(mob_living_simple_animal),
        typeof(obj_item_fish)
    )) as DmList;

    // tg: code/game/objects/items/dna_probe.dm:38
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (interacting_with is obj_machinery_dna_vault)
        {
            if (Dm.truthy(dna_vault_ref?.resolve()))
                // Weirdly we can upload to any existing DNA vault so long as we're linked to any other existing DNA vault.
                return Dm.truthy(try_upload_dna(interacting_with as obj_machinery_dna_vault, user)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
            return Dm.truthy(try_linking_vault(interacting_with as obj_machinery_dna_vault, user)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(valid_scan_target(interacting_with)))
            return NONE;

        if (Dm.truthy(scan_dna(interacting_with, user)))
            return ITEM_INTERACT_SUCCESS;

        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/game/objects/items/dna_probe.dm:53
    public virtual object try_linking_vault(obj_machinery_dna_vault target = null, mob user = null)
    {
        var our_vault = dna_vault_ref?.resolve() as obj_machinery_dna_vault;
        if (our_vault == null)
        {
            dna_vault_ref = WEAKREF(target) as datum_weakref; //linking the dna vault with the probe
            balloon_alert(user, I18n.T("vault linked"));
            playsound(this, Dm.resource("sound/machines/terminal/terminal_success.ogg"), 50);
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/objects/items/dna_probe.dm:62
    public virtual object try_upload_dna(obj_machinery_dna_vault target = null, mob user = null)
    {
        float uploaded = 0;
        Dm.remove(ref stored_dna_plants, target.plant_dna);
        Dm.remove(ref stored_dna_human, target.human_dna);
        Dm.remove(ref stored_dna_animal, target.animal_dna);
        float plant_dna_length = Dm.length(stored_dna_plants);
        float human_dna_length = Dm.length(stored_dna_human);
        float animal_dna_length = Dm.length(stored_dna_animal);
        if (Dm.truthy(plant_dna_length))
        {
            uploaded += plant_dna_length;
            Dm.append(ref target.plant_dna, stored_dna_plants);
            stored_dna_plants.Cut();
        }
        if (Dm.truthy(human_dna_length))
        {
            uploaded += human_dna_length;
            Dm.append(ref target.human_dna, stored_dna_human);
            stored_dna_human.Cut();
        }
        if (Dm.truthy(animal_dna_length))
        {
            uploaded += animal_dna_length;
            Dm.append(ref target.animal_dna, stored_dna_animal);
            stored_dna_animal.Cut();
        }
        target.check_goal();
        playsound(target, Dm.resource("sound/machines/compiler/compiler-stage1.ogg"), 50);
        to_chat(user, span_notice(I18n.T("[] new datapoints uploaded.", uploaded)));
        return uploaded;
    }

    // tg: code/game/objects/items/dna_probe.dm:87
    public virtual object scan_dna(atom target = null, mob user = null)
    {
        var our_vault = dna_vault_ref?.resolve() as obj_machinery_dna_vault;
        if (our_vault == null)
        {
            playsound(user, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50);
            balloon_alert(user, I18n.T("need database!"));
            return FALSE;
        }
        if (target is obj_machinery_hydroponics)
        {
            var hydro_tray = (obj_machinery_hydroponics)target;
            if (hydro_tray.myseed == null)
                return FALSE;
            if (Dm.truthy(our_vault.plant_dna[hydro_tray.myseed.type]))
            {
                balloon_alert(user, I18n.T("data already in vault!"));
                return FALSE;
            }
            if (Dm.truthy(stored_dna_plants[hydro_tray.myseed.type]))
            {
                balloon_alert(user, I18n.T("data already in scanner!"));
                return FALSE;
            }
            if (hydro_tray.plant_status != HYDROTRAY_PLANT_HARVESTABLE) // So it's bit harder.
            {
                balloon_alert(user, I18n.T("plant is not harvestable!"));
                return FALSE;
            }
            stored_dna_plants[hydro_tray.myseed.type] = TRUE;
            playsound(this, Dm.resource("sound/machines/compiler/compiler-stage2.ogg"), 50);
            balloon_alert(user, I18n.T("data added"));
            return TRUE;
        }

        if (ishuman(target) && !HAS_TRAIT(target, TRAIT_LESSER_HUMANOID))
        {
            var human_target = (mob_living_carbon_human)target;
            if (Dm.truthy(our_vault.human_dna[human_target.dna.unique_identity]))
            {
                balloon_alert(user, I18n.T("data already in vault!"));
                return FALSE;
            }
            if (Dm.truthy(stored_dna_human[human_target.dna.unique_identity]))
            {
                balloon_alert(user, I18n.T("data already in scanner!"));
                return FALSE;
            }
            if (!Dm.truthy(Dm.bitand(human_target.mob_biotypes, MOB_ORGANIC)))
            {
                balloon_alert(user, I18n.T("no compatible dna!"));
                return FALSE;
            }
            stored_dna_human[human_target.dna.unique_identity] = TRUE;
            playsound(this, Dm.resource("sound/machines/compiler/compiler-stage2.ogg"), 50);
            balloon_alert(user, I18n.T("data added"));
            return TRUE;
        }

        if (!Dm.truthy(is_type_in_typecache(target, animal_typecache)) && !HAS_TRAIT(target, TRAIT_LESSER_HUMANOID))
            return FALSE;
        if (Dm.truthy(our_vault.animal_dna[target.type]))
        {
            balloon_alert(user, I18n.T("data already in vault!"));
            return FALSE;
        }
        if (Dm.truthy(stored_dna_animal[target.type]))
        {
            balloon_alert(user, I18n.T("data already in scanner!"));
            return FALSE;
        }
        if (isliving(target))
        {
            var living_target = (mob_living)target;
            if (!Dm.truthy(Dm.bitand(living_target.mob_biotypes, MOB_ORGANIC)))
            {
                balloon_alert(user, I18n.T("no compatible dna!"));
                return FALSE;
            }
        }

        stored_dna_animal[target.type] = TRUE;
        playsound(this, Dm.resource("sound/machines/compiler/compiler-stage2.ogg"), 50);
        balloon_alert(user, I18n.T("data added"));
        return TRUE;
    }

    // tg: code/game/objects/items/dna_probe.dm:146
    public virtual object valid_scan_target(atom target = null)
    {
        if (Dm.truthy(Dm.bitand(allowed_scans, DNA_PROBE_SCAN_PLANTS)) && target is obj_machinery_hydroponics)
            return TRUE;
        if (Dm.truthy(Dm.bitand(allowed_scans, DNA_PROBE_SCAN_HUMANS)) && (ishuman(target) && !HAS_TRAIT(target, TRAIT_LESSER_HUMANOID)))
            return TRUE;
        if (Dm.truthy(Dm.bitand(allowed_scans, DNA_PROBE_SCAN_ANIMALS)) && (Dm.truthy(is_type_in_typecache(target, animal_typecache)) || HAS_TRAIT(target, TRAIT_LESSER_HUMANOID)))
            return TRUE;
        return FALSE;
    }
}

///Used for scanning carps, and then turning yourself into one.
// tg: code/game/objects/items/dna_probe.dm:158
public partial class obj_item_dna_probe_carp_scanner : obj_item_dna_probe
{
    private const float CARP_MIX_DNA_TIMER = (15 * SECONDS); // tg: dna_probe.dm:155 (file-local, #undef at :195)

    // tg: code/game/objects/items/dna_probe.dm:164
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        dot = Dm.list(span_notice(I18n.T("Using this on a Space Carp will harvest its DNA. Use it in-hand once complete to mutate it with yourself.")));
        return dot;
    }

    // tg: code/game/objects/items/dna_probe.dm:168
    public override object scan_dna(atom target = null, mob user = null)
    {
        if (target is mob_living_basic_carp)
        {
            carp_dna_loaded = TRUE;
            playsound(this, Dm.resource("sound/machines/compiler/compiler-stage2.ogg"), 50);
            balloon_alert(user, I18n.T("dna scanned"));
        }
        else
            return base.scan_dna(target, user);
        return null;
    }

    // tg: code/game/objects/items/dna_probe.dm:176
    public override object valid_scan_target(atom target = null)
    {
        if (target is mob_living_basic_carp)
            return TRUE;
        return base.valid_scan_target(target);
    }

    // tg: code/game/objects/items/dna_probe.dm:181
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (!Dm.truthy(carp_dna_loaded))
        {
            to_chat(user, span_notice(I18n.T("Space carp DNA is required to use the self-mutation mechanism!")));
            return dot;
        }
        to_chat(user, span_notice(I18n.T("You pull out the needle from [] and flip the switch, and start injecting yourself with it.", this)));
        if (!Dm.truthy(await do_after(user, CARP_MIX_DNA_TIMER)))
            return dot;
        Dm.NotTranslated("code/game/objects/items/dna_probe.dm:189", "attack_self: the user turned into a raging space dragon (change_mob_type, its anger glow and dragon_rage, the Lifesign Alert, the sampler deleted): the space dragon, an antagonist, is not ported (D333)");
        return dot;
    }
}

// tg: code/game/objects/items/dna_probe.dm:31
public sealed partial class TgState
{
    internal DmList obj_item_dna_probe__animal_typecache;
}
