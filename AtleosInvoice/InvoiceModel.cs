using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AtleosInvoice;

public class InvoiceModel : NotifyBase
{
    string _businessName = "NCR ATLEOS GHANA LIMITED", _businessAddress = "P.O BOX 1010\nACCRA, GHANA  233-21228168/9";
    string _invoiceNumber = "", _division = "PS", _clientName = "", _invoiceTitle = "";
    string _currencyCode = "GHS";
    string _clientAddress = "", _clientCity = "", _clientPhone = "", _invoiceDate = "";
    string _yourReference = "", _ssfNumber = "", _fob = "", _paymentMethod = "Cheque";
    string _dueDate = "Within 15 days";
    decimal _getFundRate = 2.5m, _nhilRate = 2.5m, _vatRate = 15m;

    public string BusinessName { get => _businessName; set => Set(ref _businessName, value); }
    public string BusinessAddress { get => _businessAddress; set => Set(ref _businessAddress, value); }
    public string InvoiceNumber { get => _invoiceNumber; set => Set(ref _invoiceNumber, value); }
    public string Division { get => _division; set => Set(ref _division, value); }
    public string CurrencyCode
    {
        get => _currencyCode;
        set => Set(ref _currencyCode, string.Equals(value, "USD", StringComparison.OrdinalIgnoreCase) ? "USD" : "GHS");
    }
    public string InvoiceTitle { get => _invoiceTitle; set => Set(ref _invoiceTitle, value); }
    public string ClientName { get => _clientName; set => Set(ref _clientName, value); }
    public string ClientAddress { get => _clientAddress; set => Set(ref _clientAddress, value); }
    public string ClientCity { get => _clientCity; set => Set(ref _clientCity, value); }
    public string ClientPhone { get => _clientPhone; set => Set(ref _clientPhone, value); }
    public string InvoiceDate
    {
        get => _invoiceDate;
        set
        {
            if (Set(ref _invoiceDate, value))
            {
                OnPropertyChanged(nameof(InvoiceDatePickerValue));
                OnPropertyChanged(nameof(InvoiceDateFormatted));
            }
        }
    }
    [JsonIgnore]
    public DateTime? InvoiceDatePickerValue
    {
        get => TryParseInvoiceDate(_invoiceDate, out var date) ? date : null;
        set => InvoiceDate = value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
    }
    [JsonIgnore]
    public string InvoiceDateFormatted => TryParseInvoiceDate(_invoiceDate, out var date) ? FormatInvoiceDate(date) : _invoiceDate;
    public string YourReference { get => _yourReference; set => Set(ref _yourReference, value); }
    public string SsfNumber { get => _ssfNumber; set => Set(ref _ssfNumber, value); }
    public string Fob { get => _fob; set => Set(ref _fob, value); }
    public string PaymentMethod { get => _paymentMethod; set => Set(ref _paymentMethod, value); }
    public string DueDate { get => _dueDate; set => Set(ref _dueDate, value); }
    public string TinNumber => "C0002788233";
    public string VatNumber => "314E000013";
    public decimal GetFundRate { get => _getFundRate; set { if (Set(ref _getFundRate, value)) TotalsChanged(); } }
    public decimal NhilRate { get => _nhilRate; set { if (Set(ref _nhilRate, value)) TotalsChanged(); } }
    public decimal VatRate { get => _vatRate; set { if (Set(ref _vatRate, value)) TotalsChanged(); } }
    public ObservableCollection<InvoiceLine> Items { get; set; } = new();

    [JsonIgnore] public decimal Subtotal => Items.Sum(i => i.Total ?? 0m);
    [JsonIgnore] public decimal GetFundTax => decimal.Round(Subtotal * GetFundRate / 100, 2, MidpointRounding.AwayFromZero);
    [JsonIgnore] public decimal NhilTax => decimal.Round(Subtotal * NhilRate / 100, 2, MidpointRounding.AwayFromZero);
    [JsonIgnore] public decimal VatTax => decimal.Round(Subtotal * VatRate / 100, 2, MidpointRounding.AwayFromZero);
    [JsonIgnore] public decimal GrandTotal => decimal.Round(Subtotal * (1 + (GetFundRate + NhilRate + VatRate) / 100), 2, MidpointRounding.AwayFromZero);

    public InvoiceModel()
    {
        Items.CollectionChanged += (_, _) => { OnPropertyChanged(nameof(Items)); TotalsChanged(); };
    }

    public void Refresh()
    {
        Items.CollectionChanged += (_, _) => TotalsChanged();
        foreach (var item in Items) item.PropertyChanged += (_, _) => TotalsChanged();
        TotalsChanged();
    }
    void TotalsChanged()
    {
        OnPropertyChanged(nameof(Subtotal)); OnPropertyChanged(nameof(GetFundTax));
        OnPropertyChanged(nameof(NhilTax)); OnPropertyChanged(nameof(VatTax)); OnPropertyChanged(nameof(GrandTotal));
    }

    static bool TryParseInvoiceDate(string value, out DateTime date)
    {
        var parseableValue = Regex.Replace(value, @"(?<=\d)(st|nd|rd|th)\b", "", RegexOptions.IgnoreCase);
        return DateTime.TryParse(parseableValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
               || DateTime.TryParse(parseableValue, CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
    }

    static string FormatInvoiceDate(DateTime date)
    {
        var day = date.Day;
        var suffix = (day % 100 is 11 or 12 or 13) ? "th" : (day % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"{day}{suffix} {date.ToString("MMMM yyyy", CultureInfo.InvariantCulture)}";
    }
}

public class InvoiceLine : NotifyBase
{
    string _description = "";
    decimal? _quantity, _unitPrice, _discountPercent;
    public string Description { get => _description; set => Set(ref _description, value); }
    public decimal? Quantity { get => _quantity; set { if (Set(ref _quantity, value)) OnPropertyChanged(nameof(Total)); } }
    public decimal? UnitPrice { get => _unitPrice; set { if (Set(ref _unitPrice, value)) OnPropertyChanged(nameof(Total)); } }
    public decimal? DiscountPercent { get => _discountPercent; set { if (Set(ref _discountPercent, value)) { OnPropertyChanged(nameof(Total)); OnPropertyChanged(nameof(DiscountText)); } } }
    [JsonIgnore] public string DiscountText => DiscountPercent > 0 ? $"Discount: {DiscountPercent:0.##}%" : "";
    [JsonIgnore] public decimal? Total => Quantity.HasValue && UnitPrice.HasValue
        ? decimal.Round(Quantity.Value * UnitPrice.Value * (1 - (DiscountPercent ?? 0m) / 100), 2, MidpointRounding.AwayFromZero)
        : null;
}

public abstract class NotifyBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; OnPropertyChanged(name); return true;
    }
}
