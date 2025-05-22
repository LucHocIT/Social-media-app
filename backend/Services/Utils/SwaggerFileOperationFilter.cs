using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SocialApp.Services.Utils
{
    public class SwaggerFileOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var fileParameters = context.ApiDescription.ParameterDescriptions
                .Where(p => p.ModelMetadata?.ModelType == typeof(IFormFile))
                .ToList();

            if (fileParameters.Count == 0)
            {
                return;
            }

            // Set the correct content type for file upload
            operation.RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = fileParameters.ToDictionary(
                                p => p.Name,
                                p => new OpenApiSchema
                                {
                                    Type = "string",
                                    Format = "binary",
                                    Description = p.ModelMetadata?.Description
                                }
                            ),
                            Required = new HashSet<string>(
                                fileParameters
                                    .Where(p => p.ModelMetadata?.IsRequired == true)
                                    .Select(p => p.Name)
                            )
                        }
                    }
                },
                Required = fileParameters.Any(p => p.ModelMetadata?.IsRequired == true)
            };

            // Remove the parameter from the operation parameters
            foreach (var fileParameter in fileParameters)
            {
                var paramToRemove = operation.Parameters.FirstOrDefault(p => p.Name == fileParameter.Name);
                if (paramToRemove != null)
                {
                    operation.Parameters.Remove(paramToRemove);
                }
            }
        }
    }
}
