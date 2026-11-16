namespace RonSijm.Slopsidian.Services;

public interface IVaultHost
{
    Task<VaultSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
    Task<VaultSnapshot> OpenAsync(string path, CancellationToken cancellationToken = default);
    Task<VaultSnapshot> CreateAsync(CreateVaultRequest request, CancellationToken cancellationToken = default);
}
