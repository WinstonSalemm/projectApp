using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ProjectApp.Api.Swagger;

public class AuthorizeCheckOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasAuthorize = context.MethodInfo.DeclaringType!.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any()
                           || context.MethodInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any();
        if (!hasAuthorize)
            return;

        operation.Security ??= new List<OpenApiSecurityRequirement>();
        var requirement = new OpenApiSecurityRequirement
        {
            [ new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "ApiKey"
                    }
                }
            ] = new List<string>()
        };
        operation.Security.Add(requirement);
    }
}
