// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/_common/antag_hud.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A1 (D316): the whole file: the HUD a team sees of its own antagonists (has_antagonist) and every mind's combo HUD
// of its antagonist icons (antagonist_hud: the admins' combo HUD and everyone at the round's end). The generated `image` var of the
// basic kind is a /type, so the image is `alt_image` (game/atom/alternate_appearance.cs). The var blocks are generated.

public partial class datum_controller_global_vars
{
    /// All active /datum/atom_hud/alternate_appearance/basic/has_antagonist instances
    // tg: code/modules/antagonists/_common/antag_hud.dm:2  GLOBAL_LIST_EMPTY_TYPED(has_antagonist_huds, /datum/atom_hud/alternate_appearance/basic/has_antagonist)
    public DmList has_antagonist_huds;

    // tg: code/modules/antagonists/_common/antag_hud.dm:2  (GLOBAL_MANAGED(has_antagonist_huds, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalhas_antagonist_huds()
    {
        has_antagonist_huds = Dm.list();
        Dm.append(ref gvars_datum_init_order, "has_antagonist_huds");
    }
}

/// An alternate appearance that will only show if you have the antag datum
// tg: code/modules/antagonists/_common/antag_hud.dm:5
public partial class datum_atom_hud_alternate_appearance_basic_has_antagonist : datum_atom_hud_alternate_appearance_basic
{
    // tg: code/modules/antagonists/_common/antag_hud.dm:10
    [DmParams("key", "I", "antag_datum_type", "team")]
    public override async DmTask New(params object[] __extra)
    {
        object key = Dm.arg(__extra, 0);
        image I = Dm.arg<image>(__extra, 1);
        object antag_datum_type = Dm.arg(__extra, 2);
        datum_weakref team = Dm.arg<datum_weakref>(__extra, 3);
        if (Dm.truthy(antag_datum_type))
            this.antag_datum_type = antag_datum_type as Type;
        this.team_ref = team;
        Dm.append(GLOB.has_antagonist_huds, this);
        await base.New(Dm.forward(__extra, key, I, NONE));
    }

    // tg: code/modules/antagonists/_common/antag_hud.dm:17
    public override object Destroy(float? force = null)
    {
        Dm.remove(GLOB.has_antagonist_huds, this);
        return base.Destroy(force);
    }

    // tg: code/modules/antagonists/_common/antag_hud.dm:21
    public override object mobShouldSee(mob M = null)
    {
        if (Dm.truthy(add_ghost_version) && isobserver(M))
            return FALSE; // use the ghost version instead
        datum_team antag_team = team_ref?.resolve() as datum_team;
        if (antag_team != null)
            return Dm.num(Dm.@in(M.mind, antag_team.members));
        return Dm.num(Dm.truthy(M.mind?.has_antag_datum(antag_datum_type)));
    }
}

/// An alternate appearance that will show all the antagonists this mob has
// tg: code/modules/antagonists/_common/antag_hud.dm:30
public partial class datum_atom_hud_alternate_appearance_basic_antagonist_hud : datum_atom_hud_alternate_appearance_basic
{
    // tg: code/modules/antagonists/_common/antag_hud.dm:36
    [DmParams("key", "mind")]
    public override async DmTask New(params object[] __extra)
    {
        object key = Dm.arg(__extra, 0);
        datum_mind mind = Dm.arg<datum_mind>(__extra, 1);
        this.mind = mind;

        antag_hud_images = get_antag_hud_images(mind) as DmList;

        image first_antagonist = get_antag_image(1) as image ?? image(icon(Dm.resource("icons/blanks/32x32.dmi"), "nothing"), mind.current);

        RegisterSignals(
            mind,
            Dm.list(COMSIG_ANTAGONIST_GAINED, COMSIG_ANTAGONIST_REMOVED),
            nameof(update_antag_hud_images));

        check_processing();

        await base.New(Dm.forward(__extra, key, first_antagonist, NONE));
    }

    // tg: code/modules/antagonists/_common/antag_hud.dm:53
    public override object Destroy(float? force = null)
    {
        QDEL_LIST(antag_hud_images);
        STOP_PROCESSING(SSantag_hud, this);
        mind.antag_hud = null;
        mind = null;

        return base.Destroy(force);
    }

    // tg: code/modules/antagonists/_common/antag_hud.dm:61
    public override object mobShouldSee(mob mob = null)
    {
        return Dm.num(Dm.num(Master.current_runlevel) >= RUNLEVEL_POSTGAME || (Dm.truthy(mob.client?.combo_hud_enabled) && mob.client?.holder != null));
    }

    // tg: code/modules/antagonists/_common/antag_hud.dm:64
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        index += 1;
        update_icon();
        return null;
    }

    // tg: code/modules/antagonists/_common/antag_hud.dm:68
    public virtual void check_processing()
    {
        // tg's quirk: datum_flags is a number, so DF_ISPROCESSING is never "in" it (START_PROCESSING checks the flag itself)
        if (antag_hud_images.len > 1 && !Dm.@in(DF_ISPROCESSING, datum_flags))
            START_PROCESSING(SSantag_hud, this);
        else if (antag_hud_images.len <= 1)
            STOP_PROCESSING(SSantag_hud, this);
    }

    // tg: code/modules/antagonists/_common/antag_hud.dm:74
    public virtual object get_antag_image(object index = null)
    {
        if (Dm.truthy(antag_hud_images.len))
            return antag_hud_images[(Dm.num(index) % antag_hud_images.len) + 1];
        return null;
    }

    // tg: code/modules/antagonists/_common/antag_hud.dm:79
    public virtual object get_antag_hud_images(datum_mind mind = null)
    {
        DmList final_antag_hud_images = Dm.list();

        foreach (object a in Dm.each(mind?.antag_datums))
        {
            datum_antagonist antagonist = (datum_antagonist)a;
            if (antagonist.antag_hud_name == null)
                continue;
            Dm.append(ref final_antag_hud_images, antagonist.hud_image_on(mind.current));
        }

        return final_antag_hud_images;
    }

    // tg: code/modules/antagonists/_common/antag_hud.dm:89
    public virtual void update_icon()
    {
        if (antag_hud_images.len == 0)
            alt_image.icon = icon(Dm.resource("icons/blanks/32x32.dmi"), "nothing");
        else
            alt_image.icon = icon(((image)get_antag_image(index)).icon, ((image)get_antag_image(index)).icon_state);
    }

    // tg: code/modules/antagonists/_common/antag_hud.dm:95
    [SignalHandler]
    public virtual void update_antag_hud_images(datum_mind source = null)
    {
        antag_hud_images = get_antag_hud_images(source) as DmList;
        index = clamp(Dm.num(index), 1, antag_hud_images.len);
        update_icon();
        check_processing();
    }
}
