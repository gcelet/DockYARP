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

    [Parameter("Docker image tag produced by the DockerImage target")]
    readonly string ImageTag = "dockyarp:local";

    AbsolutePath SolutionFile => RootDirectory / "DockYarp.slnx";
    AbsolutePath AppProject => RootDirectory / "src" / "DockYarp.App" / "DockYarp.App.csproj";
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

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() => DotNetTest(s => s
            .SetProjectFile(SolutionFile)
            .SetConfiguration(Configuration)
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
            .StartProcess("docker", $"build -t {ImageTag} .", RootDirectory)
            .AssertZeroExitCode());

    // Requires Docker (with the compose plugin) on PATH.
    Target E2E => _ => _
        .Executes(() =>
        {
            ProcessTasks.StartProcess("docker", "compose up -d --build", RootDirectory).AssertZeroExitCode();
            try
            {
                Assert.True(ProbeSampleService(), "Sample service was not reachable through DockYarp.");
            }
            finally
            {
                ProcessTasks.StartProcess("docker", "compose down -v", RootDirectory).AssertWaitForExit();
            }
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
