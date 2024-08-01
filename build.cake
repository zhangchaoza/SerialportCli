///////////////////////////////////////////////////////////////////////////////
// TOOLS / ADDINS
///////////////////////////////////////////////////////////////////////////////

#tool dotnet:?package=GitVersion.Tool&version=6.0.0
#addin nuget:?package=Cake.FileHelpers&version=7.0.0

///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

using Spectre.Console;

var targets = Arguments("target", "Default");
var verbosity = DotNetVerbosity.Minimal;
var description = Argument("description", "SerialportCli");

///////////////////////////////////////////////////////////////////////////////
// PREPARATION
///////////////////////////////////////////////////////////////////////////////

var repoName = "SerialportCli";
var isLocal = BuildSystem.IsLocalBuild;

// // Set build version
if (isLocal == false || verbosity >= DotNetVerbosity.Normal)
{
    GitVersion(new GitVersionSettings { OutputType = GitVersionOutput.BuildServer });
}
GitVersion gitVersion = GitVersion(new GitVersionSettings { OutputType = GitVersionOutput.Json });

var branchName = gitVersion.BranchName;
var isDevelopBranch = StringComparer.OrdinalIgnoreCase.Equals("develop", branchName);
var isReleaseBranch = StringComparer.OrdinalIgnoreCase.Equals("main", branchName);
var isGitHubActionsBuild = GitHubActions.IsRunningOnGitHubActions;
var shortSha = gitVersion.Sha.Substring(0, 7);
var publishVersion = $"{gitVersion.FullSemVer}.{shortSha}";
var nugetVersion = $"{gitVersion.NuGetVersionV2}";

// Directories and Paths
var solution = "./src/SerialportCli.csproj";
var solution_dev = "./src/SerialportCli.csproj";
var solution_test = "./src/SerialportCli.sln";

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(ctx =>
{
    AnsiConsole.Write(new FigletText($"{repoName} (*^-^*)").LeftJustified().Color(Color.Green));

    Information("Informational   Version: {0}", gitVersion.InformationalVersion);
    Information("SemVer          Version: {0}", gitVersion.SemVer);
    Information("FullSemVer      Version: {0}", gitVersion.FullSemVer);
    Information("ShortSha        Version: {0}", shortSha);
    Information("AssemblySemVer  Version: {0}", gitVersion.AssemblySemVer);
    Information("MajorMinorPatch Version: {0}", gitVersion.MajorMinorPatch);
    Information("Debug           Version: {0}", $"{gitVersion.FullSemVer}.{shortSha}");
    Information("Publish         Version: {0}", publishVersion);
    Information("IsLocalBuild           : {0}", isLocal);
    Information("Targets                : {0}", string.Join(",", targets));
    Information("Branch                 : {0}", branchName);
    Information("IsDevelopBranch        : {0}", isDevelopBranch);
    Information("OsReleaseBranch        : {0}", isReleaseBranch);
    Information("IsGitHubActionsBuild   : {0}", isGitHubActionsBuild);
});

Teardown(ctx =>
{
});

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Task("Ver")
    .Description("show gitversion")
    .ContinueOnError()
    .Does(() =>
    {
    });

Task("Clean")
    .Description($"clean.")
    .Does(() =>
    {
        var directoriesToDelete = GetDirectories("./publish")
            // .Concat(GetDirectories("./bin"))
            // .Concat(GetDirectories("./*/bin"))
            // .Concat(GetDirectories("./*/obj"))
            .Concat(GetDirectories("./**/bin"))
            .Concat(GetDirectories("./**/obj"));

        var filesToDelete = GetFiles("../nugets/*.nupkg");

        foreach (var item in directoriesToDelete)
        {
            Information("DIR :{0}", item);
        }
        foreach (var item in filesToDelete)
        {
            Information("FILE:{0}", item);
        }
        DeleteDirectories(directoriesToDelete, new DeleteDirectorySettings { Recursive = true, Force = true });
        DeleteFiles(filesToDelete);
    });

Task("Test")
    .Description($"Test project. [{solution_test}]")
    .Does(() =>
    {
        var settings = new DotNetTestSettings
        {
            Verbosity = verbosity,
            Loggers = new List<string>() { "console;verbosity=detailed" }
        };
        DotNetTest(solution, settings);
    });

Task("Debug")
    .Description($"build with debug configuration. [{solution_dev}]")
    .Does(() =>
    {
        var buildConfiguration = Argument("configuration", "Debug");

        foreach (var r in new string[] { "win-x64", "win-x86" })
        {
            var msBuildSettings = new DotNetMSBuildSettings
            {
                ArgumentCustomization = args => args
                    .Append("-nodeReuse:true")
                ,
                BinaryLogger = new MSBuildBinaryLoggerSettings() { Enabled = isLocal }
            };

            msBuildSettings = msBuildSettings
                .SetMaxCpuCount(2)

                .WithProperty("SelfContained", "false")
                .WithProperty("RuntimeIdentifier", r)

                .WithProperty("AppendTargetFrameworkToOutputPath", "false")
                .WithProperty("AppendRuntimeIdentifierToOutputPath", "false")

                .WithProperty("Description", description)
                .WithProperty("Version", gitVersion.MajorMinorPatch)
                .WithProperty("AssemblyVersion", gitVersion.AssemblySemVer)
                .WithProperty("FileVersion", gitVersion.AssemblySemFileVer)
                .WithProperty("InformationalVersion", publishVersion);

            var buildSetting = new DotNetBuildSettings
            {
                MSBuildSettings = msBuildSettings,
                Configuration = buildConfiguration,
                OutputDirectory = $@"bin/{r}/{buildConfiguration}/",
                Verbosity = verbosity,
            };
            DotNetBuild(solution_dev, buildSetting);
        }
    });

Task("PublishWindows")
    .Description($"publish release configuration. [{solution}] .")
    .Does(() =>
    {
        var buildConfiguration = Argument("configuration", "Release");
        var msBuildSettings = new DotNetMSBuildSettings
        {
            ArgumentCustomization = args => args
                    .Append("-nodeReuse:false")
                ,
            BinaryLogger = new MSBuildBinaryLoggerSettings() { Enabled = isLocal }
        };

        msBuildSettings = msBuildSettings
            .SetMaxCpuCount(2)
            .WithProperty("DebugType", "none")
            .WithProperty("DebugSymbols", "false")
            .WithProperty("GenerateDocumentationFile", "false")
            .WithProperty("Description", description)
            .WithProperty("Version", gitVersion.MajorMinorPatch)
            .WithProperty("AssemblyVersion", gitVersion.AssemblySemVer)
            .WithProperty("FileVersion", gitVersion.AssemblySemFileVer)
            .WithProperty("InformationalVersion", publishVersion)
            .WithProperty("NativeBuild", "false")
            .WithProperty("TieredPGO", "true");

        foreach (var r in new string[]{"win-x64", "win-x86","win-arm64" })
        {
            var setting = new DotNetPublishSettings
            {
                MSBuildSettings = msBuildSettings,
                Configuration = buildConfiguration,
                Runtime = r,
                OutputDirectory = $@"Publish/{r}/{buildConfiguration}/",
                PublishSingleFile = true,
                SelfContained = true,
                PublishReadyToRun = true,
                IncludeNativeLibrariesForSelfExtract = true,
                EnableCompressionInSingleFile = false,
                PublishTrimmed = true,
                Verbosity = verbosity,
            };
            DotNetPublish(solution, setting);

            // clean obj for cross-compile
            DeleteDirectories(GetDirectories("./src/obj"), new DeleteDirectorySettings { Recursive = true, Force = true });

            // archive
            var archivePath = $"Publish/SerialportCli-v{publishVersion}-{r}.tar.gz";
            try { DeleteFile(archivePath); } catch { }
            using var fs = System.IO.File.OpenWrite(archivePath);
            using var gzips = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionLevel.SmallestSize, true);
            System.Formats.Tar.TarFile.CreateFromDirectory($"Publish/{r}/{buildConfiguration}", gzips, false);

            // github action output
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_REF")))
            {
                FileAppendText(Environment.GetEnvironmentVariable("GITHUB_ENV"), System.Text.Encoding.UTF8, $"ASSET_{r.Replace('-','_')}={archivePath}\n");
            }
        }

        // github action output
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_REF")))
        {
            var m = System.Text.RegularExpressions.Regex.Match(Environment.GetEnvironmentVariable("GITHUB_REF"), @"(?<=(/tags/))([\s\S]+)");
            var tag = m.Value;
            FileAppendText(Environment.GetEnvironmentVariable("GITHUB_ENV"), System.Text.Encoding.UTF8, $"TAG={tag}\n");
        }
    });

Task("PublishWindowsAot")
    .Description($"publish aot release configuration. [{solution}] .")
    .Does(() =>
    {
        var buildConfiguration = Argument("configuration", "Release");
        var msBuildSettings = new DotNetMSBuildSettings
        {
            ArgumentCustomization = args => args
                .Append("-nodeReuse:false")
            ,
            BinaryLogger = new MSBuildBinaryLoggerSettings() { Enabled = isLocal }
        };

        msBuildSettings = msBuildSettings
            .SetMaxCpuCount(2)
            .WithProperty("DebugType", "none")
            .WithProperty("DebugSymbols", "false")
            .WithProperty("GenerateDocumentationFile", "false")
            .WithProperty("Description", description)
            .WithProperty("Version", gitVersion.MajorMinorPatch)
            .WithProperty("AssemblyVersion", gitVersion.AssemblySemVer)
            .WithProperty("FileVersion", gitVersion.AssemblySemFileVer)
            .WithProperty("InformationalVersion", publishVersion)
            .WithProperty("NativeBuild", "true");

        foreach (var r in new string[] { "win-x64" })
        {
            var setting = new DotNetPublishSettings
            {
                MSBuildSettings = msBuildSettings,
                Configuration = buildConfiguration,
                Runtime = r,
                PublishTrimmed = true,
                SelfContained = true,
                PublishReadyToRun = true,
                OutputDirectory = $@"Publish/{r}-aot/{buildConfiguration}/",
                Verbosity = verbosity,
            };
            DotNetPublish(solution, setting);

            // clean obj for cross-compile
            DeleteDirectories(GetDirectories("./src/obj"), new DeleteDirectorySettings { Recursive = true, Force = true });

            // archive
            var archivePath = $"Publish/SerialportCli-v{publishVersion}-{r}-aot.tar.gz";
            try { DeleteFile(archivePath); } catch { }
            using var fs = System.IO.File.OpenWrite(archivePath);
            using var gzips = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionLevel.SmallestSize, true);
            System.Formats.Tar.TarFile.CreateFromDirectory($"Publish/{r}-aot/{buildConfiguration}", gzips, false);

            // github action output
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_REF")))
            {
                FileAppendText(Environment.GetEnvironmentVariable("GITHUB_ENV"), System.Text.Encoding.UTF8, $"ASSET_{r.Replace('-','_')}_aot={archivePath}\n");
            }
        }

        // github action output
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_REF")))
        {
            var m = System.Text.RegularExpressions.Regex.Match(Environment.GetEnvironmentVariable("GITHUB_REF"), @"(?<=(/tags/))([\s\S]+)");
            var tag = m.Value;
            FileAppendText(Environment.GetEnvironmentVariable("GITHUB_ENV"), System.Text.Encoding.UTF8, $"TAG={tag}\n");
        }
    });

Task("PublishLinux")
    .Description($"publish release configuration. [{solution}] .")
    .Does(() =>
    {
        var buildConfiguration = Argument("configuration", "Release");
        var msBuildSettings = new DotNetMSBuildSettings
        {
            ArgumentCustomization = args => args
                    .Append("-nodeReuse:false")
                ,
            BinaryLogger = new MSBuildBinaryLoggerSettings() { Enabled = isLocal }
        };

        msBuildSettings = msBuildSettings
            .SetMaxCpuCount(2)
            .WithProperty("DebugType", "none")
            .WithProperty("DebugSymbols", "false")
            .WithProperty("GenerateDocumentationFile", "false")
            .WithProperty("Description", description)
            .WithProperty("Version", gitVersion.MajorMinorPatch)
            .WithProperty("AssemblyVersion", gitVersion.AssemblySemVer)
            .WithProperty("FileVersion", gitVersion.AssemblySemFileVer)
            .WithProperty("InformationalVersion", publishVersion)
            .WithProperty("NativeBuild", "false")
            .WithProperty("TieredPGO", "true");

        foreach (var r in new string[]{"linux-x64", "linux-arm64"})
        {
            var setting = new DotNetPublishSettings
            {
                MSBuildSettings = msBuildSettings,
                Configuration = buildConfiguration,
                Runtime = r,
                OutputDirectory = $@"Publish/{r}/{buildConfiguration}/",
                PublishSingleFile = true,
                SelfContained = true,
                PublishReadyToRun = true,
                IncludeNativeLibrariesForSelfExtract = true,
                EnableCompressionInSingleFile = false,
                PublishTrimmed = true,
                Verbosity = verbosity,
            };
            DotNetPublish(solution, setting);

            // clean obj for cross-compile
            DeleteDirectories(GetDirectories("./src/obj"), new DeleteDirectorySettings { Recursive = true, Force = true });

            // archive
            var archivePath = $"Publish/SerialportCli-v{publishVersion}-{r}.tar.gz";
            try { DeleteFile(archivePath); } catch { }
            using var fs = System.IO.File.OpenWrite(archivePath);
            using var gzips = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionLevel.SmallestSize, true);
            System.Formats.Tar.TarFile.CreateFromDirectory($"Publish/{r}/{buildConfiguration}", gzips, false);

            // github action output
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_REF")))
            {
                FileAppendText(Environment.GetEnvironmentVariable("GITHUB_ENV"), System.Text.Encoding.UTF8, $"ASSET_{r.Replace('-','_')}={archivePath}\n");
            }
        }

        // github action output
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_REF")))
        {
            var m = System.Text.RegularExpressions.Regex.Match(Environment.GetEnvironmentVariable("GITHUB_REF"), @"(?<=(/tags/))([\s\S]+)");
            var tag = m.Value;
            FileAppendText(Environment.GetEnvironmentVariable("GITHUB_ENV"), System.Text.Encoding.UTF8, $"TAG={tag}\n");
        }
    });

Task("PublishLinuxAot")
    .Description($"publish aot release configuration. [{solution}] .")
    .Does(() =>
    {
        var buildConfiguration = Argument("configuration", "Release");
        var msBuildSettings = new DotNetMSBuildSettings
        {
            ArgumentCustomization = args => args
                .Append("-nodeReuse:false")
            ,
            BinaryLogger = new MSBuildBinaryLoggerSettings() { Enabled = isLocal }
        };

        msBuildSettings = msBuildSettings
            .SetMaxCpuCount(2)
            .WithProperty("DebugType", "none")
            .WithProperty("DebugSymbols", "false")
            .WithProperty("GenerateDocumentationFile", "false")
            .WithProperty("Description", description)
            .WithProperty("Version", gitVersion.MajorMinorPatch)
            .WithProperty("AssemblyVersion", gitVersion.AssemblySemVer)
            .WithProperty("FileVersion", gitVersion.AssemblySemFileVer)
            .WithProperty("InformationalVersion", publishVersion)
            .WithProperty("NativeBuild", "true");

        foreach (var r in new string[] { "linux-x64" })
        {
            var setting = new DotNetPublishSettings
            {
                MSBuildSettings = msBuildSettings,
                Configuration = buildConfiguration,
                Runtime = r,
                PublishTrimmed = true,
                SelfContained = true,
                PublishReadyToRun = true,
                OutputDirectory = $@"Publish/{r}-aot/{buildConfiguration}/",
                Verbosity = verbosity,
            };
            DotNetPublish(solution, setting);

            // clean obj for cross-compile
            DeleteDirectories(GetDirectories("./src/obj"), new DeleteDirectorySettings { Recursive = true, Force = true });

            // archive
            var archivePath = $"Publish/SerialportCli-v{publishVersion}-{r}-aot.tar.gz";
            try { DeleteFile(archivePath); } catch { }
            using var fs = System.IO.File.OpenWrite(archivePath);
            using var gzips = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionLevel.SmallestSize, true);
            System.Formats.Tar.TarFile.CreateFromDirectory($"Publish/{r}-aot/{buildConfiguration}", gzips, false);

            // github action output
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_REF")))
            {
                FileAppendText(Environment.GetEnvironmentVariable("GITHUB_ENV"), System.Text.Encoding.UTF8, $"ASSET_{r.Replace('-','_')}_aot={archivePath}\n");
            }
        }

        // github action output
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_REF")))
        {
            var m = System.Text.RegularExpressions.Regex.Match(Environment.GetEnvironmentVariable("GITHUB_REF"), @"(?<=(/tags/))([\s\S]+)");
            var tag = m.Value;
            FileAppendText(Environment.GetEnvironmentVariable("GITHUB_ENV"), System.Text.Encoding.UTF8, $"TAG={tag}\n");
        }
    });

Task("Pack")
    .Description($"pack as tool. [{solution}] .")
    .IsDependentOn("Clean")
    .Does(() =>
    {
        var buildConfiguration = Argument("configuration", "Release");
        var msBuildSettings = new DotNetMSBuildSettings
        {
            ArgumentCustomization = args => args
                .Append("-nodeReuse:false")
            ,
            BinaryLogger = new MSBuildBinaryLoggerSettings() { Enabled = isLocal }
        };

        msBuildSettings = msBuildSettings
            .SetMaxCpuCount(2)
            .WithProperty("DebugType", "none")
            .WithProperty("DebugSymbols", "false")
            .WithProperty("Description", description)
            .WithProperty("VersionPrefix", gitVersion.MajorMinorPatch)
            .WithProperty("AssemblyVersion", gitVersion.AssemblySemVer)
            .WithProperty("FileVersion", gitVersion.AssemblySemFileVer)
            .WithProperty("InformationalVersion", publishVersion)
            .WithProperty("NativeBuild", "false")
            .WithProperty("TieredPGO", "true")
            .WithProperty("PackAsTool", "true")
            .WithProperty("ToolCommandName", "SerialportCli");

        if (!isReleaseBranch)
        {
            msBuildSettings = msBuildSettings
                .WithProperty("VersionSuffix", gitVersion.FullSemVer);
        }

        var setting = new DotNetPackSettings
        {
            MSBuildSettings = msBuildSettings,
            Configuration = buildConfiguration,
            OutputDirectory = $@"Publish/tool/",
            Verbosity = verbosity,
        };
        DotNetPack(solution, setting);

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_REF")))
        {
            var archivePath = GetFiles($"Publish/tool/SerialportCli.{gitVersion.FullSemVer}.nupkg").First();
            var m = System.Text.RegularExpressions.Regex.Match(Environment.GetEnvironmentVariable("GITHUB_REF"), @"(?<=(/tags/))([\s\S]+)");
            var tag = m.Value;
            FileAppendText(Environment.GetEnvironmentVariable("GITHUB_ENV"), System.Text.Encoding.UTF8, $"TAG={tag}\n");
            FileAppendText(Environment.GetEnvironmentVariable("GITHUB_ENV"), System.Text.Encoding.UTF8, $"ASSET={archivePath}\n");
        }
    });

Task("Push")
    .Description("Push pack to nuget.org")
    .IsDependentOn("Pack")
    .Does(() =>
    {
        var apiKey =  Environment.GetEnvironmentVariable("NUGET_TOKEN") switch
        {
            null => AnsiConsole.Prompt(new TextPrompt<string>("Input api-key of nuget.org :").PromptStyle("red").Secret('*')),
            string s => s
        };

        var nugetFiles = GetFiles("./Publish/tool/*.nupkg");
        foreach (var nugetFile in nugetFiles)
        {
            Information("Push file to nuget.org, uploading {0}", nugetFile);
            Command(
                new[] { "dotnet", "dotnet.exe" },
                new ProcessArgumentBuilder()
                    .Append("nuget")
                    .Append("push")
                    .Append($"{nugetFile}")
                    .Append("--skip-duplicate")
                    .Append("--api-key")
                    .Append(apiKey)
                    .Append("-s")
                    .AppendQuoted("https://api.nuget.org/v3/index.json")
            );
        }
    });

///////////////////////////////////////////////////////////////////////////////
// TASK TARGETS
///////////////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Pack");

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTargets(targets);
