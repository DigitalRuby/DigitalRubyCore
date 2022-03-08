using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

using System.Xml.Linq;

namespace DigitalRuby.Core.Setup;

/// <summary>
/// Swagger options
/// </summary>
[Configuration("SwaggerOptions")]
public class SwaggerOptions
{
	/// <summary>
	/// Version, i.e. v1
	/// </summary>
	public string Version { get; set; } = string.Empty;

	/// <summary>
	/// Title
	/// </summary>
	public string Title { get; set; } = string.Empty;

	/// <summary>
	/// Description
	/// </summary>
	public string Description { get; set; } = string.Empty;

	/// <summary>
	/// Terms of service url
	/// </summary>
	public string TermsOfServiceUrl { get; set; } = string.Empty;

	/// <summary>
	/// Contact name
	/// </summary>
	public string ContactName { get; set; } = string.Empty;

	/// <summary>
	/// Contact url
	/// </summary>
	public string ContactUrl { get; set; } = string.Empty;

	/// <summary>
	/// License name
	/// </summary>
	public string LicenseName { get; set; } = string.Empty;

	/// <summary>
	/// License url
	/// </summary>
	public string LicenseUrl { get; set; } = string.Empty;

	/// <summary>
	/// Whether to include xml comments, default is true
	/// </summary>
	public bool XmlComments { get; set; } = true;
}

/// <summary>
/// Swagger helper
/// </summary>
public static class SwaggerHelper
{
	private class EnumTypesSchemaFilter : ISchemaFilter
	{
		private readonly XDocument? _xmlComments;

		public EnumTypesSchemaFilter(string xmlPath)
		{
			if (File.Exists(xmlPath))
			{
				_xmlComments = XDocument.Load(xmlPath);
			}
		}

		public void Apply(OpenApiSchema schema, SchemaFilterContext context)
		{
			if (_xmlComments is null)
			{
				return;
			}

			else if (schema.Enum is not null && schema.Enum.Count > 0 &&
				context.Type is not null &&
				context.Type.IsEnum)
			{
				schema.Description += "<p>Members:</p><ul>";
				var fullTypeName = context.Type.FullName;
				foreach (var enumMemberName in schema.Enum.OfType<OpenApiString>().Select(v => v.Value))
				{
					var fullEnumMemberName = $"F:{fullTypeName}.{enumMemberName}";

					var enumMemberComments = _xmlComments.Descendants("member")
						.FirstOrDefault(m => (m.Attribute("name")?.Value ?? string.Empty).Equals
						(fullEnumMemberName, StringComparison.OrdinalIgnoreCase));

					if (enumMemberComments is null)
					{
						continue;
					}

					var summary = enumMemberComments.Descendants("summary").FirstOrDefault();

					if (summary is null)
					{
						continue;
					}

					schema.Description += $"<li><i>{enumMemberName}</i> - { summary.Value.Trim()}</li>";
				}
				schema.Description += "</ul>";
			}
		}
	}

	private class SwaggerJsonIgnore : IOperationFilter
	{
		private static bool ShouldIgnoreProperty(PropertyInfo info)
		{
			foreach (var attr in info.GetCustomAttributes())
			{
				if (attr is JsonIgnoreAttribute ||
					attr is System.Runtime.Serialization.IgnoreDataMemberAttribute ||
					attr is System.Text.Json.Serialization.JsonIgnoreAttribute ||
					attr is System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute)
				{
					return true;
				}
			}
			return false;
		}

		public void Apply(OpenApiOperation operation, OperationFilterContext context)
		{
			var ignoredProperties = context.MethodInfo.GetParameters()
				.SelectMany(p => p.ParameterType.GetProperties().Where(prop => ShouldIgnoreProperty(prop)));
			foreach (var property in ignoredProperties)
			{
				operation.Parameters = operation.Parameters
					.Where(p => (!p.Name.Equals(property.Name, StringComparison.InvariantCulture) &&
					!p.Name.StartsWith(property.Name + ".", StringComparison.OrdinalIgnoreCase))).ToList();
			}
		}
	}

	/// <summary>
	/// Add swagger to services
	/// </summary>
	/// <param name="services">Services</param>
	/// <param name="configuration">Configuration</param>
	public static void AddSwagger(this IServiceCollection services, IConfiguration configuration)
	{
		// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
		SwaggerOptions options = new();
		configuration.Bind("SwaggerOptions", options);
		services.AddEndpointsApiExplorer();
		services.AddSwaggerGen(o =>
		{
			o.OrderActionsBy((apiDesc) => $"{apiDesc.ActionDescriptor.RouteValues["controller"]}_{apiDesc.RelativePath}_{apiDesc.HttpMethod}");

				// To Enable authorization using Swagger (JWT)    
				o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
			{
				Name = "Authorization",
				Type = SecuritySchemeType.ApiKey,
				Scheme = "Bearer",
				BearerFormat = "JWT",
				In = ParameterLocation.Header,
				Description = "Enter 'Bearer' [space] and then your valid token in the text input below.\r\n\r\nExample: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9\"",
			});
			o.AddSecurityRequirement(new OpenApiSecurityRequirement
			{
				{
					new OpenApiSecurityScheme
					{
						Reference = new OpenApiReference
						{
							Type = ReferenceType.SecurityScheme,
							Id = "Bearer"
						}
					},
					Array.Empty<string>()
				}
			});
			o.SwaggerDoc(options.Version, new OpenApiInfo
			{
				Version = options.Version,
				Title = options.Title,
				Description = options.Description,
				TermsOfService = new Uri(options.TermsOfServiceUrl),
				Contact = new OpenApiContact
				{
					Name = options.ContactName,
					Url = new Uri(options.ContactUrl)
				},
				License = new OpenApiLicense
				{
					Name = options.LicenseName,
					Url = new Uri(options.LicenseUrl)
				}
			});
			o.OperationFilter<SwaggerJsonIgnore>();
			if (options.XmlComments)
			{
				foreach (var xmlFile in Directory.GetFiles(AppContext.BaseDirectory, "*.xml"))
				{
					try
					{
						o.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
						o.SchemaFilter<EnumTypesSchemaFilter>(xmlFile);
					}
					catch
					{
						// ignore
					}
				}
			}
		});
		services.AddSwaggerGenNewtonsoftSupport();
	}
}