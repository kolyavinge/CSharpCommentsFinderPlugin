using EnvDTE;
using System;
using System.Collections.Generic;

namespace CSharpCommentsFinder.Model
{
    public class ProjectsCollection : IProjectsCollection
    {
        private readonly DTE _dte;

        public ProjectsCollection(DTE dte)
        {
            _dte = dte ?? throw new ArgumentNullException(nameof(dte));
        }

        public IEnumerable<IProject> Projects
        {
            get
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                var result = new List<IProject>();
                for (int i = 1; i <= _dte.Solution.Projects.Count; i++)
                {
                    var project = _dte.Solution.Projects.Item(i);
                    FindAllProjects(project, result);
                }

                return result;
            }
        }

        private void FindAllProjects(EnvDTE.Project parentProjectItem, List<IProject> result)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            if (parentProjectItem.ProjectItems == null) return;
            for (int i = 1; i <= parentProjectItem.ProjectItems.Count; i++)
            {
                var projectItem = parentProjectItem.ProjectItems.Item(i).Object as EnvDTE.Project;
                if (projectItem == null) continue;
                if (projectItem.CodeModel != null &&
                    projectItem.CodeModel.Language == CodeModelLanguageConstants.vsCMLanguageCSharp)
                {
                    result.Add(new Project(projectItem));
                }
                else
                {
                    FindAllProjects(projectItem, result);
                }
            }
        }
    }
}
