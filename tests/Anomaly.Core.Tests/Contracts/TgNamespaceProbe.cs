// Not a translation: a compile probe with the exact header of docs/ai/translation-rules.md 1.2. It must keep compiling
// inside namespace Anomaly.Core.Tg with every import of a translated file (the wave-0 bug: a namespace segment named Dm
// shadowed the class Dm). CompileOnly is never run; RunImplemented runs the parts that exist.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

internal static class TgNamespaceProbe
{
    /// <summary>Every bare (C.1) and qualified (C.2) shim name in a translated-code shape. Never executed.</summary>
    public static async DmTask<object> CompileOnly(object x, DmList L, float f, float? n, string s, Type T)
    {
        object dot = null;

        // C.1 constants and builtins, bare.
        float dirs = NORTH + SOUTH + EAST + WEST + UP + DOWN + NORTHEAST + NORTHWEST + SOUTHEAST + SOUTHWEST + TRUE - FALSE;
        if (prob(50) && Dm.truthy(x))
            dot = rand(1, 6) + rand() + rand(5);
        f = round(f) + round(f, 0.5f) + floor(f) + ceil(Dm.num(n)) + trunc(f) + fract(f) + sign(f) + abs(f) + sqrt(f) + log(f) + log(2, f);
        f += lerp(0, 1, f) + sin(f) + cos(f) + tan(f) + arcsin(f) + arccos(f) + arctan(f) + arctan(f, f);
        f += min(f, 1) + max(f, 2, 3) + clamp(f, 0, 1) + dirs;
        object mixed = min(x, 1);
        mixed = max(L);
        mixed = clamp(x, 0, 1);
        f += length(s) + length_char(L) + findtext(s, "a") + findtextEx(s, "a") + findlasttext(s, "a") + text2ascii(s)
            + sorttext(s, "b") + cmptext(s, "b") + spantext(s, " ") + nonspantext(s, " ");
        f += findtext_char(s, "a") + findtextEx_char(s, "a") + findlasttext_char(s, "a") + text2ascii_char(s, 2)
            + spantext_char(s, " ") + nonspantext_char(s, " ");
        s = copytext(s, 1, 5) + replacetext(s, "a", "b") + replacetextEx(s, "a", "b") + splicetext(s, 1, 2, "x") + uppertext(s)
            + lowertext(s) + trimtext(s) + jointext(L, ",") + ckey(s) + ckeyEx(s) + ascii2text(65) + num2text(f) + num2text(f, 4, 16)
            + html_encode(s) + html_decode(s) + json_encode(L) + list2params(L) + time2text(world.realtime, "hh:mm")
            + rgb(1, 2, 3) + rgb(1, 2, 3, space: COLORSPACE_HSV);
        s += copytext_char(s, 2) + replacetext_char(s, "a", "b") + replacetextEx_char(s, "a", "b") + splicetext_char(s, 1, 2);
        DmList parts = splittext(s, ",");
        parts = splittext_char(s, ",", 1, 0, TRUE);
        parts = params2list(s);
        parts = rgb2num("#ffffff");
        object decoded = json_decode(s);
        float? parsed = text2num(s);
        bool kinds = istype(x, T) && ispath(T) && ispath(T, typeof(DmList)) && isnum(x) && istext(x) && islist(x) && isfile(x) && isicon(x) && hascall(x, "f");
        object narrowed = astype(x, T);
        DmList types = typesof(T);
        Type path = text2path("/datum");
        string reference = @ref(x);
        sound snd = sound("a.ogg", volume: 50);
        matrix M = matrix();
        M = matrix(2, MATRIX_SCALE);
        object img = image("icon.dmi", x, "state");
        object ic = icon("icon.dmi");
        animate(x, Dm.named("alpha", 0), Dm.named("time", 5));
        flick("state", x);
        object picked = pick(1, 2, 3);
        picked = pick(L);
        f += roll("3d6") + roll(3, 6);
        await sleep(world.tick_lag);
        spawn(10, async () => { await sleep(0); });
        object answer = await input(usr, "message");
        answer = await alert(usr, "message");
        world.maxz++;
        f += world.time + world.fps + world.tick_usage + world.cpu + world.maxx + world.maxy + world.timeofday;
        DmList everything = world.contents;
        Dm.output_to(world.log, s);
        Dm.output_to(usr, snd);

        // C.2, qualified.
        dot = Dm.str(x);
        s = Dm.text("[] is []", x, f) + I18n.T("You see [].", x);
        sound made = Dm.@new<sound>("a.ogg");
        object made2 = Dm.@new(T, x, Dm.named("loc", x), Dm.arglist(L));
        made = await Dm.new_async<sound>();
        made2 = await Dm.new_async(T, x);
        object called = await Dm.Call(x, "f", 1, Dm.named("a", 2));
        called = Waiting.NoSleep(Dm.Call(null, nameof(GlobalProcs.usr), Dm.arglist(L)));
        Waiting.Detach(Dm.Call(x, "g"));
        dot = Dm.get_var(x, "v");
        Dm.set_var(x, "v", 1);
        dot = Dm.get_var_or_null(x, "v");
        dot = Dm.initial(x, "v");
        float initialVolume = Dm.initial(snd, (sound p) => p.volume);
        sound found = Dm.locate<sound>(L);
        found = Dm.locate<sound>(world);
        dot = Dm.locate_ref("[0x1]");
        Type parent = Dm.parent_type(T);
        object[] extra = Dm.forward(new object[0], x);
        DmList args = Dm.args(x, extra);
        dot = Dm.arg(extra, 0);
        float? second = Dm.arg<float?>(extra, 1);
        bool compare = Dm.eq(x, 1) && !Dm.eq(n, f) && Dm.equiv(L, L) && Dm.lt(x, s) && Dm.le(x, 1) && Dm.gt(x, 1) && Dm.ge(x, 1)
            && Dm.@in(x, L) && Dm.in_to(f, 1, 5) && Dm.in_to(x, 1, 5);
        dot = Dm.add(x, 1);
        dot = Dm.sub(x, 1);
        dot = Dm.mul(x, 2);
        f = Dm.div(f, f);
        dot = Dm.div(x, 2);
        dot = Dm.neg(x);
        f += Dm.mod(f, 3) + Dm.mod(x, 3) + Dm.fmod(f, 3) + Dm.pow(f, 2) + Dm.cmp(x, 1) + Dm.not(x) + Dm.num(x) + Dm.num(n) + Dm.num(f > 1);
        n = Dm.num_or_null(x);
        Dm.inc(ref dot);
        Dm.dec(ref n);
        f += Dm.postinc(ref dot) + Dm.preinc(ref n) + Dm.postdec(ref dot) + Dm.predec(ref n);
        Dm.append(ref dot, 1);
        Dm.append(ref n, 1);
        Dm.append(ref L, x);
        Dm.append(everything, x);
        Dm.remove(ref dot, 1);
        Dm.remove(ref L, x);
        Dm.remove(everything, x);
        float flags = 0;
        Dm.combine(ref flags, 4);
        Dm.mask(ref flags, Dm.bitnot(4));
        Dm.bitxor_assign(ref flags, 1);
        Dm.combine(ref L, x);
        Dm.mask(ref dot, x);
        Dm.bitxor_assign(ref L, x);
        Dm.combine(everything, x);
        Dm.append_at(L, "k", Dm.list(1));
        Dm.remove_at(L, "k", 1);
        Dm.combine_at(L, 1, 2);
        Dm.mask_at(L, 1, 2);
        Dm.bitxor_assign_at(L, 1, 2);
        Dm.inc_at(L, "k");
        Dm.dec_at(L, "k");
        f += Dm.postinc_at(L, 1) + Dm.preinc_at(L, 1) + Dm.postdec_at(L, 1) + Dm.predec_at(L, 1);
        f += Dm.bitand(flags, 4) + Dm.bitor(flags, 4) + Dm.bitxor(flags, 4) + Dm.shl(flags, 2) + Dm.shr(flags, 2) + Dm.bitnot(x);
        f += Dm.shl(x, 2) + Dm.shr(n, x) + Dm.bitnot(n);                    // rules 4.4: any operand types
        dot = Dm.bitand(x, L);
        dot = Dm.bitor(x, L);
        dot = Dm.bitxor(x, L);
        // Call shapes of the rules' examples with float?, string, class and Type operands (3.5, 4.1-4.4, 6.2).
        bool shapes = Dm.truthy(f) || Dm.truthy(n) || Dm.truthy(s) || Dm.truthy(L) || Dm.truthy(T) || Dm.truthy(usr)
            || Dm.eq(n, 1) || Dm.eq(1, n) || Dm.eq(x, n) || Dm.eq(n, x) || Dm.eq(s, x) || Dm.eq(T, x);
        f += Dm.not(f) + Dm.not(n) + Dm.not(s) + Dm.not(L);
        Dm.combine(ref dot, Waiting.NoSleep(Dm.Call(x, "update_icon", n)));   // waiting-model.md 5.3
        Dm.combine(ref n, 1);
        Dm.mask(ref flags, n);
        Dm.bitxor_assign(ref flags, x);
        if (Dm.truthy(Dm.bitand(n, 4)))
            dot = L[n];                                                        // a float? index is any value
        object delay = null;
        delay ??= world.tick_lag;                                              // waiting-model.md 5.1 prologue
        dot = (x is var or__1 && Dm.truthy(or__1) ? or__1 : 1);                 // rules 4.5
        n = (n is var or__2 && Dm.truthy(or__2) ? or__2 : 1);                   // rules 4.5, a float? operand
        Dm.mask(ref n, Dm.bitnot(x));                                          // waiting-model.md 5.3: updates &= ~...
        n = max(ceil(Dm.num(n)), 1);                                           // rules 4.6
        n = Dm.num(n) * f;                                                     // rules 4.3: *= on a float? target
        n = Dm.div(Dm.num(n), f);
        dot = Dm.mul(dot, x);                                                  // rules 4.3: *= on an object target
        n = Dm.mod(n, 3);                                                      // rules 4.3: %= on a float? target
        spawn(10, async () => { Dm.output_to(world.log, Dm.text("[]", f)); });    // rules 8: a block that never awaits
        f = Dm.initial(typeof(sound), (sound p) => p.volume);
        foreach (sound item in Dm.each<sound>(world))
            dot = item;
        foreach (mob each_mob in Dm.each<mob>())                               // rules 6.5: for(var/mob/M), no in
            dot = each_mob;
        foreach (object item in Dm.each(L))
            dot = item;
        foreach (sound item in Dm.each<sound>(L))
            dot = item;
        foreach (Type item in Dm.each_cast<Type>(L))
            dot = item;
        foreach (object item in Dm.each_as(L, DmAs.mob | DmAs.obj))
            dot = item;
        foreach ((object k, object v) in Dm.pairs(L))
            dot = v;
        foreach ((sound k, object v) in Dm.pairs<sound>(L))
            dot = k;
        DmList built = Dm.list(1, Dm.kv("a", 2));
        built = Dm.alist(Dm.kv(1, "x"));
        built = Dm.list_n(3);
        dot = Dm.pick_weighted((20, "brown"), (5, "blue"), (100, "green"));
        L.Add(x);
        f += L.Remove(x) + L.RemoveAll(x) + L.Find(x) + L.Insert(1, x) + L.len;
        L.Cut();
        DmList copy = L.Copy(1, 0);
        s = L.Join(",");
        L.Swap(1, 2);
        L.Splice(1, 2, x);
        L.len--;
        L["key"] = L[1];
        if (f > 100)
            throw Dm.CRASH("bad", "code/x.dm", 1);
        try
        {
            throw Dm.throw_value(Dm.EXCEPTION("oops", "code/x.dm", 2));
        }
        catch (Exception ex)
        {
            exception e = Dm.exception(ex);
            dot = e.name;
        }
        Dm.NotTranslated("code/x.dm:3", "probe");

        // GlobalProcs accessors.
        mob user = usr;
        usr = user;
        datum_controller_global_vars glob = GLOB__var;
        GLOB__var = glob;
        glob = GLOB;
        Master = null;
        TgState.Current.InitStatics();
        return dot;
    }

    /// <summary>The implemented part of the surface, run by <c>TgNamespaceCompileTests</c>.</summary>
    public static List<string> RunImplemented(World w, mob player, datum_controller_master mc)
    {
        var results = new List<string>();
        using (w.Enter())
        {
            results.Add(Dm.num(NORTH + EAST == NORTHEAST).ToString(System.Globalization.CultureInfo.InvariantCulture));
            results.Add(Dm.truthy(TRUE) && !Dm.truthy(FALSE) ? "truthy ok" : "truthy bad");
            DmList L = Dm.list("a", Dm.kv("b", 2));
            foreach (object item in Dm.each(L))
                results.Add(item as string);
            results.Add(Dm.eq(L["b"], 2) ? "assoc ok" : "assoc bad");
            usr = player;
            results.Add(ReferenceEquals(usr, player) && ReferenceEquals(w.usr, player) ? "usr ok" : "usr bad");
            usr = null;
            Master = mc;
            results.Add(ReferenceEquals(TgState.Current.Master, mc) ? "Master ok" : "Master bad");
            results.Add(GLOB__var == null ? "GLOB null" : "GLOB set");
            results.Add(I18n.T("can't reach!"));
            Dm.output_to(world.log, "logged");
            results.Add(world.time.ToString(System.Globalization.CultureInfo.InvariantCulture));
            try
            {
                throw Dm.CRASH("crash", "code/x.dm", 4);
            }
            catch (Exception ex)
            {
                results.Add((string)Dm.exception(ex).name);
            }
        }
        return results;
    }
}
