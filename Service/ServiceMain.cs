using nooLite2MQTT;
using System.ServiceProcess;

namespace ServiceApp
{
    public partial class ServiceMain : ServiceBase
    {
        private readonly Server Service;

        public ServiceMain()
        {
            InitializeComponent();
            Service = new Server();
        } // ServiceMain()

        protected override void OnStart(string[] args)
        {
            Service.OnStart();
        } // OnStart(string[])

        protected override void OnStop()
        {
            Service.OnStop();
        } // OnStop()
    } // class ServiceMain
}
