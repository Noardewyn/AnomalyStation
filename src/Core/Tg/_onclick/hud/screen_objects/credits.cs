// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/screen_objects/credits.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R5 (D327): the whole file: the end credits each client rolls when the round ends, from the contributors' portraits of the
// config folder's contributors.dmi (tgstation ships none, so tg's servers roll none either), the Hide Credits verb and the credit
// screen object. The roll's order is the .dmi's icon states, which the server cannot list (it reads no icon files, D104), so a host
// that adds a contributors.dmi gets no credits until the client reads it (not-translated.md 186). The var blocks are generated (D111).

public partial class client : datum
{
    // tg: code/_onclick/hud/screen_objects/credits.dm:1-5  (file-local defines, rules 11.4)
    internal const float CREDIT_ROLL_SPEED = 12.5f * SECONDS;
    internal const float CREDIT_SPAWN_SPEED = 1 * SECONDS;
    internal const float CREDIT_ANIMATE_HEIGHT = 14 * ICON_SIZE_Y;
    internal const float CREDIT_EASE_DURATION = 2.2f * SECONDS;
    internal static string CREDITS_PATH => Dm.text("[]/contributors.dmi", config.directory);

    // tg: code/_onclick/hud/screen_objects/credits.dm:7
    public virtual void RollCredits()
    {
        Waiting.Detach(RollCredits__body()); // set waitfor = FALSE
    }

    // tg: code/_onclick/hud/screen_objects/credits.dm:7  RollCredits' body
    private async DmTask RollCredits__body()
    {
        if (!Dm.truthy(fexists(CREDITS_PATH)))
            return;
        // tg: :11-22  new /icon(CREDITS_PATH), LAZYINITLIST(credits), ASSIGN_GAME_VERB(src, /client, ClearCredits), the roll's order from
        // icon_states(credits_icon) (shuffled after "Thanks for playing!"), a new credit screen object every CREDIT_SPAWN_SPEED, then
        // UNASSIGN_GAME_VERB after CREDIT_ROLL_SPEED: icon_states of a .dmi needs the icon files, which the server does not read (D104)
        Dm.Deferred("code/_onclick/hud/screen_objects/credits.dm:11", "RollCredits: the contributors' portraits of config/contributors.dmi (icon_states, the server reads no icon files, D104)");
        await DmTask.CompletedTask;
    }

    // tg: code/_onclick/hud/screen_objects/credits.dm:27  GAME_VERB_PROC(/client, ClearCredits, "Hide Credits", "OOC"): the body
    public virtual async DmTask __gvb_ClearCredits(DmList structured_args = null)
    {
        SSverbs.unassign_verb(this, typeof(datum_verb_metadata_client_ClearCredits)); // UNASSIGN_GAME_VERB(src, /client, ClearCredits)
        QDEL_LIST(credits);
        credits = null;
        await DmTask.CompletedTask;
    }

    // tg: code/_onclick/hud/screen_objects/credits.dm:27  the verb proc
    public virtual void ClearCredits(params object[] __extra)
    {
        Waiting.Detach(__gvb_ClearCredits(Dm.list()));
    }
}

// tg: code/_onclick/hud/screen_objects/credits.dm:32
public partial class atom_movable_screen_credit : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/credits.dm:39
    [DmParams("mapload", "hud_owner", "credited", "P", "I")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object credited = Dm.arg(__extra, 1);
        client P = Dm.arg<client>(__extra, 2);
        object I = Dm.arg(__extra, 3);
        object dot = base.Initialize(mapload, __extra);
        icon = I;
        parent = P;
        icon_state = Dm.str(credited);
        maptext = MAPTEXT_PIXELLARI(credited);
        maptext_x = ICON_SIZE_X + 8;
        maptext_y = (ICON_SIZE_Y / 2) - 4;
        maptext_width = ICON_SIZE_X * 6;
        var M = Dm.matrix(transform);
        M.Translate(0, client.CREDIT_ANIMATE_HEIGHT);
        animate(this, Dm.named("transform", M), Dm.named("time", client.CREDIT_ROLL_SPEED));
        animate(this, Dm.named("alpha", 255), Dm.named("time", client.CREDIT_EASE_DURATION), Dm.named("flags", ANIMATION_PARALLEL));
        _addtimer(Dm.@new<datum_callback>(this, nameof(FadeOut)), client.CREDIT_ROLL_SPEED - client.CREDIT_EASE_DURATION, file: "code/_onclick/hud/screen_objects/credits.dm", line: 52);
        // QDEL_IN(src, CREDIT_ROLL_SPEED), inline (rules 11.1 (f))
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), client.CREDIT_ROLL_SPEED > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this), client.CREDIT_ROLL_SPEED, file: "code/_onclick/hud/screen_objects/credits.dm", line: 53);
        if (parent != null)
            parent.screen.Add(this);
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/credits.dm:57
    public override object Destroy(float? force = null)
    {
        icon = null;
        if (parent != null)
        {
            parent.screen.Remove(this);
            LAZYREMOVE(ref parent.credits, this);
            parent = null;
        }
        return base.Destroy(force);
    }

    // tg: code/_onclick/hud/screen_objects/credits.dm:65
    public virtual void FadeOut()
    {
        animate(this, Dm.named("alpha", 0), Dm.named("time", client.CREDIT_EASE_DURATION), Dm.named("flags", ANIMATION_PARALLEL));
    }
}
