// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/maths.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only TICK_DELTA_TO_MS, TICK_USAGE_TO_MS, REALTIMEOFDAY, MIDNIGHT_ROLLOVER_CHECK, CEILING, ROUND_UP, FLOOR,
// WRAP_UP, WRAP_UID, MODULUS, SIMPLIFY_DEGREES and GET_ANGLE_OF_INCIDENCE translated (the MC, SStimer, SSgarbage, SSatoms
// and client/Move use them); the rest of maths.dm is completed by the package that translates it. M4b slice 2 (the gas
// pumps' pressure solver, gas_mixture.dm:670): IS_FINITE__UNSAFE, IS_FINITE and SolveQuadratic. M4b slice 3 (the gas
// reactions and the hotspots' colours): INVERSE and LERP. M5x slice 1 (explosions): ATAN2 and CHEAP_HYPOTENUSE. M9 slice D (telecomms): GET_CARDINAL_DIST.
// M2 P1 (the living core) added the members of its paths, marked "M2 P1" below. The M7 projectiles slice: SIGNED_FLOOR_DIVISION, RANDOM_DECIMAL and
// closer_angle_difference (the points a projectile moves with, its homing and ricochet aim). The M7 supermatter slice: CLAMP01
// (the crystal's radiation, supermatter_extra_effects.dm:25). Syndicate N1c (the navigation computer): ISINRANGE_EX (a camera eye's step
// off the map's edge) and get_overlap (the docking ports a landing spot overlaps). Gaps P3 (the DNA console's enzyme pulse): gaussian.
// Gaps RD3 (the experiments): PERCENT (a reagent scan's purity).

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/maths.dm:188
    public static object get_turf_in_angle(float angle = 0, turf starting = null, float increments = 0)
    {
        float pixel_x = 0;
        float pixel_y = 0;
        for (int i = 1; i <= increments; i++)
        {
            pixel_x += sin(angle) + (ICON_SIZE_X / 2) * sin(angle) * 2;
            pixel_y += cos(angle) + (ICON_SIZE_Y / 2) * cos(angle) * 2;
        }
        float new_x = starting.x;
        float new_y = starting.y;
        while (pixel_x > (ICON_SIZE_X / 2))
        {
            pixel_x -= ICON_SIZE_X;
            new_x++;
        }
        while (pixel_x < -(ICON_SIZE_X / 2))
        {
            pixel_x += ICON_SIZE_X;
            new_x--;
        }
        while (pixel_y > (ICON_SIZE_Y / 2))
        {
            pixel_y -= ICON_SIZE_Y;
            new_y++;
        }
        while (pixel_y < -(ICON_SIZE_Y / 2))
        {
            pixel_y += ICON_SIZE_Y;
            new_y--;
        }
        new_x = clamp(new_x, 1, world.maxx);
        new_y = clamp(new_y, 1, world.maxy);
        return locate(new_x, new_y, starting.z);
    }

    // tg: code/__DEFINES/maths.dm:1  #define IS_FINITE__UNSAFE(a) (!isinf(a) && !isnan(a))
    public static bool IS_FINITE__UNSAFE(object a) => (!Dm.truthy(isinf(a)) && !Dm.truthy(isnan(a)));

    // tg: code/__DEFINES/maths.dm:2  #define IS_FINITE(a) (isnum(a) && IS_FINITE__UNSAFE(a))
    public static bool IS_FINITE(object a) => (isnum(a) && IS_FINITE__UNSAFE(a));

    // tg: code/__DEFINES/maths.dm:22  #define CLAMP01(x) (clamp(x, 0, 1))
    public static float CLAMP01(float x) => (clamp(x, 0, 1));

    // The quadratic formula. Returns a list with the solutions, or an empty list
    // if they are imaginary.
    // tg: code/__DEFINES/maths.dm:114
    public static object SolveQuadratic(object a = null, object b = null, object c = null)
    {
        // ASSERT(a) (__DEFINES/assert.dm:4), expanded inline (rules 11.1)
        if (!Dm.truthy(a))
            throw Dm.CRASH(assertion_message("code/__DEFINES/maths.dm", 115, "a"), "code/__DEFINES/maths.dm", 115);
        DmList dot = Dm.list();
        float d = Dm.num(b) * Dm.num(b) - 4 * Dm.num(a) * Dm.num(c);
        float bottom = 2 * Dm.num(a);
        if (d < 0 || !IS_FINITE__UNSAFE(d) || !IS_FINITE__UNSAFE(bottom))
            return dot;
        float root = sqrt(d);
        Dm.append(ref dot, (-Dm.num(b) + root) / bottom);
        if (!Dm.truthy(d))
            return dot;
        Dm.append(ref dot, (-Dm.num(b) - root) / bottom);
        return dot;
    }

    //"fancy" math for calculating time in ms from tick_usage percentage and the length of ticks
    //percent_of_tick_used * (ticklag * 100(to convert to ms)) / 100(percent ratio)
    //collapsed to percent_of_tick_used * tick_lag
    // tg: code/__DEFINES/maths.dm:18
    public static float TICK_DELTA_TO_MS(float percent_of_tick_used) => (percent_of_tick_used) * world.tick_lag;
    // tg: code/__DEFINES/maths.dm:19  (TICK_USAGE_REAL is world.tick_usage, virtual and always 0, DV3)
    public static float TICK_USAGE_TO_MS(float starting_tickusage) => (TICK_DELTA_TO_MS(TICK_USAGE_REAL() - starting_tickusage));

    //time of day but automatically adjusts to the server going into the next day within the same round.
    //for when you need a reliable time number that doesn't depend on byond time.
    // tg: code/__DEFINES/maths.dm:26  (world.timeofday derives from world.time, DV3)
    public static float REALTIMEOFDAY() => (world.timeofday + (MIDNIGHT_ROLLOVER * MIDNIGHT_ROLLOVER_CHECK()));
    // tg: code/__DEFINES/maths.dm:27
    public static float MIDNIGHT_ROLLOVER_CHECK() => (GLOB.rollovercheck_last_timeofday != world.timeofday ? update_midnight_rollover() : GLOB.midnight_rollovers);

    // tg: code/__DEFINES/maths.dm:32
    public static float CEILING(float x, float y) => (-round(Dm.div(-(x), (y))) * (y));

    // tg: code/__DEFINES/maths.dm:34
    public static float ROUND_UP(float x) => (-round(-(x)));

    // round() acts like floor(x, 1) by default but can't handle other values
    // tg: code/__DEFINES/maths.dm:45
    public static float FLOOR(float x, float y) => (round(Dm.div((x), (y))) * (y));

    // Similar to clamp but the bottom rolls around to the top and vice versa. min is inclusive, max is exclusive
    // tg: code/__DEFINES/maths.dm:48
    public static float WRAP(float val, float min, float max) => Dm.clamp((min == max ? min : (val) - (round(((val) - (min)) / ((max) - (min))) * ((max) - (min)))), min, max);

    /// Increments a value and wraps it if it exceeds some value. Can be used to circularly iterate through a list through `idx = WRAP_UP(idx, length_of_list)`.
    // tg: code/__DEFINES/maths.dm:51
    public static float WRAP_UP(float val, float max) => (Dm.mod((val), (max)) + 1);

    /// Helper that increments and wraps the passed in number when it hits the integer limit
    // tg: code/__DEFINES/maths.dm:54
    public static float WRAP_UID(float val) => WRAP_UP(val, SHORT_REAL_LIMIT - 1);

    // Real modulus that handles decimals, now just a wrapper for BYOND's %% operator
    // tg: code/__DEFINES/maths.dm:57
    public static float MODULUS(float x, float y) => (Dm.fmod((x), (y)));

    // tg: code/__DEFINES/maths.dm:77  #define INVERSE(x) ( 1/(x) )
    public static float INVERSE(float x) => (Dm.div(1, (x)));

    // Linear interpolation
    // tg: code/__DEFINES/maths.dm:101  #define LERP(a, b, amount) ( amount ? ((a) + ((b) - (a)) * (amount)) : a )
    public static float LERP(float a, float b, float amount) => (amount != 0 ? ((a) + ((b) - (a)) * (amount)) : a);

    // Will filter out extra rotations and negative rotations
    // E.g: 540 becomes 180. -180 becomes 180.
    // tg: code/__DEFINES/maths.dm:137
    public static float SIMPLIFY_DEGREES(float degrees) => (MODULUS((degrees), 360));

    // Engineering E12b (the HFR's toroidal size, hfr_main_processes.dm:102):
    // tg: code/__DEFINES/maths.dm:129  #define TORADIANS(degrees) ((degrees) * 0.0174532925)
    public static float TORADIANS(float degrees) => ((degrees) * 0.0174532925f);

    // tg: code/__DEFINES/maths.dm:142
    public static float GET_ANGLE_OF_INCIDENCE(float face, float input) => (MODULUS((face) - (input), 360));

    // The M7 projectiles slice (a shot robotic limb's sparks, living_defense.dm:228):
    // tg: code/__DEFINES/maths.dm:266  #define RANDOM_DECIMAL(x, y) LERP((x), (y), rand())
    public static float RANDOM_DECIMAL(float x, float y) => LERP((x), (y), rand());

    // The M7 projectiles slice (position_point_vector.dm):
    // tg: code/__DEFINES/maths.dm:30  #define SIGNED_FLOOR_DIVISION(x, y) (sign(x) * floor(abs(x) / y))
    //This will return +/- decimals, situated about mean with standard deviation stddev
    //68% chance that the number is within 1stddev
    //95% chance that the number is within 2stddev
    //98% chance that the number is within 3stddev...etc
    // tg: code/__DEFINES/maths.dm:169  #define ACCURACY 10000 (file-local, #undef at :186)
    private const float gaussian__ACCURACY = 10000;

    // tg: code/__DEFINES/maths.dm:171  var/static/gaussian_next (a proc-level static, rules 10.6)
    private static ref object gaussian__gaussian_next => ref TgState.Current.GlobalProcs__gaussian__gaussian_next;

    // tg: code/__DEFINES/maths.dm:170
    public static float gaussian(float mean, float stddev)
    {
        float R1;
        float R2;
        float working;
        if (gaussian__gaussian_next != null)
        {
            R1 = Dm.num(gaussian__gaussian_next);
            gaussian__gaussian_next = null;
        }
        else
        {
            do
            {
                R1 = rand(-gaussian__ACCURACY, gaussian__ACCURACY) / gaussian__ACCURACY;
                R2 = rand(-gaussian__ACCURACY, gaussian__ACCURACY) / gaussian__ACCURACY;
                working = R1 * R1 + R2 * R2;
            }
            while (working >= 1 || working == 0);
            working = sqrt(-2 * log(working) / working);
            R1 *= working;
            gaussian__gaussian_next = R2 * working;
        }
        return mean + stddev * R1;
    }

    public static float SIGNED_FLOOR_DIVISION(float x, float y) => (sign(x) * floor(abs(x) / y));

    //Finds the shortest angle that angle A has to change to get to angle B. Aka, whether to move clock or counterclockwise.
    // tg: code/__DEFINES/maths.dm:145
    public static object closer_angle_difference(object a = null, object b = null)
    {
        object dot = null;
        if (!isnum(a) || !isnum(b))
            return dot;
        a = SIMPLIFY_DEGREES(Dm.num(a));
        b = SIMPLIFY_DEGREES(Dm.num(b));
        float inc = Dm.num(b) - Dm.num(a);
        if (inc < 0)
            inc += 360;
        float dec = Dm.num(a) - Dm.num(b);
        if (dec < 0)
            dec += 360;
        dot = (inc > dec ? -dec : inc);
        return dot;
    }

    // M5x slice 1 (explosions: get_ranged_target_turf_direct, propagate_blastwave):
    // tg: code/__DEFINES/maths.dm:68  #define ATAN2(x, y) ( !(x) && !(y) ? 0 : (y) >= 0 ? arccos((x) / sqrt((x)*(x) + (y)*(y))) : -arccos((x) / sqrt((x)*(x) + (y)*(y))) )
    public static float ATAN2(float x, float y) => (!Dm.truthy(x) && !Dm.truthy(y) ? 0 : (y) >= 0 ? arccos((x) / sqrt((x) * (x) + (y) * (y))) : -arccos((x) / sqrt((x) * (x) + (y) * (y))));

    // tg: code/__DEFINES/maths.dm:260  #define CHEAP_HYPOTENUSE(Ax, Ay, Bx, By) (sqrt((Ax - Bx) ** 2 + (Ay - By) ** 2)) //A squared + B squared = C squared
    public static float CHEAP_HYPOTENUSE(float Ax, float Ay, float Bx, float By) => (sqrt(Dm.num(Dm.pow((Ax - Bx), 2)) + Dm.num(Dm.pow((Ay - By), 2)))); //A squared + B squared = C squared

    // M2 P1 (living core):
    // tg: code/__DEFINES/maths.dm:244  #define SPT_PROB_RATE(prob_per_second, seconds_per_tick) (1 - (1 - (prob_per_second)) ** (seconds_per_tick))
    public static float SPT_PROB_RATE(float prob_per_second, object seconds_per_tick) => (1 - Dm.num(Dm.pow((1 - (prob_per_second)), (seconds_per_tick))));

    // tg: code/__DEFINES/maths.dm:247  #define SPT_PROB(prob_per_second_percent, seconds_per_tick) (prob(100*SPT_PROB_RATE((prob_per_second_percent)/100, (seconds_per_tick))))
    public static bool SPT_PROB(float prob_per_second_percent, object seconds_per_tick) => (prob(100 * SPT_PROB_RATE((prob_per_second_percent) / 100, (seconds_per_tick))));

    // tg: code/__DEFINES/maths.dm:256  #define GET_CARDINAL_DIST(a, b) ((a == null || b == null) ? -1 : max(abs(a.x -b.x), abs(a.y-b.y))) (M9 slice D: the telecomms autolink)
    // tg: code/__DEFINES/maths.dm:71
    public static float Gcd(float a, float b) => b != 0 ? Gcd(b, a % b) : a;

    // tg: code/__DEFINES/maths.dm:263
    public static float DIAMOND_AREA(float X) => (1 + 2 * (X) * ((X) + 1));

    public static float GET_CARDINAL_DIST(atom a, atom b) => (a == null || b == null) ? -1 : Dm.max(Dm.abs(a.x - b.x), Dm.abs(a.y - b.y));

    // This value per these many units. Very unnecessary but helpful for readability (For example wanting 30 units of synthflesh to heal 50 damage - VALUE_PER(50, 30))
    // tg: code/__DEFINES/maths.dm:251  #define VALUE_PER(value, per) (value / per) (gaps RG1: the salt's and the salt water's burn wound sanitizing)
    public static float VALUE_PER(float value, float per) => (value / per);

    // tg: code/__DEFINES/maths.dm:21  #define PERCENT(val) (round((val)*100, 0.1))
    public static float PERCENT(float val) => round((val) * 100, 0.1f);

    // tg: code/__DEFINES/maths.dm:89  #define ISINRANGE(val, min, max) (min <= val && val <= max)
    public static bool ISINRANGE(float val, float min, float max) => min <= val && val <= max;

    // tg: code/__DEFINES/maths.dm:92  #define ISINRANGE_EX(val, min, max) (min < val && val < max)
    public static bool ISINRANGE_EX(float val, float min, float max) => min < val && val < max;

    // tg: code/__DEFINES/maths.dm:213
    public static DmList get_overlap(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4)
    {
        DmList region_x1 = Dm.list();
        DmList region_y1 = Dm.list();
        DmList region_x2 = Dm.list();
        DmList region_y2 = Dm.list();

        // These loops create loops filled with x/y values that the boundaries inhabit
        // ex: list(5, 6, 7, 8, 9)
        for (float i = Dm.min(x1, x2); i <= Dm.max(x1, x2); i++)
            region_x1[Dm.text("[]", i)] = TRUE;
        for (float i = Dm.min(y1, y2); i <= Dm.max(y1, y2); i++)
            region_y1[Dm.text("[]", i)] = TRUE;
        for (float i = Dm.min(x3, x4); i <= Dm.max(x3, x4); i++)
            region_x2[Dm.text("[]", i)] = TRUE;
        for (float i = Dm.min(y3, y4); i <= Dm.max(y3, y4); i++)
            region_y2[Dm.text("[]", i)] = TRUE;

        return Dm.list(Dm.bitand(region_x1, region_x2), Dm.bitand(region_y1, region_y2));
    }
}

public sealed partial class TgState
{
    // tg: code/__DEFINES/maths.dm:171  var/static/gaussian_next
    internal object GlobalProcs__gaussian__gaussian_next;
}
