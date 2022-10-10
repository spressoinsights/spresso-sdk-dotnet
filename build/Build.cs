using System;
using System.Collections.Generic;
using System.Linq;
using Crayon;
using Humanizer;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Utilities.Collections;
using Serilog;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;

class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main ()
    {
        PrintLogo();
        return Execute<Build>(x => x.Compile);
    }

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("Set target.  Defaults to entire solution")]
    readonly string TargetProject = null;

    [Solution(GenerateProjects = true)]
    readonly Solution Solution;

    private static readonly IOutput SpressoPurple = Crayon.Output.Rgb(58, 56, 169);

    private static void PrintLogo()
        =>
            Console.WriteLine(
                SpressoPurple.Text(
                    @"  


      ___          ___       ___          ___          ___          ___          ___     
     /  /\        /  /\     /  /\        /  /\        /  /\        /  /\        /  /\    
    /  /:/_      /  /::\   /  /::\      /  /:/_      /  /:/_      /  /:/_      /  /::\   
   /  /:/ /\    /  /:/\:\ /  /:/\:\    /  /:/ /\    /  /:/ /\    /  /:/ /\    /  /:/\:\  
  /  /:/ /::\  /  /:/~/://  /:/~/:/   /  /:/ /:/_  /  /:/ /::\  /  /:/ /::\  /  /:/  \:\ 
 /__/:/ /:/\:\/__/:/ /://__/:/ /:/___/__/:/ /:/ /\/__/:/ /:/\:\/__/:/ /:/\:\/__/:/ \__\:\
 \  \:\/:/~/:/\  \:\/:/ \  \:\/:::::/\  \:\/:/ /:/\  \:\/:/~/:/\  \:\/:/~/:/\  \:\ /  /:/
  \  \::/ /:/  \  \::/   \  \::/~~~~  \  \::/ /:/  \  \::/ /:/  \  \::/ /:/  \  \:\  /:/ 
   \__\/ /:/    \  \:\    \  \:\       \  \:\/:/    \__\/ /:/    \__\/ /:/    \  \:\/:/  
     /__/:/      \  \:\    \  \:\       \  \::/       /__/:/       /__/:/      \  \::/   
     \__\/        \__\/     \__\/        \__\/        \__\/        \__\/        \__\/ 


"));

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            DotNetTasks.DotNetClean(o => o.SetConfiguration(Configuration).SetProject(GetProjectFile()));
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetTasks.DotNetRestore();
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetTasks.DotNetBuild(o => o.SetConfiguration(Configuration).SetProjectFile(GetProjectFile()));
        });

    Target ListSdks => _ => _
        .Description("Lists all Spresso .NET SDKs")
        .Executes(() =>
        {
            foreach (var sdk in GetSdkProjects())
            {
                var name = sdk.Name.Replace("Spresso.Sdk.", "");
                name = name.Kebaberize();
                Console.WriteLine(Crayon.Output.Magenta(name));
            }
            
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTasks.DotNetTest(o => o.SetConfiguration(Configuration).SetProjectFile(GetProjectFile(true)));
        });

    private IReadOnlyCollection<Project> GetSdkProjects() => Solution.SolutionFolders.Single(sf => sf.Name == "SDKs").Projects;

    private string GetProjectFile(bool isTest = false)
    {
        if (String.IsNullOrEmpty(TargetProject))
            return Solution;

        var target = TargetProject.Dehumanize().ToLowerInvariant();
        if (isTest)
        {
            target += ".tests";
        }

        var project = Solution.AllProjects.SingleOrDefault(p => p.Name.ToLowerInvariant().EndsWith(target));
        if (project == null)
        {
            throw new ArgumentException($"{target} not found.  Run list-sdks for the list of SDKs");
        }


        Console.WriteLine(Crayon.Output.Magenta($"Targeting project {project}"));
        return project;
    }

}
