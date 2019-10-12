using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Iftm.ComputedProperties {
    public class TaskPropertyChanged<T> : INotifyPropertyChanged {
        private Func<CancellationToken, ValueTask<T>>? _factory;
        private T _value;
        private Exception? _exception;
        
        private PropertyChangedEventHandler? _propertyChanged;
        private CancellationTokenSource? _cancellation;

        #pragma warning disable 8618
        public TaskPropertyChanged(Func<CancellationToken, ValueTask<T>> factory) {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }
        #pragma warning restore 8618

        public bool HasValue => _factory == null;
        
        [MaybeNull]
        public T Value {
            get {
                if (_exception != null) ExceptionDispatchInfo.Capture(_exception).Throw();
                return _value;
            }
        }

        public Exception? Exception => _exception;

        public event PropertyChangedEventHandler PropertyChanged {
            add {
                bool wasNull = _propertyChanged == null;
                _propertyChanged += value ?? throw new ArgumentNullException(nameof(value));

                if (wasNull && _factory != null) GetTaskResultAsync();
            }
            remove {
                if (_propertyChanged == null) return;

                _propertyChanged -= value ?? throw new ArgumentNullException(nameof(value));
                if (_propertyChanged == null) {
                    if (_cancellation != null) {
                        _cancellation.Cancel();
                        _cancellation = null;
                    }
                }
            }
        }

        private async void GetTaskResultAsync() {
            Debug.Assert(_cancellation == null);
            Debug.Assert(_factory != null);
            Debug.Assert(_propertyChanged != null);

            if (_factory == null || _propertyChanged == null) return;

            _cancellation = new CancellationTokenSource();
            var ct = _cancellation.Token;

            try {
                var value = await _factory(ct).ConfigureAwait(false);

                ct.ThrowIfCancellationRequested();

                _value = value;
                _factory = null;
                _cancellation = null;
                _propertyChanged.Invoke(this, AllPropertiesChanged.EventArgs);
            }
            catch (OperationCanceledException e) when (e.CancellationToken == ct) {
            }
            catch (Exception e) {
                _exception = e;
                _factory = null;
                _cancellation = null;
                _propertyChanged.Invoke(this, AllPropertiesChanged.EventArgs);
            }
        }
    }
}
