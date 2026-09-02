using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using EHS_PORTAL.Areas.ESTAFF.Models.Data;

namespace EHS_PORTAL.Areas.ESTAFF.Models.ViewModels
{
    public class GenerateReportViewModel
    {
        // The plant this return covers. A report is one per plant per period,
        // so this is the first thing it needs - the tasks follow from it.
        [Required(ErrorMessage = "Select the plant this report covers")]
        [Display(Name = "Plant")]
        public int? PlantId { get; set; }

        // Every plant, not only the generator's. CLIP.UserPlants is
        // EHS_PORTAL's incomplete record of who works where, and restricting
        // the list to it would leave an unmapped employee unable to generate
        // anything at all.
        public List<Plant> Plants { get; set; } = new List<Plant>();

        [Required(ErrorMessage = "Report type is required")]
        [Display(Name = "Report Type")]
        public ReportType ReportType { get; set; }

        [Required(ErrorMessage = "Period start is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Period Start")]
        public DateTime PeriodStart { get; set; } = DateTime.Today
            .AddDays(-(int)DateTime.Today.DayOfWeek + 1);

        [Required(ErrorMessage = "Period end is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Period End")]
        public DateTime PeriodEnd { get; set; } = DateTime.Today;

        // Preview tasks
        public List<TaskItem> Tasks { get; set; }
            = new List<TaskItem>();

        // The same tasks with their lookups, linked CLIP record and action
        // history resolved, so the preview can show the employee exactly the
        // breakdown the submitted report will carry.
        public List<ReportTaskDetailViewModel> TaskDetails { get; set; }
            = new List<ReportTaskDetailViewModel>();
    }

    public class ReportListItemViewModel
    {
        public int ReportId { get; set; }

        // Null on reports submitted before reports covered a plant. Those are
        // legacy personal returns and say so rather than claiming a plant.
        public int? PlantId { get; set; }
        public string PlantName { get; set; }

        public string PlantLabel => string.IsNullOrWhiteSpace(PlantName)
            ? (PlantId.HasValue ? "Plant #" + PlantId.Value : "Personal (legacy)")
            : PlantName;

        public string EmpName { get; set; }
        public string EmpNumber { get; set; }
        public ReportType ReportType { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public ReportStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string RejectionReason { get; set; }
    }

    public class ReportDetailViewModel
    {
        public int ReportId { get; set; }

        // The plant the return covers. Null on a legacy personal report, in
        // which case the printed letterhead falls back to the Esh:Plant
        // setting as it always did.
        public int? PlantId { get; set; }
        public string PlantName { get; set; }

        public string EmpName { get; set; }
        public string EmpNumber { get; set; }
        public string EmpEmail { get; set; }
        public string EmpPosition { get; set; }
        public string EmpJkkpNo { get; set; }
        public ReportType ReportType { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public ReportStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public string DecidedByName { get; set; }
        public string DecidedByPosition { get; set; }
        public string DecidedByJkkpNo { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string RejectionReason { get; set; }
        public List<TaskItem> Tasks { get; set; }
            = new List<TaskItem>();

        // The same tasks with their lookups, linked CLIP record and action
        // history resolved. The on-screen views read Tasks; the PDF reads this,
        // because a printed report is read away from the system and has to
        // stand on its own.
        public List<ReportTaskDetailViewModel> TaskDetails { get; set; }
            = new List<ReportTaskDetailViewModel>();

        // Stats
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int PendingTasks { get; set; }
        public int OverdueTasks { get; set; }
        public decimal CompletionRate { get; set; }

        // The enum names are lower case, which is not how a report should
        // introduce itself.
        public string ReportTypeLabel =>
            ReportType == ReportType.monthly ? "Monthly" : "Weekly";

        // A quotable identifier for the printed copy.
        public string Reference => "RPT-" + ReportId.ToString("D5");

        public string PeriodText =>
            PeriodStart.ToString("dd MMM yyyy") + " to " +
            PeriodEnd.ToString("dd MMM yyyy");

        public int PeriodDays =>
            (int)(PeriodEnd.Date - PeriodStart.Date).TotalDays + 1;
    }

    // Everything the printed report says about one task: its own fields, the
    // classification it belongs to, the CLIP record it covers, and every action
    // taken on it. Built by the controllers so the employee and admin downloads
    // cannot drift apart.
    public class ReportTaskDetailViewModel
    {
        public int TaskId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }

        public string ClassificationName { get; set; }
        public string TaskListName { get; set; }

        // Which part of the statutory ESH report this task prints under,
        // inherited from its classification. Null where the classification has
        // never been mapped; the printed report files those under
        // EshSections.Default rather than leaving them out of the return.
        public EshSection? ReportSection { get; set; }

        public TaskStatus Status { get; set; }
        public TaskPriority? Priority { get; set; }

        public DateTime CreatedDate { get; set; }
        public DateTime AssignedDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }

        public string AssignedToName { get; set; }
        public string AssignedByName { get; set; }

        // Set only for CLIP-classified tasks that are linked to a record.
        public ClipItemViewModel ClipItem { get; set; }

        // Status changes with the action described at each, oldest first.
        public List<StatusRemarkViewModel> Actions { get; set; }
            = new List<StatusRemarkViewModel>();

        public string StatusLabel => TaskDisplay.StatusLabel(Status);

        public string PriorityLabel =>
            Priority.HasValue ? Priority.Value.ToString() : "Not set";

        public string ClassificationLabel =>
            string.IsNullOrWhiteSpace(ClassificationName)
                ? "Unclassified"
                : ClassificationName;

        public bool IsOverdue => Status != TaskStatus.Complete
            && DueDate.Date < DateTime.Today;

        // The section this task is actually printed under, with an unmapped
        // classification resolved to the catch-all.
        public EshSection EffectiveSection =>
            EshSections.Sanitise(ReportSection) ?? EshSections.Default;

        // The "Action Taken" cell of the statutory grid. The form asks for one
        // description of what was done; the task holds a remark per status
        // change, so they are printed in the order they happened - dropping all
        // but the last would lose the part of the record an audit reads.
        //
        // Steps moved without a note contribute nothing: a row saying only
        // "no action described" is worse than a shorter list.
        public string ActionTakenText
        {
            get
            {
                if (Actions == null) return null;

                var described = Actions
                    .Where(a => a.HasRemark)
                    .Select(a => a.Remark.Trim())
                    .ToList();

                return described.Any()
                    ? string.Join("\n", described)
                    : null;
            }
        }

        // The date the statutory grid prints against a task: when the work was
        // actually done, falling back to when it was due for anything still
        // open. Both are what the officer filling the form in by hand writes.
        public DateTime EffectiveDate => CompletedDate ?? DueDate;

        // How the task landed against its due date - the line a manager reads
        // first when scanning for slippage.
        public string DeliveryText
        {
            get
            {
                if (CompletedDate.HasValue)
                {
                    var days = (int)(CompletedDate.Value.Date
                                     - DueDate.Date).TotalDays;

                    if (days > 0) return "Completed " + Plural(days, "day")
                                       + " after the due date";
                    if (days == 0) return "Completed on the due date";
                    return "Completed " + Plural(-days, "day") + " early";
                }

                var remaining = (int)(DueDate.Date
                                      - DateTime.Today).TotalDays;

                if (remaining < 0)
                    return Plural(-remaining, "day") + " overdue";
                if (remaining == 0) return "Due today";
                return Plural(remaining, "day") + " remaining";
            }
        }

        private static string Plural(int count, string noun)
        {
            return count + " " + noun + (count == 1 ? "" : "s");
        }

        public static ReportTaskDetailViewModel From(TaskItem task,
            ClipItemViewModel clipItem,
            List<StatusRemarkViewModel> actions)
        {
            return new ReportTaskDetailViewModel
            {
                TaskId             = task.TaskId,
                Title              = task.Title,
                Description        = task.Description,
                ClassificationName = task.TaskClassification?.Name,
                TaskListName       = task.TaskList?.Name,
                ReportSection      = EshSections.Sanitise(
                                         task.TaskClassification?.ReportSection),
                Status             = task.Status,
                Priority           = task.Priority,
                CreatedDate        = task.CreatedDate,
                AssignedDate       = task.AssignedDate,
                DueDate            = task.DueDate,
                CompletedDate      = task.CompletedDate,
                LastModifiedDate   = task.LastModifiedDate,
                AssignedToName     = task.AssignedToUser?.UserName ?? "-",
                AssignedByName     = task.CreatedByUser?.UserName ?? "-",
                ClipItem           = clipItem,
                Actions            = actions ?? new List<StatusRemarkViewModel>()
            };
        }
    }

    public class ApproveReportViewModel
    {
        public int ReportId { get; set; }

        [Display(Name = "Rejection Reason")]
        public string RejectionReason { get; set; }
    }
}