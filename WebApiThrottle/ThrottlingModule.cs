using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using WebApiThrottle.Net;

namespace WebApiThrottle
{
    public class ThrottlingModule : IHttpModule
    {
        private ThrottlingCore core;
        private IPolicyRepository policyRepository;
        private ThrottlePolicy policy;

        public IPolicyRepository PolicyRepository { get; set; }
        public ThrottlePolicy Policy { get; set; }
        public IThrottleRepository Repository { get; set; }
        public IThrottleLogger Logger { get; set; }
        public string QuotaExceededMessage { get; set; }
        public HttpStatusCode QuotaExceededResponseCode { get; set; }

        public ThrottlingModule()
        {
            QuotaExceededResponseCode = (HttpStatusCode)429;
            Repository = new CacheRepository();
            core = new ThrottlingCore();
        }

        public ThrottlingModule(ThrottlePolicy policy,
            IPolicyRepository policyRepository,
            IThrottleRepository repository,
            IThrottleLogger logger = null,
            IIpAddressParser ipAddressParser = null)
        {
            core = new ThrottlingCore();
            core.Repository = repository;
            Repository = repository;
            Logger = logger;

            if (ipAddressParser != null)
            {
                core.IpAddressParser = ipAddressParser;
            }

            QuotaExceededResponseCode = (HttpStatusCode)429;

            this.policy = policy;
            this.policyRepository = policyRepository;

            if (policyRepository != null)
            {
                policyRepository.Save(ThrottleManager.GetPolicyKey(), policy);
            }
        }

        public void Init(HttpApplication context)
        {
            context.BeginRequest += OnBeginRequest;
        }

        private void OnBeginRequest(object sender, EventArgs e)
        {
            HttpContext context = ((HttpApplication)sender).Context;

            // get policy from repo
            if (policyRepository != null)
            {
                policy = policyRepository.FirstOrDefault(ThrottleManager.GetPolicyKey());
            }

            if (policy == null || (!policy.IpThrottling && !policy.ClientThrottling && !policy.EndpointThrottling)) return;

            core.Repository = Repository;
            core.Policy = policy;

            var identity = SetIdentity(context.Request);

            if (core.IsWhitelisted(identity)) return;

            TimeSpan timeSpan = TimeSpan.FromSeconds(1);

            // get default rates
            var defRates = core.RatesWithDefaults(Policy.Rates.ToList());
            if (Policy.StackBlockedRequests)
            {
                // all requests including the rejected ones will stack in this order: week, day, hour, min, sec
                // if a client hits the hour limit then the minutes and seconds counters will expire and will eventually get erased from cache
                defRates.Reverse();
            }

            // apply policy
            foreach (var rate in defRates)
            {
                var rateLimitPeriod = rate.Key;
                var rateLimit = rate.Value;

                timeSpan = core.GetTimeSpanFromPeriod(rateLimitPeriod);

                // apply global rules
                core.ApplyRules(identity, timeSpan, rateLimitPeriod, ref rateLimit);

                if (rateLimit > 0)
                {
                    // increment counter
                    var requestId = ComputeThrottleKey(identity, rateLimitPeriod);
                    var throttleCounter = core.ProcessRequest(timeSpan, requestId);

                    // check if key expired
                    if (throttleCounter.Timestamp + timeSpan < DateTime.UtcNow)
                    {
                        continue;
                    }

                    // check if limit is reached
                    if (throttleCounter.TotalRequests > rateLimit)
                    {
                        // log blocked request
                        if (Logger != null)
                        {
                            Logger.Log(core.ComputeLogEntry(requestId, identity, throttleCounter, rateLimitPeriod.ToString(), rateLimit, null));
                        }

                        var message = !string.IsNullOrEmpty(this.QuotaExceededMessage) 
                            ? this.QuotaExceededMessage 
                            : "API calls quota exceeded! maximum admitted {0} per {1}.";

                        // break execution
                        QuotaExceededResponse(
                            context.Response,
                            string.Format(message, rateLimit, rateLimitPeriod),
                            QuotaExceededResponseCode,
                            core.RetryAfterFrom(throttleCounter.Timestamp, rateLimitPeriod));
                    }
                }
            }
        }

        protected IPAddress GetClientIp(HttpRequest request)
        {
            return core.GetClientIp(request);
        }

        protected virtual RequestIdentity SetIdentity(HttpRequest request)
        {
            var entry = new RequestIdentity();
            entry.ClientIp = core.GetClientIp(request).ToString();
            entry.Endpoint = request.Url.AbsolutePath.ToLowerInvariant();
            entry.ClientKey = request.Headers["Authorization-Token"] ?? "anon";

            return entry;
        }

        protected virtual string ComputeThrottleKey(RequestIdentity requestIdentity, RateLimitPeriod period)
        {
            return core.ComputeThrottleKey(requestIdentity, period);
        }

        protected virtual void QuotaExceededResponse(HttpResponse response, string content, HttpStatusCode responseCode, string retryAfter)
        {
            response.Clear();
            response.StatusCode = (int)responseCode;
            response.StatusDescription = "Too Many Requests";
            response.ContentType = "text/plain";
            response.Write(content);
            response.AddHeader("Retry-After", retryAfter);

            // End the request processing
            response.End();
        }

        public void Dispose()
        {
            // Cleanup code, if any
        }
    }
}
