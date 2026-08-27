using System;
using System.Collections.Generic;
using System.Linq;
using ESTAFF.Models.Data;

namespace ESTAFF.Models.ViewModels
{
    // ClipItemKind lives in Models.Data: it is written to TaskItem, not just
    // displayed.

    // How close an item is to its expiry. Drives the colour/urgency treatment in
    // the CLIP picker and on the task cards.
    public enum ClipUrgency
    {
        None        = 0,   // no expiry date recorded
        Active      = 1,
        ExpiringSoon = 2,
        Expired     = 3
    }

    // A single CLIP record (Certificate of Fitness or Plant Monitoring) flattened
    // into one shape so both kinds can share the same dropdown and badges.
    public class ClipItemViewModel
    {
        public ClipItemKind Kind { get; set; }
        public int Id { get; set; }

        public int PlantId { get; set; }
        public string PlantName { get; set; }

        // COF: registration number.  Plant Monitoring: monitoring name.
        public string Title { get; set; }

        // COF: machine name.  Plant Monitoring: area.
        public string Subtitle { get; set; }

        public DateTime? ExpiryDate { get; set; }

        // "Expired" / "Expiring Soon" / "Active" / "No Expiry" — mirrors CLIP.
        public string ExpiryStatus { get; set; }

        // Plant Monitoring only: "Not Started" / "Quotation Requested" / ...
        public string ProcessStatus { get; set; }

        public ClipUrgency Urgency { get; set; }

        // ── Supporting evidence ────────────────────────────────
        //
        // How far CLIP says the work on this record has actually got. A task
        // that covers a CLIP record is reporting on work tracked in another
        // system, so the record's own progress is the evidence behind whatever
        // the task claims - and it is read live rather than copied, because
        // CLIP is where that work is really recorded.

        // Plant Monitoring only: the three phases CLIP tracks, always all
        // three, in order, including ones not started. A phase that has not
        // begun is evidence too - it is what "still outstanding" looks like.
        public List<ClipPhaseViewModel> Phases { get; set; }
            = new List<ClipPhaseViewModel>();

        // Notes recorded against the record in CLIP itself (monitoring only).
        public string Remarks { get; set; }

        // COF only: the certificate's own fields, which are its whole progress
        // story - a certificate is valid or it is not.
        public string RecordStatus { get; set; }
        public string Location { get; set; }
        public string Department { get; set; }

        // Whether there is any progress worth printing. A monitoring record
        // nobody has started on has nothing to evidence beyond its expiry,
        // which the header line already carries.
        public bool HasProgress
        {
            get
            {
                return Phases.Any(p => p.State != ClipPhaseState.NotStarted)
                    || !string.IsNullOrWhiteSpace(Remarks);
            }
        }

        // The phases that have actually happened, which is what evidences the
        // task. Phases still to come are summarised separately rather than
        // given a line each.
        public List<ClipPhaseViewModel> StartedPhases
        {
            get
            {
                return Phases
                    .Where(p => p.State != ClipPhaseState.NotStarted)
                    .ToList();
            }
        }

        public List<ClipPhaseViewModel> OutstandingPhases
        {
            get
            {
                return Phases
                    .Where(p => p.State == ClipPhaseState.NotStarted)
                    .ToList();
            }
        }

        // Stable identifier posted by the CLIP picker, e.g. "COF:14" / "PM:3".
        // Carries the kind as well as the id because both CLIP tables number
        // their rows independently — the id alone is ambiguous.
        public string Key
        {
            get
            {
                return (Kind == ClipItemKind.COF ? "COF" : "PM") + ":" + Id;
            }
        }

        public string KindLabel
        {
            get
            {
                return Kind == ClipItemKind.COF
                    ? "Certificate of Fitness"
                    : "Plant Monitoring";
            }
        }

        public string KindShortLabel
        {
            get { return Kind == ClipItemKind.COF ? "COF" : "Monitoring"; }
        }

        public string KindIcon
        {
            get
            {
                return Kind == ClipItemKind.COF
                    ? "fa-certificate"
                    : "fa-gauge-high";
            }
        }

        // Negative once the item is past its expiry date.
        public int? DaysToExpiry
        {
            get
            {
                if (!ExpiryDate.HasValue) return null;
                return (int)Math.Round(
                    (ExpiryDate.Value.Date - DateTime.Today).TotalDays);
            }
        }

        // "expired" / "soon" / "active" / "none" — used as a CSS modifier suffix.
        public string UrgencyClass
        {
            get
            {
                switch (Urgency)
                {
                    case ClipUrgency.Expired:      return "expired";
                    case ClipUrgency.ExpiringSoon: return "soon";
                    case ClipUrgency.Active:       return "active";
                    default:                       return "none";
                }
            }
        }

        // Short human phrasing of the countdown, e.g. "Expired 12 days ago".
        public string ExpiryText
        {
            get
            {
                var days = DaysToExpiry;
                if (!days.HasValue) return "No expiry date";

                if (days.Value < 0)
                {
                    var overdue = -days.Value;
                    return overdue == 1
                        ? "Expired 1 day ago"
                        : "Expired " + overdue + " days ago";
                }

                if (days.Value == 0) return "Expires today";
                if (days.Value == 1) return "Expires tomorrow";
                return "Expires in " + days.Value + " days";
            }
        }

        public string ExpiryDateText
        {
            get
            {
                return ExpiryDate.HasValue
                    ? ExpiryDate.Value.ToString("dd MMM yyyy")
                    : "—";
            }
        }

        // One-line label for the native <select> fallback.
        public string OptionLabel
        {
            get
            {
                var label = KindShortLabel + " · " + Title;
                if (!string.IsNullOrWhiteSpace(Subtitle))
                    label += " (" + Subtitle + ")";
                return label + " — " + ExpiryStatus + " · " + ExpiryText;
            }
        }
    }

    public enum ClipPhaseState
    {
        NotStarted = 0,
        InProgress = 1,
        Complete   = 2
    }

    // One phase of the plant monitoring workflow CLIP tracks, read straight off
    // CLIP.PlantMonitoring. The three phases and their columns are EHS_PORTAL's
    // design, not ours:
    //
    //   Quotation        QuoteDate  -> QuoteCompleteDate   QuoteUserAssign / QuoteDoc
    //   Preparation      EprDate    -> EprCompleteDate     EprUserAssign   / EprDoc
    //   Work Execution   WorkDate   -> WorkCompleteDate    WorkUserAssign  / WorkDoc
    //                    (with WorkSubmitDate in between)
    //
    // QuoteSubmitDate and EprSubmitDate exist in CLIP but are not mapped by
    // ESTAFF's projection, so only the work phase has a submitted date here.
    public class ClipPhaseViewModel
    {
        public string Name { get; set; }

        public DateTime? StartedDate { get; set; }

        // Work execution only; null on the other two.
        public DateTime? SubmittedDate { get; set; }

        public DateTime? CompletedDate { get; set; }

        // Who CLIP has the phase assigned to.
        public string AssignedTo { get; set; }

        // The document CLIP holds against the phase - the quotation, the ePR,
        // the completed work report. Stored as a path; only the file name is
        // shown, because the path is EHS_PORTAL's business and the name is what
        // someone would ask for.
        public string Document { get; set; }

        public ClipPhaseState State
        {
            get
            {
                if (CompletedDate.HasValue) return ClipPhaseState.Complete;
                if (StartedDate.HasValue || SubmittedDate.HasValue)
                    return ClipPhaseState.InProgress;
                return ClipPhaseState.NotStarted;
            }
        }

        public string StateLabel
        {
            get
            {
                switch (State)
                {
                    case ClipPhaseState.Complete:   return "Completed";
                    case ClipPhaseState.InProgress: return "In progress";
                    default:                        return "Not started";
                }
            }
        }

        // "complete" / "progress" / "none" - a CSS modifier suffix.
        public string StateClass
        {
            get
            {
                switch (State)
                {
                    case ClipPhaseState.Complete:   return "complete";
                    case ClipPhaseState.InProgress: return "progress";
                    default:                        return "none";
                }
            }
        }

        public bool HasDocument =>
            !string.IsNullOrWhiteSpace(Document);

        // The file name on its own. CLIP stores a full path, which says more
        // about EHS_PORTAL's disk than about the evidence.
        public string DocumentName
        {
            get
            {
                if (!HasDocument) return null;

                var trimmed = Document.Trim().TrimEnd('/', '\\');
                var cut = trimmed.LastIndexOfAny(new[] { '/', '\\' });

                return cut >= 0 && cut < trimmed.Length - 1
                    ? trimmed.Substring(cut + 1)
                    : trimmed;
            }
        }

        // "Raised 02 Jun 2026 · completed 09 Jun 2026" — the dates that
        // actually exist, in the order they happened.
        public string TimelineText
        {
            get
            {
                var parts = new List<string>();

                if (StartedDate.HasValue)
                    parts.Add("raised " + Date(StartedDate));

                if (SubmittedDate.HasValue)
                    parts.Add("submitted " + Date(SubmittedDate));

                if (CompletedDate.HasValue)
                    parts.Add("completed " + Date(CompletedDate));

                return parts.Any()
                    ? string.Join(" · ", parts)
                    : "No dates recorded";
            }
        }

        private static string Date(DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToString("dd MMM yyyy")
                : "—";
        }
    }

    // Everything the _ClipPicker partial needs. The picker renders a real
    // <select> and progressively enhances it, so the form still submits when
    // JavaScript is unavailable.
    public class ClipPickerViewModel
    {
        // Name/id of the posted field, e.g. "ClipItemKey".
        public string FieldName { get; set; } = "ClipItemKey";

        public string SelectedKey { get; set; }

        public List<ClipItemViewModel> Items { get; set; }
            = new List<ClipItemViewModel>();

        // Optional: URL returning the item list as JSON for another employee.
        // Used on the admin Assign/Edit Task pages, where the list depends on
        // which employee the task is assigned to.
        public string ReloadUrl { get; set; }

        // Optional: id of the employee <select> that triggers a reload.
        public string ReloadTriggerId { get; set; }
    }
}
