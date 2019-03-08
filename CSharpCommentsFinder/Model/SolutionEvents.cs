using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;

namespace CSharpCommentsFinder.Model
{
    public class SolutionEvents : ISolutionEvents
    {
        public SolutionEvents(DTE dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var events = (Events2)dte.Events;
            events.SolutionEvents.Opened += OnSolutionOpened;
            events.SolutionEvents.AfterClosing += OnSolutionClosing;
            events.SolutionEvents.ProjectAdded += OnProjectAdded;
            events.SolutionEvents.ProjectRemoved += OnProjectRemoved;
            events.SolutionEvents.ProjectRenamed += OnProjectRenamed;
        }

        private void OnSolutionOpened()
        {
            if (SolutionOpened != null) SolutionOpened(this, EventArgs.Empty);
        }

        private void OnSolutionClosing()
        {
            if (SolutionClosing != null) SolutionClosing(this, EventArgs.Empty);
        }

        private void OnProjectAdded(EnvDTE.Project Project)
        {
            if (ProjectAdded != null) ProjectAdded(this, EventArgs.Empty);
        }

        private void OnProjectRemoved(EnvDTE.Project Project)
        {
            if (ProjectRemoved != null) ProjectRemoved(this, EventArgs.Empty);
        }

        private void OnProjectRenamed(EnvDTE.Project Project, string OldName)
        {
            if (ProjectRenamed != null) ProjectRenamed(this, EventArgs.Empty);
        }

        public event EventHandler SolutionOpened;

        public event EventHandler SolutionClosing;

        public event EventHandler ProjectAdded;

        public event EventHandler ProjectRemoved;

        public event EventHandler ProjectRenamed;
    }
}
