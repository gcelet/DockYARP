using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;

using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using Fallout.Common.Tools.Docker;
using Fallout.Common.Tools.DotNet;
using Fallout.Common.Tools.GitVersion;
using Fallout.Common.Tools.Npm;

using static Fallout.Common.Tools.Docker.DockerTasks;
using static Fallout.Common.Tools.DotNet.DotNetTasks;
using static Fallout.Common.Tools.Npm.NpmTasks;

class Build : FalloutBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("Container registry host; empty targets Docker Hub")]
    readonly string Registry = "";

    [Parameter("Image repository name")]
    readonly string ImageRepository = "dockyarp";

    [Parameter("Image tag (local DockerImage builds only)")]
    readonly string ImageTag = "latest";

    [Parameter("DockerPublish: publish exactly this one tag, bypassing the computed release/prerelease/edge scheme (e.g. base-image-refresh.yml's ':latest'-only republish)")]
    readonly string PublishTag = "";

    [Parameter("DockerPublish: also tag the image 'edge' (in-development builds off the trunk branch)")]
    readonly bool Edge;

    [Parameter("DockerPublish: skip the Test/E2E gate before pushing — for a quick manual/local push only; CI never sets this")]
    readonly bool SkipPublishGate;

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

    string ImageRef(string tag) => string.IsNullOrEmpty(Registry)
        ? $"{ImageRepository}:{tag}"
        : $"{Registry}/{ImageRepository}:{tag}";

    string FullImage => ImageRef(ImageTag);

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

    // Depends on GenerateVersionDetails only, not Compile: this target's sole caller is the Dockerfile's build
    // stage, and DotNetPublish restores + builds AppProject's own graph on its own. A prior version depended on
    // Compile (a full-solution `dotnet build`), which meant every image build also built the E2E/test projects
    // for no reason — including DockYarp.E2E.GrpcBackend/E2E.Tests, whose Grpc.Tools native protoc/plugin
    // binaries segfault under QEMU emulation on a multi-arch (edge) build. Confirmed via a real CI failure
    // (`qemu : error : uncaught target signal 11 (Segmentation fault)`), not assumed. Narrowing this to just
    // AppProject's own graph removes both the wasted work and the crash; test/E2E gating already happens
    // elsewhere (the outer Test/Release targets), which this inner, Dockerfile-only invocation never reaches.
    Target Publish => _ => _
        .DependsOn(GenerateVersionDetails)
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
        .Executes(() => DockerBuildxBuild(s => s
            .SetProcessWorkingDirectory(RootDirectory)
            .SetPath(".")
            .SetPlatform(Platforms)
            .SetBuildArg($"VERSION={VersionDetails.Version}")
            .AddTag(FullImage)
            .EnableLoad()));

    // Build + push in one buildx step (multi-arch capable). The published tag set depends on the release
    // channel (PublishTags) unless the caller pins one explicit tag via --publish-tag. The caller sets
    // --registry/--image-repository/--platforms and must already be authenticated to the registry
    // (`docker login`); the release CI does that then calls this target. Requires Docker + buildx on PATH.
    //
    // Uses the DockerBuildxBuild tool task (typed AddTag(params string[])) rather than a hand-built process
    // argument string: an earlier version concatenated "-t {value}" pairs into one interpolated string, which
    // hit ProcessTasks.StartProcess's ArgumentStringHandler auto-quoting a single value containing a space as
    // ONE argv token — docker received "-t <value>" glued together (with the space baked into the tag), not
    // two separate arguments, and rejected it as an invalid reference. Caught by a live registry push, not by
    // this file's own tests. The typed settings API sidesteps the whole class of bug.
    // Gated on Test + E2E directly on this target (not just co-listed under a wrapper) — Nuke's DependsOn
    // guarantees a target's own dependencies run, and must succeed, before its own Executes body starts;
    // listing unrelated targets side by side (e.g. on a CI command line) does NOT give that guarantee, since
    // nothing then constrains their relative order or blocks this target's push on their failure. Matches the
    // precedent already set by DockerImage.DependsOn(Test). --skip-publish-gate opts out for a quick
    // manual/local push only; CI never sets it, so a release publish is always gated by default.
    Target DockerPublish => _ => _
        .DependsOn(GenerateVersionDetails)
        .DependsOn(SkipPublishGate ? [] : [Test, E2E])
        .Executes(() => DockerBuildxBuild(s => s
            .SetProcessWorkingDirectory(RootDirectory)
            .SetPath(".")
            .SetPlatform(Platforms)
            .SetBuildArg($"VERSION={VersionDetails.Version}")
            .AddTag(PublishTags().Select(ImageRef).ToArray())
            .EnablePush()));

    // Stable (no pre-release suffix): the exact version + rolling Major.Minor + Major + latest.
    // Prerelease: the exact version only (rolling tags/latest are left untouched). --edge additionally tags
    // "edge" (the in-development channel, set by the develop-triggered publish job). --publish-tag bypasses
    // this scheme entirely — used by base-image-refresh.yml to republish exactly ":latest".
    IReadOnlyList<string> PublishTags()
    {
        if (!string.IsNullOrEmpty(PublishTag))
        {
            return [PublishTag];
        }

        List<string> tags = [VersionDetails.Version];
        if (string.IsNullOrEmpty(VersionDetails.PackageVersionSuffix))
        {
            string[] parts = VersionDetails.PackageVersionPrefix.Split('.');
            tags.Add($"{parts[0]}.{parts[1]}");
            tags.Add(parts[0]);
            tags.Add("latest");
        }

        if (Edge)
        {
            tags.Add("edge");
        }

        return tags;
    }

    // Opt-in end-to-end suite: builds the images the Aspire AppHost consumes (the proxy image and the echo
    // backend image), then runs the EndToEnd-categorized tests, which boot the distributed system on a real
    // Docker daemon. Requires Docker reachable by Aspire's DCP. Never a dependency of the default flow.
    Target E2E => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DockerImageBuild(s => s
                .SetProcessWorkingDirectory(RootDirectory)
                .SetPath(".")
                .AddTag(LocalProxyImage));
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
