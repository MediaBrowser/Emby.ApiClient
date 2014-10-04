using Microsoft.Win32;
using System;

namespace MediaBrowser.ApiInteraction
{
    public class Device : IDevice
    {
        public event EventHandler<EventArgs> ResumeFromSleep;

        public string DeviceName { get; set; }
        public string DeviceId { get; set; }

        public Device()
        {
            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
        }

        void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                if (ResumeFromSleep != null)
                {
                    ResumeFromSleep(this, EventArgs.Empty);
                }
            }
        }
    }
}
