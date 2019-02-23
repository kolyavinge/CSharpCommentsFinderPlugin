using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpCommentsFinder.ViewModel
{
    public class ItemViewModel<T> : NotificationObject
    {
        public T Item { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                RaisePropertyChanged("IsSelected");
            }
        }

        public ItemViewModel(T item)
        {
            Item = item;
            IsSelected = false;
        }
    }
}
