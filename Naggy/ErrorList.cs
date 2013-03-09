﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NaggyClang;

namespace Naggy
{
    static class ErrorList
    {
        private static ErrorListProvider errorListProvider;
        private static DTE dte;

        public static void Initialize(IServiceProvider provider, DTE _dte)
        {
            dte = _dte;
            if (errorListProvider != null)
                return;

            errorListProvider = new ErrorListProvider(provider);
            errorListProvider.ProviderGuid = Guid.Parse("7C2C89EC-D368-4B15-B93A-E506EEA449E4");
            errorListProvider.ProviderName = "Naggy.DiagnosticsProvider";
        }

        public static void ClearDiagnosticsFromFile(string filePath)
        {
            var tasksToDelete = new List<Task>();
            foreach (ErrorTask task in errorListProvider.Tasks)
            {
                if (task.Document == filePath)
                    tasksToDelete.Add(task);
            }

            foreach(var task in tasksToDelete)
                errorListProvider.Tasks.Remove(task);
        }

        public static void Show(Diagnostic diag)
        {
            var task = new ErrorTask
                           {
                               Text = diag.ID + " : " + diag.Message,
                               Category = TaskCategory.CodeSense,
                               ErrorCategory =
                                   diag.Level == DiagnosticLevel.Warning
                                       ? TaskErrorCategory.Warning
                                       : TaskErrorCategory.Error,
                               Column = diag.StartColumn,
                               Line = diag.StartLine,
                               Document = diag.FilePath,
                               HierarchyItem = (IVsHierarchy)AVRStudio.GetProjectItem(dte, diag.FilePath).Object,
                           };
            task.Navigate += (sender, args) =>
                                 {
                                     task.Line++;
                                     errorListProvider.Navigate(task, Guid.Parse(EnvDTE.Constants.vsViewKindCode));
                                     task.Line--;
                                 };

            errorListProvider.Tasks.Add(task);
            errorListProvider.Show();
        }
    }
}