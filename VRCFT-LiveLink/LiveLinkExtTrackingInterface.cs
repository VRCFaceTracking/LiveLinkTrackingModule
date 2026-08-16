using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Runtime.InteropServices;

using System.Diagnostics;

using VRCFaceTracking;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Types;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Params.Expressions;
using System.Drawing.Imaging;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace LiveLinkExtTrackingInterface;
// simple config file for changing LiveLink port
//public struct LLModuleConfigData
//{
//    public const ushort DefaultPortNo = 11111;

//    [JsonInclude]
//    public ushort PortNo;

//    public static LLModuleConfigData Default
//    {
//        get => new LLModuleConfigData()
//        {
//            PortNo = DefaultPortNo,
//        };
//    }
//}

public class LiveLinkExtTrackingInterface : ExtTrackingModule
{
    private ILiveLinkPacketReader[] _readers =
    [
        new ArKitPacketReader(),
        new MetaHumanPacketReader()
    ];

    private UdpClient? _liveLinkConnection;
    private IPEndPoint? _liveLinkRemoteEndpoint;

    private (bool, bool) trackingSupported = (false, false);

    private bool disconnectWarned = false;

    private LiveLinkModuleConfig _moduleConfig = new();

    //List<Stream> _images = new List<Stream>();

    public static IEnumerable<string> GetLocalIPAddresses()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                yield return ip.ToString();
            }
        }
    }

    // Synchronous module initialization. Take as much time as you need to initialize any external modules. This runs in the init-thread
    public override (bool SupportsEye, bool SupportsExpression) Supported => (true, true);

    public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
    {
        ModuleInformation.Name = "LiveLink";

        var stream = GetType().Assembly.GetManifestResourceStream("VRCFT___LiveLink.Assets.iphone-livelink.png");
        ModuleInformation.StaticImages = stream != null ? new List<Stream> { stream } : ModuleInformation.StaticImages;

        Logger.LogInformation("Initializing Live Link Tracking module");

        _moduleConfig = LiveLinkModuleConfig.LoadOrNewConfig(Logger, LiveLinkModuleConfig.ModuleConfigPath.FullPath);

        //_cancellationToken?.Cancel();
        //UnifiedTrackingData.LatestEyeData.SupportsImage = false;
        //UnifiedTrackingData.LatestLipData.SupportsImage = false;

        // UPD client stuff
        _liveLinkConnection = new UdpClient(_moduleConfig.PortNum);
        //_liveLinkRemoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
        _liveLinkRemoteEndpoint = new IPEndPoint(IPAddress.Any, _moduleConfig.PortNum);
        //_liveLinkConnection.Client.Bind(_liveLinkRemoteEndpoint);

        //_liveLinkConnection.Client.SendTimeout = 1000;
        _liveLinkConnection.Client.ReceiveTimeout = 1000;

        // async wait for connection, timeout after timetoWait
        var timeToWait = TimeSpan.FromSeconds(180);

        // compile list of IP addresses
        string likelyIpAddressList = "";
        string otherIpAddressList = "";
        foreach (string ip in GetLocalIPAddresses())
        {
            if (!string.IsNullOrEmpty(ip))
            {
                // reference for expected internal IP address format: https://www.okta.com/identity-101/internal-ip/
                string[] addressBytes = ip.Split('.');
                string networkPart = $"{addressBytes[0]}.{addressBytes[1]}";

                // 192.168.0.0 to 192.168.255.255, which offers about 65,000 unique IP addresses 
                // 10.0.0.0 to 10.255.255.255, a range that provides up to 16 million unique IP addresses
                if (networkPart.Equals("192.168") || addressBytes[0].Equals("10"))
                {
                    likelyIpAddressList += $"\n\t{ip}";
                }    
                else if (addressBytes[0].Equals("172"))
                {
                    // convert addressBytes[1] to integer
                    int addressByte1 = 0;
                    try
                    {
                        addressByte1 = int.Parse(addressBytes[1]);
                    }
                    catch (FormatException)
                    {
                        Logger.LogError("Invalid IP address happened somehow..");
                        continue;
                    }
                    // 172.16.0.0 to 172.31.255.255, providing about 1 million unique IP addresses 
                    if (addressByte1 >= 16 && addressByte1 <= 31)
                    {
                        likelyIpAddressList += $"\n\t{ip}";
                    }  
                }
                else
                {
                    otherIpAddressList += $"\n\t{ip}";
                }
            }
        }
        if (likelyIpAddressList.Length == 0 && otherIpAddressList.Length == 0)
        {
            Logger.LogError("No local network connection found!");
            return (false, false);
        }
        Logger.LogInformation($"Seeking LiveLink connection for {timeToWait.TotalSeconds} seconds. " +
                              $"Accepting data on: \n\nIP Address(es)\n========================{likelyIpAddressList}\n========================\n\nUse Port: {_moduleConfig.PortNum}\n");
        Logger.LogDebug($"Other IP Addresses found: \n{otherIpAddressList}\n");

        var asyncResult = _liveLinkConnection.BeginReceive(null, null);

        asyncResult.AsyncWaitHandle.WaitOne(timeToWait);
        if (asyncResult.IsCompleted)
        {
            try
            {
                // EndReceive worked and we have received data and remote endpoint
                byte[] receivedBytes = _liveLinkConnection.EndReceive(asyncResult, ref _liveLinkRemoteEndpoint);
                Logger.LogInformation("Successful message receive"); 
                //if (receivedBytes.Length < 244)
                //{
                //    // wrong kind of data
                //    trackingSupported = (false, false);
                //    return trackingSupported;
                //}

            }
            catch (Exception ex)
            {
                // EndReceive failed and we ended up here
                Logger.LogError($"Error Occurred Attempting Receiving Data: {ex.ToString()}");
            }
        }
        else
        {
            // The operation wasn't completed before the timeout and we're off the hook
            // nothing init so return false
            Logger.LogWarning("Did not receive message from LiveLink within initialization period, re-initialize the module to try again...");
            trackingSupported = (false, false);
            return trackingSupported;
        }

        trackingSupported = (true, true);
        return trackingSupported;
    }

    // This will be run in the tracking thread. This is exposed so you can control when and if the tracking data is updated down to the lowest level.
    public override void Update()
    {
        if (_liveLinkConnection == null || _liveLinkRemoteEndpoint == null)
        {
            Logger.LogDebug("Attempted to update tracking without module initialization");
            return;
        }
        if (ReadData(_liveLinkConnection, _liveLinkRemoteEndpoint, out var bytes))
        {
            foreach (var reader in _readers)
            {
                if (!reader.CanRead(bytes)) continue;
                
                reader.Parse(bytes);
                
                //TODO: Maybe use UsingEye/UsingExpressions for this. Not important now as we filter in the module process anyway
                reader.UpdateUnifiedExpressions(ref UnifiedTracking.Data, trackingSupported.Item1, trackingSupported.Item2);
            }
        }
    }

    // A chance to de-initialize everything.
    public override void Teardown()
    {
        // shut down the upd client
        Logger.LogInformation("Closing LiveLink UDP client...");
        _liveLinkConnection.Close();
        _liveLinkConnection.Dispose();
    }

    // ===============================================================

    // Read the data from the LiveLink UDP stream and place it into a LiveLinkTrackingDataStruct
    private bool ReadData(UdpClient liveLinkConnection, IPEndPoint liveLinkRemoteEndpoint, out byte[] receiveBytes)
    {
        Dictionary<string, float> values = new Dictionary<string, float>();

        try
        {
            // Grab the packet
            // will block but with a timeout set in the init function
            receiveBytes = liveLinkConnection.Receive(ref liveLinkRemoteEndpoint);
                
            // got a good message
            if (disconnectWarned)
            {
                Logger.LogInformation("LiveLink connection reestablished");
                disconnectWarned = false;
            }
        }
        catch (SocketException se)
        {
            if (se.SocketErrorCode == SocketError.TimedOut)
            {
                if (!disconnectWarned)
                {
                    Logger.LogWarning("LiveLink connection lost");
                    disconnectWarned = true;
                }
            } else
            {
                // some other network socket exception
                Logger.LogError(se.ToString());
            }
            receiveBytes = [];
            return false;
        }
        catch (Exception e)
        {
            // some other exception
            Logger.LogError(e.ToString());
            receiveBytes = [];
            return false;
        }

        return true; 
    }
}