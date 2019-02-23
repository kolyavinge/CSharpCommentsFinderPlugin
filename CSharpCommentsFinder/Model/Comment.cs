using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSharpCommentsFinder.CSharpGrammar;

namespace CSharpCommentsFinder.Model
{
    public class Comment : IComment
    {
        private Token _commentToken;

        public Comment(Token commentToken)
        {
            _commentToken = commentToken;
        }

        public string Text => _commentToken.val;

        public int StartPosition => _commentToken.charPos;

        public int EndPosition => _commentToken.charPos + _commentToken.pos;
    }
}
