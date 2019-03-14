using EnvDTE;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CSharpCommentsFinder.Model
{
    public class ProjectFile : IProjectFile
    {
        private ProjectItem _projectItem;

        public ProjectFile(ProjectItem projectItem)
        {
            _projectItem = projectItem ?? throw new ArgumentNullException(nameof(projectItem));
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            Name = _projectItem.Name;
            FullPath = _projectItem.FileNames[0];
        }

        public string Name { get; private set; }

        public string FullPath { get; private set; }

        public IEnumerable<IComment> GetComments()
        {
            var commentsFinder = new CommentsFinder();
            var comments = commentsFinder.GetComments(FullPath).ToList();
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
