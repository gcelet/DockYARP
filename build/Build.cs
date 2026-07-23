using System;
using System.Net.Http;
using System.Threading;

using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;

using static Nuke.Common.Tools.DotNet.DotNetTasks;

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

    [Parameter("Version validated by a release run")]
    readonly string Version = "";

    // NUnit category tagging the Aspire end-to-end suite; excluded by default, run by E2E/Release only.
    const string EndToEndCategory = "EndToEnd";

    // The AppHost consumes these fixed image names; E2E builds them before booting the distributed system.
    const string LocalProxyImage = "dockyarp:local";

    string FullImage => string.IsNullOrEmpty(Registry)
        ? $"{ImageRepository}:{ImageTag}"
        : $"{Registry}/{ImageRepository}:{ImageTag}";

    AbsolutePath SolutionFile => RootDirectory / "DockYarp.slnx";
    AbsolutePath AppProject => RootDirectory / "src" / "DockYarp.App" / "DockYarp.App.csproj";
    AbsolutePath BackendProject => RootDirectory / "tests" / "DockYarp.E2E.Backend" / "DockYarp.E2E.Backend.csproj";
    AbsolutePath E2EProject => RootDirectory / "tests" / "DockYarp.E2E.Tests" / "DockYarp.E2E.Tests.csproj";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() => ArtifactsDirectory.CreateOrCleanDirectory());

    Target Restore => _ => _
        .Executes(() => DotNetRestore(s => s.SetProjectFile(SolutionFile)));

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() => DotNetBuild(s => s
            .SetProjectFile(SolutionFile)
            .SetConfiguration(Configuration)
            .EnableNoRestore()));

    // Unit/integration suite. The end-to-end tests are excluded so the default build needs no Docker daemon.
    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() => DotNetTest(s => s
            .SetProjectFile(SolutionFile)
            .SetConfiguration(Configuration)
            .SetFilter($"TestCategory!={EndToEndCategory}")
            .EnableNoBuild()));

    Target Publish => _ => _
        .DependsOn(Compile)
        .Executes(() => DotNetPublish(s => s
            .SetProject(AppProject)
            .SetConfiguration(Configuration)
            .SetOutput(ArtifactsDirectory / "publish")));

    // The Dockerfile's build stage runs the Nuke build (build.sh) itself; this just gates on tests
    // and invokes `docker build`. Requires Docker on PATH.
    Target DockerImage => _ => _
        .DependsOn(Test)
        .Executes(() => ProcessTasks
            .StartProcess("docker", $"build -t {FullImage} .", RootDirectory)
            .AssertZeroExitCode());

    // Pushes the image to the configured registry (Docker Hub by default). Assumes the environment is
    // already authenticated (`docker login`). Requires Docker on PATH.
    Target DockerPublish => _ => _
        .DependsOn(DockerImage)
        .Executes(() => ProcessTasks
            .StartProcess("docker", $"push {FullImage}", RootDirectory)
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
            DotNetTest(s => s
                .SetProjectFile(E2EProject)
                .SetConfiguration(Configuration)
                .SetFilter($"TestCategory={EndToEndCategory}"));
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
