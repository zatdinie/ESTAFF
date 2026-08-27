using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EHS_PORTAL.Areas.ESTAFF.Models.Data
{
    [Table("TaskHistories", Schema = "ESTAFF")]

    public class TaskHistory
    {
        [Key]
        public int HistoryId { get; set; }

        [Required]
        [ForeignKey("Task")]
        public int TaskId { get; set; }

        [Required]
        [StringLength(50)]
        public string Action { get; set; }

        public string OldValue { get; set; }

        public string NewValue { get; set; }

        // Free-text note the user attached to this change (currently captured on
        // status changes so the latest one can be surfaced on the task itself).
        [StringLength(500)]
        public string Remark { get; set; }

        [Required]
        [ForeignKey("ChangedByUser")]
        public string ChangedByUserId { get; set; }

        public DateTime ChangedDate { get; set; } = DateTime.Now;

        public virtual TaskItem Task { get; set; }
        public virtual ApplicationUser ChangedByUser { get; set; }
    }
}