using Microsoft.OData.Client;
using OcerraOdoo.Models;
using OcerraOdoo.OcerraOData;
using OcerraOdoo.Properties;
using OdooRpc.CoreCLR.Client;
using OdooRpc.CoreCLR.Client.Models;
using OdooRpc.CoreCLR.Client.Models.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OcerraOdoo.Services
{
    public class ExportService
    {
        private readonly OcerraClient ocerraClient;
        private readonly OdooRpcClient odooClient;
        private OdataProxy odata;
        public bool Initialized { get; set; }
        private Regex searchForPo = new Regex(@"PO\d+");
        public ExportService(OcerraClient ocerraClient, OdooRpcClient odooClient, OdataProxy odata)
        {
            this.ocerraClient = ocerraClient;
            this.odooClient = odooClient;
            this.odata = odata;
        }

        private async Task Init()
        {
            if (Initialized) return;
            await odooClient.Authenticate();
            Initialized = true;
        }

        public async Task<ExportResult> ExportInvoices(DateTime lastSyncDate)
        {
            var result = new ExportResult();

            try
            {
                await Init();

                var settings = Helpers.AppSetting();

                var invoiceStatuses = settings.ExportStatuses.Split(',');

                foreach (var invoiceStatus in invoiceStatuses) {
                    var ocerraInvoices = odata.VoucherHeader
                        .Expand("VoucherLines($expand=TaxAccount,ItemCode,PurchaseOrderLine)")
                        .Expand(vh => vh.Vendor)
                        .Where(vh => !vh.IsArchived && vh.UpdatedDate > lastSyncDate && vh.Workflow.WorkflowState.Name == invoiceStatus)
                        .OrderByDescending(vh => vh.CreatedDate)
                        .Take(1) //Should take 100
                        .ToList();
                    
                    await ExportInvoicesFromListV8(ocerraInvoices, result);
                }
                
                result.Message = $"Invoices exported successfully: created {result.NewItems}, updated: {result.UpdatedItems}, skipped: {result.SkippedItems}";

                return result;
            }
            catch (Exception ex)
            {
                ex.LogError("Error on Bill Export");
                return new ExportResult
                {
                    NewItems = result.NewItems,
                    UpdatedItems = result.UpdatedItems,
                    Message = "There was an error on Bill export."
                };
            }
        }

        public async Task<ExportResult> ExportInvoicesByIds(Guid[] voucherHeaderIds)
        {
            var result = new ExportResult();

            try
            {
                await Init();

                foreach (var voucherHeaderId in voucherHeaderIds) {

                    var query = odata.VoucherHeader
                        .Expand("Vendor,PurchaseOrderHeader,VoucherLines($expand=TaxAccount,ItemCode,PurchaseOrderLine)");
                    query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)query
                        .AddQueryOption("$filter", $"VoucherHeaderId eq {voucherHeaderId}"); // .Where(vh => vh.VoucherHeaderId == voucherHeaderId);
                    //Export invoices one by one
                    var ocerraInvoices = query.Execute().ToList();

                    if(ocerraInvoices.Any())
                        await ExportInvoicesFromListV8(ocerraInvoices, result);
                    else
                    {
                        throw new Exception($"This invoice {voucherHeaderId} is not found");
                    }
                }
                

                result.Message = $"Invoices exported successfully: created {result.NewItems}, updated: {result.UpdatedItems}, skipped: {result.SkippedItems}";

                return result;
            }
            catch (Exception ex)
            {
                ex.LogError("Error on Bill Export");
                return new ExportResult
                {
                    NewItems = result.NewItems,
                    UpdatedItems = result.UpdatedItems,
                    Message = "There was an error on Bill export. " + ex.Message
                };
            }
        }

        private async Task ExportInvoicesFromListCloud(List<ODataClient.Proxies.VoucherHeader> ocerraInvoices, ExportResult result) {

            
            if (ocerraInvoices.HasItems())
            {

                var odooJournalIds = await odooClient.Search<long[]>(new OdooSearchParameters("account.journal",
                    new OdooDomainFilter().Filter("code", "=", Settings.Default.OdooPurchasesJournal)),                        
                    new OdooPaginationParameters(0, 5));

                if (!odooJournalIds.HasItems())
                    throw new Exception($"Active journal of Code eq {Settings.Default.OdooPurchasesJournal} is not found in Odoo");

                
                var odooCurrencyIds = await odooClient.Search<long[]>(new OdooSearchParameters("res.currency",
                    new OdooDomainFilter().Filter("active", "=", true)), new OdooPaginationParameters(0, 100));

                var odooCurrencies = odooCurrencyIds.HasItems() ?
                    await odooClient.Get<OdooCurrency[]>(new OdooGetParameters("res.currency", odooCurrencyIds),
                        new OdooFieldParameters(new[] { "id", "name" })) : null;

                var payableAccountCodes = Settings.Default.OdooPayableAccount.Split(',');
                var taxAccountCodes = Settings.Default.OdooTaxAccount.Split(',');
                var expenseAccountCodes = Settings.Default.OdooExpenseAccount.Split(',');

                var payableAccountIds = await odooClient.Search<long[]>(new OdooSearchParameters("account.account",
                    new OdooDomainFilter().Filter("code", "=", payableAccountCodes[0])),
                    new OdooPaginationParameters(0, 1));

                var taxAccountIds = await odooClient.Search<long[]>(new OdooSearchParameters("account.account",
                    new OdooDomainFilter().Filter("code", "=", taxAccountCodes[0])),
                    new OdooPaginationParameters(0, 1));

                var expenseAccountIds = expenseAccountCodes[0] != "0" ? await odooClient.Search<long[]>(new OdooSearchParameters("account.account",
                    new OdooDomainFilter().Filter("code", "=", expenseAccountCodes[0])),
                    new OdooPaginationParameters(0, 5)) : null;

                var odooBillIds = ocerraInvoices
                    .Where(i => i.ExternalId != null)
                    .Select(i => i.ExternalId.ToLong(0))
                    .Where(i => i > 0)
                    .ToArray();

                var odooBills = odooBillIds.HasItems() ?
                        await odooClient.Get<OdooBill[]>(new OdooGetParameters("account.move", odooBillIds), new OdooFieldParameters()) : new OdooBill[] { };


                var defaultOdooExpenseAccountId = expenseAccountIds.HasItems() ? expenseAccountIds[0].ToString() : null;
                var defaultExpenseAccount = defaultOdooExpenseAccountId != null ? odata.TaxAccount.Where(ta => ta.ExternalId == defaultOdooExpenseAccountId).FirstOrDefault() : null;
                var ocerraTaxRates = odata.TaxRate.ToList();
                var ocerraCurrencies = odata.CurrencyCode.ToList();


                foreach (var invoice in ocerraInvoices)
                {
                    //Skip invoices without the vendor
                    if (invoice.VendorId == null) continue;

                    var odooBill = invoice.ExternalId != null ? odooBills.FirstOrDefault(b => b.Id == invoice.ExternalId.ToLong(0)) : null;

                    var newBillLines = new List<OdooBillLine>();

                    var currencyCode = ocerraCurrencies.First(cc => cc.CurrencyCodeId == invoice.CurrencyCodeId);

                    ODataClient.Proxies.TaxRate taxRate = null;

                    //Accounts Payable Line
                    newBillLines.Add(new OdooBillLine
                    {
                        MoveName = invoice.Number,
                        Name = "Payable",
                        AccountId = new OdooKeyValue(payableAccountIds[0]),
                        AccountInternalType = payableAccountCodes[1],
                        Date = invoice.CreatedDate.Date,
                        Ref = invoice.Number,
                        JournalId = new OdooKeyValue(odooJournalIds.First()),
                        CompanyCurrencyId = new OdooKeyValue(currencyCode.ExternalId.ToLong(0)),
                        PartnerId = new OdooKeyValue(invoice.Vendor.ExternalId.ToLong(0)),
                        Quantity = 1,
                        PriceUnit = -1 * (decimal?)invoice.FcGross,
                        Debit = 0,
                        Credit = (decimal?)invoice.FcGross,
                        AmountCurrency = 0,
                        PriceSubtotal = -1 * (decimal?)invoice.FcGross,
                        PriceTotal = -1 * (decimal?)invoice.FcGross,
                        Balance = -1 * (decimal?)invoice.FcGross,
                        ExcludeFromInvoiceTab = true,
                        TaxIds = new List<long>(),
                    });

                    foreach (var voucherLine in invoice.VoucherLines)
                    {

                        if ((voucherLine.FcNet ?? 0) == 0 || voucherLine.FcNet > 100000) continue;

                        var taxAccount = voucherLine.TaxAccount;
                        taxAccount = taxAccount ?? defaultExpenseAccount;
                        taxRate = taxRate ?? ocerraTaxRates.FirstOrDefault(ta => ta.TaxRateId == (voucherLine.TaxRateId ?? taxAccount?.TaxRateId));

                        //Skip non coded lines
                        if (taxAccount == null && expenseAccountIds == null) continue;
                        if (taxRate == null && expenseAccountIds == null) continue;


                        //Net Amount Line
                        newBillLines.Add(new OdooBillLine
                        {
                            Name = voucherLine.Description,
                            MoveName = invoice.Number,
                            AccountId = new OdooKeyValue( taxAccount != null ?
                                taxAccount.ExternalId.ToLong(0) : expenseAccountIds[0]),
                            AccountInternalType = expenseAccountCodes[1],
                            Date = invoice.CreatedDate.Date,
                            Ref = invoice.Number,
                            JournalId = new OdooKeyValue(odooJournalIds.First()),
                            CompanyCurrencyId = new OdooKeyValue(currencyCode.ExternalId.ToLong(0)),
                            Quantity = (decimal?)voucherLine.Quantity ?? 1,
                            Discount = 0,
                            Credit = 0,
                            AmountCurrency = 0,
                            Debit = (decimal?)voucherLine.FcNet,
                            Balance = (decimal?)voucherLine.FcNet,
                            PriceSubtotal = (decimal?)voucherLine.FcNet,
                            PriceTotal = (decimal?)voucherLine.FcGross,
                            PartnerId = new OdooKeyValue(invoice.Vendor.ExternalId.ToLong(0)),
                            TaxIds = new List<long>() { taxRate.ExternalId.ToLong(0) },
                            ExcludeFromInvoiceTab = false
                        });
                    }

                    //Tax Amount Line
                    if ((invoice.FcTax ?? 0) != 0)
                    {
                        newBillLines.Insert(1, new OdooBillLine
                        {
                            Name = "Purchase Tax",
                            MoveName = invoice.Number,
                            AccountId = new OdooKeyValue(taxAccountIds[0]),
                            AccountInternalType = taxAccountCodes[1],
                            Date = invoice.CreatedDate.Date,
                            Ref = invoice.Number,
                            JournalId = new OdooKeyValue(odooJournalIds.First()),
                            CompanyCurrencyId = new OdooKeyValue(currencyCode.ExternalId.ToLong(0)),
                            Quantity = 1,
                            PriceUnit = (decimal?)invoice.FcTax,
                            Discount = 0,
                            Credit = 0,
                            Debit = (decimal?)invoice.FcTax,
                            Balance = (decimal?)invoice.FcTax,
                            AmountCurrency = 0,
                            PriceSubtotal = (decimal?)invoice.FcTax,
                            PartnerId = new OdooKeyValue(invoice.Vendor.ExternalId.ToLong(0)),
                            PriceTotal = (decimal?)invoice.FcTax,
                            TaxLineId = new OdooKeyValue(taxRate.ExternalId.ToLong(0)),
                            TaxIds = new List<long>(),
                            ExcludeFromInvoiceTab = true
                        });
                    }

                    if (odooBill == null)
                    {
                        odooBill = new OdooBill
                        {
                            Name = invoice.Number,
                            PartnerId = new OdooKeyValue(invoice.Vendor.ExternalId.ToLong(0)),
                            CommercialPartnerId = new OdooKeyValue(invoice.Vendor.ExternalId.ToLong(0)),
                            CompanyCurrencyId = new OdooKeyValue { Key = currencyCode.ExternalId.ToLong(0) },

                            AmountUntaxed = (decimal?)invoice.FcNet ?? 0,
                            AmountTax = (decimal?)invoice.FcTax ?? 0,
                            AmountTotal = (decimal?)invoice.FcGross ?? 0,
                            AmountResidual = (decimal?)invoice.FcGross ?? 0,

                            AmountUntaxedSigned = (decimal?)invoice.FcNet ?? 0,
                            AmountTaxSigned = (decimal?)invoice.FcTax ?? 0,
                            AmountTotalSigned = (decimal?)invoice.FcGross ?? 0,
                            AmountResidualSigned = (decimal?)invoice.FcGross ?? 0,

                            InvoiceDate = (invoice.Date ?? invoice.CreatedDate).DateTime,
                            InvoiceDateDue = (invoice.DueDate ?? invoice.CreatedDate.AddDays(30)).DateTime,
                            Type = "in_invoice",
                            Ref = invoice.Number,
                            Date = DateTime.Now.Date,
                            AutoPost = false,
                            State = "draft",
                            InvoiceOrigin = invoice.PurchaseOrderNumber,
                            JournalId = new OdooKeyValue(odooJournalIds.First()),
                            InvoicePaymentState = "not_paid",
                            LineIds = new OdooArray<OdooBillLine>() { Objects = newBillLines }
                        };

                        odooBill.Id = await odooClient.Create("account.move", odooBill);

                        var voucherHeader = await ocerraClient.ApiVoucherHeaderByIdGetAsync(invoice.VoucherHeaderId.Value);
                        voucherHeader.ExternalId = odooBill.Id.ToString();
                        await ocerraClient.ApiVoucherHeaderByIdPutAsync(voucherHeader.VoucherHeaderId, voucherHeader);

                        result.NewItems++;
                    }
                    else
                    {
                        odooBill.Ref = invoice.Number;
                        odooBill.Date = DateTime.Now.Date;

                        odooBill.Name = invoice.Number;
                        odooBill.PartnerId = new OdooKeyValue(invoice.Vendor.ExternalId.ToLong(0));
                        odooBill.CommercialPartnerId = new OdooKeyValue(invoice.Vendor.ExternalId.ToLong(0));
                        odooBill.CurrencyId = new OdooKeyValue { Key = currencyCode.ExternalId.ToLong(0) };
                        odooBill.CompanyCurrencyId = new OdooKeyValue { Key = currencyCode.ExternalId.ToLong(0) };

                        odooBill.AmountUntaxed = (decimal?)invoice.FcNet ?? 0;
                        odooBill.AmountTax = (decimal?)invoice.FcTax ?? 0;
                        odooBill.AmountTotal = (decimal?)invoice.FcGross ?? 0;
                        odooBill.AmountResidual = (decimal?)invoice.FcGross ?? 0;

                        odooBill.AmountUntaxedSigned = (decimal?)invoice.FcNet ?? 0;
                        odooBill.AmountTaxSigned = (decimal?)invoice.FcTax ?? 0;
                        odooBill.AmountTotalSigned = (decimal?)invoice.FcGross ?? 0;
                        odooBill.AmountResidualSigned = (decimal?)invoice.FcGross ?? 0;

                        odooBill.InvoiceDate = (invoice.Date ?? invoice.CreatedDate).DateTime;
                        odooBill.InvoiceDateDue = (invoice.DueDate ?? invoice.CreatedDate.AddDays(30)).DateTime;
                        odooBill.Type = "in_invoice";
                        odooBill.InvoiceOrigin = invoice.PurchaseOrderNumber;

                        /*odooBill.AutoPost = false;
                        odooBill.State = "draft";
                        odooBill.JournalId = new OdooKeyValue(odooJournalIds.First());
                        odooBill.InvoicePaymentState = "not_paid";*/

                        odooBill.InvoiceLineIds = null;

                        //Remove old lines
                        odooBill.LineIds = new OdooArray<OdooBillLine>();
                        await odooClient.Update("account.move", odooBill.Id, odooBill);

                        //Create new lines
                        odooBill.LineIds = new OdooArray<OdooBillLine>()
                        {
                            Objects = newBillLines
                        };
                        await odooClient.Update("account.move", odooBill.Id, odooBill);

                        result.UpdatedItems++;
                    }
                }
            }
        }

        private async Task ExportInvoicesFromListV8(List<ODataClient.Proxies.VoucherHeader> ocerraInvoices, ExportResult result)
        {
            if (ocerraInvoices.HasItems())
            {

                var settings = Helpers.AppSetting();

                var odooJournalIds = await odooClient.Search<long[]>(new OdooSearchParameters("account.journal",
                    new OdooDomainFilter().Filter("code", "=", settings.OdooPurchasesJournal)),
                    new OdooPaginationParameters(0, 5));

                if (!odooJournalIds.HasItems())
                    throw new Exception($"Active journal of Code eq {Settings.Default.OdooPurchasesJournal} is not found in Odoo");


                var odooCurrencyIds = await odooClient.Search<long[]>(new OdooSearchParameters("res.currency",
                    new OdooDomainFilter().Filter("active", "=", true)), new OdooPaginationParameters(0, 100));

                var odooCurrencies = odooCurrencyIds.HasItems() ?
                    await odooClient.Get<OdooCurrency[]>(new OdooGetParameters("res.currency", odooCurrencyIds),
                        new OdooFieldParameters(new[] { "id", "name" })) : null;

                var payableAccountCodes = settings.OdooPayableAccount.Split(',');
                var taxAccountCodes = settings.OdooTaxAccount.Split(',');
                var expenseAccountCodes = settings.OdooExpenseAccount.Split(',');

                var payableAccountIds = await odooClient.Search<long[]>(new OdooSearchParameters("account.account",
                    new OdooDomainFilter().Filter("code", "=", payableAccountCodes[0])),
                    new OdooPaginationParameters(0, 1));

                var taxAccountIds = await odooClient.Search<long[]>(new OdooSearchParameters("account.account",
                    new OdooDomainFilter().Filter("code", "=", taxAccountCodes[0])),
                    new OdooPaginationParameters(0, 1));

                var expenseAccountIds = expenseAccountCodes[0] != "0" ? await odooClient.Search<long[]>(new OdooSearchParameters("account.account",
                    new OdooDomainFilter().Filter("code", "=", expenseAccountCodes[0])),
                    new OdooPaginationParameters(0, 5)) : null;

                var odooBillIds = ocerraInvoices
                    .Where(i => i.ExternalId != null || i.Extra1 != null)
                    .Select(i => i.ExternalId?.ToLong(0) ?? i.Extra1?.ToLong(0) ?? 0)
                    .Where(i => i > 0)
                    .ToList();



                //Detect duplicates by PO Number
                if (settings.UseDraftInvoicesByPoBool)
                {
                    var odooPoNumbers = ocerraInvoices.Where(oi => oi.PurchaseOrderHeader != null)
                        .Select(oi => new { SupplierId = oi.Vendor.ExternalId, oi.PurchaseOrderHeader.Number })
                        .ToList();

                    var odooBillsByPoNo = new List<OdooBill>();
                    foreach (var odooPoNumber in odooPoNumbers)
                    {
                        var odooBillByPoNo = await odooClient.Search<long[]>(new OdooSearchParameters("account.invoice",
                            new OdooDomainFilter()
                                .Filter("type", "=", "in_invoice")
                                .Filter("origin", "ilike", odooPoNumber.Number)
                                .Filter("partner_id", "=", odooPoNumber.SupplierId.ToLong(0))
                                ), new OdooPaginationParameters(0, 100));
                        
                        if (odooBillByPoNo.HasItems())
                            odooBillIds.AddRange(odooBillByPoNo);
                    }
                }


                //Detect duplicates by Invoice number
                var odooDuplicateNumbers = ocerraInvoices
                        .Select(oi => new { SupplierId = oi.Vendor.ExternalId, oi.Number })
                        .ToList();

                foreach (var odooDuplicateNumber in odooDuplicateNumbers)
                {
                    var odooBillByNo = await odooClient.Search<long[]>(new OdooSearchParameters("account.invoice",
                        new OdooDomainFilter()
                            .Filter("type", "=", "in_invoice")
                            .Filter("supplier_invoice_number", "ilike", odooDuplicateNumber.Number)
                            .Filter("partner_id", "=", odooDuplicateNumber.SupplierId.ToLong(0))
                            ), new OdooPaginationParameters(0, 100));

                    if (odooBillByNo.HasItems())
                        odooBillIds.AddRange(odooBillByNo);
                }



                var odooBills = new List<OdooBill>();
                
                var odooBillsById = odooBillIds.HasItems() ?
                        await odooClient.Get<OdooBill[]>(new OdooGetParameters("account.invoice", odooBillIds.Distinct().ToArray()),
                            new OdooFieldParameters()) : new OdooBill[] { };
                
                if(odooBillsById.HasItems())
                    odooBills.AddRange(odooBillsById);

                
                var defaultOdooExpenseAccountId = expenseAccountIds.HasItems() ? expenseAccountIds[0].ToString() : null;
                var defaultExpenseAccount = defaultOdooExpenseAccountId != null ? odata.TaxAccount.Where(ta => ta.ExternalId == defaultOdooExpenseAccountId).FirstOrDefault() : null;
                var ocerraTaxRates = odata.TaxRate.ToList();
                var ocerraCurrencies = odata.CurrencyCode.ToList();


                foreach (var invoice in ocerraInvoices)
                {
                    //Skip invoices without the vendor
                    if (invoice.VendorId == null) continue;

                    var byExternalId = invoice.ExternalId != null ? 
                        odooBills.FirstOrDefault(b => b.Id == invoice.ExternalId.ToLong(0)) : null;

                    var byExtra1 = invoice.Extra1 != null ?
                        odooBills.FirstOrDefault(b => b.Id == invoice.Extra1.ToLong(0)) : null;

                    var byNumber = invoice.Number != null ?
                        odooBills.FirstOrDefault(b =>
                            (b.SupplierInvoiceNumberV8?.Value?.Contains(invoice.Number) ?? false) &&
                            b.PartnerId?.Key == invoice.Vendor.ExternalId.ToLong(0)) : null;

                    var byOriginDraft = invoice.PurchaseOrderHeader?.Number != null ?
                        odooBills.FirstOrDefault(b =>
                            (b.State == "draft") &&
                            (b.Origin?.Value?.Contains(invoice.PurchaseOrderHeader.Number) ?? false) &&
                            b.PartnerId?.Key == invoice.Vendor.ExternalId.ToLong(0)) : null;

                    var byOrigin = invoice.PurchaseOrderHeader?.Number != null ?
                        odooBills.FirstOrDefault(b =>
                            (b.Origin?.Value?.Contains(invoice.PurchaseOrderHeader.Number) ?? false) &&
                            b.PartnerId?.Key == invoice.Vendor.ExternalId.ToLong(0)) : null;

                    //Find bill by ExternalId or by Origin and supplier for automatically created invoices.
                    var odooBill = byExternalId ?? byExtra1 ?? byNumber ?? byOriginDraft ?? byOrigin;

                    //Do not create invoices with JobPO, wait for draft invoice
                    if ((invoice.PurchaseOrderHeader?.Number?.Contains("JSPO") ?? false) && (odooBill == null || odooBill?.State != "draft"))
                    {
                        result.Message = $"Invoice {invoice.Number} was not exported because draft invoicew is not found";
                        result.SkippedItems++;
                        continue;
                    }

                    var purchaseAttributes = invoice.PurchaseOrderHeader?.Attributes.FromJson<OdooPurchaseOrderDetails>();

                    var existingBillLines = odooBill != null && odooBill.InvoiceLineIdsV8?.Ids != null ? 
                        await odooClient.Get<OdooBillLineV8[]>(new OdooGetParameters("account.invoice.line", odooBill.InvoiceLineIdsV8.Ids), new OdooFieldParameters()) : null;

                    
                    var billLines = new List<OdooBillLineV8>();
                    var taxLines = new List<OdooTaxLine>();

                    var currencyCode = ocerraCurrencies.First(cc => cc.CurrencyCodeId == invoice.CurrencyCodeId);

                    ODataClient.Proxies.TaxRate taxRate = null;
                    ODataClient.Proxies.TaxAccount firstTaxAccount = null;
                    
                    long? currentTaxRateId = invoice?.Vendor?.DefaultTaxRateId != null ? 
                        //use the default tax rate from the vendor where possible
                        (long?)ocerraTaxRates.Find(tr => tr.TaxRateId == invoice?.Vendor?.DefaultTaxRateId)?.ExternalId.ToLong(-1)
                            : null;

                    long? currentDivisionId = null;
                    long? currentBrandId = null;
                    long? currentStageId = null;

                    foreach (var voucherLine in invoice.VoucherLines.OrderBy(vl => vl.Sequence))
                    {
                        if ((voucherLine.FcNet ?? 0) == 0 || voucherLine.FcNet > 100000) continue;

                        var taxAccount = voucherLine.TaxAccount;
                        taxAccount = taxAccount ?? defaultExpenseAccount;
                        firstTaxAccount = firstTaxAccount ?? taxAccount;

                        taxRate = taxRate ?? ocerraTaxRates.FirstOrDefault(ta => ta.TaxRateId == (voucherLine.TaxRateId ?? taxAccount?.TaxRateId));

                        if((invoice.FcTax ?? 0) == 0 || currencyCode?.Code != "NZD") //this is PNT Tax
                            taxRate = ocerraTaxRates.FirstOrDefault(ta => ta.Code == "PNT" || ta.Description == "PNT");                        

                        var lineAttributes = voucherLine.PurchaseOrderLine?.Attributes?.FromJson<OdooPurchaseOrderLineAttributes>();
                        
                        var odooTaxRateId = lineAttributes?.TaxId ?? taxRate.ExternalId.ToLong(0);
                        currentTaxRateId = currentTaxRateId ?? odooTaxRateId;

                        //Skip non coded lines
                        if (taxAccount == null && expenseAccountIds == null) continue;
                        if (taxRate == null && expenseAccountIds == null) continue;

                        var sequence = 1000 + voucherLine.Sequence;

                        var lineQuantity = (decimal?)voucherLine.Quantity ?? 1;
                        if (settings.UsePurchaseOrderQuantityBool) {
                            lineQuantity = voucherLine.PurchaseOrderLine?.Quantity ?? lineQuantity;
                        }

                        //rememeber the brand from previous line
                        currentDivisionId = lineAttributes?.DivisionId ?? currentDivisionId;
                        currentBrandId = lineAttributes?.BrandId ?? currentBrandId;
                        currentStageId = lineAttributes?.StageId ?? currentStageId;

                        var calcQuantity = voucherLine.Quantity > 0 ? voucherLine.Quantity : 1;
                        var productId = voucherLine.ItemCode != null ?
                                new OdooKeyValue(voucherLine.ItemCode.ExternalId.ToLong(0)) : null;
                        var priceUnit = Math.Round((decimal)((voucherLine.FcNet ?? 0) / calcQuantity), 2);

                        var existingBillLineId =
                                existingBillLines?.FirstOrDefault(el => el.Sequence == sequence)?.Id ??
                                existingBillLines?.FirstOrDefault(el => productId != null && el.ProductId?.Key == productId.Key 
                                    && el.Quantity == voucherLine.Quantity && el.PriceUnit == priceUnit)?.Id ??
                                existingBillLines?.FirstOrDefault(el => productId != null && el.ProductId?.Key == productId.Key)?.Id ??
                                existingBillLines?.FirstOrDefault(el => el.Quantity == voucherLine.Quantity && el.PriceUnit == priceUnit)?.Id;

                        //Net Amount Line
                        billLines.Add(new OdooBillLineV8
                        {
                            Id = existingBillLineId ?? 0,
                            ProductId = productId,
                            Name = voucherLine.PurchaseOrderLine?.Description ?? voucherLine.Description,
                            AccountId = new OdooKeyValue(taxAccount != null ?
                                taxAccount.ExternalId.ToLong(0) : expenseAccountIds[0]),
                            Quantity = lineQuantity,
                            PriceUnit = priceUnit,
                            Discount = 0,
                            PriceSubtotal = (decimal?)voucherLine.FcNet,
                            AmountUntaxed = (decimal?)voucherLine.FcNet,
                            Sequence = sequence,
                            TaxLineIdsV8 = invoice.FcTax > 0 || odooTaxRateId > 0 ? new OdooKeyValue(odooTaxRateId) : null,
                            DevisionId = currentDivisionId != null ? new OdooKeyValue(currentDivisionId.Value) : null,
                            BrandId = currentBrandId != null ? new OdooKeyValue(currentBrandId.Value) : null,
                            JobServiceStageId = currentStageId != null ? new OdooKeyValue(currentStageId.Value) : null
                        });
                    }

                    //Add other line
                    if (invoice.FcOther > 0 && (firstTaxAccount != null || expenseAccountIds != null)) 
                    {
                        billLines.Add(new OdooBillLineV8
                        {
                            Id = existingBillLines?.FirstOrDefault(el => el.Sequence == 9999)?.Id ?? 0,
                            Name = "Other",
                            AccountId = new OdooKeyValue(firstTaxAccount != null ?
                                firstTaxAccount.ExternalId.ToLong(0) : expenseAccountIds[0]),
                            Quantity = 1,
                            PriceUnit = invoice.FcOther,
                            Discount = 0,
                            Sequence = 9999,
                            TaxLineIdsV8 = invoice.FcTax > 0 || currentTaxRateId > 0 ? new OdooKeyValue(currentTaxRateId.Value) : null,
                            DevisionId = currentDivisionId != null ? new OdooKeyValue(currentDivisionId.Value) : null,
                            BrandId = currentBrandId != null ? new OdooKeyValue(currentBrandId.Value) : null,
                        });
                    }

                    //remove other lines
                    var linesForRemove = existingBillLines.HasItems() ? existingBillLines.Where(l => billLines.All(bl => bl.Id != l.Id)).ToList() : null;
                    if(linesForRemove.HasItems())
                        linesForRemove.ForEach((bl) => billLines.Add(new OdooBillLineV8 { 
                            Id = bl.Id * -1 //make line id negative
                        }));

                    //Tax Amount Line
                    if ((invoice.FcTax ?? 0) != 0)
                    {
                        taxLines.Add(new OdooTaxLine
                        {
                            //Id = odooBill?.TaxLinesV8?.Ids?.FirstOrDefault() ?? 0,
                            Name = "Purchase Tax",
                            AccountId = new OdooKeyValue(taxAccountIds[0]),
                            BaseAmount = invoice.FcNet,
                            Base = invoice.FcNet,
                            TaxAmount = invoice.FcTax,
                            Amount = invoice.FcTax,
                            FactorBase = 1,
                            FactorTax = 1
                        });
                    }

                    var invoiceUrl = $"https://app.ocerra.com/#/v/{invoice.DocumentId.Value.ToString("N")}";

                    if (odooBill == null)
                    {
                        odooBill = new OdooBill
                        {
                            AccountIdV8 = new OdooKeyValue(payableAccountIds[0]),
                            SupplierInvoiceNumberV8 = invoice.Number,
                            DateInvoiceV8 = (invoice.Date ?? invoice.CreatedDate).DateTime,
                            DueDateV8 = (invoice.DueDate ?? invoice.CreatedDate.AddDays(30)).DateTime,
                            InvoiceLineIdsV8 = new OdooArray<OdooBillLineV8>() { 
                                Objects = billLines 
                            },
                            //Calculate Taxes in Odoo
                            TaxLinesV8 = new OdooArray<OdooTaxLine> { Objects = new List<OdooTaxLine>() }, // new OdooArray<OdooTaxLine> { Objects = taxLines },
                            AmountTotalV8 = new OdooDecimal(invoice.FcGross),
                            Origin = invoice.PurchaseOrderHeader != null ? string.Join(" | ", invoice.PurchaseOrderHeader?.Number, invoiceUrl) : invoiceUrl,

                            Name = invoice.Number,
                            PartnerId = new OdooKeyValue(invoice.Vendor.ExternalId.ToLong(0)),
                            CommercialPartnerId = new OdooKeyValue(invoice.Vendor.ExternalId.ToLong(0)),
                            CurrencyId = new OdooKeyValue { Key = currencyCode.ExternalId.ToLong(0) },

                            AmountUntaxed = (decimal?)invoice.FcNet ?? 0,
                            AmountTax = (decimal?)invoice.FcTax ?? 0,
                            AmountTotal = (decimal?)invoice.FcGross ?? 0,
                            AmountResidual = (decimal?)invoice.FcGross ?? 0,
                            Residual = (decimal?)invoice.FcGross ?? 0,

                            AmountUntaxedSigned = (decimal?)invoice.FcNet ?? 0,
                            AmountTaxSigned = (decimal?)invoice.FcTax ?? 0,
                            AmountTotalSigned = (decimal?)invoice.FcGross ?? 0,
                            AmountResidualSigned = (decimal?)invoice.FcGross ?? 0,

                            InvoiceDate = (invoice.Date ?? invoice.CreatedDate).DateTime,
                            InvoiceDateDue = (invoice.DueDate ?? invoice.CreatedDate.AddDays(30)).DateTime,
                            Type = "in_invoice",
                            Ref = invoice.Number,
                            Date = DateTime.Now.Date,
                            AutoPost = false,
                            State = settings.OdooInvoiceState,
                            InvoiceOrigin = invoice.PurchaseOrderNumber,
                            JournalId = new OdooKeyValue(odooJournalIds.First()),
                            InvoicePaymentState = "not_paid",

                            JobServiceId = purchaseAttributes?.JobServiceId != null ? new OdooKeyValue(purchaseAttributes.JobServiceId.Value) : null
                        };

                        try
                        {
                            odooBill.Id = await odooClient.Create("account.invoice", new
                            {
                                partner_id = odooBill.PartnerId,
                                fiscal_position = 1,
                                job_service_id = odooBill.JobServiceId,
                                on_hold = false,
                                origin = odooBill.Origin,
                                supplier_invoice_number = odooBill.SupplierInvoiceNumberV8,
                                reference = odooBill.Ref,
                                date_invoice = odooBill.DateInvoiceV8,
                                date_due = odooBill.DueDateV8,
                                account_id = odooBill.AccountIdV8,
                                journal_id = odooBill.JournalId,
                                currency_id = odooBill.CurrencyId,
                                exchange_rate = 0,
                                check_total = odooBill.AmountTotalV8,
                                invoice_line = billLines.Select(bl =>
                                //add new lines
                                new List<object> {
                                    0, false, new {
                                        product_id = bl.ProductId,
                                        name = bl.Name,
                                        account_id = bl.AccountId,
                                        account_analytic_id = false,
                                        attribute1 = bl.DevisionId,
                                        attribute2 = bl.BrandId,
                                        attribute3 = false,
                                        quantity = bl.Quantity,
                                        uos_id = false,
                                        price_unit = bl.PriceUnit,
                                        discount = 0,
                                        invoice_line_tax_id = new List<object> {
                                            new List<object> {
                                                6, false, new List<object> { bl.TaxLineIdsV8 }
                                            }
                                        },
                                        job_service_stage_id = false,
                                        type = "normal",
                                        sequence = bl.Sequence
                                    }
                                }),
                                tax_line = new List<object>(),
                                comment = false,
                                partner_bank_id = false,
                                //user_id = 347,
                                name = odooBill.SupplierInvoiceNumberV8,
                                payment_term = false,
                                period_id = false,
                                company_id = 3,
                                payment_lines = new List<object>(),
                                message_follower_ids = false,
                                message_ids = false,
                                state = odooBill.State,
                                auto_post = odooBill.AutoPost,
                                type = odooBill.Type,
                                invoice_payment_state = odooBill.InvoicePaymentState,






                                /*journal_id = odooBill.JournalId,
                                invoice_payment_state = odooBill.InvoicePaymentState,
                                state = odooBill.State,
                                auto_post = odooBill.AutoPost,
                                type = odooBill.Type,
                                partner_id = odooBill.PartnerId,
                                @ref = odooBill.Ref,
                                @date = odooBill.Date,
                                name = odooBill.Name,
                                currency_id = odooBill.CurrencyId,
                                invoice_date = odooBill.InvoiceDate,
                                invoice_date_due = odooBill.InvoiceDateDue,
                                origin = odooBill.Origin,
                                job_service_id = odooBill.JobServiceId,
                                amount_untaxed = odooBill.AmountUntaxed,
                                amount_tax = odooBill.AmountTax,
                                amount_total = odooBill.AmountTotal,
                                check_total = odooBill.AmountTotalV8,

                                invoice_line = billLines.Select(bl =>
                                //add new lines
                                new List<object> {
                                    0, false, new {
                                        product_id = bl.ProductId,
                                        name = bl.Name,
                                        account_id = bl.AccountId,
                                        account_analytic_id = false,
                                        attribute1 = bl.DevisionId,
                                        attribute2 = bl.BrandId,
                                        attribute3 = false,
                                        quantity = bl.Quantity,
                                        uos_id = false,
                                        price_unit = bl.PriceUnit,
                                        discount = 0,
                                        invoice_line_tax_id = new List<object> {
                                            new List<object> {
                                                6, false, new List<object> { bl.TaxLineIdsV8 }
                                            }
                                        },
                                        job_service_stage_id = false,
                                        type = "normal",
                                        sequence = bl.Sequence
                                    }
                                }),
                                //Empty tax lines
                                tax_line = new List<object>(),*/
                            });
                        }
                        catch(Exception ex)
                        {
                            ex.LogError("Was an error on voucher export: " + invoice.Number);
                            throw ex;
                        }
                        
                        await UpdateExternalId(invoice, odooBill);

                        if (invoice.FcTax > 0)
                            await odooClient.CreateDynamic<long>("account.invoice", "button_reset_taxes", odooBill.Id);

                        result.NewItems++;
                    }
                    else
                    {
                        //You cannot update Open invoices
                        if (odooBill.State != "draft") {
                            result.SkippedItems++;
                            continue;
                        }

                        odooBill.Ref = invoice.Number;
                        odooBill.Date = DateTime.Now.Date;

                        odooBill.Name = invoice.Number;
                        odooBill.PartnerId = new OdooKeyValue(invoice.Vendor.ExternalId.ToLong(0));
                        odooBill.CommercialPartnerId = new OdooKeyValue(invoice.Vendor.ExternalId.ToLong(0));
                        odooBill.CurrencyId = new OdooKeyValue { Key = currencyCode.ExternalId.ToLong(0) };
                        odooBill.CompanyCurrencyId = new OdooKeyValue { Key = currencyCode.ExternalId.ToLong(0) };
                        
                        odooBill.AmountUntaxed = (decimal?)invoice.FcNet ?? 0;
                        odooBill.AmountTax = (decimal?)invoice.FcTax ?? 0;
                        odooBill.AmountTotal = (decimal?)invoice.FcGross ?? 0;
                        odooBill.AmountResidual = (decimal?)invoice.FcGross ?? 0;
                        odooBill.Residual = (decimal?)invoice.FcGross ?? 0;
                        
                        odooBill.AmountUntaxedSigned = (decimal?)invoice.FcNet ?? 0;
                        odooBill.AmountTaxSigned = (decimal?)invoice.FcTax ?? 0;
                        odooBill.AmountTotalSigned = (decimal?)invoice.FcGross ?? 0;
                        odooBill.AmountResidualSigned = (decimal?)invoice.FcGross ?? 0;

                        odooBill.InvoiceDate = (invoice.Date ?? invoice.CreatedDate).DateTime;
                        odooBill.InvoiceDateDue = (invoice.DueDate ?? invoice.CreatedDate.AddDays(30)).DateTime;
                        odooBill.Type = "in_invoice";
                        odooBill.InvoiceOrigin = invoice.PurchaseOrderNumber;

                        odooBill.AccountIdV8 = new OdooKeyValue(payableAccountIds[0]);
                        odooBill.SupplierInvoiceNumberV8 = invoice.Number;
                        odooBill.DateInvoiceV8 = (invoice.Date ?? invoice.CreatedDate).DateTime;
                        odooBill.DueDateV8 = (invoice.DueDate ?? invoice.CreatedDate.AddDays(30)).DateTime;
                        
                        //calculate taxes in Odoo
                        odooBill.TaxLinesV8 = new OdooArray<OdooTaxLine> { 
                            Objects = new List<OdooTaxLine>() 
                        }; 

                        odooBill.AmountTotalV8 = new OdooDecimal(invoice.FcGross);
                        odooBill.AmountTotal = new OdooDecimal(invoice.FcGross);

                        odooBill.Origin = invoice.PurchaseOrderHeader != null ? 
                            string.Join(" | ", invoice.PurchaseOrderHeader?.Number, invoiceUrl) : invoiceUrl;

                        odooBill.JobServiceId = purchaseAttributes?.JobServiceId != null ? 
                            new OdooKeyValue(purchaseAttributes.JobServiceId.Value) : null;

                        //Create, Update, Delete lines
                        await odooClient.Update("account.invoice", odooBill.Id, new
                        {
                            @ref = odooBill.Ref,
                            @date = odooBill.Date,
                            name = odooBill.Name,

                            partner_id = odooBill.PartnerId,
                            fiscal_position = 1,
                            job_service_id = odooBill.JobServiceId,
                            on_hold = false,
                            origin = odooBill.Origin,
                            supplier_invoice_number = odooBill.SupplierInvoiceNumberV8,
                            reference = odooBill.Ref,
                            date_invoice = odooBill.DateInvoiceV8,
                            date_due = odooBill.DueDateV8,
                            account_id = odooBill.AccountIdV8,
                            journal_id = odooBill.JournalId,
                            currency_id = odooBill.CurrencyId,
                            exchange_rate = 0,
                            check_total = odooBill.AmountTotalV8,

                            invoice_date = odooBill.InvoiceDate,
                            invoice_date_due = odooBill.InvoiceDateDue,
                            amount_untaxed = odooBill.AmountUntaxed,
                            amount_tax = odooBill.AmountTax,
                            amount_total = odooBill.AmountTotal,
                            
                            invoice_line = billLines.Select(bl =>
                            //remove other lines
                            bl.Id < 0 ? new List<object> {
                                2, Math.Abs(bl.Id), false
                            } :
                            //Update quantity and price unit for existing lines only
                            bl.Id > 0 ? new List<object> {
                                1, bl.Id, new { 
                                    quantity = bl.Quantity?.Value, 
                                    price_unit = bl.PriceUnit.Value,
                                    invoice_line_tax_id = new List<object> {
                                        new List<object> {
                                            6, false, new List<object> { bl.TaxLineIdsV8 }
                                        }
                                    },
                                    //Remove tax line
                                    tax_line = odooBill.TaxLinesV8?.Ids?.Select(ti => new List<object> { 2, ti, false }),
                                    sequence = bl.Sequence
                                }
                            } : 
                            //add new line
                            new List<object> {
                                0, false, new {
                                    product_id = bl.ProductId,
                                    name = bl.Name,
                                    account_id = bl.AccountId,
                                    account_analytic_id = false,
                                    attribute1 = bl.DevisionId,
                                    attribute2 = bl.BrandId,
                                    attribute3 = false,
                                    quantity = bl.Quantity,
                                    uos_id = false,
                                    price_unit = bl.PriceUnit,
                                    discount = 0,
                                    invoice_line_tax_id = new List<object> { 
                                        new List<object> { 
                                            6, false, new List<object> { bl.TaxLineIdsV8 } 
                                        } 
                                    },
                                    job_service_stage_id = false,
                                    type = "normal",
                                    sequence = bl.Sequence
                                }
                            }),
                            //Remove tax line
                            tax_line = odooBill.TaxLinesV8?.Ids?.Select(ti => new List<object> { 2, ti, false }),
                        });

                        if(invoice.ExternalId != odooBill.Id.ToString() || invoice.Extra1 == null)
                            await UpdateExternalId(invoice, odooBill);

                        await odooClient.CreateDynamic<long>("account.invoice", "button_reset_taxes", odooBill.Id);

                        result.UpdatedItems++;
                    }
                }
            }
        }

        private async Task UpdateExternalId(ODataClient.Proxies.VoucherHeader invoice, OdooBill odooBill)
        {
            await ocerraClient.ApiVoucherHeaderByIdPatchAsync(invoice.VoucherHeaderId.Value, new List<Operation> {
                new Operation
                {
                    Path = "ExternalId",
                    Op = "replace",
                    Value = odooBill.Id.ToString()
                },
                new Operation
                {
                    Path = "Extra1", //draft invoice id
                    Op = "replace",
                    Value = odooBill.Id.ToString()
                },
                new Operation
                {
                    Path = "Extra2",
                    Op = "replace",
                    Value = "draft"
                }
            });
        }
    }
}
