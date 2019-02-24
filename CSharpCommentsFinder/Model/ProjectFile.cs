using EnvDTE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CSharpCommentsFinder.Model
{
    public class ProjectFile : IProjectFile
    {
        private ProjectItem _projectItem;

        public ProjectFile(ProjectItem projectItem)
        {
            _projectItem = projectItem ?? throw new ArgumentNullException(nameof(projectItem));
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
            var comments = commentsFinder.GetComments(fileContent).ToList();
            comments.ForEach(c => c.ProjectFile = this);

            return comments;
        }

        public void NavigateTo(IComment comment)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            var window = _projectItem.Open();
            window.Activate();
            var selection = (TextSelection)window.Document.Selection;
            selection.MoveToLineAndOffset(comment.LineNumber, 1);
        }
    }
}
