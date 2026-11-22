// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/maths.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only power_to_energy (M3 P3: the areas' static power usage, which every machine adds at init) and get_line (M3 P4:
// the random spawners' unblocked lines, spawners/random/_random.dm:146), siunit_isolated and display_energy (M4c slice 2: a power
// cell's description), get_angle and delta_to_angle (M5x slice 1: a directional explosion's arc, throw_item's drift),
// display_power and energy_to_power (M7 slice 1: a cable's power reading, a powernet's shock damage), get_angle_raw (the M7 zaps
// slice: a beam's angle, beam.dm:242), get_perimeter (engineering E10: a large modular shield's ring), get_pixel_angle (syndicate G4c: a falling supply pod), at_least (gaps X5: the alien stomach's jolt), make_tuple (gaps H2: an ice cream's name).

public static partial class GlobalProcs
{
    /// Takes a value, and a threshold it has to at least match
    /// returns the correctly signed value max'd to the threshold
    // tg: code/__HELPERS/maths.dm:230
    public static object at_least(object new_value = null, object threshold = null)
    {
        float sign = Dm.sign(Dm.num(new_value));
        // SIGN will return 0 if the value is 0, so we just go to the positive threshold
        if (!Dm.truthy(sign))
            return threshold;
        if (sign == 1)
            return max(Dm.num(new_value), Dm.num(threshold));
        if (sign == -1)
            return min(Dm.num(new_value), Dm.num(threshold) * -1);
        return null;
    }

    ///Calculate the angle between two movables and the west|east coordinate
    // tg: code/__HELPERS/maths.dm:2  (parameters typed /atom, rules 3.5: tg passes turfs, explosions.dm:637, which DM does not
    // check against the declared /atom/movable)
    public static object get_angle(atom start = null, atom end = null)
    {
        if (!Dm.truthy(start) || !Dm.truthy(end))
            return 0;
        float dy = (ICON_SIZE_Y * end.y + end.pixel_y) - (ICON_SIZE_Y * start.y + start.pixel_y);
        float dx = (ICON_SIZE_X * end.x + end.pixel_x) - (ICON_SIZE_X * start.x + start.pixel_x);
        return delta_to_angle(dx, dy);
    }

    ///for getting the angle when animating something's pixel_x and pixel_y
    // tg: code/__HELPERS/maths.dm:32
    public static float get_pixel_angle(float y, float x)
    {
        if (y == 0)
            return (x >= 0) ? 90 : 270;
        float dot = arctan(x / y);
        if (y < 0)
            dot += 180;
        else if (x < 0)
            dot += 360;
        return dot;
    }

    /// Calculate the angle produced by a pair of x and y deltas
    // tg: code/__HELPERS/maths.dm:10
    public static object delta_to_angle(object x = null, object y = null)
    {
        object dot = null;
        if (!Dm.truthy(y))
            return (Dm.num(x) >= 0) ? 90 : 270;
        dot = arctan(Dm.num(Dm.div(x, y)));
        if (Dm.num(y) < 0)
            dot = Dm.num(dot) + 180;
        else if (Dm.num(x) < 0)
            dot = Dm.num(dot) + 360;
        return dot;
    }

    // tg: code/__HELPERS/maths.dm:20
    public static object get_angle_raw(object start_x = null, object start_y = null, object start_pixel_x = null, object start_pixel_y = null, object end_x = null, object end_y = null, object end_pixel_x = null, object end_pixel_y = null)
    {
        object dot = null;
        float dy = (ICON_SIZE_Y * Dm.num(end_y) + Dm.num(end_pixel_y)) - (ICON_SIZE_Y * Dm.num(start_y) + Dm.num(start_pixel_y));
        float dx = (ICON_SIZE_X * Dm.num(end_x) + Dm.num(end_pixel_x)) - (ICON_SIZE_X * Dm.num(start_x) + Dm.num(start_pixel_x));
        if (!Dm.truthy(dy))
            return (dx >= 0) ? 90 : 270;
        dot = arctan(dx / dy);
        if (dy < 0)
            dot = Dm.num(dot) + 180;
        else if (dx < 0)
            dot = Dm.num(dot) + 360;
        return dot;
    }

    ///Converts watts to joules: the energy a power draws over one tick of the scheduler (SSmachines by default)
    // tg: code/__HELPERS/maths.dm:195
    public static object power_to_energy(object watts = null, datum_controller_subsystem scheduler = null)
    {
        scheduler ??= SSmachines;
        // rules 10.6: tg's Master creates SSmachines before the map; a test harness world may lack it (no scheduler: 0).
        return Dm.div(Dm.mul(watts, Dm.num(scheduler?.wait)), 1 * SECONDS);
    }

    //Returns the list of turfs between two turfs (including both).
    // tg: code/__HELPERS/maths.dm:46
    public static object get_line(atom starting_atom = null, atom ending_atom = null)
    {
        float current_x_step = starting_atom.x;//start at x and y, then add 1 or -1 to these to get every turf from starting_atom to ending_atom
        float current_y_step = starting_atom.y;
        float starting_z = starting_atom.z;

        DmList line = Dm.list(get_turf(starting_atom));//get_turf(atom) is faster than locate(x, y, z)

        float x_distance = ending_atom.x - current_x_step; //x distance
        float y_distance = ending_atom.y - current_y_step;

        float abs_x_distance = abs(x_distance);//Absolute value of x distance
        float abs_y_distance = abs(y_distance);

        float x_distance_sign = sign(x_distance); //Sign of x distance (+ or -)
        float y_distance_sign = sign(y_distance);

        float x = Dm.shr(abs_x_distance, 1); //Counters for steps taken, setting to distance/2
        float y = Dm.shr(abs_y_distance, 1); //Bit-shifting makes me l33t.  It also makes get_line() unnecessarily fast.

        if (abs_x_distance >= abs_y_distance) //x distance is greater than y
        {
            for (float distance_counter = 0, distance_counter__end = abs_x_distance - 1; distance_counter <= distance_counter__end; distance_counter++)//It'll take abs_x_distance steps to get there
            {
                y += abs_y_distance;

                if (y >= abs_x_distance) //Every abs_y_distance steps, step once in y direction
                {
                    y -= abs_x_distance;
                    current_y_step += y_distance_sign;
                }

                current_x_step += x_distance_sign; //Step on in x direction
                Dm.append(ref line, locate(current_x_step, current_y_step, starting_z));//Add the turf to the list
            }
        }
        else
        {
            for (float distance_counter = 0, distance_counter__end = abs_y_distance - 1; distance_counter <= distance_counter__end; distance_counter++)
            {
                x += abs_x_distance;

                if (x >= abs_y_distance)
                {
                    x -= abs_y_distance;
                    current_x_step += x_distance_sign;
                }

                current_y_step += y_distance_sign;
                Dm.append(ref line, locate(current_x_step, current_y_step, starting_z));
            }
        }
        return line;
    }

    /**
     * Get a list of turfs in a perimeter given the `center_atom` and `radius`.
     * Automatically rounds down decimals and does not accept values less than positive 1 as they don't play well with it.
     * Is efficient on large circles but ugly on small ones
     * Uses [Jesko`s method to the midpoint circle Algorithm](https://en.wikipedia.org/wiki/Midpoint_circle_algorithm).
     */
    // tg: code/__HELPERS/maths.dm:93
    public static object get_perimeter(atom center = null, object radius = null)
    {
        if (Dm.num(radius) < 1)
            return null;
        float rounded_radius = round(Dm.num(radius));
        float x = center.x;
        float y = center.y;
        float z = center.z;
        float t1 = rounded_radius / 16;
        float dx = rounded_radius;
        float dy = 0;
        float t2;
        DmList perimeter = Dm.list();
        while (dx >= dy)
        {
            Dm.append(ref perimeter, locate(x + dx, y + dy, z));
            Dm.append(ref perimeter, locate(x - dx, y + dy, z));
            Dm.append(ref perimeter, locate(x + dx, y - dy, z));
            Dm.append(ref perimeter, locate(x - dx, y - dy, z));
            Dm.append(ref perimeter, locate(x + dy, y + dx, z));
            Dm.append(ref perimeter, locate(x - dy, y + dx, z));
            Dm.append(ref perimeter, locate(x + dy, y - dx, z));
            Dm.append(ref perimeter, locate(x - dy, y - dx, z));
            dy += 1;
            t1 += dy;
            t2 = t1 - dx;
            if (t2 > 0)
            {
                t1 = t2;
                dx -= 1;
            }
        }
        return perimeter;
    }

    /// Formats a number into a list representing the si unit.
    /// Access the coefficient with [SI_COEFFICIENT], and access the unit with [SI_UNIT].
    // tg: code/__HELPERS/maths.dm:133
    public static object siunit_isolated(object value = null, object unit = null, float? maxdecimals = null)
    {
        maxdecimals ??= 1;
        object dot = null;
        DmList prefixes = siunit_isolated__prefixes;

        // We don't have prefixes beyond this point
        // and this also captures value = 0 which you can't compute the logarithm for
        // and also byond numbers are floats and doesn't have much precision beyond this point anyway
        if (abs(Dm.num(value)) < 1e-30f)
        {
            dot = Dm.list(Dm.kv(SI_COEFFICIENT, 0), Dm.kv(SI_UNIT, Dm.text(" []", unit)));
            return dot;
        }

        float exponent = clamp(log(10, abs(Dm.num(value))), -30, 30); // Calculate the exponent and clamp it so we don't go outside the prefix list bounds
        float divider = Dm.pow(10, round(exponent / 3) * 3); // Rounds the exponent to nearest SI unit and power it back to the full form
        float coefficient = round(Dm.num(value) / divider, Dm.pow(10, -maxdecimals.Value)); // Calculate the coefficient and round it to desired decimals
        float prefix_index = round(exponent / 3) + 11; // Calculate the index in the prefixes list for this exponent

        // An edge case which happens if we round 999.9 to 0 decimals for example, which gets rounded to 1000
        // In that case, we manually swap up to the next prefix if there is one available
        if (coefficient >= 1000 && prefix_index < 21)
        {
            coefficient /= 1e3f;
            prefix_index++;
        }

        object prefix = prefixes[prefix_index];
        dot = Dm.list(Dm.kv(SI_COEFFICIENT, coefficient), Dm.kv(SI_UNIT, Dm.text(" [][]", prefix, unit)));
        return dot;
    }

    // tg: code/__HELPERS/maths.dm:134  var/static/list/prefixes (a proc-level static, rules 10.6)
    private static DmList siunit_isolated__prefixes => TgState.Current.GlobalProcs__siunit_isolated__prefixes;

    /**
     * Format an energy value in prefixed joules.
     * Arguments
     *
     * * units - the value t convert
     */
    /**Format a power value in prefixed watts.
     * Converts from energy if convert is true.
     * Args:
     * - power: The value of power to format.
     * - convert: Whether to convert this from joules.
     * - datum/controller/subsystem/scheduler: used in the conversion
     * Returns: The string containing the formatted power.
     */
    // tg: code/__HELPERS/maths.dm:165
    public static object display_power(object power = null, float? convert = null, datum_controller_subsystem scheduler = null)
    {
        convert ??= TRUE;
        scheduler ??= SSmachines;
        power = Dm.truthy(convert) ? energy_to_power(power, scheduler) : power;
        return siunit(power, "W", 3);
    }

    // tg: code/__HELPERS/maths.dm:175
    public static object display_energy(object units = null)
    {
        return siunit(units, "J", 3);
    }

    /**
     * Converts the joule to the watt, assuming SSmachines tick rate.
     * Arguments
     *
     * * joules - the value in joules to convert
     * * datum/controller/subsystem/scheduler - the subsystem whos wait time is used in the conversion
     */
    // tg: code/__HELPERS/maths.dm:185
    public static object energy_to_power(object joules = null, datum_controller_subsystem scheduler = null)
    {
        scheduler ??= SSmachines;
        return Dm.div(Dm.mul(joules, 1 * SECONDS), scheduler.wait);
    }
}

public sealed partial class TgState
{
    // tg: code/__HELPERS/maths.dm:134  var/static/list/prefixes = list("q","r","y","z","a","f","p","n","μ","m","","k","M","G","T","P","E","Z","Y","R","Q")
    internal DmList GlobalProcs__siunit_isolated__prefixes = Dm.list("q", "r", "y", "z", "a", "f", "p", "n", "μ", "m", "", "k", "M", "G", "T", "P", "E", "Z", "Y", "R", "Q");
}

// tg: code/__HELPERS/maths.dm:209 (gaps H2)
public static partial class GlobalProcs
{
    /// Returns the name of the mathematical tuple of same length as the number arg (rounded down).
    // tg: code/__HELPERS/maths.dm:209
    public static object make_tuple(object number = null)
    {
        DmList units_prefix = make_tuple__units_prefix;
        DmList tens_prefix = make_tuple__tens_prefix;
        DmList one_to_nine = make_tuple__one_to_nine;
        float number__1 = round(Dm.num(number));
        float sw__1 = number__1;
        if (sw__1 == 0)
            return "empty tuple";
        else if (sw__1 >= 1 && sw__1 <= 9)
            return one_to_nine[number__1];
        else if (sw__1 >= 10 && sw__1 <= 19)
            return Dm.text("[]decuple", units_prefix[Dm.num(Dm.mod(number__1, 10)) + 1]);
        else if (sw__1 >= 20 && sw__1 <= 99)
            return Dm.text("[][]tuple", units_prefix[Dm.num(Dm.mod(number__1, 10)) + 1], tens_prefix[round(Dm.num(Dm.mod(number__1, 100)) / 10) + 1]);
        else if (sw__1 == 100)
            return "centuple";
        else //It gets too tedious to use latin prefixes from here.
            return Dm.text("[]-tuple", number__1);
    }

    // tg: code/__HELPERS/maths.dm:210-212  make_tuple's var/static lists (rules 10.6)
    private static DmList make_tuple__units_prefix => TgState.Current.GlobalProcs__make_tuple__units_prefix ??= Dm.list("", "un", "duo", "tre", "quattuor", "quin", "sex", "septen", "octo", "novem");
    private static DmList make_tuple__tens_prefix => TgState.Current.GlobalProcs__make_tuple__tens_prefix ??= Dm.list("", "decem", "vigin", "trigin", "quadragin", "quinquagin", "sexagin", "septuagin", "octogin", "nongen");
    private static DmList make_tuple__one_to_nine => TgState.Current.GlobalProcs__make_tuple__one_to_nine ??= Dm.list("monuple", "double", "triple", "quadruple", "quintuple", "sextuple", "septuple", "octuple", "nonuple");
}

public sealed partial class TgState
{
    // tg: code/__HELPERS/maths.dm:210-212  make_tuple's var/static lists
    internal DmList GlobalProcs__make_tuple__units_prefix;
    internal DmList GlobalProcs__make_tuple__tens_prefix;
    internal DmList GlobalProcs__make_tuple__one_to_nine;
}
