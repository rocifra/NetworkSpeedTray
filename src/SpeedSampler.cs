using System;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace NetworkSpeedTray
{
    /// <summary>
    /// Samples cumulative byte counters across the active network adapters and
    /// derives download / upload rates in bytes per second between calls.
    /// </summary>
    internal sealed class SpeedSampler
    {
        private long _prevReceived;
        private long _prevSent;
        private bool _hasPrevious;
        private readonly Stopwatch _stopwatch = new Stopwatch();

        public double DownBytesPerSec { get; private set; }
        public double UpBytesPerSec { get; private set; }

        /// <summary>
        /// Reads current totals, computes the rate since the previous call using
        /// real elapsed time, and stores the new baseline. The first call only
        /// establishes a baseline and reports zero.
        /// </summary>
        public void Sample()
        {
            long received;
            long sent;
            ReadTotals(out received, out sent);

            if (!_hasPrevious)
            {
                _prevReceived = received;
                _prevSent = sent;
                _hasPrevious = true;
                _stopwatch.Restart();
                DownBytesPerSec = 0;
                UpBytesPerSec = 0;
                return;
            }

            double seconds = _stopwatch.Elapsed.TotalSeconds;
            _stopwatch.Restart();

            if (seconds <= 0)
            {
                // Guard against a zero interval; keep the previous reading.
                _prevReceived = received;
                _prevSent = sent;
                return;
            }

            long deltaReceived = received - _prevReceived;
            long deltaSent = sent - _prevSent;

            // Counters can reset (adapter change / rollover); clamp negatives to 0.
            if (deltaReceived < 0) deltaReceived = 0;
            if (deltaSent < 0) deltaSent = 0;

            DownBytesPerSec = deltaReceived / seconds;
            UpBytesPerSec = deltaSent / seconds;

            _prevReceived = received;
            _prevSent = sent;
        }

        /// <summary>
        /// Sums received/sent bytes over all operational, non-loopback,
        /// non-tunnel adapters.
        /// </summary>
        private static void ReadTotals(out long received, out long sent)
        {
            received = 0;
            sent = 0;

            NetworkInterface[] interfaces;
            try
            {
                interfaces = NetworkInterface.GetAllNetworkInterfaces();
            }
            catch (NetworkInformationException)
            {
                return;
            }

            foreach (NetworkInterface nic in interfaces)
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                NetworkInterfaceType type = nic.NetworkInterfaceType;
                if (type == NetworkInterfaceType.Loopback || type == NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                try
                {
                    IPv4InterfaceStatistics stats = nic.GetIPv4Statistics();
                    received += stats.BytesReceived;
                    sent += stats.BytesSent;
                }
                catch (NetworkInformationException)
                {
                    // Some virtual adapters expose no statistics; skip them.
                }
            }
        }
    }
}
