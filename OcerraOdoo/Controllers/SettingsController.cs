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
using OcerraOdoo.OcerraOData;
using Microsoft.OData.Client;

namespace OcerraOdoo.Controllers
{
    public class SettingsController : Controller<SettingsModel>
    {
        public SettingsController()
        {
            Get("/Settings", args => {

                Model.Settings = Helpers.AppSetting(); 

                return View["Settings.html", Model];
            });

            Post("/UpdateSettings", args => {

                if (Request.Form.Keys != null)
                {
                    var keyCollection = ((Nancy.DynamicDictionary)Request.Form).Keys;
                    var valueCollection = ((Nancy.DynamicDictionary)Request.Form).Values;
                    foreach (string key in keyCollection)
                    {
                        Helpers.AddUpdateAppSettings(key, (string)((Nancy.DynamicDictionary)Request.Form)[key]);
                    }
                    return Response.AsJson(new { message = $"Config was updated" });
                }
                else {
                    return Response.AsJson(new { message = "Settings are not found" });
                }
            });
        }

        public override SettingsModel Init()
        {
            return new SettingsModel();
        }
    }
}
