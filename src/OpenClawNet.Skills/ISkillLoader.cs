namespace OpenClawNet.Skills;

public interface ISkillLoader
{
    Task<IReadOnlyList<SkillContent>> GetActiveSkillsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SkillDefinition>> ListSkillsAsync(CancellationToken cancellationToken = default);
    Task ReloadAsync(CancellationToken cancellationToken = default);
    void EnableSkill(string name);
    void DisableSkill(string name);
    Task InstallSkillAsync(string name, string content, CancellationToken cancellationToken = default);
    Task UninstallSkillAsync(string name, CancellationToken cancellationToken = default);
}
