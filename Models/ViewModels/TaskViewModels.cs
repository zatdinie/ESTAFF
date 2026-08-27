using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using EHS_PORTAL.Areas.ESTAFF.Models.Data;
using Newtonsoft.Json;

namespace EHS_PORTAL.Areas.ESTAFF.Models.ViewModels
{
    public class AssignTaskViewModel : ITaskPeriodFields
    {
        [Required(ErrorMessage = "Title is required")]
        [StringLength(256)]
        [Display(Name = "Task Title")]
        public string Title { get; set; }

        [Display(Name = "Concern/Issue")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Please assign to an employee")]
        [Display(Name = "Assign To")]
        public string AssignedToUserId { get; set; }

        // Asked for by long-term tasks only. A daily task hides this field
        // and takes its due date from PeriodDate, so the controllers read
        // TaskPeriod.EffectiveDueDate rather than this property directly.
        [Required(ErrorMessage = "Due date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Due Date")]
        public DateTime DueDate { get; set; } = DateTime.Today.AddDays(7);

        // ITaskPeriodFields. The two kinds of task are scheduled in
        // different terms and the form asks for one or the other, never both:
        // daily work happens on one named day between two hours, and is due
        // that day; long-term work is tracked to the due date above and
        // carries no period.
        //
        // No [Required] on the period: whether it is needed depends on
        // ScheduleType, which an annotation cannot see. TaskPeriod.Validate
        // holds that rule, and the controllers report what it returns.
        [Display(Name = "Task Type")]
        public TaskScheduleType ScheduleType { get; set; }
            = TaskScheduleType.LongTerm;

        [DataType(DataType.Date)]
        [Display(Name = "Period Date")]
        public DateTime? PeriodDate { get; set; }

        [Display(Name = "Period Start")]
        public TimeSpan? PeriodStart { get; set; }

        [Display(Name = "Period End")]
        public TimeSpan? PeriodEnd { get; set; }

        [Display(Name = "Priority")]
        public TaskPriority? Priority { get; set; }

        [Required(ErrorMessage = "Please select a task classification")]
        [Display(Name = "Task Classification")]
        public int TaskClassificationId { get; set; }

        // Nullable on the model but required in the controller, which is the
        // only place that can check it belongs to the chosen classification.
        [Display(Name = "Task")]
        public int? TaskListId { get; set; }

        // Optional. Posted by the CLIP picker as "COF:14" / "PM:3", empty when
        // the task covers no CLIP record - which is most of them.
        [Display(Name = "CLIP Item")]
        public string ClipItemKey { get; set; }

        public TaskFormOptions Options { get; set; } = new TaskFormOptions();

        // Dropdown List of Employees
        public List<EmployeeSelectItem> Employees { get; set; } 
            = new List<EmployeeSelectItem>();
    }

    public class EditTaskViewModel : ITaskPeriodFields
    {
        public int TaskId { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(256)]
        [Display(Name = "Task Title")]
        public string Title { get; set; }

        [Display(Name = "Concern/Issue")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Please assign to an employee")]
        [Display(Name = "Assign To")]
        public string AssignedToUserId { get; set; }

        // Asked for by long-term tasks only. A daily task hides this field
        // and takes its due date from PeriodDate, so the controllers read
        // TaskPeriod.EffectiveDueDate rather than this property directly.
        [Required(ErrorMessage = "Due date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Due Date")]
        public DateTime DueDate { get; set; }

        // ITaskPeriodFields. The two kinds of task are scheduled in
        // different terms and the form asks for one or the other, never both:
        // daily work happens on one named day between two hours, and is due
        // that day; long-term work is tracked to the due date above and
        // carries no period.
        //
        // No [Required] on the period: whether it is needed depends on
        // ScheduleType, which an annotation cannot see. TaskPeriod.Validate
        // holds that rule, and the controllers report what it returns.
        [Display(Name = "Task Type")]
        public TaskScheduleType ScheduleType { get; set; }
            = TaskScheduleType.LongTerm;

        [DataType(DataType.Date)]
        [Display(Name = "Period Date")]
        public DateTime? PeriodDate { get; set; }

        [Display(Name = "Period Start")]
        public TimeSpan? PeriodStart { get; set; }

        [Display(Name = "Period End")]
        public TimeSpan? PeriodEnd { get; set; }

        [Display(Name = "Priority")]
        public TaskPriority? Priority { get; set; }

        [Display(Name = "Status")]
        public TaskStatus Status { get; set; }

        [Display(Name = "Task Classification")]
        public int TaskClassificationId { get; set; }

        [Display(Name = "Task")]
        public int? TaskListId { get; set; }

        [Display(Name = "CLIP Item")]
        public string ClipItemKey { get; set; }

        // Only recorded when the status actually changes on save.
        [StringLength(500)]
        [Display(Name = "Status Remark")]
        public string StatusRemark { get; set; }

        public TaskFormOptions Options { get; set; } = new TaskFormOptions();

        // Dropdown List
        public List<EmployeeSelectItem> Employees { get; set; }
            = new List<EmployeeSelectItem>();
    }

    public class TaskListItemViewModel
    {
        public int TaskId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int? SubTaskId { get; set; }
        public string TaskClassificationName { get; set; }
        public TaskStatus Status { get; set; }
        public TaskPriority? Priority { get; set; }
        public DateTime DueDate { get; set; }
        public TimeSpan? PeriodStart { get; set; }
        public TimeSpan? PeriodEnd { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime AssignedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string AssignedToUserId { get; set; }
        public string AssignedToName { get; set; }
        public string AssignedToEmpID { get; set; }
        public string CreatedByName { get; set; }

        // Who raised the task. Ownership decides whether the employee may
        // delete it, and that has to be judged on the id — comparing display
        // names gets it wrong the moment two people share a user name or one
        // of them is renamed.
        public string CreatedByUserId { get; set; }

        public int TaskClassificationId { get; set; }
        public string ClassificationName { get; set; }
        public int? TaskListId { get; set; }
        public string TaskListName { get; set; }

        // The attached CLIP record, when the task carries one. Independent of
        // classification - any task may have one.
        public ClipItemViewModel ClipItem { get; set; }

        // Newest status transition, shown inline on the task.
        public StatusRemarkViewModel LatestStatusRemark { get; set; }

        // Every status transition on the task, oldest first — rendered as the
        // action flow in the task table.
        public List<StatusRemarkViewModel> StatusActions { get; set; }
            = new List<StatusRemarkViewModel>();

        public bool IsOverdue => Status != TaskStatus.Complete
            && DueDate.Date < DateTime.Today;

        // CSS modifier suffix for the status badge / dot (badge-pending, ...).
        public string StatusClass => TaskDisplay.StatusClass(Status);

        public string StatusLabel => TaskDisplay.StatusLabel(Status);

        // The due date in the terms the person actually reads it in — "3 days
        // left", "due tomorrow", "5 days overdue". A date alone makes the
        // reader do the arithmetic before they can judge urgency.
        public string DueText
        {
            get
            {
                if (Status == TaskStatus.Complete)
                {
                    return CompletedDate.HasValue
                        ? "Completed " + CompletedDate.Value.ToString("dd MMM")
                        : "Completed";
                }

                var days = (int)(DueDate.Date - DateTime.Today).TotalDays;

                if (days < 0) return Plural(-days, "day") + " overdue";
                if (days == 0) return "Due today";
                if (days == 1) return "Due tomorrow";
                return Plural(days, "day") + " left";
            }
        }

        // Urgency band the card border and due line are keyed on. Colour is
        // never the only carrier — DueText says the same thing in words.
        public string DueClass
        {
            get
            {
                if (Status == TaskStatus.Complete) return "done";
                var days = (int)(DueDate.Date - DateTime.Today).TotalDays;
                if (days < 0) return "overdue";
                if (days <= 2) return "soon";
                return "later";
            }
        }

        private static string Plural(int count, string noun)
        {
            return count + " " + noun + (count == 1 ? "" : "s");
        }

        public string ClassificationSlug =>
            TaskDisplay.ClassificationSlug(ClassificationName);

        public string ClassificationIcon =>
            TaskDisplay.ClassificationIcon(ClassificationName);

        public string ClassificationLabel =>
            string.IsNullOrWhiteSpace(ClassificationName)
                ? "Unclassified"
                : ClassificationName;
    }

    public class TaskHistoryItemViewModel
    {
        public int HistoryId { get; set; }
        public int TaskId { get; set; }
        public string TaskTitle { get; set; }
        public string Action { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string Remark { get; set; }
        public string ChangedByName { get; set; }
        public DateTime ChangedDate { get; set; }
    }

    public class EmployeeSelectItem
    {
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string EmpID { get; set; }
        public string Display => $"{FullName} ({EmpID})";
    }


    // Employee Side. Carries the same classification/task-type/CLIP trio as the
    // admin forms so both routes can render _ClassificationField.
    public class CreateTaskViewModel : ITaskPeriodFields
    {
        // Optional: blank assigns the task to whoever is creating it. Only
        // employees sharing a plant with the creator may be chosen, which the
        // controller re-checks on post.
        [Display(Name = "Assign To")]
        public string AssignedToUserId { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(256)]
        [Display(Name = "Task Title")]
        public string Title { get; set; }

        [Display(Name = "Concern/Issue")]
        public string Description { get; set; }

        // Asked for by long-term tasks only. A daily task hides this field
        // and takes its due date from PeriodDate, so the controllers read
        // TaskPeriod.EffectiveDueDate rather than this property directly.
        [Required(ErrorMessage = "Due date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Due Date")]
        public DateTime DueDate { get; set; } = DateTime.Today.AddDays(1);

        // ITaskPeriodFields. The two kinds of task are scheduled in
        // different terms and the form asks for one or the other, never both:
        // daily work happens on one named day between two hours, and is due
        // that day; long-term work is tracked to the due date above and
        // carries no period.
        //
        // No [Required] on the period: whether it is needed depends on
        // ScheduleType, which an annotation cannot see. TaskPeriod.Validate
        // holds that rule, and the controllers report what it returns.
        [Display(Name = "Task Type")]
        public TaskScheduleType ScheduleType { get; set; }
            = TaskScheduleType.LongTerm;

        [DataType(DataType.Date)]
        [Display(Name = "Period Date")]
        public DateTime? PeriodDate { get; set; }

        [Display(Name = "Period Start")]
        public TimeSpan? PeriodStart { get; set; }

        [Display(Name = "Period End")]
        public TimeSpan? PeriodEnd { get; set; }

        [Required(ErrorMessage = "Please select a task classification")]
        [Display(Name = "Task Classification")]
        public int TaskClassificationId { get; set; }

        // Nullable for the same reason as AssignTaskViewModel.TaskListId.
        [Display(Name = "Task")]
        public int? TaskListId { get; set; }

        // Optional. Posted by the CLIP picker as "COF:14" / "PM:3".
        [Display(Name = "CLIP Item")]
        public string ClipItemKey { get; set; }

        [Display(Name = "Priority")]
        public TaskPriority? Priority { get; set; }

        public TaskFormOptions Options { get; set; } = new TaskFormOptions();
        
        // Dropdown List
        public List<EmployeeSelectItem> Employees { get; set; }
            = new List<EmployeeSelectItem>();
    }

    public class UpdateTaskStatusViewModel
    {
        public int TaskId { get; set; }
        public TaskStatus Status { get; set; }

        [StringLength(500)]
        public string Remark { get; set; }
    }

    // A single status transition, with whatever note the user attached to it.
    // Populated by TaskService.GetLatestStatusRemark(s).
    public class StatusRemarkViewModel
    {
        public int TaskId { get; set; }
        public TaskStatus? FromStatus { get; set; }
        public TaskStatus? ToStatus { get; set; }
        public string Remark { get; set; }
        public string ChangedByName { get; set; }
        public DateTime ChangedDate { get; set; }

        // Shown wherever a step of the flow carries no note of its own.
        public const string NoActionText = "No action described";

        public bool HasRemark => !string.IsNullOrWhiteSpace(Remark);

        // What was done at this step. The remark is the action the person
        // described when they moved the task; without one there is nothing to
        // show but the transition itself.
        public string ActionText => HasRemark ? Remark : NoActionText;

        // Badge modifier for the status this step moved the task into.
        public string StatusClass => ToStatus.HasValue
            ? TaskDisplay.StatusClass(ToStatus.Value)
            : "draft";

        public string FullTimestamp =>
            ChangedDate.ToString("dd MMM yyyy, h:mm tt");

        // "Pending -> In Progress", or just the new status on the first entry.
        public string TransitionText
        {
            get
            {
                var to = ToStatus.HasValue
                    ? TaskDisplay.StatusLabel(ToStatus.Value)
                    : "-";

                return FromStatus.HasValue
                    ? TaskDisplay.StatusLabel(FromStatus.Value) + " → " + to
                    : to;
            }
        }

        // "just now" / "3h ago" / "12 Jul 2026"
        public string RelativeTime
        {
            get
            {
                var span = DateTime.Now - ChangedDate;

                if (span.TotalSeconds < 60) return "just now";
                if (span.TotalMinutes < 60)
                    return (int)span.TotalMinutes + "m ago";
                if (span.TotalHours < 24)
                    return (int)span.TotalHours + "h ago";
                if (span.TotalDays < 7)
                    return (int)span.TotalDays + "d ago";

                return ChangedDate.ToString("dd MMM yyyy");
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // Admin-side task form support.
    //
    // The employee pages drive classification through the cascading
    // ViewBag SelectLists in EmployeeController (PopulateTaskClassification /
    // PopulateTaskList / GetTaskByClassification). The admin pages use these
    // strongly-typed options instead. Worth unifying on one approach later -
    // until then both are live and neither depends on the other.
    // ══════════════════════════════════════════════════════════════

    public class TaskFormOptions
    {
        public List<ClassificationOption> Classifications { get; set; }
            = new List<ClassificationOption>();

        // All task types across every classification. The form filters them
        // client-side as the classification changes.
        public List<TaskListOption> TaskLists { get; set; }
            = new List<TaskListOption>();

        // CLIP records for the relevant employee's plants, nearest expiry first.
        // Offered on every task regardless of classification - attaching one is
        // optional and always available.
        public List<ClipItemViewModel> ClipItems { get; set; }
            = new List<ClipItemViewModel>();
    }

    public class ClassificationOption
    {
        public int TaskClassificationId { get; set; }
        public string Name { get; set; }

        public string Slug => TaskDisplay.ClassificationSlug(Name);
        public string Icon => TaskDisplay.ClassificationIcon(Name);
    }

    public class TaskListOption
    {
        public int TaskListId { get; set; }
        public int TaskClassificationId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    // What the _ClassificationField partial needs, so the admin task forms can
    // render the same block from their own view models.
    public class TaskFormFieldsViewModel
    {
        public int? TaskClassificationId { get; set; }
        public int? TaskListId { get; set; }
        public string ClipItemKey { get; set; }

        public TaskFormOptions Options { get; set; } = new TaskFormOptions();

        // Set on the admin forms, where the CLIP list depends on the assignee.
        public string ClipReloadUrl { get; set; }
        public string ClipReloadTriggerId { get; set; }

        public string ClipHint { get; set; } =
            "Optional. Attach the certificate of fitness or plant monitoring " +
            "record this task covers, and the report will carry its plant, " +
            "expiry and progress alongside the task. Expired items are " +
            "listed first.";

        public static TaskFormFieldsViewModel From(AssignTaskViewModel m)
        {
            return new TaskFormFieldsViewModel
            {
                TaskClassificationId = m.TaskClassificationId,
                TaskListId = m.TaskListId,
                ClipItemKey = m.ClipItemKey,
                Options = m.Options
            };
        }

        public static TaskFormFieldsViewModel From(EditTaskViewModel m)
        {
            return new TaskFormFieldsViewModel
            {
                TaskClassificationId = m.TaskClassificationId,
                TaskListId = m.TaskListId,
                ClipItemKey = m.ClipItemKey,
                Options = m.Options
            };
        }

        // Employee forms need no reload wiring: the assignee is always the
        // signed-in user, so the CLIP list is final at render time.
        public static TaskFormFieldsViewModel From(CreateTaskViewModel m)
        {
            return new TaskFormFieldsViewModel
            {
                TaskClassificationId = m.TaskClassificationId,
                TaskListId = m.TaskListId,
                ClipItemKey = m.ClipItemKey,
                Options = m.Options,
                ClipHint =
                    "Certificates of fitness and plant monitoring records for "
                    + "your plants. Expired items are listed first."
            };
        }
    }

    // What _ClassificationBadge needs. A small carrier type so the partial can
    // be fed from a TaskItem or a TaskListItemViewModel alike.
    public class ClassificationBadgeModel
    {
        public string Name { get; set; }
        public string TaskListName { get; set; }

        // Dense layouts (table cells, week chips) hide the sub-type.
        public bool ShowTaskList { get; set; } = true;

        public static ClassificationBadgeModel From(
            TaskListItemViewModel task, bool showTaskList = true)
        {
            return new ClassificationBadgeModel
            {
                Name = task.ClassificationName,
                TaskListName = task.TaskListName,
                ShowTaskList = showTaskList
            };
        }

        public static ClassificationBadgeModel From(
            TaskItem task, bool showTaskList = true)
        {
            return new ClassificationBadgeModel
            {
                Name = task.TaskClassification?.Name,
                TaskListName = task.TaskList?.Name,
                ShowTaskList = showTaskList
            };
        }
    }

    // Presentation rules shared by every view that renders a task, so badge
    // classes and labels cannot drift between the admin and employee pages.
    public static class TaskDisplay
    {
        public static string StatusClass(TaskStatus status)
        {
            return status == TaskStatus.InProgress
                ? "inprogress"
                : status.ToString().ToLower();
        }

        public static string StatusLabel(TaskStatus status)
        {
            return status == TaskStatus.InProgress
                ? "In Progress"
                : status.ToString();
        }

        // Classifications are rows, not an enum, so the badge colour is keyed on
        // a slug derived from the name: "DOSH / BOMBA / DOE" -> "dosh-bomba-doe".
        // A classification added later simply falls back to the neutral style.
        public static string ClassificationSlug(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "unknown";

            var slug = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-");
            return slug.Trim('-');
        }

        public static string ClassificationIcon(string name)
        {
            switch (ClassificationSlug(name))
            {
                case "clip":            return "fa-shield-halved";
                case "compliance-activity-with-safety-and-health-related-regulation":  return "fa-clipboard-check";
                case "methods-of-establishing-and-maintaining-a-safe-and-healthy-workplace": return "fa-people-arrows";
                case "safety-and-health-statistics":  return "fa-helmet-safety";
                case "machinery-plant-equipment-process-that-can-lead-to-injuries": return "fa-gears";
                case "machinery-plant-equipment-ppe-required-for-minimizing-risk":   return "fa-vest";
                case "layout-changes-in-the-premises":   return "fa-compass-drafting";
                case "safety-and-health-training-promotions-activities-and-inspection":   return "fa-people-group";
                case "matters-arising":   return "fa-circle-exclamation";
                case "feedback-and-communication":   return "fa-people-arrows";
                default:                return "fa-tag";
            }
        }

        public static List<ClassificationOption> ToOptions(
            IEnumerable<TaskClassification> classifications)
        {
            return classifications
                .Select(c => new ClassificationOption
                {
                    TaskClassificationId = c.TaskClassificationId,
                    Name = c.Name
                })
                .ToList();
        }

        public static List<TaskListOption> ToOptions(IEnumerable<TaskList> lists)
        {
            return lists
                .Select(l => new TaskListOption
                {
                    TaskListId = l.TaskListId,
                    TaskClassificationId = l.TaskClassificationId,
                    Name = l.Name,
                    Description = l.Description
                })
                .ToList();
        }
    }
}