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
using System.Threading.Tasks;

namespace OcerraOdoo.Services
{
    public class ExportService
    {
        private readonly OcerraClient ocerraClient;
        private readonly OdooRpcClient odooClient;
        private OdataProxy odata;
        public bool Initialized { get; set; }

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

                var ocerraInvoices = odata.VoucherHeader
                    .Expand("VoucherLines($expand=TaxAccount,ItemCode)")
                    .Expand(vh => vh.Vendor)
                    .Where(vh => !vh.IsArchived && vh.UpdatedDate > lastSyncDate)
                    .OrderByDescending(vh => vh.CreatedDate)
                    .Take(1) //Should take 100
                    .ToList();

                await ExportInvoicesFromListV8(ocerraInvoices, result);

                result.Message = $"Invoices exported successfully: created {result.NewItems}, updated: {result.UpdatedItems}";

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
                    
                    //Export invoices one by one
                    var ocerraInvoice = odata.VoucherHeader
                        .Expand("Vendor,VoucherLines($expand=TaxAccount,ItemCode)")
                        //.Expand(vh => vh.Vendor)
                        .Where(vh => vh.VoucherHeaderId == voucherHeaderId)
                        .FirstOrDefault();

                    if(ocerraInvoice != null && ocerraInvoice.VoucherHeaderId == voucherHeaderId)
                        await ExportInvoicesFromListV8(new List<ODataClient.Proxies.VoucherHeader> { ocerraInvoice }, result);
                    else
                    {
                        throw new Exception($"This invoice {voucherHeaderId} is not found");
                    }
                }
                

                result.Message = $"Invoices exported successfully: created {result.NewItems}, updated: {result.UpdatedItems}";

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
                        await odooClient.Get<OdooBill[]>(new OdooGetParameters("account.invoice", odooBillIds), new OdooFieldParameters()) : new OdooBill[] { };


                var defaultOdooExpenseAccountId = expenseAccountIds.HasItems() ? expenseAccountIds[0].ToString() : null;
                var defaultExpenseAccount = defaultOdooExpenseAccountId != null ? odata.TaxAccount.Where(ta => ta.ExternalId == defaultOdooExpenseAccountId).FirstOrDefault() : null;
                var ocerraTaxRates = odata.TaxRate.ToList();
                var ocerraCurrencies = odata.CurrencyCode.ToList();


                foreach (var invoice in ocerraInvoices)
                {
                    //Skip invoices without the vendor
                    if (invoice.VendorId == null) continue;

                    var odooBill = invoice.ExternalId != null ? odooBills.FirstOrDefault(b => b.Id == invoice.ExternalId.ToLong(0)) : null;

                    /*ar existingBillLines = odooBill != null && odooBill.InvoiceLineIdsV8.Ids != null ? 
                        await odooClient.Get<OdooBillLineV8[]>(new OdooGetParameters("account.invoice.line", odooBill.InvoiceLineIdsV8.Ids), new OdooFieldParameters()) : null;*/

                    
                    var newBillLines = new List<OdooBillLineV8>();
                    var taxLines = new List<OdooTaxLine>();

                    var currencyCode = ocerraCurrencies.First(cc => cc.CurrencyCodeId == invoice.CurrencyCodeId);

                    ODataClient.Proxies.TaxRate taxRate = null;
                    ODataClient.Proxies.TaxAccount firstTaxAccount = null;

                    
                    foreach (var voucherLine in invoice.VoucherLines.OrderBy(vl => vl.Sequence))
                    {

                        if ((voucherLine.FcNet ?? 0) == 0 || voucherLine.FcNet > 100000) continue;

                        var taxAccount = voucherLine.TaxAccount;
                        taxAccount = taxAccount ?? defaultExpenseAccount;
                        taxRate = taxRate ?? ocerraTaxRates.FirstOrDefault(ta => ta.TaxRateId == (voucherLine.TaxRateId ?? taxAccount?.TaxRateId));
                        firstTaxAccount = firstTaxAccount ?? taxAccount;

                        //Skip non coded lines
                        if (taxAccount == null && expenseAccountIds == null) continue;
                        if (taxRate == null && expenseAccountIds == null) continue;

                        var sequence = 1000 + voucherLine.Sequence;
                        //Net Amount Line
                        newBillLines.Add(new OdooBillLineV8
                        {
                            //Id = existingBillLines?.FirstOrDefault(el => el.Sequence == sequence)?.Id ?? 0,
                            ProductId = voucherLine.ItemCode != null ? 
                                new OdooKeyValue(voucherLine.ItemCode.ExternalId.ToLong(0)) : null,
                            Name = voucherLine.Description,
                            AccountId = new OdooKeyValue(taxAccount != null ?
                                taxAccount.ExternalId.ToLong(0) : expenseAccountIds[0]),
                            Quantity = (decimal?)voucherLine.Quantity ?? 1,
                            PriceUnit = Math.Round((decimal?)voucherLine.FcNet / (decimal?)voucherLine.Quantity ?? 1, 2),
                            Discount = 0,
                            //PriceSubtotal = (decimal?)voucherLine.FcNet,
                            //AmountUntaxed = (decimal?)voucherLine.FcNet,
                            //AmountTotal = (decimal?)voucherLine.FcNet,
                            Sequence = sequence,
                            TaxLineIdsV8 = invoice.FcTax > 0 ? new List<List<object>> { new List<object> { 6, false, new[] { taxRate.ExternalId.ToLong(0) } } } : null
                        });
                    }

                    //Add other tax
                    if (invoice.FcOther > 0 && (firstTaxAccount != null || expenseAccountIds != null)) 
                    {
                        newBillLines.Add(new OdooBillLineV8
                        {
                            //Id = existingBillLines?.FirstOrDefault(el => el.Sequence == 9999)?.Id ?? 0,
                            Name = "Other",
                            AccountId = new OdooKeyValue(firstTaxAccount != null ?
                                firstTaxAccount.ExternalId.ToLong(0) : expenseAccountIds[0]),
                            Quantity = 1,
                            PriceUnit = invoice.FcOther,
                            Discount = 0,
                            Sequence = 9999,
                            TaxLineIdsV8 = invoice.FcTax > 0 ? new List<List<object>> { new List<object> { 6, false, new[] { taxRate.ExternalId.ToLong(0) } } } : null
                        });
                    }

                    //Tax Amount Line
                    if ((invoice.FcTax ?? 0) != 0)
                    {
                        taxLines.Add(new OdooTaxLine
                        {
                            //Id = odooBill?.TaxLinesV8?.Ids?.FirstOrDefault() ?? 0,
                            Name = "Purchase Tax",
                            AccountId = new OdooKeyValue(taxAccountIds[0]),
                            Amount = invoice.FcTax,
                            BaseAmount = invoice.FcNet,
                            TaxAmount = invoice.FcTax
                        });
                    }

                    if (odooBill == null)
                    {
                        odooBill = new OdooBill
                        {
                            AccountIdV8 = new OdooKeyValue(payableAccountIds[0]),
                            SupplierInvoiceNumberV8 = invoice.Number,
                            DateInvoiceV8 = (invoice.Date ?? invoice.CreatedDate).DateTime,
                            DueDateV8 = (invoice.DueDate ?? invoice.CreatedDate.AddDays(30)).DateTime,
                            InvoiceLineIdsV8 = new OdooArray<OdooBillLineV8>() { Objects = newBillLines },
                            TaxLinesV8 = new OdooArray<OdooTaxLine> { Objects = taxLines },
                            AmountTotalV8 = new OdooDecimal(invoice.FcGross),

                            Name = invoice.Number,
                            PartnerId = new OdooKeyValue(invoice.Vendor.ExternalId.ToLong(0)),
                            CommercialPartnerId = new OdooKeyValue(invoice.Vendor.ExternalId.ToLong(0)),
                            CurrencyId = new OdooKeyValue { Key = currencyCode.ExternalId.ToLong(0) },

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
                            
                        };

                        odooBill.Id = await odooClient.Create("account.invoice", odooBill);

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

                        //Re-Do Invoice update
                        //Remove old lines
                        //odooBill.InvoiceLineIdsV8 = new OdooArray<OdooBillLineV8>();
                        await odooClient.Update("account.invoice", odooBill.Id, new
                        {
                            invoice_line = odooBill.InvoiceLineIdsV8.Ids.Select(li => new List<object> { 2, li, false }),
                            tax_line = odooBill.TaxLinesV8.Ids.Select(ti => new List<object> { 2, ti, false }),
                        });

                        odooBill.AccountIdV8 = new OdooKeyValue(payableAccountIds[0]);
                        odooBill.SupplierInvoiceNumberV8 = invoice.Number;
                        odooBill.DateInvoiceV8 = (invoice.Date ?? invoice.CreatedDate).DateTime;
                        odooBill.DueDateV8 = (invoice.DueDate ?? invoice.CreatedDate.AddDays(30)).DateTime;
                        odooBill.InvoiceLineIdsV8 = new OdooArray<OdooBillLineV8>() { Objects = newBillLines };
                        odooBill.TaxLinesV8 = new OdooArray<OdooTaxLine> { Objects = taxLines };
                        odooBill.AmountTotalV8 = new OdooDecimal(invoice.FcGross);


                        //Create new lines
                        //odooBill.InvoiceLineIdsV8 = new OdooArray<OdooBillLineV8>() { Objects = newBillLines };
                        await odooClient.Update("account.invoice", odooBill.Id, odooBill);

                        result.UpdatedItems++;
                    }
                }
            }
        }
    }
}
