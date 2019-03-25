using System;
using System.IO;

namespace CSharpCommentsFinder.Utils
{
    public class Logger
    {
        public static void Info(string message)
        {
            File.WriteAllText(@"C:\CSharpCommentsFinder.log", message + Environment.NewLine);
        }
    }
}
