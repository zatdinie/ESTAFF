using System;
using System.Data.Entity;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using ESTAFF.Models.Data;

namespace ESTAFF
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            // Schema management is deliberately not done here. This used to run
            // MigrateDatabaseToLatestVersion, which could reconcile ESTAFF's
            // partial model against a database shared with EHS_PORTAL and drop
            // columns another application owns. ApplicationDbContext's static
            // constructor now disables the initializer for every consumer, not
            // just the web host — see the comment there.

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }
    }
}
