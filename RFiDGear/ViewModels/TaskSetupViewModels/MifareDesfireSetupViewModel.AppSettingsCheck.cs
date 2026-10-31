namespace RFiDGear.ViewModel.TaskSetupViewModels
{
    /// <summary>
    /// Comparison logic of <see cref="Infrastructure.Tasks.TaskType_MifareDesfireTask.ReadAppSettings"/>
    /// ("Check: App Config"), kept in a partial class file so the upstream-owned view model file only needs
    /// a one-line hook.
    /// </summary>
    public partial class MifareDesfireSetupViewModel
    {
        private bool isAppSettingsCheckChangeKeyCondition;

        /// <summary>
        /// When <see langword="true"/>, the change-key condition (upper nibble of the key-settings byte:
        /// master key, targeted key number or frozen) is compared too, using the selection of the
        /// change-key combo box. Off by default, so existing tasks keep checking only the four setting bits.
        /// </summary>
        public bool IsAppSettingsCheckChangeKeyCondition
        {
            get => isAppSettingsCheckChangeKeyCondition;
            set
            {
                isAppSettingsCheckChangeKeyCondition = value;
                OnPropertyChanged(nameof(IsAppSettingsCheckChangeKeyCondition));
            }
        }

        /// <summary>
        /// Compares the key-settings byte read from the card with the expected one.
        /// The four setting bits (lower nibble) follow the exact / "at least" mode. The change-key condition
        /// (upper nibble) is an enumerated value, not a set of flags, so it is always compared for equality,
        /// and only when <see cref="IsAppSettingsCheckChangeKeyCondition"/> is set.
        /// </summary>
        private bool CompareAppKeySettings(byte actual, byte expected)
        {
            const byte settingBits = 0x0F;
            const byte changeKeyCondition = 0xF0;

            var settingsOk = IsAppSettingsExactMatch
                ? (actual & settingBits) == (expected & settingBits)
                : (actual & expected & settingBits) == (expected & settingBits);

            var changeKeyOk = !IsAppSettingsCheckChangeKeyCondition
                              || (actual & changeKeyCondition) == (expected & changeKeyCondition);

            return settingsOk && changeKeyOk;
        }
    }
}
