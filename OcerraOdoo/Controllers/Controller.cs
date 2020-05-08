using Nancy;
using Nancy.Security;
using Nancy.Extensions;
using OcerraOdoo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OcerraOdoo.Properties;

namespace OcerraOdoo.Controllers
{
    public abstract class Controller<TModel> : NancyModule where TModel : BaseModel
    {
        public TModel Model { get; set; }

        public Controller()
        {
            Model = Init();

            Model.ApplicationPath = Settings.Default.ApplicationPath;

            Model.Ocerra = Bootstrapper.OcerraModel;
            Model.Odoo = Bootstrapper.OdooModel;

            this.RequiresAuthentication();
        }

        public abstract TModel Init();

        public dynamic RequestJson {
            get {
                var jsonString = Request.Body.AsString();
                return JsonConvert.DeserializeObject(jsonString);
            }
        }
    }
}
