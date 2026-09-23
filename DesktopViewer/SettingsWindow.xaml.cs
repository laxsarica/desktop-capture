using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace DesktopViewer;

public partial class SettingsWindow : Window
{
    [DllImport("user32.dll")]
    public static extern uint SetWindowDisplayAffinity(IntPtr hwnd, uint dwAffinity);

    const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    public SettingsWindow()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
        };

        // Load current settings
        ApiKeyBox.Password = App.Settings.OpenAiApiKey;

        // Select current model in combo box
        foreach (ComboBoxItem item in ModelCombo.Items)
        {
            if (item.Content?.ToString() == App.Settings.Model)
            {
                ModelCombo.SelectedItem = item;
                break;
            }
        }

        // Allow dragging the window
        MouseLeftButtonDown += (_, _) =>
        {
            try { DragMove(); } catch { }
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var apiKey = ApiKeyBox.Password.Trim();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            MessageBox.Show(
                "Please enter your OpenAI API key.",
                "Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var model = (ModelCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "gpt-4o";

        // Update in-memory settings
        App.Settings.OpenAiApiKey = apiKey;
        App.Settings.Model = model;

        // Persist to disk
        App.Settings.Save();

        // Reload
        App.LoadSettings();

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
