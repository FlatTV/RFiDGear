using CommunityToolkit.Mvvm.Input;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Infrastructure.Tasks;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace RFiDGear.ViewModel.TaskSetupViewModels
{
    /// <summary>
    /// Execution of <see cref="TaskType_MifareDesfireTask.ValidateFile"/> ("Check: File and Keys").
    /// Uses App ID, file number and the four access-right selections of the file page as the EXPECTED key
    /// assignment, and the key of the app key page to authenticate. Lives in a partial class file so the
    /// large upstream-owned view model file only needs a few one-line hooks.
    /// </summary>
    public partial class MifareDesfireSetupViewModel
    {
        /// <summary>True while the "check file" button of the file page should be shown.</summary>
        [XmlIgnore]
        public bool ShowValidateFileButton => SelectedTaskType == TaskType_MifareDesfireTask.ValidateFile;

        /// <summary>Button on the file page: runs the check with reader status feedback.</summary>
        public IAsyncRelayCommand ValidateFileRelayCommand => new AsyncRelayCommand(async () =>
        {
            await UpdateReaderStatusCommand.ExecuteAsync(true);
            await ValidateFileCommand();
            await UpdateReaderStatusCommand.ExecuteAsync(false);
        });

        /// <summary>
        /// Checks that the file exists and that read/write/read-write/change are assigned to the expected key numbers.
        /// Used by the task dispatcher and by the button of the file page.
        /// </summary>
        public async Task<ERROR> ValidateFileCommand()
        {
            CurrentTaskErrorLevel = ERROR.Empty;

            using (var device = ReaderDevice.Instance)
            {
                if (device != null)
                {
                    StatusText = string.Format("{0}: {1}\n", DateTime.Now, ResourceLoader.GetResource("textBoxStatusTextBoxDllLoaded"));

                    if (IsValidAppNumberCurrent != false
                        && IsValidFileNumberCurrent != false
                        && IsValidDesfireKeyValue(DesfireAppKeyCurrent, SelectedDesfireAppKeyEncryptionTypeCurrent))
                    {
                        var expected = new DESFireAccessRights
                        {
                            readAccess = SelectedDesfireFileAccessRightRead,
                            writeAccess = SelectedDesfireFileAccessRightWrite,
                            readAndWriteAccess = SelectedDesfireFileAccessRightReadWrite,
                            changeAccess = SelectedDesfireFileAccessRightChange
                        };

                        var report = await DesfireFileKeyValidator.ValidateAsync(
                            device,
                            AppNumberCurrentAsInt,
                            FileNumberCurrentAsInt,
                            DesfireAppKeyCurrent,
                            SelectedDesfireAppKeyEncryptionTypeCurrent,
                            selectedDesfireAppKeyNumberCurrentAsInt,
                            expected);

                        foreach (var line in report.Lines)
                        {
                            StatusText += string.Format("{0}: {1}\n", DateTime.Now, line);
                        }

                        CurrentTaskErrorLevel = report.ErrorLevel;
                    }
                    else
                    {
                        StatusText += string.Format("{0}: Invalid App ID, file number or key (length/format does not match the key type).\n", DateTime.Now);
                        CurrentTaskErrorLevel = ERROR.IsNotTrue;
                    }
                }
                else
                {
                    CurrentTaskErrorLevel = ERROR.TransportError;
                }
            }

            await FinalizeTaskAsync();
            return CurrentTaskErrorLevel;
        }
    }
}
