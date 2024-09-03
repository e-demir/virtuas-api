using System;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace fazz.Middlewares
{
    public class AuthMiddleware
    {
        private readonly RequestDelegate _nextMiddleware;

        public AuthMiddleware(RequestDelegate next)
        {
            _nextMiddleware = next;
        }

        public async Task Invoke(HttpContext context)
        {
            // Check if the request path is for login or register
            if (context.Request.Path.StartsWithSegments("/api/Auth/Login", StringComparison.OrdinalIgnoreCase) ||
                context.Request.Path.StartsWithSegments("/api/Auth/Login", StringComparison.OrdinalIgnoreCase))
            {
                // Skip authentication for these endpoints
                await _nextMiddleware(context);
                return;
            }

            // Proceed with authentication for other endpoints
            string authHeader = context.Request.Headers["Authorization"];
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("basic", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var token = authHeader.Substring(6).Trim();
                    var credentialString = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                    var credentials = credentialString.Split(':');

                    if (credentials.Length == 2 && credentials[0] == "appyko" && credentials[1] == "1903")
                    {
                        var claims = new[]
                        {
                            new Claim(ClaimTypes.Name, credentials[0]),
                            new Claim(ClaimTypes.Role, "Admin")
                        };
                        var identity = new ClaimsIdentity(claims, "Basic");
                        context.User = new ClaimsPrincipal(identity);

                        // Continue processing the request
                        await _nextMiddleware(context);
                        return;
                    }
                    else
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsync("Unauthorized");
                    }
                }
                catch
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync("Bad Request");
                }
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Forbidden");
            }
        }
    }
}
