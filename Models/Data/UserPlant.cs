using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESTAFF.Models.Data
{
    // Which plants a user may act on. Read-only projection of CLIP.UserPlants.
    public class UserPlant
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        public int PlantId { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        [ForeignKey("PlantId")]
        public virtual Plant Plant { get; set; }
    }
}
