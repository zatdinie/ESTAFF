using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESTAFF.Models.Data
{
    // The EHS work stream a task belongs to. A lookup table rather than an enum
    // because the taxonomy is maintained as data (see ESTAFF.TaskClassifications):
    //   1 Chemical & Legal   2 DOSH / BOMBA / DOE   3 Environmental
    //
    // Every row here is an ordinary work stream. There is no longer a
    // classification with special meaning: attaching a CLIP record used to
    // require filing the task under a classification named "CLIP", which meant
    // the classification had to describe where the work came from instead of
    // what kind of work it was. A CLIP record is now attached to a task
    // directly (TaskItem.ClipItemKind / SubTaskId), independently of this.
    [Table("TaskClassifications", Schema = "ESTAFF")]
    public class TaskClassification
    {
        [Key]
        public int TaskClassificationId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        // Which part of the statutory ESH monthly report the tasks under this
        // classification are printed in. Maintained on the classification
        // rather than the task because the mapping is a property of the work
        // stream, not of one job: everything under "DOSH / BOMBA / DOE" is a
        // compliance activity whoever raised it.
        //
        // Nullable because a classification added before this existed has not
        // been mapped yet, and because an admin can add one and map it later.
        // Unmapped tasks print under EshSections.Default rather than vanishing
        // from a return that has to account for the month.
        [Display(Name = "Report Section")]
        public EshSection? ReportSection { get; set; }

        public virtual ICollection<TaskList> TaskLists { get; set; }
        public virtual ICollection<TaskItem> TaskItems { get; set; }
    }
}
