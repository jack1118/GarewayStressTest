// Copyright (c) 2012-2020 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

using System;
using System.Configuration;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dicom.Network;
using DicomClient = Dicom.Network.Client.DicomClient;

namespace GatewayStressTest
{
    internal static class Program
    {
        private static readonly string _storeServerHost = ConfigurationManager.AppSettings.Get("Host");
        private static readonly int _storeServerPort = Int32.Parse(ConfigurationManager.AppSettings.Get("Port"));
        private static readonly string _storeServerAET = ConfigurationManager.AppSettings.Get("AETserver");
        private static readonly string _aet = ConfigurationManager.AppSettings.Get("AETclient");
        private static readonly string _testDICOMPath = ConfigurationManager.AppSettings.Get("TestDICOMPath");

        private static readonly int _count = Int32.Parse(ConfigurationManager.AppSettings.Get("Count"));
        private static readonly string _interval = ConfigurationManager.AppSettings.Get("Interval");
        private static readonly int _randomIntervalmin = Int32.Parse(ConfigurationManager.AppSettings.Get("RandomIntervalmin"));
        private static readonly int _randomIntervalmax = Int32.Parse(ConfigurationManager.AppSettings.Get("RandomIntervalmax"));
        private static readonly int _thread = Int32.Parse(ConfigurationManager.AppSettings.Get("Thread"));
        private static readonly string _manual = ConfigurationManager.AppSettings.Get("ManualMode");

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly Random rand = new Random();

        static void Main(string[] args)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName) + "\\Log");

            var storeMore = "";
            int tCnt = _thread;

            log.Info("***************************************************");
            log.Info("Server Host Address: " + _storeServerHost);
            log.Info("Server Port: " + _storeServerPort);
            log.Info("Server AE Title: " + _storeServerAET);
            log.Info("Client AE Title: " + _aet);
            log.Info("Test count: " + _count);
            log.Info("Test interval: " + _interval);
            log.Info("Test thread: " + _thread);
            log.Info("Test dicom: " + _testDICOMPath);
            log.Info("***************************************************");
            if (_manual == "Y")
            {
                tCnt = 1;
                log.Info("To start test, enter \"y\"; Othersie, press any key to exit: ");
                storeMore = Console.ReadLine().Trim();
                if (storeMore.Length > 0 && storeMore.ToLower()[0] == 'y')
                {
                }
                else
                {
                    Environment.Exit(0);
                }
            }

            Thread[] workerThreads = new Thread[tCnt];

            for (int i = 0; i < workerThreads.Length; i++)
            {
                int tNum = i;
                workerThreads[i] = new Thread(new ThreadStart(async () =>
                {
                    //var client = new DicomClient(_storeServerHost, _storeServerPort, false, _aet, _storeServerAET);
                    //Add a handler to be notified of any association rejections
                    //client.AssociationRejected += (sender, e) => {
                    //    log.Warn($"Association was rejected. Rejected Reason:{e.Reason}");
                    //};

                    //Add a handler to be notified of any association information on successful connections
                    //client.AssociationAccepted += (sender, e) =>
                    //{
                    //    log.Info(($"Association was accepted by:{e.Association.RemoteHost}");
                    //};

                    //Add a handler to be notified when association is successfully released - this can be triggered by the remote peer as well
                    //client.AssociationReleased += (sender, e) => {
                    //    log.Info("Association was released. BYE BYE!");
                    //};
                    //client.RequestTimedOut += (sender, e) =>
                    //{
                    //    log.Warn($"Send PACS error exception:{e.Request} {e.Timeout}");
                    //    throw new NotImplementedException();
                    //};
                    //client.NegotiateAsyncOps();

                    int count = 0;
                    while ((_manual == "Y" && storeMore.Length > 0 && storeMore.ToLower()[0] == 'y') || (_manual != "Y" && count < _count))
                    {
                        try
                        {
                            var client = new DicomClient(_storeServerHost, _storeServerPort, false, _aet, _storeServerAET);                            
                            client.NegotiateAsyncOps();

                            string dicomFile = _testDICOMPath;

                            if (_manual == "Y")
                            {
                                while (!File.Exists(dicomFile))
                                {
                                    log.Warn("Invalid file path, enter the path for a DICOM file or press Enter to Exit:");

                                    dicomFile = Console.ReadLine();

                                    if (string.IsNullOrWhiteSpace(dicomFile))
                                    {
                                        return;
                                    }
                                }
                            }
                            else
                            {
                                if (!File.Exists(dicomFile))
                                {
                                    log.Warn("Invalid file path, check test dicom file: " + _testDICOMPath);
                                    return;
                                }
                            }

                            if (_manual != "Y")
                            {
                                log.Info("Test " + tNum + "-[" + (count + 1) + "]");
                            }

                            var request = new DicomCStoreRequest(dicomFile);

                            request.OnResponseReceived += (req, response) =>
                            {
                                if (_manual == "Y")
                                {
                                    log.Info("C-Store Response Received, Status: " + response.Status);
                                    log.Info("To test again, enter \"y\"; Othersie, press any key to exit: ");
                                    storeMore = Console.ReadLine().Trim();
                                    if (storeMore.Length > 0 && storeMore.ToLower()[0] == 'y')
                                    {
                                    }
                                    else
                                    {
                                        Environment.Exit(0);
                                    }
                                }
                                else
                                {
                                    log.Info(tNum + "-[" + (count + 1) + "] " + "C-Store Response Received, Status: " + response.Status);
                                    if (count < (_count - 1))
                                    {                                        
                                        int fortimerinterval = 0;
                                        if (_interval == "random")
                                        {
                                            fortimerinterval = rand.Next(_randomIntervalmin, _randomIntervalmax);
                                        }
                                        else
                                        {
                                            fortimerinterval = Int32.Parse(_interval);
                                        }
                                        log.Info(tNum + "-[" + (count + 1) + "] " + "Time interval " + fortimerinterval / 1000 + " seconds");
                                        count++;
                                        Thread.Sleep(fortimerinterval);
                                    }
                                    else
                                    {
                                        bool allDone = true;
                                        foreach (var workerThread in workerThreads)
                                        {
                                            if (workerThread.IsAlive)
                                            {
                                                allDone = false;
                                                break;
                                            }
                                        }
                                        if (allDone)
                                        {
                                            Thread.Sleep(15000);
                                            Environment.Exit(0);
                                        }
                                    }
                                }
                            };

                            await client.AddRequestAsync(request);
                            await client.SendAsync();
                        }
                        catch (DicomAssociationRejectedException assoRejectEx)
                        {
                            log.Warn("----------------------------------------------------");
                            log.Warn(tNum + "-[" + (count + 1) + "] " + assoRejectEx.Message);
                            log.Warn("----------------------------------------------------");
                        }
                        catch (Exception exception)
                        {
                            log.Error("----------------------------------------------------");
                            log.Error(tNum + "-[" + (count + 1) + "] " + exception.ToString());
                            log.Error("----------------------------------------------------");
                        }
                    }
                }
                ))
                {
                    IsBackground = true,
                    Name = $"SenderThread #{i}",
                    Priority = ThreadPriority.AboveNormal
                };
                workerThreads[i].Start();
            }
            //  Await all background threads
            foreach (var workerThread in workerThreads)
                workerThread.Join(600000);      //  10 minutes thread timeout           
            SpinWait.SpinUntil(() => false);
        }
    }
}
