using System.Net;
using System.Net.Sockets;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PayloadSender;

public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    public partial List<IPAddress> AddressList { get; set; }

    [ObservableProperty]
    public partial object SelectedAddress { get; set; }

    public BaseViewModel()
    {
        AddressList = GetHostIp();
        var selectedIPs = AddressList?.ToList();

        if (AddressList.Any())
        {
            var defaultIP = AddressList.Where(ip => ip.ToString().StartsWith("192.168.1.")).FirstOrDefault();
            if (defaultIP != null)
            {
                SelectedAddress = defaultIP;
            }
            else
            {
                SelectedAddress = AddressList?.LastOrDefault();
            }
        }
    }

    public static List<IPAddress> GetHostIp()
    {
        IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());

        return ipHost.AddressList
                .Where(p => p.AddressFamily == AddressFamily.InterNetwork)
                .ToList();
    }
}
