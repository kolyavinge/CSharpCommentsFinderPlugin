using System;

namespace CSharpCommentsFinder.Model
{
    public interface ISolutionEvents
    {
        event EventHandler SolutionOpened;

        event EventHandler SolutionClosing;

        event EventHandler ProjectAdded;

        event EventHandler ProjectRemoved;

        event EventHandler ProjectRenamed;
    }
}
