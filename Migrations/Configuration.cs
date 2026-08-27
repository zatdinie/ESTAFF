using System;
using System.Data.Entity.Migrations;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using ESTAFF.Models.Data;

namespace ESTAFF.Migrations
{
    internal sealed class Configuration : DbMigrationsConfiguration<ApplicationDbContext>
    {
        public Configuration()
        {
            // Off deliberately. This database also hosts EHS_PORTAL's CLIP, CORD
            // and FETS schemas, and ESTAFF's entities map only the columns it
            // reads — an automatic migration reconciling that partial model would
            // drop live columns belonging to another application. Schema changes
            // go through explicit migrations or the scripts in DATABASE/.
            AutomaticMigrationsEnabled = false;
            AutomaticMigrationDataLossAllowed = false;
        }

        protected override void Seed(ApplicationDbContext context)
        {
            var userManager = new UserManager<ApplicationUser>(
                new UserStore<ApplicationUser>(context)
            );

            string adminEmail = "admin";
            string adminPassword = "Admin123";

            if (userManager.FindByEmail(adminEmail) == null)
            {
                var manager = new ApplicationUser
                {
                    Email = adminEmail,
                    UserName = "System Admin",
                    IsAdmin = true,
                    IsActive = true,
                    EmpID = null,
                    CreatedDate = DateTime.Now,
                    LastModifiedDate = DateTime.Now
                };

                userManager.Create(manager, adminPassword);
            }

            string employeeEmail = "employee1@estaff.com";
            string employeeNumber = "EMP001";
            string employeePassword = "Employee123";

            if (userManager.FindByEmail(employeeEmail) == null)
            {
                var employee = new ApplicationUser
                {
                    Email = employeeEmail,
                    UserName = "Test Employee",
                    IsAdmin = false,
                    IsActive = true,
                    EmpID = employeeNumber,
                    HireDate = DateTime.Now,
                    CreatedDate = DateTime.Now,
                    LastModifiedDate = DateTime.Now
                };

                userManager.Create(employee, employeePassword);
            }
        }
    }
}