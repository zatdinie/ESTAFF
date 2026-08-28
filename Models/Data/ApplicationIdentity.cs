using System;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.Owin;
using Microsoft.Owin.Security;

namespace EHS_PORTAL.Areas.ESTAFF.Models.Data
{
    // Role Manager
    public class EstaffUserManager : UserManager<ApplicationUser>
    {
        public EstaffUserManager(IUserStore<ApplicationUser> store)
            : base(store)
        {
        }

        public static EstaffUserManager Create(
            IdentityFactoryOptions<EstaffUserManager> options,
            IOwinContext context)
        {
            var manager = new EstaffUserManager(
                new UserStore<ApplicationUser>(context.Get<ApplicationDbContext>()));
            
            manager.PasswordValidator = new PasswordValidator
            {
                RequiredLength = 6,
                RequireNonLetterOrDigit = false,
                RequireDigit = true,
                RequireLowercase = true,
                RequireUppercase = false
            };

            manager.UserLockoutEnabledByDefault = true;
            manager.DefaultAccountLockoutTimeSpan = TimeSpan.FromMinutes(5);
            manager.MaxFailedAccessAttemptsBeforeLockout = 5;

            return manager;
        }
    }

    // Sign in Manager
    public class EstaffSignInManager : SignInManager<ApplicationUser, string>
    {
        public EstaffSignInManager (
            EstaffUserManager userManager,
            IAuthenticationManager authenticationManager)
            : base(userManager, authenticationManager)
        {
        }
        
        public static EstaffSignInManager Create(
            IdentityFactoryOptions<EstaffSignInManager> options,
            IOwinContext context)
        {
            return new EstaffSignInManager(
                context.GetUserManager<EstaffUserManager>(),
                context.Authentication);
        }
    }
}