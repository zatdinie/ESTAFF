using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text.RegularExpressions;
using ESTAFF.Models.Data;
using ESTAFF.Models.ViewModels;

namespace ESTAFF.Services
{
    public class TaskService
    {
        // TaskHistory.Action value used for every status transition. Status
        // changes are logged under their own action so the newest remark for a
        // task can be found without parsing free text.
        public const string StatusChangedAction = "StatusChanged";

        private readonly ApplicationDbContext _db;

        public TaskService(ApplicationDbContext db)
        {
            _db = db;
        }

        public List<COF> GetCOF(int plantId)
        {
            return _db.COFs
                .Where(c => c.PlantId == plantId)
                .ToList();
        }

        public List<COF> GetCOFsForPlants(IEnumerable<int> plantIds)
        {
            return _db.COFs
                .Where(c => plantIds.Contains(c.PlantId))
                .ToList();
        }

        public List<PlantMonitoring> GetPlantMonitoringList(int plantId)
        {
            return _db.PlantMonitoring
                .Where(m => m.PlantID == plantId)
                .ToList();
        }

        public List<COF> GetCOFList(int plantId)
        {
            return _db.COFs
                .Where(c => c.PlantId == plantId)
                .ToList();
        }

        public List<TaskClassification> GetTaskClassification()
        {
            return _db.TaskClassifications.ToList();
        }

        public List<TaskList> GetTaskList(int classificationId)
        {
            return _db.TaskLists.Where(t => t.TaskClassificationId == classificationId).ToList();
        }

        // Auto-flag overdue tasks
        public void UpdateOverdueTasks()
        {
            var today = DateTime.Today;

            var overdueTasks = _db.TaskItems
                .Where(t => t.Status != TaskStatus.Complete
                         && t.Status != TaskStatus.Overdue
                         && DbFunctions.TruncateTime(t.DueDate) < today)
                .ToList();

            if (!overdueTasks.Any()) return;

            foreach (var task in overdueTasks)
            {
                var oldStatus = task.Status.ToString();
                task.Status           = TaskStatus.Overdue;
                task.LastModifiedDate = DateTime.Now;

                _db.TaskHistories.Add(new TaskHistory
                {
                    TaskId          = task.TaskId,
                    Action          = StatusChangedAction,
                    OldValue        = oldStatus,
                    NewValue        = TaskStatus.Overdue.ToString(),
                    Remark          = "Automatically flagged overdue - the due date passed.",
                    ChangedByUserId = task.CreatedByUserId,
                    ChangedDate     = DateTime.Now
                });
            }

            _db.SaveChanges();
        }

        // Log task history
        public void LogHistory(int taskId, string action,
            string oldValue, string newValue, string changedByUserId,
            string remark = null)
        {
            _db.TaskHistories.Add(new TaskHistory
            {
                TaskId          = taskId,
                Action          = action,
                OldValue        = oldValue,
                NewValue        = newValue,
                Remark          = TrimRemark(remark),
                ChangedByUserId = changedByUserId,
                ChangedDate     = DateTime.Now
            });

            _db.SaveChanges();
        }

        // Log a status transition with the optional remark the user supplied.
        public void LogStatusChange(int taskId, TaskStatus oldStatus,
            TaskStatus newStatus, string changedByUserId, string remark)
        {
            LogHistory(taskId, StatusChangedAction,
                oldStatus.ToString(), newStatus.ToString(),
                changedByUserId, remark);
        }

        // ══════════════════════════════════════════
        // LATEST STATUS REMARK
        // ══════════════════════════════════════════

        public StatusRemarkViewModel GetLatestStatusRemark(int taskId)
        {
            var entry = _db.TaskHistories
                .Include(h => h.ChangedByUser)
                .Where(h => h.TaskId == taskId
                         && h.Action == StatusChangedAction)
                .OrderByDescending(h => h.ChangedDate)
                .ThenByDescending(h => h.HistoryId)
                .FirstOrDefault();

            return MapRemark(entry);
        }

        // ══════════════════════════════════════════
        // ACTION FLOW
        // ══════════════════════════════════════════

        // Every status change on a task, oldest first, so a view can render the
        // actions taken as a flow rather than only the newest one.
        public List<StatusRemarkViewModel> GetStatusActionFlow(int taskId)
        {
            return _db.TaskHistories
                .Include(h => h.ChangedByUser)
                .Where(h => h.TaskId == taskId
                         && h.Action == StatusChangedAction)
                .ToList()
                .OrderBy(h => h.ChangedDate)
                .ThenBy(h => h.HistoryId)
                .Select(MapRemark)
                .ToList();
        }

        // Same, for a page's worth of tasks in one query.
        public Dictionary<int, List<StatusRemarkViewModel>> GetStatusActionFlows(
            IEnumerable<int> taskIds)
        {
            var ids = (taskIds ?? Enumerable.Empty<int>()).Distinct().ToList();
            if (!ids.Any())
                return new Dictionary<int, List<StatusRemarkViewModel>>();

            return _db.TaskHistories
                .Include(h => h.ChangedByUser)
                .Where(h => ids.Contains(h.TaskId)
                         && h.Action == StatusChangedAction)
                .ToList()
                .GroupBy(h => h.TaskId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(h => h.ChangedDate)
                          .ThenBy(h => h.HistoryId)
                          .Select(MapRemark)
                          .ToList());
        }

        // ══════════════════════════════════════════
        // REPORT DETAIL
        // ══════════════════════════════════════════

        // The tasks one report covers: every task belonging to the plant whose
        // due date falls inside the period, with the lookups every report
        // surface prints already joined.
        //
        // Both the employee's copy and the admin's review read a report through
        // here. They used to run this query themselves and disagreed about
        // which date decided membership - the employee's pages filtered on
        // DueDate, the admin's on CreatedDate - so an approver could be reading
        // a different set of tasks than the employee submitted, and the two
        // PDFs of one report could differ.
        //
        // DueDate is the one that decides. The form prints CompletedDate ??
        // DueDate as each task's date, so a task raised in January but due in
        // March belongs to March's return, not January's.
        //
        // A report used to cover one employee's tasks. It now covers a plant's,
        // because that is what the statutory return is: one per plant per
        // month, not one per officer.
        public List<TaskItem> GetTasksForReportPeriod(
            int plantId, DateTime periodStart, DateTime periodEnd)
        {
            return TasksInPeriod(UserIdsAtPlant(plantId), periodStart,
                periodEnd);
        }

        // The legacy path: reports submitted before reports had a plant cover
        // one employee's tasks, and reprinting one has to show what it showed
        // when it was filed rather than silently widening to a whole plant.
        public List<TaskItem> GetTasksForReportPeriod(
            string userId, DateTime periodStart, DateTime periodEnd)
        {
            return TasksInPeriod(new List<string> { userId }, periodStart,
                periodEnd);
        }

        // Who works at a plant, according to EHS_PORTAL.
        //
        // CLIP.UserPlants is that system's own record and is incomplete: an
        // employee with no rows there belongs to no plant, so their tasks reach
        // no report at all. ESTAFF cannot fix that from here - the rows are
        // EHS_PORTAL's to write - but callers should know the set can be
        // smaller than "everyone who works there".
        public List<string> UserIdsAtPlant(int plantId)
        {
            return _db.UserPlants
                .Where(up => up.PlantId == plantId)
                .Select(up => up.UserId)
                .Distinct()
                .ToList();
        }

        private List<TaskItem> TasksInPeriod(List<string> userIds,
            DateTime periodStart, DateTime periodEnd)
        {
            if (userIds == null || !userIds.Any())
                return new List<TaskItem>();

            var endOfDay = periodEnd.AddDays(1).AddTicks(-1);

            return _db.TaskItems
                .Include(t => t.TaskClassification)
                .Include(t => t.TaskList)
                .Include(t => t.AssignedToUser)
                .Include(t => t.CreatedByUser)
                .Where(t => userIds.Contains(t.AssignedToUserId)
                         && t.DueDate >= periodStart
                         && t.DueDate <= endOfDay)
                .OrderBy(t => t.DueDate)
                .ToList();
        }

        // Every plant, for the report form's plant chooser.
        //
        // All of them rather than only the generator's own, for the same reason
        // ClipService.GetAllItems offers every CLIP record: the UserPlants
        // mapping is incomplete, and restricting the list to it would leave the
        // four unmapped users unable to generate any report at all.
        public List<Plant> GetPlants()
        {
            return _db.Plants
                .OrderBy(p => p.PlantName)
                .ToList();
        }

        // Tasks with everything the printed report needs. Both download paths
        // (employee and admin) go through here so the two copies of the same
        // report cannot describe a task differently.
        public List<ReportTaskDetailViewModel> BuildReportTaskDetails(
            List<TaskItem> tasks,
            Dictionary<int, ClipItemViewModel> clipItems = null)
        {
            if (tasks == null || !tasks.Any())
                return new List<ReportTaskDetailViewModel>();

            var flows = GetStatusActionFlows(tasks.Select(t => t.TaskId));

            return tasks
                .Select(t => ReportTaskDetailViewModel.From(
                    t,
                    clipItems != null && clipItems.ContainsKey(t.TaskId)
                        ? clipItems[t.TaskId]
                        : null,
                    flows.ContainsKey(t.TaskId) ? flows[t.TaskId] : null))
                .ToList();
        }

        private static StatusRemarkViewModel MapRemark(TaskHistory entry)
        {
            if (entry == null) return null;

            // Rows written before the Remark column existed folded the whole
            // change into NewValue - "Status: In Progress -> Complete. Action
            // taken: <text>" - leaving OldValue as a display label and Remark
            // null. Add_Task_Status_Remark.sql normalises them, but a database
            // that has not had it applied must still read correctly here, so
            // the legacy text is parsed as a fallback for each field.
            var legacyNew = ParseLegacyEntry(entry.NewValue);
            var legacyRem = ParseLegacyEntry(entry.Remark);

            return new StatusRemarkViewModel
            {
                TaskId        = entry.TaskId,
                FromStatus    = ParseStatus(entry.OldValue)
                                    ?? legacyNew.FromStatus
                                    ?? legacyRem.FromStatus,
                ToStatus      = ParseStatus(entry.NewValue)
                                    ?? legacyNew.ToStatus
                                    ?? legacyRem.ToStatus,
                // A remark that still carries the old preamble is reduced to
                // the action itself - the transition is shown separately, so
                // repeating it in the text reads as a rendering fault.
                Remark        = legacyRem.Action
                                    ?? Clean(entry.Remark)
                                    ?? legacyNew.Action,
                ChangedByName = entry.ChangedByUser != null
                                    ? entry.ChangedByUser.UserName
                                    : "-",
                ChangedDate   = entry.ChangedDate
            };
        }

        // "Status: In Progress -> Complete. Action taken: <text>", in any of the
        // arrow spellings that reached the table. Either half may be missing.
        private static readonly Regex LegacyEntryPattern = new Regex(
            @"^\s*Status:\s*(?<from>[^.\->→]+?)\s*(?:-+>|→)\s*" +
            @"(?<to>[^.]+?)\s*\.?\s*(?:Action\s*taken:\s*(?<action>.*))?$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // "Action taken: <text>" on its own, without the status preamble.
        private static readonly Regex LegacyActionPattern = new Regex(
            @"^\s*Action\s*taken:\s*(?<action>.*)$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static LegacyEntry ParseLegacyEntry(string value)
        {
            var result = new LegacyEntry();
            if (string.IsNullOrWhiteSpace(value)) return result;

            var match = LegacyEntryPattern.Match(value);

            if (match.Success)
            {
                result.FromStatus = ParseStatus(match.Groups["from"].Value);
                result.ToStatus   = ParseStatus(match.Groups["to"].Value);
                result.Action     = Clean(match.Groups["action"].Value);

                // "Status: ..." that parsed into neither status is not the
                // legacy shape at all - leave the text to the caller.
                if (result.FromStatus == null && result.ToStatus == null)
                    return new LegacyEntry();

                return result;
            }

            match = LegacyActionPattern.Match(value);
            if (match.Success)
                result.Action = Clean(match.Groups["action"].Value);

            return result;
        }

        private class LegacyEntry
        {
            public TaskStatus? FromStatus { get; set; }
            public TaskStatus? ToStatus { get; set; }
            public string Action { get; set; }
        }

        private static string Clean(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return value.Trim();
        }

        // Accepts the enum name and the display label alike: rows written
        // before the values were normalised stored "In Progress".
        private static TaskStatus? ParseStatus(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var cleaned = value.Trim().Replace(" ", "");

            TaskStatus parsed;
            return Enum.TryParse(cleaned, true, out parsed)
                && Enum.IsDefined(typeof(TaskStatus), parsed)
                    ? parsed
                    : (TaskStatus?)null;
        }

        private static string TrimRemark(string remark)
        {
            if (string.IsNullOrWhiteSpace(remark)) return null;

            remark = remark.Trim();
            return remark.Length > 500 ? remark.Substring(0, 500) : remark;
        }
    }
}
