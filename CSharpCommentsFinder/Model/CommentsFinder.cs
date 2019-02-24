using CSharpCommentsFinder.CSharpGrammar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpCommentsFinder.Model
{
    public class CommentsFinder
    {
        public IEnumerable<Comment> GetComments(string text)
        {
            var scanner = Scanner.FromText(text);
            var allTokens = scanner.ScanAllTokens().ToList();
            var comments = allTokens.Where(t => t.kind == (int)TokenKinds.LineComment || t.kind == (int)TokenKinds.MultilineComment).ToList();
            var csCodeComments = comments.Where(IsCSharpCode).ToList();

            return csCodeComments.Select(c => new Comment(c));
        }

        private HashSet<int> _textCommentTokenKinds = new HashSet<int> { 1, 2, 88, 91, 139 };

        public bool IsCSharpCode(Token comment)
        {
            var scanner = Scanner.FromText(comment.value);
            var tokens = scanner.ScanAllTokens().ToList();
            var kinds = tokens.Select(x => x.kind).ToList();

            return kinds.Any(k => !_textCommentTokenKinds.Contains(k));
        }
    }
}
