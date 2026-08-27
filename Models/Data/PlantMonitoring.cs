using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESTAFF.Models.Data
{
    public class PlantMonitoring
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int PlantID { get; set; }

        [Required]
        public int MonitoringID { get; set; }

        [StringLength(100)]
        public string Area { get; set; }

        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}", ApplyFormatInEditMode = false)]
        public DateTime? ExpDate { get; set; }

        // Quotation Phase
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}", ApplyFormatInEditMode = false)]
        public DateTime? QuoteDate { get; set; }

        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}", ApplyFormatInEditMode = false)]
        public DateTime? QuoteCompleteDate { get; set; }

        [StringLength(100)]
        public string QuoteUserAssign { get; set; }

        [StringLength(500)]
        public string QuoteDoc { get; set; }

        // Preparation Phase
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}", ApplyFormatInEditMode = false)]
        public DateTime? EprDate { get; set; }

        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}", ApplyFormatInEditMode = false)]
        public DateTime? EprCompleteDate { get; set; }

        [StringLength(100)]
        [Display(Name = "Preparation Assigned To")]
        public string EprUserAssign { get; set; }

        [StringLength(500)]
        [Display(Name = "ePR Document")]
        public string EprDoc { get; set; }

        // Work Execution Phase
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}", ApplyFormatInEditMode = false)]
        public DateTime? WorkDate { get; set; }

        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}", ApplyFormatInEditMode = false)]
        public DateTime? WorkSubmitDate { get; set; }

        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}", ApplyFormatInEditMode = false)]
        public DateTime? WorkCompleteDate { get; set; }

        [StringLength(100)]
        public string WorkUserAssign { get; set; }

        [StringLength(500)]
        public string WorkDoc { get; set; }

        [Display(Name = "Remarks")]
        public string Remarks { get; set; }

        [StringLength(50)]
        public string ProcStatus { get; set; }

        [StringLength(50)]
        public string ExpStatus { get; set; }

        // Navigation properties
        [ForeignKey("PlantID")]
        public virtual Plant Plant { get; set; }

        [ForeignKey("MonitoringID")]
        public virtual Monitoring Monitoring { get; set; }
    }
} 