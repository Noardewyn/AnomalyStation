// Translated from tgstation (snapshot 1d99bd7f): code/datums/records/record.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice E (the crew manifest): the record datum, the crew record and the admin locked record, with get_byond_gender (M9 slice D:
// the radio's virtual speaker); the rapsheet printout (get_rapsheet) came with the paperwork (gaps S3), the records' photos
// (get_front_photo, get_side_photo, recreate_manifest_photos, delete_photos, get_photo, make_photo) with the records consoles (gaps
// SC2, D333): a photo's picture is the port's flat icon of the record's appearance (DV497). The var blocks are generated (D111) but
// for blood_type, species and trim, which hold text, and character_appearance, which holds an appearance or a photo's icon (the
// Hand mode).

/**
 * Record datum. Used for crew records and admin locked records.
 */
// tg: code/datums/records/record.dm:4
public partial class datum_record : datum
{
    // tg: code/datums/records/record.dm:8, :24, :26  (text: the generated guess of a type path does not hold; the Hand mode)
    /// Their blood type
    public object blood_type;
    /// The character's species
    public object species;
    /// The character's ID trim
    public object trim;

    // tg: code/datums/records/record.dm:10  (an appearance, or the /icon of the photo a records console makes a record from,
    // records.dm:156: the declared mutable_appearance would null the icon, rules 3.2)
    /// Character appearance
    public object character_appearance;

    // tg: code/datums/records/record.dm:30
    [DmParams("age", "blood_type", "character_appearance", "dna_string", "fingerprint", "gender", "initial_rank", "name", "rank", "species", "trim", "voice")]
    public override async DmTask New(params object[] __extra)
    {
        age = Dm.arg(__extra, 0) ?? 18;
        blood_type = Dm.arg(__extra, 1) ?? "?";
        character_appearance = Dm.arg(__extra, 2);
        dna_string = Dm.arg(__extra, 3) ?? "Unknown";
        fingerprint = Dm.arg(__extra, 4) ?? "?????";
        gender = Dm.arg(__extra, 5) ?? "Other";
        object rank_arg = Dm.arg(__extra, 8) ?? "Unassigned";
        initial_rank = rank_arg; // src.initial_rank = rank, as tg sets it
        name = Dm.str(Dm.arg(__extra, 7) ?? "Unknown");
        rank = rank_arg;
        species = Dm.arg(__extra, 9) ?? "Human";
        trim = Dm.arg(__extra, 10) ?? "Unassigned";
    }

    /// Parses gender field into byond gender defines
    // tg: code/datums/records/record.dm:57
    public virtual string get_byond_gender()
    {
        string record_gender = Dm.str(LOWER_TEXT(gender));
        // already slots nicely into a define
        if (record_gender == MALE || record_gender == FEMALE || record_gender == NEUTER || record_gender == PLURAL)
            return record_gender;
        // they/them if "unknown"
        if (record_gender == "unknown")
            return PLURAL;
        // it/its if "other"
        return NEUTER;
    }
}

/**
 * Crew record datum
 */
// tg: code/datums/records/record.dm:71
public partial class datum_record_crew : datum_record
{
    // tg: code/datums/records/record.dm:104
    [DmParams("age", "blood_type", "character_appearance", "dna_string", "fingerprint", "gender", "initial_rank", "name", "rank", "species", "trim", "lock_ref", "major_disabilities", "major_disabilities_desc", "minor_disabilities", "minor_disabilities_desc", "physical_status", "mental_status", "quirk_notes")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra); // . = ..(): the same arguments
        lock_ref = Dm.arg(__extra, 11);
        major_disabilities = Dm.arg(__extra, 12) ?? "None";
        major_disabilities_desc = Dm.arg(__extra, 13) ?? "No disabilities have been diagnosed at the moment.";
        minor_disabilities = Dm.arg(__extra, 14) ?? "None";
        minor_disabilities_desc = Dm.arg(__extra, 15) ?? "No disabilities have been diagnosed at the moment.";
        physical_status = Dm.arg(__extra, 16) ?? PHYSICAL_ACTIVE;
        mental_status = Dm.arg(__extra, 17) ?? MENTAL_STABLE;
        quirk_notes = Dm.arg(__extra, 18);

        Dm.append(ref GLOB.manifest.general, this);
    }

    // tg: code/datums/records/record.dm:138
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.manifest.general, this);
        QDEL_LAZYLIST(ref record_photos);
        return base.Destroy(force);
    }

    /// A helper proc to get the front photo of a character from the record.
    /// Handles calling `get_photo()`, read its documentation for more information.
    // tg: code/datums/records/record.dm:183
    public virtual object get_front_photo()
    {
        return get_photo("photo_front", SOUTH);
    }

    /// A helper proc to get the side photo of a character from the record.
    /// Handles calling `get_photo()`, read its documentation for more information.
    // tg: code/datums/records/record.dm:188
    public virtual object get_side_photo()
    {
        return get_photo("photo_side", WEST);
    }

    /// A helper proc to recreate all photos of a character from the record.
    // tg: code/datums/records/record.dm:192
    public virtual void recreate_manifest_photos(object add_height_chart = null)
    {
        delete_photos("photo_front");
        make_photo("photo_front", SOUTH, add_height_chart);
        delete_photos("photo_side");
        make_photo("photo_side", WEST, add_height_chart);
    }

    ///Deletes the existing photo for field_name
    // tg: code/datums/records/record.dm:199
    public virtual void delete_photos(object field_name = null)
    {
        obj_item_photo existing_photo = LAZYACCESS(record_photos, field_name) as obj_item_photo;
        if (existing_photo != null)
        {
            qdel(existing_photo);
            LAZYREMOVE(ref record_photos, field_name);
        }
    }

    /**
     * You shouldn't be calling this directly, use `get_front_photo()` or `get_side_photo()`
     * instead.
     *
     * This is the proc that handles either fetching (if it was already generated before) or
     * generating (if it wasn't) the specified photo from the specified record. This is only
     * intended to be used by records that used to try to access `fields["photo_front"]` or
     * `fields["photo_side"]`, and will return an empty icon if there isn't any of the necessary
     * fields.
     *
     * Arguments:
     * * field_name - The name of the key in the `fields` list, of the record itself.
     * * orientation - The direction in which you want the character appearance to be rotated
     * in the outputed photo.
     *
     * Returns an empty `/icon` if there was no `character_appearance` entry in the `fields` list,
     * returns the generated/cached photo otherwise.
     */
    // tg: code/datums/records/record.dm:223
    public virtual object get_photo(object field_name = null, float? orientation = null)
    {
        orientation ??= SOUTH;
        if (!Dm.truthy(field_name))
            return null;
        if (!Dm.truthy(character_appearance))
            return Dm.@new<icon>();
        obj_item_photo existing_photo = LAZYACCESS(record_photos, field_name) as obj_item_photo;
        if (existing_photo == null)
            existing_photo = make_photo(field_name, orientation) as obj_item_photo;
        return existing_photo;
    }

    /**
     * make_photo
     *
     * Called if the person doesn't already have a photo, this will make a photo of the person,
     * then make a picture out of it, then finally create a new photo.
     */
    // tg: code/datums/records/record.dm:239
    public virtual object make_photo(object field_name = null, object orientation = null, object add_height_chart = null)
    {
        icon picture_image = null;
        if (!isicon(character_appearance))
        {
            mutable_appearance appearance = character_appearance as mutable_appearance;
            appearance.dir = Dm.num(orientation); // appearance.setDir(orientation): /image/proc/setDir is `dir = newdir` (__HELPERS/icons.dm:829)
            if (Dm.truthy(add_height_chart))
                Dm.append(ref appearance.underlays, mutable_appearance(Dm.resource("icons/obj/machines/photobooth.dmi"), "height_chart", alpha: 125, appearance_flags: Dm.bitor(Dm.bitor(Dm.bitor(RESET_ALPHA, RESET_COLOR), RESET_TRANSFORM), KEEP_APART)));
            // DEVIATION(DV497): getFlatIcon(appearance) is the port's flat icon of the appearance (get_flat_uni_icon, the client's recipe,
            // D104), as DV440's: the server draws no picture
            picture_image = get_flat_uni_icon(appearance) as icon;
        }
        else
            picture_image = character_appearance as icon;

        datum_picture picture = Dm.@new<datum_picture>();
        picture.picture_name = name;
        picture.picture_desc = I18n.T("This is [].", name);
        picture.picture_image = picture_image;

        obj_item_photo new_photo = Dm.@new<obj_item_photo>(null, picture);

        LAZYSET(ref record_photos, field_name, new_photo);
        return new_photo;
    }

    /// Returns a paper printout of the current record's crime data.
    // tg: code/datums/records/record.dm:262  (gaps S3: the security filing cabinet's papers)
    public virtual object get_rapsheet(object alias = null, string header = null, string description = null)
    {
        header ??= "Rapsheet";
        description ??= "No further details.";
        float print_count = ++GLOB.manifest.print_count;
        obj_item_paper printed_paper = Dm.@new<obj_item_paper>();
        string final_paper_text = Dm.text("<center><b>SR-[]: []</b></center><br>", print_count, header);

        final_paper_text += Dm.text("Name: []<br>Gender: []<br>Age: []<br>", name, gender, age);
        if (!Dm.eq(alias, name))
            final_paper_text += Dm.text("Alias: []<br>", alias);

        final_paper_text += Dm.text("Species: []<br>Fingerprint: []<br>Wanted Status: []<br><br>", species, fingerprint, wanted_status);

        final_paper_text += "<center><B>Security Data</B></center><br><br>";

        final_paper_text += "Crimes:<br>";
        final_paper_text += "<table style=\"text-align:center;\" border=\"1\" cellspacing=\"0\" width=\"100%\">\n\t\t\t\t\t\t<tr>\n\t\t\t\t\t\t<th>Crime</th>\n\t\t\t\t\t\t<th>Details</th>\n\t\t\t\t\t\t<th>Author</th>\n\t\t\t\t\t\t<th>Time Added</th>\n\t\t\t\t\t\t</tr>";
        foreach (datum_crime crime in Dm.each<datum_crime>(crimes))
        {
            if (Dm.truthy(crime.valid))
            {
                final_paper_text += Dm.text("<tr><td>[]</td>", crime.name);
                final_paper_text += Dm.text("<td>[]</td>", crime.details);
                final_paper_text += Dm.text("<td>[]</td>", crime.author);
                final_paper_text += Dm.text("<td>[]</td>", crime.time);
            }
            else
            {
                for (int i = 1; i <= 4; i++)
                    final_paper_text += "<td>--REDACTED--</td>";
            }
            final_paper_text += "</tr>";
        }
        final_paper_text += "</table><br><br>";

        final_paper_text += "Citations:<br>";
        final_paper_text += "<table style=\"text-align:center;\" border=\"1\" cellspacing=\"0\" width=\"100%\">\n\t\t\t\t\t\t<tr>\n\t\t\t\t\t\t<th>Citation</th>\n\t\t\t\t\t\t<th>Details</th>\n\t\t\t\t\t\t<th>Author</th>\n\t\t\t\t\t\t<th>Time Added</th>\n\t\t\t\t\t\t<th>Fine</th>\n\t\t\t\t\t\t</tr><br>";
        foreach (datum_crime_citation warrant in Dm.each<datum_crime_citation>(citations))
        {
            final_paper_text += Dm.text("<tr><td>[]</td>", warrant.name);
            final_paper_text += Dm.text("<td>[]</td>", warrant.details);
            final_paper_text += Dm.text("<td>[]</td>", warrant.author);
            final_paper_text += Dm.text("<td>[]</td>", warrant.time);
            final_paper_text += Dm.text("<td>[]</td>", warrant.fine);
            final_paper_text += "</tr>";
        }
        final_paper_text += "</table><br><br>";

        final_paper_text += "<center>Important Notes:</center><br>";
        if (Dm.truthy(security_note))
            final_paper_text += Dm.text("- []<br>", security_note);
        if (Dm.truthy(description))
            final_paper_text += Dm.text("- []<br>", description);

        printed_paper.name = I18n.T("SR-[] '[]'", print_count, name);
        printed_paper.add_raw_text(final_paper_text);
        printed_paper.update_appearance();

        return printed_paper;
    }
}

/**
 * Admin locked record
 */
// tg: code/datums/records/record.dm:146
public partial class datum_record_locked : datum_record
{
    // tg: code/datums/records/record.dm:154
    [DmParams("age", "blood_type", "character_appearance", "dna_string", "fingerprint", "gender", "initial_rank", "name", "rank", "species", "trim", "locked_dna", "mind_ref")]
    public override async DmTask New(params object[] __extra)
    {
        // . = ..() with this proc's own default gender, "neuter"
        object[] base_args = new object[Math.Max(6, __extra?.Length ?? 0)];
        __extra?.CopyTo(base_args, 0);
        base_args[5] ??= "neuter";
        await base.New(base_args);
        locked_dna = Dm.arg(__extra, 11) as datum_dna;
        mind_ref = WEAKREF(Dm.arg(__extra, 12) as datum) as datum_weakref;
        species_type = locked_dna.species.GetType();

        Dm.append(ref GLOB.manifest.locked, this);
    }

    // tg: code/datums/records/record.dm:177
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.manifest.locked, this);
        return base.Destroy(force);
    }
}
