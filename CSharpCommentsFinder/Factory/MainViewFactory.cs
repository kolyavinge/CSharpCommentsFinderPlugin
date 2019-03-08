using CSharpCommentsFinder.Model;
using CSharpCommentsFinder.View;
using CSharpCommentsFinder.ViewModel;
using Microsoft.VisualStudio.Shell;

namespace CSharpCommentsFinder.Factory
{
    public class MainViewFactory
    {
        public MainView Make()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = (EnvDTE.DTE)Package.GetGlobalService(typeof(EnvDTE.DTE));
            var projectsCollection = new ProjectsCollection(dte);
            var solutionEvents = new SolutionEvents(dte);
            var vm = new MainViewModel(projectsCollection, solutionEvents);
            var view = new MainView { DataContext = vm };

            return view;
        }
    }
}
