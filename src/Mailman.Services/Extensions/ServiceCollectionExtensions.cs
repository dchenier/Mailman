﻿using Google.Apis.Gmail.v1;
using Google.Apis.Http;
using Google.Apis.Sheets.v4;
using Mailman.Services.Google;
using Mailman.Services.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using Mailman.Services.Data;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.OpenApi.Models;

namespace Mailman.Services
{
    // excluding from code coverage because this is all about ASP.NET setup
    [ExcludeFromCodeCoverage()]
    public static class ServiceCollectionExtensions
    {
    public static IServiceCollection ConfigureLogging(this IServiceCollection services,
        string module,
        IConfiguration configuration = null,
        Action<LoggerConfiguration> config = null)
    {
      services.AddSingleton<ILogger>(x =>
      {
        var loggerConfig = new LoggerConfiguration()
                  .Enrich.FromLogContext()
                  .Enrich.WithProperty("Application", "Mailman")
                  .Enrich.WithProperty("Module", module);

        if (configuration != null)
          loggerConfig = loggerConfig.ReadFrom.Configuration(configuration);

        loggerConfig = loggerConfig.WriteTo.Console();


        // give a chance for the caller to further configure the Logger
        config?.Invoke(loggerConfig);

        return loggerConfig
                  .CreateLogger();
      });

      return services;
    }

    public static IServiceCollection ConfigureSwagger(this IServiceCollection services,
        IEnumerable<Type> modelBaseClasses = null)
    {
      // Register the Swagger generator, defining 1 or more Swagger documents
      services.AddSwaggerGen(c =>
      {
        c.SwaggerDoc("v1",
                  new OpenApiInfo
                  {
                    Title = "Mailman API",
                    Description = "API for interacting with Mailman templates and running mail merges",
                    License = new OpenApiLicense()
                    {
                      Name = "GNU General Public License",
                      Url = new Uri("https://www.gnu.org/licenses/gpl-3.0.en.html")
                    },
                    Version = "v1"
                  });

        // Set the comments path for the Swagger JSON and UI.
        var xmlFile = $"{Assembly.GetEntryAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        c.IncludeXmlComments(xmlPath, true);
      });


      return services;
    }



    public static AuthenticationBuilder AddMailmanAuthentication(this IServiceCollection services,
        IConfiguration configuration = null)
    {
      var authenticationBuilder = services.AddAuthentication(options =>
     {
       options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
       options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
     })
          .AddCookie(configuration)
          .AddServiceAuth(configuration)
          .AddGoogle(configuration);

      // configure the service that saves the tokens to a cache
      services.ConfureGoogleOAuthTokenService(configuration);

      return authenticationBuilder;
    }

    public static IServiceCollection AddMailmanServices(this IServiceCollection services,
        IConfiguration configuration = null,
        Action<DbContextOptionsBuilder> dbOptionsAction = null,
        IConfigurableHttpClientInitializer googleCredentials = null)
    {
      // configure the Google Sheets service
      if (googleCredentials == null)
      {
        services.AddScoped<IGoogleServicesAccessor, HttpAccessTokenGoogleServicesAccessor>();
      }
      else
      {
        // this support using static credentials for accessing Google Sheets (i.e. ServiceCredentials)
        services.AddScoped<IGoogleServicesAccessor>(x => new StaticGoogleServicesAccessor(googleCredentials));
      }
      services.AddScoped<ISheetsService, SheetsServiceImpl>();

      if (dbOptionsAction == null)
      {
        dbOptionsAction = new Action<DbContextOptionsBuilder>(options =>
        {
          options.UseSqlite("Data Source=../mergetemplate.db");
        });
      }
      services.AddDbContext<MergeTemplateContext>(dbOptionsAction);
      services.AddScoped<IMergeTemplateRepository, MergeTemplateRepository>();

      services.AddScoped<IEmailService, GmailServiceImpl>();
      services.AddScoped<IMergeTemplateService, MergeTemplateService>();

      services.AddMailMergeProxyServices(configuration);
      services.AddJwtServices(configuration);

      return services;
    }

        private static string GetJwtAudience(IConfiguration configuration)
        {
            string jwtAudience = Environment.GetEnvironmentVariable("MAILMAN_JWT_AUDIENCE");
            if (string.IsNullOrWhiteSpace(jwtAudience) && configuration != null)
            {
                jwtAudience = configuration["Security:Audience"];
            }

            return jwtAudience;
        }

        private static string GetJwtIssuer(IConfiguration configuration)
        {
            string jwtIssuer = Environment.GetEnvironmentVariable("MAILMAN_JWT_ISSUER");
            if (string.IsNullOrWhiteSpace(jwtIssuer) && configuration != null)
            {
                jwtIssuer = configuration["Security:Issuer"];
            }

            return jwtIssuer;
        }

        private static string GetMailmanWorkerServiceUrl(IConfiguration configuration)
        {
            string workerUrl = Environment.GetEnvironmentVariable("MAILMAN_WORKER_URL");
            if (string.IsNullOrWhiteSpace(workerUrl) && configuration != null)
            {
                workerUrl = configuration["WorkerServiceUrl"];
            }


    private static string GetAuthKey(IConfiguration configuration)
    {
      string authKey = Environment.GetEnvironmentVariable("MAILMAN_AUTH_KEY");
      if (string.IsNullOrWhiteSpace(authKey) && configuration != null)
      {
        authKey = configuration["Security:AuthKey"];
      }

      if (string.IsNullOrWhiteSpace(authKey))
      {
        // convencience setting - do not use in production!!
        authKey = "secretsecretsecret";
      }

      return authKey;
    }

    internal static IServiceCollection AddMailMergeProxyServices(this IServiceCollection services,
        IConfiguration configuration)
    {
      string authKey = GetAuthKey(configuration);

      services.Configure<MailmanServicesProxyOptions>(x =>
      {
        x.AuthKey = authKey;
      });
      services.AddScoped<IMailmanServicesProxy, MailmanServicesProxy>();
      return services;
    }

    internal static AuthenticationBuilder AddCookie(this AuthenticationBuilder authenticationBuilder, IConfiguration configuration)
    {
      authenticationBuilder.AddCookie(options =>
      {
        options.Cookie.SameSite = SameSiteMode.None;
      });

      return authenticationBuilder;
    }

    internal static AuthenticationBuilder AddServiceAuth(
        this AuthenticationBuilder authenticationBuilder,
        IConfiguration configuration)
    {
      authenticationBuilder.AddJwtBearer(options =>
      {
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters()
        {
          ValidateIssuer = false,
          ValidateAudience = false,
          ValidateIssuerSigningKey = true,
          IssuerSigningKey = new SymmetricSecurityKey(
                      Encoding.UTF8.GetBytes(GetAuthKey(configuration)))
        };
      });
      return authenticationBuilder;
    }

    internal static AuthenticationBuilder AddGoogle(
        this AuthenticationBuilder authenticationBuilder, IConfiguration configuration)
    {
      // Environment variables take precendence
      string googleClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
      if (string.IsNullOrWhiteSpace(googleClientId) && configuration != null)
        googleClientId = configuration["Authentication:Google:ClientId"];
      if (string.IsNullOrWhiteSpace(googleClientId))
        throw new InvalidOperationException("Google ClientId must be specified in a GOOGLE_CLIENT_ID environment variable or in a configuration file at Authentication:Google:ClientId");

      string googleClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");
      if (string.IsNullOrWhiteSpace(googleClientSecret) && configuration != null)
        googleClientSecret = configuration["Authentication:Google:ClientSecret"];
      if (string.IsNullOrWhiteSpace(googleClientSecret))
        throw new InvalidOperationException("Google ClientSecret must be specified in a GOOGLE_CLIENT_SECRET environment variable or in a configuration file at Authentication:Google:ClientSecret");

      authenticationBuilder.AddGoogle(options =>
      {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.Scope.Add(SheetsService.Scope.Spreadsheets);
        options.Scope.Add(GmailService.Scope.GmailSend);
        options.UserInformationEndpoint = "https://www.googleapis.com/oauth2/v2/userinfo";
        options.ClaimActions.Clear();
        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
        options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
        options.ClaimActions.MapJsonKey(ClaimTypes.GivenName, "given_name");
        options.ClaimActions.MapJsonKey(ClaimTypes.Surname, "family_name");
        options.ClaimActions.MapJsonKey("urn:google:profile", "url");
        options.ClaimActions.MapJsonKey("displayName", "displayName");
        options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
        options.CallbackPath = "/login/signin-google";
        options.SaveTokens = true;
        options.AccessType = "offline";
        options.Events.OnRedirectToAuthorizationEndpoint = context =>
          {
            if (IsAjaxRequest(context.Request))
            {
              context.Response.Headers["Location"] = context.RedirectUri;
              context.Response.StatusCode = 401;
              context.Response.ContentType = "application/json";
            }
            else
            {
              context.Response.Redirect(context.RedirectUri);
            }
            return Task.CompletedTask;
          };
      });
      return authenticationBuilder;
    }

        internal static IServiceCollection AddJwtServices(this IServiceCollection services,
            IConfiguration configuration)
        {
            string jwtAudience = GetJwtAudience(configuration);
            string jwtIssuer = GetJwtIssuer(configuration);
            string authKey = GetAuthKey(configuration);

            services.Configure<JwtOptions>(x =>
            {
                x.Issuer = jwtIssuer;
                x.Audience = jwtAudience;
                x.AuthKey = authKey;
            });
            services.AddScoped<IJwtSigner, JwtSigner>();
            return services;
        }

        internal static AuthenticationBuilder AddCookie(this AuthenticationBuilder authenticationBuilder, IConfiguration configuration)
        {
           authenticationBuilder.AddCookie(options =>
           {
                options.Cookie.SameSite = SameSiteMode.None;
           });

            return authenticationBuilder;
        }

    internal static void ConfureGoogleOAuthTokenService(this IServiceCollection services, IConfiguration configuration)
    {
      string redisUrl = Environment.GetEnvironmentVariable("GOOGLE_TOKEN_CACHE_URL");
      if (!string.IsNullOrWhiteSpace(redisUrl))
      {
        throw new NotSupportedException("The Redis OAuth token cache has not been implemented yet");
        //services.AddSingleton<IGoogleOAuthTokenService, RedisGoogleOAuthTokenService>();
      }
      else
      {
        string connectionString = Environment.GetEnvironmentVariable("GOOGLE_TOKEN_CACHE_DB_CONN");
        if (string.IsNullOrWhiteSpace(connectionString))
          connectionString = configuration.GetConnectionString("GoogleTokenCache");
        if (string.IsNullOrWhiteSpace(connectionString))
          connectionString = "Data Source=../oauth.db";
        services.AddDbContext<OAuthTokenContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IGoogleOAuthTokenService, EntityFrameworkGoogleOAuthTokenService>();
      }
    }

    private static bool IsAjaxRequest(HttpRequest request)
    {
      return string.Equals(request.Query["X-Requested-With"], "XMLHttpRequest", StringComparison.Ordinal) ||
          string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.Ordinal);
    }
  }
}
