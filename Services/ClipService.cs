using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using ESTAFF.Models.Data;
using ESTAFF.Models.ViewModels;

namespace ESTAFF.Services
{
    // Read-only access to the CLIP schema that EHS_PORTAL owns, plus the rules
    // that attach an ESTAFF task to a CLIP record.
    //
    // A task carries an attached record when it has both a ClipItemKind and a
    // SubTaskId. Any task can, whatever its classification: covering a
    // certificate of fitness is something a task *does*, not a category it
    // belongs to. The pair is written straight from the picker; nothing is
    // inferred from the classification or the task type.
    //
    // The expiry rules below deliberately mirror EHS_PORTAL so the two apps
    // never disagree about whether an item is expiring:
    //   Areas/CLIP/Controllers/CertificateOfFitnessController.CalculateStatus  (60 days)
    //   Areas/CLIP/Models/PlantMonitoring.CalculateExpStatus                   (90 days)
    // CLIP stores computed Status/ExpStatus columns, but they only refresh when
    // EHS_PORTAL saves the record, so they go stale. We recompute from the
    // expiry date instead.
    public class ClipService : IDisposable
    {
        public const int CofExpiringSoonDays = 60;
        public const int MonitoringExpiringSoonDays = 90;

        private readonly ApplicationDbContext _db;
        private readonly ClipDbContext _clip;
        private readonly bool _ownsClipContext;

        public ClipService(ApplicationDbContext db)
        {
            _db = db;
            _clip = new ClipDbContext();
            _ownsClipContext = true;
        }

        public ClipService(ApplicationDbContext db, ClipDbContext clip)
        {
            _db = db;
            _clip = clip;
            _ownsClipContext = false;
        }
        
        // ══════════════════════════════════════════
        // URL BUILDING
        // ══════════════════════════════════════════

        public static string GetPortalBaseUrl()
        {
            return ConfigurationManager.AppSettings["PortalBaseUrl"];
        }

        public static string BuildProgressUrl(ClipItemKind kind, int id)
        {
            var baseUrl = GetPortalBaseUrl();
            if (string.IsNullOrWhiteSpace(baseUrl) || id <= 0) return null;

            var path = kind == ClipItemKind.COF
                ? "CLIP/CertificateOfFitness/Details/"
                : "CLIP/PlantMonitoring/Details/";

            Uri result;
            return Uri.TryCreate(new Uri(baseUrl.TrimEnd('/') + "/"), path + id, out result)
                ? result.AbsoluteUri
                : null;
        }

        // ══════════════════════════════════════════
        // STATUS RULES (mirrored from EHS_PORTAL/CLIP)
        // ══════════════════════════════════════════

        public static string CalculateCofStatus(DateTime expiryDate)
        {
            var today = DateTime.Today;

            if (today > expiryDate.Date) return "Expired";
            if (today >= expiryDate.Date.AddDays(-CofExpiringSoonDays))
                return "Expiring Soon";
            return "Active";
        }

        public static string CalculateMonitoringExpStatus(DateTime? expDate)
        {
            if (!expDate.HasValue) return "No Expiry";
            if (expDate.Value < DateTime.Now) return "Expired";
            if (expDate.Value < DateTime.Now.AddDays(MonitoringExpiringSoonDays))
                return "Expiring Soon";
            return "Active";
        }

        public static ClipUrgency ToUrgency(string expiryStatus)
        {
            switch (expiryStatus)
            {
                case "Expired":       return ClipUrgency.Expired;
                case "Expiring Soon": return ClipUrgency.ExpiringSoon;
                case "Active":        return ClipUrgency.Active;
                default:              return ClipUrgency.None;
            }
        }

        // CLIP.UserPlants is no longer read. It is EHS_PORTAL's own record of
        // who works where, it is incomplete (four of seventeen users have no
        // rows, the ESTAFF admin among them), and using it to decide which CLIP
        // records an ESTAFF task could cite left the picker empty for exactly
        // those people. The UserPlant projection stays on the context because
        // it maps a real table, but nothing in ESTAFF depends on it.

        // ══════════════════════════════════════════
        // ITEM LOOKUPS
        // ══════════════════════════════════════════

        // Every CLIP record there is, nearest-expiry first (already-expired
        // items lead, then soonest, undated items last). The picker offers all
        // of them and filters by plant in the UI.
        //
        // It used to offer only the records under the assignee's CLIP.UserPlants
        // rows. That mapping is EHS_PORTAL's own notion of who works where and
        // is incomplete - four of seventeen users have no rows at all, the
        // ESTAFF admin among them - so the picker rendered empty and disabled
        // for exactly the people who needed to attach something, and on the
        // admin's Assign Task form it was empty on load because no assignee had
        // been chosen yet.
        //
        // Attaching evidence is not an access-control decision. Every ESTAFF
        // user is staff, the records are internal EHS data that the report
        // prints anyway, and a task may legitimately concern a plant its
        // assignee is not mapped to.
        public List<ClipItemViewModel> GetAllItems()
        {
            var items = new List<ClipItemViewModel>();

            items.AddRange(_clip.COFs
                .Include(c => c.Plant)
                .ToList()
                .Select(MapCof));

            items.AddRange(_clip.PlantMonitoring
                .Include(pm => pm.Plant)
                .Include(pm => pm.Monitoring)
                .ToList()
                .Select(MapMonitoring));

            return SortByExpiry(items);
        }

        // Nearest expiry first: expired items are the most urgent, undated last.
        public static List<ClipItemViewModel> SortByExpiry(
            IEnumerable<ClipItemViewModel> items)
        {
            return items
                .OrderBy(i => i.ExpiryDate.HasValue ? 0 : 1)
                .ThenBy(i => i.ExpiryDate ?? DateTime.MaxValue)
                .ThenBy(i => i.Title)
                .ToList();
        }

        public ClipItemViewModel GetCofItem(int cofId)
        {
            var cof = _clip.COFs
                .Include(c => c.Plant)
                .FirstOrDefault(c => c.Id == cofId);
            return cof == null ? null : MapCof(cof);
        }

        public ClipItemViewModel GetMonitoringItem(int plantMonitoringId)
        {
            var pm = _clip.PlantMonitoring
                .Include(m => m.Plant)
                .Include(m => m.Monitoring)
                .FirstOrDefault(m => m.Id == plantMonitoringId);
            return pm == null ? null : MapMonitoring(pm);
        }

        // Batch lookup for list pages so rendering N tasks stays at two queries
        // rather than N. Keyed by TaskId.
        public Dictionary<int, ClipItemViewModel> GetItemsForTasks(
            IEnumerable<TaskItem> tasks)
        {
            var list = (tasks ?? Enumerable.Empty<TaskItem>()).ToList();
            var result = new Dictionary<int, ClipItemViewModel>();

            // The attachment is on the task itself, so no lookup is needed to
            // work out what each id points at.
            var linked = list
                .Where(t => t.HasClipItem)
                .Select(t => new
                {
                    Task = t,
                    Kind = t.ClipItemKind.Value,
                    Id   = t.SubTaskId.Value
                })
                .ToList();

            if (!linked.Any()) return result;

            var cofIds = linked.Where(x => x.Kind == ClipItemKind.COF)
                .Select(x => x.Id).Distinct().ToList();
            var pmIds = linked.Where(x => x.Kind == ClipItemKind.PlantMonitoring)
                .Select(x => x.Id).Distinct().ToList();

            var cofs = cofIds.Any()
                ? _clip.COFs
                    .Include(c => c.Plant)
                    .Where(c => cofIds.Contains(c.Id))
                    .ToList().Select(MapCof)
                    .ToDictionary(i => i.Id)
                : new Dictionary<int, ClipItemViewModel>();

            var monitorings = pmIds.Any()
                ? _clip.PlantMonitoring
                    .Include(m => m.Plant)
                    .Include(m => m.Monitoring)
                    .Where(m => pmIds.Contains(m.Id))
                    .ToList().Select(MapMonitoring)
                    .ToDictionary(i => i.Id)
                : new Dictionary<int, ClipItemViewModel>();

            foreach (var entry in linked)
            {
                ClipItemViewModel item = null;

                if (entry.Kind == ClipItemKind.COF)
                    cofs.TryGetValue(entry.Id, out item);
                else
                    monitorings.TryGetValue(entry.Id, out item);

                if (item != null) result[entry.Task.TaskId] = item;
            }

            return result;
        }

        // ══════════════════════════════════════════
        // PICKER KEY  ("COF:14" / "PM:3")
        // ══════════════════════════════════════════

        public static bool TryParseKey(string key, out ClipItemKind kind, out int id)
        {
            kind = ClipItemKind.COF;
            id = 0;

            if (string.IsNullOrWhiteSpace(key)) return false;

            var parts = key.Split(':');
            if (parts.Length != 2) return false;

            if (!int.TryParse(parts[1], out id) || id <= 0) return false;

            switch (parts[0].Trim().ToUpperInvariant())
            {
                case "COF": kind = ClipItemKind.COF; return true;
                case "PM":  kind = ClipItemKind.PlantMonitoring; return true;
                default:    return false;
            }
        }

        public static string BuildKey(ClipItemKind kind, int? subTaskId)
        {
            if (!subTaskId.HasValue) return null;

            return (kind == ClipItemKind.COF ? "COF:" : "PM:") + subTaskId.Value;
        }

        // The picker key for a task that already has a record attached, or null.
        public static string BuildKeyForTask(TaskItem task)
        {
            return task != null && task.HasClipItem
                ? BuildKey(task.ClipItemKind.Value, task.SubTaskId)
                : null;
        }

        // ══════════════════════════════════════════
        // APPLYING A FORM POST
        // ══════════════════════════════════════════

        // Why the posted CLIP key was not accepted. The attachment is optional,
        // so "nothing was picked" is a success with no record attached, not a
        // failure — only Unavailable is worth telling the user about.
        public enum ClipAttachResult
        {
            Cleared,
            Attached,

            // Parsed, but the record does not exist or belongs to a plant the
            // task's owner has no access to.
            Unavailable
        }

        // Attaches the picked CLIP record to the task, or clears the attachment
        // when nothing was picked.
        //
        // The record has to exist — a posted id that names nothing would print
        // as a dangling citation in a statutory report. It no longer has to
        // fall under the task owner's CLIP.UserPlants rows; see GetAllItems for
        // why that restriction was wrong.
        public ClipAttachResult ApplyClipItem(TaskItem task, string key)
        {
            task.ClipItemKind = null;
            task.SubTaskId = null;

            ClipItemKind kind;
            int id;

            // A blank key is the ordinary case: most tasks cover no CLIP
            // record. An unparseable one is treated the same rather than
            // rejected, because the only way to produce one is to post by hand.
            if (!TryParseKey(key, out kind, out id))
                return ClipAttachResult.Cleared;

            var exists = kind == ClipItemKind.COF
                ? _clip.COFs.Any(c => c.Id == id)
                : _clip.PlantMonitoring.Any(m => m.Id == id);

            if (!exists) return ClipAttachResult.Unavailable;

            task.ClipItemKind = kind;
            task.SubTaskId = id;
            return ClipAttachResult.Attached;
        }

        // Sets the task type, then the optional CLIP attachment. The two are
        // independent now: a task type says what kind of job this is, an
        // attached record says which certificate or monitoring row it covers,
        // and any combination of the two is legitimate.
        //
        // Shared by the admin and employee forms so the rules cannot drift.
        public ClipAttachResult TryApplyClassificationLink(TaskItem task,
            int? classificationId, int? taskListId, string clipItemKey)
        {
            task.TaskListId = null;

            // Only accept a task type that actually belongs to the chosen
            // classification, so a stale or hand-edited post cannot cross them.
            if (taskListId.HasValue && classificationId.HasValue)
            {
                var belongs = _db.TaskLists.Any(l =>
                    l.TaskListId == taskListId.Value
                    && l.TaskClassificationId == classificationId.Value);

                if (belongs) task.TaskListId = taskListId;
            }

            return ApplyClipItem(task, clipItemKey);
        }

        // "Environmental / Weekly Patrol / COF #12" — a stable string for the
        // audit trail, so a change of classification, task type or attached
        // record all read the same way in the history.
        public string DescribeClassification(TaskItem task)
        {
            var classification = _db.TaskClassifications
                .FirstOrDefault(c =>
                    c.TaskClassificationId == task.TaskClassificationId);

            var parts = new List<string>
            {
                classification?.Name ?? task.TaskClassificationId.ToString()
            };

            if (task.TaskListId.HasValue)
            {
                var list = _db.TaskLists
                    .FirstOrDefault(l => l.TaskListId == task.TaskListId.Value);
                parts.Add(list?.Name ?? ("#" + task.TaskListId.Value));
            }

            if (task.HasClipItem)
            {
                parts.Add((task.ClipItemKind.Value == ClipItemKind.COF
                              ? "COF #"
                              : "Monitoring #")
                          + task.SubTaskId.Value);
            }

            return string.Join(" / ", parts);
        }

        // ══════════════════════════════════════════
        // MAPPING
        // ══════════════════════════════════════════

        private static ClipItemViewModel MapCof(COF cof)
        {
            var status = CalculateCofStatus(cof.ExpiryDate);

            return new ClipItemViewModel
            {
                Kind         = ClipItemKind.COF,
                Id           = cof.Id,
                PlantId      = cof.PlantId,
                PlantName    = cof.Plant != null ? cof.Plant.PlantName : null,
                Title        = cof.RegistrationNo,
                Subtitle     = !string.IsNullOrWhiteSpace(cof.MachineName)
                                   ? cof.MachineName
                                   : cof.Location,
                ExpiryDate   = cof.ExpiryDate,
                ExpiryStatus = status,
                Urgency      = ToUrgency(status),

                // A certificate has no phases: it is valid or it is not, and
                // where the machine is and who owns it is what identifies the
                // thing the certificate covers.
                RecordStatus = cof.Status,
                Location     = cof.Location,
                Department   = cof.Department
            };
        }

        private static ClipItemViewModel MapMonitoring(PlantMonitoring pm)
        {
            var status = CalculateMonitoringExpStatus(pm.ExpDate);

            return new ClipItemViewModel
            {
                Kind          = ClipItemKind.PlantMonitoring,
                Id            = pm.Id,
                PlantId       = pm.PlantID,
                PlantName     = pm.Plant != null ? pm.Plant.PlantName : null,
                Title         = pm.Monitoring != null
                                    ? pm.Monitoring.MonitoringName
                                    : "Monitoring #" + pm.MonitoringID,
                Subtitle      = !string.IsNullOrWhiteSpace(pm.Area)
                                    ? pm.Area
                                    : (pm.Monitoring != null
                                        ? pm.Monitoring.MonitoringCategory
                                        : null),
                ExpiryDate    = pm.ExpDate,
                ExpiryStatus  = status,
                ProcessStatus = CalculateProcStatus(pm),
                Urgency       = ToUrgency(status),
                Remarks       = pm.Remarks,
                Phases        = BuildPhases(pm)
            };
        }

        // The three phases CLIP tracks against a monitoring record, in the
        // order they happen. All three are returned even when untouched: a
        // phase nobody has started is as much a part of the evidence as one
        // that is finished, because it is what "still outstanding" looks like.
        //
        // Read live rather than copied onto the task. CLIP is where this work
        // is actually recorded, so a snapshot would start lying the moment the
        // vendor moved on - and the whole point of citing it is that it is the
        // other system's account, not ours.
        private static List<ClipPhaseViewModel> BuildPhases(PlantMonitoring pm)
        {
            return new List<ClipPhaseViewModel>
            {
                new ClipPhaseViewModel
                {
                    Name          = "Quotation",
                    StartedDate   = pm.QuoteDate,
                    CompletedDate = pm.QuoteCompleteDate,
                    AssignedTo    = pm.QuoteUserAssign,
                    Document      = pm.QuoteDoc
                },
                new ClipPhaseViewModel
                {
                    Name          = "Preparation (ePR)",
                    StartedDate   = pm.EprDate,
                    CompletedDate = pm.EprCompleteDate,
                    AssignedTo    = pm.EprUserAssign,
                    Document      = pm.EprDoc
                },
                new ClipPhaseViewModel
                {
                    Name          = "Work Execution",
                    StartedDate   = pm.WorkDate,

                    // The only phase whose submitted date ESTAFF's projection
                    // maps; QuoteSubmitDate and EprSubmitDate are not read.
                    SubmittedDate = pm.WorkSubmitDate,
                    CompletedDate = pm.WorkCompleteDate,
                    AssignedTo    = pm.WorkUserAssign,
                    Document      = pm.WorkDoc
                }
            };
        }

        // Mirrors PlantMonitoring.CalculateProcStatus in EHS_PORTAL. Recomputed
        // rather than read from ProcStatus so it cannot drift out of date.
        private static string CalculateProcStatus(PlantMonitoring pm)
        {
            if (pm.WorkCompleteDate.HasValue) return "Completed";
            if (pm.WorkDate.HasValue)         return "Work In Progress";
            if (pm.EprDate.HasValue)          return "ePR Raised";
            if (pm.QuoteDate.HasValue)        return "Quotation Requested";
            return "Not Started";
        }

        public void Dispose()
        {
            if (_ownsClipContext && _clip != null) _clip.Dispose();
        }
    }
}
