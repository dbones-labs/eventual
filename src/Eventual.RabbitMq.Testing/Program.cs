using System;

namespace Eventual.RabbitMq.Testing
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Configuration;
    using Infrastructure.Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;


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


    public class Settings
    {
        public string VHost { get; set; } //= "/";
        public string User { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string Location { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public int AdminPort { get; set; } = 15672;
    }

    public class RabbitClient : IDisposable
    {
        private ServiceProvider _container;
        private static int _clientCount = 0;

        public RabbitClient(Settings settings, string vhost, HttpClient client, Action<SetupWrapper> setupAction = null)
        { 
            _clientCount++;


            //a test client of eventual will be used to listen to messages.
            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<MessageState>();
            serviceCollection.AddLogging(configure => configure.AddConsole());
            serviceCollection.AddEventual(null, setup =>
            {
                setup.UseTransport<RabbitMq>(mq =>
                {
                    //"amqp://user:pass@hostName:port/vhost"
                    //"amqp://localhost/%2f"
                    mq.BusConfiguration.ConnectionString =
                        $"amqp://{settings.User}:{settings.Password}@{settings.Location}:{settings.Port}/{vhost}";

                    mq.BusConfiguration.ServiceName = $"test-client-{_clientCount}";

                });

                var wrapper = new SetupWrapper(setup);
                setupAction?.Invoke(wrapper);
            });

            _container = serviceCollection.BuildServiceProvider();
            var busService = _container.GetService<IInitBus>();
            busService.Start().Wait(5000);
            Bus = _container.GetService<IBus>();
            State = _container.GetService<MessageState>();
        }

        public IPublisher Bus { get; set; }
        public MessageState State { get; set; }

        

        public void Dispose()
        {
            _container?.Dispose();
        }
    }

    internal class TestConsumer<T> : IConsumer<T>
    {
        private readonly MessageState _state;

        public TestConsumer(MessageState state)
        {
            _state = state;
        }

        public Task Handle(Message<T> message)
        {
            _state.Add(message);
            return Task.CompletedTask;
        }
    }

    public class MessageState : INotifyPropertyChanged
    {

        private readonly IList<object> _messages = new List<object>();


        public void Add<T>(Message<T> message)
        {
            lock (_messages)
            {
                _messages.Add(message);
            }

            OnPropertyChanged("Messages");
        }

        public IEnumerable<Object> AllMessages
        {
            get
            {
                lock (_messages)
                {
                    return _messages.ToList();
                }
            }
        }

        public IEnumerable<Message<T>> Messages<T>() where T : class
        {
            lock (_messages)
            {
                return _messages.Where(x => x is Message<T>).Cast<Message<T>>().ToList();
            }

        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }


    public static class Wait
    {
        public static void WaitFor(this Task task, Func<bool> criteria)
        {
            For(criteria);
        }

        public static void WaitFor(this Task task, Func<bool> criteria, TimeSpan timeout)
        {
            For(criteria, timeout);
        }

        public static void For(Func<bool> criteria)
        {
            For(criteria, new TimeSpan(0, 0, 5, 0));
        }

        public static void For(Func<bool> criteria, TimeSpan timeout)
        {
            var actualTimeout = DateTime.UtcNow.Add(timeout);

            while (!criteria())
            {
                if (DateTime.UtcNow > actualTimeout)
                {
                    throw new TimeoutException();
                }

                Thread.Sleep(50);
            }
        }
    }

    public class SetupWrapper
    {
        private readonly Setup _setup;

        public SetupWrapper(Setup setup)
        {
            _setup = setup;
        }

        //public void Subscribe(Type consumer, Action<ConsumerSetup> conf = null)
        //{
        //    _setup.Subscribe(consumer, conf);
        //}

        //public void Subscribe(ConsumerSetup setup)
        //{
        //    _setup.Subscribe(setup);
        //}

        public void Subscribe<T>(Action<ConsumerSetup> conf = null) where T : class
        {
            _setup.Subscribe<TestConsumer<T>>(conf);
        }
    }

    public class JsonSerializer
    {
        private readonly JsonSerializerSettings _settings;

        public JsonSerializer()
        {
            var contractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            };

            _settings = new JsonSerializerSettings
            {
                ContractResolver = contractResolver,
                Formatting = Formatting.Indented
            };
        }

        public T Deserialize<T>(string value)
        {
            return JsonConvert.DeserializeObject<T>(value, _settings);
        }

    }


    public class Exchange
    {
        public bool AutoDelete { get; set; }
        public bool Durable { get; set; }
        public bool Internal { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Vhost { get; set; }
    }

    public class VHost
    {
        public string Name { get; set; }
    }

    public class Queue
    {
        public bool AutoDelete { get; set; }
        public bool Durable { get; set; }
        public bool Exclusive { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string State { get; set; }
        public string Vhost { get; set; }
    }
}
