using Microsoft.Extensions.DependencyInjection;

namespace OpenClawNet.Skills;

public static class SkillsServiceCollectionExtensions
{
    public static IServiceCollection AddSkills(this IServiceCollection services, IEnumerable<string>? skillDirectories = null)
    {
        if (skillDirectories is not null)
        {
            services.AddSingleton(skillDirectories);
        }
        services.AddSingleton<ISkillLoader, FileSkillLoader>();
        return services;
    }
}
