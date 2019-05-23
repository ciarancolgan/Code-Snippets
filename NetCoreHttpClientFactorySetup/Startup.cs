using System;
using Api.Common.Adapters;
using API.Common.Enums;
using API.Common.Helpers;
using API.Common.Middleware;
using ExternalTool.Common;
using ExternalTool.DataAccess.Access;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;

namespace ExternalTool.Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(typeof(ILoggerAdapter<>), typeof(LoggerAdapter<>));
            services.AddScoped<IJiraAccess, JiraAccess>();
            services.AddTransient<IHttpContextAccessor, HttpContextAccessor>();

            // Load the App settings into a strongly-typed class.
            services.Configure<AppSettings>(Configuration.GetSection("AppSettings"));

            HttpClientHelper.AddNamedHttpClient(services, HttpClientTypeEnum.Jira, new Random());

            // .NET Core compatibility version
            services.AddMvc()
                    .SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info
                {
                    Title = "External Tool API",
                    Version = "v1"
                });
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "External Tool API v1");
                c.RoutePrefix = string.Empty;
            });

            // Add our custom Error handling middleware to provide a consistent response to all errors. 
            app.UseMiddleware<ErrorWrappingMiddleware>();

            app.UseMvc();
        }
    }
}
