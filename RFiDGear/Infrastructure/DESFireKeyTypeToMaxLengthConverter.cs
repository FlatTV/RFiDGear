using RFiDGear.Models;
using System;
using System.Globalization;
using System.Windows.Data;

namespace RFiDGear.Infrastructure
{
    /// <summary>
    /// Converts a <see cref="DESFireKeyType"/> value to the maximum permitted *raw* character count
    /// for a key TextBox: the hex-digit count (48 for <see cref="DESFireKeyType.DF_KEY_3K3DES"/>, 32
    /// for all other types) PLUS the byte-separator spaces that CustomConverter's own
    /// "12 34 56 ..." formatting inserts (one space between every byte pair, i.e. hexLength/2 - 1
    /// extra characters). WPF's TextBox.MaxLength counts every raw character including spaces, so
    /// binding it to the bare hex-digit count truncates a pasted, space-formatted key well before
    /// all its hex digits arrive - the ViewModel's own NormalizeDesfireKeyInput already strips
    /// spaces correctly, this converter's job is only to not cut the paste off first.
    /// </summary>
    [ValueConversion(typeof(DESFireKeyType), typeof(int))]
    public sealed class DESFireKeyTypeToMaxLengthConverter : IValueConverter
    {
        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is DESFireKeyType keyType))
            {
                return 32;
            }

            var hexLength = CustomConverter.GetExpectedKeyHexLength(keyType);
            return hexLength + (hexLength / 2 - 1);
        }

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException($"{nameof(DESFireKeyTypeToMaxLengthConverter)} does not support ConvertBack.");
    }
}
