using System;
using System.ComponentModel.DataAnnotations;

namespace ESTAFF.Models.ViewModels
{
    public class CreateStaffViewModel
    {
        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(256)]
        [Display(Name = "Full Name")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Employee number is required")]
        [StringLength(50)]
        [Display(Name = "Employee Number")]
        public string EmpID { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = " Password must be at least 6 characters")]
        [DataType(DataType.Password)]
        [Display(Name = "Temporary Password")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Hire date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Hire Date")]
        public DateTime HireDate { get; set; } = DateTime.Today;
    }

    public class EmployeeCardViewModel
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string EmpID { get; set; }
        public string Email { get; set; }
        public bool IsActive { get; set; }
        public DateTime HireDate { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int PendingTasks { get; set; }
        public int OverdueTasks { get; set; }
        public decimal OnTimeRate { get; set; }
    }
    public class EditEmployeeViewModel
    {
        public string UserId { get; set; }

        [Required(ErrorMessage = "Full name is required")]
        [StringLength(256)]
        [Display(Name = "Full Name")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Employee number is required")]
        [StringLength(50)]
        [Display(Name = "Employee Number")]
        public string EmpID { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Hire Date")]
        public DateTime HireDate { get; set; } 

        [Display(Name = "Active")]
        public bool IsActive { get; set; } 
        
        // Stats
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int PendingTasks { get; set; }
        public int OverdueTasks { get; set; }
        public decimal OnTimeRate { get; set; }
    }
}