using RFiDGear.ViewModel;

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace RFiDGear
{
    /// <summary>
    /// Description of MainForm.
    /// </summary>
    ///
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.MaxHeight = (uint)SystemParameters.MaximizedPrimaryScreenHeight-8;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }

        private bool closeConfirmed;
        /// <summary>
        /// Handles the native window close (X button, Alt+F4, Windows shutdown). The
        /// CloseApplication command (Escape / File > Exit) already goes through
        /// Environment.Exit and its own confirmation, so this only needs to cover the
        /// paths that bypass that command. Tentatively cancels the close, asks the view
        /// model whether it's safe to proceed (prompting to save if there are unsaved
        /// changes), then closes for real if confirmed.
        /// </summary>
        private async void OnClosing(object sender, CancelEventArgs e)
        {
            if (closeConfirmed || !(DataContext is MainWindowViewModel viewModel))
            {
                return;
            }

            e.Cancel = true;

            if (await viewModel.ConfirmCloseWithUnsavedChangesAsync())
            {
                closeConfirmed = true;
                // Calling Close() directly here would re-enter Closing while WPF is still
                // processing this same event dispatch (we got here via an async
                // continuation of the very Closing event we're in) - that reentrant call
                // is silently ignored by WPF, so the window would stay open until a
                // second, fresh X click. Defer to a new dispatcher pass instead.
                Dispatcher.BeginInvoke(new Action(Close));
            }
        }

        private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            e.Column.Header = ((PropertyDescriptor)e.PropertyDescriptor).DisplayName;
        }
    }
}