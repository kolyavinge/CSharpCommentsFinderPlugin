using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpCommentsFinder.Model
{
    public interface IProjectFile
    {
        string Name { get; }

        string FullPath { get; }

        IEnumerable<IComment> GetComments();
    }
}
