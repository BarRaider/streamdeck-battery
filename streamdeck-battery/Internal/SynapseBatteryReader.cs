using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Battery.Internal
{
    internal class SynapseReader
    {
        #region Private members

        private static SynapseReader instance = null;
        private static readonly object objLock = new object();
       
        private List<SynapseBatteryStats> devices = new List<SynapseBatteryStats>();

        #endregion

        #region Constructors

        public static SynapseReader Instance
        {
            get
            {                
                if (instance != null)
                {
                    return instance;
                }

                lock (objLock)
                {
                    if (instance == null)
                    {
                        instance = new SynapseReader();
                    }
                    return instance;
                }
            }
        }

        private SynapseReader()
        {
            var ts = new ThreadStart(ReadLogFiles);
            var backgroundThread = new Thread(ts);
            backgroundThread.Start();
        }

        #endregion

        #region Public Methods

        public List<DeviceInfo> GetAllDevices()
        {
            // TODO: Need to find a way of forcing a comms check with devices, as they are not populated in the log until one plugs/unplugs them
            return devices.Select(s => new DeviceInfo() { Name = s.DeviceName }).ToList();
        }

        public SynapseBatteryStats GetBatteryStats(string deviceName)
        {
            if (devices == null || !devices.Any(x=>x.DeviceName == deviceName))
            {
                return null;
            }

            return devices.Single(x=>x.DeviceName == deviceName);
        }


        #endregion

        #region Private Methods

        private void ReadLogFiles()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var fileName = $"{userProfile}\\AppData\\Local\\Razer\\Synapse3\\Log\\Razer Synapse 3.log";
            using (StreamReader reader = new StreamReader(new FileStream(fileName,
                                 FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                //start at the end of the file
                long lastMaxOffset = reader.BaseStream.Length;

                while (true)
                {
                    System.Threading.Thread.Sleep(250);


                    //if the file size has not changed, idle
                    if (reader.BaseStream.Length == lastMaxOffset)
                        continue;

                    //seek to the last max offset
                    reader.BaseStream.Seek(lastMaxOffset, SeekOrigin.Begin);

                    //read out of the file until the EOF
                    string line = "";
                    while ((line = reader.ReadLine()) != null)
                    {
                        // The log file should contain entries like:
                        //   2023 - 03 - 20 18:34:38.4929 INFO 1 Battery Get By Device Handle:
                        //   Name: Razer BlackWidow
                        //   Product_ID: 602(0x25A)
                        //   Edition_ID: 0(0x0)
                        //   Vendor_ID: 5426(0x1532)
                        //   Layout: 6(0x6)
                        //   Serial_Number: IO2204F46400041
                        //   Firmware_Version: 
                        //   Handle: 481147576(0x1CADBAB8)
                        //   Type: 2
                        //   SKU_ID: SKU
                        //   RegionId: 0
                        //   Feature Name: Battery
                        //   Feature Id: 1315d015 - e350 - 4541 - 9658 - a6030fb62776
                        //   Battery Percentage: 95
                        //   Battery State: Charging
                        //   Device on Dock State: Invalid
                        //   Charging StatusInvalid
                        if (line.Contains("INFO 1 Battery Get By Device Handle"))
                        {
                            var dt = DateTime.Parse(line.Substring(0, 24));


                            var deviceName = reader.ReadLine();
                            deviceName = deviceName?.Substring(deviceName.LastIndexOf(':') + 2);


                            var device = devices.SingleOrDefault(x => x.DeviceName == deviceName);

                            if (device == null)
                            {
                                device = new SynapseBatteryStats { DeviceName = deviceName };
                                devices.Add(device);
                            }

                            device.UpdateDate = dt;

                            var gotEverything = false;
                            while (gotEverything == false)
                            {
                                line = reader.ReadLine();
                                if (line.Contains("Battery Percentage:"))
                                {
                                    device.Percentage = Convert.ToInt32(line.Substring(line.LastIndexOf(':') + 2));
                                }

                                if (line.Contains("Battery State:"))
                                {
                                    device.ChargingState = line.Substring(line.LastIndexOf(':') + 2);
                                    gotEverything = true;
                                }
                            }
                        }
                    }

                    //update the last max offset
                    lastMaxOffset = reader.BaseStream.Position;
                }
            }
        }


        #endregion
    }
}
