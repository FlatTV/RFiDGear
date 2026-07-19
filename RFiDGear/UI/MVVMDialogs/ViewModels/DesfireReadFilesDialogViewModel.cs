using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using RFiDGear.ViewModel;

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace RFiDGear.UI.MVVMDialogs.ViewModels
{
    /// <summary>
    /// Small standalone dialog (not a task): authenticates to a single DESFire application with
    /// a user-supplied key and reads its file list + file settings, then writes the result
    /// straight into that application's node in the quick-check tree.
    ///
    /// This exists because <c>GetKeySettings</c> (the application-level info shown for every app
    /// regardless of key) is always readable per the DESFire spec, but the file list of an
    /// application is only readable without authentication when that application has "Free
    /// Directory Listing without MK" enabled. For applications where it isn't, the quick check
    /// has no way to show files without knowing that application's key - hence this dialog.
    /// </summary>
    public class DesfireReadFilesDialogViewModel : ObservableObject, IUserDialogViewModel
    {
        private readonly RFiDChipChildLayerViewModel appNode;

        public DesfireReadFilesDialogViewModel(RFiDChipChildLayerViewModel _appNode, bool isModal = true)
        {
            IsModal = isModal;
            appNode = _appNode;

            AppKey = new string('0', 32);
            SelectedKeyType = DESFireKeyType.DF_KEY_DES;
            SelectedKeyNumber = "0";
            KeyNumbers = CustomConverter.GenerateStringSequence(0, 16).ToArray();

            Caption = string.Format(
                "{0} - AppID {1} (0x{2})",
                ResourceLoader.GetResource("dialogCaptionDesfireReadFilesWithKey"),
                appNode?.AppID,
                appNode?.AppID?.ToString("X8"));
        }

        #region IUserDialogViewModel Implementation

        public bool IsModal { get; private set; }

        public event EventHandler DialogClosing;

        public void RequestClose()
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

        public void Close()
        {
            DialogClosing?.Invoke(this, new EventArgs());
        }

        #endregion IUserDialogViewModel Implementation

        /// <summary>
        /// Set by the caller (mirrors the OnCloseRequest pattern used by the other dialog view
        /// models in this app) so the host can decide how to actually close the window.
        /// </summary>
        public Action<DesfireReadFilesDialogViewModel> OnCloseRequest { get; set; }

        public ICommand CloseCommand => new RelayCommand(RequestClose);

        public string Caption { get; }

        /// <summary>
        /// Dummy trigger property for the app's <c>Localization</c> XAML converter, which reads its
        /// resource key from <c>ConverterParameter</c> and ignores the bound value itself - this just
        /// needs to exist and be bindable, matching the same convention used throughout the rest of
        /// the app (see e.g. MifareDesfireSetupViewModel.LocalizationResourceSet).
        /// </summary>
        public string LocalizationResourceSet { get; set; }

        /// <summary>Key numbers 0-15, for the key-number combo box.</summary>
        public string[] KeyNumbers { get; }

        public string AppKey
        {
            get => appKey;
            set
            {
                appKey = value;
                OnPropertyChanged(nameof(AppKey));
            }
        }
        private string appKey;

        public DESFireKeyType SelectedKeyType
        {
            get => selectedKeyType;
            set
            {
                selectedKeyType = value;
                OnPropertyChanged(nameof(SelectedKeyType));
            }
        }
        private DESFireKeyType selectedKeyType;

        public string SelectedKeyNumber
        {
            get => selectedKeyNumber;
            set
            {
                selectedKeyNumber = value;
                OnPropertyChanged(nameof(SelectedKeyNumber));
            }
        }
        private string selectedKeyNumber;

        public string StatusText
        {
            get => statusText;
            private set
            {
                statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }
        private string statusText = string.Empty;

        public bool IsBusy
        {
            get => isBusy;
            private set
            {
                isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }
        private bool isBusy;

        /// <summary>Convenience property so XAML can bind IsEnabled without needing a bool-inverter converter.</summary>
        public bool IsNotBusy => !IsBusy;

        public IAsyncRelayCommand ReadFilesCommand => new AsyncRelayCommand(ReadFilesAsync);
        private async Task ReadFilesAsync()
        {
            if (appNode?.AppID == null)
            {
                StatusText = ResourceLoader.GetResource("statusDesfireReadFilesNoApp");
                return;
            }

            IsBusy = true;
            StatusText = ResourceLoader.GetResource("statusDesfireReadFilesReading");

            try
            {
                using (var device = ReaderDevice.Instance)
                {
                    if (device == null)
                    {
                        StatusText = ResourceLoader.GetResource("statusDesfireReadFilesNoReader");
                        return;
                    }

                    var appId = (int)appNode.AppID.Value;
                    var keyNumber = int.TryParse(SelectedKeyNumber, out var parsedKeyNumber) ? parsedKeyNumber : 0;

                    // GetMifareDesfireFileList authenticates internally with the key/type/number
                    // passed here - no separate pre-authentication call needed (and none should be
                    // added: on EV2/EV3 cards, authenticating twice in the same connection can
                    // itself cause the second attempt to fail).
                    var listResult = await device.GetMifareDesfireFileList(AppKey, SelectedKeyType, keyNumber, appId);

                    if (listResult != ERROR.NoError)
                    {
                        var detail = string.IsNullOrEmpty(device.LastNativeErrorMessage)
                            ? listResult.ToString()
                            : string.Format("{0} ({1})", listResult, device.LastNativeErrorMessage);
                        StatusText = string.Format(ResourceLoader.GetResource("statusDesfireReadFilesFailed"), detail);
                        return;
                    }

                    var newFileNodes = new ObservableCollection<RFiDChipGrandChildLayerViewModel>();

                    foreach (var fileID in device.FileIDList)
                    {
                        var fileNode = new RFiDChipGrandChildLayerViewModel(new MifareDesfireFileModel(null, fileID), appNode);

                        if (await device.GetMifareDesfireFileSettings(AppKey, SelectedKeyType, keyNumber, appId, fileID) == ERROR.NoError)
                        {
                            // Mirrors the detail rows built for the free (unauthenticated) quick-check
                            // path, see RFiDChipParentLayerViewModel.
                            fileNode.Children.Add(new RFiDChipGrandGrandChildLayerViewModel(string.Format("FileType: {0}", Enum.GetName(typeof(FileType_MifareDesfireFileType), device.DesfireFileSettings.FileType)), fileNode));
                            fileNode.Children.Add(new RFiDChipGrandGrandChildLayerViewModel(string.Format("FileSize: {0}Bytes", device.DesfireFileSettings.dataFile.fileSize.ToString(CultureInfo.CurrentCulture)), fileNode));
                            fileNode.Children.Add(new RFiDChipGrandGrandChildLayerViewModel(string.Format("CommMode: {0}", Enum.GetName(typeof(EncryptionMode), device.DesfireFileSettings.comSett)), fileNode));
                            fileNode.Children.Add(new RFiDChipGrandGrandChildLayerViewModel(string.Format("Read: {0}", Enum.GetName(typeof(TaskAccessRights), (device.DesfireFileSettings.accessRights[1] & 0xF0) >> 4)), fileNode));
                            fileNode.Children.Add(new RFiDChipGrandGrandChildLayerViewModel(string.Format("Write: {0}", Enum.GetName(typeof(TaskAccessRights), device.DesfireFileSettings.accessRights[1] & 0x0F)), fileNode));
                            fileNode.Children.Add(new RFiDChipGrandGrandChildLayerViewModel(string.Format("RW: {0}", Enum.GetName(typeof(TaskAccessRights), (device.DesfireFileSettings.accessRights[0] & 0xF0) >> 4)), fileNode));
                            fileNode.Children.Add(new RFiDChipGrandGrandChildLayerViewModel(string.Format("Change: {0}", Enum.GetName(typeof(TaskAccessRights), device.DesfireFileSettings.accessRights[0] & 0x0F)), fileNode));
                        }

                        newFileNodes.Add(fileNode);
                    }

                    appNode.Children.Clear();
                    foreach (var node in newFileNodes)
                    {
                        appNode.Children.Add(node);
                    }

                    StatusText = string.Format(ResourceLoader.GetResource("statusDesfireReadFilesSuccess"), newFileNodes.Count);
                }
            }
            catch (Exception ex)
            {
                StatusText = string.Format(ResourceLoader.GetResource("statusDesfireReadFilesFailed"), ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
