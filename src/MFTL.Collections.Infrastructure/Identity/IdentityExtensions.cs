using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MFTL.Collections.Infrastructure.Configuration;

namespace MFTL.Collections.Infrastructure.Identity;

public static class IdentityExtensions
{
    public static IServiceCollection AddAuth0Authentication(this IServiceCollection services, Auth0Options options)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwtOptions =>
            {
                jwtOptions.Authority = $"https://{options.Domain}/";
                jwtOptions.Audience = options.Audience;
                jwtOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = $"https://{options.Domain}/",
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateLifetime = true
                };
            });

        return services;
    }
}
