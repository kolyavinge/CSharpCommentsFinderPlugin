using System;
using System.Runtime.InteropServices;
using CSharpCommentsFinder.Factory;
using Microsoft.VisualStudio.Shell;

namespace CSharpCommentsFinder.VsToolWindow
{
    [Guid("62d5fb1a-f235-4c37-8b61-0af3da9757ce")]
    public class ToolWindow : ToolWindowPane
    {
        public ToolWindow() : base(null)
        {
            Caption = "CSharp Comments Finder";
            var mainViewFactory = new MainViewFactory();
            var mainView = mainViewFactory.Make();
            var toolWindowControl = new ToolWindowControl();
            toolWindowControl.rootLayout.Children.Add(mainView);
            Content = toolWindowControl;
        }
    }
}
