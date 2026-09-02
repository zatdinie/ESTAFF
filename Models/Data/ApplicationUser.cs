using System;
using System.Collections.Generic;
using Microsoft.AspNet.Identity.EntityFramework;

namespace EHS_PORTAL.Areas.ESTAFF.Models.Data
{
    
    public class ApplicationUser : IdentityUser
    {
        public string EmpID { get; set; }
        public bool IsAdmin { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public DateTime? HireDate { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime LastModifiedDate { get; set; } = DateTime.Now;
        public Position Position { get; set; }
        public string JkkpNo { get; set; }
    }

    public enum Position
    {
        Unassigned = 0,
        EshManager = 1,
        EshEngineer = 2,
        ShoOfficer = 3,
        EshOfficer = 4,
    }
}