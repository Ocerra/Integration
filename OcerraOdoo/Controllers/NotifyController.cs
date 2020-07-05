using Microsoft.OData.Client;
using Nancy;
using OcerraOdoo.Models;
using OcerraOdoo.OcerraOData;
using OcerraOdoo.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OcerraOdoo.Controllers
{
    public class NotifyController : Controller<NotifyModel>
    {
        private readonly ReminderService reminderService;

        public NotifyController(OdataProxy odata, ReminderService reminderService)
        {
            Get("/Reminders", args => {
                int page = int.Parse((string)Request.Query.page ?? "1");
                page = page < 1 ? 1 : page;

                string search = Request.Query.search;
                string canNotify = Request.Query.canNotify;

                var query = odata.VoucherHeader
                    .Expand("vendor,workflow($expand=workflowState),voucherValidation,purchaseOrderHeader");

                //Find all un-approved voucher headers, where PO is not approved (approved date is null)
                query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)
                    query.Where(vh => vh.IsActive && !vh.IsArchived && vh.PurchaseOrderHeaderId != null);

                if (!string.IsNullOrEmpty(search))
                    query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)query.Where(vh => vh.Number.Contains(search) || vh.Vendor.Name.Contains(search));

                if (!string.IsNullOrEmpty(canNotify)) {
                    var canNotifyBool = canNotify == "True";
                    if (canNotifyBool) {
                        query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)query.Where(vh => vh.PurchaseOrderHeader != null && vh.PurchaseOrderHeader.Status != "Done");
                    } 
                    else
                    {
                        query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)query.Where(vh => vh.PurchaseOrderHeader != null && vh.PurchaseOrderHeader.Status == "Done");
                    }
                }
                
                query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)query.OrderByDescending(vh => vh.CreatedDate).Skip((page - 1) * 20).Take(20);

                Model.Reminders = query
                .Execute()
                .Select(vh => new Reminder
                {
                    VoucherId = vh.VoucherHeaderId.ToString(),
                    DocumentId = vh.DocumentId.ToString(),
                    Vendor = vh.Vendor != null ? vh.Vendor.Name : "Unknown",
                    Number = vh.Number,
                    Date = (vh.Date ?? vh.CreatedDate).ToString("dd-MMM-yy"),
                    PONumber = vh.PurchaseOrderHeader?.Number ?? "Unknown",
                    POOriginator = vh.PurchaseOrderHeader?.PurchaserName,
                    POOriginatorEmail = vh.PurchaseOrderHeader?.PurchaserEmail,
                    POStatus = vh.PurchaseOrderHeader?.Status,
                    CanNotify = vh.PurchaseOrderHeader != null && vh.PurchaseOrderHeader?.Status != "Done" ? "" : "disabled='disabled'",
                    CanNotifyMessage =
                        vh.PurchaseOrderHeader == null ? "You cannot notify without Purchase Order" :
                        vh.PurchaseOrderHeader?.Status == "Done" ? "This purchase order has already been done" :
                        null,
                }).ToList();

                var totalCount = query.Count();

                Model.Page = page;
                Model.Count = totalCount;
                Model.SearchStr = !string.IsNullOrEmpty(search) ? search.Replace("\"", "\\\"") : null;
                
                return View["Reminders.html", Model];
            });

            Post("/NotifyPurchasersByIds", async args => {
                
                var voucherHeaderIdsStr = (string)Request.Form["voucherHeaderIds[]"];
                if (!string.IsNullOrEmpty(voucherHeaderIdsStr))
                {
                    var voucherHeaderIds = voucherHeaderIdsStr.Split(',').Select(s => Guid.Parse(s)).ToArray();

                    var result = await reminderService.RemindPurchasersByIds(voucherHeaderIds);

                    return Response.AsJson(new { message = $"Remint notification was sumitted: " + result + " times."});
                }
                else
                {

                    return Response.AsJson(new { message = "You have not selected any invoices for remind." });
                }

            });
            this.reminderService = reminderService;
        }



        public override NotifyModel Init()
        {
            return new NotifyModel();
        }
    }
}
