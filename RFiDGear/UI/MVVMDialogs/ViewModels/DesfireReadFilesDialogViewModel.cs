using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using RFiDGear.ViewModel;

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
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

            SelectedKeyType = DESFireKeyType.DF_KEY_AES;
            AppKey = new string('0', GetExpectedKeyHexLength(SelectedKeyType));
            SelectedKeyNumber = "0";
            KeyNumbers = CustomConverter.GenerateStringSequence(0, 16).ToArray();

            RevalidateAppKey();

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
                appKey = NormalizeDesfireKeyInput(value, GetExpectedKeyHexLength(SelectedKeyType));
                OnPropertyChanged(nameof(AppKey));
                RevalidateAppKey();
            }
        }
        private string appKey;

        /// <summary>
        /// Null while <see cref="AppKey"/> is empty (not shown as an error), true/false once it has
        /// been checked for hex format and the exact byte length <see cref="SelectedKeyType"/> requires
        /// (DES: 8 bytes/16 hex chars, 3K3DES: 24 bytes/48 hex chars, AES: 16 bytes/32 hex chars).
        /// </summary>
        public bool? IsValidAppKey
        {
            get => isValidAppKey;
            private set
            {
                isValidAppKey = value;
                OnPropertyChanged(nameof(IsValidAppKey));
            }
        }
        private bool? isValidAppKey;

        private void RevalidateAppKey()
        {
            if (string.IsNullOrEmpty(AppKey))
            {
                IsValidAppKey = null;
                return;
            }

            IsValidAppKey = CustomConverter.IsInHexFormat(AppKey) && AppKey.Length == GetExpectedKeyHexLength(SelectedKeyType);
        }

        /// <summary>
        /// Strips anything that isn't a hex digit (including whitespace, e.g. pasted "00 11 22..."),
        /// uppercases, and truncates to <paramref name="maxLength"/> - same normalization used
        /// throughout MifareDesfireSetupViewModel's key fields.
        /// </summary>
        private static string NormalizeDesfireKeyInput(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                if (Uri.IsHexDigit(character))
                {
                    builder.Append(char.ToUpperInvariant(character));
                }
            }

            var normalized = builder.ToString();
            return normalized.Length > maxLength ? normalized.Substring(0, maxLength) : normalized;
        }

        private static int GetExpectedKeyHexLength(DESFireKeyType keyType)
        {
            switch (keyType)
            {
                case DESFireKeyType.DF_KEY_DES:
                    return 16;

                case DESFireKeyType.DF_KEY_3K3DES:
                    return 48;

                case DESFireKeyType.DF_KEY_AES:
                default:
                    return 32;
            }
        }

        public DESFireKeyType SelectedKeyType
        {
            get => selectedKeyType;
            set
            {
                var previousExpectedLength = GetExpectedKeyHexLength(selectedKeyType);
                selectedKeyType = value;
                OnPropertyChanged(nameof(SelectedKeyType));

                // If the key field still holds nothing but the all-zero default (i.e. the user hasn't
                // typed a real key yet), re-pad it to match the new type's length - so switching e.g.
                // 3DES -> AES doesn't just leave a now-wrong-length string of zeros behind.
                if (!string.IsNullOrEmpty(AppKey) && AppKey.Length == previousExpectedLength && AppKey.All(c => c == '0'))
                {
                    AppKey = new string('0', GetExpectedKeyHexLength(value));
                }

                RevalidateAppKey();
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

                    var newChildren = new ObservableCollection<RFiDChipGrandChildLayerViewModel>();

                    // GetMifareDesfireFileList authenticates internally with the key/type/number
                    // passed here - no separate pre-authentication call needed (and none should be
                    // added: on EV2/EV3 cards, authenticating twice in the same connection can
                    // itself cause the second attempt to fail). This runs BEFORE the app-settings
                    // read below on purpose: it resets the reader connection when its own
                    // authenticate attempt fails, which also clears out any leftover session state
                    // from whatever happened before this dialog was opened (e.g. browsing other
                    // apps in the tree) - GetMifareDesfireAppSettings has no such reset of its own,
                    // so running it first would risk failing on a connection someone else left dirty.
                    var listResult = await device.GetMifareDesfireFileList(AppKey, SelectedKeyType, keyNumber, appId);

                    // App-level key settings (GetKeySettings) don't require authentication per the
                    // DESFire spec, so this first attempt matches how the normal (free) quick check
                    // reads it, and works for any app regardless of whether the supplied key is
                    // correct.
                    var appSettingsResult = await device.GetMifareDesfireAppSettings(AppKey, SelectedKeyType, keyNumber, appId, authenticateBeforeReading: false);

                    // Fallback: if the free read failed - e.g. this app's settings block access
                    // without the master key even at this level, or something about this specific
                    // card requires it - retry authenticated, using the key that was just proven
                    // correct by the (successful or attempted) file list read above.
                    if (appSettingsResult.Code != ERROR.NoError)
                    {
                        appSettingsResult = await device.GetMifareDesfireAppSettings(AppKey, SelectedKeyType, keyNumber, appId, authenticateBeforeReading: true);
                    }

                    if (appSettingsResult.Code == ERROR.NoError)
                    {
                        newChildren.Add(new RFiDChipGrandChildLayerViewModel(string.Format("Available Keys: {0}", device.MaxNumberOfAppKeys)));
                        newChildren.Add(new RFiDChipGrandChildLayerViewModel(string.Format("App Encryption Type: {0}", Enum.GetName(typeof(DESFireKeyType), device.EncryptionType))));
                        newChildren.Add(new RFiDChipGrandChildLayerViewModel(string.Format("Key Settings: {0} (0x{1:X2})", Enum.GetName(typeof(DESFireKeySettings), device.DesfireAppKeySetting & (DESFireKeySettings)0xF0), (byte)device.DesfireAppKeySetting)));
                        newChildren.Add(new RFiDChipGrandChildLayerViewModel(string.Format("Allow Change AMK: {0}", (device.DesfireAppKeySetting & (DESFireKeySettings)0x01) == (DESFireKeySettings)0x01 ? "yes" : "no")));
                        newChildren.Add(new RFiDChipGrandChildLayerViewModel(string.Format("Allow Listing without AMK: {0}", (device.DesfireAppKeySetting & (DESFireKeySettings)0x02) == (DESFireKeySettings)0x02 ? "yes" : "no")));
                        newChildren.Add(new RFiDChipGrandChildLayerViewModel(string.Format("Allow Create/Delete without AMK: {0}", (device.DesfireAppKeySetting & (DESFireKeySettings)0x04) == (DESFireKeySettings)0x04 ? "yes" : "no")));
                        newChildren.Add(new RFiDChipGrandChildLayerViewModel(string.Format("Allow Change Config: {0}", (device.DesfireAppKeySetting & (DESFireKeySettings)0x08) == (DESFireKeySettings)0x08 ? "yes" : "no")));
                    }
                    else
                    {
                        // Surface WHY app settings could not be read, instead of silently omitting them -
                        // OperationResult carries more detail than the ERROR enum alone.
                        var appSettingsDetail = string.Format(
                            "{0}{1}{2}",
                            appSettingsResult.Code,
                            string.IsNullOrEmpty(appSettingsResult.Message) ? string.Empty : " - " + appSettingsResult.Message,
                            string.IsNullOrEmpty(appSettingsResult.Details) ? string.Empty : " (" + appSettingsResult.Details + ")");
                        newChildren.Add(new RFiDChipGrandChildLayerViewModel(string.Format("App Settings: could not be read - {0}", appSettingsDetail)));
                    }

                    if (listResult != ERROR.NoError)
                    {
                        var detail = string.IsNullOrEmpty(device.LastNativeErrorMessage)
                            ? listResult.ToString()
                            : string.Format("{0} ({1})", listResult, device.LastNativeErrorMessage);

                        // Still commit whatever app-level settings were read above, even though the
                        // file list itself failed (e.g. wrong key) - better than losing that info too.
                        appNode.Children.Clear();
                        foreach (var node in newChildren)
                        {
                            appNode.Children.Add(node);
                        }

                        StatusText = string.Format(ResourceLoader.GetResource("statusDesfireReadFilesFailed"), detail);
                        return;
                    }

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

                        newChildren.Add(fileNode);
                    }

                    appNode.Children.Clear();
                    foreach (var node in newChildren)
                    {
                        appNode.Children.Add(node);
                    }

                    StatusText = string.Format(ResourceLoader.GetResource("statusDesfireReadFilesSuccess"), device.FileIDList.Length);
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
