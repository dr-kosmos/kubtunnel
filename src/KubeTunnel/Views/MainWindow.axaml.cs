using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using KubeTunnel.ViewModels;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace KubeTunnel.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    public MainWindow()
    {
        var vm = new MainWindowViewModel();
        DataContext = vm;

        vm.ShowMessageAction = ShowMessage;
        vm.CreateProfileAction = CreateProfile;

        InitializeComponent();
        BuildProfileMenu();
        BuildThemeMenu();

        vm.PropertyChanged += OnViewModelPropertyChanged;

        // DataGrid swallows Enter, so we must listen with handledEventsToo
        ServicesGrid.AddHandler(KeyDownEvent, ServicesGrid_OnKeyDown, RoutingStrategies.Tunnel);
        // ComboBox swallows Enter (opens dropdown), so intercept via Tunnel
        CmbPort.AddHandler(KeyDownEvent, CmbPort_OnKeyDown, RoutingStrategies.Tunnel);

        TxtSearch.Focus();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.LogText))
        {
            LogScroller.ScrollToEnd();
        }
    }

    private void BuildProfileMenu()
    {
        ProfileMenuItem.Items.Clear();
        foreach (var profile in ViewModel.ProfileList)
        {
            var header = profile == ViewModel.CurrentProfile
                ? $"✓ {profile}"
                : $"   {profile}";
            var item = new MenuItem { Header = header };
            item.Click += (_, _) =>
            {
                ViewModel.ChangeProfile(profile);
                BuildProfileMenu();
            };
            ProfileMenuItem.Items.Add(item);
        }
    }

    private async void DeleteProfile_OnClick(object? sender, RoutedEventArgs e)
    {
        var box = MessageBoxManager.GetMessageBoxStandard(
            "Delete Profile",
            $"Are you sure you want to delete profile \"{ViewModel.CurrentProfile}\"? This cannot be undone.",
            ButtonEnum.YesNo,
            MsBox.Avalonia.Enums.Icon.Warning);
        var result = await box.ShowAsPopupAsync(this);

        if (result == ButtonResult.Yes)
        {
            ViewModel.DeleteProfile(ViewModel.CurrentProfile);
            BuildProfileMenu();
        }
    }

    private void BuildThemeMenu()
    {
        ThemeMenuItem.Items.Clear();
        foreach (var theme in MainWindowViewModel.ThemeList)
        {
            var header = theme.Name == ViewModel.CurrentThemeName
                ? $"✓ {theme.Name}"
                : $"   {theme.Name}";
            var item = new MenuItem { Header = header };
            item.Click += (_, _) =>
            {
                ViewModel.ChangeTheme(theme);
                BuildThemeMenu();
            };
            ThemeMenuItem.Items.Add(item);
        }
    }

    private async void CreateProfile_OnClick(object? sender, RoutedEventArgs e)
    {
        await CreateProfile();
    }

    private async Task CreateProfile()
    {
        var dialog = new TextInputDialog("Create profile");
        var result = await dialog.ShowDialog<string?>(this);

        if (!string.IsNullOrWhiteSpace(result))
        {
            ViewModel.AddProfile(result);
            BuildProfileMenu();
        }
    }

    private async Task ShowMessage(string message)
    {
        var box = MessageBoxManager.GetMessageBoxStandard("KubeTunnel", message, ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Warning);
        await box.ShowAsPopupAsync(this);
    }

    // Search box → Enter → select first service in grid and focus it
    private void TxtSearch_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (ViewModel.FilteredServices.Count > 0)
            {
                ServicesGrid.SelectedIndex = 0;
                ServicesGrid.Focus();
                ServicesGrid.ScrollIntoView(ViewModel.FilteredServices[0], null);
            }
            e.Handled = true;
        }
    }

    // Grid → Enter → if multiple ports, focus port combo; otherwise go straight to local port
    private void ServicesGrid_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (ViewModel.HasMultiplePorts)
            {
                CmbPort.Focus();
            }
            else
            {
                SuggestLocalPortAndFocus();
            }
            e.Handled = true;
        }
    }

    // Port combo → Enter → suggest local port and focus the local port field
    // Port combo → Escape → back to search
    private void CmbPort_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !CmbPort.IsDropDownOpen)
        {
            FocusSearch();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter) return;

        if (CmbPort.IsDropDownOpen)
        {
            // Let it close the dropdown, then move on next Enter
            return;
        }

        SuggestLocalPortAndFocus();
        e.Handled = true;
    }

    private void SuggestLocalPortAndFocus()
    {
        var maxPort = ViewModel.ConfiguredServices.Count > 0
            ? ViewModel.ConfiguredServices.Max(c => c.LocalPort)
            : 0;

        if (maxPort > 0)
            ViewModel.LocalPortText = (maxPort + 1).ToString();

        TxtLocalPort.Focus();
        TxtLocalPort.SelectAll();
    }

    // Local port → Enter → add service, focus back to search
    // Local port → Escape → back to search
    private void TxtLocalPort_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel.AddToConfigCommand.Execute(null);
            FocusSearch();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            FocusSearch();
            e.Handled = true;
        }
    }

    private void FocusSearch()
    {
        TxtSearch.Focus();
        TxtSearch.SelectAll();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        ViewModel.Cleanup();
        base.OnClosing(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled) return;

        if (e.Key == Key.P && e.KeyModifiers == KeyModifiers.Control)
        {
            _ = CreateProfile();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            ServicesGrid.SelectedItem = null;
            ConfiguredGrid.SelectedItem = null;
            FocusSearch();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            FocusSearch();
            e.Handled = true;
        }
    }
}
