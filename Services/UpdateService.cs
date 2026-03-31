using System;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Velopack;

namespace SantexnikaSRM.Services
{
    public sealed class UpdateService
    {
        public async Task<UpdateCheckResult> CheckAsync(string serverUrl, string currentVersion)
        {
            string? lastError = null;
            List<string> feeds = ResolveFeedUrls(serverUrl);

            if (feeds.Count == 0)
            {
                return UpdateCheckResult.WithError("Update manzili topilmadi.");
            }

            string localVersion = currentVersion?.Trim() ?? string.Empty;

            foreach (string feedUrl in feeds)
            {
                try
                {
                    var manager = new UpdateManager(feedUrl);
                    Velopack.UpdateInfo? updates = await manager.CheckForUpdatesAsync();
                    if (updates == null || updates.TargetFullRelease == null)
                    {
                        continue;
                    }

                    string version = updates.TargetFullRelease.Version?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(localVersion)
                        && string.Equals(localVersion, version, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string note = updates.TargetFullRelease.NotesMarkdown ?? string.Empty;
                    return UpdateCheckResult.WithUpdate(new AppUpdateInfo(
                        feedUrl,
                        version,
                        note,
                        mandatory: false,
                        updates));
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                }
            }

            if (!string.IsNullOrWhiteSpace(lastError))
            {
                return UpdateCheckResult.WithError(lastError);
            }

            return UpdateCheckResult.NoUpdate();
        }

        public async Task ApplyAndRestartAsync(AppUpdateInfo pending)
        {
            try
            {
                if (pending == null)
                {
                    throw new InvalidOperationException("Update topilmadi.");
                }

                var manager = new UpdateManager(pending.FeedUrl);
                await manager.DownloadUpdatesAsync(pending.InternalInfo);
                manager.WaitExitThenApplyUpdates(pending.InternalInfo.TargetFullRelease, false, true, null);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Update o'rnatishda xatolik: {ex.Message}", ex);
            }
        }

        private static List<string> ResolveFeedUrls(string serverUrl)
        {
            var urls = new List<string>();
            string? explicitFeed = ConfigurationManager.AppSettings["VelopackFeedUrl"];
            if (!string.IsNullOrWhiteSpace(explicitFeed))
            {
                AddIfMissing(urls, explicitFeed.Trim());
            }

            string baseUrl = (serverUrl ?? string.Empty).Trim().TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                AddIfMissing(urls, baseUrl + "/releases");
            }

            if (!string.IsNullOrWhiteSpace(explicitFeed))
            {
                string normalized = explicitFeed.Trim();
                if (normalized.IndexOf("raw.githubusercontent.com/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // raw.githubusercontent fallback as-is can fail on private repo or tokenless access.
                    // jsDelivr can serve public GitHub content with better edge caching.
                    string jsdelivr = normalized
                        .Replace("https://raw.githubusercontent.com/", "https://cdn.jsdelivr.net/gh/")
                        .Replace("/main/", "@main/")
                        .Replace("/master/", "@master/");
                    AddIfMissing(urls, jsdelivr);
                }
            }

            return urls;
        }

        private static void AddIfMissing(List<string> urls, string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            if (!urls.Any(x => string.Equals(x, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                urls.Add(candidate);
            }
        }
    }

    public sealed class UpdateCheckResult
    {
        private UpdateCheckResult(bool hasUpdate, AppUpdateInfo? info, string? errorMessage)
        {
            HasUpdate = hasUpdate;
            Info = info;
            ErrorMessage = errorMessage;
        }

        public bool HasUpdate { get; }
        public AppUpdateInfo? Info { get; }
        public string? ErrorMessage { get; }

        public static UpdateCheckResult NoUpdate() => new UpdateCheckResult(false, null, null);
        public static UpdateCheckResult WithUpdate(AppUpdateInfo info) => new UpdateCheckResult(true, info, null);
        public static UpdateCheckResult WithError(string error) => new UpdateCheckResult(false, null, error);
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
