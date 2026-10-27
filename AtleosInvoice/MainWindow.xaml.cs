using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Printing;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AtleosInvoice;

public partial class MainWindow : Window
{
    InvoiceModel Model = new();
    string? currentFile;
    readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true, MaxDepth = 32 };
    const long MaxInvoiceFileBytes = 5 * 1024 * 1024;

    public MainWindow()
    {
        InitializeComponent();
        Model.Items.CollectionChanged += (_, _) => Model.Refresh();
        foreach (var line in Model.Items) line.PropertyChanged += (_, _) => Model.Refresh();
        DataContext = Model;
    }

    void BindModel(InvoiceModel model)
    {
        Model = model;
        if (Model.PaymentMethod.Equals("Check", StringComparison.OrdinalIgnoreCase)) Model.PaymentMethod = "Cheque";
        if (Model.DueDate is not ("Within 15 days" or "Within 30 days" or "Within 45 days")) Model.DueDate = "Within 15 days";
        Model.Refresh();
        Model.Items.CollectionChanged += (_, _) => Model.Refresh();
        foreach (var line in Model.Items) line.PropertyChanged += (_, _) => Model.Refresh();
        DataContext = Model;
    }

    void New_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        BindModel(new InvoiceModel()); currentFile = null;
    }

    bool ConfirmDiscard() => MessageBox.Show("Start a new invoice? Unsaved edits will be lost.", "New invoice", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    void AddLine_Click(object sender, RoutedEventArgs e)
    {
        var line = new InvoiceLine();
        line.PropertyChanged += (_, _) => Model.Refresh();
        Model.Items.Add(line);
    }

    void RemoveLine_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is InvoiceLine line) Model.Items.Remove(line);
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        var path = currentFile;
        if (path is null)
        {
            var dialog = new SaveFileDialog { Filter = "Invoice data (*.json)|*.json", FileName = $"Invoice-{SafeFileName(Model.InvoiceNumber)}.json", InitialDirectory = LocalInvoiceFolder() };
            if (dialog.ShowDialog(this) != true) return;
            path = dialog.FileName;
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(Model, jsonOptions)); currentFile = path;
            MessageBox.Show("Invoice saved on this computer.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show($"Could not save invoice:\n{ex.Message}", "Save error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Invoice data (*.json)|*.json", InitialDirectory = LocalInvoiceFolder() };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            using var input = File.OpenRead(dialog.FileName);
            if (input.Length > MaxInvoiceFileBytes)
                throw new InvalidDataException("Invoice data files must be 5 MB or smaller.");
            var model = JsonSerializer.Deserialize<InvoiceModel>(input, jsonOptions)
                ?? throw new InvalidDataException("This file does not contain an invoice.");
            if (model.Items is null) model.Items = new ObservableCollection<InvoiceLine>();
            BindModel(model); currentFile = dialog.FileName;
        }
        catch (Exception ex) { MessageBox.Show($"Could not open invoice:\n{ex.Message}", "Open error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var invoiceName = string.IsNullOrWhiteSpace(Model.InvoiceNumber) ? "New" : SafeFileName(Model.InvoiceNumber);
            var dialog = new SaveFileDialog
            {
                Title = "Save Invoice PDF",
                Filter = "PDF invoice (*.pdf)|*.pdf",
                DefaultExt = ".pdf",
                AddExtension = true,
                OverwritePrompt = true,
                CheckPathExists = true,
                FileName = $"Invoice-{invoiceName}.pdf",
                InitialDirectory = ExportFolder()
            };
            if (dialog.ShowDialog(this) != true) return;

            var outputPath = Path.ChangeExtension(dialog.FileName, ".pdf");
            new InvoicePdf().GeneratePdf(outputPath, Model);

            if (MessageBox.Show($"PDF saved to:\n{outputPath}\n\nOpen it now?", "Export complete",
                    MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                Process.Start(new ProcessStartInfo(outputPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not export PDF:\n{ex.Message}", "Export error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void Print_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true) return;

            var pageWidth = 595d;
            var pageHeight = 842d;
            var scale = Math.Min(dialog.PrintableAreaWidth / pageWidth, dialog.PrintableAreaHeight / pageHeight);
            var left = (dialog.PrintableAreaWidth - pageWidth * scale) / 2;
            var top = (dialog.PrintableAreaHeight - pageHeight * scale) / 2;

            var pageVisual = new DrawingVisual();
            using (var drawing = pageVisual.RenderOpen())
            {
                drawing.PushTransform(new TranslateTransform(left, top));
                drawing.PushTransform(new ScaleTransform(scale, scale));
                drawing.DrawRectangle(new VisualBrush(InvoicePage), null, new Rect(0, 0, pageWidth, pageHeight));
                drawing.Pop();
                drawing.Pop();
            }
            dialog.PrintVisual(pageVisual, $"Invoice {Model.InvoiceNumber}".Trim());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not print the invoice:\n{ex.Message}", "Print error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    static string LocalInvoiceFolder()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NCR Atleos Invoice Studio");
        Directory.CreateDirectory(path); return path;
    }
    static string LocalExportsFolder()
    {
        var path = Path.Combine(LocalInvoiceFolder(), "Exports");
        Directory.CreateDirectory(path);
        return path;
    }
    static string ExportFolder()
    {
        const string downloadsValue = "{374DE290-123F-4565-9164-39C4925E467B}";
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders");
            var configured = key?.GetValue(downloadsValue) as string;
            var downloads = Environment.ExpandEnvironmentVariables(string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
                : configured);
            Directory.CreateDirectory(downloads);
            return IsWritable(downloads) ? downloads : LocalExportsFolder();
        }
        catch
        {
            return LocalExportsFolder();
        }
    }
    static bool IsWritable(string folder)
    {
        var probe = Path.Combine(folder, $".write-test-{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch { return false; }
        finally
        {
            try { if (File.Exists(probe)) File.Delete(probe); }
            catch { }
        }
    }
    static string SafeFileName(string value) => string.Join("", value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
