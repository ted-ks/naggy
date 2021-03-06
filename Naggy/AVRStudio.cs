﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Shell;
using EnvDTE;
using System.IO;

namespace Naggy
{
    static class AVRStudio
    {
        public static IEnumerable<string> GetPredefinedSymbols(string fileName, DTE dte)
        {
            var project = GetProject(dte, fileName);

            if (project == null)
            {
                return Enumerable.Empty<string>();
            }

            string deviceName = (string)project.Properties.Item("DeviceName").Value;
            var implicitSymbol = DeviceNameToPredefinedSymbolMapper.GetSymbol(deviceName);

            dynamic toolchainOptions = project.Properties.Item("ToolchainOptions").Value;
            var symbolsInProject = GetPredefinedSymbols(toolchainOptions);

            var predefinedSymbols = new List<string>();
            predefinedSymbols.AddRange(implicitSymbol);

            predefinedSymbols.AddRange(symbolsInProject);
            return predefinedSymbols;
        }

        public static bool IsC99Enabled(string fileName, DTE dte)
        {
            var project = GetProject(dte, fileName);

            if (project == null)
                return false;

            dynamic toolchainOptions = project.Properties.Item("ToolchainOptions").Value;
            var commandLine = (string) toolchainOptions.CCompiler.CommandLine;
            return commandLine != null && (commandLine.Contains("-std=gnu99") || commandLine.Contains("-std=c99"));
        }

        public static IEnumerable<string> GetIncludePaths(string fileName, DTE dte)
        {
            var project = GetProject(dte, fileName);

            // Before giving up, see if it is a file inside the toolchain dirs, if so, find 
            // a project with the same toolchain dir and returns the DefaultIncludePaths
            if (project == null)
                return Enumerable.Empty<string>();

            dynamic toolchainOptions = project.Properties.Item("ToolchainOptions").Value;
            IEnumerable<string> defaultIncludePaths = toolchainOptions.CCompiler.DefaultIncludePaths;

            var adjustedDefaultIncludePaths = defaultIncludePaths
                .Select(p => p.Replace("bin\\", string.Empty));
            
            IEnumerable<string> projectSpecificIncludePaths = toolchainOptions.CCompiler.IncludePaths;
            string outputFolder = ((dynamic)project.Object).GetProjectProperty("OutputDirectory");
            var absoluteProjectSpecificFolderPaths = projectSpecificIncludePaths
                .Select(p => Path.IsPathRooted(p) ? p : Path.Combine(outputFolder, p));
            
            return adjustedDefaultIncludePaths.Concat(absoluteProjectSpecificFolderPaths);
        }

        private static string[] GetPredefinedSymbols(dynamic toolchainOptions)
        {
            return toolchainOptions.CCompiler.SymbolDefines.ToArray();
        }

        internal static ProjectItem GetProjectItem(DTE dte, string fileName)
        {
            if (dte.Solution == null)
                return null;

            return dte.Solution.FindProjectItem(fileName);
        }

        static Project GetProject(DTE dte, string fileName)
        {
            var projectItem = GetProjectItem(dte, fileName);
            if (projectItem != null && projectItem.ContainingProject != null && projectItem.ContainingProject.Properties != null)
                return projectItem.ContainingProject;

            // The file was not a project item. See if we can find it any project's toolchain header paths.
            var project = GetPossibleProjectBasedOnToolchainHeaderPath(fileName, dte);
            if (project != null)
                return project;

            // Otherwise we're out of options, just return the first project we find.
            var projects = GetProjectsInSolution(dte);
            return projects.FirstOrDefault();
        }

        static IEnumerable<Project> GetProjectsInSolution(DTE dte)
        {
            var projects = dte.Solution.Projects;
            for (int i = 1; i <= projects.Count; ++i)
                yield return projects.Item(i);
        }

        private static Project GetPossibleProjectBasedOnToolchainHeaderPath(string fileName, DTE dte)
        {
            foreach (var project in GetProjectsInSolution(dte))
            {
                dynamic toolchainOptions = project.Properties.Item("ToolchainOptions").Value;
                IEnumerable<string> defaultIncludePaths = toolchainOptions.CCompiler.DefaultIncludePaths;
                if (defaultIncludePaths.Any(fileName.Contains))
                    return project;
            }

            return null;
        }
    }
}
