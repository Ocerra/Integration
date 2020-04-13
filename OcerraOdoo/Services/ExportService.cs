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

                var ocerraTaxAccounts = odata.TaxAccount.ToList();
                var ocerraTaxRates = odata.TaxRate.ToList();
                var ocerraCurrencies = odata.CurrencyCode.ToList();

                var ocerraInvoices = odata.VoucherHeader
                    .Expand(vh => vh.VoucherLines)
                    .Expand(vh => vh.Vendor)
                    .Where(vh => !vh.IsArchived && vh.UpdatedDate > lastSyncDate)
                    .OrderByDescending(vh => vh.CreatedDate)
                    .Take(1) //Should take 100
                    .ToList();

                if (ocerraInvoices.HasItems()) {
                    
                    var odooJournalIds = await odooClient.Search<long[]>(new OdooSearchParameters("account.journal",
                        new OdooDomainFilter().Filter("active", "=", true).Filter("code", "=", "BILL")),
                        new OdooPaginationParameters(0, 5));

                    if (!odooJournalIds.HasItems())
                        throw new Exception("Active journal of Code eq BILL is not found in Odoo");

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
                            PriceUnit = -1 * invoice.FcGross,
                            Debit = 0,
                            Credit = invoice.FcGross,
                            AmountCurrency = 0,
                            PriceSubtotal = -1 * invoice.FcGross,
                            PriceTotal = -1 * invoice.FcGross,
                            Balance = -1 * invoice.FcGross,
                            ExcludeFromInvoiceTab = true,
                            TaxIds = new List<long>(),
                        });

                        foreach (var voucherLine in invoice.VoucherLines) {

                            if ((voucherLine.FcNet ?? 0) == 0 || voucherLine.FcNet > 100000) continue;

                            var taxAccount = ocerraTaxAccounts.First(ta => ta.TaxAccountId == voucherLine.TaxAccountId);
                            taxAccount = taxAccount ?? ocerraTaxAccounts.First(ta => ta.ExternalId == expenseAccountIds[0].ToString());
                            taxRate = taxRate ?? ocerraTaxRates.First(ta => ta.TaxRateId == voucherLine.TaxRateId);

                            
                            //Net Amount Line
                            newBillLines.Add(new OdooBillLine
                            {
                                Name = voucherLine.Description,
                                MoveName = invoice.Number,
                                AccountId = new OdooKeyValue(expenseAccountIds != null ? 
                                    expenseAccountIds[0] : taxAccount.ExternalId.ToLong(0)),
                                AccountInternalType = expenseAccountCodes[1],
                                Date = invoice.CreatedDate.Date,
                                Ref = invoice.Number,
                                JournalId = new OdooKeyValue(odooJournalIds.First()),
                                CompanyCurrencyId = new OdooKeyValue(currencyCode.ExternalId.ToLong(0)),
                                Quantity = voucherLine.Quantity ?? 1,
                                Discount = 0,
                                Credit = 0,
                                AmountCurrency = 0,
                                Debit = voucherLine.FcNet,
                                Balance = voucherLine.FcNet,
                                PriceSubtotal = voucherLine.FcNet,
                                PriceTotal = voucherLine.FcGross,
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
                                PriceUnit = invoice.FcTax,
                                Discount = 0,
                                Credit = 0,
                                Debit = invoice.FcTax,
                                Balance = invoice.FcTax,
                                AmountCurrency = 0,
                                PriceSubtotal = invoice.FcTax,
                                PartnerId = new OdooKeyValue(invoice.Vendor.ExternalId.ToLong(0)),
                                PriceTotal = invoice.FcTax,
                                TaxLineId = new OdooKeyValue(taxRate.ExternalId.ToLong(0)),
                                TaxIds = new List<long>(),
                                ExcludeFromInvoiceTab = true
                            });
                        }

                        if (odooBill == null) {
                            odooBill = new OdooBill
                            {
                                Name = invoice.Number,
                                PartnerId = new OdooKeyValue(invoice.Vendor.ExternalId.ToLong(0)),
                                CommercialPartnerId = new OdooKeyValue(invoice.Vendor.ExternalId.ToLong(0)),
                                CompanyCurrencyId = new OdooKeyValue { Key = currencyCode.ExternalId.ToLong(0) },
                                
                                AmountUntaxed = invoice.FcNet ?? 0,
                                AmountTax = invoice.FcTax ?? 0,
                                AmountTotal = invoice.FcGross ?? 0,
                                AmountResidual = invoice.FcGross ?? 0,

                                AmountUntaxedSigned = invoice.FcNet ?? 0,
                                AmountTaxSigned = invoice.FcTax ?? 0,
                                AmountTotalSigned = invoice.FcGross ?? 0,
                                AmountResidualSigned = invoice.FcGross ?? 0,

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

                            odooBill.AmountUntaxed = invoice.FcNet ?? 0;
                            odooBill.AmountTax = invoice.FcTax ?? 0;
                            odooBill.AmountTotal = invoice.FcGross ?? 0;
                            odooBill.AmountResidual = invoice.FcGross ?? 0;

                            odooBill.AmountUntaxedSigned = invoice.FcNet ?? 0;
                            odooBill.AmountTaxSigned = invoice.FcTax ?? 0;
                            odooBill.AmountTotalSigned = invoice.FcGross ?? 0;
                            odooBill.AmountResidualSigned = invoice.FcGross ?? 0;

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
                            odooBill.LineIds = new OdooArray<OdooBillLine>() {
                                Objects = newBillLines
                            };
                            await odooClient.Update("account.move", odooBill.Id, odooBill);

                            result.UpdatedItems++;
                        }
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
                    Message = "There was an error on Bill export."
                };
            }
        }
    }
}
