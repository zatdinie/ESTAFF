using System;
using System.Collections.Generic;
using EHS_PORTAL.Areas.ESTAFF.Models.Data;

namespace EHS_PORTAL.Areas.ESTAFF.Models.ViewModels
{
    // The scheduling fields every task form carries, so one partial can render
    // them and one set of rules can check them.
    //
    // An interface rather than a projection class with a From() per form model:
    // the three form view models already declare these exact properties, so
    // there is nothing to reshape. _PeriodField takes this interface and each
    // view passes its own model straight through.
    public interface ITaskPeriodFields
    {
        TaskScheduleType ScheduleType { get; set; }

        // What the form posted in its Due Date box. A daily task is not asked
        // for one - it is due on the day it is worked - so read the due date
        // through TaskPeriod.EffectiveDueDate rather than from here.
        DateTime DueDate { get; set; }

        DateTime? PeriodDate { get; set; }
        TimeSpan? PeriodStart { get; set; }
        TimeSpan? PeriodEnd { get; set; }
    }

    // One hour in the period dropdowns.
    public class HourOption
    {
        // What posts. A full TimeSpan so the default model binder parses it
        // without a custom binder.
        public string Value { get; set; }

        // What the user reads.
        public string Text { get; set; }
    }

    // The rules about a task's period, in one place because the employee and
    // admin forms have to agree about them. The controllers report what these
    // return; they do not decide anything themselves.
    public static class TaskPeriod
    {
        public static readonly TimeSpan DefaultStart = new TimeSpan(8, 0, 0);
        public static readonly TimeSpan DefaultEnd = new TimeSpan(17, 0, 0);

        // Whole hours only, 00:00 through 23:00.
        public static List<HourOption> HourOptions()
        {
            var hours = new List<HourOption>();

            for (var h = 0; h < 24; h++)
            {
                var hour = new TimeSpan(h, 0, 0);

                hours.Add(new HourOption
                {
                    Value = hour.ToString(@"hh\:mm\:ss"),
                    Text = hour.ToString(@"hh\:mm")
                });
            }

            return hours;
        }

        // The option value matching a stored time, or null when there is none.
        //
        // A time that is not on the hour - written before the picker was
        // narrowed to whole hours, or by hand - matches no option, and the
        // dropdown would silently fall back to the first one. Truncating it to
        // its own hour keeps the form showing something close to the truth.
        public static string HourValue(TimeSpan? time)
        {
            if (!time.HasValue) return null;

            return new TimeSpan(time.Value.Hours, 0, 0).ToString(@"hh\:mm\:ss");
        }

        // Whether the period is filled in at all. A period is a day and both
        // hours together; anything less is not one.
        public static bool HasPeriod(ITaskPeriodFields fields)
        {
            return fields.PeriodDate.HasValue
                && fields.PeriodStart.HasValue
                && fields.PeriodEnd.HasValue;
        }

        // What is wrong with the posted period, as field name -> message.
        //
        // This cannot be data annotations: whether the period is required
        // depends on ScheduleType, and [Required] only ever sees one property
        // at a time. Empty means the post is acceptable.
        //
        // Nothing checks that PeriodEnd is after PeriodStart. An end earlier
        // than the start is a night shift (22:00 to 06:00), not a mistake.
        public static Dictionary<string, string> Validate(
            ITaskPeriodFields fields)
        {
            var errors = new Dictionary<string, string>();

            if (fields.ScheduleType == TaskScheduleType.Daily)
            {
                // A daily task is defined by the day and hours it was worked,
                // so it has to have them.
                if (!fields.PeriodDate.HasValue)
                    errors["PeriodDate"] =
                        "A daily task needs the date the work is done on.";

                if (!fields.PeriodStart.HasValue)
                    errors["PeriodStart"] =
                        "A daily task needs the hour the work starts.";

                if (!fields.PeriodEnd.HasValue)
                    errors["PeriodEnd"] =
                        "A daily task needs the hour the work ends.";

                return errors;
            }

            // Long-term: there is no period to check. The form does not offer
            // one, and ApplyTo clears anything that posts anyway, so a stale
            // half-filled period is not the user's problem to fix.
            return errors;
        }

        // The date the task is actually due.
        //
        // A daily task is due on the day it is worked, so its period date is
        // its due date and the form never asks for a second one. A long-term
        // task is due when the form said it was.
        public static DateTime EffectiveDueDate(ITaskPeriodFields fields)
        {
            if (fields.ScheduleType == TaskScheduleType.Daily
                && fields.PeriodDate.HasValue)
                return fields.PeriodDate.Value.Date;

            return fields.DueDate;
        }

        // Copies the posted schedule onto the task - the type, the period and
        // the due date, because the two kinds of task answer that last one
        // differently and only this method knows which kind it is holding.
        //
        // A long-term task has its period cleared rather than left as it was.
        // That matters on the edit forms: a task switched from daily to long
        // term would otherwise keep hours it no longer has, and the form is no
        // longer showing them to be cleared by hand.
        public static void ApplyTo(TaskItem task, ITaskPeriodFields fields)
        {
            task.ScheduleType = fields.ScheduleType;
            task.DueDate = EffectiveDueDate(fields);

            // HasPeriod is belt and braces: Validate has already refused a
            // daily task without a complete period.
            if (fields.ScheduleType != TaskScheduleType.Daily
                || !HasPeriod(fields))
            {
                task.PeriodDate = null;
                task.PeriodStart = null;
                task.PeriodEnd = null;
                return;
            }

            task.PeriodDate = fields.PeriodDate.Value.Date;
            task.PeriodStart = fields.PeriodStart;
            task.PeriodEnd = fields.PeriodEnd;
        }

        // How a task's schedule reads in the audit trail.
        public static string Describe(TaskItem task)
        {
            return Describe(task.ScheduleType, task.PeriodDate,
                task.PeriodStart, task.PeriodEnd);
        }

        public static string Describe(ITaskPeriodFields fields)
        {
            return Describe(fields.ScheduleType, fields.PeriodDate,
                fields.PeriodStart, fields.PeriodEnd);
        }

        // "Daily, 25 Aug 08:00 - 17:00" / "Long term, no period".
        private static string Describe(TaskScheduleType type, DateTime? date,
            TimeSpan? start, TimeSpan? end)
        {
            var kind = type == TaskScheduleType.Daily ? "Daily" : "Long term";

            if (!date.HasValue || !start.HasValue || !end.HasValue)
                return kind + ", no period";

            var text = string.Format("{0}, {1:dd MMM} {2:hh\\:mm} - {3:hh\\:mm}",
                kind, date.Value, start.Value, end.Value);

            // Worth saying outright: the pair reads backwards otherwise, and a
            // reader cannot tell a night shift from a slip.
            return end.Value < start.Value ? text + " (overnight)" : text;
        }
    }
}
