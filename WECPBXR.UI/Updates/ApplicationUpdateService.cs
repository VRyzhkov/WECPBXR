using Velopack;
using Velopack.Sources;

namespace WECPBXR.UI.Updates;

public sealed class ApplicationUpdateService
{
    private const string RepositoryUrl = "https://github.com/VRyzhkov/WECPBXR";

    public async Task<ApplicationUpdateResult> CheckDownloadAndApplyAsync(
        Action<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        UpdateManager manager = new(new GithubSource(RepositoryUrl, accessToken: null, prerelease: true));

        if (!manager.IsInstalled)
        {
            return ApplicationUpdateResult.NotInstalled;
        }

        VelopackAsset? pendingUpdate = manager.UpdatePendingRestart;
        if (pendingUpdate is not null)
        {
            manager.ApplyUpdatesAndRestart(pendingUpdate);
            return ApplicationUpdateResult.Restarting(pendingUpdate.Version.ToString());
        }

        UpdateInfo? update = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
        if (update is null)
        {
            return ApplicationUpdateResult.NoUpdate;
        }

        await manager.DownloadUpdatesAsync(update, progress, cancellationToken).ConfigureAwait(false);
        manager.ApplyUpdatesAndRestart(update.TargetFullRelease);

        return ApplicationUpdateResult.Restarting(update.TargetFullRelease.Version.ToString());
    }
}

public sealed record ApplicationUpdateResult(ApplicationUpdateResultKind Kind, string? Version = null)
{
    public static ApplicationUpdateResult NotInstalled { get; } = new(ApplicationUpdateResultKind.NotInstalled);

    public static ApplicationUpdateResult NoUpdate { get; } = new(ApplicationUpdateResultKind.NoUpdate);

    public static ApplicationUpdateResult Restarting(string version)
    {
        return new ApplicationUpdateResult(ApplicationUpdateResultKind.Restarting, version);
    }
}

public enum ApplicationUpdateResultKind
{
    NotInstalled,
    NoUpdate,
    Restarting
}
