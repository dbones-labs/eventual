namespace Eventual.RabbitMq.Testing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;


    public class Host : IDisposable
    {
        HttpClient _client;
        readonly Settings _settings;
        JsonSerializer _serializer = new();
        List<RabbitClient> _clients = new();
        bool _shouldDeleteVhost = false;

        static int _hostCount = 0;

        public Host(Settings settings)
        {
            _settings = settings;
            _hostCount++;
            _settings.VHost ??= $"test-host-{_hostCount}";
            VhostEncoded = WebUtility.HtmlEncode(_settings.VHost);

            //HTTP client will be used to query/delete the RabbitMQ server about queues and exchanges
            var config = new HttpClientHandler()
            {
                AutomaticDecompression =
                    DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.None,
                //MaxConnectionsPerServer = 100,
            };


            _client = new HttpClient(config);
            var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.User}:{settings.Password}"));

            _client.BaseAddress = new Uri($"http://{settings.Location}:{settings.AdminPort}");
            _client.DefaultRequestHeaders.Clear();
            
            _client.DefaultRequestHeaders
                .Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json")); 
            
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

            var t = GetVHosts();
            t.Wait(5000);
            var vHosts = t.Result;
            if (vHosts.All(x => x.Name != settings.VHost))
            {
                CreateVHost(VhostEncoded).Wait(5000);
                _shouldDeleteVhost = true;
            }

            if (settings.VHost.StartsWith("test-host"))
            {
                _shouldDeleteVhost = true;
            }

            //we need to clean up the server before we test.
            Clean().Wait(5000);
        }

        public string VhostEncoded { get; set; }

        public RabbitClient CreateClient(Action<SetupWrapper> setupAction = null)
        {
            var client = new RabbitClient(_settings, VhostEncoded, _client, setupAction);
            _clients.Add(client);
            return client;
        }

        public async Task CreateVHost(string encodedName)
        {
            var message = new HttpRequestMessage(HttpMethod.Put, $"api/vhosts/{encodedName}");
            await _client.SendAsync(message);
        }

        public async Task<IEnumerable<VHost>> GetVHosts()
        {
            var message = new HttpRequestMessage(HttpMethod.Get, "api/vhosts");
            var response = await _client.SendAsync(message);

            var content = await response.Content.ReadAsStringAsync();

            return _serializer.Deserialize<List<VHost>>(content);
            //return await GetItems<VHost>();
        }

        public async Task<IEnumerable<Exchange>> GetExchanges()
        {
            var items = await GetItems<Exchange>("exchanges");
            return items.Where(x => !x.Name.StartsWith("amq.") && x.Vhost == _settings.VHost).ToList();
        }

        public async Task<IEnumerable<Queue>> GetQueues()
        {
            var items = await GetItems<Queue>("queues");
            return items.Where(x => x.Vhost == _settings.VHost).ToList();
        }

        
        private async Task<IEnumerable<T>> GetItems<T>(string itemType = null)
        {
            var route = itemType == null
                ? $"api/{VhostEncoded}"
                : $"api/{itemType}/{VhostEncoded}";

            var message = new HttpRequestMessage(HttpMethod.Get, route);
            var response = await _client.SendAsync(message);

            var content = await response.Content.ReadAsStringAsync();

            return _serializer.Deserialize<List<T>>(content);


            //using var jsonTextReader = new JsonTextReader(new StringReader(content));
            //var rabbitItems = JArray.ReadFrom(jsonTextReader);

            //return rabbitItems.Select(q => q["name"].Value<string>()).Where(name => !string.IsNullOrWhiteSpace(name)).ToList();
        }


        public async Task Clean()
        {
            await RemoveQueues();
            await RemoveExchanges();
        }

        public async Task RemoveVHost(string encodedName)
        {
            var message = new HttpRequestMessage(HttpMethod.Delete, $"api/vhosts/{encodedName}");
            await _client.SendAsync(message);
        }

        public async Task RemoveExchanges()
        {
            var exchanges = await GetExchanges();
            foreach (var exchange in exchanges)
            {
                await Delete("exchanges", exchange.Name);
            }
        }

        public async Task RemoveQueues()
        {
            var queues = await GetQueues();
            foreach (var queue in queues)
            {
                await Delete("queues", queue.Name);
            }
        }

        private async Task Delete(string itemType, string name)
        {
            var message = new HttpRequestMessage(HttpMethod.Delete, $"api/{itemType}/{VhostEncoded}/{name}");
            await _client.SendAsync(message);
        }

        public void Dispose()
        {
            foreach (var rabbitClient in _clients)
            {
                rabbitClient.Dispose();
            }

            if (_shouldDeleteVhost)
            {
                RemoveVHost(VhostEncoded).Wait(5000);
            }

            _client?.Dispose();
        }
    }
}
