using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESTAFF.Models.Data
{
    [Table("Staffs", Schema = "ESTAFF")]

    public class Staff
    {
        [Key]
        public string StaffId { get; set; }

        [Required]
        [ForeignKey("User")]
        public string UserId { get; set; }

        [Required]
        [ForeignKey("Manager")]
        public string ManagerId { get; set; }

        [StringLength(100)]
        public string Department { get; set; }

        public DateTime HireDate { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public virtual ApplicationUser User { get; set; }
        public virtual ApplicationUser Manager { get; set; }
    }
}