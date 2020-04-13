using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OcerraOdoo.Models
{
    public class BaseModel
    {
        public OcerraModel Ocerra { get; set; }
        public OdooModel Odoo { get; set; }
    }

    public class OcerraModel
    {
        public Guid ClientId { get; set; }
        public bool Connected { get; set; }
        public string ClientName { get; set; }
        public DateTime LastHeartBeat { get; set; }

        public string CountryCode { get; set; }
        public string CurrencyCode { get; set; }

        public List<CurrencyCodeModel> CurrencyCodes { get; set; }
    }

    public class OdooModel
    {
        public bool Connected { get; set; }
        public string VersionNumber { get; set; }
        public string Message { get; set; }
    }
}
