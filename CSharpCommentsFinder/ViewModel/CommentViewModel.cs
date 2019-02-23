using CSharpCommentsFinder.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpCommentsFinder.ViewModel
{
    public class CommentViewModel : ItemViewModel<IComment>
    {
        public CommentViewModel(IComment item) : base(item)
        {
        }
    }
}
