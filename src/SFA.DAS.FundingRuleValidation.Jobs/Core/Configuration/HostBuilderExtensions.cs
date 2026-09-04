using System.Diagnostics.CodeAnalysis;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SFA.DAS.FundingRuleValidation.Jobs.Data;
using SFA.DAS.FundingRuleValidation.Jobs.Data.TableStorage;

namespace SFA.DAS.FundingRuleValidation.Jobs.Core.Configuration;

[ExcludeFromCodeCoverage]
public static class HostBuilderExtensions
{
    extension(FunctionsApplicationBuilder builder)
    {
        public FunctionsApplicationBuilder ConfigureFundingRuleValidationApp()
        {
            return builder
                .ConfigureFunctionsWebApplication()
                .RegisterConfiguration()
                .RegisterServices()
                .RegisterDependencies();
        }

        private FunctionsApplicationBuilder RegisterConfiguration()
        {
            builder.Configuration
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddEnvironmentVariables()
                .AddJsonFile("local.settings.json", optional: true);

            builder.Configuration.AddAzureTableStorageConfiguration();

            // add custom configuration sections
            builder.Services.Configure<ConnectionStringsConfiguration>(builder.Configuration.GetSection("ConnectionStrings"));
            builder.Services.AddSingleton(cfg => cfg.GetService<IOptions<ConnectionStringsConfiguration>>()!.Value);

            return builder;
        }

        private FunctionsApplicationBuilder RegisterServices()
        {
            builder.AddOpenTelemetryRegistration(builder.Configuration.GetValue<string>("APPLICATIONINSIGHTS_CONNECTION_STRING"));
            return builder;
        }

        private FunctionsApplicationBuilder RegisterDependencies()
        {
            var services = builder.Services;
            var connectionStrings = builder.Configuration.GetSection("ConnectionStrings").Get<ConnectionStringsConfiguration>();
            // IMPORTANT: use only one of the following storage mechanisms

            // table storage
            services.AddTransient(_ => new TableServiceClient(connectionStrings?.TableStorageConnectionString));
            services.AddTransient<IRulesRepository, TableStorageRulesRepository>();

            // sql server
            // services.AddDbContext<FundingRulesDbContext>(options => options.UseSqlServer(connectionStrings?.SqlConnectionString));
            // services.AddTransient<IFundingRulesDataContext, FundingRulesDbContext>();
            // services.AddTransient<IRulesRepository, SqlRulesRepository>();

            // service bus
            services.AddSingleton(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var fqdn = config[$"{GlobalConstants.ServiceBusConnectionName}:fullyQualifiedNamespace"]!;
                return new ServiceBusClient(fqdn, new DefaultAzureCredential());
            });
            
            return builder;
        }
    }
}