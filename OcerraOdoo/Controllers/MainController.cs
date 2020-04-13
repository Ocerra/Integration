using Nancy;
using Nancy.Security;
using OcerraOdoo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nancy.Extensions;
using OcerraOdoo.Services;
using OcerraOdoo.Properties;

namespace OcerraOdoo.Controllers
{
    public class MainController : Controller<MainModel>
    {
        public MainController(ImportService importService, ExportService exportService)
        {
            Get("/", args => View["Index.html", Model]);

            Post("/Echo", args => {
                return Response.AsJson(new { message = "Echo: " + Request.Form.message });
            });

            Post("/SyncChartOfAccounts", async args => {
                var currencyResult = await importService.ImportCurrency();
                var taxResult = await importService.ImportTaxes();
                var accountResult = await importService.ImportAccounts();

                return Response.AsJson(new { message = $"Currency: {currencyResult.Message}, Tax: {taxResult.Message}, Accounts: {accountResult.Message}" });
            });

            Post("/SyncVendors", async args => {
                var vendorResult = await importService.ImportVendors(Settings.Default.LastVendorSyncDate.ToDate(DateTime.Now.AddMonths(-3)));
                
                return Response.AsJson(new { message = $"Vendors: {vendorResult.Message}, New: {vendorResult.NewItems}, Updated: {vendorResult.UpdatedItems}" });
            });

            Post("/SyncPurchaseOrders", async args => {
                var poResult = await importService.ImportPurchaseOrders(Settings.Default.LastPurchaseSyncDate.ToDate(DateTime.Now.AddMonths(-3)));

                return Response.AsJson(new { message = $"Purchase Orders: {poResult.Message}, New: {poResult.NewItems}, Updated: {poResult.UpdatedItems}" });
            });

            Post("/SyncInvoices", async args => {
                var poResult = await exportService.ExportInvoices(Settings.Default.LastPurchaseSyncDate.ToDate(DateTime.Now.AddMonths(-3)));

                return Response.AsJson(new { message = $"Bills: {poResult.Message}, New: {poResult.NewItems}, Updated: {poResult.UpdatedItems}" });
            });
        }

        public override MainModel Init()
        {
            return new MainModel() { 
                
            };
        }
    }
}
