using System.Web.Mvc;
using EHS_PORTAL.Areas.ESTAFF.Models.Data;
using Microsoft.AspNet.Identity;

namespace EHS_PORTAL.Areas.ESTAFF.Filters
{
    public class AdminOnlyAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var httpContext = filterContext.HttpContext;

            // Check if the user is authenticated
            if (!httpContext.User.Identity.IsAuthenticated)
            {
                filterContext.Result = new RedirectResult("/Account/Login");
                return;
            }

            // Check if the user is an admin
            using (var db = new ApplicationDbContext())
            {
                var userId = httpContext.User.Identity.GetUserId();
                var user = db.Users.Find(userId);

                if (user == null || !user.IsAdmin)
                {
                    filterContext.Result = new RedirectResult("/Account/Login");
                }
            }

            base.OnActionExecuting(filterContext);
        }
    }
}