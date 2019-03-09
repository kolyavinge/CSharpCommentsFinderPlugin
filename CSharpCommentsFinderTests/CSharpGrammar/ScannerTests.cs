using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace CSharpCommentsFinder.CSharpGrammar.Tests
{
    [TestClass]
    public class ScannerTests
    {
        [TestMethod]
        public void Scanner_SimpleComment1()
        {
            var csCode = @"// simple comment";
            var tokens = GetTokens(csCode);
            Assert.AreEqual(1, tokens.Count);
            Assert.AreEqual(" simple comment", tokens.First().value);
            Assert.AreEqual((int)TokenKinds.LineComment, tokens.First().kind);
        }

        [TestMethod]
        public void Scanner_SimpleComment2()
        {
            var csCode = @"// simple comment 1
// simple comment 2";
            var tokens = GetTokens(csCode);
            Assert.AreEqual(2, tokens.Count);
            Assert.AreEqual(" simple comment 1", tokens.First().value);
            Assert.AreEqual(" simple comment 2", tokens.Last().value);
            Assert.AreEqual((int)TokenKinds.LineComment, tokens.First().kind);
            Assert.AreEqual((int)TokenKinds.LineComment, tokens.Last().kind);
        }

        [TestMethod]
        public void Scanner_SimpleComment3()
        {
            var csCode = @"// комментарий 1
// комментарий 2";
            var tokens = GetTokens(csCode);
            Assert.AreEqual(2, tokens.Count);
            Assert.AreEqual(" комментарий 1", tokens.First().value);
            Assert.AreEqual(" комментарий 2", tokens.Last().value);
            Assert.AreEqual((int)TokenKinds.LineComment, tokens.First().kind);
            Assert.AreEqual((int)TokenKinds.LineComment, tokens.Last().kind);
            Assert.AreEqual(1, tokens.First().line);
            Assert.AreEqual(2, tokens.Last().line);
        }

        [TestMethod]
        public void Scanner_MultiLineComment1()
        {
            var csCode = @"/* multiline comment 1 */";
            var tokens = GetTokens(csCode);
            Assert.AreEqual(1, tokens.Count);
            Assert.AreEqual(" multiline comment 1 ", tokens.First().value);
            Assert.AreEqual((int)TokenKinds.MultilineComment, tokens.First().kind);
        }

        [TestMethod]
        public void Scanner_MultiLineComment2()
        {
            var csCode = @"/* multiline
comment
1*/";
            var tokens = GetTokens(csCode);
            Assert.AreEqual(1, tokens.Count);
            Assert.AreEqual(@" multiline
comment
1", tokens.First().value);
            Assert.AreEqual((int)TokenKinds.MultilineComment, tokens.First().kind);
            Assert.AreEqual(1, tokens.First().line);
        }

        [TestMethod]
        public void Scanner_MultiLineComment3()
        {
            var csCode = @"/* multiline comment 1 */
/* multiline comment 2 */";
            var tokens = GetTokens(csCode);
            Assert.AreEqual(2, tokens.Count);
            Assert.AreEqual(" multiline comment 1 ", tokens.First().value);
            Assert.AreEqual(" multiline comment 2 ", tokens.Last().value);
            Assert.AreEqual((int)TokenKinds.MultilineComment, tokens.First().kind);
            Assert.AreEqual((int)TokenKinds.MultilineComment, tokens.Last().kind);
        }

        [TestMethod]
        public void Scanner_XmlComment()
        {
            var csCode = @"
/// <summary>
/// Summary
/// </summary>";
            var tokens = GetTokens(csCode);
            Assert.AreEqual(3, tokens.Count);
            Assert.AreEqual((int)TokenKinds.XmlComment, tokens[0].kind);
            Assert.AreEqual((int)TokenKinds.XmlComment, tokens[1].kind);
            Assert.AreEqual((int)TokenKinds.XmlComment, tokens[2].kind);
        }

        [TestMethod]
        public void Scanner_CommentLineNumber()
        {
            var csCode = @"internal class IndexBase : IComparable<IndexBase>
{
    internal short Index;
    public int CompareTo(IndexBase other)
    {
        //return Index < other.Index ? -1 : Index > other.Index ? 1 : 0;
        return Index - other.Index;
    }
}";
            var tokens = GetTokens(csCode);
            var commentToken = tokens.First(x => x.kind == (int)TokenKinds.LineComment);
            Assert.AreEqual("return Index < other.Index ? -1 : Index > other.Index ? 1 : 0;", commentToken.value);
            Assert.AreEqual(6, commentToken.line);
        }

        [TestMethod]
        public void Scanner_CommentLineNumber2()
        {
            var csCode = @"internal class IndexBase : IComparable<IndexBase>
{
    internal short Index;
    public int CompareTo(IndexBase other)
    {
        /*return Index < other.Index ? -1 : Index > other.Index ? 1 : 0;*/
        return Index - other.Index;
    }
}";
            var tokens = GetTokens(csCode);
            var commentToken = tokens.First(x => x.kind == (int)TokenKinds.MultilineComment);
            Assert.AreEqual("return Index < other.Index ? -1 : Index > other.Index ? 1 : 0;", commentToken.value);
            Assert.AreEqual(6, commentToken.line);
        }

        private List<Token> GetTokens(string csCode)
        {
            var scanner = Scanner.FromText(csCode);
            return scanner.ScanAllTokens().ToList();
        }
    }
}
