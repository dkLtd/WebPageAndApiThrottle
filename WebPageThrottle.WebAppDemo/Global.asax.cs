using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using System.Web.SessionState;
using WebApiThrottle;

namespace WebPageThrottle.WebAppDemo
{
    public class Global : HttpApplication
    {
        void Application_Start(object sender, EventArgs e)
        {
            // Code that runs on application startup
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            RegisterThrottlingModule();
        }

        private void RegisterThrottlingModule()
        {
            var myHttpModule = new ThrottlingModule(
                policy: new ThrottlePolicy()
                {
                    IpThrottling = true,
                    //ClientThrottling = true,
                    EndpointThrottling = true,
                    StackBlockedRequests = false,
                    EndpointRules = null,
                    IpWhitelist = ""?.Split(',').Select(i => i.Trim()).ToList() ?? new List<string>(),
                    ClientWhitelist = ""?.Split(',').Select(i => i.Trim()).ToList() ?? new List<string>()
                },
                repository: new CacheRepository(),
                policyRepository: new PolicyCacheRepository()
            );

            myHttpModule.Init(this);
        }
    }
}