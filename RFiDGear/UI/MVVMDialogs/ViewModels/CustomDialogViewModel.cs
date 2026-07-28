using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;

namespace RFiDGear.UI.MVVMDialogs.ViewModels
{
    public class CustomDialogViewModel : ObservableObject, IUserDialogViewModel
    {
        #region IUserDialogViewModel Implementation

        public bool IsModal { get; private set; }

        public virtual void RequestClose()
        {
            if (OnCloseRequest != null)
            {
                OnCloseRequest(this);
            }
            else
            {
                Close();
            }
        }

        public event EventHandler DialogClosing;

        #endregion IUserDialogViewModel Implementation

        #region Commands

        public ICommand OkCommand => new RelayCommand(Ok);

        protected virtual void Ok()
        {
            if (OnOk != null)
            {
                OnOk(this);
            }
            else
            {
                Close();
            }
        }

        public ICommand CancelCommand => new RelayCommand(Cancel);

        protected virtual void Cancel()
        {
            if (OnCancel != null)
            {
                OnCancel(this);
            }
            else
            {
                Close();
            }
        }

        public ICommand PreviousCommand => new RelayCommand(Previous);

        protected virtual void Previous()
        {
            OnPrevious?.Invoke(this);
        }

        public ICommand NextCommand => new RelayCommand(Next);

        protected virtual void Next()
        {
            OnNext?.Invoke(this);
        }

        #endregion Commands

        private Window _ParentWindow = null;

        public Window ParentWindow
        {
            get => _ParentWindow;
            set => _ParentWindow = value;
        }

        private string _Message;

        public string Message
        {
            get => _Message;
            set { _Message = value; OnPropertyChanged(nameof(Message)); }
        }

        private string _Caption;

        public string Caption
        {
            get => _Caption;
            set { _Caption = value; OnPropertyChanged(nameof(Caption)); }
        }

        private bool _ShowNavigation;

        /// <summary>
        /// When true, shows "Zurück"/"Vor" buttons that let the caller step through a set of
        /// records (e.g. lines of a source file) while the dialog stays open, without closing it.
        /// The caller wires <see cref="OnPrevious"/>/<see cref="OnNext"/> to update
        /// <see cref="Message"/> (and its own tracked "current index") accordingly.
        /// </summary>
        public bool ShowNavigation
        {
            get => _ShowNavigation;
            set { _ShowNavigation = value; OnPropertyChanged(nameof(ShowNavigation)); }
        }

        private bool _IsPreviousEnabled = true;

        public bool IsPreviousEnabled
        {
            get => _IsPreviousEnabled;
            set { _IsPreviousEnabled = value; OnPropertyChanged(nameof(IsPreviousEnabled)); }
        }

        private bool _IsNextEnabled = true;

        public bool IsNextEnabled
        {
            get => _IsNextEnabled;
            set { _IsNextEnabled = value; OnPropertyChanged(nameof(IsNextEnabled)); }
        }

        public Action<CustomDialogViewModel> OnOk { get; set; }
        public Action<CustomDialogViewModel> OnCancel { get; set; }
        public Action<CustomDialogViewModel> OnCloseRequest { get; set; }
        public Action<CustomDialogViewModel> OnPrevious { get; set; }
        public Action<CustomDialogViewModel> OnNext { get; set; }

        public CustomDialogViewModel(bool isModal = true)
        {
            IsModal = isModal;
        }

        public void Close()
        {
            DialogClosing?.Invoke(this, new EventArgs());
        }

        public void Show(IList<IDialogViewModel> collection)
        {
            collection.Add(this);
        }
    }
}