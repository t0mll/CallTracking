using CallTracking.Kafka.Common.Producer.Extensions;
using CallTraking.NEventSocket.Common.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CallTracking.FSProducerWorker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    IConfiguration configuration = hostContext.Configuration;
                    services.AddHostedService<Worker>();

                    var host = configuration["FREESWITCH_ESL_HOSTNAME"];
                    var port = int.Parse(configuration["FREESWITCH_ESL_PORT"]);
                    var password = configuration["FREESWITCH_ESL_PASSWORD"];
                    services.AddSingleton<IEventSocket, InboundSocket>(x => new InboundSocket(x.GetRequiredService<ILogger<InboundSocket>>(), host:host, port:port, password:password));
                    services.AddKafkaProducer();
                });
    }
}
