using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;

namespace Itm.Tickets.Fuctions_
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWebApplication()
                .Build();

            host.Run();
        }
    }
}
