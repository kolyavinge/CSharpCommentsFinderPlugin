using CSharpCommentsFinder.Model;
using CSharpCommentsFinder.View;
using CSharpCommentsFinder.ViewModel;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfTestApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var projectsCollection = MakeProjectsCollection();
            var vm = new MainViewModel(projectsCollection);
            var view = new MainView { DataContext = vm };
            mainGrid.Children.Add(view);
        }

        private IProjectsCollection MakeProjectsCollection()
        {
            var project1File1Comment1 = new Mock<IComment>();
            project1File1Comment1.SetupGet(x => x.Text).Returns("Text");
            project1File1Comment1.SetupGet(x => x.StartPosition).Returns(2);
            project1File1Comment1.SetupGet(x => x.EndPosition).Returns(10);

            var project1File1 = new Mock<IProjectFile>();
            project1File1.SetupGet(x => x.Name).Returns("File 1.cs");
            project1File1.SetupGet(x => x.FullPath).Returns(@"C:\Folder\File1.cs");
            project1File1.Setup(x => x.GetComments()).Returns(new[] { project1File1Comment1.Object });

            var project1 = new Mock<IProject>();
            project1.SetupGet(x => x.Name).Returns("Project 1");
            project1.SetupGet(x => x.AllFiles).Returns(new[] { project1File1.Object });

            var projectsCollection = new Mock<IProjectsCollection>();
            projectsCollection.SetupGet(x => x.Projects).Returns(new[] { project1.Object });

            return projectsCollection.Object;
        }
    }
}
