using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;

namespace CSharpCommentsFinder.Model
{
    public class ProjectsCollection : IProjectsCollection
    {
        private readonly DTE2 _dte;

        public ProjectsCollection(DTE2 dte)
        {
            _dte = dte;
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
