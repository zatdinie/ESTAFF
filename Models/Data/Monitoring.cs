using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EHS_PORTAL.Areas.ESTAFF.Models.Data
{
    public class Monitoring
    {
        public Monitoring()
        {
            PlantMonitorings = new HashSet<PlantMonitoring>();
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MonitoringID { get; set; }

        [Required]
        [StringLength(100)]
        public string MonitoringName { get; set; }

        [Required]
        [StringLength(50)]
        public string MonitoringCategory { get; set; }

        [Required]
        public int MonitoringFreq { get; set; }

        // Navigation property
        public virtual ICollection<PlantMonitoring> PlantMonitorings { get; set; }
    }
}