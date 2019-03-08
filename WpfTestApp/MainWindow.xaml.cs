using CSharpCommentsFinder.Model;
using CSharpCommentsFinder.View;
using CSharpCommentsFinder.ViewModel;
using Moq;
using System.Linq;
using System.Windows;

namespace WpfTestApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            //ScannerTest();

            var projectsCollection = MakeProjectsCollection();
            var solutionEvents = MakeSolutionEvents();
            var vm = new MainViewModel(projectsCollection, solutionEvents);
            var view = new MainView { DataContext = vm };
            mainGrid.Children.Add(view);
        }

        private IProjectsCollection MakeProjectsCollection()
        {
            var project1File1Comment1 = new Mock<IComment>();
            project1File1Comment1.SetupGet(x => x.Text).Returns("Text 1");
            project1File1Comment1.SetupGet(x => x.LineNumber).Returns(2);

            var project1File1Comment2 = new Mock<IComment>();
            project1File1Comment2.SetupGet(x => x.Text).Returns("Text 2");
            project1File1Comment2.SetupGet(x => x.LineNumber).Returns(2);

            var project1File1 = new Mock<IProjectFile>();
            project1File1.SetupGet(x => x.Name).Returns("File 1.cs");
            project1File1.SetupGet(x => x.FullPath).Returns(@"C:\Folder\File1.cs");
            project1File1.Setup(x => x.GetComments()).Returns(new[] { project1File1Comment1.Object, project1File1Comment2.Object });

            var project1 = new Mock<IProject>();
            project1.SetupGet(x => x.Name).Returns("Project 1");
            project1.SetupGet(x => x.AllFiles).Returns(new[] { project1File1.Object });

            var projectsCollection = new Mock<IProjectsCollection>();
            projectsCollection.SetupGet(x => x.Projects).Returns(new[] { project1.Object });

            return projectsCollection.Object;
        }

        private ISolutionEvents MakeSolutionEvents()
        {
            var solutionEvents = new Mock<ISolutionEvents>();

            return solutionEvents.Object;
        }

        private void ScannerTest()
        {
            var scanner = new CSharpCommentsFinder.CSharpGrammar.Scanner(@"D:\Projects\CSharpCommentsFinderPlugin\CSharpCommentsFinder\CSharpGrammar\Scanner.cs");
            var tokens = scanner.ScanAllTokens().ToList();
        }
    }
}
