using RFiDGear.Infrastructure.ReaderProviders;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RFiDGear.Infrastructure.Tasks
{
    /// <summary>
    /// Checks that a file exists in a DESFire application and which key numbers are assigned to it for
    /// read, write, read/write and change (task <see cref="TaskType_MifareDesfireTask.ValidateFile"/>).
    /// The key from the app key page is verified by authenticating with it (the key content cannot be read from a
    /// card); actually reading the file is covered by the read task. Uses only the public <see cref="ReaderDevice"/> API.
    /// </summary>
    public static class DesfireFileKeyValidator
    {
        public sealed class ValidationReport
        {
            public ERROR ErrorLevel { get; set; } = ERROR.Empty;
            public List<string> Lines { get; } = new List<string>();
        }

        /// <param name="device">Open reader device.</param>
        /// <param name="appId">Application containing the file (must be &gt; 0).</param>
        /// <param name="fileNo">File number (0..31).</param>
        /// <param name="keyHex">Key used to authenticate for listing files / reading file settings.</param>
        /// <param name="keyType">Type of <paramref name="keyHex"/>.</param>
        /// <param name="keyNumber">Key number of <paramref name="keyHex"/>.</param>
        /// <param name="expected">Expected key assignment (read, write, read/write, change).</param>
        public static async Task<ValidationReport> ValidateAsync(
            ReaderDevice device,
            int appId,
            int fileNo,
            string keyHex,
            DESFireKeyType keyType,
            int keyNumber,
            DESFireAccessRights expected)
        {
            var report = new ValidationReport();

            if (appId <= 0)
            {
                report.ErrorLevel = ERROR.IsNotTrue;
                report.Lines.Add("Invalid App ID: enter an application ID greater than 0.");
                return report;
            }

            if (fileNo < 0 || fileNo > 31)
            {
                report.ErrorLevel = ERROR.IsNotTrue;
                report.Lines.Add("Invalid file number: DESFire files are numbered 0 to 31.");
                return report;
            }

            try
            {
                // 1) Existence via file list (best effort: the list may be protected, or the app may not be selectable).
                var listing = await device.GetMifareDesfireFileList(keyHex, keyType, keyNumber, appId);

                if (listing == ERROR.NoError && device.FileIDList != null
                    && !Array.Exists(device.FileIDList, x => x == fileNo))
                {
                    report.ErrorLevel = ERROR.IsNotTrue;
                    report.Lines.Add(string.Format("File {0} does not exist in app {1:X}.", fileNo, appId));
                    return report;
                }

                // 2) Key assignment from the file settings.
                var result = await device.GetMifareDesfireFileSettings(keyHex, keyType, keyNumber, appId, fileNo);

                if (result != ERROR.NoError)
                {
                    report.ErrorLevel = result;
                    report.Lines.Add(string.Format(
                        "File settings of file {0} in app {1:X} could not be read ({2}): the file or app may not exist, or the key has no access.",
                        fileNo, appId, result));
                    return report;
                }

                var rights = device.DesfireFileSettings?.accessRights;

                if (rights == null || rights.Length < 2)
                {
                    report.ErrorLevel = ERROR.TransportError;
                    report.Lines.Add("The reader returned no access rights for the file.");
                    return report;
                }

                // Internal convention shared by all providers (see LibLogicalAccessProvider.ConvertWireAccessRights):
                // byte[0]: low nibble = Read, high nibble = Write; byte[1]: low nibble = Read/Write, high nibble = Change.
                var actual = new DESFireAccessRights
                {
                    readAccess = (TaskAccessRights)(rights[0] & 0x0F),
                    writeAccess = (TaskAccessRights)((rights[0] & 0xF0) >> 4),
                    readAndWriteAccess = (TaskAccessRights)(rights[1] & 0x0F),
                    changeAccess = (TaskAccessRights)((rights[1] & 0xF0) >> 4)
                };

                var differences = new List<string>();
                Compare("Read", expected.readAccess, actual.readAccess, differences);
                Compare("Write", expected.writeAccess, actual.writeAccess, differences);
                Compare("RW", expected.readAndWriteAccess, actual.readAndWriteAccess, differences);
                Compare("Change", expected.changeAccess, actual.changeAccess, differences);

                // 3) The key itself. Listing files and reading file settings silently fall back to an unauthenticated
                // request when the key is wrong, so they prove nothing about the key. Verify it explicitly: a DESFire
                // key cannot be read, the only way to compare its content is to authenticate with the expected value.
                var auth = await device.VerifyMifareDesfireKeyChange(keyHex, keyType, keyNumber, appId);
                var authenticated = auth == ERROR.NoError;

                if (differences.Count == 0)
                {
                    report.Lines.Add(string.Format(
                        "File {0} exists in app {1:X}; Read: {2}, Write: {3}, RW: {4}, Change: {5} as expected.",
                        fileNo, appId, actual.readAccess, actual.writeAccess, actual.readAndWriteAccess, actual.changeAccess));
                }
                else
                {
                    report.Lines.Add(string.Format("File {0} exists in app {1:X}, but the key assignment differs:", fileNo, appId));
                    report.Lines.AddRange(differences);
                }

                if (authenticated)
                {
                    var holds = new List<string>();
                    if ((int)actual.readAccess == keyNumber) holds.Add("Read");
                    if ((int)actual.writeAccess == keyNumber) holds.Add("Write");
                    if ((int)actual.readAndWriteAccess == keyNumber) holds.Add("RW");
                    if ((int)actual.changeAccess == keyNumber) holds.Add("Change");

                    report.Lines.Add(holds.Count > 0
                        ? string.Format("Key no. {0} authenticated and is assigned to this file for: {1}.", keyNumber, string.Join(", ", holds))
                        : string.Format("Key no. {0} authenticated; it is not assigned to this file for any access.", keyNumber));
                }
                else
                {
                    report.Lines.Add(string.Format("Key no. {0} did not authenticate ({1}): wrong key value, type or key number.", keyNumber, auth));
                }

                report.ErrorLevel = !authenticated
                    ? ERROR.AuthFailure
                    : differences.Count > 0 ? ERROR.IsNotTrue : ERROR.NoError;
            }
            catch (Exception ex)
            {
                report.ErrorLevel = ERROR.TransportError;
                report.Lines.Add("Check aborted: " + ex.Message);
            }

            return report;
        }

        private static void Compare(string name, TaskAccessRights expected, TaskAccessRights actual, List<string> differences)
        {
            if (expected != actual)
            {
                differences.Add(string.Format("{0}: expected {1}, actual {2}", name, expected, actual));
            }
        }
    }
}
