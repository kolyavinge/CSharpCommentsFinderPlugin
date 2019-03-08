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

        public string FormattedText
        {
            get
            {
                var text = Item.Text.TrimStart();
                var carretIndex = text.IndexOfAny(new char[] { '\r', '\n' });
                return carretIndex == -1 ? text : text.TrimStart().Substring(0, carretIndex);
            }
        }
    }
}
