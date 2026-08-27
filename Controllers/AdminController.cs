using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using TaskStatus = ESTAFF.Models.Data.TaskStatus;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using ESTAFF.Filters;
using ESTAFF.Models.Data;
using ESTAFF.Models.ViewModels;
using ESTAFF.Services;

namespace ESTAFF.Controllers
{
    [AdminOnly]
    public class AdminController : Controller
    {
        private ApplicationDbContext _db = new ApplicationDbContext();
        private ClipDbContext _clip = new ClipDbContext();

        private ClipService Clip => new ClipService(_db, _clip);

        // ══════════════════════════════════════════
        // DASHBOARD
        // ══════════════════════════════════════════
        public ActionResult Index()
        {
            ViewBag.PageTitle    = "Dashboard";
            ViewBag.PageSubtitle = "Welcome back! Here's what's happening today.";

            var totalEmployees = _db.Users
                .Count(u => !u.IsAdmin && u.IsActive);

            var activeTasks = _db.TaskItems
                .Count(t => t.Status == TaskStatus.Pending
                         || t.Status == TaskStatus.InProgress);

            var overdueTasks = _db.TaskItems
                .Count(t => t.Status == TaskStatus.Overdue);

            var pendingReports = _db.Reports
                .Count(r => r.Status == ReportStatus.Submitted);

            var totalTasks = _db.TaskItems.Count();

            var completedTasks = _db.TaskItems
                .Where(t => t.Status == TaskStatus.Complete)
                .ToList();

            var onTimeCount = completedTasks
                .Count(t => t.CompletedDate.HasValue
                         && t.CompletedDate <= t.DueDate);

            var onTimeRate = completedTasks.Count > 0
                ? Math.Round((decimal)onTimeCount / completedTasks.Count * 100, 1)
                : 0;

            ViewBag.TotalEmployees  = totalEmployees;
            ViewBag.ActiveTasks     = activeTasks;
            ViewBag.OverdueTasks    = overdueTasks;
            ViewBag.PendingReports  = pendingReports;
            ViewBag.TotalTasks      = totalTasks;
            ViewBag.OnTimeRate      = onTimeRate;
            ViewBag.PendingCount    = _db.TaskItems.Count(t => t.Status == TaskStatus.Pending);
            ViewBag.InProgressCount = _db.TaskItems.Count(t => t.Status == TaskStatus.InProgress);
            ViewBag.CompleteCount   = _db.TaskItems.Count(t => t.Status == TaskStatus.Complete);
            ViewBag.OverdueCount    = _db.TaskItems.Count(t => t.Status == TaskStatus.Overdue);

            ViewBag.RecentTasks = BuildTaskList(TaskQuery()
                .OrderByDescending(t => t.CreatedDate)
                .Take(8)
                .ToList());

            return View();
        }

        // ══════════════════════════════════════════
        // EMPLOYEES — LIST
        // ══════════════════════════════════════════
        public ActionResult Employees()
        {
            ViewBag.PageTitle    = "My Employees";
            ViewBag.PageSubtitle = "Manage your team members.";

            var employees = _db.Users
                .Where(u => !u.IsAdmin)
                .OrderByDescending(u => u.CreatedDate)
                .ToList()
                .Select(u => new EmployeeCardViewModel
                {
                    UserId             = u.Id,
                    UserName           = u.UserName,
                    EmpID              = u.EmpID,
                    Email              = u.Email,
                    IsActive           = u.IsActive,
                    HireDate           = u.HireDate ?? DateTime.Now,
                    TotalTasks         = _db.TaskItems
                        .Count(t => t.AssignedToUserId == u.Id),
                    CompletedTasks     = _db.TaskItems
                        .Count(t => t.AssignedToUserId == u.Id
                                 && t.Status == TaskStatus.Complete),
                    PendingTasks       = _db.TaskItems
                        .Count(t => t.AssignedToUserId == u.Id
                                 && (t.Status == TaskStatus.Pending
                                 ||  t.Status == TaskStatus.InProgress)),
                    OverdueTasks       = _db.TaskItems
                        .Count(t => t.AssignedToUserId == u.Id
                                 && t.Status == TaskStatus.Overdue),
                    OnTimeRate         = CalculateOnTimeRate(u.Id)
                })
                .ToList();

            return View(employees);
        }

        // ══════════════════════════════════════════
        // CREATE STAFF — GET
        // ══════════════════════════════════════════
        public ActionResult CreateStaff()
        {
            ViewBag.PageTitle    = "Add Employee";
            ViewBag.PageSubtitle = "Create a new staff account.";
            return View(new CreateStaffViewModel());
        }

        // ══════════════════════════════════════════
        // CREATE STAFF — POST
        // ══════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateStaff(CreateStaffViewModel model)
        {
            ViewBag.PageTitle    = "Add Employee";
            ViewBag.PageSubtitle = "Create a new staff account.";

            if (!ModelState.IsValid)
                return View("CreateStaff", model);

            var userManager = HttpContext.GetOwinContext()
                .GetUserManager<ApplicationUserManager>();

            // Check duplicate employee number
            if (_db.Users.Any(u => u.EmpID == model.EmpID))
            {
                ModelState.AddModelError("EmpID",
                    "This employee number is already in use.");
                return View("CreateStaff", model);
            }

            // Check duplicate email
            if (await userManager.FindByEmailAsync(model.Email) != null)
            {
                ModelState.AddModelError("Email",
                    "An account with this email already exists.");
                return View("CreateStaff", model);
            }

            var newUser = new ApplicationUser
            {
                Email            = model.Email,
                UserName         = model.UserName,
                EmpID              = model.EmpID,
                IsAdmin          = false,
                IsActive         = true,
                HireDate         = model.HireDate,
                CreatedDate      = DateTime.Now,
                LastModifiedDate = DateTime.Now
            };

            var result = await userManager.CreateAsync(newUser, model.Password);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] =
                    $"Account for {model.UserName} ({model.EmpID}) created successfully!";
                return RedirectToAction("Employees");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error);

            return View("CreateStaff", model);
        }

        // ══════════════════════════════════════════
        // EDIT EMPLOYEE — GET
        // ══════════════════════════════════════════
        public ActionResult EditEmployee(string id)
        {
            ViewBag.PageTitle    = "Edit Employee";
            ViewBag.PageSubtitle = "Update employee information.";

            var user = _db.Users.Find(id);
            if (user == null || user.IsAdmin)
                return HttpNotFound();

            var completedTasks = _db.TaskItems
                .Where(t => t.AssignedToUserId == id
                         && t.Status == TaskStatus.Complete)
                .ToList();

            var onTime = completedTasks
                .Count(t => t.CompletedDate.HasValue
                         && t.CompletedDate <= t.DueDate);

            var vm = new EditEmployeeViewModel
            {
                UserId             = user.Id,
                UserName           = user.UserName,
                EmpID              = user.EmpID,
                Email              = user.Email,
                HireDate           = user.HireDate ?? DateTime.Now,
                IsActive           = user.IsActive,
                TotalTasks         = _db.TaskItems.Count(t => t.AssignedToUserId == id),
                CompletedTasks     = completedTasks.Count,
                PendingTasks       = _db.TaskItems.Count(t => t.AssignedToUserId == id
                                         && (t.Status == TaskStatus.Pending
                                         ||  t.Status == TaskStatus.InProgress)),
                OverdueTasks       = _db.TaskItems.Count(t => t.AssignedToUserId == id
                                         && t.Status == TaskStatus.Overdue),
                OnTimeRate         = completedTasks.Count > 0
                                         ? Math.Round((decimal)onTime / completedTasks.Count * 100, 1)
                                         : 0
            };

            return View(vm);
        }

        // ══════════════════════════════════════════
        // EDIT EMPLOYEE — POST
        // ══════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditEmployee(
            string id, EditEmployeeViewModel model)
        {
            ViewBag.PageTitle    = "Edit Employee";
            ViewBag.PageSubtitle = "Update employee information.";

            var user = _db.Users.Find(id);
            if (user == null || user.IsAdmin)
                return HttpNotFound();

            if (!ModelState.IsValid)
                return View("EditEmployee", model);

            // Check duplicate employee number (exclude self)
            if (_db.Users.Any(u => u.EmpID == model.EmpID
                                && u.Id != id))
            {
                ModelState.AddModelError("EmpID",
                    "This employee number is already in use.");
                return View("EditEmployee", model);
            }

            user.UserName           = model.UserName;
            user.EmpID              = model.EmpID;
            user.Email              = model.Email;
            user.HireDate           = model.HireDate;
            user.IsActive           = model.IsActive;
            user.LastModifiedDate   = DateTime.Now;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"{model.UserName}'s information updated successfully!";
            return RedirectToAction("Employees");
        }

        // ══════════════════════════════════════════
        // TOGGLE ACTIVE STATUS
        // ══════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ToggleActive(string id)
        {
            var user = _db.Users.Find(id);
            if (user == null || user.IsAdmin)
                return HttpNotFound();

            user.IsActive          = !user.IsActive;
            user.LastModifiedDate  = DateTime.Now;
            await _db.SaveChangesAsync();

            var status = user.IsActive ? "activated" : "deactivated";
            TempData["SuccessMessage"] =
                $"{user.UserName}'s account has been {status}.";

            return RedirectToAction("Employees");
        }

        // ══════════════════════════════════════════
        // PLACEHOLDER ACTIONS
        // ══════════════════════════════════════════

        // ══════════════════════════════════════════
        // TASKS — LIST
        // ══════════════════════════════════════════
        public ActionResult Tasks(string status = "", string employeeId = "",
            string classification = "")
        {
            ViewBag.PageTitle    = "All Tasks";
            ViewBag.PageSubtitle = "View and manage all employee tasks.";

            // Auto flag overdue tasks
            var taskService = new TaskService(_db);
            taskService.UpdateOverdueTasks();

            var query = TaskQuery();

            // Filter by status
            if (!string.IsNullOrEmpty(status) &&
                Enum.TryParse<TaskStatus>(status, out var statusEnum))
                query = query.Where(t => t.Status == statusEnum);

            // Filter by employee
            if (!string.IsNullOrEmpty(employeeId))
                query = query.Where(t => t.AssignedToUserId == employeeId);

            // Filter by classification
            if (!string.IsNullOrEmpty(classification) &&
                int.TryParse(classification, out var classificationId))
                query = query.Where(t =>
                    t.TaskClassificationId == classificationId);

            var tasks = BuildTaskList(query
                .OrderByDescending(t => t.CreatedDate)
                .ToList());

            // Employee dropdown for filtering
            ViewBag.Employees = _db.Users
                .Where(u => !u.IsAdmin && u.IsActive)
                .OrderBy(u => u.UserName)
                .ToList();

            ViewBag.SelectedStatus = status;
            ViewBag.SelectedEmployeeId = employeeId;
            ViewBag.SelectedClassification = classification;
            ViewBag.Classifications = GetClassificationOptions();

            return View(tasks);
        }

        // The ClipItems JSON action that used to refetch the picker's list when
        // the assignee changed is gone: the picker now carries every record and
        // filters by plant on the client, so there is nothing to refetch.

        // ══════════════════════════════════════════
        // ASSIGN TASK — GET
        // ══════════════════════════════════════════
        public ActionResult AssignTask()
        {
            ViewBag.PageTitle    = "Assign Task";
            ViewBag.PageSubtitle = "Create and assign a task to an employee.";

            PopulateTaskClassification();

            var vm = new AssignTaskViewModel
            {
                Employees = GetEmployeeSelectList(),
                Options   = GetFormOptions(),

                // Prefilled with today so that choosing "Daily" hands the
                // user the day they are almost certainly recording. Ignored
                // while the task is long term - ApplyTo clears it.
                PeriodDate = DateTime.Today,

                // Long term with no period is the ordinary task, so the
                // form opens on it and asks for nothing extra. Choosing
                // "Daily" is what brings the period into play.
                ScheduleType = TaskScheduleType.LongTerm
            };
            
            return View(vm);
        }

        // ══════════════════════════════════════════
        // ASSIGN TASK — POST
        // ══════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssignTask(AssignTaskViewModel model)
        {
            ViewBag.PageTitle = "Assign Task";
            ViewBag.PageSubtitle = "Create and assign a task to an employee.";

            model.Employees = GetEmployeeSelectList();
            model.Options = GetFormOptions();

            // Every task has a task type now. Attaching a CLIP record is
            // optional and independent of it.
            if (!model.TaskListId.HasValue)
            {
                ModelState.AddModelError("TaskListId",
                    "Select the task type this task covers.");
            }

            ValidatePeriod(model);

            if (!ModelState.IsValid)
                return View(model);

            var adminId = System.Web.HttpContext.Current.User
                .Identity.GetUserId();

            var task = new TaskItem
            {
                Title = model.Title,
                Description = model.Description,
                AssignedToUserId = model.AssignedToUserId,
                CreatedByUserId = adminId,
                TaskClassificationId = model.TaskClassificationId,
                Priority = model.Priority,
                Status = TaskStatus.Pending,
                CreatedDate = DateTime.Now,
                LastModifiedDate = DateTime.Now
            };

            // Schedule type, period and due date together: a daily task is due
            // on the day it is worked and carries the hours, a long-term one
            // is due when the form said and carries no period.
            TaskPeriod.ApplyTo(task, model);

            if (!ApplyClassificationLink(task, model.TaskClassificationId,
                    model.TaskListId, model.ClipItemKey))
                return View(model);

            _db.TaskItems.Add(task);
            _db.SaveChanges();

            // Log history
            var taskService = new TaskService(_db);
            taskService.LogHistory(
                task.TaskId,
                "Created",
                null,
                $"Task '{task.Title}' assigned",
                adminId);

            TempData["SuccessMessage"] =
                $"Task '{model.Title}' assigned successfully!";
            return RedirectToAction("Tasks");
        }

        // ══════════════════════════════════════════
        // EDIT TASK — GET
        // ══════════════════════════════════════════
        public ActionResult EditTask(int id)
        {
            ViewBag.PageTitle = "Edit Tasks";
            ViewBag.PageSubtitle = "Update task details.";

            var task = _db.TaskItems.Find(id);
            if (task == null) return HttpNotFound();

            var vm = new EditTaskViewModel
            {
                TaskId = task.TaskId,
                Title = task.Title,
                Description = task.Description,
                AssignedToUserId = task.AssignedToUserId,
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
                Status = task.Status,
                TaskClassificationId = task.TaskClassificationId,
                TaskListId = task.TaskListId,
                ClipItemKey = ClipService.BuildKeyForTask(task),
                Options = GetFormOptions(),
                Employees = GetEmployeeSelectList()
            };

            ViewBag.LatestStatusRemark =
                new TaskService(_db).GetLatestStatusRemark(task.TaskId);

            return View(vm);
        }

        // ══════════════════════════════════════════
        // EDIT TASK — POST
        // ══════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditTask(int id, EditTaskViewModel model)
        {
            ViewBag.PageTitle = "Edit Task";
            ViewBag.PageSubtitle = "Update task details.";

            model.Employees = GetEmployeeSelectList();
            model.Options = GetFormOptions();

            var task = _db.TaskItems.Find(id);
            if (task == null) return HttpNotFound();

            ViewBag.LatestStatusRemark =
                new TaskService(_db).GetLatestStatusRemark(task.TaskId);

            if (!model.TaskListId.HasValue)
            {
                ModelState.AddModelError("TaskListId",
                    "Select the task type this task covers.");
            }

            ValidatePeriod(model);

            if (!ModelState.IsValid)
                return View(model);

            var adminId = System.Web.HttpContext.Current.User
                .Identity.GetUserId();
            var taskService = new TaskService(_db);
            var changes = new System.Text.StringBuilder();

            // Track changes
            if (task.Title != model.Title)
            {
                changes.Append($"Title: '{task.Title}' → '{model.Title}'. ");
                task.Title = model.Title;
            }

            if (task.AssignedToUserId != model.AssignedToUserId)
            {
                var oldEmp = _db.Users.Find(task.AssignedToUserId);
                var newEmp = _db.Users.Find(model.AssignedToUserId);
                changes.Append($"Assigned: '{oldEmp?.UserName}'" + 
                    $" → '{newEmp?.UserName}'. ");
                task.AssignedToUserId = model.AssignedToUserId;
            }

            // Read from the schedule, not straight off the form: a daily
            // task's due date is the day it is worked, and the form posts no
            // due date of its own. The write is ApplyTo's below; this only
            // records the change while the old value is still readable.
            var dueDate = TaskPeriod.EffectiveDueDate(model);

            if (task.DueDate != dueDate)
            {
                changes.Append($"Due Date: '{task.DueDate:MMM dd}'" +
                    $" → '{dueDate:MMM dd}'. ");
            }

            // Schedule type and period read as one thing in the history:
            // "Daily, 25 Aug 08:00 - 17:00", so a change to any part of it is
            // one legible line rather than three.
            var scheduleBefore = TaskPeriod.Describe(task);
            TaskPeriod.ApplyTo(task, model);
            var scheduleAfter = TaskPeriod.Describe(task);

            if (scheduleBefore != scheduleAfter)
                changes.Append(
                    $"Schedule: '{scheduleBefore}' → '{scheduleAfter}'. ");

            if (task.Priority != model.Priority)
            {
                changes.Append($"Priority: '{task.Priority}'" +
                    $" → '{model.Priority}'. ");
                task.Priority = model.Priority;
            }

            // Status transitions get their own history entry (with the remark)
            // so the latest one can be surfaced on the task itself.
            var oldStatus = task.Status;
            var statusChanged = task.Status != model.Status;

            if (statusChanged)
            {
                changes.Append($"Status: '{task.Status}'" +
                            $" → '{model.Status}'. ");
                task.Status = model.Status;

                task.CompletedDate = model.Status == TaskStatus.Complete
                    ? DateTime.Now
                    : (DateTime?)null;
            }

            var before = DescribeClassification(task);

            task.TaskClassificationId = model.TaskClassificationId;

            if (!ApplyClassificationLink(task, model.TaskClassificationId,
                    model.TaskListId, model.ClipItemKey))
                return View(model);

            var after = DescribeClassification(task);
            if (before != after)
                changes.Append($"Classification: '{before}' → '{after}'. ");

            task.LastModifiedDate = DateTime.Now;
            _db.SaveChanges();

            if (changes.Length > 0)
                taskService.LogHistory(
                    task.TaskId,
                    "Updated",
                    "Previous values",
                    changes.ToString(),
                    adminId
                    );

            if (statusChanged)
                taskService.LogStatusChange(
                    task.TaskId, oldStatus, model.Status,
                    adminId, model.StatusRemark);

            TempData["SuccessMessage"] = "Task updated successfully!";
            return RedirectToAction("Tasks");
                
        }

        // ══════════════════════════════════════════
        // DELETE TASK — POST
        // ══════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteTask(int id)
        {
            var task = _db.TaskItems.Find(id);
            if (task == null) return HttpNotFound();

            var adminId = System.Web.HttpContext.Current.User
                .Identity.GetUserId();
            var taskService = new TaskService(_db);
            var taskTitle = task.Title;

            taskService.LogHistory(
                task.TaskId,
                "Deleted",
                $"Task '{task.Title}'",
                null,
                adminId
            );

            _db.TaskItems.Remove(task);
            _db.SaveChanges();

            TempData["SuccessMessage"] = 
                $"Task '{taskTitle}' deleted successfully!";
            return RedirectToAction("Tasks");
        }

        // ══════════════════════════════════════════
        // CALENDAR — EVERY EMPLOYEE'S TASKS BY DAY
        // ══════════════════════════════════════════
        //
        // The manager's view of the same tasks the list on /Admin/Tasks holds:
        // what is committed, when, by whom, and for which plant. Tasks sit on
        // their DueDate — which for a daily task is the day it is worked, so
        // both kinds land on the day the work actually belongs to.
        //
        // Filters are deliberately the same three a manager asks by: plant,
        // employee, status. Classification is not among them; it says what
        // kind of work a task is, which is a question for the list, not for
        // "who is doing what this week".
        public ActionResult Calendar(
            string view = AdminCalendarViewModel.Weekly,
            DateTime? date = null,
            int? plantId = null,
            string employeeId = "",
            string status = "")
        {
            ViewBag.PageTitle = "Calendar";
            ViewBag.PageSubtitle =
                "Every employee's tasks, by day, week or month.";

            // Same sweep the task list does: a calendar that still shows last
            // week's work as Pending would be reporting a state nobody holds.
            var taskService = new TaskService(_db);
            taskService.UpdateOverdueTasks();

            view = NormaliseCalendarView(view);

            var target = (date?.Date ?? DateTime.Today);
            DateTime startOfTheMonth, endOfTheMonth;
            CalendarPeriod(view, target, out startOfTheMonth, out endOfTheMonth);

            var vm = new AdminCalendarViewModel
            {
                View = view,
                TargetDate = target,
                StartOfTheMonth = startOfTheMonth,
                EndOfTheMonth = endOfTheMonth,
                PlantId = plantId,
                EmployeeId = employeeId,
                Status = status,
                Plants = taskService.GetPlants(),
                Employees = GetEmployeeSelectList()
            };

            switch (view)
            {
                case AdminCalendarViewModel.Daily:
                    vm.PrevDate = target.AddDays(-1);
                    vm.NextDate = target.AddDays(1);
                    break;

                case AdminCalendarViewModel.Monthly:
                    vm.PrevDate = target.AddMonths(-1);
                    vm.NextDate = target.AddMonths(1);
                    break;

                default:
                    vm.PrevDate = target.AddDays(-7);
                    vm.NextDate = target.AddDays(7);
                    break;
            }

            // The whole of the last day, not midnight on it: DueDate is a
            // DATETIME and a task saved with a time on it would fall outside
            // the frame it is plainly inside.
            var endOfPeriod = endOfTheMonth.AddDays(1).AddTicks(-1);

            var query = TaskQuery()
                .Where(t => t.DueDate >= startOfTheMonth
                         && t.DueDate <= endOfPeriod);

            if (!string.IsNullOrEmpty(employeeId))
                query = query.Where(t => t.AssignedToUserId == employeeId);

            if (!string.IsNullOrEmpty(status) &&
                Enum.TryParse<TaskStatus>(status, out var statusEnum))
                query = query.Where(t => t.Status == statusEnum);

            if (plantId.HasValue)
            {
                // A task has no plant of its own; it belongs to a plant
                // through whoever it is assigned to. Resolved to user ids
                // first so the filter is one IN clause rather than a join
                // across the CLIP schema on every row.
                var atPlant = taskService.UserIdsAtPlant(plantId.Value);
                query = query.Where(t => atPlant.Contains(t.AssignedToUserId));
            }

            var tasks = query.OrderBy(t => t.DueDate).ToList();

            var items = BuildCalendarItems(tasks, vm.Plants);

            var byDay = items
                .GroupBy(i => i.Task.DueDate.Date)
                .ToDictionary(g => g.Key, g => g.ToList());

            for (var d = startOfTheMonth; d <= endOfTheMonth; d = d.AddDays(1))
            {
                vm.Days.Add(new CalendarDayViewModel
                {
                    Date = d,
                    Items = byDay.ContainsKey(d.Date)
                        ? byDay[d.Date]
                        : new List<CalendarTaskViewModel>()
                });
            }

            vm.Legend = BuildCalendarLegend(items);

            return View(vm);
        }

        private static string NormaliseCalendarView(string view)
        {
            switch ((view ?? "").ToLowerInvariant())
            {
                case AdminCalendarViewModel.Daily:
                    return AdminCalendarViewModel.Daily;
                case AdminCalendarViewModel.Monthly:
                    return AdminCalendarViewModel.Monthly;
                default:
                    return AdminCalendarViewModel.Weekly;
            }
        }

        // The days the frame covers. Weeks start on Monday, which is what the
        // month grid's column headers assume as well.
        private static void CalendarPeriod(string view, DateTime target,
            out DateTime periodStart, out DateTime periodEnd)
        {
            switch (view)
            {
                case AdminCalendarViewModel.Daily:
                    periodStart = target;
                    periodEnd = target;
                    return;

                case AdminCalendarViewModel.Monthly:
                    periodStart = new DateTime(target.Year, target.Month, 1);
                    periodEnd = periodStart.AddMonths(1).AddDays(-1);
                    return;

                default:
                    var offset = (int)target.DayOfWeek - (int)DayOfWeek.Monday;
                    if (offset < 0) offset += 7;
                    periodStart = target.AddDays(-offset);
                    periodEnd = periodStart.AddDays(6);
                    return;
            }
        }

        // Wraps each task with the plant it belongs to and the hours it runs.
        //
        // The plant comes from the assignee's CLIP.UserPlants rows, which is
        // EHS_PORTAL's record and incomplete — an employee with no rows there
        // shows as "No plant" rather than dropping off the calendar, because a
        // task nobody can see is worse than one whose plant is unknown.
        private List<CalendarTaskViewModel> BuildCalendarItems(
            List<TaskItem> tasks, List<Plant> plants)
        {
            var list = BuildTaskList(tasks);
            var slots = CalendarPalette.SlotsFor(plants);
            var plantsByUser = PlantsByUser(
                tasks.Select(t => t.AssignedToUserId));
            var scheduleByTask = tasks.ToDictionary(t => t.TaskId, t => t);

            return list.Select(t =>
            {
                var task = scheduleByTask[t.TaskId];

                var userPlants = plantsByUser.ContainsKey(t.AssignedToUserId)
                    ? plantsByUser[t.AssignedToUserId]
                    : new List<Plant>();

                // The first by name carries the colour when someone is mapped
                // to several plants; the rest are named in the detail panel.
                var plant = userPlants.FirstOrDefault();

                return new CalendarTaskViewModel
                {
                    Task = t,
                    PlantId = plant?.Id,
                    PlantName = plant?.PlantName,
                    OtherPlantNames = userPlants.Skip(1)
                        .Select(p => p.PlantName)
                        .ToList(),
                    ColorIndex = plant != null && slots.ContainsKey(plant.Id)
                        ? slots[plant.Id]
                        : 0,
                    IsDaily = task.ScheduleType == TaskScheduleType.Daily,
                    PeriodText = PeriodText(task),
                    ScheduleText = TaskPeriod.Describe(task)
                };
            }).ToList();
        }

        // "08:00 – 17:00", or empty when the task records no hours.
        private static string PeriodText(TaskItem task)
        {
            if (!task.PeriodStart.HasValue || !task.PeriodEnd.HasValue)
                return "";

            return string.Format("{0:hh\\:mm} – {1:hh\\:mm}",
                task.PeriodStart.Value, task.PeriodEnd.Value);
        }

        // Which plants each of these users works at, in one query rather than
        // one per task.
        private Dictionary<string, List<Plant>> PlantsByUser(
            IEnumerable<string> userIds)
        {
            var ids = userIds.Distinct().ToList();

            return _db.UserPlants
                .Where(up => ids.Contains(up.UserId))
                .Select(up => new { up.UserId, up.Plant })
                .ToList()
                .GroupBy(x => x.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.Plant)
                          .Where(p => p != null)
                          .OrderBy(p => p.PlantName)
                          .ToList());
        }

        // The colours actually on screen, with what each one accounts for.
        // "No plant" sorts last: it is a gap in EHS_PORTAL's records rather
        // than a place, and reads oddly among the real ones.
        private static List<CalendarPlantViewModel> BuildCalendarLegend(
            List<CalendarTaskViewModel> items)
        {
            return items
                .GroupBy(i => new { i.PlantId, i.PlantLabel, i.ColorIndex })
                .Select(g => new CalendarPlantViewModel
                {
                    PlantId = g.Key.PlantId,
                    Name = g.Key.PlantLabel,
                    ColorIndex = g.Key.ColorIndex,
                    Count = g.Count()
                })
                .OrderBy(p => p.PlantId.HasValue ? 0 : 1)
                .ThenBy(p => p.Name)
                .ToList();
        }

        // ══════════════════════════════════════════
        // TASK HISTORY
        // ══════════════════════════════════════════
        public ActionResult TaskHistory()
        {
            ViewBag.PageTitle    = "Task History";
            ViewBag.PageSubtitle = "Audit trail of all task changes.";

            var history = _db.TaskHistories
                .OrderByDescending(h => h.ChangedDate)
                .ToList()
                .Select(h => new TaskHistoryItemViewModel
                {
                    HistoryId = h.HistoryId,
                    TaskId = h.TaskId,
                    TaskTitle = h.Task?.Title ?? "-",
                    Action = h.Action,
                    OldValue = h.OldValue,
                    NewValue = h.NewValue,
                    Remark = h.Remark,
                    ChangedByName = h.ChangedByUser?.UserName ?? "-",
                    ChangedDate = h.ChangedDate
                })
                .ToList();

            return View(history);
        }



        // ══════════════════════════════════════════
        // PENDING REPORTS
        // ══════════════════════════════════════════

        public ActionResult PendingReports()
        {
            ViewBag.PageTitle = "Pending Approvals";
            ViewBag.PageSubtitle = "Review and approve submitted reports.";

            var reports = _db.Reports
                .Where(r => r.Status == ReportStatus.Submitted)
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

        // ══════════════════════════════════════════
        // APPROVED REPORTS
        // ══════════════════════════════════════════

        public ActionResult ApprovedReports()
        {
            ViewBag.PageTitle = "Approved Reports";
            ViewBag.PageSubtitle = "View all approved employee reports.";

            var reports = _db.Reports
                .Where(r => r.Status == ReportStatus.Approved
                         || r.Status == ReportStatus.Rejected)
                .OrderByDescending(r => r.ApprovedDate)
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

        // ══════════════════════════════════════════
        // VIEW REPORT DETAILS (admin)
        // ══════════════════════════════════════════

        public ActionResult ReviewReport(int id)
        {
            ViewBag.PageTitle = "Review Report";
            ViewBag.PageSubtitle = "Review employee report.";

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

        // ══════════════════════════════════════════
        // APPROVE REPORT — POST
        // ══════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ApproveReport(int id)
        {
            var report = _db.Reports.Find(id);
            if (report == null) return HttpNotFound();

            report.Status = ReportStatus.Approved;
            report.ApprovedDate = DateTime.Now;
            report.RejectionReason = null;
            report.LastModifiedDate = DateTime.Now;
            _db.SaveChanges();

            TempData["SuccessMessage"] =
                $"{report.User?.UserName}'s report approved!";
            return RedirectToAction("PendingReports");
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
            return RedirectToAction("PendingReports");
        }

        // ══════════════════════════════════════════
        // DOWNLOAD REPORT PDF (admin)
        // ══════════════════════════════════════════

        public ActionResult DownloadReport(int id)
        {
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

        // ══════════════════════════════════════════
        // AJAX — Get Task Lists by Classification
        // ══════════════════════════════════════════
        [HttpGet]
        public JsonResult GetTasksByClassification(int classificationId)
        {
            var taskService = new TaskService(_db);
            var tasks = taskService.GetTaskList(classificationId);
            var result = tasks.Select(t => new
            {
                value = t.TaskListId,
                text = t.Name
            });
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        // ══════════════════════════════════════════
        // HELPER
        // ══════════════════════════════════════════

        // The period rules live in TaskPeriod because both the admin and
        // employee forms answer to them; the controller only reports what they
        // return. Whether a period is required depends on ScheduleType, which
        // no data annotation can see.
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

        // Tasks with the lookups the list view model needs already joined.
        private IQueryable<TaskItem> TaskQuery()
        {
            return _db.TaskItems
                .Include(t => t.TaskClassification)
                .Include(t => t.TaskList)
                .Include(t => t.CreatedByUser)
                .Include(t => t.AssignedToUser);
        }

        // Projects tasks into the list view model, resolving each task's linked
        // CLIP record and its status action flow in batched queries.
        private List<TaskListItemViewModel> BuildTaskList(List<TaskItem> tasks)
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
                    TaskId               = t.TaskId,
                    Title                = t.Title,
                    Description          = t.Description,
                    TaskClassificationId = t.TaskClassificationId,
                    ClassificationName   = t.TaskClassification?.Name,
                    TaskListId           = t.TaskListId,
                    TaskListName         = t.TaskList?.Name,
                    SubTaskId            = t.SubTaskId,
                    Status               = t.Status,
                    Priority             = t.Priority,
                    DueDate              = t.DueDate,
                    CreatedDate          = t.CreatedDate,
                    AssignedDate         = t.AssignedDate,
                    CompletedDate        = t.CompletedDate,
                    AssignedToUserId     = t.AssignedToUserId,
                    AssignedToName       = t.AssignedToUser?.UserName ?? "-",
                    AssignedToEmpID      = t.AssignedToUser?.EmpID ?? "-",
                    CreatedByName        = t.CreatedByUser?.UserName ?? "-",
                    ClipItem             = clipItems.ContainsKey(t.TaskId)
                                               ? clipItems[t.TaskId]
                                               : null,
                    StatusActions        = flow,
                    LatestStatusRemark   = flow.LastOrDefault()
                };
            }).ToList();
        }

        private List<ClassificationOption> GetClassificationOptions()
        {
            return TaskDisplay.ToOptions(_db.TaskClassifications
                .OrderBy(c => c.TaskClassificationId)
                .ToList());
        }

        // Classifications, task types, and every CLIP record, filtered by plant
        // in the picker itself. No longer depends on which employee is selected
        // — the list is the same whoever the task goes to.
        private TaskFormOptions GetFormOptions()
        {
            var clip = Clip;

            return new TaskFormOptions
            {
                Classifications = GetClassificationOptions(),
                TaskLists = TaskDisplay.ToOptions(_db.TaskLists
                    .OrderBy(l => l.Name)
                    .ToList()),
                ClipItems = clip.GetAllItems()
            };
        }

        // Sets the task type and the optional CLIP attachment from the form.
        // Returns false (with a model error) only when the picked record does
        // not exist — picking none is ordinary.
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

        // "CLIP / Plant Monitoring / #12" — a stable string for the audit trail.
        private string DescribeClassification(TaskItem task)
        {
            return Clip.DescribeClassification(task);
        }

        private void PopulateTaskClassification(int? selectedId = null)
        {
            var taskService = new TaskService(_db);
            var classifications = taskService.GetTaskClassification();
            ViewBag.ClassificationList = new SelectList(
                classifications.Select(c => new
                {
                    Value = c.TaskClassificationId,
                    Text = c.Name
                }), "Value", "Text", selectedId);
        }

        private void PopulateTaskList(int classificationId, int? selectedId = null)
        {
            var taskService = new TaskService(_db);
            var tasks = taskService.GetTaskList(classificationId);
            ViewBag.TaskList = new SelectList(
                tasks.Select(t => new
                {
                    Value = t.TaskListId,
                    Text = t.Name
                }), "Value", "Text", selectedId);
        }

        private List<EmployeeSelectItem> GetEmployeeSelectList()
        {
            return _db.Users
                .Where(u => !u.IsAdmin && u.IsActive)
                .OrderBy(u => u.UserName)
                .Select(u => new EmployeeSelectItem
                {
                    UserId = u.Id,
                    FullName = u.UserName,
                    EmpID = u.EmpID
                })
                .ToList();
        }
        private decimal CalculateOnTimeRate(string userId)
        {
            var completed = _db.TaskItems
                .Where(t => t.AssignedToUserId == userId
                         && t.Status == TaskStatus.Complete)
                .ToList();

            if (completed.Count == 0) return 0;

            var onTime = completed
                .Count(t => t.CompletedDate.HasValue
                         && t.CompletedDate <= t.DueDate);

            return Math.Round((decimal)onTime / completed.Count * 100, 1);
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
    }
}