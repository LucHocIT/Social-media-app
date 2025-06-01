using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace SocialApp.Filters;

public class FileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var formFileParameters = context.MethodInfo.GetParameters()
            .Where(p => p.ParameterType == typeof(IFormFile) || 
                       p.ParameterType == typeof(IEnumerable<IFormFile>) ||
                       p.ParameterType == typeof(List<IFormFile>) ||
                       p.ParameterType == typeof(IFormFile[]) ||
                       HasFormFileProperty(p.ParameterType))
            .ToArray();

        if (formFileParameters.Any())
        {
            operation.RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = GenerateSchemaForFormFiles(context, formFileParameters)
                    }
                }
            };
        }
    }

    private static bool HasFormFileProperty(Type type)
    {
        return type.GetProperties()
            .Any(p => p.PropertyType == typeof(IFormFile) || 
                     p.PropertyType == typeof(IEnumerable<IFormFile>) ||
                     p.PropertyType == typeof(List<IFormFile>) ||
                     p.PropertyType == typeof(IFormFile[]));
    }

    private static OpenApiSchema GenerateSchemaForFormFiles(OperationFilterContext context, ParameterInfo[] formFileParameters)
    {
        var schema = new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>()
        };

        foreach (var parameter in formFileParameters)
        {
            if (parameter.ParameterType == typeof(IFormFile))
            {
                schema.Properties[parameter.Name!] = new OpenApiSchema
                {
                    Type = "string",
                    Format = "binary"
                };
            }
            else if (HasFormFileProperty(parameter.ParameterType))
            {
                // Handle DTOs with IFormFile properties
                var dtoProperties = parameter.ParameterType.GetProperties();
                foreach (var prop in dtoProperties)
                {
                    if (prop.PropertyType == typeof(IFormFile))
                    {
                        schema.Properties[prop.Name] = new OpenApiSchema
                        {
                            Type = "string",
                            Format = "binary"
                        };
                    }
                    else if (prop.PropertyType == typeof(string))
                    {
                        schema.Properties[prop.Name] = new OpenApiSchema
                        {
                            Type = "string"
                        };
                    }
                    else if (prop.PropertyType == typeof(List<IFormFile>) || prop.PropertyType == typeof(IFormFile[]))
                    {
                        schema.Properties[prop.Name] = new OpenApiSchema
                        {
                            Type = "array",
                            Items = new OpenApiSchema
                            {
                                Type = "string",
                                Format = "binary"
                            }
                        };
                    }
                    else if (prop.PropertyType == typeof(List<string>) || prop.PropertyType == typeof(string[]))
                    {
                        schema.Properties[prop.Name] = new OpenApiSchema
                        {
                            Type = "array",
                            Items = new OpenApiSchema
                            {
                                Type = "string"
                            }
                        };
                    }
                }
            }
            else if (parameter.ParameterType == typeof(List<IFormFile>) || parameter.ParameterType == typeof(IFormFile[]))
            {
                schema.Properties[parameter.Name!] = new OpenApiSchema
                {
                    Type = "array",
                    Items = new OpenApiSchema
                    {
                        Type = "string",
                        Format = "binary"
                    }
                };
            }
        }

        return schema;
    }
}
