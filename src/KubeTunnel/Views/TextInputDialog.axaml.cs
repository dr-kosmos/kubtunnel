using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace KubeTunnel.Views;

public partial class TextInputDialog : Window
{
    public string InputText
    {
        get => InputTextBox.Text ?? string.Empty;
        set => InputTextBox.Text = value;
    }

    public TextInputDialog()
    {
        InitializeComponent();
    }

    public TextInputDialog(string title, string defaultText = "") : this()
    {
        Title = title;
        InputTextBox.Text = defaultText;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        InputTextBox.Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
        {
            Close(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(InputText))
        {
            Close(InputText);
            e.Handled = true;
        }
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(InputText))
            Close(InputText);
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
