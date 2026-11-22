// Translated from tgstation (snapshot 1d99bd7f): code/datums/progressbar.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The progress bar is translated whole (M2 integration: a player's do_after, rest and get-up, reaches New). Its image goes
// to the user's client.images as in tg; the 3D client draws the busy state from replicated simulation state (DV13), so
// nothing replicates the image itself.

// tg: code/datums/progressbar.dm:4
[DmPath("/datum/progressbar")]
public partial class datum_progressbar : datum
{
    // tg: code/datums/progressbar.dm:1  #define PROGRESSBAR_HEIGHT 6 (file-local, #undef at :158)
    private const int PROGRESSBAR_HEIGHT = 8;
    // tg: code/datums/progressbar.dm:2  #define PROGRESSBAR_ANIMATION_TIME 5 (file-local, #undef at :157)
    private const int PROGRESSBAR_ANIMATION_TIME = 5;

    ///The progress bar visual element.
    // tg: code/datums/progressbar.dm:6
    public image bar;
    ///The target where this progress bar is applied and where it is shown.
    // tg: code/datums/progressbar.dm:8
    public atom bar_loc;
    ///The mob whose client sees the progress bar.
    // tg: code/datums/progressbar.dm:10
    public mob user;
    ///The client seeing the progress bar.
    // tg: code/datums/progressbar.dm:12
    public client user_client;
    ///Effectively the number of steps the progress bar will need to do before reaching completion.
    // tg: code/datums/progressbar.dm:14
    public float? goal = 1;
    ///Control check to see if the progress was interrupted before reaching its goal.
    // tg: code/datums/progressbar.dm:16
    public float last_progress = 0;
    ///Variable to ensure smooth visual stacking on multiple progress bars.
    // tg: code/datums/progressbar.dm:18
    public float listindex = 0;
    ///The type of our last value for bar_loc, for debugging
    // tg: code/datums/progressbar.dm:20
    public Type location_type;
    ///Where to draw the progress bar above the icon
    // tg: code/datums/progressbar.dm:22
    public object offset_y;

    // tg: code/datums/progressbar.dm:24
    [DmParams("User", "goal_number", "target", "starting_amount")]
    public override async DmTask New(params object[] __extra)
    {
        mob User = Dm.arg<mob>(__extra, 0);
        object goal_number = Dm.arg(__extra, 1);
        atom target = Dm.arg<atom>(__extra, 2);
        object starting_amount = Dm.arg(__extra, 3);
        await base.New(Dm.forward(__extra, User, goal_number, target, starting_amount));
        if (!(target is atom))
        {
            stack_trace(Dm.text("Invalid target [] passed in", target), "code/datums/progressbar.dm", 27);
            qdel(this);
            return;
        }
        if (QDELETED(User) || !(User is mob))
        {
            stack_trace(Dm.text("/datum/progressbar created with [] user", (User == null ? "null" : "invalid")), "code/datums/progressbar.dm", 31);
            qdel(this);
            return;
        }
        if (!isnum(goal_number))
        {
            stack_trace(Dm.text("/datum/progressbar created with [] goal_number", (User == null ? "null" : "invalid")), "code/datums/progressbar.dm", 35);
            qdel(this);
            return;
        }
        goal = Dm.num_or_null(goal_number);
        bar_loc = target;
        location_type = bar_loc.type;
        DmList icon_offsets = target.get_oversized_icon_offsets() as DmList;
        object offset_x = icon_offsets["x"];
        offset_y = icon_offsets["y"];

        bar = image(Dm.resource("icons/effects/progressbar.dmi"), bar_loc, "prog_bar_0", pixel_x: offset_x);
        SET_PLANE_EXPLICIT(bar, ABOVE_HUD_PLANE, User);
        bar.appearance_flags = APPEARANCE_UI_IGNORE_ALPHA;
        user = User;

        LAZYADDASSOCLIST(ref user.progressbars, bar_loc, this);
        DmList bars = user.progressbars[bar_loc] as DmList;
        listindex = bars.len;

        if (Dm.truthy(user.client))
        {
            user_client = user.client;
            add_prog_bar_image_to_client();
        }

        RegisterSignal(user, COMSIG_QDELETING, nameof(on_user_delete));
        RegisterSignal(user, COMSIG_MOB_LOGOUT, nameof(clean_user_client));
        RegisterSignal(user, COMSIG_MOB_LOGIN, nameof(on_user_login));

        if (Dm.truthy(starting_amount))
            update(starting_amount);
    }

    // tg: code/datums/progressbar.dm:66
    [DmParams()]
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(user))
        {
            foreach (object pb in Dm.each(((DmList)user.progressbars[bar_loc])))
            {
                datum_progressbar progress_bar = pb as datum_progressbar;
                if (progress_bar == this || progress_bar.listindex <= listindex)
                    continue;
                progress_bar.listindex--;

                progress_bar.bar.pixel_z = Dm.num(Dm.add(Dm.add(ICON_SIZE_Y, offset_y), PROGRESSBAR_HEIGHT * (progress_bar.listindex - 1)));
                object dist_to_travel = Dm.sub(Dm.add(Dm.add(ICON_SIZE_Y, offset_y), PROGRESSBAR_HEIGHT * (progress_bar.listindex - 1)), PROGRESSBAR_HEIGHT);
                animate(progress_bar.bar, Dm.named("pixel_z", dist_to_travel), Dm.named("time", PROGRESSBAR_ANIMATION_TIME), Dm.named("easing", SINE_EASING));
            }

            LAZYREMOVEASSOC(ref user.progressbars, bar_loc, this);
            user = null;
        }

        if (user_client != null)
            clean_user_client();

        bar_loc = null;
        bar = null;

        return base.Destroy(force);
    }


    ///Called right before the user's Destroy()
    // tg: code/datums/progressbar.dm:91
    [SignalHandler]
    public virtual void on_user_delete(datum source = null)
    {
        user.progressbars = null; //We can simply nuke the list and stop worrying about updating other prog bars if the user itself is gone.
        user = null;
        qdel(this);
    }


    ///Removes the progress bar image from the user_client and nulls the variable, if it exists.
    // tg: code/datums/progressbar.dm:100
    [SignalHandler]
    public virtual void clean_user_client(datum source = null)
    {
        if (user_client == null) //Disconnected, already gone.
            return;
        Dm.remove(user_client.images, bar);
        user_client = null;
    }


    ///Called by user's Login(), it transfers the progress bar image to the new client.
    // tg: code/datums/progressbar.dm:110
    [SignalHandler]
    public virtual void on_user_login(datum source = null)
    {
        if (user_client != null)
        {
            if (user_client == user.client) //If this was not client handling I'd condemn this sanity check. But clients are fickle things.
                return;
            clean_user_client();
        }
        if (!Dm.truthy(user.client)) //Clients can vanish at any time, the bastards.
            return;
        user_client = user.client;
        add_prog_bar_image_to_client();
    }


    ///Adds a smoothly-appearing progress bar image to the player's screen.
    // tg: code/datums/progressbar.dm:124
    public virtual void add_prog_bar_image_to_client()
    {
        bar.pixel_z = 0;
        bar.alpha = 0;
        Dm.append(user_client.images, bar);
        animate(bar, Dm.named("pixel_z", Dm.add(Dm.add(ICON_SIZE_Y, offset_y), PROGRESSBAR_HEIGHT * (listindex - 1))), Dm.named("alpha", 255), Dm.named("time", PROGRESSBAR_ANIMATION_TIME), Dm.named("easing", SINE_EASING));
    }


    ///Updates the progress bar image visually.
    // tg: code/datums/progressbar.dm:132
    public virtual void update(object progress = null)
    {
        progress = clamp(progress, 0, goal);
        if (Dm.eq(progress, last_progress))
            return;
        last_progress = Dm.num(progress);
        bar.icon_state = Dm.text("prog_bar_[]", round((Dm.div(Dm.num(progress), Dm.num(goal)) * 100), 5));
    }


    ///Called on progress end, be it successful or a failure. Wraps up things to delete the datum and bar.
    // tg: code/datums/progressbar.dm:141
    public virtual void end_progress()
    {
        if (!Dm.eq(last_progress, goal))
            bar.icon_state = Dm.text("[]_fail", bar.icon_state);

        animate(bar, Dm.named("alpha", 0), Dm.named("time", PROGRESSBAR_ANIMATION_TIME));

        // QDEL_IN(src, PROGRESSBAR_ANIMATION_TIME) (__DEFINES/qdel.dm:59), expanded inline (rules 11.1 (f))
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), (PROGRESSBAR_ANIMATION_TIME > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this)), PROGRESSBAR_ANIMATION_TIME, file: "code/datums/progressbar.dm", line: 147);
    }

    ///Progress bars are very generic, and what hangs a ref to them depends heavily on the context in which they're used
    ///So let's make hunting harddels easier yeah?
    // tg: code/datums/progressbar.dm:151
    public override object dump_harddel_info()
    {
        if (Dm.truthy(harddel_deets_dumped))
            return null;
        harddel_deets_dumped = TRUE;
        return Dm.text("Owner's type: []", location_type);
    }
}
