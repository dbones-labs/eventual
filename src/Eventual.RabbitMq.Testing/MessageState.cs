namespace Eventual.RabbitMq.Testing
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.CompilerServices;

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
}