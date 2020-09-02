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
                string exportState = Request.Query.exportState;
                string state = Request.Query.state;
                string poState = Request.Query.poState;
                string poMatches = Request.Query.poMatches;
                string reminded = Request.Query.reminded;

                var query = odata.VoucherHeader
                    .Expand("vendor,workflow($expand=workflowState),voucherValidation,purchaseOrderHeader");

                //Find all un-approved voucher headers, where PO is not approved (approved date is null)
                query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)
                    query.Where(vh => vh.IsActive && !vh.IsArchived && vh.PurchaseOrderHeaderId != null);

                if (IsDefined(search))
                    query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)query.Where(vh => vh.Number.Contains(search) || vh.Vendor.Name.Contains(search));

                if (exportState == "Yes")
                    query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)query.Where(vh => vh.ExternalId != null);

                if (exportState == "No")
                    query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)query.Where(vh => vh.ExternalId == null);

                if (poMatches == "Yes")
                    query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)query.Where(vh => vh.VoucherValidation.HasPoMatches == "Success");

                if (poMatches == "No")
                    query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)query.Where(vh => vh.VoucherValidation.HasPoMatches == "Fail" || vh.VoucherValidation.HasPoMatches == "Missing");

                if (IsDefined(state))
                    query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)query.Where(vh => vh.Workflow.WorkflowState.Name == state);

                if (IsDefined(poState))
                    query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)query.Where(vh => vh.PurchaseOrderHeader.Status == poState);

                if (IsDefined(reminded)) {
                    if (reminded == "Yes")
                        query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)query.Where(vh => vh.Extra5 == "Yes");
                    else 
                        query = (DataServiceQuery<ODataClient.Proxies.VoucherHeader>)query.Where(vh => vh.Extra5 == null);
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
                    State = vh.Workflow.WorkflowState.Name,
                    PONumber = vh.PurchaseOrderHeader?.Number ?? "Unknown",
                    POOriginator = vh.PurchaseOrderHeader?.PurchaserName,
                    POOriginatorEmail = vh.PurchaseOrderHeader?.PurchaserEmail,
                    POStatus = vh.PurchaseOrderHeader?.Status,
                    ExternalId = vh.PurchaseOrderHeader.ExternalId,
                    CanNotify = vh.PurchaseOrderHeader != null ? "" : "disabled='disabled'",
                    CanNotifyMessage =
                        vh.PurchaseOrderHeader == null ? "You cannot notify without the Purchase Order" :
                        null,
                    Total = vh.FcGross?.ToString("C") ?? "$0.00",
                    Reminded = vh.Extra5 ?? ""
                }).ToList();

                var totalCount = query.Count();

                Model.Page = page;
                Model.Count = totalCount;
                Model.SearchStr = !string.IsNullOrEmpty(search) ? search.Replace("\"", "\\\"") : null;

                Model.ExportStates = PickerModel.YesNo;
                if (IsDefined(exportState))
                    Model.ExportStates.Find(s => s.Value == exportState).Selected = "selected";

                Model.Reminded = PickerModel.YesNo;
                if (IsDefined(reminded))
                    Model.Reminded.Find(s => s.Value == reminded).Selected = "selected";

                Model.PoMatches = PickerModel.YesNo;
                if (IsDefined(poMatches))
                    Model.PoMatches.Find(s => s.Value == poMatches).Selected = "selected";

                Model.States = PickerModel.States;
                if (IsDefined(state))
                    Model.States.Find(s => s.Value == state).Selected = "selected";

                Model.PoStates = PickerModel.PoStates;
                if (IsDefined(poState))
                    Model.PoStates.Find(s => s.Value == poState).Selected = "selected";

                return View["Reminders.html", Model];
            });

            Post("/NotifyPurchasersByIds", async args => {
                
                var voucherHeaderIdsStr = (string)Request.Form["voucherHeaderIds[]"];
                if (!string.IsNullOrEmpty(voucherHeaderIdsStr))
                {
                    var voucherHeaderIds = voucherHeaderIdsStr.Split(',').Select(s => Guid.Parse(s)).ToArray();

                    var result = await reminderService.RemindPurchasersByIds(voucherHeaderIds);

                    return Response.AsJson(new { message = $"The notification was submitted: " + result + " times."});
                }
                else
                {

                    return Response.AsJson(new { message = "You have not selected any invoices for remind." });
                }

            });
            this.reminderService = reminderService;
        }

        private bool IsDefined(string value) {
            return !string.IsNullOrEmpty(value) && value != "null" && value != "undefined";
        }


        public override NotifyModel Init()
        {
            return new NotifyModel();
        }
    }
}
