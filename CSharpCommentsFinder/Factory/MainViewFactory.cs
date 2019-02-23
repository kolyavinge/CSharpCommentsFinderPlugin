using CSharpCommentsFinder.Model;
using CSharpCommentsFinder.View;
using CSharpCommentsFinder.ViewModel;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace CSharpCommentsFinder.Factory
{
    public class MainViewFactory
    {
        public MainView Make()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = (DTE)Package.GetGlobalService(typeof(DTE));
            var projectsCollection = new ProjectsCollection(dte);
            var vm = new MainViewModel(projectsCollection);
            var view = new MainView { DataContext = vm };

            return view;
        }
    }
}
