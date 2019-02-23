using CSharpCommentsFinder.Commands;
using CSharpCommentsFinder.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CSharpCommentsFinder.ViewModel
{
    public class MainViewModel : NotificationObject
    {
        public IEnumerable<ProjectViewModel> ProjectsViewModel { get; set; }

        public ObservableCollection<CommentViewModel> CommentsViewModel { get; set; }

        public ICommand FindCommentsCommand { get { return new DelegateCommand(FindComments); } }

        public MainViewModel(IProjectsCollection projects)
        {
            ProjectsViewModel = projects.Projects.Select(p => new ProjectViewModel(p)).ToList();
        }

        private void FindComments()
        {
            CommentsViewModel.Clear();
            var selectedProjects = ProjectsViewModel.Where(p => p.IsSelected).Select(p => p.Item).ToList();
            foreach (var selectedProject in selectedProjects)
            {
                foreach (var projectFile in selectedProject.AllFiles)
                {
                    var comments = projectFile.GetComments().ToList();
                    var commentViewModels = comments.Select(c => new CommentViewModel(c)).ToList();
                    foreach (var commentViewModel in commentViewModels)
                    {
                        CommentsViewModel.Add(commentViewModel);
                    }
                }
            }
        }
    }
}
