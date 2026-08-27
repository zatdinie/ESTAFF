using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESTAFF.Models.Data
{
    [Table("ReportApprovals", Schema = "ESTAFF")]

    public class ReportApproval
    {
        [Key]
        public int ApprovalId { get; set; }

        [Required]
        [ForeignKey("Report")]
        public int ReportId { get; set; }

        [Required]
        [ForeignKey("Manager")]
        public string ManagerId { get; set; }

        [Required]
        public ApprovalStatus ApprovalStatus { get; set; }

        public string Comments { get; set; }

        public DateTime ActionDate { get; set; } = DateTime.Now;

        public virtual Report Report { get; set; }
        public virtual ApplicationUser Manager { get; set; }
    }

    public enum ApprovalStatus
    {
        Pending = 1,
        Approved = 2,
        Rejected = 3
    }
}