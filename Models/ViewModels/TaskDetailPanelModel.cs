using System.Collections.Generic;

namespace ESTAFF.Models.ViewModels
{
    // What the task detail panel needs to draw itself, on either calendar.
    //
    // The panel is rendered by Razor into a hidden block per item and lifted
    // into a modal on click, so both calendars show the same rows built from
    // the same partials, rather than each assembling the detail in JavaScript
    // where the task's own text would have to be escaped by hand.
    //
    // The two callers differ in what context they can supply. The manager's
    // calendar reads across people and plants, so it names both. The
    // employee's is one person's own work by definition: an "Assigned To" row
    // would repeat the same name on every task, and the plant would be the
    // same on all of them, so neither is worth a row. Absent values omit their
    // row entirely instead of printing an empty one.
    public class TaskDetailPanelModel
    {
        public TaskListItemViewModel Task { get; set; }

        // DOM id of the hidden block. The item that opens it carries the same
        // string in data-detail.
        public string PanelId
        {
            get { return "taskdetail-" + Task.TaskId; }
        }

        // The plant's palette class, which colours the stripe down the left of
        // the panel. Null on the employee calendar, where there is no plant
        // dimension and the stripe falls back to the app's primary colour.
        public string PaletteClass { get; set; }

        public bool ShowAssignedTo { get; set; }

        // Null omits the plant row.
        public string PlantLabel { get; set; }

        // The plants beyond the one whose colour the stripe carries. Said
        // outright because a stripe can only stand for one of them.
        public List<string> OtherPlantNames { get; set; } = new List<string>();

        // "Daily, 08:00 - 17:00" and the like, from TaskPeriod.Describe.
        // Null omits the row.
        public string ScheduleText { get; set; }

        // For panel navigation purpose on Day and Month view in calendar
        public string BackPanelId { get; set; }

        // Where "Open task" goes: Admin/EditTask or Employee/EditTask.
        public string OpenUrl { get; set; }

        // The manager's calendar, which knows the plant and the assignee.
        public static TaskDetailPanelModel ForCalendarItem(
            CalendarTaskViewModel item, string openUrl, bool withBack = false)
        {
            return new TaskDetailPanelModel
            {
                Task            = item.Task,
                PaletteClass    = item.PlantClass,
                ShowAssignedTo  = true,
                PlantLabel      = item.PlantLabel,
                OtherPlantNames = item.OtherPlantNames,
                ScheduleText    = item.ScheduleText,
                OpenUrl         = openUrl,
                BackPanelId = withBack
                    ? CalendarDayEmployeeViewModel.PanelIdFor(item.Task.DueDate, item.Task.AssignedToUserId)
                    : null
            };
        }

        // The employee's own calendar: no assignee row, no plant row.
        public static TaskDetailPanelModel ForOwnTask(
            TaskListItemViewModel task, string scheduleText, string openUrl)
        {
            return new TaskDetailPanelModel
            {
                Task           = task,
                ShowAssignedTo = false,
                ScheduleText   = scheduleText,
                OpenUrl        = openUrl
            };
        }
    }
}
