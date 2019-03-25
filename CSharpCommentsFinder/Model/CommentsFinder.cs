using CSharpCommentsFinder.CSharpGrammar;
using CSharpCommentsFinder.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CSharpCommentsFinder.Model
{
    public class CommentsFinder
    {
        public IEnumerable<Comment> GetComments(string filePath)
        {
            try
            {
                var scanner = new Scanner(filePath);
                var allTokens = scanner.ScanAllTokens().ToList();
                var comments = allTokens
                    .Where(t => t.kind == (int)TokenKinds.LineComment || t.kind == (int)TokenKinds.MultilineComment)
                    .Select(c => new Comment(c) { Rating = GetRating(c) })
                    .Where(c => c.Rating > 0);

                return comments;
            }
            catch (Exception e)
            {
                Logger.Info(e.ToString());
                return Enumerable.Empty<Comment>();
            }
        }

        private HashSet<int> _textCommentTokenKinds = new HashSet<int> { 1, 2, 87, 88, 91, 103, 139, 142 };

        private int GetRating(Token comment)
        {
            int rating = 0;

            var tokens = GetTokens(comment).ToList();
            //var literalsCount = tokens.Count(t => t.isLiteral);
            var kinds = tokens.Select(x => x.kind).ToList();
            var availableKinds = kinds.Except(_textCommentTokenKinds).ToList();

            rating = availableKinds.Count;

            return rating;
        }

        private IEnumerable<Token> GetTokens(Token comment)
        {
            var scanner = Scanner.FromText(comment.value);
            return scanner.ScanAllTokens();
        }
    }
}
