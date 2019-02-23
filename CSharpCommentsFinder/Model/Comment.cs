using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpCommentsFinder.Model
{
    public class Comment : IComment
    {
        public string Text => throw new NotImplementedException();

        public int StartPosition => throw new NotImplementedException();

        public int EndPosition => throw new NotImplementedException();
    }
}
