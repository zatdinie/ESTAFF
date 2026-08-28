using System.Web.Mvc;
using EHS_PORTAL.Areas.ESTAFF.Models.Data;
using Microsoft.AspNet.Identity;

namespace EHS_PORTAL.Areas.ESTAFF.Filters
{
    public class EmployeeOnlyAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var httpContext = filterContext.HttpContext;

            // Not Logged in
            if (!httpContext.User.Identity.IsAuthenticated)
            {
                filterContext.Result = new RedirectResult("/ESTAFF/Account/Login");
                return;
            }

            // Not an employee
            using (var db = new ApplicationDbContext())
            {
                var userId = httpContext.User.Identity.GetUserId();
                var user = db.Users.Find(userId);

                if (user == null || user.IsAdmin)
                {
                    filterContext.Result = new RedirectResult("/ESTAFF/Account/Login");
                }
            }

            base.OnActionExecuting(filterContext);
        }
    }
}