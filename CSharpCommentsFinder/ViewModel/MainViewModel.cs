using CSharpCommentsFinder.Commands;
using CSharpCommentsFinder.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace CSharpCommentsFinder.ViewModel
{
    public class MainViewModel : NotificationObject
    {
        private readonly IProjectsCollection _projects;
        private readonly ISolutionEvents _solutionEvents;

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

        private IEnumerable<CommentViewModel> _commentsViewModel;
        public IEnumerable<CommentViewModel> CommentsViewModel
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

        public MainViewModel(IProjectsCollection projects, ISolutionEvents solutionEvents)
        {
            _projects = projects;
            _solutionEvents = solutionEvents;
            CommentsViewModel = new List<CommentViewModel>();
            ReloadProjects();
            _solutionEvents.SolutionOpened += OnAnySolutionEvent;
            _solutionEvents.SolutionClosing += OnAnySolutionEvent;
            _solutionEvents.ProjectAdded += OnAnySolutionEvent;
            _solutionEvents.ProjectRemoved += OnAnySolutionEvent;
            _solutionEvents.ProjectRenamed += OnAnySolutionEvent;
        }

        private void ReloadProjects()
        {
            CommentsViewModel = new List<CommentViewModel>();
            ProjectsViewModel = _projects.Projects.Select(p => new ProjectViewModel(p)).OrderBy(x => x.Item.Name).ToList();
        }

        private void FindComments()
        {
            CommentsViewModel = new List<CommentViewModel>();
            var newCommentsViewModel = new List<CommentViewModel>();
            var selectedProjects = ProjectsViewModel.Where(p => p.IsSelected).Select(p => p.Item).ToList();
            foreach (var selectedProject in selectedProjects)
            {
                foreach (var projectFile in selectedProject.AllFiles)
                {
                    var comments = projectFile.GetComments().ToList();
                    var commentViewModels = comments.Select(c => new CommentViewModel(c)).ToList();
                    newCommentsViewModel.AddRange(commentViewModels);
                }
            }
            newCommentsViewModel.Sort(new CommentViewModelComparer());
            CommentsViewModel = newCommentsViewModel;
        }

        private void NavigateToComment(IComment comment)
        {
            comment.ProjectFile.NavigateTo(comment);
        }

        private void OnAnySolutionEvent(object sender, EventArgs e)
        {
            ProjectsViewModel = new List<ProjectViewModel>();
            CommentsViewModel = new List<CommentViewModel>();
        }
    }
}
