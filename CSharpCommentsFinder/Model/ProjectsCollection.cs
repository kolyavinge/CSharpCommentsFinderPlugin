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
                for (int i = 1; i <= _dte.Solution.Projects.Count; i++)
                {
                    var solutionProject = _dte.Solution.Projects.Item(i);
                    if (solutionProject.CodeModel.Language == CodeModelLanguageConstants.vsCMLanguageCSharp)
                    {
                        yield return new Project(solutionProject);
                    }
                }
            }
        }
    }
}
