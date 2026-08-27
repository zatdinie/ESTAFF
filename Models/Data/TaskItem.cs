using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EHS_PORTAL.Areas.ESTAFF.Models.Data
{
    [Table("TaskItems", Schema = "ESTAFF")]
    public class TaskItem
    {
        [Key]
        public int TaskId { get; set; }

        [Required]
        [StringLength(256)]
        public string Title { get; set; }

        // The property and column stay "Description": renaming them would mean
        // a migration against a database shared with EHS_PORTAL. Only what the
        // user reads changes.
        [Display(Name = "Concern/Issue")]
        public string Description { get; set; }

        [Required]
        public TaskStatus Status { get; set; } = TaskStatus.Pending;

        public TaskPriority? Priority { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        // Which kind of task this is, chosen by whoever raises it. Daily work
        // is done on one named day between two hours; long-term work is
        // tracked to a due date. The difference is what the form asks for: a
        // daily task is asked for its period and nothing else, a long-term one
        // for its due date and nothing else.
        //
        // Not nullable, and existing rows read as LongTerm - a task with a due
        // date and no period is exactly what that means, so nothing is being
        // guessed on their behalf.
        [Required]
        [Display(Name = "Task Type")]
        public TaskScheduleType ScheduleType { get; set; }
            = TaskScheduleType.LongTerm;

        // The day the period falls on, and for a task raised as Daily the day
        // it is due - TaskPeriod.ApplyTo writes DueDate from this, because the
        // form only asks once.
        //
        // Still its own column rather than a reading of DueDate: rows written
        // before that rule can hold a period and a later due date both, and
        // DueDate is what every list, sweep and calendar reads.
        //
        // DATE rather than DATETIME - the time of day is the pair below.
        [Column(TypeName = "date")]
        [DataType(DataType.Date)]
        [Display(Name = "Period Date")]
        public DateTime? PeriodDate { get; set; }

        // The hours of the day the work runs between - 08:00 to 17:00 - on
        // PeriodDate. Both forms offer whole hours only.
        //
        // TimeSpan rather than DateTime because there is no date here to store;
        // EF6 maps it to SQL Server's TIME, so a row cannot carry a stray date
        // component that nothing would ever read.
        //
        // All three are nullable because a long-term task need not have a
        // period at all, and because the tasks already in ESTAFF were written
        // before these columns existed. Which combinations are acceptable is a
        // question about the form, not the column, and lives in TaskPeriod.
        //
        // PeriodEnd earlier than PeriodStart is legitimate and means the work
        // carried past midnight - a night shift of 22:00 to 06:00. Nothing
        // rejects that ordering, so nothing here assumes end > start.
        [Column(TypeName = "time")]
        [Display(Name = "Period Start")]
        public TimeSpan? PeriodStart { get; set; }

        [Column(TypeName = "time")]
        [Display(Name = "Period End")]
        public TimeSpan? PeriodEnd { get; set; }

        // True when the task carries a complete period: a day and both hours.
        // Anything less is treated as none, the same way HasClipItem treats
        // half a link.
        [NotMapped]
        public bool HasPeriod => PeriodDate.HasValue
            && PeriodStart.HasValue
            && PeriodEnd.HasValue;

        // ── Attached CLIP record (optional) ─────────────────────
        //
        // Any task may cover a certificate of fitness or a plant monitoring
        // record, whatever its classification. The pair below is the whole
        // attachment: ClipItemKind says which CLIP table, SubTaskId says which
        // row in it. Both set means attached; either null means not.
        //
        // Deliberately not a foreign key - those tables belong to EHS_PORTAL
        // and ESTAFF only ever reads them.
        //
        // This used to be implied rather than stored: a task was CLIP work when
        // its classification was named "CLIP", and its TaskList name decided
        // which table SubTaskId meant. That made attaching a record a
        // consequence of how the task was filed, so the same certificate could
        // not be covered by a task classified as anything else. Kind is now
        // recorded outright and the classification is left to mean what it
        // says.
        public ClipItemKind? ClipItemKind { get; set; }

        // The id of the attached CLIP row. Keeps the column name SubTaskId: it
        // is in a database shared with EHS_PORTAL, so renaming it would be a
        // migration against another application's neighbourhood for no gain.
        public int? SubTaskId { get; set; }

        // True when the task carries a complete CLIP attachment. Half a link -
        // a kind with no id, or an id with no kind - is treated as none, which
        // is what rows written before ClipItemKind existed look like until
        // Add_Task_ClipItemKind.sql backfills them.
        [NotMapped]
        public bool HasClipItem => ClipItemKind.HasValue && SubTaskId.HasValue;

        // The specific recurring job within the classification. The column keeps
        // the name EF generated from TaskList.TaskItems; mapping it explicitly
        // here lets a task read its own task list without loading the collection.
        [Column("TaskList_TaskListId")]
        public int? TaskListId { get; set; }

        [Required]
            [ForeignKey("TaskClassification")]
        public int TaskClassificationId { get; set; }

        [Required]
        [ForeignKey("AssignedToUser")]
        public string AssignedToUserId { get; set; }

        [Required]
        [ForeignKey("CreatedByUser")]
        public string CreatedByUserId { get; set; }

        public DateTime AssignedDate { get; set; } = DateTime.Now;

        public DateTime? CompletedDate { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime LastModifiedDate { get; set; } = DateTime.Now;


        public virtual ApplicationUser AssignedToUser { get; set; }
        public virtual ApplicationUser CreatedByUser { get; set; }
        public virtual TaskClassification TaskClassification { get; set; }
        public virtual TaskList TaskList { get; set; }
        public virtual ICollection<TaskHistory> Histories { get; set; } = new List<TaskHistory>();
    }

    public enum TaskStatus
    {
        Pending = 1,
        InProgress = 2,
        Complete = 3,
        Overdue = 4
    }

    public enum TaskPriority
    {
        Low = 1,
        Medium = 2,
        High = 3
    }

    // Whether a task is a day's work or something tracked to a deadline.
    //
    // Both kinds carry a DueDate - a daily task's is the day it is worked. The
    // period is what differs: Daily requires one, LongTerm carries none.
    //
    // Still stored rather than inferred from whether the period columns are
    // filled in: rows written while a long-term task could also record hours
    // would read as Daily, and a daily task whose period predates these
    // columns would read as long term.
    public enum TaskScheduleType
    {
        Daily = 1,
        LongTerm = 2
    }

}
