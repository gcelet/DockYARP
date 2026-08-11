using System;
using System.Linq;
using System.Net.Http;
using System.Threading;

using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Tools.Npm;

using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.Npm.NpmTasks;

class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("Container registry host; empty targets Docker Hub")]
    readonly string Registry = "";

    [Parameter("Image repository name")]
    readonly string ImageRepository = "dockyarp";

    [Parameter("Image tag")]
    readonly string ImageTag = "latest";

    [Parameter("Target platform(s) for the image, comma-separated (multi-arch requires DockerPublish/--push, not a local --load)")]
    readonly string Platforms = "linux/amd64";

    [Parameter("Explicit full version; when set (e.g. the Docker build-arg case), it is used instead of GitVersion")]
    readonly string Version = "";

    [Parameter("Base URL for the documentation build (the GitHub Pages project URL by default)")]
    readonly string DocsBaseUrl = "https://gcelet.github.io/DockYARP/";

    // GitVersion resolves the version from git height + v* tags. Resolved lazily, so RestoreTools can install the
    // gitversion.tool first; NoFetch/NoCache keep it deterministic in CI.
    [GitVersion(NoFetch = true, NoCache = true)]
    readonly GitVersion GitVersion;

    // Populated by GenerateVersionDetails; consumed by Compile/Publish (stamping) and DockerImage/DockerPublish
    // (the VERSION build-arg). Never accessed before that target runs.
    VersionDetails VersionDetails;

    // NUnit category tagging the Aspire end-to-end suite; excluded by default, run by E2E/Release only.
    const string EndToEndCategory = "EndToEnd";

    // The AppHost consumes these fixed image names; E2E builds them before booting the distributed system.
    const string LocalProxyImage = "dockyarp:local";

    string FullImage => string.IsNullOrEmpty(Registry)
        ? $"{ImageRepository}:{ImageTag}"
        : $"{Registry}/{ImageRepository}:{ImageTag}";

    string LatestImage => string.IsNullOrEmpty(Registry)
        ? $"{ImageRepository}:latest"
        : $"{Registry}/{ImageRepository}:latest";

    AbsolutePath SolutionFile => RootDirectory / "DockYarp.slnx";
    AbsolutePath AppProject => RootDirectory / "src" / "DockYarp.App" / "DockYarp.App.csproj";
    AbsolutePath BackendProject => RootDirectory / "tests" / "DockYarp.E2E.Backend" / "DockYarp.E2E.Backend.csproj";
    AbsolutePath GrpcBackendProject => RootDirectory / "tests" / "DockYarp.E2E.GrpcBackend" / "DockYarp.E2E.GrpcBackend.csproj";
    AbsolutePath E2EProject => RootDirectory / "tests" / "DockYarp.E2E.Tests" / "DockYarp.E2E.Tests.csproj";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath E2ELogDirectory => ArtifactsDirectory / "e2e-logs";
    AbsolutePath DocsDirectory => RootDirectory / "docs-site";

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() => ArtifactsDirectory.CreateOrCleanDirectory());

    Target Restore => _ => _
        .Executes(() => DotNetRestore(s => s.SetProjectFile(SolutionFile)));

    // Restores the local .NET tools (gitversion.tool) before anything reads GitVersion. Skipped when an explicit
    // --version is supplied (the Docker build stage), where GitVersion is never invoked and .git is absent.
    Target RestoreTools => _ => _
        .Before(Restore)
        .OnlyWhenDynamic(() => string.IsNullOrEmpty(Version))
        .Executes(() => DotNetToolRestore());

    // Resolves the version once per run: an explicit --version wins (the Docker build-arg case, where .git is
    // absent), otherwise GitVersion (git height + v* tags), otherwise a deterministic 0.1.0 fallback.
    Target GenerateVersionDetails => _ => _
        .DependsOn(RestoreTools)
        .Executes(() =>
        {
            if (!string.IsNullOrEmpty(Version))
            {
                VersionDetails = VersionDetails.FromExplicitVersion(Version);
            }
            else
            {
                try
                {
                    bool hasPreRelease = !string.IsNullOrEmpty(GitVersion.PreReleaseLabel);
                    VersionDetails = new VersionDetails
                    {
                        PackageVersionPrefix = GitVersion.MajorMinorPatch,
                        PackageVersionSuffix = hasPreRelease ? GitVersion.PreReleaseTag : string.Empty,
                        Version = GitVersion.SemVer,
                        AssemblyVersion = GitVersion.AssemblySemVer,
                        FileVersion = GitVersion.AssemblySemFileVer,
                        InformationalVersion = GitVersion.InformationalVersion,
                    };
                }
                catch (Exception exception)
                {
                    Serilog.Log.Warning(exception, "GitVersion unavailable; using the fallback version.");
                    VersionDetails = VersionDetails.BuildDefaultFallbackVersion();
                }
            }

            Serilog.Log.Information(
                "Version = {Version} (informational {Informational}).",
                VersionDetails.Version,
                VersionDetails.InformationalVersion);
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .DependsOn(GenerateVersionDetails)
        .Executes(() => DotNetBuild(s => s
            .SetProjectFile(SolutionFile)
            .SetConfiguration(Configuration)
            .EnableNoRestore()
            .SetVersion(VersionDetails.Version)
            .SetAssemblyVersion(VersionDetails.AssemblyVersion)
            .SetFileVersion(VersionDetails.FileVersion)
            .SetInformationalVersion(VersionDetails.InformationalVersion)));

    // Builds the documentation site reproducibly (docs-site/, Hugo + Docsy), isolated from the .NET solution.
    // `npm ci` installs the pinned Hugo Extended + PostCSS and initializes the Docsy submodule (the `prepare`
    // script); Hugo then renders the static site into docs-site/public. Requires Node/npm on PATH
    // (CI: actions/setup-node). The CI workflow uploads docs-site/public as the GitHub Pages artifact.
    Target Docs => _ => _
        .Executes(() =>
        {
            // Project deps (pinned Hugo Extended + PostCSS) + the Docsy submodule (the `prepare` script).
            NpmCi(s => s.SetProcessWorkingDirectory(DocsDirectory));

            // Vendor Docsy's SCSS deps (Bootstrap, Font Awesome) into themes/docsy/theme/node_modules, which its
            // SCSS imports. This is Docsy's own `install:theme-deps`, invoked directly (one npm process) to avoid
            // its postinstall re-spawning npm (which loses PATH under fnm on Windows). Requires Node >= 24 (Docsy).
            Npm("install --prefix themes/docsy/theme --omit=dev --omit=peer --no-audit --no-fund", DocsDirectory);

            // `npm run build` (= `hugo --minify`) renders the static site into docs-site/public.
            Npm($"run build -- --baseURL {DocsBaseUrl}", DocsDirectory);
        });

    // Unit/integration suite. The end-to-end project is excluded by project (not by a filter that would match
    // no tests, which makes a solution-wide `dotnet test` flake on the exit code), so the default build is
    // deterministic and needs no Docker daemon.
    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() => DotNetTest(s => s
            .SetConfiguration(Configuration)
            .EnableNoBuild()
            .CombineWith(
                RootDirectory.GlobFiles("tests/**/*Tests.csproj").Where(project => project != E2EProject),
                (settings, project) => settings.SetProjectFile(project))));

    Target Publish => _ => _
        .DependsOn(Compile)
        .Executes(() => DotNetPublish(s => s
            .SetProject(AppProject)
            .SetConfiguration(Configuration)
            .SetOutput(ArtifactsDirectory / "publish")
            .SetVersion(VersionDetails.Version)
            .SetAssemblyVersion(VersionDetails.AssemblyVersion)
            .SetFileVersion(VersionDetails.FileVersion)
            .SetInformationalVersion(VersionDetails.InformationalVersion)));

    // The single image-build path (local + CI). The Dockerfile's build stage runs the Nuke build (build.sh)
    // itself; this gates on tests, then buildx-builds a single-arch image and --loads it into the local Docker
    // daemon (for `docker run` / the E2E stack). Requires Docker + buildx on PATH.
    Target DockerImage => _ => _
        .DependsOn(Test)
        .DependsOn(GenerateVersionDetails)
        .Executes(() => ProcessTasks
            .StartProcess(
                "docker",
                $"buildx build --platform {Platforms} --build-arg VERSION={VersionDetails.Version} --load -t {FullImage} .",
                RootDirectory)
            .AssertZeroExitCode());

    // Build + push in one buildx step (multi-arch capable), also tagging :latest. The caller sets
    // --registry/--image-repository/--image-tag/--platforms and must already be authenticated to the registry
    // (`docker login`); the release CI does that then calls this target. Requires Docker + buildx on PATH.
    Target DockerPublish => _ => _
        .DependsOn(GenerateVersionDetails)
        .Executes(() => ProcessTasks
            .StartProcess(
                "docker",
                $"buildx build --platform {Platforms} --build-arg VERSION={VersionDetails.Version} --push -t {FullImage} -t {LatestImage} .",
                RootDirectory)
            .AssertZeroExitCode());

    // Opt-in end-to-end suite: builds the images the Aspire AppHost consumes (the proxy image and the echo
    // backend image), then runs the EndToEnd-categorized tests, which boot the distributed system on a real
    // Docker daemon. Requires Docker reachable by Aspire's DCP. Never a dependency of the default flow.
    Target E2E => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            ProcessTasks.StartProcess("docker", $"build -t {LocalProxyImage} .", RootDirectory).AssertZeroExitCode();
            DotNet($"publish \"{BackendProject}\" --configuration {Configuration} -t:PublishContainer");
            DotNet($"publish \"{GrpcBackendProject}\" --configuration {Configuration} -t:PublishContainer");

            // Capture per-resource logs to a durable directory so failures can be diagnosed after the
            // containers are torn down (cleaned at the start of each run so only the last run is kept).
            E2ELogDirectory.CreateOrCleanDirectory();
            try
            {
                DotNetTest(s => s
                    .SetProjectFile(E2EProject)
                    .SetConfiguration(Configuration)
                    .SetFilter($"TestCategory={EndToEndCategory}")
                    .SetProcessEnvironmentVariable("DOCKYARP_E2E_LOG_DIR", E2ELogDirectory));
            }
            catch
            {
                Serilog.Log.Error("E2E failed. Per-resource logs: {Directory}", E2ELogDirectory);
                AbsolutePath proxyLog = E2ELogDirectory / "dockyarp.log";
                if (proxyLog.FileExists())
                {
                    Serilog.Log.Error(
                        "---- dockyarp.log (tail) ----\n{Tail}",
                        string.Join("\n", proxyLog.ReadAllLines().TakeLast(60)));
                }

                throw;
            }
        });

    // Validates a version through the full quality gate, including the end-to-end suite.
    Target Release => _ => _
        .DependsOn(Test, E2E, DockerImage)
        .Executes(() => Serilog.Log.Information(
            "Release gate passed for version {Version}.",
            string.IsNullOrEmpty(Version) ? "(unspecified)" : Version));

    // Opt-in Compose smoke test: bring up the reference stack and probe the sample service by its
    // VIRTUAL_HOST, then tear it down. Not part of the default flow. Requires Docker (with the compose
    // plugin) on PATH.
    Target Smoke => _ => _
        .Executes(() =>
        {
            ProcessTasks.StartProcess("docker", "compose up -d --build", RootDirectory).AssertZeroExitCode();
            bool reachable;
            try
            {
                reachable = ProbeSampleService();
            }
            finally
            {
                ProcessTasks.StartProcess("docker", "compose down -v", RootDirectory).AssertWaitForExit();
            }

            if (reachable)
            {
                Serilog.Log.Information("Smoke OK: sample service reachable through DockYarp.");
            }

            Assert.True(reachable, "Smoke KO: sample service was not reachable through DockYarp.");
        });

    static bool ProbeSampleService()
    {
        using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(5) };
        client.DefaultRequestHeaders.Host = "whoami.local";
        for (int attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                using HttpRequestMessage request = new(HttpMethod.Get, "http://localhost/");
                using HttpResponseMessage response = client.Send(request);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch (HttpRequestException)
            {
                // Stack not ready yet.
            }

            Thread.Sleep(TimeSpan.FromSeconds(2));
        }

        return false;
    }
}
