using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace RFiDGear.ViewModel
{
    /// <summary>
    /// Diagnostic dialog: sends raw command bytes to the card (e.g. native DESFire commands without
    /// authentication) and shows the unprocessed response with a decoded status word.
    /// </summary>
    public class RawApduDialogViewModel : ObservableObject, IUserDialogViewModel
    {
        private readonly StringBuilder log = new StringBuilder();

        /// <summary>
        /// Gets or sets the command bytes as hex text, e.g. <c>5A F2 6D E5</c>.
        /// </summary>
        public string CommandHex
        {
            get => commandHex;
            set
            {
                commandHex = value;
                OnPropertyChanged(nameof(CommandHex));
            }
        }
        private string commandHex = string.Empty;

        /// <summary>
        /// Gets or sets how the entered bytes are framed on the wire.
        /// </summary>
        public RawApduFraming SelectedFraming
        {
            get => selectedFraming;
            set
            {
                selectedFraming = value;
                OnPropertyChanged(nameof(SelectedFraming));
            }
        }
        private RawApduFraming selectedFraming = RawApduFraming.NativeDesfire;

        /// <summary>
        /// Gets or sets whether commands that change or destroy card content may be sent.
        /// </summary>
        public bool AllowDestructiveCommands
        {
            get => allowDestructiveCommands;
            set
            {
                allowDestructiveCommands = value;
                OnPropertyChanged(nameof(AllowDestructiveCommands));
            }
        }
        private bool allowDestructiveCommands;

        /// <summary>
        /// Gets the accumulated send/receive log.
        /// </summary>
        public string ResponseLog
        {
            get => responseLog;
            private set
            {
                responseLog = value;
                OnPropertyChanged(nameof(ResponseLog));
            }
        }
        private string responseLog = string.Empty;

        /// <summary>
        /// Sends the entered command to the card.
        /// </summary>
        public IAsyncRelayCommand SendCommand => new AsyncRelayCommand(OnSendAsync);

        /// <summary>
        /// Clears the log.
        /// </summary>
        public ICommand ClearLogCommand => new RelayCommand(() =>
        {
            log.Clear();
            ResponseLog = string.Empty;
        });

        /// <summary>
        /// Closes the dialog.
        /// </summary>
        public ICommand CloseCommand => new RelayCommand(() => RequestClose());

        /// <summary>
        /// Overridable for tests: the reader to use. Defaults to the configured reader instance.
        /// </summary>
        protected virtual ReaderDevice GetReader() => ReaderDevice.Instance;

        private async Task OnSendAsync()
        {
            if (!RawApduHelper.TryParseHex(CommandHex, out var userBytes))
            {
                AppendLog("Invalid hex input (expected e.g. \"5A F2 6D E5\").");
                return;
            }

            byte[] outgoing;
            try
            {
                outgoing = RawApduHelper.BuildOutgoing(userBytes, SelectedFraming);
            }
            catch (ArgumentException e)
            {
                AppendLog(e.Message);
                return;
            }

            if (!AllowDestructiveCommands && RawApduHelper.IsPotentiallyDestructive(userBytes, SelectedFraming))
            {
                AppendLog("Blocked: this command changes or destroys card content. Tick the confirmation checkbox to send it.");
                return;
            }

            var device = GetReader();
            if (device == null || !device.SupportsRawTransceive)
            {
                AppendLog("Raw commands are only supported with the PC/SC (LibLogicalAccess) reader.");
                return;
            }

            AppendLog("> " + RawApduHelper.ToHex(outgoing));

            var (result, response) = await device.TransceiveRawAsync(outgoing);

            if (result != ERROR.NoError)
            {
                var detail = string.IsNullOrWhiteSpace(device.LastNativeErrorMessage)
                    ? string.Empty
                    : " - " + FirstLine(device.LastNativeErrorMessage);
                AppendLog("< " + result + detail);
                return;
            }

            AppendLog("< " + RawApduHelper.DescribeResponse(response));
        }

        private static string FirstLine(string text)
        {
            var index = text.IndexOfAny(new[] { '\r', '\n' });
            return index < 0 ? text : text.Substring(0, index);
        }

        private void AppendLog(string line)
        {
            log.Append(DateTime.Now.ToString("HH:mm:ss")).Append("  ").AppendLine(line);
            ResponseLog = log.ToString();
        }

        #region IUserDialogViewModel Implementation

        public Action<RawApduDialogViewModel> OnCloseRequest { get; set; }

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

        public void Close()
        {
            DialogClosing?.Invoke(this, new EventArgs());
        }

        public void Show(IList<IDialogViewModel> collection)
        {
            collection.Add(this);
        }

        #endregion IUserDialogViewModel Implementation

        #region Localization

        /// <summary>
        /// Act as a proxy between ResourceLoader and view directly.
        /// </summary>
        public string LocalizationResourceSet { get; set; }

        public string Caption
        {
            get => caption;
            set
            {
                caption = value;
                OnPropertyChanged(nameof(Caption));
            }
        }
        private string caption;

        #endregion Localization
    }
}
