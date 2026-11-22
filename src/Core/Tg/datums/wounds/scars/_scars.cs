// Translated from tgstation (snapshot 1d99bd7f): code/datums/wounds/scars/_scars.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M6 slice 2: a healed wound leaves a scar; the scar texts come from tg's strings/wounds files). The var
// block is generated (D111). Persistent scars (load, format, format_amputated) are translated with the file; their
// callers, the preferences' persistent scarring, are not ported.

/**
 * scars are cosmetic datums that are assigned to bodyparts once they recover from wounds. Each wound type and severity have their own descriptions for what the scars
 * look like, and then each body part has a list of "specific locations" like your elbow or wrist or wherever the scar can appear, to make it more interesting than "right arm"
 *
 *
 * Arguments:
 * *
 */
// tg: code/datums/wounds/scars/_scars.dm:9
public partial class datum_scar : datum
{
    // tg: code/datums/wounds/scars/_scars.dm:33
    public override object Destroy(float? force = null)
    {
        object dot = null;
        if (limb != null)
            LAZYREMOVE(ref limb.scars, this);
        if (victim != null)
            LAZYREMOVE(ref victim.all_scars, this);
        limb = null;
        victim = null;
        dot = base.Destroy(force);
        return dot;
    }

    /**
     * generate() is used to actually fill out the info for a scar, according to the limb and wound it is provided.
     *
     * After creating a scar, call this on it while targeting the scarred bodypart with a given wound to apply the scar.
     *
     * Arguments:
     * * BP- The bodypart being targeted
     * * W- The wound being used to generate the severity and description info
     * * add_to_scars- Should always be TRUE unless you're just storing a scar for later usage, like how cuts want to store a scar for the highest severity of cut, rather than the severity when the wound is fully healed (probably demoted to moderate)
     */
    // tg: code/datums/wounds/scars/_scars.dm:52
    public virtual void generate(obj_item_bodypart BP = null, datum_wound W = null, float? add_to_scars = null)
    {
        add_to_scars ??= TRUE;

        if (!Dm.truthy(W.can_scar))
        {
            qdel(this);
            return;
        }

        datum_wound_pregen_data pregen_data = GLOB.all_wound_pregen_data[W.type] as datum_wound_pregen_data;
        if (pregen_data == null)
        {
            qdel(this);
            return;
        }

        required_limb_biostate = pregen_data.required_limb_biostate;
        check_any_biostates = pregen_data.require_any_biostate;

        limb = BP;
        RegisterSignal(limb, COMSIG_QDELETING, nameof(limb_gone));

        severity = W.severity;
        if (limb.owner != null)
        {
            victim = limb.owner;
            // the generated var is a number (default 0); a mind without a slot index gives null in tg, 0 here
            persistent_character_slot = Dm.num(victim.mind?.original_character_slot_index);
        }
        if (Dm.truthy(add_to_scars))
        {
            LAZYADD(ref limb.scars, this);
            if (victim != null)
                LAZYADD(ref victim.all_scars, this);
        }

        object scar_file = W.get_scar_file(BP, add_to_scars);
        object scar_keyword = W.get_scar_keyword(BP, add_to_scars);
        if (!Dm.truthy(scar_file) || !Dm.truthy(scar_keyword))
        {
            qdel(this);
            return;
        }

        description = pick_list(scar_file, scar_keyword);
        if (!Dm.truthy(description))
        {
            stack_trace(Dm.text("no valid description found for scar! file: [] keyword: [] wound: []", scar_file, scar_keyword, W.type), "code/datums/wounds/scars/_scars.dm", 86);
            description = I18n.T("general disfigurement");
        }

        precise_location = pick_list_replacements(SCAR_LOC_FILE, limb.body_zone);
        switch (W.severity)
        {
            case WOUND_SEVERITY_MODERATE:
                visibility = 2;
                break;
            case WOUND_SEVERITY_SEVERE:
                visibility = 3;
                break;
            case WOUND_SEVERITY_CRITICAL:
                visibility = 5;
                break;
            case WOUND_SEVERITY_LOSS:
                visibility = 7;
                precise_location = "amputation";
                break;
        }
    }

    /// Used when we finalize a scar from a healing cut
    // tg: code/datums/wounds/scars/_scars.dm:102
    public virtual void lazy_attach(obj_item_bodypart BP = null, datum_wound W = null)
    {
        LAZYADD(ref BP.scars, this);
        if (BP.owner != null)
        {
            victim = BP.owner;
            LAZYADD(ref victim.all_scars, this);
        }
    }

    /// Used to "load" a persistent scar
    // tg: code/datums/wounds/scars/_scars.dm:109
    public virtual object load(obj_item_bodypart BP = null, object version = null, object description = null, object specific_location = null, float? severity = null, float? required_limb_biostate = null, object char_slot = null, float? check_any_biostates = null)
    {
        severity ??= WOUND_SEVERITY_SEVERE;
        required_limb_biostate ??= BIO_STANDARD_UNJOINTED;
        check_any_biostates ??= FALSE;

        if (!Dm.truthy(BP.scarrable))
        {
            qdel(this);
            return null;
        }

        limb = BP;
        RegisterSignal(limb, COMSIG_QDELETING, nameof(limb_gone));
        // tg: :115-116  if (isnull(check_any_biostates)) check_any_biostates = FALSE (so we dont break old scars): the
        // default prologue above already gives FALSE for a null argument
        if (Dm.truthy(check_any_biostates))
        {
            if (!Dm.truthy(Dm.bitand(limb.biological_state, required_limb_biostate)))
            {
                qdel(this);
                return null;
            }
        }
        else if (!Dm.eq(Dm.bitand(limb.biological_state, required_limb_biostate), required_limb_biostate)) // check for all
        {
            qdel(this);
            return null;
        }
        if (limb.owner != null)
        {
            victim = limb.owner;
            LAZYADD(ref victim.all_scars, this);
        }

        this.severity = severity;
        this.required_limb_biostate = required_limb_biostate;
        persistent_character_slot = Dm.num(char_slot);
        LAZYADD(ref limb.scars, this);

        this.description = description;
        precise_location = specific_location;
        switch (severity)
        {
            case WOUND_SEVERITY_MODERATE:
                visibility = 2;
                break;
            case WOUND_SEVERITY_SEVERE:
                visibility = 3;
                break;
            case WOUND_SEVERITY_CRITICAL:
                visibility = 5;
                break;
            case WOUND_SEVERITY_LOSS:
                visibility = 7;
                break;
        }
        return this;
    }

    // tg: code/datums/wounds/scars/_scars.dm:147
    [SignalHandler]
    public virtual void limb_gone()
    {
        qdel(this);
    }

    /// What will show up in examine_more() if this scar is visible
    // tg: code/datums/wounds/scars/_scars.dm:152
    public virtual object get_examine_description(mob viewer = null)
    {
        if (victim == null || !Dm.truthy(is_visible(viewer)))
            return null;

        string msg = I18n.T("[] [] [] on [] [].", victim.p_They(), victim.p_have(), description, victim.p_their(), precise_location);
        switch (severity)
        {
            case WOUND_SEVERITY_MODERATE:
                msg = span_tinynoticeital(Dm.text("[]", msg));
                break;
            case WOUND_SEVERITY_SEVERE:
                msg = span_smallnoticeital(Dm.text("[]", msg));
                break;
            case WOUND_SEVERITY_CRITICAL:
                msg = span_smallnoticeital(I18n.T("<b>[]</b>", msg));
                break;
            case WOUND_SEVERITY_LOSS:
                msg = I18n.T("[] [] [].", victim.p_Their(), limb.plaintext_zone, description); // different format
                msg = span_notice(I18n.T("<i><b>[]</b></i>", msg));
                break;
        }
        return I18n.T("\t[]", msg);
    }

    /// Whether a scar can currently be seen by the viewer
    // tg: code/datums/wounds/scars/_scars.dm:170
    public virtual object is_visible(mob viewer = null)
    {
        if (victim == null || viewer == null)
            return null;
        if (get_dist(viewer, victim) > visibility)
            return null;

        if (!ishuman(victim) || isobserver(viewer) || victim == viewer)
            return TRUE;

        mob_living_carbon_human human_victim = (mob_living_carbon_human)victim;
        if (limb is obj_item_bodypart_head)
        {
            if (Dm.truthy(Dm.bitand(human_victim.obscured_slots, HIDEFACE)))
                return FALSE;
        }
        else if (Dm.truthy(limb.scars_covered_by_clothes))
        {
            float num_covers = LAZYLEN(human_victim.get_clothing_on_part(limb));
            if (num_covers + get_dist(viewer, victim) >= visibility)
                return FALSE;
        }

        return TRUE;
    }

    /// Used to format a scar to save for either persistent scars, or for changeling disguises
    // tg: code/datums/wounds/scars/_scars.dm:191
    public virtual object format()
    {
        return Dm.text("[]|[]|[]|[]|[]|[]|[]|[]", SCAR_CURRENT_VERSION, limb.body_zone, description, precise_location, severity, required_limb_biostate, persistent_character_slot, check_any_biostates);
    }

    /// Used to format a scar to save in preferences for persistent scars
    // tg: code/datums/wounds/scars/_scars.dm:195
    public virtual object format_amputated(object body_zone = null, string scar_file = null)
    {
        scar_file ??= FLESH_SCAR_FILE;

        description = pick_list(scar_file, "dismember");
        return Dm.text("[]|[]|[]|amputated|[]|[]|[]|[]", SCAR_CURRENT_VERSION, body_zone, description, WOUND_SEVERITY_LOSS, required_limb_biostate, persistent_character_slot, check_any_biostates);
    }
}
