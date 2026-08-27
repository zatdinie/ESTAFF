using System;
using System.Collections.Generic;
using System.Linq;
using EHS_PORTAL.Areas.ESTAFF.Models.Data;

namespace EHS_PORTAL.Areas.ESTAFF.Models.ViewModels
{
    // The admin calendar: every employee's tasks laid out on the day each one
    // is due, in a day, week or month frame.
    //
    // Separate from the employee calendar (Views/Employee/Calendar.cshtml)
    // rather than shared with it, because the two answer different questions.
    // An employee's calendar is a to-do list they drag their own work around
    // in; a manager's is a picture of what a whole workforce is committed to,
    // read across plants and people. Sharing one view would mean one of them
    // carrying controls that make no sense for its reader.
    public class AdminCalendarViewModel
    {
        public const string Daily = "daily";
        public const string Weekly = "weekly";
        public const string Monthly = "monthly";

        // Which frame is on screen. A string, not an enum, because it is a
        // query-string value first and foremost — "?view=weekly" is what the
        // toolbar links post around.
        public string View { get; set; } = Weekly;

        // The day the frame is built around: the day itself, a day in the
        // week, or a day in the month.
        public DateTime TargetDate { get; set; }

        public DateTime StartOfTheMonth { get; set; }
        public DateTime EndOfTheMonth { get; set; }
        public DateTime PrevDate { get; set; }
        public DateTime NextDate { get; set; }

        // One entry per day in the period, empty days included — a day with
        // nothing on it is information too, and the grid has to draw it.
        public List<CalendarDayViewModel> Days { get; set; }
            = new List<CalendarDayViewModel>();

        // The plants present in this period, with their colour and how many
        // tasks each accounts for. Built from what is on screen rather than
        // from the whole plant table, so the legend never lists a colour the
        // reader cannot see.
        public List<CalendarPlantViewModel> Legend { get; set; }
            = new List<CalendarPlantViewModel>();

        // Filters. All optional, all carried through every navigation link so
        // that paging to next week does not silently drop them.
        public int? PlantId { get; set; }
        public string EmployeeId { get; set; }
        public string Status { get; set; }

        public List<Plant> Plants { get; set; } = new List<Plant>();

        public List<EmployeeSelectItem> Employees { get; set; }
            = new List<EmployeeSelectItem>();

        public List<CalendarTaskViewModel> Items
        {
            get { return Days.SelectMany(d => d.Items).ToList(); }
        }

        public int TotalCount { get { return Items.Count; } }

        public int CountOf(TaskStatus status)
        {
            return Items.Count(i => i.Task.Status == status);
        }

        // Overdue is a status of its own here, not a derived "past due and not
        // complete": TaskService.UpdateOverdueTasks has already moved those
        // rows, and counting both ways would double-count them.
        public int OverdueCount { get { return CountOf(TaskStatus.Overdue); } }

        public bool HasFilters
        {
            get
            {
                return PlantId.HasValue
                    || !string.IsNullOrEmpty(EmployeeId)
                    || !string.IsNullOrEmpty(Status);
            }
        }

        // Whether today is somewhere in the frame, which is what decides
        // whether the "Today" button is worth pressing.
        public bool IsCurrentPeriod
        {
            get
            {
                return StartOfTheMonth.Date <= DateTime.Today
                    && DateTime.Today <= EndOfTheMonth.Date;
            }
        }

        public string PeriodLabel
        {
            get
            {
                if (View == Daily)
                    return TargetDate.ToString("dddd, dd MMMM yyyy");

                if (View == Monthly)
                    return TargetDate.ToString("MMMM yyyy");

                // The month is repeated on both halves so a week running
                // across a month end reads correctly: "27 Dec – 02 Jan 2027",
                // not "27 – 02 Jan 2027".
                return StartOfTheMonth.ToString("dd MMM") + " – "
                    + EndOfTheMonth.ToString("dd MMM yyyy");
            }
        }

        // Route values for a calendar link, carrying the filters along. Used
        // by every control in the toolbar, so that none of them can forget
        // one and quietly widen what the reader is looking at.
        public object RouteFor(string view, DateTime date)
        {
            return new
            {
                view,
                date = date.ToString("yyyy-MM-dd"),
                plantId = PlantId,
                employeeId = EmployeeId,
                status = Status
            };
        }

        // The same link with one filter swapped — what the legend chips and
        // the filter selects need.
        public object RouteWithPlant(int? plantId)
        {
            return new
            {
                view = View,
                date = TargetDate.ToString("yyyy-MM-dd"),
                plantId,
                employeeId = EmployeeId,
                status = Status
            };
        }

        public object RouteWithStatus(string status)
        {
            return new
            {
                view = View,
                date = TargetDate.ToString("yyyy-MM-dd"),
                plantId = PlantId,
                employeeId = EmployeeId,
                status
            };
        }
    }

    public class CalendarDayViewModel
    {
        public DateTime Date { get; set; }

        public List<CalendarTaskViewModel> Items { get; set; }
            = new List<CalendarTaskViewModel>();

        // Group of employee with its TaskItems
        public List<CalendarDayEmployeeViewModel> Employees
        {
            get
            {
                return Items
                    .GroupBy(i => i.Task.AssignedToUserId)
                    .Select(g => new CalendarDayEmployeeViewModel
                    {
                        Date = Date,
                        UserId = g.Key,
                        Name = g.First().Task.AssignedToName,
                        EmpID = g.First().Task.AssignedToEmpID,
                        Items = g.ToList()
                    })
                    .OrderByDescending(e => e.HasOverdue)
                    .ThenByDescending(e => e.Count)
                    .ThenBy(e => e.Name)
                    .ToList();
            }
        }

        public bool IsToday { get { return Date.Date == DateTime.Today; } }

        public bool IsWeekend
        {
            get
            {
                return Date.DayOfWeek == DayOfWeek.Saturday
                    || Date.DayOfWeek == DayOfWeek.Sunday;
            }
        }
    }

    // One task on the calendar: the task as the rest of the admin side already
    // describes it, plus where it belongs and when in the day it happens.
    //
    // A wrapper rather than more properties on TaskListItemViewModel: the
    // plant is not a fact about the task at all - TaskItems has no plant
    // column - but about who it is assigned to, and putting it on the task
    // model would leave it silently null everywhere else it is used.
    public class CalendarTaskViewModel
    {
        public TaskListItemViewModel Task { get; set; }

        // The assignee's plant, per CLIP.UserPlants. Null when EHS_PORTAL has
        // no plant for them - which is real and not rare, so the calendar has
        // a colour for it rather than hiding those tasks.
        public int? PlantId { get; set; }
        public string PlantName { get; set; }

        // Every plant the assignee is mapped to, when there is more than one.
        // The colour can only stand for the first; the modal says the rest,
        // rather than the chip quietly picking a side.
        public List<string> OtherPlantNames { get; set; } = new List<string>();

        // Palette slot, from the plant's position in the full plant list — so
        // a plant keeps its colour as the reader pages through weeks, and two
        // plants only share one when there are more plants than colours.
        public int ColorIndex { get; set; }

        public bool IsDaily { get; set; }

        // "08:00 – 17:00" for a daily task, empty for a long-term one.
        public string PeriodText { get; set; }

        // "Daily, 25 Aug 08:00 - 17:00" / "Long term, no period".
        public string ScheduleText { get; set; }

        public string PlantClass
        {
            get
            {
                return PlantId.HasValue
                    ? "plant-" + ColorIndex
                    : "plant-none";
            }
        }

        public string PlantLabel
        {
            get
            {
                return string.IsNullOrWhiteSpace(PlantName)
                    ? "No plant"
                    : PlantName;
            }
        }
    }

    // Item in Admin Calendar View for month and week
    public class CalendarDayEmployeeViewModel
    {
        public DateTime Date { get; set; }
        public string UserId { get; set; }
        public string Name { get; set; }
        public string EmpID { get; set; }
        public List<CalendarTaskViewModel> Items { get; set; } = new List<CalendarTaskViewModel>();
        public int Count { get { return Items.Count; } }
        public int OverdueCount
        {
            get
            {
                return Items.Count(i => i.Task.Status == TaskStatus.Overdue);
            }
        }
        public bool HasOverdue { get { return OverdueCount > 0;  }  }
        public string PlantClass
        {
            get
            {
                var first = Items.FirstOrDefault();
                return first == null ? "plat-none" : first.PlantClass;
            }
        }
        public static string PanelIdFor(DateTime date, string userId)
        {
            return "dayroster-" + date.ToString("yyyyMMdd") + "-" + userId;
        }
        public string PanelId
        {
            get { return PanelIdFor(Date, UserId); }
        }
    }

    public class CalendarPlantViewModel
    {
        public int? PlantId { get; set; }
        public string Name { get; set; }
        public int ColorIndex { get; set; }
        public int Count { get; set; }

        public string PlantClass
        {
            get
            {
                return PlantId.HasValue ? "plant-" + ColorIndex : "plant-none";
            }
        }
    }

    // How a plant becomes a colour.
    //
    // The palette is defined once in admin-calendar.css as .plant-0 … and this
    // decides which slot a plant lands in: its position in the plant list,
    // ordered by name. Position rather than PlantId so the colours spread
    // across the palette instead of clustering wherever the ids happen to sit,
    // and stable as long as no plant is added or renamed — which matters
    // because the reader learns "green is Plant A" across a session.
    public static class CalendarPalette
    {
        public const int Size = 12;

        public static Dictionary<int, int> SlotsFor(IEnumerable<Plant> plants)
        {
            var slots = new Dictionary<int, int>();
            if (plants == null) return slots;

            var ordered = plants
                .OrderBy(p => p.PlantName)
                .ThenBy(p => p.Id)
                .ToList();

            for (var i = 0; i < ordered.Count; i++)
                slots[ordered[i].Id] = i % Size;

            return slots;
        }
    }
}
