using CSharpCommentsFinder.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpCommentsFinderTests.Model
{
    [TestClass]
    public class CommentsFinderTest
    {
        private CommentsFinder _commentsFinder;

        [TestInitialize]
        public void Init()
        {
            _commentsFinder = new CommentsFinder();
        }

        [TestMethod]
        public void GetComments_Declaration()
        {
            var text = @"// int i = 1;";
            var comments = _commentsFinder.GetComments(text).ToList();
            Assert.AreEqual(1, comments.Count);
        }

        [TestMethod]
        public void GetComments_FunctionCall()
        {
            var text = @"// MyFunc(123);";
            var comments = _commentsFinder.GetComments(text).ToList();
            Assert.AreEqual(1, comments.Count);
        }

        [TestMethod]
        public void GetComments_Math()
        {
            var text = @"// a = b + c;";
            var comments = _commentsFinder.GetComments(text).ToList();
            Assert.AreEqual(1, comments.Count);
        }

        [TestMethod]
        public void GetComments_Comment1()
        {
            var text = @"// just comment";
            var comments = _commentsFinder.GetComments(text).ToList();
            Assert.AreEqual(0, comments.Count);
        }

        [TestMethod]
        public void GetComments_Comment2()
        {
            var text = @"// just, comment.";
            var comments = _commentsFinder.GetComments(text).ToList();
            Assert.AreEqual(0, comments.Count);
        }
    }
}
