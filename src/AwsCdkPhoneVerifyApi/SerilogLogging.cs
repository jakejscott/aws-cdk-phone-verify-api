using Serilog;
using Serilog.Formatting.Json;

namespace AwsCdkPhoneVerifyApi
{
    public class SerilogLogging
    {
        public static ILogger ConfigureLogging()
        {
            return new LoggerConfiguration()
                .WriteTo.Console(formatter: new JsonFormatter())
                .Enrich.FromLogContext()
                .CreateLogger();
        }
    }
}