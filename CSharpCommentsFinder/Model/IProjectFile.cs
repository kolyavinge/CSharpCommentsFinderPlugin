using System.Collections.Generic;

namespace CSharpCommentsFinder.Model
{
    public interface IProjectFile
    {
        string Name { get; }

        string FullPath { get; }

        IEnumerable<IComment> GetComments();

        void NavigateTo(IComment comment);
    }
}
