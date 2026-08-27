using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using EHS_PORTAL.Areas.ESTAFF.Models.Data;
using EHS_PORTAL.Areas.ESTAFF.Models.ViewModels;

namespace EHS_PORTAL.Areas.ESTAFF.Controllers
{
    public class AccountController : Controller
    {
        private ApplicationDbContext _db = new ApplicationDbContext();

        private ApplicationUserManager UserManager =>
            HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();

        private ApplicationSignInManager SignInManager =>
            HttpContext.GetOwinContext().Get<ApplicationSignInManager>();

        private IAuthenticationManager AuthManager =>
            HttpContext.GetOwinContext().Authentication;

        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            // Already logged in — redirect to correct dashboard
            if (User.Identity.IsAuthenticated)
            {
                var userId = User.Identity.GetUserId();
                var user = _db.Users.Find(userId);
                return RedirectToDashboard(user);
            }

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Find user by email (admin) or employee number (employee)
            var user = _db.Users.FirstOrDefault(u =>
                u.Email == model.EmpID ||
                u.EmpID == model.EmpID);

            if (user == null)
            {
                ModelState.AddModelError("", "Invalid credentials. Please try again.");
                return View(model);
            }

            // Check if account is active
            if (!user.IsActive)
            {
                ModelState.AddModelError("", "Your account has been deactivated. Please contact admin.");
                return View(model);
            }

            var result = await SignInManager.PasswordSignInAsync(
                user.UserName, model.Password, model.RememberMe, shouldLockout: true);

            switch (result)
            {
                case SignInStatus.Success:
                    return RedirectToDashboard(user);

                case SignInStatus.LockedOut:
                    ModelState.AddModelError("", "Account locked. Please try again in 5 minutes.");
                    return View(model);

                default:
                    ModelState.AddModelError("", "Invalid credentials. Please try again.");
                    return View(model);
            }
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            AuthManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            return RedirectToAction("Login", "Account");
        }

        // Helper: redirect based on IsAdmin
        private ActionResult RedirectToDashboard(ApplicationUser user)
        {
            if (user.IsAdmin)
                return RedirectToAction("Index", "Admin");
            else
                return RedirectToAction("Index", "Employee");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }
    }
}