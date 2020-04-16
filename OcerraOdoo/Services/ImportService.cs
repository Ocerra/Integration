﻿using Nancy.TinyIoc;

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
                        var ocerraTax = ocerraTaxes.FirstOrDefault(t => t.ExternalId == odooTax.Id.ToString());
                        if (ocerraTax == null)
                        {
                            var taxRate = new TaxRateModel()
                            {
                                ClientId = Bootstrapper.OcerraModel.ClientId,
                                TaxRateId = Guid.NewGuid(),
                                Code = odooTax.Name,
                                Description = odooTax.Description,
                                ExternalId = odooTax.Id.ToString(),
                                ExternalAccountId = odooTax.Cash_Basis_Base_Account_Id.ToInt(null)?.ToString(),
                                IsActive = true,
                                TaxType = odooTax.Type_Tax_Use,
                                Rate = (double)odooTax.Amount
                            };
                            await ocerraClient.ApiTaxRatePostAsync(taxRate);
                            result.NewItems++;
                        }
                        else
                        {
                            ocerraTax.ExternalAccountId = odooTax.Cash_Basis_Base_Account_Id.ToInt(null)?.ToString();
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

            try
            {
                await Init();

                var odooAccounts = await odooClient.GetAll<OdooAccount[]>("account.account", new OdooFieldParameters());

                var ocerraAccounts1000 = await ocerraClient.ApiTaxAccountGetAsync(0, 1000);
                var ocerraAccounts2000 = await ocerraClient.ApiTaxAccountGetAsync(1000, 2000);
                var ocerraTaxes = await ocerraClient.ApiTaxRateGetAsync(0, 100);

                var validAccountGroups = Settings.Default.OdooAccountGroups.Split(',');

                if (odooAccounts.HasItems())
                    foreach (var odooAccount in odooAccounts.Where(a => validAccountGroups.Any(vg=> 
                        a.Internal_Group == vg || 
                        (a.Parent_Id?.Value != null && a.Parent_Id.Value.Contains(vg)) ||
                        (a.User_Type?.Value != null && a.User_Type.Value.Contains(vg))
                    ) && !a.Deprecated))
                    {
                        var ocerraAccount = ocerraAccounts1000.FirstOrDefault(t => t.ExternalId == odooAccount.Id.ToString());
                        ocerraAccount = ocerraAccount ?? ocerraAccounts2000.FirstOrDefault(t => t.ExternalId == odooAccount.Id.ToString());

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
                        "property_account_payable_id"
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
                        var defaultAccount = odooVendor.Property_Account_Payable_Id.HasItems() ? await ocerraClient.ApiTaxAccountExternalByIdGetAsync(odooVendor.Property_Account_Payable_Id[0]) : null;

                        var countryCode = odooCountryCodes.FirstOrDefault(cc => cc.Id == odooVendor.Country_Id?.FirstOrDefault().ToInt(null))?.Code?.Value
                                    ?? Bootstrapper.OcerraModel.CountryCode;

                        if (ocerraVendor == null)
                        {
                            
                            vendor = new VendorModel()
                            {
                                VendorId = Guid.NewGuid(),
                                ClientId = Bootstrapper.OcerraModel.ClientId,
                                Name = (odooVendor.Company_Name ?? odooVendor.Name).Trim(255),
                                CountryCode = countryCode,
                                Email = email.Trim(50),
                                DomainName = domain.Trim(255),
                                Description = odooVendor.Comment.Trim(512),
                                IsActive = true,
                                ExternalId = odooVendor.Id.ToString(),
                                PhoneNumber = odooVendor.Phone,
                                DefaultTaxAccountId = defaultAccount?.TaxAccountId,
                                DefaultTaxRateId = defaultAccount?.TaxRateId,
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
                            ocerraVendor.Name = (odooVendor.Company_Name ?? odooVendor.Name).Trim(255);
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
                            ocerraVendor.DefaultTaxRateId = defaultAccount?.TaxRateId;
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
                    "origin"
                };


                var odooPurchaseOrders = new List<OdooPurchaseOrder>();

                for (int x = 0; x < 100; x = x + 2) {
                    
                    var odooPurchaseOrderIds = await odooClient.Search<long[]>(new OdooSearchParameters("purchase.order",
                        filter), new OdooPaginationParameters(x * 100, 200));

                    if (!odooPurchaseOrderIds.HasItems())
                        break;

                    var odooPurchaseOrderPage = odooPurchaseOrderIds.HasItems() ?
                        await odooClient.Get<OdooPurchaseOrder[]>(new OdooGetParameters("purchase.order", odooPurchaseOrderIds),
                            new OdooFieldParameters(fieldNames)) : null;
                    
                    odooPurchaseOrders.AddRange(odooPurchaseOrderPage);
                }
                

                if (odooPurchaseOrders.HasItems())
                    foreach (var odooPurchaseOrder in odooPurchaseOrders)
                    {
                        var ocerraPurchaseOrder = await ocerraClient.ApiPurchaseOrderExternalByIdGetAsync(odooPurchaseOrder.Id.ToString());
                        var vendor = odooPurchaseOrder.Partner_Id.HasItems() ? await ocerraClient.ApiVendorsExternalByIdGetAsync(odooPurchaseOrder.Partner_Id[0].ToString()) : null;
                        var currency = odooPurchaseOrder.Currency_Id.HasItems() ? Bootstrapper.OcerraModel.CurrencyCodes.Find(cc => cc.ExternalId == odooPurchaseOrder.Currency_Id[0])
                            : Bootstrapper.OcerraModel.CurrencyCodes.Find(cc => cc.IsDefault);

                        if (vendor == null) {
                            continue;
                        }


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
                                PurchaserId = odooPurchaseOrder.User_Id.HasItems() ? odooPurchaseOrder.User_Id[0] : null,
                                PurchaserName = odooPurchaseOrder.User_Id.HasItems() ? odooPurchaseOrder.User_Id[1] : null,
                                Reference = odooPurchaseOrder.Origin.Trim(255),
                            };

                            current = purchaseOrder;

                            await ocerraClient.ApiPurchaseOrderPostAsync(purchaseOrder);

                            result.NewItems++;
                        }
                        else
                        {
                            current = ocerraPurchaseOrder;

                            ocerraPurchaseOrder.Number = odooPurchaseOrder.Name;
                            ocerraPurchaseOrder.ExternalId = odooPurchaseOrder.Id.ToString();
                            ocerraPurchaseOrder.ApprovedDate = odooPurchaseOrder.Date_Approve.ToDateOffset(null);
                            ocerraPurchaseOrder.CurrencyCodeId = currency?.CurrencyCodeId ?? Bootstrapper.OcerraModel.CurrencyCodes.Find(cc => cc.IsDefault).CurrencyCodeId;
                            ocerraPurchaseOrder.DocDate = odooPurchaseOrder.Date_Order.ToDateOffset(DateTimeOffset.Now).Value;
                            ocerraPurchaseOrder.IsTaxInclusive = odooPurchaseOrder.Amount_Tax == 0;
                            ocerraPurchaseOrder.Total = odooPurchaseOrder.Amount_Total ?? 0;
                            ocerraPurchaseOrder.OutstandingCost = odooPurchaseOrder.Amount_Total ?? 0;
                            ocerraPurchaseOrder.PurchaserId = odooPurchaseOrder.User_Id.HasItems() ? odooPurchaseOrder.User_Id[0] : null;
                            ocerraPurchaseOrder.PurchaserName = odooPurchaseOrder.User_Id.HasItems() ? odooPurchaseOrder.User_Id[1] : null;
                            ocerraPurchaseOrder.Reference = odooPurchaseOrder.Origin;
                            
                            await ocerraClient.ApiPurchaseOrderByIdPutAsync(ocerraPurchaseOrder.PurchaseOrderHeaderId, ocerraPurchaseOrder);
                            
                            result.UpdatedItems++;
                        }
                    }

                result.Message = "PurchaseOrders imported succsessfuly.";
                return result;
            }
            catch (Exception ex)
            {
                ex.LogError("Error on PurchaseOrder Import");
                result.Message = "There was an error on vendor import procedure";
                return result;
            }
        }

        public async Task<ImportResult> ImportProducts(DateTime lastSyncDate)
        {

            var result = new ImportResult();

            try
            {
                await Init();

                var filter = new OdooDomainFilter().Filter("write_date", ">=", lastSyncDate);

                var odooCountryCodes = await odooClient.GetAll<OdooCountry[]>("product.product", new OdooFieldParameters(new[] { "id", "code" }));

                var odooCompanyIds = await odooClient.Search<long[]>(new OdooSearchParameters("res.partner",
                    filter), new OdooPaginationParameters(0, 200));

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
                        "property_account_payable_id"
                    };

                var odooCompanies = odooCompanyIds.HasItems() ?
                    await odooClient.Get<OdooCompany[]>(new OdooGetParameters("res.partner", odooCompanyIds), new OdooFieldParameters()) : null;

                if (odooCompanies.HasItems())
                    foreach (var odooVendor in odooCompanies)
                    {
                        var ocerraVendor = await ocerraClient.ApiVendorsExternalByIdGetAsync(odooVendor.Id.ToString());

                        var email = odooVendor.Email?.ToLower();
                        var domain = odooVendor.Website != null && odooVendor.Website.ToUri() != null ? odooVendor.Website.ToUri().Host :
                            email != null && !Settings.Default.SharedEmailProviders.Split(',').Any(ep => email.Contains(ep)) ? email.Split('@').LastOrDefault()?.ToUri()?.Host : null;
                        var defaultAccount = odooVendor.Property_Account_Payable_Id.HasItems() ? await ocerraClient.ApiTaxAccountExternalByIdGetAsync(odooVendor.Property_Account_Payable_Id[0]) : null;

                        if (ocerraVendor == null)
                        {

                            var vendor = new VendorModel()
                            {
                                VendorId = Guid.NewGuid(),
                                ClientId = Bootstrapper.OcerraModel.ClientId,
                                Name = odooVendor.Company_Name ?? odooVendor.Name,
                                CountryCode = odooCountryCodes.FirstOrDefault(cc => cc.Id == odooVendor.Country_Id?.FirstOrDefault().ToInt(null))?.Code
                                    ?? Bootstrapper.OcerraModel.CountryCode,
                                Email = email,
                                DomainName = domain,
                                Description = odooVendor.Comment,
                                IsActive = true,
                                ExternalId = odooVendor.Id.ToString(),
                                PhoneNumber = odooVendor.Phone,
                                DefaultTaxAccountId = defaultAccount?.TaxAccountId,
                                DefaultTaxRateId = defaultAccount?.TaxRateId
                            };

                            //Auto code based on supplier country
                            vendor.CurrencyCodeId = Bootstrapper.OcerraModel.CurrencyCodes?.FirstOrDefault(cc => cc.CountryCode == vendor.CountryCode)?.CurrencyCodeId;

                            await ocerraClient.ApiVendorsPostAsync(vendor);
                            result.NewItems++;
                        }
                        else
                        {
                            ocerraVendor.Name = odooVendor.Company_Name ?? odooVendor.Name;
                            ocerraVendor.CountryCode = odooCountryCodes.FirstOrDefault(cc => cc.Id == odooVendor.Country_Id?.FirstOrDefault().ToInt(null))?.Code
                                ?? Bootstrapper.OcerraModel.CountryCode;
                            ocerraVendor.Email = email;
                            ocerraVendor.DomainName = domain;
                            ocerraVendor.Description = odooVendor.Comment;
                            ocerraVendor.IsActive = true;
                            ocerraVendor.ExternalId = odooVendor.Id.ToString();
                            ocerraVendor.PhoneNumber = odooVendor.Phone;
                            //Auto code based on supplier country
                            ocerraVendor.CurrencyCodeId = Bootstrapper.OcerraModel.CurrencyCodes?
                                .FirstOrDefault(cc => cc.CountryCode == ocerraVendor.CountryCode)?.CurrencyCodeId;
                            ocerraVendor.DefaultTaxAccountId = defaultAccount?.TaxAccountId;
                            ocerraVendor.DefaultTaxRateId = defaultAccount?.TaxRateId;

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
    }
}