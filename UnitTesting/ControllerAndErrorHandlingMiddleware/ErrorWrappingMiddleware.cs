using Api.Common.Adapters;
using API.Contract.Models;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace API.Common.Middleware
{
    public class ErrorWrappingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILoggerAdapter<ErrorWrappingMiddleware> _logger;

        public ErrorWrappingMiddleware(RequestDelegate next, ILoggerAdapter<ErrorWrappingMiddleware> logger)
        {
            _next = next;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Invoke(HttpContext context)
        {
            var errorMessage = string.Empty;

            try
            {
                await _next.Invoke(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error logged for request URL: {context.Request.Path.Value}");

                context.Response.StatusCode = 500;
                errorMessage = ex.Message;
            }

            if (!context.Response.HasStarted)
            {
                context.Response.ContentType = "application/json";

                var response = new ApiResponse((System.Net.HttpStatusCode)context.Response.StatusCode, errorMessage);

                var json = JsonConvert.SerializeObject(response);

                await context.Response.WriteAsync(json);
            }
        }
    }
}
