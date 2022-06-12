using FluentEmail.Core.Interfaces;
using FluentEmail.SendGrid;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

[assembly: FunctionsStartup(typeof(OrionStudt.ContactFunction.Startup))]
namespace OrionStudt.ContactFunction
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            // setup configuration
            builder.Services
                .AddOptions<SendGridSettings>()
                .Configure<IConfiguration>((settings, configuration) =>
                {
                    configuration.GetSection("SendGridSettings").Bind(settings);
                });

            // setup email sender
            builder.Services
                .AddScoped<ISender>(provider =>
                {
                    var options = provider.GetRequiredService<IOptions<SendGridSettings>>();
                    var settings = options.Value;
                    var sender = new SendGridSender(settings.ApiKey);
                    return sender;
                });
        }
    }
}
