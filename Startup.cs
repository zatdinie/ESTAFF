using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(ESTAFF.Startup))]

namespace ESTAFF
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}