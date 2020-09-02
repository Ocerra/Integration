using Quartz.Impl.Triggers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OcerraOdoo.Models
{
    public class NotifyModel : BaseModel
    {
        public List<Reminder> Reminders { get; set; }

        public int Page { get; set; }
        public int Count { get; set; }

        public int PrevPage => Page > 1 ? Page - 1 : 1;

        public int NextPage => Page > 1 ? Page + 1 : 2;

        public string SearchStr { get; set; }

        public List<PickerModel> PoMatches { get; set; }
        public List<PickerModel> ExportStates { get; set; }
        public List<PickerModel> Reminded { get; set; }
        public List<PickerModel> States { get; set; }
        public List<PickerModel> PoStates { get; set; }
    }

    public class PickerModel {
        public string Selected { get; set; }
        public string Value { get; set; }

        public static List<PickerModel> YesNo =>
            new List<PickerModel> {
                new PickerModel { Value = "" },
                new PickerModel { Value = "Yes" },
                new PickerModel { Value = "No" }
            };

        public static List<PickerModel> States => 
            new List<PickerModel> {
                new PickerModel { Value = "" },
                new PickerModel { Value = "Received" },
                new PickerModel { Value = "Submitted" },
                new PickerModel { Value = "Waiting for PO" },
                new PickerModel { Value = "PO Required" },
                new PickerModel { Value = "Approved" },
            };

        public static List<PickerModel> OdooStates =>
            new List<PickerModel> {
                new PickerModel { Value = "" },
                new PickerModel { Value = "draft" },
                new PickerModel { Value = "open" },
                new PickerModel { Value = "paid" }
            };

        public static List<PickerModel> PoStates =>
            new List<PickerModel> {
                new PickerModel { Value = "" },
                new PickerModel { Value = "Draft" },
                new PickerModel { Value = "Approved" },
                new PickerModel { Value = "Receipted" },
                new PickerModel { Value = "Done" }
            };
    }

    public class Reminder {
        public string VoucherId { get; set; }

        public string DocumentId { get; set; }

        public string Number { get; set; }

        public string Vendor { get; set; }

        public string State { get; set; }

        public string PONumber { get; set; }

        public string POStatus { get; set; }

        public string POOriginator { get; set; }

        public string POOriginatorEmail { get; set; }

        public string ExternalId { get; set; }

        public string CanNotify { get; set; }

        public string CanNotifyMessage { get; set; }

        public string Date { get; set; }

        public string Total { get; set; }

        public string Reminded { get; set; }
    }
}
