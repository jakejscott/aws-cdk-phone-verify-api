using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Newtonsoft.Json;
using NUnit.Framework;
using OtpNet;

namespace AwsCdkPhoneVerifyApi.IntegrationTests
{
    public class StartRequest
    {
        public string Phone { get; set; }
    }

    public class StartResponse
    {
        public Guid? Id { get; set; }
    }

    public class CheckRequest
    {
        public Guid Id { get; set; }
        public string Code { get; set; }
    }

    public class CheckResponse
    {
        public bool Verified { get; set; }
    }

    public class StatusRequest
    {
        public Guid Id { get; set; }
    }

    public class StatusResponse
    {
        public Guid Id { get; set; }
        public string Phone { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Verified { get; set; }
    }

    public class IntegrationTests
    {
        private HttpClient httpClient;
        private VerificationsRepository repository;

        [SetUp]
        public void Setup()
        {
            var baseUrl = Environment.GetEnvironmentVariable("API_URL") ?? "https://pxcktbkbc2.execute-api.ap-southeast-2.amazonaws.com";
            var apiKey = Environment.GetEnvironmentVariable("API_KEY") ?? "vnqb9BTY6V6UVtnx77EiY8HPpfJIO7t58Z6bWSlw";
            var region = Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION") ?? "ap-southeast-2";

            httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
            httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);

            var regionEndoint = RegionEndpoint.GetBySystemName(region);
            var ddb = new AmazonDynamoDBClient(regionEndoint);

            repository = new VerificationsRepository(ddb);
        }

        [Test]
        public async Task StartVerification()
        {
            var phone = "+64223062141";
            Guid verificationId;

            // Start verification
            using (var request = new HttpRequestMessage(HttpMethod.Post, "/prod/verify/start"))
            {
                var startRequest = new StartRequest { Phone = phone };
                var json = JsonConvert.SerializeObject(startRequest);

                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using (var response = await httpClient.SendAsync(request))
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Request: {request.RequestUri} Status:{response.StatusCode} Response: {content}");

                    response.EnsureSuccessStatusCode();

                    var startResponse = JsonConvert.DeserializeObject<StartResponse>(content);

                    Assert.NotNull(startResponse);
                    Assert.NotNull(startResponse.Id);

                    verificationId = startResponse.Id.Value;
                }
            }

            // Check verification
            using (var request = new HttpRequestMessage(HttpMethod.Post, "/prod/verify/check"))
            {
                var verification = await repository.GetVerificationAsync(verificationId);
                Assert.NotNull(verification);

                var hotp = new Hotp(verification.SecretKey);
                var code = hotp.ComputeHOTP(verification.Version);

                var checkRequest = new CheckRequest { Id = verificationId, Code = code };
                var json = JsonConvert.SerializeObject(checkRequest);

                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using (var response = await httpClient.SendAsync(request))
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Request: {request.RequestUri} Status:{response.StatusCode} Response: {content}");

                    response.EnsureSuccessStatusCode();

                    var checkResponse = JsonConvert.DeserializeObject<CheckResponse>(content);

                    Assert.NotNull(checkResponse);
                    Assert.True(checkResponse.Verified);
                }
            }

            // Verification Status
            using (var request = new HttpRequestMessage(HttpMethod.Post, "/prod/verify/status"))
            {
                var statusRequest = new StatusRequest { Id = verificationId };
                var json = JsonConvert.SerializeObject(statusRequest);

                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using (var response = await httpClient.SendAsync(request))
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Request: {request.RequestUri} Status:{response.StatusCode} Response: {content}");

                    response.EnsureSuccessStatusCode();

                    var statusResponse = JsonConvert.DeserializeObject<StatusResponse>(content);

                    Assert.NotNull(statusResponse);
                    Assert.AreEqual(statusResponse.Id, verificationId);
                    Assert.AreEqual(statusResponse.Phone, phone);
                    Assert.NotNull(statusResponse.Verified);
                }
            }
        }
    }
}