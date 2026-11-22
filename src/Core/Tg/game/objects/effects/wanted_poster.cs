// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/wanted_poster.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps SC2, D333): the wanted and missing posters the security records console prints, rolled up and hung on a
// wall with the record's mugshot and the header across their top. The var blocks are generated (D111). DV498: the mugshot and the
// header's letters are the hung poster's overlays over its paper, not blended into one icon.

/*
	This type poster is of 3 parts: picture, foreground, background

	background - Poster paper printed on
	foreground - Text on top of poster
	picture - The image on the poster: Typically a missing person or wanted individual.

*/

// tg: code/game/objects/effects/wanted_poster.dm:10
public partial class obj_item_poster_wanted : obj_item_poster
{
    // tg: code/game/objects/effects/wanted_poster.dm:25
    [DmParams("mapload", "new_poster_structure", "person_icon", "wanted_name", "description", "headerText", "posterHeaderColor")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_structure_sign_poster new_poster_structure = Dm.arg<obj_structure_sign_poster>(__extra, 0);
        icon person_icon = Dm.arg<icon>(__extra, 1);
        object wanted_name = Dm.arg(__extra, 2);
        object description = Dm.arg(__extra, 3);
        object headerText = Dm.arg(__extra, 4);
        object posterHeaderColor = Dm.arg(__extra, 5);
        if (Dm.truthy(posterHeaderColor))
            postHeaderColor = (string)posterHeaderColor;

        // var/obj/structure/sign/poster/wanted/wanted_poster: it holds the structure passed in, of any poster type (rules 3.4)
        obj_structure_sign_poster wanted_poster = null;

        if (new_poster_structure != null) // this is shit but a bigger refactor would be needed to properly resolve this
            wanted_poster = new_poster_structure;
        else
            wanted_poster = Dm.@new<obj_structure_sign_poster_wanted>(this, person_icon, wanted_name, description, headerText, postHeaderColor, background, postName, postDesc);

        object dot = base.Initialize(mapload, wanted_poster);
        name = I18n.T("[] ([])", postName, wanted_name);
        desc = I18n.T("[] [].", postDesc, wanted_name);
        postHeaderText = (string)headerText;
        return dot;
    }
}

// tg: code/game/objects/effects/wanted_poster.dm:41
public partial class obj_structure_sign_poster_wanted : obj_structure_sign_poster
{
    // tg: code/game/objects/effects/wanted_poster.dm:51
    [DmParams("mapload", "person_icon", "person_name", "description", "postHeaderText", "postHeaderColor", "background", "pname", "pdesc")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        icon person_icon = Dm.arg<icon>(__extra, 0);
        object person_name = Dm.arg(__extra, 1);
        object description = Dm.arg(__extra, 2);
        object postHeaderText = Dm.arg(__extra, 3);
        object postHeaderColor = Dm.arg(__extra, 4);
        object background = Dm.arg(__extra, 5);
        object pname = Dm.arg(__extra, 6);
        object pdesc = Dm.arg(__extra, 7);
        object dot = base.Initialize(mapload, __extra);
        if (person_icon == null)
            return INITIALIZE_HINT_QDEL;

        postName = pname;
        postDesc = pdesc;
        posterHeaderText = postHeaderText;
        posterHeaderColor = postHeaderColor;
        wanted_name = person_name;

        name = I18n.T("[] ([])", postName, wanted_name);
        desc = (string)description;

        person_icon = Dm.icon(person_icon, dir: SOUTH);//copy the image so we don't mess with the one in the record.
        original_icon = Dm.icon(person_icon); //cache this incase it gets torn down
        icon the_icon = Dm.icon(Dm.resource("icons/obj/poster_wanted.dmi"), background);
        person_icon.Shift(SOUTH, 7);
        person_icon.Crop(7, 4, 26, 30);
        person_icon.Crop(-5, -2, 26, 29);
        // DEVIATION(DV498): the_icon.Blend(person_icon, ICON_OVERLAY), and print_across_top's letters below, are the poster's overlays drawn
        // in the same order over its paper: the client composes an /icon from its recipe (D104), which a resource caps at 512 bytes, and
        // the mugshot's and seven letters' recipes blended into one overflow it, so the hung poster would draw nothing
        add_overlay(Dm.image(person_icon));

        // Print text on top of poster.
        print_across_top(the_icon, postHeaderText, postHeaderColor);

        // the_icon.Insert(the_icon, "wanted"), the_icon.Insert(icon('icons/obj/poster.dmi', "poster_being_set"), "poster_being_set") and
        // the_icon.Insert(icon('icons/obj/poster.dmi', "poster_ripped"), "poster_ripped"): the port's /icon holds one state (D104), its
        // own as the default (protocol.md 34.3), so the hanging animation's state is not in the picture (not-translated.md 198)

        icon = the_icon;
        return dot;
    }

    /*
    	This proc will write "WANTED" or MISSING" at the top of the poster.

    	You can put other variables in like text and color

    	text: Up to 7 characters of text to be printed on the top of the poster.
    	color: This set the text color: #ff00ff
    */
    // tg: code/game/objects/effects/wanted_poster.dm:90
    public virtual void print_across_top(icon poster_icon = null, object text = null, object color = null)
    {
        float textLen = min(length(text), 7);
        float startX = 16 - (2 * textLen);
        float i = 0;
        for (i = 1; i <= textLen; i++)
        {
            object letter = uppertext(Dm.textchar(text, i));
            icon letter_icon = Dm.icon(Dm.resource("icons/testing/Font_Minimal.dmi"), letter);
            letter_icon.Shift(EAST, startX); //16 - (2*n)
            letter_icon.Shift(SOUTH, 2);
            letter_icon.SwapColor(rgb(255, 255, 255), color);
            add_overlay(Dm.image(letter_icon)); // DEVIATION(DV498): poster_icon.Blend(letter_icon, ICON_OVERLAY), an overlay over the paper
            startX = startX + 4;
        }
    }

    /// We have some additional params since we are doing icon shenanigans
    // tg: code/game/objects/effects/wanted_poster.dm:104
    public override DmTask<object> return_to_poster_item(atom location = null)
    {
        return DmTask<object>.FromResult(Dm.@new(poster_item_type, location, this, original_icon, wanted_name, desc, posterHeaderText, posterHeaderColor));
    }
}
