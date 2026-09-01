using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EHS_PORTAL.Areas.ESTAFF.Models.Data
{
    [Table("ReportApprovals", Schema = "ESTAFF")]

    public class ReportApproval
    {
        [Key]
        public int ApprovalId { get; set; }
        public int ReportId { get; set; }
        public string ReporterId { get; set; }
        public DateTime SubmittedDate { get; set; }
        public ApprovalStatus ApprovalStatus { get; set; }
        public string ApproverId { get; set; }
        public DateTime? DateApproved { get; set; }
        public string Comments { get; set; }

        public virtual Report Report { get; set; }
        public virtual ApplicationUser Reporter { get; set; }
        public virtual ApplicationUser Approver { get; set; }
    }

    public enum ApprovalStatus
    {
        Pending = 1,
        Approved = 2,
        Rejected = 3
    }
}