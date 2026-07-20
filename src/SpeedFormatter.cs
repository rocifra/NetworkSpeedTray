using System;
using System.Globalization;

namespace NetworkSpeedTray
{
    /// <summary>
    /// Formats byte-per-second rates into compact labels for the tray icon
    /// and a longer human-readable string for the hover tooltip.
    /// </summary>
    internal static class SpeedFormatter
    {
        private static readonly string[] UnitSuffix = { "B", "K", "M", "G", "T" };
        private static readonly string[] TooltipUnit = { "B/s", "KB/s", "MB/s", "GB/s", "TB/s" };

        /// <summary>
        /// Compact label that fits a ~16px tray line, e.g. "0", "12K", "1.2M", "340M".
        /// Kept to at most 4 characters.
        /// </summary>
        public static string Short(double bytesPerSec)
        {
            if (bytesPerSec < 1 || double.IsNaN(bytesPerSec))
            {
                return "0";
            }

            int unit = 0;
            double value = bytesPerSec;
            while (value >= 1024 && unit < UnitSuffix.Length - 1)
            {
                value /= 1024.0;
                unit++;
            }

            string number;
            if (unit == 0)
            {
                // Raw bytes: no decimals.
                number = ((int)value).ToString(CultureInfo.InvariantCulture);
            }
            else if (value < 10)
            {
                // One decimal only when it fits (e.g. 1.2M).
                number = value.ToString("0.0", CultureInfo.InvariantCulture);
            }
            else
            {
                number = ((int)value).ToString(CultureInfo.InvariantCulture);
            }

            return number + UnitSuffix[unit];
        }

        /// <summary>
        /// Full rate string for tooltips, e.g. "1.2 MB/s".
        /// </summary>
        public static string Full(double bytesPerSec)
        {
            if (bytesPerSec < 1 || double.IsNaN(bytesPerSec))
            {
                return "0 B/s";
            }

            int unit = 0;
            double value = bytesPerSec;
            while (value >= 1024 && unit < TooltipUnit.Length - 1)
            {
                value /= 1024.0;
                unit++;
            }

            string number = (unit == 0)
                ? ((int)value).ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.0", CultureInfo.InvariantCulture);

            return number + " " + TooltipUnit[unit];
        }

        /// <summary>
        /// Two-line tooltip text (NotifyIcon.Text supports up to 63 chars).
        /// </summary>
        public static string Tooltip(double downBytesPerSec, double upBytesPerSec)
        {
            return "Down: " + Full(downBytesPerSec) + "\nUp: " + Full(upBytesPerSec);
        }
    }
}
