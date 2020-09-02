using Nancy.Extensions;
using Nancy.TinyIoc;

using OcerraOdoo.Models;
using OcerraOdoo.OcerraOData;
using OcerraOdoo.ODataClient.Proxies;
using OcerraOdoo.Properties;
using OdooRpc.CoreCLR.Client;
using OdooRpc.CoreCLR.Client.Models;
using OdooRpc.CoreCLR.Client.Models.Parameters;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Configuration;

namespace OcerraOdoo.Services
{
    public class ImportService
    {
        private readonly OcerraClient ocerraClient;
        private readonly OdooRpcClient odooClient;
        private OdataProxy odata;
        public bool Initialized { get; set; }

        public ImportService(OcerraClient ocerraClient, OdooRpcClient odooClient, OdataProxy odata)
        {
            this.ocerraClient = ocerraClient;
            this.odooClient = odooClient;
            this.odata = odata;
        }

        private async Task Init() {
            if (Initialized) return;
            await odooClient.Authenticate();
            Initialized = true;
        }

        public async Task<ImportResult> ImportCurrency()
        {

            var result = new ImportResult();

            try
            {
                await Init();

                var odooCurrencyIds = await odooClient.Search<long[]>(new OdooSearchParameters("res.currency",
                    new OdooDomainFilter().Filter("active", "=", true)), new OdooPaginationParameters(0, 100));

                var odooCurrencies = odooCurrencyIds.HasItems() ? 
                    await odooClient.Get<OdooCurrency[]>(new OdooGetParameters("res.currency",
                        odooCurrencyIds), new OdooFieldParameters()) : null;

                var ocerraCurrencies = await ocerraClient.ApiCurrencyGetAsync(0, 200);

                if (odooCurrencies.HasItems())
                    foreach (var odooCur in odooCurrencies)
                    {
                        var ocerraCurrency = ocerraCurrencies.FirstOrDefault(t => t.ExternalId == odooCur.Id.ToString() || t.Code == odooCur.Name);

                        if (ocerraCurrency == null)
                        {
                            var currency = new CurrencyCodeModel()
                            {
                                CurrencyCodeId = Guid.NewGuid(),
                                ClientId = Bootstrapper.OcerraModel.ClientId,
                                Code = odooCur.Name,
                                Description = odooCur.Currency_Unit_Label,
                                Sequence = 1000 - odooCur.Id,
                                ExternalId = odooCur.Id.ToString(),
                                Country = odooCur.Currency_Unit_Label ?? odooCur.Name.Substring(0, 2),
                                CountryCode = odooCur.Name.Substring(0, 2)
                            };
                            await ocerraClient.ApiCurrencyPostAsync(currency);
                            result.NewItems++;
                        }
                        else
                        {
                            ocerraCurrency.ExternalId = odooCur.Id.ToString();
                            await ocerraClient.ApiCurrencyByIdPutAsync(ocerraCurrency.CurrencyCodeId, ocerraCurrency);
                            result.UpdatedItems++;
                        }
                    }

                return new ImportResult
                {
                    Message = "Currency imported succsessfuly."
                };
            }
            catch (Exception ex)
            {
                ex.LogError("Error on Currency Import");
                return new ImportResult
                {
                    Message = "There was an error on currency import procedure"
                };
            }
        }

        public async Task<ImportResult> ImportTaxes() {

            var result = new ImportResult();

            try
            {
                await Init();

                var odooTaxes = await odooClient.GetAll<OdooTax[]>("account.tax", new OdooFieldParameters());

                var ocerraTaxes = await ocerraClient.ApiTaxRateGetAsync(0, 100);

                var validTaxTypes = Settings.Default.OdooTaxTypes.Split(',');

                if (odooTaxes.HasItems())
                    foreach (var odooTax in odooTaxes.Where(t => validTaxTypes.Contains(t.Type_Tax_Use) && t.Amount < 100))
                    {
                        var taxRateRate = odooTax.Amount < 1 && odooTax.Amount > 0 ? odooTax.Amount * 100 : odooTax.Amount;

                        var ocerraTax = ocerraTaxes.FirstOrDefault(t => t.ExternalId == odooTax.Id.ToString());
                        if (ocerraTax == null)
                        {
                            var taxRate = new TaxRateModel()
                            {
                                ClientId = Bootstrapper.OcerraModel.ClientId,
                                TaxRateId = Guid.NewGuid(),
                                Code = odooTax.Description,
                                Description = odooTax.Name,
                                ExternalId = odooTax.Id.ToString(),
                                ExternalAccountId = odooTax.Cash_Basis_Base_Account_Id.ToInt(null)?.ToString(),
                                IsActive = true,
                                TaxType = odooTax.Type_Tax_Use,
                                Rate = taxRateRate
                            };
                            await ocerraClient.ApiTaxRatePostAsync(taxRate);
                            result.NewItems++;
                        }
                        else
                        {
                            ocerraTax.ExternalAccountId = odooTax.Cash_Basis_Base_Account_Id.ToInt(null)?.ToString();
                            ocerraTax.Code = odooTax.Description;
                            ocerraTax.Description = odooTax.Name;
                            ocerraTax.ExternalId = odooTax.Id.ToString();
                            ocerraTax.ExternalAccountId = odooTax.Cash_Basis_Base_Account_Id.ToInt(null)?.ToString();
                            ocerraTax.IsActive = true;
                            ocerraTax.TaxType = odooTax.Type_Tax_Use;
                            ocerraTax.Rate = taxRateRate;

                            await ocerraClient.ApiTaxRateByIdPutAsync(ocerraTax.TaxRateId, ocerraTax);
                            result.UpdatedItems++;
                        }
                    }

                return new ImportResult
                {
                    Message = "Taxes imported succsessfuly."
                };
            } 
            catch(Exception ex)
            {
                ex.LogError("Error on Tax Import");
                return new ImportResult
                {
                    Message = "There was an error on tax import procedure"
                };
            }
        }

        public async Task<ImportResult> ImportAccounts() {
            var result = new ImportResult();

            OdooAccount currentOdooAccount = null;
            TaxAccountModel currentOcerraAccount = null;

            try
            {
                await Init();

                var odooAccounts = await odooClient.GetAll<OdooAccount[]>("account.account", new OdooFieldParameters());

                var ocerraAccounts1000 = await ocerraClient.ApiTaxAccountGetAsync(0, 1000);
                var ocerraAccounts2000 = await ocerraClient.ApiTaxAccountGetAsync(1, 1000);
                var ocerraTaxes = await ocerraClient.ApiTaxRateGetAsync(0, 100);

                var validAccountGroups = Helpers.AppSetting().OdooAccountGroups.Split(',');

                if (odooAccounts.HasItems())
                {
                    var accountsForImport = odooAccounts.Where(a => validAccountGroups.Any(vg =>
                        a.Code.ToLower() == vg.ToLower() ||
                        a.Internal_Group == vg ||
                        (a.Parent_Id?.Value != null && a.Parent_Id.Value.ToLower().Contains(vg.ToLower())) ||
                        (a.User_Type?.Value != null && a.User_Type.Value.ToLower().Contains(vg.ToLower()))
                    ) && !a.Deprecated).ToList();

                    //var assetAccounts = accountsForImport.Where(a => a.Code == "12010").ToList();

                    foreach (var odooAccount in accountsForImport)
                    {
                        currentOdooAccount = odooAccount;
                        var ocerraAccount = ocerraAccounts1000.FirstOrDefault(t => t.ExternalId == odooAccount.Id.ToString());
                        ocerraAccount = ocerraAccount ?? ocerraAccounts2000.FirstOrDefault(t => t.ExternalId == odooAccount.Id.ToString());
                        ocerraAccount = ocerraAccount ?? ocerraAccounts1000.FirstOrDefault(t => t.Code == odooAccount.Code);
                        ocerraAccount = ocerraAccount ?? ocerraAccounts2000.FirstOrDefault(t => t.Code == odooAccount.Code);

                        currentOcerraAccount = ocerraAccount;

                        var taxRateId = ocerraTaxes.FirstOrDefault(r => r.ExternalAccountId == odooAccount.Id.ToString())?.TaxRateId ??
                                    ocerraTaxes.FirstOrDefault(t => t.Code == "Purch (15%)")?.TaxRateId ??
                                    ocerraTaxes.FirstOrDefault(t => t.Code.Contains("Standard"))?.TaxRateId ??
                                    ocerraTaxes.OrderByDescending(t => t.Rate).First().TaxRateId;
                        if (ocerraAccount == null)
                        {
                            var taxAccount = new TaxAccountModel()
                            {
                                ClientId = Bootstrapper.OcerraModel.ClientId,
                                TaxAccountId = Guid.NewGuid(),
                                Code = odooAccount.Code,
                                Name = odooAccount.Name,
                                Description = odooAccount.Internal_Group ?? odooAccount.User_Type?.Value ?? odooAccount.Parent_Id.Value,
                                ExternalId = odooAccount.Id.ToString(),
                                IsActive = true,
                                TaxType = odooAccount.Internal_Type ?? odooAccount.User_Type?.Value ?? odooAccount.Parent_Id.Value,
                                TaxRateId = taxRateId
                            };
                            await ocerraClient.ApiTaxAccountPostAsync(taxAccount);
                            result.NewItems++;
                        }
                        else
                        {
                            ocerraAccount.Code = odooAccount.Code;
                            ocerraAccount.Name = odooAccount.Name;
                            ocerraAccount.Description = odooAccount.Internal_Group ?? odooAccount.User_Type?.Value ?? odooAccount.Parent_Id.Value;
                            ocerraAccount.ExternalId = odooAccount.Id.ToString();
                            ocerraAccount.IsActive = true;
                            ocerraAccount.TaxType = odooAccount.Internal_Type ?? odooAccount.User_Type?.Value ?? odooAccount.Parent_Id.Value;
                            ocerraAccount.TaxRateId = taxRateId;

                            await ocerraClient.ApiTaxAccountByIdPutAsync(ocerraAccount.TaxAccountId, ocerraAccount);

                            result.UpdatedItems++;
                        }
                    }
                }
                    

                return new ImportResult
                {
                    Message = "Accounts imported succsessfuly."
                };
            }
            catch (Exception ex)
            {
                ex.LogError("Error on Accounts Import");
                return new ImportResult
                {
                    Message = "There was an error on Accounts import procedure"
                };
            }
        }

        public async Task<ImportResult> ImportVendors(DateTime lastSyncDate)
        {
            var result = new ImportResult();

            VendorModel vendor = null;
            try
            {
                await Init();

                var taxRates = odata.TaxRate.Take(100).ToList();

                var filter = new OdooDomainFilter().Filter("write_date", ">=", lastSyncDate).Filter("supplier", "=", true).Filter("is_company", "=", true); //.Filter("customer", "=", false);

                var odooCountryCodes = await odooClient.GetAll<OdooCountry[]>("res.country", new OdooFieldParameters(new[] { "id", "code" }));

                var fieldNames = new[] {
                        "id",
                        "name",
                        "display_name",
                        "title",
                        "vat",
                        "website",
                        "comment",
                        "country_id",
                        "email",
                        "phone",
                        "mobile",
                        "contact_address",
                        "company_name",
                        "currency_id",
                        "property_account_payable_id",
                        "property_account_expense_categ",
                        "property_stock_account_input_categ"
                };

                var odooCompanies = new List<OdooCompany>();
                for (int x = 0; x < 100; x = x + 2) {
                    
                    var odooCompanyIds = await odooClient.Search<long[]>(new OdooSearchParameters("res.partner",
                        filter), new OdooPaginationParameters(x * 100, 200));

                    if (!odooCompanyIds.HasItems()) break;
                    
                    var odooCompaniesPage = odooCompanyIds.HasItems() ?
                        await odooClient.Get<OdooCompany[]>(new OdooGetParameters("res.partner", odooCompanyIds), new OdooFieldParameters(fieldNames)) : null;

                    odooCompanies.AddRange(odooCompaniesPage);
                }

                if (odooCompanies.HasItems())
                    foreach (var odooVendor in odooCompanies)
                    {
                        var ocerraVendor = await ocerraClient.ApiVendorsExternalByIdGetAsync(odooVendor.Id.ToString());

                        var email = odooVendor.Email?.ToLower();
                        
                        var domain = odooVendor.Website != null && odooVendor.Website.ToUri() != null ? odooVendor.Website.ToUri().Host :  
                            email != null && !Settings.Default.SharedEmailProviders.Split(',').Any(ep => email.Contains(ep)) ? email.Split('@').LastOrDefault()?.ToUri()?.Host : null;
                        
                        var defaultAccount = 
                            odooVendor.Property_Account_Payable_Id?.Key != null ? await GetTaxAccountByExternalId(odooVendor.Property_Account_Payable_Id.Key.ToString()) :
                            odooVendor.PropertyAccountExpenseCateg?.Key != null ? await GetTaxAccountByExternalId(odooVendor.PropertyAccountExpenseCateg.Key.ToString()) :
                            odooVendor.PropertyStockAccountInputCateg?.Key != null ? await GetTaxAccountByExternalId(odooVendor.PropertyStockAccountInputCateg.Key.ToString()) :
                            null;

                        var countryCode = odooCountryCodes.FirstOrDefault(cc => cc.Id == odooVendor.Country_Id?.Key)?.Code?.Value
                                    ?? Bootstrapper.OcerraModel.CountryCode;

                        var vendorName = (odooVendor.Company_Name ?? odooVendor.Name).Trim(255);

                        var foreighSupplier = !countryCode.Contains("NZ") || 
                            (odooVendor.Currency_Id?.Value != null && !odooVendor.Currency_Id.Value.Contains("NZ")) || odooVendor.Name.Contains("USD");

                        //Assign Zero tax rate auto for all USD suppliers
                        var taxRateId = foreighSupplier ? taxRates.FirstOrDefault(tr => tr.Rate == 0 && (tr.Code == "PNT" || tr.Description == "PNT"))?.TaxRateId : null;
                        taxRateId = taxRateId ?? defaultAccount?.TaxRateId;

                        if (ocerraVendor == null)
                        {
                            vendor = new VendorModel()
                            {
                                VendorId = Guid.NewGuid(),
                                ClientId = Bootstrapper.OcerraModel.ClientId,
                                Name = vendorName,
                                CountryCode = countryCode,
                                Email = email.Trim(50),
                                DomainName = domain.Trim(255),
                                Description = odooVendor.Comment.Trim(512),
                                IsActive = true,
                                ExternalId = odooVendor.Id.ToString(),
                                PhoneNumber = odooVendor.Phone,
                                DefaultTaxAccountId = defaultAccount?.TaxAccountId,
                                DefaultTaxRateId = taxRateId,
                                TaxNumber = odooVendor.Vat
                            };
                            
                            //Auto code based on supplier country
                            vendor.CurrencyCodeId = Bootstrapper.OcerraModel.CurrencyCodes?.FirstOrDefault(cc => cc.CountryCode == vendor.CountryCode)?.CurrencyCodeId;

                            await ocerraClient.ApiVendorsPostAsync(vendor);
                            
                            result.NewItems++;
                        }
                        else
                        {
                            vendor = ocerraVendor;
                            ocerraVendor.Name = vendorName;
                            ocerraVendor.CountryCode = countryCode;
                            ocerraVendor.Email = email.Trim(50);
                            ocerraVendor.DomainName = domain.Trim(255);
                            ocerraVendor.Description = odooVendor.Comment.Trim(512);
                            ocerraVendor.IsActive = true;
                            ocerraVendor.ExternalId = odooVendor.Id.ToString();
                            ocerraVendor.PhoneNumber = odooVendor.Phone;
                            //Auto code based on supplier country
                            ocerraVendor.CurrencyCodeId = Bootstrapper.OcerraModel.CurrencyCodes?
                                .FirstOrDefault(cc => cc.CountryCode == ocerraVendor.CountryCode)?.CurrencyCodeId;
                            ocerraVendor.DefaultTaxAccountId = defaultAccount?.TaxAccountId;
                            ocerraVendor.DefaultTaxRateId = taxRateId;
                            ocerraVendor.TaxNumber = odooVendor.Vat ?? ocerraVendor.TaxNumber;

                            await ocerraClient.ApiVendorsByIdPutAsync(ocerraVendor.VendorId, ocerraVendor);
                            result.UpdatedItems++;
                        }
                    }

                result.Message = "Vendors imported succsessfuly.";
                return result;
            }
            catch (Exception ex)
            {
                ex.LogError("Error on Vendor Import");
                result.Message = "There was an error on vendor import procedure";
                return result;
            }
        }

        public async Task<ImportResult> ImportPurchaseOrders(DateTime lastSyncDate)
        {
            var result = new ImportResult();

            PurchaseOrderHeaderModel current = null;
            var exp = new Regex(@"\<(.+?)\>", RegexOptions.IgnoreCase);

            try
            {
                await Init();

                var filter = new OdooDomainFilter().Filter("write_date", ">", lastSyncDate);

                var fieldNames = new[] {
                    "id",
                    "name",
                    "partner_id",
                    "currency_id",
                    "date_approve",
                    "date_order",
                    "amount_tax",
                    "amount_total",
                    "user_id",
                    "origin",
                    "order_line",
                    "invoice_ids",
                    "state",
                    "validator",
                    "message_ids"
                };


                var odooPurchaseOrders = new List<OdooPurchaseOrder>();

                for (int x = 0; x < 100; x++) {
                    
                    var odooPurchaseOrderIds = await odooClient.Search<long[]>(new OdooSearchParameters("purchase.order",
                        filter), new OdooPaginationParameters(x * 100, 100));

                    if (!odooPurchaseOrderIds.HasItems())
                        break;

                    var odooPurchaseOrderPage = odooPurchaseOrderIds.HasItems() ?
                        await odooClient.Get<OdooPurchaseOrder[]>(new OdooGetParameters("purchase.order", odooPurchaseOrderIds),
                            new OdooFieldParameters(fieldNames)) : null;
                    
                    odooPurchaseOrders.AddRange(odooPurchaseOrderPage);
                }

                int ignored = 0;

                if (odooPurchaseOrders.HasItems())
                    foreach (var odooPurchaseOrder in odooPurchaseOrders)
                    {
                        var lineFieldNames = new[] {
                            "company_id",
                            "display_uom",
                            "id",
                            "name",
                            "price_subtotal",
                            "price_unit",
                            "product_id",
                            "product_qty",
                            "product_uom",
                            "attribute1",
                            "attribute2",
                            "attribute3",
                            "taxes_id"
                        };

                        var purchaseOrderLines = odooPurchaseOrder.OrderLines.HasItems() ? 
                            await odooClient.Get<OdooPurchaseOrderLine[]>(new OdooGetParameters("purchase.order.line", odooPurchaseOrder.OrderLines),
                                new OdooFieldParameters(lineFieldNames)) : null;
                        var ocerraPurchaseOrder = await ocerraClient.ApiPurchaseOrderExternalByIdGetAsync(odooPurchaseOrder.Id.ToString());
                        var vendor = odooPurchaseOrder.Partner_Id.HasItems() ? await ocerraClient.ApiVendorsExternalByIdGetAsync(odooPurchaseOrder.Partner_Id[0].ToString()) : null;
                        var currency = odooPurchaseOrder.Currency_Id.HasItems() ? Bootstrapper.OcerraModel.CurrencyCodes.Find(cc => cc.ExternalId == odooPurchaseOrder.Currency_Id[0])
                            : Bootstrapper.OcerraModel.CurrencyCodes.Find(cc => cc.IsDefault);
                        
                        
                        var messages = odooPurchaseOrder.Message_Ids.HasItems() ?
                            await odooClient.Get<OdooMessage[]>(new OdooGetParameters("mail.message", odooPurchaseOrder.Message_Ids),
                                new OdooFieldParameters()) : null;

                        var firstMessage = messages?.LastOrDefault();

                        var purchaserName = firstMessage?.Author_Id?.Value;
                        
                        //var purchaserId = firstMessage?.Author_Id?.Key;
                        //var userPid = firstMessage?.User_Pid;
                        
                        var purchaserEmailGroups = firstMessage?.Email_From != null ? exp.Match(firstMessage.Email_From).Groups : null;
                        var purchaserEmail = purchaserEmailGroups != null && purchaserEmailGroups.Count > 1 ? purchaserEmailGroups[1]?.Value : null;
                        
                        var odooUser = await GetOdooUserByName(purchaserName); 
                        
                        //var odooUserById = await GetOdooUserById(purchaserId);
                        //var odooUserByPid = await GetOdooUserById(userPid);

                        purchaserEmail = odooUser?.email?.Value != null && odooUser.email.Value.Contains("@") ? odooUser.email.Value :
                            odooUser?.login != null && odooUser.login.Contains("@") ? odooUser.login : 
                            purchaserEmail;

                        if (vendor == null) {
                            ignored++;
                            continue;
                        }


                        try
                        {
                            if (ocerraPurchaseOrder == null)
                            {
                                var purchaseOrder = new PurchaseOrderHeaderModel()
                                {
                                    PurchaseOrderHeaderId = Guid.NewGuid(),
                                    VendorId = vendor.VendorId,
                                    Number = odooPurchaseOrder.Name,
                                    ExternalId = odooPurchaseOrder.Id.ToString(),
                                    ApprovedDate = odooPurchaseOrder.Date_Approve.ToDateOffset(null),
                                    CurrencyCodeId = currency?.CurrencyCodeId ?? Bootstrapper.OcerraModel.CurrencyCodes.Find(cc => cc.IsDefault).CurrencyCodeId,
                                    DocDate = odooPurchaseOrder.Date_Order.ToDateOffset(DateTimeOffset.Now).Value,
                                    IsTaxInclusive = odooPurchaseOrder.Amount_Tax == 0,
                                    Total = odooPurchaseOrder.Amount_Total ?? 0,
                                    OutstandingCost = odooPurchaseOrder.Amount_Total ?? 0,
                                    PurchaserId = odooUser?.Id.ToString(),
                                    PurchaserName = purchaserName,
                                    PurchaserEmail = purchaserEmail,
                                    Status = odooPurchaseOrder.State.ToPascalCase(),
                                    Reference = odooPurchaseOrder.Origin.Trim(255),
                                    Attributes = new OdooPurchaseOrderDetails
                                    {
                                        DraftInvoiceId = odooPurchaseOrder.invoice_ids.HasItems() ? odooPurchaseOrder.invoice_ids[0].ToLong(0) : 0,
                                        Reference = odooPurchaseOrder.Origin.Trim(255)
                                    }.ToJson(),
                                };

                                current = purchaseOrder;

                                if (purchaseOrderLines.HasItems())
                                {

                                    purchaseOrder.PurchaseOrderLines = new List<PurchaseOrderLineModel>();

                                    var counter = 1;
                                    foreach (var odooLine in purchaseOrderLines)
                                    {
                                        var poLine = new PurchaseOrderLineModel
                                        {
                                            PurchaseOrderLineId = Guid.NewGuid(),
                                            ExternalId = odooLine.Id.ToString(),
                                            Code = odooLine.ProductId?.Value?.Trim(50),
                                            Cost = (double?)(decimal?)odooLine.Price_Subtotal ?? 0,
                                            Quantity = (double?)(decimal?)odooLine.Product_Qty ?? 1,
                                            Description = odooLine.Name?.Trim(250),
                                            ItemCodeId = odooLine.ProductId?.Key != null ?
                                                (await ocerraClient.ApiItemCodeByExternalGetAsync(odooLine.ProductId?.Key.ToString()))?.FirstOrDefault()?.ItemCodeId : null,
                                            Sequence = counter,
                                            Attributes = new OdooPurchaseOrderLineAttributes
                                            {
                                                DivisionId = odooLine?.Attribute1?.Key,
                                                DivisionCode = odooLine?.Attribute1?.Value,
                                                BrandId = odooLine?.Attribute2?.Key,
                                                BrandCode = odooLine?.Attribute2?.Value,
                                                TaxId = odooLine?.Taxes?.Key
                                            }.ToJson()
                                        };

                                        purchaseOrder.PurchaseOrderLines.Add(poLine);

                                        counter++;
                                    }
                                }

                                await ocerraClient.ApiPurchaseOrderPostAsync(purchaseOrder);

                                result.NewItems++;
                            }
                            else
                            {
                                current = ocerraPurchaseOrder;

                                ocerraPurchaseOrder.Number = odooPurchaseOrder.Name;
                                ocerraPurchaseOrder.VendorId = vendor.VendorId;
                                ocerraPurchaseOrder.ExternalId = odooPurchaseOrder.Id.ToString();
                                ocerraPurchaseOrder.ApprovedDate = odooPurchaseOrder.Date_Approve.ToDateOffset(null);
                                ocerraPurchaseOrder.CurrencyCodeId = currency?.CurrencyCodeId ?? Bootstrapper.OcerraModel.CurrencyCodes.Find(cc => cc.IsDefault).CurrencyCodeId;
                                ocerraPurchaseOrder.DocDate = odooPurchaseOrder.Date_Order.ToDateOffset(DateTimeOffset.Now).Value;
                                ocerraPurchaseOrder.IsTaxInclusive = odooPurchaseOrder.Amount_Tax == 0;
                                ocerraPurchaseOrder.Total = odooPurchaseOrder.Amount_Total ?? 0;
                                ocerraPurchaseOrder.OutstandingCost = odooPurchaseOrder.Amount_Total ?? 0;
                                ocerraPurchaseOrder.PurchaserId = odooUser?.Id.ToString();
                                ocerraPurchaseOrder.PurchaserName = purchaserName;
                                ocerraPurchaseOrder.PurchaserEmail = purchaserEmail;
                                ocerraPurchaseOrder.Reference = odooPurchaseOrder.Origin.Trim(255);
                                ocerraPurchaseOrder.Attributes = new OdooPurchaseOrderDetails
                                {
                                    DraftInvoiceId = odooPurchaseOrder.invoice_ids.HasItems() ? odooPurchaseOrder.invoice_ids[0].ToLong(0) : 0
                                }.ToJson();

                                ocerraPurchaseOrder.Status = odooPurchaseOrder.State.ToPascalCase();

                                if (purchaseOrderLines.HasItems())
                                {
                                    ocerraPurchaseOrder.PurchaseOrderLines = ocerraPurchaseOrder.PurchaseOrderLines ?? new List<PurchaseOrderLineModel>();

                                    var counter = 1;
                                    foreach (var odooLine in purchaseOrderLines)
                                    {
                                        if (odooLine == null) continue;

                                        var poLine = ocerraPurchaseOrder?.PurchaseOrderLines?.FirstOrDefault(l => l.ExternalId == odooLine.Id.ToString())
                                            ?? new PurchaseOrderLineModel
                                            {
                                                ExternalId = odooLine.Id.ToString(),
                                            };

                                        poLine.Code = odooLine.ProductId.Value.Trim(50);
                                        poLine.Cost = (double?)(decimal?)odooLine.Price_Subtotal ?? 0;
                                        poLine.Quantity = (double?)(decimal?)odooLine.Product_Qty ?? 1;
                                        poLine.Description = odooLine.Name.Trim(250);
                                        poLine.ItemCodeId = odooLine.ProductId?.Key != null ?
                                            (await ocerraClient.ApiItemCodeByExternalGetAsync(odooLine.ProductId.Key.ToString()))?.FirstOrDefault()?.ItemCodeId : null;
                                        poLine.Sequence = counter;
                                        poLine.Attributes = new OdooPurchaseOrderLineAttributes
                                        {
                                            DivisionId = odooLine?.Attribute1?.Key,
                                            DivisionCode = odooLine?.Attribute1?.Value,
                                            BrandId = odooLine?.Attribute2?.Key,
                                            BrandCode = odooLine?.Attribute2?.Value,
                                            TaxId = odooLine?.Taxes?.Key
                                        }.ToJson();

                                        if (poLine.PurchaseOrderLineId == Guid.Empty)
                                        {
                                            poLine.PurchaseOrderLineId = Guid.NewGuid();
                                            poLine.IsNew = true;
                                            ocerraPurchaseOrder.PurchaseOrderLines.Add(poLine);
                                        }

                                        counter++;
                                    }

                                    var poLinesForDelete = ocerraPurchaseOrder.PurchaseOrderLines.HasItems() ?
                                        ocerraPurchaseOrder.PurchaseOrderLines.Where(l => !purchaseOrderLines.Any(ol => ol.Id.ToString() == l.ExternalId)).ToList() : null;
                                    if (poLinesForDelete.HasItems())
                                        poLinesForDelete.ForEach(l => l.Delete = true);
                                }

                                await ocerraClient.ApiPurchaseOrderByIdPutAsync(ocerraPurchaseOrder.PurchaseOrderHeaderId, ocerraPurchaseOrder);

                                result.UpdatedItems++;
                            }
                        }
                        catch (Exception ex) {
                            ex.LogError($"Error on PO Sync for POId:{odooPurchaseOrder.Id}, PONO:{odooPurchaseOrder.Name}");
                            result.Message = "There was an error on vendor import procedure";
                            ignored++;
                        }
                        
                    }

                result.Message = "PurchaseOrders imported succsessfuly. Skipped: " + ignored;
                return result;
            }
            catch (Exception ex)
            {
                ex.LogError("Error on PurchaseOrder Import");
                result.Message = "There was an error on vendor import procedure";
                return result;
            }
        }

        public async Task<ImportResult> ImportJobPurchaseOrders(DateTime lastSyncDate)
        {
            var result = new ImportResult();

            PurchaseOrderHeaderModel current = null;
            var exp = new Regex(@"\<(.+?)\>", RegexOptions.IgnoreCase);

            try
            {
                await Init();

                var filter = new OdooDomainFilter().Filter("write_date", ">", lastSyncDate);

                var fieldNames = new[] {
                    "id",
                    "approval_rejected_message",
                    "approval_stage",
                    "company_id",
                    "date_order",
                    "deliver_to_job",
                    "delivery_address_id",
                    "is_estimate",
                    "job_service_id",
                    "job_service_transaction_id",
                    "message_follower_ids",
                    "message_ids",
                    "name",
                    "note",
                    "notes",
                    "origin_jpo_id",
                    "packing_slip_number",
                    "show_contact",
                    "state",
                    "supplier_address",
                    "supplier_id",
                    "supplier_ref",
                    "warehouse",
                };


                var odooPurchaseOrders = new List<OdooJobPurchaseOrder>();
                var odooPurchaseOrderLines = new List<OdooJobPurchaseOrderLine>();
                var odooJobs = new List<OdooJob>();
                var odooJobStages = new List<OdooJobStage>();
                
                for (int x = 0; x < 100; x++)
                {

                    var odooPurchaseOrderIds = await odooClient.Search<long[]>(new OdooSearchParameters("job.service.purchase_order",
                        filter), new OdooPaginationParameters(x * 50, 50));

                    if (!odooPurchaseOrderIds.HasItems())
                        break;

                    var odooPurchaseOrderPage = odooPurchaseOrderIds.HasItems() ?
                        await odooClient.Get<OdooJobPurchaseOrder[]>(new OdooGetParameters("job.service.purchase_order", odooPurchaseOrderIds),
                            new OdooFieldParameters(fieldNames)) : null;

                    odooPurchaseOrders.AddRange(odooPurchaseOrderPage);

                    var odooJobIds = odooPurchaseOrderPage.HasItems() ? odooPurchaseOrderPage.Select(po => po.JobServiceId?.Key ?? 0).Where(i => i > 0).ToList() : null;
                    var odooJobPage = odooJobIds.HasItems() ? await odooClient.Get<OdooJob[]>(new OdooGetParameters("job.service.job", odooJobIds), new OdooFieldParameters(new[] {
                        "id", "attribute1", "display_name"
                    })) : null;

                    if(odooJobPage != null)
                        odooJobs.AddRange(odooJobPage);

                    if (odooJobPage.HasItems())
                    {
                        var filterJobStages = new OdooDomainFilter();

                        for (int jc = 0; jc < odooJobPage.Length - 1; jc++)
                            filterJobStages.Or();

                        foreach (var odooJob in odooJobPage)
                            filterJobStages = filterJobStages.Filter("job_service_id", "=", odooJob.Id);

                        var odooJobStagePage = odooJobIds.HasItems() ? await odooClient.Get<OdooJobStage[]>(new OdooSearchParameters("job.service.stage", filterJobStages),
                            new OdooFieldParameters(new[] { "id", "job_service_id", "name" })) : null;

                        if (odooJobStagePage != null)
                            odooJobStages.AddRange(odooJobStagePage);
                    }

                    var lineFieldNames = new[] {
                            "cost_currency_id",
                            "cost_price",
                            "cost_price_default",
                            "cost_price_special",
                            "create_date",
                            "curr_cost_price",
                            "curr_ex_cost",
                            "date_last_changed",
                            "description",
                            "discount",
                            "extended_cost",
                            "extended_sell",
                            "id",
                            "method",
                            "move_type",
                            "note",
                            "product_code",
                            "sell_price",
                            "show",
                            "signed_quantity",
                            "state",
                            "supplier_id",
                            "transaction_type",
                            "warehouse_id"
                        };

                    var jobServiceTransactionIds = odooPurchaseOrderPage.Where(po => po.JobServiceTransactionId != null).SelectMany(po => po.JobServiceTransactionId).ToList();
                    var odooPurchaseOrderLinePage = jobServiceTransactionIds.HasItems() ?
                            await odooClient.Get<OdooJobPurchaseOrderLine[]>(new OdooGetParameters("job.service.transaction", jobServiceTransactionIds),
                                new OdooFieldParameters(lineFieldNames)) : null;
                    if (odooPurchaseOrderLinePage != null)
                        odooPurchaseOrderLines.AddRange(odooPurchaseOrderLinePage);
                }

                int ignored = 0;

                if (odooPurchaseOrders.HasItems())
                    foreach (var odooPurchaseOrder in odooPurchaseOrders)
                    {
                        var externalId = "JSPO" + odooPurchaseOrder.Id.ToString();
                        var purchaseOrderLines = odooPurchaseOrderLines?.Where(l => odooPurchaseOrder.JobServiceTransactionId?.Any(li => li == l.Id) ?? false).ToList();
                        var firstLine = purchaseOrderLines?.FirstOrDefault();

                        var ocerraPurchaseOrder = await ocerraClient.ApiPurchaseOrderExternalByIdGetAsync(externalId);
                        var vendor = odooPurchaseOrder.SupplierId?.Key != null ? await ocerraClient.ApiVendorsExternalByIdGetAsync(odooPurchaseOrder.SupplierId.Key.ToString()) : null;
                        
                        var currency = firstLine?.CostCurrencyId?.Key != null ? 
                                Bootstrapper.OcerraModel.CurrencyCodes.Find(cc => cc.ExternalId == firstLine?.CostCurrencyId?.Key?.ToString())
                                    : Bootstrapper.OcerraModel.CurrencyCodes.Find(cc => cc.IsDefault);

                        var messages = odooPurchaseOrder.MessageIds.HasItems() ?
                            await odooClient.Get<OdooMessage[]>(new OdooGetParameters("mail.message", odooPurchaseOrder.MessageIds),
                                new OdooFieldParameters()) : null;

                        var firstMessage = messages?.LastOrDefault();

                        var purchaserName = firstMessage?.Author_Id?.Value;
                        var purchaserEmailGroups = firstMessage?.Email_From != null ? exp.Match(firstMessage.Email_From).Groups : null;
                        var purchaserEmail = purchaserEmailGroups != null && purchaserEmailGroups.Count > 1 ? purchaserEmailGroups[1]?.Value : null;
                        var odooUser = await GetOdooUserByName(purchaserName);

                        purchaserEmail = odooUser?.email?.Value != null && odooUser.email.Value.Contains("@") ? odooUser.email.Value :
                            odooUser?.login != null && odooUser.login.Contains("@") ? odooUser.login :
                            purchaserEmail;

                        if (vendor == null)
                        {
                            ignored++;
                            continue;
                        }

                        var reference = !string.IsNullOrEmpty(odooPurchaseOrder?.Notes?.Value) ? 
                            odooPurchaseOrder?.JobServiceId?.Value.Trim(25) + " : " + odooPurchaseOrder.Notes.Value.Trim(50) : 
                                odooPurchaseOrder?.JobServiceId?.Value.Trim(255);
                        var approvedDate = firstLine?.State?.ToLower()?.Contains("confirmed") ?? false ? (DateTime?)DateTime.Now : null;
                        var currencyCodeId = currency?.CurrencyCodeId ?? Bootstrapper.OcerraModel.CurrencyCodes.Find(cc => cc.IsDefault).CurrencyCodeId;
                        var odooJob = odooJobs != null ? odooJobs.FirstOrDefault(j => j.Id == odooPurchaseOrder?.JobServiceId?.Key) : null;
                        var odooStage = odooJobStages != null && odooJob != null ? odooJobStages.FirstOrDefault(js => js.JobServiceId?.Key == odooJob.Id) : null;

                        if (ocerraPurchaseOrder == null)
                        {
                            var purchaseOrder = new PurchaseOrderHeaderModel()
                            {
                                PurchaseOrderHeaderId = Guid.NewGuid(),
                                VendorId = vendor.VendorId,
                                Number = odooPurchaseOrder.Name,
                                ExternalId = externalId,
                                ApprovedDate = approvedDate,
                                CurrencyCodeId = currencyCodeId,
                                DocDate = firstLine?.CreateDate?.Value ?? DateTime.Now,
                                IsTaxInclusive = false,
                                Total = (double?)firstLine?.ExtendedCost?.Value ?? 0,
                                OutstandingCost = (double?)firstLine?.ExtendedCost?.Value ?? 0,
                                PurchaserId = odooUser?.Id.ToString(),
                                PurchaserName = purchaserName,
                                PurchaserEmail = purchaserEmail,
                                Status = odooPurchaseOrder.State.ToPascalCase(),
                                Reference = reference,
                                Attributes = new OdooPurchaseOrderDetails
                                {
                                    JobServiceId = odooPurchaseOrder?.JobServiceId?.Key,
                                    Warehouse = odooPurchaseOrder?.Warehouse?.Key,
                                    Reference = odooPurchaseOrder?.JobServiceId?.Value
                                }.ToJson(),
                            };

                            current = purchaseOrder;

                            if (purchaseOrderLines.HasItems())
                            {
                                purchaseOrder.PurchaseOrderLines = new List<PurchaseOrderLineModel>();

                                var counter = 1;
                                foreach (var odooLine in purchaseOrderLines)
                                {
                                    var poLine = new PurchaseOrderLineModel
                                    {
                                        PurchaseOrderLineId = Guid.NewGuid(),
                                        IsNew = true,
                                        ExternalId = odooLine.Id.ToString(),
                                        Code = odooLine.ProductCode,
                                        Cost = (double?)odooLine.ExtendedCost?.Value ?? 0,
                                        Quantity = (double?)odooLine.SignedQuantity?.Value ?? 1,
                                        Description = odooLine.Description.Trim(255),
                                        ItemCodeId = odooLine.ProductCode != null ? GetItemCodeByExternalId(odooLine.ProductCode)?.ItemCodeId : null,
                                        Sequence = counter,
                                        Attributes = new OdooPurchaseOrderLineAttributes
                                        {
                                            MoveType = odooLine.MoveType,
                                            ProcurementMethod = odooLine.Method,
                                            DivisionId = odooJob?.Division?.Key,
                                            DivisionCode = odooJob?.Division?.Value,
                                            StageId = odooStage?.Id,
                                            StageName = odooStage.Name?.Value
                                        }.ToJson()
                                    };

                                    purchaseOrder.PurchaseOrderLines.Add(poLine);

                                    counter++;
                                }
                            }

                            await ocerraClient.ApiPurchaseOrderPostAsync(purchaseOrder);

                            result.NewItems++;
                        }
                        else
                        {
                            current = ocerraPurchaseOrder;

                            ocerraPurchaseOrder.Number = odooPurchaseOrder.Name;
                            ocerraPurchaseOrder.VendorId = vendor.VendorId;
                            ocerraPurchaseOrder.ExternalId = externalId;
                            ocerraPurchaseOrder.ApprovedDate = approvedDate;
                            ocerraPurchaseOrder.CurrencyCodeId = currencyCodeId;
                            ocerraPurchaseOrder.DocDate = firstLine?.CreateDate?.Value ?? DateTime.Now;
                            ocerraPurchaseOrder.IsTaxInclusive = false;
                            ocerraPurchaseOrder.Total = (double?)firstLine?.ExtendedCost?.Value ?? 0;
                            ocerraPurchaseOrder.OutstandingCost = (double?)firstLine?.ExtendedCost?.Value ?? 0;
                            ocerraPurchaseOrder.PurchaserId = odooUser?.Id.ToString();
                            ocerraPurchaseOrder.PurchaserName = purchaserName;
                            ocerraPurchaseOrder.PurchaserEmail = purchaserEmail;
                            ocerraPurchaseOrder.Reference = reference;
                            ocerraPurchaseOrder.Attributes = new OdooPurchaseOrderDetails
                            {
                                JobServiceId = odooPurchaseOrder?.JobServiceId?.Key,
                                Warehouse = odooPurchaseOrder?.Warehouse?.Key,
                                Reference = odooPurchaseOrder?.JobServiceId?.Value,

                            }.ToJson();

                            ocerraPurchaseOrder.Status = odooPurchaseOrder.State.ToPascalCase();

                            if (purchaseOrderLines.HasItems())
                            {
                                var counter = 1;
                                foreach (var odooLine in purchaseOrderLines)
                                {
                                    var poLine = ocerraPurchaseOrder.PurchaseOrderLines?.FirstOrDefault(l => l.ExternalId == odooLine.Id.ToString())
                                        ?? new PurchaseOrderLineModel
                                        {
                                            ExternalId = odooLine.Id.ToString(),
                                        };

                                    poLine.ExternalId = odooLine.Id.ToString();
                                    poLine.Code = odooLine.ProductCode;
                                    poLine.Cost = (double?)odooLine.ExtendedCost?.Value ?? 0;
                                    poLine.Quantity = (double?)odooLine.SignedQuantity?.Value ?? 1;
                                    poLine.Description = odooLine.Description;
                                    poLine.ItemCodeId = odooLine.ProductCode != null ? GetItemCodeByExternalId(odooLine.ProductCode)?.ItemCodeId : null;
                                    poLine.Sequence = counter;
                                    poLine.Attributes = new OdooPurchaseOrderLineAttributes
                                    {
                                        MoveType = odooLine.MoveType,
                                        ProcurementMethod = odooLine.Method,
                                        DivisionId = odooJob?.Division?.Key,
                                        DivisionCode = odooJob?.Division?.Value,
                                        StageId = odooStage?.Id,
                                        StageName = odooStage.Name?.Value
                                    }.ToJson();

                                    if (poLine.PurchaseOrderLineId == Guid.Empty)
                                    {
                                        poLine.PurchaseOrderLineId = Guid.NewGuid();
                                        poLine.IsNew = true;
                                        ocerraPurchaseOrder.PurchaseOrderLines.Add(poLine);
                                    }

                                    counter++;
                                }

                                var poLinesForDelete = ocerraPurchaseOrder.PurchaseOrderLines.HasItems() ?
                                    ocerraPurchaseOrder.PurchaseOrderLines.Where(l => !purchaseOrderLines.Any(ol => ol.Id.ToString() == l.ExternalId)).ToList() : null;
                                if (poLinesForDelete.HasItems())
                                    poLinesForDelete.ForEach(l => l.Delete = true);
                            }

                            await ocerraClient.ApiPurchaseOrderByIdPutAsync(ocerraPurchaseOrder.PurchaseOrderHeaderId, ocerraPurchaseOrder);

                            result.UpdatedItems++;
                        }
                    }

                result.Message = "PurchaseOrders imported succsessfuly. Skipped: " + ignored;
                return result;
            }
            catch (Exception ex)
            {
                ex.LogError("Error on Job PurchaseOrder Import");
                result.Message = "There was an error on Job Purchase Order import procedure";
                return result;
            }
        }

        Dictionary<string, TaxAccountModel> glAccountsCache = new Dictionary<string, TaxAccountModel>();
        private async Task<TaxAccountModel> GetTaxAccountByExternalId(string externalId) {

            if (!string.IsNullOrEmpty(externalId)) {
                if (glAccountsCache.ContainsKey(externalId))
                    return glAccountsCache[externalId];

                var defaultAccount = await ocerraClient.ApiTaxAccountExternalByIdGetAsync(externalId);
                glAccountsCache[externalId] = defaultAccount;
                return defaultAccount;
            }

            return null;
        }

        Dictionary<long, OdooUser> odooUsersCache = new Dictionary<long, OdooUser>();
        private async Task<OdooUser> GetOdooUserById(long? odooUserId)
        {
            if (odooUserId.HasValue && odooUserId > 100)
            {
                if (odooUsersCache.ContainsKey(odooUserId.Value))
                    return odooUsersCache[odooUserId.Value];

                try
                {
                    var odooUsers = await odooClient.Get<OdooUser[]>(new OdooGetParameters("res.users",
                    new[] { odooUserId.Value }), new OdooFieldParameters());

                    if (odooUsers != null && odooUsers.HasItems())
                    {
                        odooUsersCache[odooUsers[0].Id] = odooUsers[0];
                        return odooUsers[0];
                    }
                }
                catch { }
                
            }

            return null;
        }

        Dictionary<string, OdooUser> odooUserByNameCache = new Dictionary<string, OdooUser>();
        private async Task<OdooUser> GetOdooUserByName(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                if (odooUserByNameCache.ContainsKey(name))
                    return odooUserByNameCache[name];

                try
                {
                    var odooUsers = await odooClient.Get<OdooUser[]>(new OdooSearchParameters("res.users", 
                        new OdooDomainFilter().Filter("name", "=", name)), new OdooFieldParameters(new[] {
                            "id", "name", "login", "email"
                        }));

                    if (odooUsers != null && odooUsers.HasItems())
                    {
                        odooUserByNameCache[name] = odooUsers[0];
                        return odooUsers[0];
                    }
                }
                catch { }

            }

            return null;
        }

        Dictionary<string, ODataClient.Proxies.ItemCode> itemCodeByExternalIdCache = new Dictionary<string, ODataClient.Proxies.ItemCode>();
        private ODataClient.Proxies.ItemCode GetItemCodeByExternalId(string externalId)
        {
            if (!string.IsNullOrEmpty(externalId))
            {
                if (itemCodeByExternalIdCache.ContainsKey(externalId))
                    return itemCodeByExternalIdCache[externalId];

                try
                {
                    var itemCode = odata.ItemCode.Where(ic => ic.Code == externalId).Take(1).FirstOrDefault();
                    if (itemCode != null)
                        itemCodeByExternalIdCache[externalId] = itemCode;
                    return itemCode;
                }
                catch { }

            }

            return null;
        }
        public async Task<ImportResult> ImportProducts(DateTime lastSyncDate)
        {
            var result = new ImportResult();

            OdooProductCategory[] odooProductCategories = null;

            async Task<bool> SyncItemCodePage(int pageNum) {

                odooProductCategories = odooProductCategories ?? await odooClient.GetAll<OdooProductCategory[]>("product.category", new OdooFieldParameters(new[] {
                    "id", "name", "property_account_expense_categ", "property_stock_account_input_categ" }));

                var filter = new OdooDomainFilter().Filter("write_date", ">=", lastSyncDate);

                var odooProductIds = await odooClient.Search<long[]>(new OdooSearchParameters("product.product",
                    filter), new OdooPaginationParameters(pageNum * 1000, 1000));

                var fieldNames = new[] {
                    "id",
                    "name",
                    "default_code",
                    "currency_price",
                    "active",
                    "categ_id",
                    "type"
                };

                var odooProducts = odooProductIds.HasItems() ?
                    await odooClient.Get<OdooProduct[]>(new OdooGetParameters("product.product", odooProductIds),
                        new OdooFieldParameters(fieldNames)) : null;

                if (odooProducts.HasItems())
                {
                    var odooProductBatches = odooProducts.ToBatches(100);
                    
                    foreach (var odooProductBatch in odooProductBatches) {

                        var odooProductPage = odooProductBatch.ToList();
                        var extrnalIds = string.Join(",", odooProductPage.Select(b => b.Id));
                        var ocerraProducts = await ocerraClient.ApiItemCodeByExternalGetAsync(extrnalIds);
                        
                        var itemsToPut = new List<ItemCodeModel>();
                        var itemsToPost = new List<ItemCodeModel>();

                        foreach (var odooProduct in odooProductPage)
                        {
                            var ocerraProduct = ocerraProducts.FirstOrDefault(p => p.ExternalId == odooProduct.Id.ToString());
                            
                            var glAccount = odooProduct.Type == "product" ?
                                await GetTaxAccountByExternalId(odooProduct.PropertyStockAccountInput?.Key?.ToString()) : 
                                await GetTaxAccountByExternalId(odooProduct.PropertyAccountExpense?.Key?.ToString()); 

                            var productCategory = odooProductCategories.FirstOrDefault(c => c.Id == odooProduct.ProductCategory?.Key);

                            var productAccountId = odooProduct.Type == "product" ?
                                productCategory?.PropertyStockAccountInput?.Key?.ToString() :
                                    productCategory?.PropertyAccountExpense?.Key?.ToString();
                            
                            var glAccountByCat = await GetTaxAccountByExternalId(productAccountId); //Using Expense or stock account?
                            var glAccountId = glAccount?.TaxAccountId ?? glAccountByCat?.TaxAccountId;

                            if (ocerraProduct == null)
                            {

                                var itemCode = new ItemCodeModel()
                                {
                                    ItemCodeId = Guid.NewGuid(),
                                    ClientId = Bootstrapper.OcerraModel.ClientId,
                                    Code = odooProduct.DefaultCode.Trim(255),
                                    Description = odooProduct.Name.Trim(255),
                                    ExternalId = odooProduct.Id.ToString(),
                                    TaxAccountId = glAccountId,
                                    IsActive = true
                                };

                                //await ocerraClient.ApiItemCodePostAsync(itemCode); - using Bulk insert instead
                                itemsToPost.Add(itemCode);

                                result.NewItems++;
                            }
                            //Update only changed items
                            else if (ocerraProduct.Code != odooProduct.DefaultCode.Trim(255) ||
                                ocerraProduct.Description != odooProduct.Name.Trim(255) || 
                                ocerraProduct.TaxAccountId != glAccountId)
                            {
                                ocerraProduct.Code = odooProduct.DefaultCode.Trim(255);
                                ocerraProduct.Description = odooProduct.Name.Trim(255);
                                ocerraProduct.TaxAccountId = glAccountId;
                                ocerraProduct.IsActive = true;

                                //await ocerraClient.ApiItemCodeByIdPutAsync(ocerraProduct.ItemCodeId, ocerraProduct); - using Bulk update instead
                                itemsToPut.Add(ocerraProduct);
                                
                                result.UpdatedItems++;
                            }
                            else
                            {
                                result.UpdatedItems++;
                            }
                        }

                        if (itemsToPost.HasItems())
                            await ocerraClient.ApiItemCodeBulkPostAsync(itemsToPost);
                        if (itemsToPut.HasItems())
                            await ocerraClient.ApiItemCodeBulkPutAsync(itemsToPut);

                    }
                    
                    return true;
                }

                else return false;
            }

            try
            {
                await Init();

                for(int x = 0; x < 100; x++)
                {
                    var hasItems = await SyncItemCodePage(x);
                    if (!hasItems) break;
                }
                
                result.Message = "Products imported succsessfuly.";
                return result;
            }
            catch (Exception ex)
            {
                ex.LogError("Error on Product Import");
                result.Message = "There was an error on product import procedure";
                return result;
            }
        }

        public async Task<ImportResult> ImportPayments(DateTime lastSyncDate)
        {
            var result = new ImportResult();

            var regex = new Regex(@"(JSPO\d+|PO\d+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            try
            {
                await Init();

                var filter = new OdooDomainFilter().Filter("write_date", ">=", lastSyncDate).Filter("type", "=", "in_invoice");

                var odooInvoices = await odooClient.Get<OdooBill[]>(new OdooSearchParameters("account.invoice",
                    filter), new OdooPaginationParameters(0, 1000));

                var invoicesForUpdates = new List<ODataClient.Proxies.VoucherHeader>();

                if (odooInvoices.HasItems())
                {
                    foreach (var odooInvoice in odooInvoices.OrderBy(io => io.Id))
                    {
                        var externalId = odooInvoice.Id.ToString();
                        var externalVendorId = odooInvoice.PartnerId?.Key?.ToString();
                        var number = odooInvoice.SupplierInvoiceNumberV8?.Value;
                        number = number != null && number.Length > 2 ? number : null;
                        var purchaseNumberMatch = odooInvoice.Origin != null ? regex.Match(odooInvoice.Origin) : null;
                        var purchaseNumber = purchaseNumberMatch?.Value;

                        var ocerraInvoicesByExternalId = externalId != null ?
                            odata.VoucherHeader.Where(vh => vh.ExternalId == externalId || vh.Extra1 == externalId).Take(20).ToList() : null; //exclude duplicate matching

                        var ocerraInvoicesByNumber = !ocerraInvoicesByExternalId.HasItems() && externalVendorId != null && number != null ?
                            odata.VoucherHeader.Where(vh => vh.Vendor.ExternalId == externalVendorId && vh.Number == number && (vh.Extra1 == null || vh.Extra1 == "")).Take(20).ToList() : null;

                        var ocerraInvoicesByPurchaseOrder = !ocerraInvoicesByNumber.HasItems() && externalVendorId != null && purchaseNumber != null && number == null ?
                            odata.VoucherHeader
                                .Where(vh => vh.Vendor.ExternalId == externalVendorId && vh.PurchaseOrderHeader.Number == purchaseNumber && (vh.Extra1 == null || vh.Extra1 == ""))
                                .OrderBy(vh => vh.CreatedDate)
                                .Take(20)
                                .ToList() : null;


                        if (ocerraInvoicesByExternalId.HasItems())
                            foreach (var invoiceById in ocerraInvoicesByExternalId)
                            {
                                var odooInvoiceTxt = $"{odooInvoice.SupplierInvoiceNumberV8} / {odooInvoice.AmountTotalV8?.Value}";
                                var ocerraInvoiceTxt = $"{invoiceById.Number} / {invoiceById.FcGross}";
                                await UpdateInvoiceFields(invoicesForUpdates, odooInvoice, invoiceById, true);
                            }

                        if (ocerraInvoicesByNumber.HasItems())
                            foreach (var invoiceByNumber in ocerraInvoicesByNumber)
                            {
                                var odooInvoiceTxt = $"{odooInvoice.SupplierInvoiceNumberV8} / {odooInvoice.AmountTotalV8?.Value}";
                                var ocerraInvoiceTxt = $"{invoiceByNumber.Number} / {invoiceByNumber.FcGross}";
                                await UpdateInvoiceFields(invoicesForUpdates, odooInvoice, invoiceByNumber, true);
                            }

                        //search invoice by amount
                        if (ocerraInvoicesByPurchaseOrder.HasItems())
                        {
                            var matchingInvoice = ocerraInvoicesByPurchaseOrder
                                .OrderBy(oi => Math.Abs((oi.FcGross ?? 0) - (odooInvoice.AmountTotalV8?.Value ?? 0)))
                                    .ThenBy(oi => oi.CreatedDate)
                                .FirstOrDefault();
                            if (matchingInvoice != null)
                            {
                                var odooInvoiceTxt = $"{odooInvoice.SupplierInvoiceNumberV8} / {odooInvoice.AmountTotalV8?.Value}";
                                var ocerraInvoiceTxt = $"{matchingInvoice.Number} / {matchingInvoice.FcGross}";
                                await UpdateInvoiceFields(invoicesForUpdates, odooInvoice, matchingInvoice, false);
                            }
                        }
                    }
                }

                //Find 100 last updated invoices without OdooId-draft invoice
                var reverseSearch = odata.VoucherHeader
                    .Expand("PurchaseOrderHeader($select=PurchaseOrderHeaderId,Number)")
                    .Where(vh => vh.VendorId != null && vh.PurchaseOrderHeaderId != null && vh.UpdatedDate > lastSyncDate)
                    .OrderBy(vh => vh.CreatedDate)
                    .Take(100)
                    .ToList();

                //Reverse search by PO
                foreach (var reverseInvoice in reverseSearch.Where(i => i.Extra1 == null || i.Extra1 == ""))
                {
                    var odooInvoiceFilter = new OdooDomainFilter()
                        .Filter("origin", "ilike", reverseInvoice.PurchaseOrderHeader.Number)
                        .Filter("type", "=", "in_invoice");

                    var odooInvoicesByOrigin = await odooClient.Get<OdooBill[]>(new OdooSearchParameters("account.invoice",
                        odooInvoiceFilter), new OdooPaginationParameters(0, 10));

                    if (odooInvoicesByOrigin.HasItems())
                    {
                        var odooInvoiceByOrigin =
                            odooInvoicesByOrigin.FirstOrDefault(ob => ob.SupplierInvoiceNumberV8 == reverseInvoice.Number) ??
                            odooInvoicesByOrigin
                                .Where(ob => !invoicesForUpdates.Any(i=>i.Extra1 == ob.Id.ToString()))
                                .OrderBy(ob => Math.Abs((ob.AmountTotalV8.Value ?? 0) - (reverseInvoice.FcNet ?? 0)))
                                .FirstOrDefault(ob => (string.IsNullOrEmpty(ob.SupplierInvoiceNumberV8) || ob.SupplierInvoiceNumberV8.Value.Length < 2) && ob.State == "draft");

                        if (odooInvoiceByOrigin != null)
                        {
                            var odooInvoiceTxt = $"{odooInvoiceByOrigin.SupplierInvoiceNumberV8} / {odooInvoiceByOrigin.AmountTotalV8?.Value}";
                            var ocerraInvoiceTxt = $"{reverseInvoice.Number} / {reverseInvoice.FcGross}";
                            await UpdateInvoiceFields(invoicesForUpdates, odooInvoiceByOrigin, reverseInvoice, false);
                        }
                    }
                }

                //Reverse search by ExternalId
                var odooBillIds = reverseSearch.Where(i => !string.IsNullOrEmpty(i.Extra1) || !string.IsNullOrEmpty(i.ExternalId))
                    .Select(i => i.ExternalId != null ? i.ExternalId.ToLong(0) : i.Extra1.ToLong(0)).ToArray();

                var reverseOdooBills = odooBillIds.HasItems() ? await odooClient.Get<OdooBill[]>(new OdooGetParameters("account.invoice", odooBillIds)) : null;

                if(reverseOdooBills != null)
                    foreach (var reverseOdooInvoice in reverseOdooBills)
                    {
                        var reverseInvoice = reverseSearch
                            .FirstOrDefault(i => i.ExternalId == reverseOdooInvoice.Id.ToString() || i.Extra1 == reverseOdooInvoice.Id.ToString());

                        if (reverseInvoice != null)
                        {
                            var odooInvoiceTxt = $"{reverseOdooInvoice.SupplierInvoiceNumberV8} / {reverseOdooInvoice.AmountTotalV8?.Value}";
                            var ocerraInvoiceTxt = $"{reverseInvoice.Number} / {reverseInvoice.FcGross}";
                            await UpdateInvoiceFields(invoicesForUpdates, reverseOdooInvoice, reverseInvoice, false);
                        }
                    }

                invoicesForUpdates = invoicesForUpdates.Distinct().ToList();

                result.UpdatedItems = invoicesForUpdates.Count();
                
                //await PatchInvoices(invoicesForUpdates); - update as we go

                result.Message = "Payments imported succsessfuly: " + result.UpdatedItems;

                return result;
            }
            catch (Exception ex)
            {
                ex.LogError("Error on Payments Import");
                result.Message = "There was an error on payments import procedure";
                return result;
            }
        }

        private async Task PatchInvoices(List<VoucherHeader> invoicesForUpdates)
        {
            foreach (var invoicesForUpdate in invoicesForUpdates)
            {
                await ocerraClient.ApiVoucherHeaderByIdPatchAsync(invoicesForUpdate.VoucherHeaderId.Value, new List<Operation> {
                            new Operation
                            {
                                Path = "Extra1",
                                Op = "replace",
                                Value = invoicesForUpdate.Extra1
                            },
                            new Operation
                            {
                                Path = "Extra2",
                                Op = "replace",
                                Value = invoicesForUpdate.Extra2
                            },
                            new Operation
                            {
                                Path = "IsPaid",
                                Op = "replace",
                                Value = invoicesForUpdate.IsPaid
                            }
                        });
            }
        }

        private async Task UpdateInvoiceFields(List<VoucherHeader> invoicesForUpdates, 
            OdooBill odooInvoice, VoucherHeader invoiceById, bool updatePaidInfo)
        {
            //Ignore updated invoices
            if (invoicesForUpdates.Any(u => u.VoucherHeaderId == invoiceById.VoucherHeaderId))
                return;

            var added = false;
            if (string.IsNullOrEmpty(invoiceById.Extra1))
            {
                invoiceById.Extra1 = odooInvoice.Id.ToString(); //Copy OdooId to 
                invoicesForUpdates.Add(invoiceById);
                added = true;
            }

            if (invoiceById.Extra2 != odooInvoice.State)
            {
                invoiceById.Extra2 = odooInvoice.State;
                invoicesForUpdates.Add(invoiceById);
                added = true;
            }

            if (updatePaidInfo && odooInvoice.State == "paid" && !invoiceById.IsPaid)
            {
                invoiceById.IsPaid = true;
                invoicesForUpdates.Add(invoiceById);
                added = true;
            }

            if(added)
                await ocerraClient.ApiVoucherHeaderByIdPatchAsync(invoiceById.VoucherHeaderId.Value, new List<Operation> {
                    new Operation
                    {
                        Path = "Extra1",
                        Op = "replace",
                        Value = invoiceById.Extra1
                    },
                    new Operation
                    {
                        Path = "Extra2",
                        Op = "replace",
                        Value = invoiceById.Extra2
                    },
                    new Operation
                    {
                        Path = "IsPaid",
                        Op = "replace",
                        Value = invoiceById.IsPaid
                    }
                });
        }
    }
}
