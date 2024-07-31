///////////////////////////////////////////////////////////////////////////////
// TOOLS / ADDINS
///////////////////////////////////////////////////////////////////////////////

#tool dotnet:?package=GitVersion.Tool&version=5.12.0

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
var isTagged = AppVeyor.Environment.Repository.Tag.IsTag;
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
    Information("IsLocalBuild           : {0}", isLocal);
    Information("Target                 : {0}", string.Join(",", targets));
    Information("Branch                 : {0}", branchName);
    Information("IsDevelopBranch        : {0}", isDevelopBranch);
    Information("OsReleaseBranch        : {0}", isReleaseBranch);
    Information("IsTagged               : {0}", isTagged);
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
            .Concat(GetDirectories("./bin"))
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
                OutputDirectory = $@"bin\{r}\{buildConfiguration}\",
                Verbosity = verbosity,
            };
            DotNetBuild(solution_dev, buildSetting);
        }
    });

Task("Publish_win_x64")
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

        foreach (var r in new string[] { "win-x64", "win-x86" })
        {
            var setting = new DotNetPublishSettings
            {
                MSBuildSettings = msBuildSettings,
                Configuration = buildConfiguration,
                Runtime = r,
                OutputDirectory = $@"Publish\{r}\{buildConfiguration}\",
                PublishSingleFile = true,
                SelfContained = true,
                PublishReadyToRun = true,
                IncludeNativeLibrariesForSelfExtract = true,
                EnableCompressionInSingleFile = false,
                PublishTrimmed = true,
                Verbosity = verbosity,
            };
            DotNetPublish(solution, setting);

            // 为了交叉编译，需要清理以下obj
            DeleteDirectories(GetDirectories("./src/obj"), new DeleteDirectorySettings { Recursive = true, Force = true });
        }
    });

Task("Publish_win_x64_Aot")
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
                OutputDirectory = $@"Publish\{r}-aot\{buildConfiguration}\",
                Verbosity = verbosity,
            };
            DotNetPublish(solution, setting);

            // 为了交叉编译，需要清理以下obj
            DeleteDirectories(GetDirectories("./src/obj"), new DeleteDirectorySettings { Recursive = true, Force = true });
        }
    });

Task("Pack")
    .Description($"pack as tool. [{solution}] .")
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
                .WithProperty("VersionSuffix", gitVersion.NuGetPreReleaseTagV2);
        }

        var setting = new DotNetPackSettings
        {
            MSBuildSettings = msBuildSettings,
            Configuration = buildConfiguration,
            OutputDirectory = $@"Publish/tool/",
            Verbosity = verbosity,
        };
        DotNetPack(solution, setting);
    });

Task("Push")
    .Description("Push pack to nuget.org")
    .IsDependentOn("Clean")
    .IsDependentOn("Pack")
    .Does(() =>
    {
        var apiKey = AnsiConsole.Prompt(new TextPrompt<string>("Input api-key of nuget.org :").PromptStyle("red").Secret('*'));
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
    .IsDependentOn("Publish");

Task("Publish")
    .IsDependentOn("Clean")
    .IsDependentOn("Publish_win_x64")
    .IsDependentOn("Publish_win_x64_Aot")
    .IsDependentOn("Pack");

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTargets(targets);
