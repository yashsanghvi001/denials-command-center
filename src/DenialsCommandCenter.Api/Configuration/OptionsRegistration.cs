using Microsoft.Extensions.Options;

namespace DenialsCommandCenter.Api.Configuration;

public static class OptionsRegistration
{
    extension(IServiceCollection services)
    {
        // Binds the section when the options are first resolved (so test hosts can override settings), rejects
        // missing or out-of-range values at startup, and lets constructors take the options class directly.
        public IServiceCollection AddValidatedOptions<TOptions>(string section) where TOptions : class
        {
            services.AddOptions<TOptions>().BindConfiguration(section).ValidateDataAnnotations().ValidateOnStart();
            services.AddSingleton(provider => provider.GetRequiredService<IOptions<TOptions>>().Value);
            return services;
        }
    }
}
