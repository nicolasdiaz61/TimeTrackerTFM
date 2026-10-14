using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace TimeTracker.Api.Configuration;

/// <summary>
/// Declara en el contrato el esquema <c>Bearer</c> (JWT) como requisito global y lo retira (<c>security: []</c>) de
/// las operaciones anónimas: el login y la salud (DD-28).
/// </summary>
public static class OpenApiSecurityTransformers
{
    private const string SchemeName = "Bearer";

    public static OpenApiOptions AddBearerSecurity(this OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.AddComponent(SchemeName, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Token de acceso obtenido en POST /api/auth/login.",
            });
            document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(SchemeName, document)] = [] }];
            return Task.CompletedTask;
        });
        options.AddOperationTransformer((operation, context, _) =>
        {
            if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
            {
                operation.Security = [];
            }

            return Task.CompletedTask;
        });

        return options;
    }
}
