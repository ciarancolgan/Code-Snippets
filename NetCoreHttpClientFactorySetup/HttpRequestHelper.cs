using Api.Common.Extensions;
using API.Contract.Encryption;
using API.Contract.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace API.Common.Helpers
{
    public class HttpRequestHelper : IHttpRequestHelper
    {
        public string ReplaceParameters(string db2ConnectionString, List<string> parameters)
        {
            return string.Format(db2ConnectionString, parameters.ToArray());
        }

        public Tuple<string, string> GetAuthenticationParameters(HttpRequest httpRequest, IConfiguration configuration)
        {
            var usernameParameter = httpRequest.Headers["UserName"].ToString();
            var passwordParameter = httpRequest.Headers["Password"].ToString();

            if (string.IsNullOrEmpty(usernameParameter))
            {
                // See if we have a default to fall back on
                usernameParameter = configuration.GetSection("AppSettings").GetValue<string>("UsernameDefaultValue");
            }

            // Password parameter is an encrypted string. Decrypt. 
            passwordParameter = GenesisSecurity.DecryptString(passwordParameter);

            if (string.IsNullOrEmpty(passwordParameter))
            {
                // See if we have a default to fall back on
                passwordParameter = configuration.GetSection("AppSettings").GetValue<string>("PasswordDefaultValue");
            }

            return new Tuple<string, string>(usernameParameter, passwordParameter);
        }

        public Tuple<string, string> GetAuthenticationParameters(HttpRequest httpRequest, string defaultUserNameParameter, string defaultPasswordParameter)
        {
            var usernameParameter = httpRequest.Headers["UserName"].ToString();
            var passwordParameter = httpRequest.Headers["Password"].ToString();

            if (string.IsNullOrEmpty(usernameParameter))
            {
                // See if we have a default to fall back on
                usernameParameter = defaultUserNameParameter;
            }

            // Password parameter is an encrypted string. Decrypt. 
            passwordParameter = GenesisSecurity.DecryptString(passwordParameter);

            if (string.IsNullOrEmpty(passwordParameter))
            {
                // See if we have a default to fall back on
                passwordParameter = defaultPasswordParameter;
            }

            return new Tuple<string, string>(usernameParameter, passwordParameter);
        }
    }
}
