using System;
using System.Windows.Input;

namespace CSharpCommentsFinder.Commands
{
    public class DelegateCommand : ICommand
    {
        private Action _action;

        public event EventHandler CanExecuteChanged;

        public DelegateCommand(Action action)
        {
            _action = action;
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            _action();
        }
    }
}
