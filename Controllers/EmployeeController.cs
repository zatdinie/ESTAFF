using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using EHS_PORTAL.Areas.ESTAFF.Filters;
using EHS_PORTAL.Areas.ESTAFF.Models.Data;
using EHS_PORTAL.Areas.ESTAFF.Models.ViewModels;
using EHS_PORTAL.Areas.ESTAFF.Services;
using Microsoft.Win32;

namespace EHS_PORTAL.Areas.ESTAFF.Controllers
{
    [EmployeeOnly]
    public class EmployeeController : Controller
    {
        private ApplicationDbContext _db = new ApplicationDbContext();
        private ClipDbContext _clip = new ClipDbContext();

        private ClipService Clip => new ClipService(_db, _clip);

        // Helper method to get current user empnumber
        private ApplicationUser CurrentUser =>
            _db.Users.Find(User.Identity.GetUserId());

        // Helper method to set layout variables
        private void SetLayoutData()
        {
            var user = CurrentUser;
            ViewBag.FullName = user?.UserName ?? "";
        }

        // ===========
        // Dashboard
        // ===========
        public ActionResult Index()
        {
            SetLayoutData();
            ViewBag.PageTitle = "Dashboard";
            ViewBag.PageSubtitle = "Here's your task overview for today.";

            var userId = User.Identity.GetUserId();

            // Auto-flag overdue
            new TaskService(_db).UpdateOverdueTasks();

            var allTasks = _db.TaskItems
                .Where(t => t.AssignedToUserId == userId)
                .ToList();

            ViewBag.TotalTasks = allTasks.Count;
            ViewBag.PendingTasks = allTasks.Count(t =>
                t.Status == TaskStatus.Pending);
            ViewBag.InProgressTasks = allTasks.Count(t =>
                t.Status == TaskStatus.InProgress);
            ViewBag.CompletedTasks = allTasks.Count(t =>
                t.Status == TaskStatus.Complete);
            ViewBag.OverdueTasks = allTasks.Count(t =>
                t.Status == TaskStatus.Overdue);

            // On-time rate
            var completed = allTasks
                .Where(t => t.Status == TaskStatus.Complete)
                .ToList();
            var onTime = completed
                .Count(t => t.CompletedDate.HasValue
                    && t.CompletedDate <= t.DueDate);
            ViewBag.OnTimeRate = completed.Count > 0
                ? Math.Round((decimal)onTime / completed.Count * 100, 1)
                : 0;

            // Due Today
            var today = DateTime.Today;
            ViewBag.DueToday = allTasks
                .Where(t => t.DueDate.Date == today
                    && t.Status != TaskStatus.Complete)
                .OrderBy(t => t.DueDate)
                .Take(5)
                .ToList();

            // Recent Tasks
            ViewBag.RecentTasks = allTasks
                .OrderByDescending(t => t.CreatedDate)
                .Take(6)
                .ToList();

            return View();
        }

        // ===========
        // Task Management
        // ===========
        public ActionResult MyTasks(string status = "", string q = null,
            string sort = null)
        {
            SetLayoutData();
            ViewBag.PageTitle = "My Tasks";
            ViewBag.PageSubtitle = "Manage all your tasks.";

            var userId = User.Identity.GetUserId();

            // Auto-flag overdue
            new TaskService(_db).UpdateOverdueTasks();

            // Tab counts are taken from the unfiltered set, so they keep
            // reading as totals for the whole workload while a search narrows
            // only the cards below them.
            var all = _db.TaskItems
                .Where(t => t.AssignedToUserId == userId)
                .ToList();

            ViewBag.AllCount = all.Count;
            ViewBag.PendingCount = all.Count(t =>
                t.Status == TaskStatus.Pending);
            ViewBag.InProgCount = all.Count(t =>
                t.Status == TaskStatus.InProgress);
            ViewBag.CompleteCount = all.Count(t =>
                t.Status == TaskStatus.Complete);
            ViewBag.OverdueCount = all.Count(t =>
                t.Status == TaskStatus.Overdue);

            // The cards show the classification, task type, who raised the
            // task and its CLIP record, so the lookups are joined rather than
            // lazily loaded one row at a time.
            var query = _db.TaskItems
                .Include(t => t.TaskClassification)
                .Include(t => t.TaskList)
                .Include(t => t.CreatedByUser)
                .Where(t => t.AssignedToUserId == userId);

            if (!string.IsNullOrEmpty(status) &&
                Enum.TryParse<TaskStatus>(status, out var statusEnum))
                query = query.Where(t => t.Status == statusEnum);

            var term = (q ?? "").Trim();
            if (term.Length > 0)
            {
                query = query.Where(t => t.Title.Contains(term)
                    || (t.Description != null && t.Description.Contains(term))
                    || (t.TaskClassification != null
                        && t.TaskClassification.Name.Contains(term))
                    || (t.TaskList != null && t.TaskList.Name.Contains(term)));
            }

            var tasks = Sort(query.ToList(), sort);

            ViewBag.SelectedStatus = status;
            ViewBag.SearchTerm = term;
            ViewBag.SelectedSort = NormaliseSort(sort);
            ViewBag.TotalCount = all.Count;
            ViewBag.CurrentUserId = userId;

            return View(BuildMyTaskList(tasks, userId));
        }

        private static string NormaliseSort(string sort)
        {
            switch (sort)
            {
                case "created":
                case "priority":
                case "title":
                    return sort;
                default:
                    return "due";
            }
        }

        // Default order answers "what needs me next": open work by due date,
        // soonest first, with finished tasks pushed to the end rather than
        // interleaved. Sorting by creation date — the previous behaviour —
        // buried an overdue task from last month at the bottom of the page.
        private static List<TaskItem> Sort(List<TaskItem> tasks, string sort)
        {
            switch (NormaliseSort(sort))
            {
                case "created":
                    return tasks
                        .OrderByDescending(t => t.CreatedDate)
                        .ToList();

                case "priority":
                    return tasks
                        .OrderBy(t => t.Status == TaskStatus.Complete ? 1 : 0)
                        .ThenByDescending(t => t.Priority.HasValue
                            ? (int)t.Priority.Value
                            : 0)
                        .ThenBy(t => t.DueDate)
                        .ToList();

                case "title":
                    return tasks
                        .OrderBy(t => t.Title)
                        .ToList();

                default:
                    return tasks
                        .OrderBy(t => t.Status == TaskStatus.Complete ? 1 : 0)
                        .ThenBy(t => t.DueDate)
                        .ThenByDescending(t => t.Priority.HasValue
                            ? (int)t.Priority.Value
                            : 0)
                        .ToList();
            }
        }

        // Mirrors AdminController.BuildTaskList: the CLIP record and the status
        // action flow are resolved in batched queries rather than per card, so
        // the employee sees the same detail about their own task that an admin
        // sees when reviewing it.
        private List<TaskListItemViewModel> BuildMyTaskList(
            List<TaskItem> tasks, string userId)
        {
            var clipItems = Clip.GetItemsForTasks(tasks);
            var flows = new TaskService(_db)
                .GetStatusActionFlows(tasks.Select(t => t.TaskId));

            return tasks.Select(t =>
            {
                var flow = flows.ContainsKey(t.TaskId)
                    ? flows[t.TaskId]
                    : new List<StatusRemarkViewModel>();

                return new TaskListItemViewModel
                {
                    TaskId                 = t.TaskId,
                    Title                  = t.Title,
                    Description            = t.Description,
                    SubTaskId              = t.SubTaskId,
                    TaskClassificationId   = t.TaskClassificationId,
                    ClassificationName     = t.TaskClassification?.Name,
                    TaskListId             = t.TaskListId,
                    TaskListName           = t.TaskList?.Name,
                    Status                 = t.Status,
                    Priority               = t.Priority,
                    DueDate                = t.DueDate,
                    PeriodStart            = t.PeriodStart ?? null,
                    PeriodEnd              = t.PeriodEnd ?? null,
                    CreatedDate            = t.CreatedDate,
                    AssignedDate           = t.AssignedDate,
                    CompletedDate          = t.CompletedDate,
                    AssignedToUserId       = t.AssignedToUserId,
                    CreatedByUserId        = t.CreatedByUserId,
                    CreatedByName          = t.CreatedByUserId == userId
                                                 ? "You"
                                                 : t.CreatedByUser?.UserName ?? "-",
                    ClipItem               = clipItems.ContainsKey(t.TaskId)
                                                 ? clipItems[t.TaskId]
                                                 : null,
                    StatusActions          = flow,
                    LatestStatusRemark     = flow.LastOrDefault()
                };
            }).ToList();
        }


        // Classifications, task types, and the CLIP records for the signed-in
        // employee's own plants. The admin equivalent takes an employee id
        // because an admin picks the assignee; here it is always the caller.
        private TaskFormOptions GetFormOptions()
        {
            var clip = Clip;

            return new TaskFormOptions
            {
                Classifications = TaskDisplay.ToOptions(
                    _db.TaskClassifications
                        .OrderBy(c => c.TaskClassificationId)
                        .ToList()),
                TaskLists = TaskDisplay.ToOptions(_db.TaskLists
                    .OrderBy(l => l.Name)
                    .ToList()),
                // Every CLIP record, filtered by plant in the picker. Not just
                // the employee's own plants: that mapping is EHS_PORTAL's and
                // is incomplete, so it left the picker empty for anyone
                // missing a CLIP.UserPlants row. See ClipService.GetAllItems.
                ClipItems = clip.GetAllItems()
            };
        }

        // The one check the data annotations cannot express: a task type has to
        // belong to the chosen classification, so it is required here rather
        // than on the model. Attaching a CLIP record stays optional.
        private void ValidateClassification(CreateTaskViewModel model)
        {
            if (!model.TaskListId.HasValue)
            {
                ModelState.AddModelError("TaskListId",
                    "Select the task type this task covers.");
            }
        }

        // The period rules live in TaskPeriod because both forms answer to
        // them; the controller only reports what they return. Whether a period
        // is required depends on ScheduleType, which no annotation can see.
        private void ValidatePeriod(ITaskPeriodFields fields)
        {
            // A daily task is due on the day it is worked, so the form hides
            // the due date and may post nothing for it. The binder's complaint
            // about a required field the user was never shown is not an error
            // anyone can act on - TaskPeriod.EffectiveDueDate supplies the
            // date, and the missing period is reported below in its own words.
            if (fields.ScheduleType == TaskScheduleType.Daily)
                ModelState.Remove("DueDate");

            foreach (var error in TaskPeriod.Validate(fields))
                ModelState.AddModelError(error.Key, error.Value);
        }

        // Mirrors AdminController.ApplyClassificationLink: the rule itself lives
        // in ClipService, the controller only reports the rejection.
        private bool ApplyClassificationLink(TaskItem task,
            int? classificationId, int? taskListId, string clipItemKey)
        {
            var result = Clip.TryApplyClassificationLink(task,
                classificationId, taskListId, clipItemKey);

            if (result != ClipService.ClipAttachResult.Unavailable)
                return true;

            ModelState.AddModelError("ClipItemKey",
                "That CLIP item no longer exists in CLIP. Pick another.");
            return false;
        }

        // ===========
        // Create Task - Get
        // ===========
        public ActionResult CreateTask()
        {
            SetLayoutData();
            ViewBag.PageTitle = "Create Task";
            ViewBag.PageSubtitle = "Add a new task to your list.";

            return View(new CreateTaskViewModel
            {
                // Yourself by default - assigning to a plant colleague is the
                // exception, not the usual case.
                AssignedToUserId = User.Identity.GetUserId(),

                // Prefilled with today so that choosing "Daily" hands the
                // user the day they are almost certainly recording. Ignored
                // while the task is long term - ApplyTo clears it.
                PeriodDate = DateTime.Today,

                // Long term with no period is the ordinary task, so the
                // form opens on it and asks for nothing extra. Choosing
                // "Daily" is what brings the period into play.
                ScheduleType = TaskScheduleType.LongTerm,

                Options = GetFormOptions(),
                Employees = GetEmployeeSelectList()
            });
        }

        // ===========
        // Populate TaskList based on selected TaskClassification
        //============
        [HttpGet]
        public JsonResult GetTaskByClassification(int classificationId)
        {
            var tasks = new TaskService(_db).GetTaskList(classificationId);
            var result = tasks.Select(t => new
            {
                value = t.TaskListId,
                text = t.Name
            });
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        // ===========
        // Create Task - Post
        // ===========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateTask(CreateTaskViewModel model)
        {
            SetLayoutData();
            ViewBag.PageTitle = "Create Task";
            ViewBag.PageSubtitle = "Add a new task to your list.";

            model.Options = GetFormOptions();
            model.Employees = GetEmployeeSelectList();

            var userId = User.Identity.GetUserId();

            // Blank means "mine". Anything else has to be somebody the picker
            // actually offered: sharing a plant is the authorisation here, so
            // it is re-checked against the database rather than trusted from
            // the form, which a caller can post anything into.
            var assigneeId = string.IsNullOrWhiteSpace(model.AssignedToUserId)
                ? userId
                : model.AssignedToUserId;

            if (assigneeId != userId &&
                model.Employees.All(e => e.UserId != assigneeId))
            {
                ModelState.AddModelError("AssignedToUserId",
                    "You can only assign tasks to employees at your own plant.");
            }

            ValidateClassification(model);
            ValidatePeriod(model);

            if (!ModelState.IsValid)
                return View(model);

            var task = new TaskItem
            {
                Title = model.Title,
                Description = model.Description,
                AssignedToUserId = assigneeId,
                CreatedByUserId = userId,
                Priority = model.Priority,
                Status = TaskStatus.Pending,
                CreatedDate = DateTime.Now,
                LastModifiedDate = DateTime.Now,
                TaskClassificationId = model.TaskClassificationId
            };

            // Schedule type, period and due date together: a daily task is due
            // on the day it is worked and carries the hours, a long-term one
            // is due when the form said and carries no period.
            TaskPeriod.ApplyTo(task, model);

            // Sets the task type and any attached CLIP record. Any task may
            // carry one, whoever it is assigned to.
            if (!ApplyClassificationLink(task, model.TaskClassificationId,
                    model.TaskListId, model.ClipItemKey))
                return View(model);

            _db.TaskItems.Add(task);
            _db.SaveChanges();

            var assigneeName = assigneeId == userId
                ? null
                : model.Employees.First(e => e.UserId == assigneeId).FullName;

            new TaskService(_db).LogHistory(
                task.TaskId,
                "Created",
                null,
                assigneeName == null
                    ? $"Task '{task.Title}' created by employee."
                    : $"Task '{task.Title}' created and assigned to {assigneeName}.",
                userId);

            TempData["SuccessMessage"] = assigneeName == null
                ? $"Task '{model.Title}' created successfully."
                : $"Task '{model.Title}' assigned to {assigneeName}.";
            return RedirectToAction("MyTasks");
            
        }

        // ===========
        // Edit Task - Get
        // ===========
        public ActionResult EditTask(int id)
        {
            SetLayoutData();
            ViewBag.PageTitle = "Edit Task";
            ViewBag.PageSubtitle = "Update your task details.";

            var userId = User.Identity.GetUserId();
            var task = _db.TaskItems.Find(id);

            // only allow to edit own tasks
            if (task == null || task.AssignedToUserId != userId)
               return HttpNotFound();

            var vm = new CreateTaskViewModel
            {
                Title = task.Title,
                Description = task.Description,
                DueDate = task.DueDate,

                // Shown exactly as stored. A task with no period keeps none:
                // the form only insists on one if it is switched to Daily, and
                // filling the hours in here would put a period on a long-term
                // task nobody asked to change.
                ScheduleType = task.ScheduleType,
                PeriodDate = task.PeriodDate,
                PeriodStart = task.PeriodStart,
                PeriodEnd = task.PeriodEnd,

                Priority = task.Priority,
                TaskClassificationId = task.TaskClassificationId,
                TaskListId = task.TaskListId,
                ClipItemKey = ClipService.BuildKeyForTask(task),
                Options = GetFormOptions()
            };

            ViewBag.TaskId = id;
            ViewBag.Status = task.Status;
            return View(vm);
        }

        // ===========
        // Edit Task - Post
        // ===========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditTask(int id, CreateTaskViewModel model)
        {
            SetLayoutData();
            ViewBag.PageTitle = "Edit Task";
            ViewBag.PageSubtitle = "Update your task details.";
            ViewBag.TaskId = id;

            var userId = User.Identity.GetUserId();
            var task = _db.TaskItems.Find(id);

            if (task == null || task.AssignedToUserId != userId)
                return HttpNotFound();

            ViewBag.Status = task.Status;

            model.Options = GetFormOptions();

            ValidateClassification(model);
            ValidatePeriod(model);

            if (!ModelState.IsValid)
                return View(model);

            var changes = new System.Text.StringBuilder();

            if (task.Title != model.Title)
            {
                changes.Append($"Title: '{task.Title}' -> '{model.Title}'. ");
                task.Title = model.Title;
            }

            if (task.Description != model.Description)
            {
                changes.Append("Concern/Issue updated. ");
                task.Description = model.Description;
            }

            // Read from the schedule, not straight off the form: a daily
            // task's due date is the day it is worked, and the form posts no
            // due date of its own. The write is ApplyTo's below; this only
            // records the change while the old value is still readable.
            var dueDate = TaskPeriod.EffectiveDueDate(model);

            if (task.DueDate != dueDate)
            {
                changes.Append($"Due: '{task.DueDate:MMM dd}'" +
                    $" -> '{dueDate:MMM dd}'. ");
            }

            // Schedule type and period read as one thing in the history:
            // "Daily, 25 Aug 08:00 - 17:00", so a change to any part of it is
            // one legible line rather than three.
            var scheduleBefore = TaskPeriod.Describe(task);
            TaskPeriod.ApplyTo(task, model);
            var scheduleAfter = TaskPeriod.Describe(task);

            if (scheduleBefore != scheduleAfter)
                changes.Append(
                    $"Schedule: '{scheduleBefore}' -> '{scheduleAfter}'. ");

            if (task.Priority != model.Priority)
            {
                changes.Append($"Priority: '{task.Priority}'" +
                    $" -> '{model.Priority}'. ");
                task.Priority = model.Priority;
            }

            var before = Clip.DescribeClassification(task);

            task.TaskClassificationId = model.TaskClassificationId;

            if (!ApplyClassificationLink(task, model.TaskClassificationId,
                    model.TaskListId, model.ClipItemKey))
                return View(model);

            var after = Clip.DescribeClassification(task);
            if (before != after)
                changes.Append($"Classification: '{before}' -> '{after}'. ");

            task.LastModifiedDate = DateTime.Now;
            _db.SaveChanges();

            if (changes.Length > 0)
            
                new TaskService(_db).LogHistory(
                    task.TaskId,
                    "Updated",
                    "Previous values",
                    changes.ToString(),
                    userId);
            
            TempData["SuccessMessage"] = "Task updated successfully!";
            return RedirectToAction("MyTasks");

        }

        // ===========
        // Update Task Status - POST
        // ===========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateStatus(
            int taskId, TaskStatus status,
            string actionTaken, string returnUrl = null)
        {
            var userId = User.Identity.GetUserId();
            var task = _db.TaskItems.Find(taskId);

            // only allow to update own tasks
            if (task == null || task.AssignedToUserId != userId)
                return HttpNotFound();

            // In Progress / Complete must say what was actually done
            if (RequiresActionTaken(status)
                && string.IsNullOrWhiteSpace(actionTaken))
            {
                TempData["ErrorMessage"] =
                    $"Please describe the action taken before moving " +
                    $"'{task.Title}' to {StatusLabel(status)}.";
                return RedirectBack(returnUrl);
            }

            if (task.Status == status)
                return RedirectBack(returnUrl);

            var oldStatus = task.Status;

            task.Status = status;
            task.CompletedDate = status == TaskStatus.Complete
                ? DateTime.Now
                : (DateTime?)null;
            task.LastModifiedDate = DateTime.Now;
            _db.SaveChanges();

            // The action-taken text belongs in Remark, not folded into the new
            // value: Remark is the field GetLatestStatusRemark reads back onto
            // the task. Old/new values stay raw enum names so the transition
            // parses back into a TaskStatus.
            new TaskService(_db).LogStatusChange(
                task.TaskId, oldStatus, status, userId, actionTaken);

            TempData["SuccessMessage"] =
                $"'{task.Title}' marked as {StatusLabel(status)}.";
            return RedirectBack(returnUrl);
        }

        private static bool RequiresActionTaken(TaskStatus status)
        {
            return status == TaskStatus.InProgress
                || status == TaskStatus.Complete;
        }

        private static string StatusLabel(TaskStatus status)
        {
            return status == TaskStatus.InProgress
                ? "In Progress"
                : status.ToString();
        }

        // Returns to the page the status was changed from
        // (keeps the tab filter / calendar period intact)
        private ActionResult RedirectBack(string returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("MyTasks");
        }

       // ============
       // Calendar - Unified View
       // ============
        public ActionResult Calendar(
            string view = "weekly",
            DateTime? date = null)
        {
            SetLayoutData();
            ViewBag.PageTitle = "Calendar";
            ViewBag.PageSubtitle = "Manage your tasks in a calendar view.";

            var userId = User.Identity.GetUserId();
            var targetDate = date?.Date ?? DateTime.Today;

            new TaskService(_db).UpdateOverdueTasks();

            // Calculate period based on view
            DateTime periodStart;
            DateTime periodEnd;

            switch (view.ToLower())
            {
                case "daily":
                    periodStart = targetDate;
                    periodEnd = targetDate;
                    break;

                case "monthly":
                    periodStart = new DateTime(
                        targetDate.Year, targetDate.Month, 1);
                    periodEnd = periodStart
                        .AddMonths(1).AddDays(-1);
                    break;

                default: // weekly
                    int diff = (int)targetDate.DayOfWeek 
                        - (int)DayOfWeek.Monday;
                    if (diff < 0) diff += 7;
                    periodStart = targetDate.AddDays(-diff);
                    periodEnd = periodStart.AddDays(6);
                    break;
            }

            var endOfDay = periodEnd.AddDays(1).AddTicks(-1);

            var tasks = _db.TaskItems
                .Include(t => t.TaskClassification)
                .Include(t => t.TaskList)
                .Include(t => t.CreatedByUser)
                .Where(t => t.AssignedToUserId == userId
                         && t.DueDate >= periodStart
                         && t.DueDate <= endOfDay)
                .OrderBy(t => t.DueDate)
                .ToList();

            // The detail behind each card, resolved once for the whole
            // period rather than per card: BuildMyTaskList batches the CLIP
            // lookups and the status history into two queries. The view
            // renders one hidden panel per task and the modal copies it, so
            // the employee sees the same detail an admin sees on theirs.
            var taskById = tasks.ToDictionary(t => t.TaskId);

            ViewBag.TaskDetails = BuildMyTaskList(tasks, userId)
                .ToDictionary(
                    t => t.TaskId,
                    t => TaskDetailPanelModel.ForOwnTask(
                        t,
                        TaskPeriod.Describe(taskById[t.TaskId]),
                        Url.Action("EditTask", "Employee",
                            new { id = t.TaskId })));

            // Build day groups
            var days = new List<DayTaskGroup>();
            for (var d = periodStart; d <= periodEnd;
                d = d.AddDays(1))
            {
                days.Add(new DayTaskGroup
                {
                    Date = d,
                    Tasks = tasks.Where(t => 
                        t.DueDate.Date == d.Date).ToList()
                });
            }

            // Navigaiton dates
            switch (view.ToLower())
            {
                case "daily":
                    ViewBag.PrevDate = targetDate.AddDays(-1);
                    ViewBag.NextDate = targetDate.AddDays(1);
                    break;
                case "monthly":
                    ViewBag.PrevDate = targetDate.AddMonths(-1);
                    ViewBag.NextDate = targetDate.AddMonths(1);
                    break;
                default: // weekly
                    ViewBag.PrevDate = targetDate.AddDays(-7);
                    ViewBag.NextDate = targetDate.AddDays(7);
                    break;
            }

            ViewBag.CurrentView = view.ToLower();
            ViewBag.TargetDate = targetDate;
            ViewBag.PeriodStart = periodStart;
            ViewBag.PeriodEnd = periodEnd;
            ViewBag.IsToday = targetDate == DateTime.Today ||
                (periodStart <= DateTime.Today && 
                DateTime.Today <= periodEnd);

            // All tasks for this employee (drag drop)
            ViewBag.TotalTaskCount = _db.TaskItems
                .Count(t => t.AssignedToUserId == userId);

            return View(days);
        }

        // ===========
        // Reschedule Task - POST (Drag & Drop)
        // ===========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RescheduleTask(
            int taskId, string newDate)
        {
            var userId = User.Identity.GetUserId();
            var task = _db.TaskItems.Find(taskId);

            if (task == null || task.AssignedToUserId != userId)
                return Json(new
                {
                    success = false,
                    message = "Task not found."
                });

            if (!DateTime.TryParse(newDate, out var parsedDate))
                return Json(new
                {
                    success = false,
                    message = "Invalid date."
                });

            var oldDate = task.DueDate;
            task.DueDate = parsedDate;

            // A daily task is due on the day it is worked, so dragging it to
            // another day moves the period too. Left alone, one task would
            // carry two dates disagreeing about when the work happens.
            if (task.ScheduleType == TaskScheduleType.Daily
                && task.PeriodDate.HasValue)
                task.PeriodDate = parsedDate.Date;

            task.LastModifiedDate = DateTime.Now;

            // overdue and new date is future, reset to pending
            if (task.Status == TaskStatus.Overdue
                && parsedDate >= DateTime.Today)
            {
                task.Status = TaskStatus.Pending;
            }

            _db.SaveChanges();

            new TaskService(_db).LogHistory(
                task.TaskId, 
                "Updated",
                $"Due: {oldDate:MMM dd, yyyy}",
                $"Due: {parsedDate:MMM dd, yyyy} (rescheduled)",
                userId);
            
            return Json(new
            {
                success = true,
                message = $"Task resecheduled to " +
                    $"{parsedDate:MMM dd, yyyy}."
            });
            
        }

        // ===========
        // Delete Task - Post
        // ===========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteTask(int id)
        {
            var userId = User.Identity.GetUserId();
            var task = _db.TaskItems.Find(id);

            // Employees may only delete tasks they raised themselves - tasks
            // assigned by an admin stay on the board.
            if (task == null
                || task.AssignedToUserId != userId
                || task.CreatedByUserId != userId)
                return HttpNotFound();

            var title = task.Title;

            _db.TaskItems.Remove(task);
            _db.SaveChanges();

            TempData["SuccessMessage"] = $"Task '{title}' deleted.";
            return RedirectToAction("MyTasks");
        }

        // ===========
        // Profile
        // ===========
        // The sidebar links here and Views/Employee/Profile.cshtml exists, so
        // the action has to as well. "new" because Controller.Profile is a
        // protected property on the base class.
        public new ActionResult Profile()
        {
            SetLayoutData();
            ViewBag.PageTitle = "My Profile";
            ViewBag.PageSubtitle = "View and update your profile.";

            var user = CurrentUser;
            if (user == null) return HttpNotFound();

            var userId = User.Identity.GetUserId();

            ViewBag.TotalTasks = _db.TaskItems
                .Count(t => t.AssignedToUserId == userId);
            ViewBag.CompletedTasks = _db.TaskItems
                .Count(t => t.AssignedToUserId == userId
                         && t.Status == TaskStatus.Complete);
            ViewBag.PendingTasks = _db.TaskItems
                .Count(t => t.AssignedToUserId == userId
                         && (t.Status == TaskStatus.Pending
                         || t.Status == TaskStatus.InProgress));
            ViewBag.OverdueTasks = _db.TaskItems
                .Count(t => t.AssignedToUserId == userId
                         && t.Status == TaskStatus.Overdue);

            var completed = _db.TaskItems
                .Where(t => t.AssignedToUserId == userId
                         && t.Status == TaskStatus.Complete)
                .ToList();

            var onTime = completed
                .Count(t => t.CompletedDate.HasValue
                    && t.CompletedDate <= t.DueDate);
            ViewBag.OnTimeRate = completed.Count > 0
                ? Math.Round((decimal)onTime / completed.Count * 100, 1)
                : 0;

            return View(user);
        }

        public ActionResult DailyView(DateTime? date = null)
        {
            return RedirectToAction("Calendar",
                new { view = "daily",
                    date = (date ?? DateTime.Today)
                        .ToString("yyyy-MM-dd") });
        }

        public ActionResult WeeklyView(DateTime? weekStart = null)
        {
            return RedirectToAction("Calendar",
                new { view = "weekly",
                    date = (weekStart ?? DateTime.Today)
                        .ToString("yyyy-MM-dd") });
        }
        

        // ===========
        // Reports - LIST
        // ===========
        //
        // Every report, not only the caller's. A return covers a plant for a
        // period and there is one of them, so whoever generated it is a detail
        // of its provenance rather than a reason to hide it from a colleague
        // who needs to see whether this month has been filed.
        public ActionResult MyReports()
        {
            SetLayoutData();
            ViewBag.PageTitle = "Reports";
            ViewBag.PageSubtitle =
                "Monthly returns filed for your plants.";

            var reports = _db.Reports
                .OrderByDescending(r => r.CreatedDate)
                .ToList()
                .Select(r => new ReportListItemViewModel
                {
                    ReportId = r.ReportId,
                    PlantId = r.PlantId,
                    PlantName = r.Plant?.PlantName,
                    EmpName = r.User?.UserName ?? "-",
                    EmpNumber = r.User?.EmpID ?? "-",
                    ReportType = r.ReportType,    
                    PeriodStart = r.PeriodStart,
                    PeriodEnd = r.PeriodEnd,
                    Status = r.Status,
                    CreatedDate = r.CreatedDate,
                    SubmittedDate = r.SubmittedDate,
                    ApprovedDate = r.ApprovedDate,
                    RejectionReason = r.RejectionReason
                })
                .ToList();

            return View(reports);
        }

        // ===========
        // Generate Report - GET
        // ===========
        public ActionResult GenerateReport()
        {
            SetLayoutData();
            ViewBag.PageTitle = "Generate Report";
            ViewBag.PageSubtitle = "Create a weekly or monthly report.";

            // Default to current week
            var today = DateTime.Today;
            var weekStart = today.AddDays(
                -(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            if (today.DayOfWeek == DayOfWeek.Sunday)
                weekStart = today.AddDays(-6);
            
            var taskService = new TaskService(_db);
            var plants = taskService.GetPlants();

            // Default to a plant the generator actually works at, when
            // EHS_PORTAL knows of one. Someone with no UserPlants row gets no
            // default and has to choose, rather than being blocked.
            //
            // The id is read into a local first: GetUserId() is an extension
            // method, and EF cannot translate one inside an expression tree -
            // it has to be a plain value by the time the query is built.
            var userId = User.Identity.GetUserId();

            var mine = _db.UserPlants
                .Where(up => up.UserId == userId)
                .Select(up => up.PlantId)
                .ToList();

            var vm = new GenerateReportViewModel
            {
                PeriodStart = weekStart,
                PeriodEnd = today,
                Plants = plants,
                PlantId = plants
                    .Where(p => mine.Contains(p.Id))
                    .Select(p => (int?)p.Id)
                    .FirstOrDefault()
            };

            return View(vm);
        }

        // ===========
        // Generate Report - POST
        // ===========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PreviewReport(GenerateReportViewModel model)
        {
            SetLayoutData();
            ViewBag.PageTitle = "Preview Report";
            ViewBag.PageSubtitle = "Review before submitting.";

            var taskService = new TaskService(_db);
            model.Plants = taskService.GetPlants();

            if (!ModelState.IsValid)
                return View("GenerateReport", model);

            // The same period query the submitted report is read back through,
            // so the preview cannot show a different set of tasks than the one
            // that gets filed.
            var tasks = taskService.GetTasksForReportPeriod(
                model.PlantId.Value, model.PeriodStart, model.PeriodEnd);

            // Worth saying plainly rather than printing an empty return: an
            // empty result here usually means nobody is mapped to the plant in
            // EHS_PORTAL, not that nothing was done.
            if (!tasks.Any() && !taskService.UserIdsAtPlant(
                    model.PlantId.Value).Any())
            {
                TempData["ErrorMessage"] =
                    "No employees are mapped to that plant in CLIP, so no "
                    + "tasks can be collected for it. Ask for the plant's "
                    + "staff to be assigned in EHS_PORTAL.";
            }

            model.Tasks = tasks;

            // The preview shows the same breakdown as the submitted report, so
            // the employee can check the actions they recorded before sending.
            model.TaskDetails = taskService
                .BuildReportTaskDetails(tasks, Clip.GetItemsForTasks(tasks));

            return View("PreviewReport", model);
        }

        // ===========
        // Pending Report - GET
        // ===========
        public ActionResult PendingReport()
        {
            ViewBag.PageTitle = "Pending Report Approval";
            ViewBag.PageSubtitle = "Awaiting for approval by plant representative.";
            var userId = User.Identity.GetUserId();
            var userPlant = _db.UserPlants.FirstOrDefault(up => up.UserId == userId);

            var reports = _db.Reports
                .Where(r => r.Status == ReportStatus.Submitted && r.PlantId == userPlant.PlantId)
                .OrderBy(r => r.SubmittedDate)
                .ToList()
                .Select(r => new ReportListItemViewModel
                {
                    ReportId = r.ReportId,
                    PlantId = r.PlantId,
                    PlantName = r.Plant?.PlantName,
                    EmpName = r.User?.UserName ?? "-",
                    EmpNumber = r.User?.EmpID ?? "-",
                    ReportType = r.ReportType,
                    PeriodStart = r.PeriodStart,
                    PeriodEnd = r.PeriodEnd,
                    Status = r.Status,
                    CreatedDate = r.CreatedDate,
                    SubmittedDate = r.SubmittedDate
                })
                .ToList();

            return View(reports);
        }

        // ===========
        // Review Report - GET
        // ===========
        public ActionResult ReviewReport(int id)
        {
            ViewBag.PageTitle = "Review Report";
            ViewBag.PageSubtitle = "Review plant report.";

            var report = _db.Reports.Find(id);
            if (report == null) return HttpNotFound();

            var taskService = new TaskService(_db);

            // Read through TaskService so the admin's copy of a report covers
            // exactly the tasks the employee submitted. This used to run its
            // own query filtered on CreatedDate while every employee-facing
            // page filtered on DueDate, which meant an approver could be
            // reviewing a different set of tasks than the one that was filed.
            //
            // Plant-scoped reports read their plant's tasks; a legacy personal
            // one still reads the filer's, so an old return reprints as filed.
            var tasks = report.PlantId.HasValue
                ? taskService.GetTasksForReportPeriod(
                    report.PlantId.Value, report.PeriodStart, report.PeriodEnd)
                : taskService.GetTasksForReportPeriod(
                    report.UserId, report.PeriodStart, report.PeriodEnd);

            var completed = tasks.Count(t =>
                t.Status == TaskStatus.Complete);

            var vm = new ReportDetailViewModel
            {
                ReportId = report.ReportId,
                PlantId = report.PlantId,
                PlantName = report.Plant?.PlantName,
                EmpName = report.User?.UserName ?? "-",
                EmpNumber = report.User?.EmpID ?? "-",
                EmpEmail = report.User?.Email ?? "-",
                ReportType = report.ReportType,
                PeriodStart = report.PeriodStart,
                PeriodEnd = report.PeriodEnd,
                Status = report.Status,
                CreatedDate = report.CreatedDate,
                SubmittedDate = report.SubmittedDate,
                ApprovedDate = report.ApprovedDate,
                RejectionReason = report.RejectionReason,
                Tasks = tasks,
                TotalTasks = tasks.Count,
                CompletedTasks = completed,
                PendingTasks = tasks.Count(t => 
                    t.Status == TaskStatus.Pending || 
                    t.Status == TaskStatus.InProgress),
                OverdueTasks = tasks.Count(t =>
                    t.Status == TaskStatus.Overdue),
                CompletionRate = tasks.Count > 0
                    ? Math.Round((decimal)completed / tasks.Count * 100, 1)
                    : 0

            };

            // The review table shows the actions taken on each task, so the
            // page needs the same resolved detail the PDF is built from.
            vm.TaskDetails = taskService
                .BuildReportTaskDetails(tasks, Clip.GetItemsForTasks(tasks));

            return View(vm);
        }

        // ======================
        // APPROVE REPORT — POST
        // ======================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ApproveReport(int id)
        {
            var report = _db.Reports.Find(id);
            var userId = User.Identity.GetUserId();
            var userPlant = _db.UserPlants.FirstOrDefault(up => up.UserId == userId);
            if (report == null || report.PlantId != userPlant.PlantId) return HttpNotFound();

            report.Status = ReportStatus.Approved;
            report.ApprovedDate = DateTime.Now;
            report.RejectionReason = null;
            report.LastModifiedDate = DateTime.Now;
            _db.SaveChanges();

            TempData["SuccessMessage"] =
                $"{report.User?.UserName}'s report approved!";
            return RedirectToAction("PendingReport");
        }

        // ══════════════════════════════════════════
        // REJECT REPORT — POST
        // ══════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectReport(ApproveReportViewModel model)
        {
            var report = _db.Reports.Find(model.ReportId);
            if (report == null) return HttpNotFound();

            if (string.IsNullOrWhiteSpace(model.RejectionReason))
            {
                TempData["ErrorMessage"] = 
                    "Please provide a rejection reason.";
                return RedirectToAction(
                    "ReviewReport", new { id = model.ReportId }
                );
            }

            report.Status = ReportStatus.Rejected;
            report.RejectionReason = model.RejectionReason;
            report.LastModifiedDate = DateTime.Now;
            _db.SaveChanges();

            TempData["SuccessMessage"] = 
                $"{report.User?.UserName}'s report rejected.";
            return RedirectToAction("PendingReport");
        }

        // ===========
        // Submit Report - POST
        // ===========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitReport(GenerateReportViewModel model)
        {
            SetLayoutData();
            var userId = User.Identity.GetUserId();

            if (!model.PlantId.HasValue)
            {
                TempData["ErrorMessage"] =
                    "Select the plant this report covers.";
                return RedirectToAction("GenerateReport");
            }

            // One return per plant per period, whoever files it - the report is
            // the plant's, so a colleague filing the same month again would be
            // a duplicate return rather than a second opinion. A rejected one
            // does not block: replacing it is the whole point of a rejection.
            var existingReport = _db.Reports.FirstOrDefault(r =>
                r.PlantId == model.PlantId
                && r.PeriodStart == model.PeriodStart
                && r.PeriodEnd == model.PeriodEnd
                && r.Status != ReportStatus.Rejected);

            if (existingReport != null)
            {
                TempData["ErrorMessage"] =
                    "A report covering that plant and period has already "
                    + "been submitted.";
                return RedirectToAction("MyReports");
            }

            var report = new Report
            {
                UserId = userId,
                PlantId = model.PlantId,
                ReportType = model.ReportType,
                PeriodStart = model.PeriodStart,
                PeriodEnd = model.PeriodEnd,
                Status = ReportStatus.Submitted,
                SubmittedDate = DateTime.Now,
                CreatedDate = DateTime.Now,
                LastModifiedDate = DateTime.Now
            };

            _db.Reports.Add(report);
            _db.SaveChanges();

            TempData["SuccessMessage"] = 
                "Report submitted successfully! " + 
                "Awaiting manager approval.";
            return RedirectToAction("MyReports");
        }

        // ===========
        // View Report - GET
        // ===========
        public ActionResult ViewReport(int id)
        {
            SetLayoutData();
            ViewBag.PageTitle = "Report Details";
            ViewBag.PageSubtitle = "Monthly return for the plant.";

            var report = _db.Reports.Find(id);

            // No owner check: a return belongs to a plant, not to whoever
            // happened to file it, and a colleague at that plant has every
            // reason to read it.
            if (report == null)
                return HttpNotFound();

            var taskService = new TaskService(_db);

            // A plant-scoped report reads its plant's tasks. A legacy personal
            // one still reads the tasks of the employee who filed it, so
            // reprinting it shows what it showed when it was filed rather than
            // silently widening to a whole plant.
            var tasks = report.PlantId.HasValue
                ? taskService.GetTasksForReportPeriod(
                    report.PlantId.Value, report.PeriodStart, report.PeriodEnd)
                : taskService.GetTasksForReportPeriod(
                    report.UserId, report.PeriodStart, report.PeriodEnd);

            var completed = tasks.Count(t =>
                t.Status == TaskStatus.Complete);

            var vm = new ReportDetailViewModel
            {
                ReportId = report.ReportId,
                PlantId = report.PlantId,
                PlantName = report.Plant?.PlantName,
                EmpName = report.User?.UserName ?? "-",
                EmpNumber = report.User?.EmpID ?? "-",
                EmpEmail = report.User?.Email ?? "-",
                ReportType = report.ReportType,
                PeriodStart = report.PeriodStart,
                PeriodEnd = report.PeriodEnd,
                Status = report.Status,
                CreatedDate = report.CreatedDate,
                SubmittedDate = report.SubmittedDate,
                ApprovedDate = report.ApprovedDate,
                RejectionReason = report.RejectionReason,
                Tasks = tasks,
                TotalTasks = tasks.Count,
                CompletedTasks = completed,
                PendingTasks = tasks.Count(t =>
                    t.Status == TaskStatus.Pending ||
                    t.Status == TaskStatus.InProgress),
                OverdueTasks = tasks.Count(t =>
                    t.Status == TaskStatus.Overdue),
                CompletionRate = tasks.Count > 0
                    ? Math.Round(
                        (decimal)completed / tasks.Count * 100, 1)
                    : 0
            };

            // Same resolved detail the PDF is built from, so the page and the
            // downloaded copy describe each task identically.
            vm.TaskDetails = taskService
                .BuildReportTaskDetails(tasks, Clip.GetItemsForTasks(tasks));

            return View(vm);
        }

        // ===========
        // Download Report Pdf
        // ===========
        public ActionResult DownloadReportPdf(int id)
        {
            var report = _db.Reports.Find(id);

            // No owner check: a return belongs to a plant, not to whoever
            // happened to file it, and a colleague at that plant has every
            // reason to read it.
            if (report == null)
                return HttpNotFound();

            var taskService = new TaskService(_db);

            // A plant-scoped report reads its plant's tasks. A legacy personal
            // one still reads the tasks of the employee who filed it, so
            // reprinting it shows what it showed when it was filed rather than
            // silently widening to a whole plant.
            var tasks = report.PlantId.HasValue
                ? taskService.GetTasksForReportPeriod(
                    report.PlantId.Value, report.PeriodStart, report.PeriodEnd)
                : taskService.GetTasksForReportPeriod(
                    report.UserId, report.PeriodStart, report.PeriodEnd);

            var completed = tasks.Count(t =>
                t.Status == TaskStatus.Complete);

            var vm = new ReportDetailViewModel
            {
                ReportId = report.ReportId,
                PlantId = report.PlantId,
                PlantName = report.Plant?.PlantName,
                EmpName = report.User?.UserName ?? "-",
                EmpNumber = report.User?.EmpID ?? "-",
                EmpEmail = report.User?.Email ?? "-",
                ReportType = report.ReportType,
                PeriodStart = report.PeriodStart,
                PeriodEnd = report.PeriodEnd,
                Status = report.Status,
                CreatedDate = report.CreatedDate,
                SubmittedDate = report.SubmittedDate,
                ApprovedDate = report.ApprovedDate,
                RejectionReason = report.RejectionReason,
                Tasks = tasks,
                TotalTasks = tasks.Count,
                CompletedTasks = completed,
                PendingTasks = tasks.Count(t =>
                    t.Status == TaskStatus.Pending ||
                    t.Status == TaskStatus.InProgress),
                OverdueTasks = tasks.Count(t =>
                    t.Status == TaskStatus.Overdue),
                CompletionRate = tasks.Count > 0
                    ? Math.Round(
                        (decimal)completed / tasks.Count * 100, 1)
                    : 0
            };

            vm.TaskDetails = taskService
                .BuildReportTaskDetails(tasks, Clip.GetItemsForTasks(tasks));

            var pdfService = new ReportPdfService();
            var bytes = pdfService.GeneratePdf(vm);
            // Named after the statutory return it is, so a downloaded copy is
            // filed under the same name as the one the SHO keeps.
            var fileName =
                $"ESH_{vm.ReportTypeLabel}_Report_" +
                $"{vm.EmpNumber}_" +
                $"{vm.PeriodStart:yyyyMMdd}_" +
                $"{vm.PeriodEnd:yyyyMMdd}.pdf";

            return File(bytes, "application/pdf", fileName);
        }

        // ===========
        // Resubmit Rejected Report - Post
        // ===========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResubmitReport(int id)
        {
            var report = _db.Reports.Find(id);

            // Same reasoning as ViewReport: the return is the plant's, so any
            // employee may put a rejected one back up for approval.
            if (report == null
                || report.Status != ReportStatus.Rejected)
                return HttpNotFound();

            report.Status = ReportStatus.Submitted;
            report.SubmittedDate = DateTime.Now;
            report.RejectionReason = null;
            report.LastModifiedDate = DateTime.Now;
            _db.SaveChanges();

            TempData["SuccessMessage"] = 
                "Report resubmitted successfully!";
            return RedirectToAction("MyReports");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
                _clip.Dispose();
            }
            base.Dispose(disposing);
        }
        
        // Colleagues the signed-in employee may assign work to: every active,
        // non-admin user who shares at least one plant with them.
        //
        // There is no PlantId on ApplicationUser to filter by — AspNetUsers is
        // EHS_PORTAL's table and ESTAFF does not add columns to it. Who works
        // where is CLIP.UserPlants, a user-to-plant many-to-many, so "same
        // plant" means "shares a row in that table", not an equality test.
        //
        // The caller is always included, so creating a task for yourself still
        // works. Be aware CLIP.UserPlants is EHS_PORTAL's own record and is
        // incomplete — an employee with no rows there sees only themselves.
        private List<EmployeeSelectItem> GetEmployeeSelectList()
        {
            var userId = User.Identity.GetUserId();

            // Materialised rather than left as a subquery: both lists are tiny
            // and it keeps the final query a plain IN (...).
            var myPlantIds = _db.UserPlants
                .Where(up => up.UserId == userId)
                .Select(up => up.PlantId)
                .Distinct()
                .ToList();

            var plantMateIds = _db.UserPlants
                .Where(up => myPlantIds.Contains(up.PlantId))
                .Select(up => up.UserId)
                .Distinct()
                .ToList();

            if (!plantMateIds.Contains(userId))
                plantMateIds.Add(userId);

            return _db.Users
                .Where(u => !u.IsAdmin
                            && u.IsActive
                            && plantMateIds.Contains(u.Id))
                .OrderBy(u => u.UserName)
                .Select(u => new EmployeeSelectItem
                {
                    UserId = u.Id,
                    FullName = u.UserName,
                    EmpID = u.EmpID
                })
                .ToList();
        }
    }

    // Helper class 
    public class DayTaskGroup
    {
        public DateTime Date { get; set; }
        public List<TaskItem> Tasks { get; set; }
            = new List<TaskItem>();
    }
}
