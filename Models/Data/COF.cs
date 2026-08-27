using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EHS_PORTAL.Areas.ESTAFF.Models.Data
{
    // Read-only projection of EHS_PORTAL's CLIP.CertificateOfFitness table - see ApplicationDbContext.
    public class COF
    {
        [Key]
        public int Id { get; set; }

        [Required] [Display(Name = "Plant")] public int PlantId { get; set; }

        [Required]
        [Display(Name = "Registration Number")]
        public string RegistrationNo { get; set; }

        [Required]
        [Display(Name = "Expiry Date")]
        [DataType(DataType.Date)]
        public DateTime ExpiryDate { get; set; }

        [Display(Name = "Machine Name")] public string MachineName { get; set; }

        [Display(Name = "Status")] public string Status { get; set; }

        [Display(Name = "Location")] public string Location { get; set; }

        [Display(Name = "Department")] public string Department { get; set; }

        [ForeignKey("PlantId")] public virtual Plant Plant { get; set; }
    }
}