using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CSharpCommentsFinder.Commands
{
    public class ParametrizedCommand<T> : ICommand
    {
        private readonly Action<T> _func;

        public event EventHandler CanExecuteChanged;

        public ParametrizedCommand(Action<T> func)
        {
            _func = func;
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            _func((T)parameter);
        }
    }
}
