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
            _commentToken = commentToken ?? throw new ArgumentNullException(nameof(commentToken));
        }

        public string Text => _commentToken.value;

        public int LineNumber => _commentToken.line;

        public IProjectFile ProjectFile { get; set; }

        public int Rating { get; set; }
    }
}
