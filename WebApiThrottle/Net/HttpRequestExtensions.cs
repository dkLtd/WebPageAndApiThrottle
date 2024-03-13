using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.ServiceModel.Channels;
using System.Web;

namespace WebApiThrottle.Net
{
    public static class HttpRequestExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static string GetClientIpAddress(this HttpRequestMessage request)
        {
            // Always return all zeroes for any failure (my calling code expects it)
            string ipAddress = "0.0.0.0";

            if (request.Properties.ContainsKey("MS_HttpContext"))
            {
                ipAddress = ((HttpContextBase)request.Properties["MS_HttpContext"]).Request.UserHostAddress;
            }
            else if (request.Properties.ContainsKey(RemoteEndpointMessageProperty.Name))
            {
                ipAddress = ((RemoteEndpointMessageProperty)request.Properties[RemoteEndpointMessageProperty.Name]).Address;
            }

            if (request.Properties.ContainsKey("MS_OwinContext"))
            {
                ipAddress = ((Microsoft.Owin.OwinContext) request.Properties["MS_OwinContext"]).Request.RemoteIpAddress;
            }

            // get the X-Forward-For headers (should only really be one)
            IEnumerable<string> xForwardForList;
            if (!request.Headers.TryGetValues("X-Forwarded-For", out xForwardForList))
            {
               return ipAddress;
            }

            var xForwardedFor = xForwardForList.FirstOrDefault();

            // check that we have a value
            if (string.IsNullOrEmpty(xForwardedFor))
            {
                return ipAddress;
            }

            // Get a list of public ip addresses in the X_FORWARDED_FOR variable
            var publicForwardingIps = xForwardedFor.Split(',').Where(ip => !IpAddressUtil.IsPrivateIpAddress(ip)).ToList();

            // If we found any, return the last one, otherwise return the user host address
            return publicForwardingIps.Any() ? publicForwardingIps.Last() : ipAddress;

        }

        public static string GetClientIpAddress(this HttpRequest request)
        {
            // Always return all zeroes for any failure (my calling code expects it)
            string ipAddress = "0.0.0.0";

            // Check if request is available
            if (request != null)
            {
                // Attempt to get client IP address
                if (!String.IsNullOrEmpty(request.UserHostAddress))
                    ipAddress = request.UserHostAddress;

                // Check for X-Forwarded-For header for proxy scenarios
                string forwardedFor = request.ServerVariables["HTTP_X_FORWARDED_FOR"];

                // Use the first IP address in the X-Forwarded-For list
                if (!String.IsNullOrEmpty(forwardedFor))
                    ipAddress = forwardedFor.Split(',')[0].Trim();

                // Attempt to get client IP address from server variables
                if (ipAddress == "0.0.0.0" && request.ServerVariables != null &&
                    request.ServerVariables["REMOTE_ADDR"] != null)
                {
                    ipAddress = request.ServerVariables["REMOTE_ADDR"];
                }
            }

            return ipAddress;
        }
    }
}