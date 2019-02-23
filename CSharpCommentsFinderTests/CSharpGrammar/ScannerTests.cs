using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
            Assert.AreEqual(" simple comment", tokens.First().val);
            Assert.AreEqual((int)TokenKinds.LineComment, tokens.First().kind);
        }

        [TestMethod]
        public void Scanner_SimpleComment2()
        {
            var csCode = @"// simple comment 1
// simple comment 2";
            var tokens = GetTokens(csCode);
            Assert.AreEqual(2, tokens.Count);
            Assert.AreEqual(" simple comment 1", tokens.First().val);
            Assert.AreEqual(" simple comment 2", tokens.Last().val);
            Assert.AreEqual((int)TokenKinds.LineComment, tokens.First().kind);
            Assert.AreEqual((int)TokenKinds.LineComment, tokens.Last().kind);
        }

        [TestMethod]
        public void Scanner_MultiLineComment1()
        {
            var csCode = @"/* multiline comment 1 */";
            var tokens = GetTokens(csCode);
            Assert.AreEqual(1, tokens.Count);
            Assert.AreEqual(" multiline comment 1 ", tokens.First().val);
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
1", tokens.First().val);
            Assert.AreEqual((int)TokenKinds.MultilineComment, tokens.First().kind);
        }

        [TestMethod]
        public void Scanner_MultiLineComment3()
        {
            var csCode = @"/* multiline comment 1 */
/* multiline comment 2 */";
            var tokens = GetTokens(csCode);
            Assert.AreEqual(2, tokens.Count);
            Assert.AreEqual(" multiline comment 1 ", tokens.First().val);
            Assert.AreEqual(" multiline comment 2 ", tokens.Last().val);
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
            Assert.AreEqual(1, tokens.Count);
            Assert.AreEqual((int)TokenKinds.XmlComment, tokens.First().kind);
        }

        private List<Token> GetTokens(string csCode)
        {
            var scanner = Scanner.FromText(csCode);
            return scanner.ScanAllTokens().ToList();
        }
    }
}
