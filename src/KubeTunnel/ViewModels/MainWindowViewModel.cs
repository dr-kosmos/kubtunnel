using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Threading;
using KubeTunnel.Services;
using KubeTunnel.Models;

namespace KubeTunnel.ViewModels;

public class ConfiguredServiceRow : INotifyPropertyChanged
{
    public required string Namespace { get; init; }
    public required string Service { get; init; }
    public required int RemotePort { get; init; }
    public required int LocalPort { get; init; }
    public string Protocol { get; init; } = "TCP";

    private TunnelStatus _status = TunnelStatus.Idle;
    public TunnelStatus Status
    {
        get => _status;
        set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public string StatusText => Status switch
    {
        TunnelStatus.Idle => "Idle",
        TunnelStatus.Connecting => "Connecting",
        TunnelStatus.Active => "Active",
        TunnelStatus.Reconnecting => "Reconnecting",
        TunnelStatus.Failed => "Failed",
        _ => "Unknown"
    };

    public string ServiceKey => $"{Namespace}/{Service}";

    public PortForwardConfig ToPortForwardConfig() => new()
    {
        Namespace = Namespace,
        Service = Service,
        RemotePort = RemotePort,
        LocalPort = LocalPort,
        Protocol = Protocol
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly doConfigService _configService = new();
    private readonly TunnelService _tunnelService = new();

    // --- Backing fields ---
    private string? _searchText;
    private string _currentProfile = Config.DefaultConfigName;
    private ServiceInfo? _selectedService;
    private ServicePort? _selectedPort;
    private string _localPortText = string.Empty;
    private string _logText = string.Empty;
    private bool _isRunning;
    private string? _saveStatusText;
    private CancellationTokenSource? _saveStatusCts;
    private string _currentThemeName = "Default Dark";
    private bool _dnsMode;

    // --- Collections ---
    public ObservableCollection<ServiceInfo> AllServices { get; } = [];
    public ObservableCollection<ServiceInfo> FilteredServices { get; } = [];
    public ObservableCollection<ConfiguredServiceRow> ConfiguredServices { get; } = [];
    public ObservableCollection<string> ProfileList { get; } = [];
    public ObservableCollection<ServicePort> AvailablePorts { get; } = [];

    // --- Properties ---
    public string? SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(); FilterServices(); }
    }

    public string CurrentProfile
    {
        get => _currentProfile;
        set { _currentProfile = value; OnPropertyChanged(); OnPropertyChanged(nameof(AddSectionHeader)); OnPropertyChanged(nameof(IsNotDefaultProfile)); }
    }

    public string AddSectionHeader => $"Add Service to profile: {CurrentProfile}";
    public bool IsNotDefaultProfile => CurrentProfile != Config.DefaultConfigName;

    public ServiceInfo? SelectedService
    {
        get => _selectedService;
        set
        {
            _selectedService = value;
            OnPropertyChanged();
            RefreshAvailablePorts();
        }
    }

    public ServicePort? SelectedPort
    {
        get => _selectedPort;
        set { _selectedPort = value; OnPropertyChanged(); }
    }

    public bool HasMultiplePorts => AvailablePorts.Count > 1;

    public string LocalPortText
    {
        get => _localPortText;
        set { _localPortText = value; OnPropertyChanged(); }
    }

    public string LogText
    {
        get => _logText;
        private set { _logText = value; OnPropertyChanged(); }
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            _isRunning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotRunning));
            ((RelayCommand)RunCommand).RaiseCanExecuteChanged();
            ((RelayCommand)StopCommand).RaiseCanExecuteChanged();
        }
    }

    public bool IsNotRunning => !IsRunning;

    public string? SaveStatusText
    {
        get => _saveStatusText;
        private set { _saveStatusText = value; OnPropertyChanged(); }
    }

    public bool DnsMode
    {
        get => _dnsMode;
        set
        {
            if (_dnsMode == value) return;
            _dnsMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotDnsMode));

            var config = _configService.LoadConfig();
            config.DnsMode = value;
            _configService.SaveConfig(config);
        }
    }

    public bool IsNotDnsMode => !DnsMode;

    // --- Commands ---
    public ICommand ToggleDnsModeCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand AddToConfigCommand { get; }
    public ICommand DeleteServiceCommand { get; }
    public ICommand KillKubectlCommand { get; }
    public ICommand RunCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand ClearLogCommand { get; }
    public ICommand CopyLogCommand { get; }
    public ICommand CopyRouteCommand { get; }

    // --- Theme ---
    public static List<AppTheme> ThemeList => AppTheme.Presets;

    public string CurrentThemeName
    {
        get => _currentThemeName;
        private set { _currentThemeName = value; OnPropertyChanged(); }
    }

    // These are invoked from code-behind (async dialogs need Window reference)
    public Func<Task>? CreateProfileAction { get; set; }
    public Func<string, Task>? ShowMessageAction { get; set; }
    public Func<string, Task>? CopyToClipboardAction { get; set; }

    public MainWindowViewModel()
    {
        ToggleDnsModeCommand = new RelayCommand(ToggleDnsMode);
        SaveCommand = new RelayCommand(Save);
        AddToConfigCommand = new RelayCommand(AddToConfig);
        DeleteServiceCommand = new RelayCommand<ConfiguredServiceRow>(DeleteService);
        KillKubectlCommand = new RelayCommand(KillKubectl);
        RunCommand = new RelayCommand(Run, () => !IsRunning);
        StopCommand = new RelayCommand(Stop, () => IsRunning);
        ClearLogCommand = new RelayCommand(() => LogText = string.Empty);
        CopyLogCommand = new RelayCommand(CopyLog);
        CopyRouteCommand = new RelayCommand<ConfiguredServiceRow>(CopyRoute);

        _tunnelService.LogMessage += OnTunnelLog;
        _tunnelService.StatusChanged += OnTunnelStatusChanged;

        Initialize();
    }

    private async void Initialize()
    {
        var config = _configService.LoadConfig();
        CurrentProfile = config.CurrentProfile;
        _dnsMode = config.DnsMode;
        OnPropertyChanged(nameof(DnsMode));
        OnPropertyChanged(nameof(IsNotDnsMode));

        // Apply saved theme
        var savedTheme = AppTheme.Presets.FirstOrDefault(t => t.Name == config.Theme)
                         ?? AppTheme.Presets[1]; // Default Dark
        App.ApplyTheme(savedTheme);
        CurrentThemeName = savedTheme.Name;

        // Load profile list
        foreach (var p in _configService.GetProfileList())
            ProfileList.Add(p);

        // Load configured services for current profile
        LoadProfileServices(CurrentProfile);

        // Load available services from kubectl (runs off-thread, then updates UI)
        List<ServiceInfo> services;
        try
        {
            services = await Task.Run(() => _configService.LoadServicesAsync());
        }
        catch
        {
            services = [];
        }

        // Back on UI thread after await
        foreach (var svc in services)
            AllServices.Add(svc);

        FilterServices();
    }

    private void LoadProfileServices(string profileName)
    {
        ConfiguredServices.Clear();
        var configs = _configService.LoadProfile(profileName);
        foreach (var c in configs)
        {
            ConfiguredServices.Add(new ConfiguredServiceRow
            {
                Namespace = c.Namespace,
                Service = c.Service,
                RemotePort = c.RemotePort,
                LocalPort = c.LocalPort,
                Protocol = c.Protocol
            });
        }
    }

    public void FilterServices()
    {
        FilteredServices.Clear();
        foreach (var svc in AllServices)
        {
            // Keep visible if any port+protocol combo is not yet configured
            var hasUnconfigured = svc.Ports.Any(p =>
                !ConfiguredServices.Any(c =>
                    c.Namespace == svc.Namespace &&
                    c.Service == svc.Service &&
                    c.RemotePort == p.Port &&
                    c.Protocol == p.Protocol));

            if (!hasUnconfigured)
                continue;

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(SearchText) &&
                !svc.Service.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                continue;

            FilteredServices.Add(svc);
        }

        RefreshAvailablePorts();
    }

    private void RefreshAvailablePorts()
    {
        AvailablePorts.Clear();

        if (_selectedService == null) return;

        var unconfigured = _selectedService.Ports.Where(p =>
            !ConfiguredServices.Any(c =>
                c.Namespace == _selectedService.Namespace &&
                c.Service == _selectedService.Service &&
                c.RemotePort == p.Port &&
                c.Protocol == p.Protocol));

        foreach (var port in unconfigured)
            AvailablePorts.Add(port);

        SelectedPort = AvailablePorts.FirstOrDefault();
        OnPropertyChanged(nameof(HasMultiplePorts));
    }

    private void ToggleDnsMode()
    {
        DnsMode = !DnsMode;
    }

    private void AddToConfig()
    {
        if (SelectedService == null || SelectedPort == null) return;

        if (!int.TryParse(LocalPortText, out var port))
        {
            ShowMessageAction?.Invoke("Invalid port.");
            return;
        }
        
        if (ConfiguredServices.Any(x =>
                x.Service == SelectedService.Service &&
                x.Namespace == SelectedService.Namespace &&
                x.RemotePort == SelectedPort.Port &&
                x.Protocol == SelectedPort.Protocol))
        {
            ShowMessageAction?.Invoke("This port+protocol is already configured.");
            return;
        }


        if (ConfiguredServices.Any(x => x.LocalPort == port))
        {
            ShowMessageAction?.Invoke("Port conflict.");
            return;
        }

        ConfiguredServices.Add(new ConfiguredServiceRow
        {
            Service = SelectedService.Service,
            Namespace = SelectedService.Namespace,
            RemotePort = SelectedPort.Port,
            LocalPort = port,
            Protocol = SelectedPort.Protocol
        });

        LocalPortText = string.Empty;
        FilterServices();
    }

    private void DeleteService(ConfiguredServiceRow? row)
    {
        if (row == null) return;
        ConfiguredServices.Remove(row);
        FilterServices();
    }

    public void Save()
    {
        var config = _configService.LoadConfig();
        config.CurrentProfile = CurrentProfile;
        _configService.SaveConfig(config);
        _configService.SaveProfile(CurrentProfile, ConfiguredServices.Select(c => c.ToPortForwardConfig()));
        ShowSaveStatus();
    }

    private async void ShowSaveStatus()
    {
        _saveStatusCts?.Cancel();
        _saveStatusCts = new CancellationTokenSource();
        var token = _saveStatusCts.Token;

        SaveStatusText = "Saved";
        try
        {
            await Task.Delay(2000, token);
            SaveStatusText = null;
        }
        catch (OperationCanceledException)
        {
            // restarted by another save
        }
    }

    public void ChangeProfile(string profileName)
    {
        // Save current profile before switching
        _configService.SaveProfile(CurrentProfile, ConfiguredServices.Select(c => c.ToPortForwardConfig()));

        CurrentProfile = profileName;
        LoadProfileServices(profileName);
        FilterServices();
    }

    public void ChangeTheme(AppTheme theme)
    {
        App.ApplyTheme(theme);
        CurrentThemeName = theme.Name;
        var config = _configService.LoadConfig();
        config.Theme = theme.Name;
        _configService.SaveConfig(config);
    }

    public void AddProfile(string profileName)
    {
        _configService.SaveProfile(CurrentProfile, ConfiguredServices.Select(c => c.ToPortForwardConfig()));
        ProfileList.Add(profileName);
        CurrentProfile = profileName;
        ConfiguredServices.Clear();
        FilterServices();
    }

    public void DeleteProfile(string profileName)
    {
        if (profileName == Config.DefaultConfigName) return;

        _configService.DeleteProfile(profileName);
        ProfileList.Remove(profileName);

        if (!ProfileList.Contains(Config.DefaultConfigName))
            ProfileList.Insert(0, Config.DefaultConfigName);

        CurrentProfile = Config.DefaultConfigName;
        LoadProfileServices(CurrentProfile);
        FilterServices();
    }

    private void Run()
    {
        if (ConfiguredServices.Count == 0) return;

        var configs = ConfiguredServices.Select(c => c.ToPortForwardConfig()).ToList();
        try
        {
            _tunnelService.StartAll(configs, _dnsMode);
        }
        catch (InvalidOperationException ex)
        {
            var ts = DateTime.Now.ToString("HH:mm:ss");
            LogText += $"[{ts}] {ex.Message}\n";
            return;
        }
        IsRunning = true;
    }

    private void Stop()
    {
        _tunnelService.StopAll();
        IsRunning = false;
        LogText = string.Empty;
    }

    private async void CopyLog()
    {
        if (string.IsNullOrEmpty(LogText)) return;
        if (CopyToClipboardAction != null)
            await CopyToClipboardAction(LogText);
    }

    private async void CopyRoute(ConfiguredServiceRow? row)
    {
        if (row == null) return;
        if (CopyToClipboardAction == null) return;

        var route = _dnsMode
            ? $"{row.Service}.{row.Namespace}.svc.cluster.local:{row.RemotePort}"
            : $"localhost:{row.LocalPort}";

        await CopyToClipboardAction(route);
    }

    public void Cleanup()
    {
        _tunnelService.StopAll();
        KillKubectl();
    }

    private void KillKubectl()
    {
        foreach (var p in Process.GetProcessesByName("kubectl"))
        {
            try { p.Kill(); }
            catch { /* ignored */ }
        }
    }

    private void OnTunnelLog(string timestamp, string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LogText += $"[{timestamp}] {message}\n";
        });
    }

    private void OnTunnelStatusChanged(string serviceKey, TunnelStatus status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var row = ConfiguredServices.FirstOrDefault(c => c.ServiceKey == serviceKey);
            if (row != null)
                row.Status = status;
        });
    }

    // --- INotifyPropertyChanged ---
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
