using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.UI.Windowing;
using Microsoft.Windows.Storage.Pickers;
using Windows.System;
using WinRT;

namespace PayloadSender.Views;

public sealed partial class MainWindow : Window
{
    public BaseViewModel ViewModel { get; }

    public MainWindow()
    {
        ViewModel = App.GetService<BaseViewModel>();
        this.InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        var presenter = AppWindow.Presenter.As<OverlappedPresenter>();
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = true;
        presenter.IsResizable = false;
        presenter.IsAlwaysOnTop = true;
        var manager = new WindowManager(this);
        manager.Width = 600;
        manager.Height = 360;
    }

    private async void BtnSendPayload_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            PrgStatus.Value = 0;
            PrgStatus.PercentCritical = 1001;
            PrgStatus.Visibility = Visibility.Visible;

            IFStatus.Title = "Sending Payload...";
            IFStatus.Message = string.Empty;
            IFStatus.Severity = InfoBarSeverity.Warning;
            BtnSendPayload.IsEnabled = false;

            if (ViewModel.SelectedAddress is null || string.IsNullOrEmpty(ViewModel.SelectedAddress.ToString()) || string.IsNullOrEmpty(TxtPayloadFilePath.Text))
                return;

            if (!File.Exists(TxtPayloadFilePath.Text))
            {
                await MessageBox.ShowErrorAsync("Payload not found!");
                return;
            }

            var cts = new CancellationTokenSource();
            var progress = new Progress<double>(value =>
            {
                PrgStatus.Value = value;
            });

            await SendPayloadAsync(ViewModel.SelectedAddress.ToString(), (int)NBPort.Value, TxtPayloadFilePath.Text, progress, cts.Token);

            IFStatus.Title = "Payload sent successfully.";
            IFStatus.Message = "";
            IFStatus.Severity = InfoBarSeverity.Success;
        }
        catch (Exception ex)
        {
            IFStatus.Title = "Error";
            IFStatus.Message = ex.Message;
            IFStatus.Severity = InfoBarSeverity.Error;
            PrgStatus.PercentCritical = 1;

            BtnSendPayload.IsEnabled = true;
        }
    }

    private async void BtnOpenFile_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker(AppWindow.Id);
        picker.FileTypeChoices.Add("All Files", new[] { "*" });
        picker.FileTypeChoices.Add("ELF Files", new[] { ".elf" });
        picker.InitialFileTypeIndex = 1;
        var result = await picker.PickSingleFileAsync();
        if (result != null)
        {
            TxtPayloadFilePath.Text = result.Path;
            BtnSendPayload.IsEnabled = true;
        }
    }

    public async Task SendPayloadAsync(string ip, int port, string filePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        const int BufferSize = 64 * 1024;

        using TcpClient client = new();

        // Connect with timeout
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await client.ConnectAsync(ip, port, timeoutCts.Token);

        using NetworkStream network = client.GetStream();
        using FileStream file = File.OpenRead(filePath);

        long totalBytes = file.Length;
        long sentBytes = 0;

        byte[] buffer = new byte[BufferSize];

        while (true)
        {
            int read = await file.ReadAsync(buffer, cancellationToken);

            if (read == 0)
                break;

            await network.WriteAsync(buffer.AsMemory(0, read), cancellationToken);

            sentBytes += read;

            progress?.Report((double)sentBytes / totalBytes * 100.0);
        }

        await network.FlushAsync(cancellationToken);

        client.Client.Shutdown(SocketShutdown.Send);
    }
    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri("http://github.com/ghost1372/PayloadSender"));
    }
}

