using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESTAFF.Models.Data
{
    // A recurring piece of work within a classification - e.g. "HIRADC Revision"
    // under Chemical & Legal. Under CLIP there are exactly two, and they decide
    // what TaskItem.SubTaskId points at:
    //   "Certificate Of FItness" -> CLIP.CertificateOfFitness.Id
    //   "Plant Monitoring"       -> CLIP.PlantMonitoring.Id
    [Table("TaskLists", Schema = "ESTAFF")]
    public class TaskList
    {
        [Key]
        public int TaskListId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(100)]
        public string Description { get; set; }

        [ForeignKey("TaskClassification")]
        public int TaskClassificationId { get; set; }

        public virtual TaskClassification TaskClassification { get; set; }

        public virtual ICollection<TaskItem> TaskItems { get; set; }
    }
}
