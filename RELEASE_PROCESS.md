# Windows release process

The application uses Velopack for both first-time installation and in-app updates.
The release workflow builds the installer, portable ZIP, and update-feed assets
on Windows. Pull requests run the build and service regression tests without
publishing a release.
The application entry point must run `VelopackApp.Build().Run()` before
initializing the database or UI so Velopack can handle install and update hooks.

## Publishing

1. Update all four version values in `SantexnikaSRM.csproj` to the same `x.y.z`
   version and merge the change to `main`.
2. Create and push a matching tag, for example `v1.2.43`.
3. The workflow builds and validates the package. Its publish job waits for an
   approval through the `stable-release` GitHub environment.
4. After approval, the workflow publishes the Velopack feed files, installer,
   and portable ZIP as assets on the GitHub Release.

To enable the approval gate, open **Settings → Environments → stable-release**,
add the release administrator under **Required reviewers**, and save. Without a
required reviewer configured, GitHub does not pause the publish job for approval.

The Render `LicenseAdminServer` proxies `/releases/*` to assets on the latest
GitHub Release. It recognizes the previous raw-GitHub `RELEASE_FEED_SOURCE_URL`
and migrates it automatically; if the latest release asset is not available,
the proxy temporarily falls back to the existing `main/releases` feed.

The setup executable is for new installations. Existing Velopack installations
check the same release feed and apply the update from the in-app update action.
