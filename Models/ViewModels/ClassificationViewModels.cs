using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ESTAFF.Models.Data;

namespace ESTAFF.Models.ViewModels
{
    // ══════════════════════════════════════════════════════════════
    // Task taxonomy maintenance.
    //
    // The assign/edit task forms read ESTAFF.TaskClassifications and
    // ESTAFF.TaskLists straight out of the database, so these screens are the
    // supported way to change what those dropdowns offer.
    //
    // "Task type" is what the UI calls a TaskList row - the recurring job
    // within a classification. The entity keeps its original name.
    // ══════════════════════════════════════════════════════════════

    // One row on the classification list.
    public class ClassificationListItemViewModel
    {
        public int TaskClassificationId { get; set; }
        public string Name { get; set; }

        // Which part of the printed statutory ESH report these tasks appear
        // under. Null means nobody has said, and the report files them under
        // EshSections.Default - worth flagging on the list, because a mapping
        // is invisible until someone reads the PDF.
        public EshSection? ReportSection { get; set; }

        public bool IsMapped => ReportSection.HasValue;

        public string ReportSectionLabel =>
            EshSections.ShortLabel(ReportSection);

        // What the report will actually do with an unmapped row.
        public string ReportSectionFallback =>
            EshSections.ShortLabel(EshSections.Default);

        // How much depends on this row. Both have to be zero before it can be
        // removed, and both are worth showing before anyone tries.
        public int TaskTypeCount { get; set; }
        public int TaskCount { get; set; }

        public bool CanDelete => TaskCount == 0 && TaskTypeCount == 0;

        public string Slug => TaskDisplay.ClassificationSlug(Name);
        public string Icon => TaskDisplay.ClassificationIcon(Name);

        // Why the delete button is disabled, phrased for the person reading it.
        public string DeleteBlockedReason
        {
            get
            {
                if (TaskCount > 0)
                    return "In use by " + Count(TaskCount, "task") + ".";

                if (TaskTypeCount > 0)
                    return "Remove its " + Count(TaskTypeCount, "task type")
                         + " first.";

                return null;
            }
        }

        private static string Count(int value, string noun)
        {
            return value + " " + noun + (value == 1 ? "" : "s");
        }
    }

    // Create and edit a classification. One shape for both, because the two
    // forms ask for exactly the same thing.
    public class ClassificationFormViewModel
    {
        public int? TaskClassificationId { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, ErrorMessage =
            "Name cannot be longer than 100 characters")]
        [Display(Name = "Classification Name")]
        public string Name { get; set; }

        // Which numbered part of the statutory ESH report the tasks under this
        // classification are printed in. Optional: a classification can be
        // created before anyone has decided, and the report has a fallback.
        [Display(Name = "Statutory Report Section")]
        public EshSection? ReportSection { get; set; }

        // The sections offered on the form - only the ones that read their rows
        // from tasks. Sections 3 and 6 print blank statutory grids and have
        // nowhere to put a task, so they are never offered.
        public IEnumerable<EshSectionInfo> SectionChoices =>
            EshSections.Mappable();

        // What happens to tasks under an unmapped classification, said on the
        // form rather than discovered in a printed return.
        public string UnmappedFallbackLabel =>
            EshSections.ShortLabel(EshSections.Default);

        // Populated on edit only - a classification has to exist before task
        // types can hang off it.
        public List<TaskTypeRowViewModel> TaskTypes { get; set; }
            = new List<TaskTypeRowViewModel>();

        public bool IsNew => !TaskClassificationId.HasValue;

        public string Slug => TaskDisplay.ClassificationSlug(Name);
        public string Icon => TaskDisplay.ClassificationIcon(Name);
    }

    // One task type under a classification, as shown on the edit screen.
    public class TaskTypeRowViewModel
    {
        public int TaskListId { get; set; }
        public int TaskClassificationId { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }

        public int TaskCount { get; set; }

        public bool CanDelete => TaskCount == 0;
    }

    // The add/edit form for a task type. Posted from the classification edit
    // screen, one form per row.
    //
    // No validation attributes here on purpose: these forms redirect back to
    // the edit screen, and ModelState does not survive a redirect. The rules
    // live in ClassificationsController.ValidateTaskType, which reports them
    // through TempData - one place rather than two that can disagree.
    public class TaskTypeFormViewModel
    {
        public int TaskListId { get; set; }
        public int TaskClassificationId { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }

        public bool IsNew => TaskListId == 0;
    }
}
