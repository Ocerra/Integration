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
    }

    public class Reminder {
        public string VoucherId { get; set; }

        public string DocumentId { get; set; }

        public string Number { get; set; }

        public string Vendor { get; set; }

        public string PONumber { get; set; }

        public string POStatus { get; set; }

        public string POOriginator { get; set; }

        public string POOriginatorEmail { get; set; }

        public string CanNotify { get; set; }

        public string CanNotifyMessage { get; set; }

        public string Date { get; set; }
    }
}
