using CSharpCommentsFinder.CSharpGrammar;
using System.Collections.Generic;
using System.Linq;

namespace CSharpCommentsFinder.Model
{
    public class CommentsFinder
    {
        public IEnumerable<Comment> GetComments(string filePath)
        {
            var scanner = new Scanner(filePath);
            var allTokens = scanner.ScanAllTokens().ToList();
            var comments = allTokens
                .Where(t => t.kind == (int)TokenKinds.LineComment || t.kind == (int)TokenKinds.MultilineComment)
                .Select(c => new Comment(c) { Rating = GetRating(c) })
                .Where(c => c.Rating > 0);

            return comments;
        }

        private HashSet<int> _textCommentTokenKinds = new HashSet<int> { 1, 2, 87, 88, 91, 103, 139, 142 };

        private int GetRating(Token comment)
        {
            var tokens = GetTokens(comment);
            var kinds = tokens.Select(x => x.kind).ToList();
            var availableKinds = kinds.Except(_textCommentTokenKinds).ToList();

            return availableKinds.Count;
        }

        private IEnumerable<Token> GetTokens(Token comment)
        {
            var scanner = Scanner.FromText(comment.value);
            return scanner.ScanAllTokens();
        }
    }
}
