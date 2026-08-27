using System.ComponentModel.DataAnnotations;

namespace EHS_PORTAL.Areas.ESTAFF.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "This Field is required")]
        [Display(Name = "Employee Number")]
        public string EmpID { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }
}