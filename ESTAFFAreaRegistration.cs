using System.Security.Policy;
using System.Web.Mvc;

namespace EHS_PORTAL.Areas.ESTAFF
{
    public class ESTAFFAreaRegistration : AreaRegistration
    {
        public override string AreaName
        {
            get { return "ESTAFF"; }
        }

        public override void RegisterArea(AreaRegistrationContext context)
        {
            context.MapRoute(
                "ESTAFF_default",
                "ESTAFF/{controller}/{action}/{id}",
                new { controller = "Account", action = "Login", id = UrlParameter.Optional },
                new[] { "EHS_PORTAL.Areas.ESTAFF.Controllers" }
            );
        }
    }
}