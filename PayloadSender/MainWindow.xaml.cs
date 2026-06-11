using System.Net.Sockets;
using Microsoft.UI.Windowing;
using Microsoft.Windows.Storage.Pickers;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
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

            IFStatus.Title = $"{Path.GetFileName(TxtPayloadFilePath.Text)} Payload sent successfully.";
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
        IFStatus.Title = "Please select your payload file and click Send Payload.";
        IFStatus.Message = "";
        IFStatus.Severity = InfoBarSeverity.Informational;

        var picker = new FileOpenPicker(AppWindow.Id);
        picker.FileTypeChoices.Add("All Files", new[] { "*" });
        picker.FileTypeChoices.Add("Payload Files", new[] { ".js", ".elf", ".pkg", ".bin" });
        picker.FileTypeChoices.Add("ELF Files", new[] { ".elf" });
        picker.FileTypeChoices.Add("JavaScript Files", new[] { ".js" });
        picker.FileTypeChoices.Add("PKG Files", new[] { ".pkg" });
        picker.FileTypeChoices.Add("Bin Files", new[] { ".bin" });
        picker.Title = "Open Payload";
        picker.InitialFileTypeIndex = 1;
        var result = await picker.PickSingleFileAsync();
        if (result != null)
        {
            TxtPayloadFilePath.Text = result.Path;
            BtnSendPayload.IsEnabled = true;
            IFStatus.Title = $"{Path.GetFileName(result.Path)} selected.";
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

    private async void Grid_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();

            if (items.Count > 0)
            {
                var file = items[0] as StorageFile;

                if (file != null)
                {
                    IFStatus.Title = $"{file.Name} selected.";
                    IFStatus.Message = "";
                    IFStatus.Severity = InfoBarSeverity.Informational;

                    TxtPayloadFilePath.Text = file.Path;

                    BtnSendPayload.IsEnabled = true;
                }
            }
        }
    }

    private void Grid_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Drop file here";
        e.DragUIOverride.IsCaptionVisible = true;
        e.DragUIOverride.IsContentVisible = true;
    }
}

