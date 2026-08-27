using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESTAFF.Models.Data
{
    [Table("Reports", Schema = "ESTAFF")]

    public class Report
    {
        [Key]
        public int ReportId { get; set; }

        [Required]
        [ForeignKey("User")]
        public string UserId { get; set; }

        // The plant this return covers. A report lists every task belonging
        // to the plant for the period, so the plant - not the employee who
        // generated it - is what the document is about.
        //
        // Nullable because the reports submitted before this existed were
        // personal ones covering one employee's tasks. There is no honest
        // plant to backfill them with, so they keep none and read as legacy.
        //
        // No [ForeignKey] attribute and no database constraint: CLIP.Plants
        // belongs to EHS_PORTAL, and a submitted report is a historical record
        // that should survive a plant being removed there. The navigation
        // below is mapped as optional in ApplicationDbContext purely so the
        // name can be read back.
        public int? PlantId { get; set; }

        [Required]
        public ReportType ReportType { get; set; }

        [Required]
        public DateTime PeriodStart { get; set; }

        [Required]
        public DateTime PeriodEnd { get; set; }

        public string Content { get; set; }

        [Required]
        public ReportStatus Status { get; set; } = ReportStatus.Draft;

        public DateTime? SubmittedDate { get; set; }

        public DateTime? ApprovedDate { get; set; }

        public string RejectionReason { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime LastModifiedDate { get; set; } = DateTime.Now;

        // Navigation
        public virtual ApplicationUser User { get; set; }

        // Read-only, like every other CLIP projection. Null when the report is
        // a legacy personal one, or when EHS_PORTAL no longer has the plant.
        public virtual Plant Plant { get; set; }
    }

    public enum ReportType
    {
        weekly = 1,
        monthly = 2,   
    }

    public enum ReportStatus
    {
        Draft = 1, 
        Submitted = 2,
        Approved = 3,
        Rejected = 4
    }
}