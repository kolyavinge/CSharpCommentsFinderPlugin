using CSharpCommentsFinder.Commands;
using CSharpCommentsFinder.Model;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace CSharpCommentsFinder.ViewModel
{
    public class MainViewModel : NotificationObject
    {
        private readonly IProjectsCollection _projects;

        private IEnumerable<ProjectViewModel> _projectsViewModel;
        public IEnumerable<ProjectViewModel> ProjectsViewModel
        {
            get { return _projectsViewModel; }
            set
            {
                _projectsViewModel = value;
                RaisePropertyChanged("ProjectsViewModel");
            }
        }

        private ObservableCollection<CommentViewModel> _commentsViewModel;
        public ObservableCollection<CommentViewModel> CommentsViewModel
        {
            get { return _commentsViewModel; }
            set
            {
                _commentsViewModel = value;
                RaisePropertyChanged("CommentsViewModel");
            }
        }

        public ICommand ReloadProjectsCommand { get { return new ActionCommand(ReloadProjects); } }

        public ICommand FindCommentsCommand { get { return new ActionCommand(FindComments); } }

        public ICommand NavigateToCommentCommand { get { return new ParametrizedCommand<IComment>(NavigateToComment); } }

        public MainViewModel(IProjectsCollection projects)
        {
            _projects = projects;
            CommentsViewModel = new ObservableCollection<CommentViewModel>();
            ReloadProjects();
        }

        private void ReloadProjects()
        {
            CommentsViewModel.Clear();
            ProjectsViewModel = _projects.Projects.Select(p => new ProjectViewModel(p)).OrderBy(x => x.Item.Name).ToList();
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

        private void NavigateToComment(IComment comment)
        {
            comment.ProjectFile.NavigateTo(comment);
        }
    }
}
