# NCR Atleos Invoice Studio

A Windows desktop invoice editor built with WPF and .NET 10. It recreates the one-page portrait NCR Atleos Ghana invoice form from the supplied scan, with an editable form on the left and a live A4 preview on the right.

## Build and run

Requirements: Windows 10/11 and the .NET 10 SDK with the Windows Desktop runtime.

From PowerShell:

```powershell
cd .\AtleosInvoice
dotnet restore
dotnet run
```

Use **Save** to store an editable JSON invoice and **Open** to reopen one. The default folder is `%LOCALAPPDATA%\NCR Atleos Invoice Studio`; data stays on the computer. **Export PDF** opens a Save As dialog starting in Downloads, with the filename based on the invoice number. **Print** opens the Windows printer dialog and prints the invoice preview. Choose **GHS - Ghana Cedis** or **USD - US Dollars** from Currency; this changes the displayed currency code and does not convert amounts. The PDF writer uses built-in PDF fonts and an embedded logo without third-party packages.

## Included fields and calculations

- Business name, address and contact, division (PS, SSB, or WCS), invoice number, one customer name, customer address/city/phone, date, reference, SSF number, FOB, and an invoice title above the line items.
- Multiple line items with description, quantity, unit price, and optional line discount percentage.
- Subtotal after line discounts; editable GETFund, NHIL, and VAT rates; payment method (Cheque or Bank Transfer), due terms (within 15, 30, or 45 days), and fixed TIN/VAT IDs from the scan. TIN and VAT numbers are fixed to the values printed on the reference invoice.
- Tax and levy amounts are each computed from the discounted subtotal. The default rates match the form: GETFund 2.5%, NHIL 2.5%, and VAT 15%. Individual displayed tax amounts are rounded to cents; the grand total uses the unrounded percentage calculations before rounding, matching the sample invoice's printed total.

## Reference interpretation

- The input PDF is a photographed scan with page geometry 1983 × 2855 points. Its portrait ratio is close to A4; the output uses standard A4 (595.28 × 841.89 points) so it prints on normal paper without reproducing camera perspective, paper shadows, or binder holes.
- The top-right globe is the user-supplied `Assets/Icon.jpeg`; the NCR ATLEOS wordmark remains alongside it. Currency is selectable as GHS (Ghana cedis) or USD (US dollars).
- An empty outlined rectangle near the footer has no visible label, so it is preserved as an unlabeled stamp/signature area. TIN and VAT numbers are centered across the page below the outlined box.
- A discount is shown in the description area only when a nonzero discount is used, because the reference form has no discount column.

Only the business name and business address/contact are prefilled. Division defaults to PS, payment defaults to Cheque, and due terms default to Within 15 days. The TIN and VAT numbers are fixed to the values printed on the scan. Invoice title, customer details, invoice number, date, and line item inputs start blank. The three tax-rate defaults are retained from the printed template.

## Portable Windows build

Create a Release folder containing the app executable and its runtime files:

```powershell
dotnet publish -c Release -o publish\release
```

Copy the **entire** `publish\release` folder to a USB drive, then run `AtleosInvoice.exe` from that folder. Or extract the USB ZIP and double-click `AtleosInvoice.exe`. Keep the other files beside the EXE. This build uses the installed .NET 10 Windows Desktop Runtime; install that runtime on another PC if it is missing. The project has no third-party NuGet packages, uses no network connection, embeds its logo, and limits opened invoice JSON files to 5 MB and 32 levels of nesting. The executable is unsigned, so Windows may show a SmartScreen warning. Do not disable Windows protection to run software you do not trust.
