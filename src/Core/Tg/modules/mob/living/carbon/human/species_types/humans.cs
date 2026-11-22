// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/species_types/humans.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The var block was written by the M2 contracts step (docs/ai/specs/m2-ownership.md); gaps SP1 (D338): the human's procs, the preview and the
// Character Setup's species page (the description, the lore, the perks).

// tg: code/modules/mob/living/carbon/human/species_types/humans.dm:1
// The var overrides (:2-9) were written by the M2 contracts step (rules 10.2).
[DmPath("/datum/species/human")]
public partial class datum_species_human : datum_species
{
    // tg: code/modules/mob/living/carbon/human/species_types/humans.dm:2-9  the var overrides (rules 10.2);
    // inherent_traits (:4) creates a list: dm_init_vars
    public datum_species_human()
    {
        name = Dm.text("\\improper Human");
        id = SPECIES_HUMAN;
        skinned_type = typeof(obj_item_stack_sheet_animalhide_carbon_human);
        changesource_flags = Dm.bitor(Dm.bitor(Dm.bitor(Dm.bitor(Dm.bitor(Dm.bitor(MIRROR_BADMIN, WABBAJACK), MIRROR_MAGIC), MIRROR_PRIDE), ERT_SPAWN), RACE_SWAP), SLIME_EXTRACT);
        payday_modifier = 1.1f;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/humans.dm:4  the object-creating default (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        inherent_traits = Dm.list(
            TRAIT_USES_SKINTONES);
    }
    // tg: code/modules/mob/living/carbon/human/species_types/humans.dm:11
    public override async DmTask prepare_human_for_preview(mob_living_carbon_human human = null)
    {
        human.set_haircolor("#bb9966", update: FALSE); // brown
        human.set_hairstyle("Business Hair", update: TRUE);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/humans.dm:15
    public override object get_species_description()
    {
        return I18n.T("Humans are the dominant species in the known galaxy. Their kind extend from old Earth to the edges of known space.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/humans.dm:19
    public override object get_species_lore()
    {
        return Dm.list(
            I18n.T("These primate-descended creatures, originating from the mostly harmless Earth, have long-since outgrown their home and semi-benign designation. The space age has taken humans out of their solar system and into the galaxy-at-large."),

            I18n.T("In traditional human fashion, this near-record pace from terra firma to the final frontier spat in the face of other races they now shared a stage with. This included the lizards - if anyone was offended by these upstarts, it was certainly lizardkind."),

            I18n.T("Humanity never managed to find the kind of peace to fully unite under one banner like other species. The pencil and paper pushing of the UN bureaucrat lives on in the mosaic that is TerraGov; a composite of the nation-states that still live on in human society."),

            I18n.T("The human spirit of opportunity and enterprise continues on in its peak form: the hypercorporation. Acting outside of TerraGov's influence, literally and figuratively, hypercorporations buy the senate votes they need and establish territory far past the Earth Government's reach. In hypercorporation territory company policy is law, giving new meaning to \"employee termination\"."));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/humans.dm:39
    public override object create_pref_unique_perks()
    {
        DmList to_add = Dm.list();

        if (Dm.num(config.Get(typeof(datum_config_entry_number_default_laws))) == 0 || Dm.truthy(config.Get(typeof(datum_config_entry_flag_silicon_asimov_superiority_override)))) // Default lawset is set to Asimov
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "robot"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Asimov Superiority")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("The AI and their cyborgs are, by default, subservient only to humans. As a human, silicons are required to both protect and obey you.")))));

        object human_authority_setting = config.Get(typeof(datum_config_entry_string_human_authority));

        if (Dm.eq(human_authority_setting, HUMAN_AUTHORITY_NON_HUMAN_WHITELIST) || Dm.eq(human_authority_setting, HUMAN_AUTHORITY_ENFORCED))
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "bullhorn"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Chain of Command")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Nanotrasen only recognizes humans for command roles, such as Captain.")))));

        return to_add;
    }
}
