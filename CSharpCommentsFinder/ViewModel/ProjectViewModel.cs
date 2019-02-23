using CSharpCommentsFinder.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpCommentsFinder.ViewModel
{
    public class ProjectViewModel : ItemViewModel<IProject>
    {
        public ProjectViewModel(IProject item) : base(item)
        {
        }
    }
}
