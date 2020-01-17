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

    /// <summary>
    /// <para>
    /// Wraps a function CancellationToken -> ValueTask&lt;<see cref="T"/>&gt; in an object
    /// that implements <see cref="INotifyPropertyChanged"/>. The function is called when
    /// the first <see cref="PropertyChanged"/> listener is attached with a new
    /// <see cref="CancellationToken"/>, and the returned task is awaited on.
    /// </para>
    /// <para>
    /// If all listeners detach before the awaited task completes, the <see cref="CancellationToken"/>
    /// is cancelled and this object is returned to the initial state.
    /// </para>
    /// <para>
    /// If instead the awaited task completes while there are listeners, the property <see cref="HasValue"/>
    /// is set to true, the property <see cref="Value"/> is set to the task result, and all subsequent
    /// changes to the <see cref="PropertyChanged"/> listeners don't do anything.
    /// </para>
    /// </summary>
    /// <typeparam name="T">Type that the function returns.</typeparam>
    public class TaskModel<T> : INotifyPropertyChanged, IDisposable, IEquatable<TaskModel<T>> {
        private readonly Func<CancellationToken, ValueTask<T>> _factory;

        private T _value;
        private Exception? _exception;
        private bool _hasValue;
        
        private PropertyChangedEventHandler? _propertyChanged;
        private CancellationTokenSource? _cancellation;

        /// <summary>
        /// Creates a new TaskPropertyChanged object.
        /// </summary>
        /// <param name="factory">The function that given a <see cref="CancellationToken"/> returns
        /// a <see cref="ValueTask&lt<see cref="T"/>"/>&gt; whose result we are interested in.</param>
        #nullable disable
        public TaskModel(Func<CancellationToken, ValueTask<T>> factory) {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }
         #nullable restore

        /// <summary>
        /// True if the value of the task has been completed.
        /// </summary>
        public bool HasValue => _hasValue;

        /// <summary>
        /// Result of the task, or default value of <see cref="T"/> the task has not completed yet.
        /// Can throw an exception if the task was faulted.
        /// </summary>
        [MaybeNull]
        public T Value {
            get {
                if (_exception != null) ExceptionDispatchInfo.Capture(_exception).Throw();
                return _value;
            }
        }

        /// <summary>
        /// Task exception in case the task faulted, null otherwise.
        /// </summary>
        public Exception? Exception => _exception;

        public event PropertyChangedEventHandler PropertyChanged {
            add {
                if (HasValue) return; // no point to adding listeners and adding pressure to the GC

                bool wasNull = _propertyChanged == null;
                _propertyChanged += value ?? throw new ArgumentNullException(nameof(value));

                if (wasNull && !_hasValue) GetTaskResultAsync();
            }
            remove {
                if (_propertyChanged == null) return;

                _propertyChanged -= value ?? throw new ArgumentNullException(nameof(value));
                if (_propertyChanged == null) {
                    if (_cancellation != null) {
                        _cancellation.Cancel(true);
                        _cancellation = null;
                    }
                }
            }
        }

        private async void GetTaskResultAsync() {
            Debug.Assert(_cancellation == null);
            Debug.Assert(_propertyChanged != null);

            _cancellation = new CancellationTokenSource();
            var ct = _cancellation.Token;

            try {
                await Task.Yield();
                var value = await _factory(ct);
                ct.ThrowIfCancellationRequested();

                _value = value;
                _hasValue = true;
                _cancellation = null;
                _propertyChanged!.Invoke(this, AllPropertiesChanged.EventArgs);
                _propertyChanged = null;
            }
            catch (OperationCanceledException e) when (e.CancellationToken == ct) {
            }
            catch (Exception e) {
                _exception = e;
                _hasValue = true;
                _cancellation = null;
                _propertyChanged!.Invoke(this, AllPropertiesChanged.EventArgs);
                _propertyChanged = null;
            }
        }

        /// <summary>
        /// Clears the property list and cancells the task if it's running. This function
        /// is a no-op if there are no <see cref="PropertyChanged"/> listeners.
        /// </summary>
        public void Dispose() {
            if (_propertyChanged != null) {
                _propertyChanged = null;

                if (_cancellation != null) {
                    _cancellation.Cancel(true);
                    _cancellation = null;
                }
            }
        }

        public virtual bool Equals(TaskModel<T> other) => _factory == other._factory;
        public override bool Equals(object obj) => obj is TaskModel<T> other && Equals(other);
        public static bool operator ==(TaskModel<T> x, TaskModel<T> y) => x.Equals(y);
        public static bool operator !=(TaskModel<T> x, TaskModel<T> y) => !x.Equals(y);
        public override int GetHashCode() => _factory.GetHashCode();
    }

    public class TaskModel<IIdentity, TResult> : TaskModel<TResult> {
        private readonly IIdentity _identity;

        public TaskModel(IIdentity args, Func<CancellationToken, ValueTask<TResult>> factory) :
            base(factory) {

            _identity = args;
        }

        public override bool Equals(TaskModel<TResult> other) =>
            other is TaskModel<IIdentity, TResult> otherModel &&
            EqualityComparer<IIdentity>.Default.Equals(_identity, otherModel._identity)
            ;
    }

    public static class TaskModel {
        public static TaskModel<T> Create<T>(Func<CancellationToken, ValueTask<T>> factory) =>
            new TaskModel<T>(factory);

        public static TaskModel<T> Create<Arg1, T>(Arg1 arg1, Func<Arg1, CancellationToken, ValueTask<T>> factory) =>
            new TaskModel<(Arg1, Delegate), T>((arg1, factory), ct => factory(arg1, ct));

        public static TaskModel<T> Create<Arg1, Arg2, T>(Arg1 arg1, Arg2 arg2, Func<Arg1, Arg2, CancellationToken, ValueTask<T>> factory) =>
            new TaskModel<(Arg1, Arg2, Delegate), T>((arg1, arg2, factory), ct => factory(arg1, arg2, ct));

        public static TaskModel<T> Create<Arg1, Arg2, Arg3, T>(Arg1 arg1, Arg2 arg2, Arg3 arg3, Func<Arg1, Arg2, Arg3, CancellationToken, ValueTask<T>> factory) =>
            new TaskModel<(Arg1, Arg2, Arg3, Delegate), T>((arg1, arg2, arg3, factory), ct => factory(arg1, arg2, arg3, ct));

        public static TaskModel<T> Create<Arg1, Arg2, Arg3, Arg4, T>(Arg1 arg1, Arg2 arg2, Arg3 arg3, Arg4 arg4, Func<Arg1, Arg2, Arg3, Arg4, CancellationToken, ValueTask<T>> factory) =>
            new TaskModel<(Arg1, Arg2, Arg3, Arg4, Delegate), T>((arg1, arg2, arg3, arg4, factory), ct => factory(arg1, arg2, arg3, arg4, ct));

        public static TaskModel<T> Create<Arg1, Arg2, Arg3, Arg4, Arg5, T>(Arg1 arg1, Arg2 arg2, Arg3 arg3, Arg4 arg4, Arg5 arg5, Func<Arg1, Arg2, Arg3, Arg4, Arg5, CancellationToken, ValueTask<T>> factory) =>
            new TaskModel<(Arg1, Arg2, Arg3, Arg4, Arg5, Delegate), T>((arg1, arg2, arg3, arg4, arg5, factory), ct => factory(arg1, arg2, arg3, arg4, arg5, ct));
    }
}
