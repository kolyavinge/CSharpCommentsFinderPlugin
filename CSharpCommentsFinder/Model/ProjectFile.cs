using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;

namespace CSharpCommentsFinder.Model
{
    public class ProjectFile : IProjectFile
    {
        private ProjectItem _projectItem;

        public ProjectFile(ProjectItem projectItem)
        {
            _projectItem = projectItem;
        }

        public string Name
        {
            get
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                return _projectItem.Name;
            }
        }

        public string FullPath
        {
            get
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                return _projectItem.FileNames[0];
            }
        }

        public IEnumerable<IComment> GetComments()
        {
            var fileContent = File.ReadAllText(FullPath);
            var commentsFinder = new CommentsFinder();
            var comments = commentsFinder.GetComments(fileContent);

            return comments;
        }
    }
}
