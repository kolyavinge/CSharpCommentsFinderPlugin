using EnvDTE;
using System;
using System.Collections.Generic;
using System.IO;

namespace CSharpCommentsFinder.Model
{
    public class Project : IProject
    {
        private EnvDTE.Project _solutionProject;

        public Project(EnvDTE.Project solutionProject)
        {
            _solutionProject = solutionProject ?? throw new ArgumentNullException(nameof(solutionProject));
        }

        public string Name
        {
            get
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                return _solutionProject.Name;
            }
        }

        public IEnumerable<IProjectFile> AllFiles
        {
            get
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                var allFiles = new List<IProjectFile>();
                for (int i = 1; i <= _solutionProject.ProjectItems.Count; i++)
                {
                    var projectItem = _solutionProject.ProjectItems.Item(i);
                    FindCSFiles(projectItem, allFiles);
                }

                return allFiles;
            }
        }

        private void FindCSFiles(ProjectItem parent, List<IProjectFile> allFiles)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            if (parent.Kind == Constants.vsProjectItemKindPhysicalFile
                && parent.FileCount == 1
                && Path.GetExtension(parent.FileNames[0]).Equals(".cs", StringComparison.InvariantCultureIgnoreCase))
            {
                allFiles.Add(new ProjectFile(parent));
            }
            else
            {
                for (int i = 1; i < parent.ProjectItems.Count; i++)
                {
                    var projectItem = parent.ProjectItems.Item(i);
                    FindCSFiles(projectItem, allFiles);
                }
            }
        }
    }
}
