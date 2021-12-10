namespace Eventual.RabbitMq.Tests
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


    public class Settings
    {
        public string VHost { get; set; } = "/";
        public string User { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string Location { get; set; } = "localhost";
        public int Port { get; set; } = 15672;
    }

    public class RabbitClient : IDisposable
    {
        private readonly Settings _settings;
        private string _vhost;
        private HttpClient _client;
        private JsonSerializer _serializer = new();
        private ServiceProvider _container;
        private IPublisher bus;

        public RabbitClient(Settings settings, Action<SetupWrapper> setupAction)
        {
            _settings = settings;
            _vhost = WebUtility.HtmlEncode(_settings.VHost);


            //HTTP client will be used to query/delete the RabbitMQ server about queues and exchanges
            var config = new HttpClientHandler()
            {
                AutomaticDecompression =
                    DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.None,
                //MaxConnectionsPerServer = 100,
            };


            _client = new HttpClient(config);
            var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.User}:{settings.Password}"));

            _client.BaseAddress = new Uri($"http://{settings.Location}:{settings.Port}/api");
            _client.DefaultRequestHeaders.Clear();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

            //we need to clean up the server before we test.
            Clean().Wait(5000);

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
                        $"amqp://{settings.User}:{settings.Password}@{settings.Location}:{settings.Port}/{_vhost}";

                    mq.BusConfiguration.ServiceName = "TestClient";

                });

                var wrapper = new SetupWrapper(setup);
                setupAction?.Invoke(wrapper);
            });

            _container = serviceCollection.BuildServiceProvider();
            var busService = _container.GetService<BusHostedService>();
            busService?.StartAsync(new CancellationToken()).Wait(5000);
            bus = _container.GetService<IBus>();
            
        }

        public RabbitClient() : this(new Settings(), null)
        {
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

        private async Task<IEnumerable<T>> GetItems<T>(string itemType)
        {
            var message = new HttpRequestMessage(HttpMethod.Get, $"{itemType}/{_vhost}");
            var response = await _client.SendAsync(message);

            var content = await response.Content.ReadAsStringAsync();

            return _serializer.Deserialize<List<T>>(content);


            //using var jsonTextReader = new JsonTextReader(new StringReader(content));
            //var rabbitItems = JArray.ReadFrom(jsonTextReader);

            //return rabbitItems.Select(q => q["name"].Value<string>()).Where(name => !string.IsNullOrWhiteSpace(name)).ToList();
        }

        private async Task Delete(string itemType, string name)
        {
            var message = new HttpRequestMessage(HttpMethod.Get, $"{itemType}/{_vhost}/{name}");
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

        public async Task Clean()
        {
            await RemoveQueues();
            await RemoveExchanges();
        }

        public void Dispose()
        {
            _client?.Dispose();
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


        public void Add<T>(T message)
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

        public IEnumerable<T> Messages<T>() where T : class
        {
            lock (_messages)
            {
                return _messages.Where(x => x is T).Cast<T>().ToList();
            }

        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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


    public static class ApplicationContainer
    {
        public static IServiceCollection Setup(this IServiceCollection serviceCollection,
            Action<IServiceCollection> setup = null,
            Action<IServiceCollection> configureAuditable = null)
        {

            configureAuditable?.Invoke(serviceCollection);


            setup?.Invoke(serviceCollection);
            return serviceCollection;
        }

        public static IServiceProvider Build(
            Action<IServiceCollection> setup = null,
            Action<IServiceCollection> configureAuditable = null)
        {



            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(configure => configure.AddConsole());
            serviceCollection.Setup(setup, configureAuditable);
            return serviceCollection.BuildServiceProvider();

        }
    }
}
