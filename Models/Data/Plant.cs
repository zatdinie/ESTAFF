using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EHS_PORTAL.Areas.ESTAFF.Models.Data
{
    public class Plant
    {
        public Plant()
        {
            UserPlants = new HashSet<UserPlant>();
        }

        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string PlantName { get; set; }
        
        // Navigation property
        public virtual ICollection<UserPlant> UserPlants { get; set; }
    }
}