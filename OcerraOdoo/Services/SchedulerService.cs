using OcerraOdoo.Properties;
using Quartz;
using Quartz.Impl;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OcerraOdoo.Services
{
    public class SchedulerService
    {
        private static IScheduler sched = null;
        private static StdSchedulerFactory factory = null;

        public SchedulerService()
        {

        }

        public async Task Init() {

            // construct a scheduler factory
            NameValueCollection props = new NameValueCollection
            {
                { "quartz.serializer.type", "binary" }
            };
            
            factory = new StdSchedulerFactory(props);

            // get a scheduler
            sched = await factory.GetScheduler();

            await sched.Start();

            // define the job and tie it to our HelloJob class
            var importJob = JobBuilder.Create<ImportJob>()
                .Build();

            var importTrigger = TriggerBuilder
                .Create()
                .StartNow()
                .WithCronSchedule(Settings.Default.ImportCron)
                .Build();

            await sched.ScheduleJob(importJob, importTrigger);

            // define the job and tie it to our HelloJob class
            var exportJob = JobBuilder.Create<ExportJob>()
                .Build();

            var exportTrigger = TriggerBuilder
                .Create()
                .StartNow()
                .WithCronSchedule(Settings.Default.ExportCron)
                .Build();

            await sched.ScheduleJob(exportJob, exportTrigger);

        }

        public class ImportJob : IJob
        {
            public async Task Execute(IJobExecutionContext context)
            {
                try
                {
                    var settings = Helpers.AppSetting();
                    var importService = (ImportService)Bootstrapper.Container.Resolve(typeof(ImportService));

                    await importService.ImportVendors(DateTime.Parse(settings.LastVendorSyncDate));
                    await importService.ImportProducts(DateTime.Parse(settings.LastProductSyncDate));
                    await importService.ImportPurchaseOrders(DateTime.Parse(settings.LastPurchaseSyncDate));

                    settings.LastVendorSyncDate = DateTime.Now.ToString("s");
                    settings.LastProductSyncDate = DateTime.Now.ToString("s");
                    settings.LastPurchaseSyncDate = DateTime.Now.ToString("s");
                } 
                catch(Exception ex)
                {
                    Helpers.LogError(ex, "There was an error on Odoo to Ocerra import");
                }
            }
        }

        public class ExportJob : IJob 
        {
            public async Task Execute(IJobExecutionContext context)
            {
                try
                {
                    var settings = Helpers.AppSetting();

                    var exportService = (ExportService)Bootstrapper.Container.Resolve(typeof(ExportService));

                    await exportService.ExportInvoices(DateTime.Parse(settings.LastInvoiceSyncDate));
                    
                    settings.LastInvoiceSyncDate = DateTime.Now.ToString("s");
                }
                catch (Exception ex)
                {
                    Helpers.LogError(ex, "There was an error on Ocerra to Odoo export");
                }
            }
        }
    }
}
