using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace RFiDGear.Infrastructure
{
    /// <summary>
    /// How the bytes entered in the raw command dialog are put on the wire.
    /// </summary>
    public enum RawApduFraming
    {
        /// <summary>
        /// Native DESFire command (<c>INS [data...]</c>), wrapped as ISO 7816-4 APDU
        /// <c>90 INS 00 00 [Lc data...] 00</c>. The response ends with <c>91 SW2</c>.
        /// </summary>
        NativeDesfire,

        /// <summary>
        /// The bytes are sent exactly as entered (full APDU incl. CLA).
        /// </summary>
        ApduAsEntered
    }

    /// <summary>
    /// Pure helper functions for the raw command dialog: hex parsing/formatting, framing and
    /// decoding of DESFire / ISO 7816 status words. Free of any reader dependency.
    /// </summary>
    public static class RawApduHelper
    {
        /// <summary>
        /// Native DESFire command codes that modify or destroy card content or configuration.
        /// </summary>
        private static readonly HashSet<byte> DestructiveNativeCommands = new HashSet<byte>
        {
            0xFC, // Format PICC
            0xDA, // Delete Application
            0xDF, // Delete File
            0xC4, // Change Key
            0x54, // Change Key Settings
            0x5C, // Set Configuration
            0x5F, // Change File Settings
            0xCA, // Create Application
            0xCD, // Create Std Data File
            0xCB, // Create Backup Data File
            0xCC, // Create Value File
            0xC1, // Create Linear Record File
            0xC0  // Create Cyclic Record File
        };

        private static readonly Dictionary<byte, string> DesfireStatus = new Dictionary<byte, string>
        {
            { 0x00, "OPERATION_OK" },
            { 0x0C, "NO_CHANGES" },
            { 0x0E, "OUT_OF_EEPROM_ERROR" },
            { 0x1C, "ILLEGAL_COMMAND_CODE" },
            { 0x1E, "INTEGRITY_ERROR" },
            { 0x40, "NO_SUCH_KEY" },
            { 0x7E, "LENGTH_ERROR" },
            { 0x9D, "PERMISSION_DENIED" },
            { 0x9E, "PARAMETER_ERROR" },
            { 0xA0, "APPLICATION_NOT_FOUND" },
            { 0xA1, "APPL_INTEGRITY_ERROR" },
            { 0xAE, "AUTHENTICATION_ERROR" },
            { 0xAF, "ADDITIONAL_FRAME" },
            { 0xBE, "BOUNDARY_ERROR" },
            { 0xC1, "PICC_INTEGRITY_ERROR" },
            { 0xCA, "COMMAND_ABORTED" },
            { 0xCD, "PICC_DISABLED_ERROR" },
            { 0xCE, "COUNT_ERROR" },
            { 0xDE, "DUPLICATE_ERROR" },
            { 0xEE, "EEPROM_ERROR" },
            { 0xF0, "FILE_NOT_FOUND" },
            { 0xF1, "FILE_INTEGRITY_ERROR" }
        };

        /// <summary>
        /// Parses a hex string. Accepts spaces, commas, dashes, colons and an optional <c>0x</c> prefix per byte.
        /// </summary>
        public static bool TryParseHex(string input, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var cleaned = new StringBuilder(input.Length);
            foreach (var token in input.Split(new[] { ' ', '\t', '\r', '\n', ',', ';', '-', ':' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? token.Substring(2) : token;
                cleaned.Append(t);
            }

            var hex = cleaned.ToString();
            if (hex.Length == 0 || hex.Length % 2 != 0)
            {
                return false;
            }

            var result = new byte[hex.Length / 2];
            for (var i = 0; i < result.Length; i++)
            {
                if (!byte.TryParse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result[i]))
                {
                    return false;
                }
            }

            bytes = result;
            return true;
        }

        /// <summary>
        /// Formats bytes as upper-case hex separated by single spaces.
        /// </summary>
        public static string ToHex(byte[] bytes)
            => bytes == null || bytes.Length == 0 ? string.Empty : string.Join(" ", bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));

        /// <summary>
        /// Wraps a native DESFire command (<c>INS [data]</c>) into an ISO 7816-4 APDU:
        /// <c>90 INS 00 00 [Lc data] 00</c>.
        /// </summary>
        public static byte[] WrapNativeDesfire(byte[] native)
        {
            if (native == null || native.Length == 0)
            {
                throw new ArgumentException("A native DESFire command needs at least the command code.", nameof(native));
            }

            var data = native.Skip(1).ToArray();
            if (data.Length > 255)
            {
                throw new ArgumentException("Native DESFire command data is limited to 255 bytes.", nameof(native));
            }

            var apdu = new List<byte> { 0x90, native[0], 0x00, 0x00 };
            if (data.Length > 0)
            {
                apdu.Add((byte)data.Length);
                apdu.AddRange(data);
            }

            apdu.Add(0x00);
            return apdu.ToArray();
        }

        /// <summary>
        /// Builds the bytes that go to the reader for the given user input and framing.
        /// </summary>
        public static byte[] BuildOutgoing(byte[] userBytes, RawApduFraming framing)
            => framing == RawApduFraming.NativeDesfire ? WrapNativeDesfire(userBytes) : userBytes;

        /// <summary>
        /// True if the command is a native DESFire command that changes or destroys card content/configuration.
        /// For <see cref="RawApduFraming.ApduAsEntered"/> the INS byte (second byte) is inspected when CLA is 0x90.
        /// </summary>
        public static bool IsPotentiallyDestructive(byte[] userBytes, RawApduFraming framing)
        {
            if (userBytes == null || userBytes.Length == 0)
            {
                return false;
            }

            if (framing == RawApduFraming.NativeDesfire)
            {
                return DestructiveNativeCommands.Contains(userBytes[0]);
            }

            return userBytes.Length >= 2 && userBytes[0] == 0x90 && DestructiveNativeCommands.Contains(userBytes[1]);
        }

        /// <summary>
        /// Splits a response into payload and status word (SW1 SW2) and describes the status.
        /// </summary>
        public static string DescribeResponse(byte[] response)
        {
            if (response == null || response.Length == 0)
            {
                return "(no response)";
            }

            if (response.Length < 2)
            {
                return ToHex(response);
            }

            var sw1 = response[response.Length - 2];
            var sw2 = response[response.Length - 1];
            var payload = response.Take(response.Length - 2).ToArray();
            var text = payload.Length > 0 ? ToHex(payload) + "  | " : string.Empty;

            return text + $"SW={sw1:X2} {sw2:X2} ({DescribeStatusWord(sw1, sw2)})";
        }

        /// <summary>
        /// Describes a status word. <c>91 xx</c> uses the DESFire status table, <c>90 00</c> is ISO success.
        /// </summary>
        public static string DescribeStatusWord(byte sw1, byte sw2)
        {
            if (sw1 == 0x91)
            {
                return DesfireStatus.TryGetValue(sw2, out var name) ? name : "unknown DESFire status";
            }

            if (sw1 == 0x90 && sw2 == 0x00)
            {
                return "OK";
            }

            switch (sw1)
            {
                case 0x61: return "response bytes still available";
                case 0x67: return "wrong length";
                case 0x69: return "command not allowed";
                case 0x6A: return "wrong parameters / not found";
                case 0x6D: return "instruction not supported";
                case 0x6E: return "class not supported";
                default: return "unknown status word";
            }
        }
    }
}
