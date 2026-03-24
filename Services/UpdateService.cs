using System;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using Velopack;

namespace SantexnikaSRM.Services
{
    public sealed class UpdateService
    {
        public async Task<UpdateCheckResult> CheckAsync(string serverUrl, string currentVersion)
        {
            try
            {
                _ = currentVersion;
                string feedUrl = ResolveFeedUrl(serverUrl);
                if (string.IsNullOrWhiteSpace(feedUrl))
                {
                    return UpdateCheckResult.NoUpdate();
                }

                var manager = new UpdateManager(feedUrl);
                Velopack.UpdateInfo? updates = await manager.CheckForUpdatesAsync();
                if (updates == null || updates.TargetFullRelease == null)
                {
                    return UpdateCheckResult.NoUpdate();
                }

                string version = updates.TargetFullRelease.Version?.ToString() ?? string.Empty;
                string note = updates.TargetFullRelease.NotesMarkdown ?? string.Empty;

                return UpdateCheckResult.WithUpdate(new AppUpdateInfo(
                    feedUrl,
                    version,
                    note,
                    mandatory: false,
                    updates));
            }
            catch
            {
                return UpdateCheckResult.NoUpdate();
            }
        }

        public async Task ApplyAndRestartAsync(AppUpdateInfo pending)
        {
            if (pending == null)
            {
                throw new InvalidOperationException("Update topilmadi.");
            }

            var manager = new UpdateManager(pending.FeedUrl);
            await manager.DownloadUpdatesAsync(pending.InternalInfo);
            manager.WaitExitThenApplyUpdates(pending.InternalInfo.TargetFullRelease, false, true, null);
        }

        private static string ResolveFeedUrl(string serverUrl)
        {
            string? explicitFeed = ConfigurationManager.AppSettings["VelopackFeedUrl"];
            if (!string.IsNullOrWhiteSpace(explicitFeed))
            {
                return explicitFeed.Trim();
            }

            string baseUrl = (serverUrl ?? string.Empty).Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return string.Empty;
            }

            return baseUrl + "/releases";
        }
    }

    public sealed class UpdateCheckResult
    {
        private UpdateCheckResult(bool hasUpdate, AppUpdateInfo? info)
        {
            HasUpdate = hasUpdate;
            Info = info;
        }

        public bool HasUpdate { get; }
        public AppUpdateInfo? Info { get; }

        public static UpdateCheckResult NoUpdate() => new UpdateCheckResult(false, null);
        public static UpdateCheckResult WithUpdate(AppUpdateInfo info) => new UpdateCheckResult(true, info);
    }

    public sealed class AppUpdateInfo
    {
        public AppUpdateInfo(string feedUrl, string version, string note, bool mandatory, Velopack.UpdateInfo internalInfo)
        {
            FeedUrl = feedUrl;
            Version = version;
            Note = note;
            Mandatory = mandatory;
            InternalInfo = internalInfo;
        }

        public string FeedUrl { get; }
        public string Version { get; }
        public string Note { get; }
        public bool Mandatory { get; }
        internal Velopack.UpdateInfo InternalInfo { get; }
    }
}
