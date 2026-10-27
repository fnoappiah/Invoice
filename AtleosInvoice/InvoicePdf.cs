using System.Globalization;
using System.IO;
using System.Text;

namespace AtleosInvoice;

// Minimal PDF writer. Uses built-in PDF fonts and embeds the supplied JPEG logo without external packages.
public sealed class InvoicePdf
{
    const double W = 595.28, H = 841.89;
    readonly StringBuilder content = new();
    public void GeneratePdf(string path, InvoiceModel invoice)
    {
        content.Clear(); content.AppendLine("q 1 0 0 -1 0 841.89 cm");
        DrawInvoice(invoice);
        content.AppendLine("Q");
        var stream = Encoding.ASCII.GetBytes(content.ToString());
        using var logoStream = typeof(InvoicePdf).Assembly.GetManifestResourceStream("AtleosInvoice.Assets.Icon.jpeg")
            ?? throw new InvalidOperationException("The embedded invoice logo is missing.");
        using var logoBuffer = new MemoryStream();
        logoStream.CopyTo(logoBuffer);
        var logoBytes = logoBuffer.ToArray();
        using var output = new MemoryStream();
        var offsets = new List<long> { 0 };
        void Obj(int id, string body) { offsets.Add(output.Position); Write($"{id} 0 obj\n{body}\nendobj\n"); }
        void Write(string text) { var b = Encoding.ASCII.GetBytes(text); output.Write(b); }
        Write("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");
        Obj(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Obj(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Obj(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595.28 841.89] /Resources << /Font << /F1 4 0 R /F2 5 0 R /F3 6 0 R /F4 7 0 R >> /XObject << /Im1 9 0 R >> >> /Contents 8 0 R >>");
        Obj(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        Obj(5, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>");
        Obj(6, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Oblique >>");
        Obj(7, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-BoldOblique >>");
        offsets.Add(output.Position); Write($"8 0 obj\n<< /Length {stream.Length} >>\nstream\n"); output.Write(stream); Write("endstream\nendobj\n");
        offsets.Add(output.Position); Write($"9 0 obj\n<< /Type /XObject /Subtype /Image /Width 400 /Height 400 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {logoBytes.Length} >>\nstream\n");
        output.Write(logoBytes); Write("\nendstream\nendobj\n");
        var xref = output.Position; Write("xref\n0 10\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) Write(offset.ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \n");
        Write($"trailer\n<< /Size 10 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        File.WriteAllBytes(path, output.ToArray());
    }

    void DrawInvoice(InvoiceModel m)
    {
        const double left = 43, right = 552;
        var green = "0.27 0.36 0.32";
        Text(left + 24, 45, 14, m.BusinessName, "F4");
        var biz = (m.BusinessAddress ?? "").Split('\n');
        for (var i = 0; i < biz.Length; i++) Text(left + 24, 62 + i * 11, 8.5, biz[i], "F2");
        DrawLogo(429, 29, green);
        TextCentered(471, 83, 15, m.InvoiceNumber, "F2");
        TextCentered(471, 98, 7.5, "Division:  " + m.Division);
        Line(left, 126, right, 126, 1.4, "0.20 0.24 0.22");
        Text(265, 121, 12, "INVOICE", "F3", "0 0 0 rg", true);

        RoundRect(left, 137, 247, 58, 8, "0.28 0.32 0.30");
        Text(left + 8, 149, 8, "ATTN:", "F2");
        Text(left + 8, 162, 7.5, "Customer    " + m.ClientName);
        Text(left + 8, 174, 7.5, "Address    " + m.ClientAddress);
        Text(left + 8, 186, 7.5, "City       " + m.ClientCity + "     Phone  " + m.ClientPhone);
        RoundRect(298, 137, 254, 58, 8, "0.28 0.32 0.30");
        Text(308, 149, 7.5, "Date"); Text(376, 149, 7.5, m.InvoiceDateFormatted, "F2");
        Text(308, 162, 7.5, "Your Ref."); Text(376, 162, 7.5, m.YourReference);
        Text(308, 174, 7.5, "SSF NO."); Text(376, 174, 7.5, m.SsfNumber);
        Text(308, 186, 7.5, "FOB"); Text(376, 186, 7.5, m.Fob);

        const double x0 = 45, x1 = 87, x2 = 377, x3 = 457, x4 = 550;
        const double top = 207, headerBottom = 224, bodyBottom = 585;
        Rect(x0, top, x4 - x0, headerBottom - top, "0.94 0.95 0.94", "0.23 0.27 0.25", .65);
        Line(x0, top, x0, bodyBottom, .65, "0.23 0.27 0.25"); Line(x4, top, x4, bodyBottom, .65, "0.23 0.27 0.25");
        Line(x1, top, x1, bodyBottom, .65, "0.23 0.27 0.25"); Line(x2, top, x2, bodyBottom, .65, "0.23 0.27 0.25");
        Line(x3, top, x3, bodyBottom, .65, "0.23 0.27 0.25");
        Line(x0, bodyBottom, x4, bodyBottom, .65, "0.23 0.27 0.25");
        Text(x0 + 15, 219, 7.5, "Qty", "F2"); Text(x1 + 108, 219, 7.5, "Description", "F2");
        Text(x2 + 25, 219, 7.5, "Unit Price", "F2"); Text(x3 + 26, 219, 7.5, "TOTAL", "F2");
        Text(x1 + 5, 246, 10, m.InvoiceTitle, "F2");
        if (!string.IsNullOrWhiteSpace(m.InvoiceTitle))
            Line(x1 + 5, 249, x1 + 5 + Math.Min(270, m.InvoiceTitle.Length * 5.2), 249, .45, "0.20 0.24 0.22");
        var y = 283.0;
        foreach (var item in m.Items)
        {
            Text(x0 + 12, y, 9, item.Quantity?.ToString("0.##", CultureInfo.InvariantCulture), "F1", "0 0 0 rg", true);
            Text(x2 + 4, y, 8, Money(m, item.UnitPrice));
            Text(x3 + 4, y, 8, Money(m, item.Total), "F2");
            var lineY = y;
            var firstText = true;
            foreach (var paragraph in (item.Description ?? "").Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(paragraph)) { lineY += 40; continue; }
                foreach (var line in Wrap(paragraph, 47))
                {
                    Text(x1 + 5, lineY, 8, line, firstText ? "F2" : "F1");
                    firstText = false; lineY += 10;
                }
            }
            if (item.DiscountPercent > 0) Text(x1 + 5, Math.Max(y + 12, lineY + 2), 7, item.DiscountText, "F1", "0.27 0.36 0.32 rg");
            var descriptionHeight = Math.Max(10, lineY - y);
            y += Math.Max(30, descriptionHeight + (item.DiscountPercent > 0 ? 16 : 8));
        }

        Text(left + 8, 608, 8, "Payment Details", "F2");
        y = 621;
        Text(left + 17, y, 8, m.PaymentMethod); y += 11;
        Text(left + 8, 672, 8, "Due Date", "F2"); Text(left + 17, 684, 8, m.DueDate);
        var tx = 347.0;
        TotalRow(m, tx, 608, "SubTotal", m.Subtotal);
        TotalRow(m, tx, 623, $"GETFUND @ {m.GetFundRate:0.##}%", m.GetFundTax);
        TotalRow(m, tx, 638, $"NHIL @ {m.NhilRate:0.##}%", m.NhilTax);
        Rect(367, 650, 185, 1, "0.25 0.30 0.28");
        TotalRow(m, tx, 669, $"VAT @ {m.VatRate:0.##}%", m.VatTax);
        Rect(367, 675, 185, 17, "0.94 0.95 0.94", "0.25 0.30 0.28", .6);
        Text(450, 687, 10, "TOTAL", "F2", "0 0 0 rg", true); Text(462, 687, 8, m.CurrencyCode, "F2");
        Text(548, 687, 11, m.GrandTotal.ToString("N2", CultureInfo.InvariantCulture), "F2", "0 0 0 rg", true);

        Rect(397, 716, 155, 25, null, "0.55 0.57 0.56", .6);
        TextCentered(W / 2, 752, 6.5, $"TIN NO. {m.TinNumber} / VAT NO. {m.VatNumber}", "F4");
        Line(45, 770, 550, 770, 1.5, "0.20 0.24 0.22");
        Text(228, 786, 7, "EXPERIENCE A NEW WORLD OF INTERACTION", "F3", "0 0 0 rg", true);
    }

    void TotalRow(InvoiceModel invoice, double x, double y, string label, decimal amount)
    {
        Text(450, y, 9, label, "F2", "0 0 0 rg", true); Text(462, y, 7, invoice.CurrencyCode, "F1");
        Text(550, y, 9, amount.ToString("N2", CultureInfo.InvariantCulture), "F2", "0 0 0 rg", true);
    }
    static string Money(InvoiceModel invoice, decimal? amount)
        => amount.HasValue ? $"{invoice.CurrencyCode} {amount.Value.ToString("N2", CultureInfo.InvariantCulture)}" : "";
    void DrawLogo(double x, double y, string color)
    {
        content.AppendLine($"q 36 0 0 36 {x} {y} cm /Im1 Do Q");
        Text(x + 44, y + 14, 13, "NCR", "F2", color); Text(x + 44, y + 29, 13, "ATLEOS", "F2", color);
    }
    void Line(double x1, double y1, double x2, double y2, double width, string color)
        => content.AppendLine($"q {color} RG {width.ToString(CultureInfo.InvariantCulture)} w {x1:0.##} {y1:0.##} m {x2:0.##} {y2:0.##} l S Q");
    void Rect(double x, double y, double w, double h, string? fill = null, string stroke = "", double lineWidth = .6)
    {
        content.Append("q "); if (fill != null) content.Append(fill).Append(" rg ");
        if (stroke.Length > 0) content.Append(stroke).Append(" RG ").Append(lineWidth.ToString(CultureInfo.InvariantCulture)).Append(" w ");
        content.Append($"{x:0.##} {y:0.##} {w:0.##} {h:0.##} re ").AppendLine(fill != null && stroke.Length > 0 ? "B Q" : fill != null ? "f Q" : "S Q");
    }
    void RoundRect(double x, double y, double w, double h, double r, string stroke)
    {
        var k = 0.55228475 * r;
        content.AppendLine($"q {stroke} RG 1.35 w");
        content.AppendLine($"{x + r} {y} m {x + w - r} {y} l");
        content.AppendLine($"{x + w - r + k} {y} {x + w} {y + r - k} {x + w} {y + r} c");
        content.AppendLine($"{x + w} {y + h - r} l {x + w} {y + h - r + k} {x + w - r + k} {y + h} {x + w - r} {y + h} c");
        content.AppendLine($"{x + r} {y + h} l {x + r - k} {y + h} {x} {y + h - r + k} {x} {y + h - r} c");
        content.AppendLine($"{x} {y + r} l {x} {y + r - k} {x + r - k} {y} {x + r} {y} c h S Q");
    }
    void Text(double x, double y, double size, string? value, string font = "F1", string color = "0 0 0 rg", bool rightAligned = false)
    {
        var safe = Ascii(value ?? "");
        if (rightAligned) x -= safe.Length * size * .52;
        safe = safe.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        content.AppendLine($"BT /{font} {size.ToString(CultureInfo.InvariantCulture)} Tf {color} 1 0 0 -1 {x.ToString("0.##", CultureInfo.InvariantCulture)} {y.ToString("0.##", CultureInfo.InvariantCulture)} Tm ({safe}) Tj ET");
    }
    void TextCentered(double centerX, double y, double size, string? value, string font = "F1")
    {
        var safe = Ascii(value ?? "");
        Text(centerX - safe.Length * size * .26, y, size, safe, font);
    }
    static string Ascii(string value) => value.Replace("•", "*").Replace("–", "-").Replace("—", "-").Replace("’", "'").Replace("‘", "'").Replace("“", "\"").Replace("”", "\"").Replace("é", "e").Replace("É", "E").Where(c => c >= 32 && c <= 126).Aggregate(new StringBuilder(), (b, c) => b.Append(c)).ToString();
    static List<string> Wrap(string? value, int max) => (value ?? "").Split('\n').SelectMany(part =>
    {
        var words = part.Split(' ', StringSplitOptions.RemoveEmptyEntries); var result = new List<string>(); var line = "";
        foreach (var word in words) { if (line.Length > 0 && line.Length + word.Length + 1 > max) { result.Add(line); line = word; } else line = line.Length == 0 ? word : line + " " + word; }
        if (line.Length > 0) result.Add(line); return result;
    }).ToList();
}
