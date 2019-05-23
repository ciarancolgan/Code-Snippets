using API.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using System;
using System.Collections.Specialized;

namespace API.Common.Helpers
{
    public static class HttpClientHelper
    {
        private const int MaxRetryAttempts = 8;

        public static void AddNamedHttpClient(IServiceCollection services, HttpClientTypeEnum httpClientType, 
            Random randomInterval, NameValueCollection defaultHeadersCollection = null)
        {
            services.AddHttpClient(httpClientType.ToString(), c =>
                {
                    if (defaultHeadersCollection != null)
                    {
                        foreach (string headerKey in defaultHeadersCollection)
                        {
                            c.DefaultRequestHeaders.Add(headerKey, defaultHeadersCollection[headerKey]);
                        }
                    }                    
                })
                .SetHandlerLifetime(TimeSpan.FromMinutes(30))
                .AddTransientHttpErrorPolicy(b => b.WaitAndRetryAsync(MaxRetryAttempts, x =>

                    // Exponential backoff + random jitter
                    TimeSpan.FromSeconds(Math.Pow(2, x)) +
                    TimeSpan.FromMilliseconds(randomInterval.Next(0, 500))
                ));
        }
    }
}
