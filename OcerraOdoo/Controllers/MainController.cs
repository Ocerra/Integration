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
                var vendorResult = await importService.ImportVendors(Model.LastVendorSyncDate.ToDate(DateTime.Now.AddDays(-1)));
                Helpers.AddUpdateAppSettings("LastVendorSyncDate", DateTime.Now.ToString("s"));
                return Response.AsJson(new { message = $"Vendors: {vendorResult.Message}, New: {vendorResult.NewItems}, Updated: {vendorResult.UpdatedItems}" });
            });

            Post("/SyncPurchaseOrders", async args => {
                var lastPoSyncDate = Model.LastPurchaseSyncDate.ToDate(DateTime.Now.AddDays(-1));
                var poResult = await importService.ImportPurchaseOrders(lastPoSyncDate);
                Helpers.AddUpdateAppSettings("LastPurchaseSyncDate", DateTime.Now.ToString("s"));
                return Response.AsJson(new { message = $"Purchase Orders: {poResult.Message}, New: {poResult.NewItems}, Updated: {poResult.UpdatedItems} from {lastPoSyncDate}" });
            });

            Post("/SyncProducts", async args => {
                var poResult = await importService.ImportProducts(Model.LastProductSyncDate.ToDate(DateTime.Now.AddDays(-1)));
                Helpers.AddUpdateAppSettings("LastProductSyncDate", DateTime.Now.ToString("s"));
                return Response.AsJson(new { message = $"Products: {poResult.Message}, New: {poResult.NewItems}, Updated: {poResult.UpdatedItems}" });
            });

            Post("/SyncInvoices", async args => {
                var poResult = await exportService.ExportInvoices(Model.LastInvoiceSyncDate.ToDate(DateTime.Now.AddDays(-1)));
                Helpers.AddUpdateAppSettings("LastInvoiceSyncDate", DateTime.Now.ToString("s"));
                return Response.AsJson(new { message = $"Bills: {poResult.Message}, New: {poResult.NewItems}, Updated: {poResult.UpdatedItems}" });
            });

            Post("/UpdateTime", args => {

                var updateType = (string)Request.Form.type;
                var updateVal = (string)Request.Form.val;

                Helpers.AddUpdateAppSettings(updateType, updateVal);

                return Response.AsJson(new { message = $"Setting was updated" });
            });

            Get("/Settings", args => {
                return View["Settings.html", Model];
            });
        }

        public override MainModel Init()
        {
            return new MainModel() { 
                LastVendorSyncDate = Helpers.AppSetting("LastVendorSyncDate").ToDate(DateTime.Now.AddDays(-1)).ToString("s"),
                LastPurchaseSyncDate = Helpers.AppSetting("LastPurchaseSyncDate").ToDate(DateTime.Now.AddDays(-1)).ToString("s"),
                LastInvoiceSyncDate = Helpers.AppSetting("LastInvoiceSyncDate").ToDate(DateTime.Now.AddDays(-1)).ToString("s"),
                LastProductSyncDate = Helpers.AppSetting("LastProductSyncDate").ToDate(DateTime.Now.AddDays(-1)).ToString("s"),
            };
        }
    }
}
