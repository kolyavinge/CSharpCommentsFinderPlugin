using CSharpCommentsFinder.Commands;
using CSharpCommentsFinder.Model;
using CSharpCommentsFinder.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CSharpCommentsFinder.ViewModel
{
    public class MainViewModel : NotificationObject
    {
        private readonly IProjectsCollection _projects;
        private readonly ISolutionEvents _solutionEvents;

        private Cursor _cursor;
        public Cursor Cursor
        {
            get { return _cursor; }
            set
            {
                _cursor = value;
                RaisePropertyChanged("Cursor");
            }
        }

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

        public ICommand FindCommentsCommand { get { return new ActionCommand(FindCommentsAsync); } }

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

        private async void FindCommentsAsync()
        {
            try
            {
                Cursor = Cursors.Wait;
                CommentsViewModel = new List<CommentViewModel>();
                var selectedProjects = ProjectsViewModel.Where(p => p.IsSelected).Select(p => p.Item).ToList();
                var allSelectedProjectsFiles = selectedProjects.SelectMany(p => p.AllFiles).ToList();
                var newCommentsViewModel = await MakeCommentViewModelAsync(allSelectedProjectsFiles);
                newCommentsViewModel.Sort(new CommentViewModelComparer());
                CommentsViewModel = newCommentsViewModel;
            }
            catch (Exception e)
            {
                Logger.Info(e.ToString());
            }
            finally
            {
                Cursor = Cursors.Arrow;
            }
        }

        private async Task<List<CommentViewModel>> MakeCommentViewModelAsync(IEnumerable<IProjectFile> projectFiles)
        {
            Func<List<CommentViewModel>> func = () =>
            {
                var result = new List<CommentViewModel>();
                foreach (var projectFile in projectFiles)
                {
                    var comments = projectFile.GetComments().ToList();
                    var commentViewModels = comments.Select(c => new CommentViewModel(c));
                    result.AddRange(commentViewModels);
                }

                return result;
            };

            return await Task.Factory.StartNew(func);
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
