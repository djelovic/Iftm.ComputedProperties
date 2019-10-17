using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Iftm.ComputedProperties {

    /// <summary>
    /// A base class for automatic firing of <see cref="PropertyChanged"/> events for computed properties
    /// when any inputs change.
    /// </summary>
    public abstract class WithComputedProperties : INotifyPropertyChanged, IDependenciesTarget, IDisposable {
        private PropertyChangedEventHandler? _propertyChanged;

        [ThreadStatic] private static Dictionary<string, PropertyChangedEventArgs>? _nameToArgs;
        [ThreadStatic] private static InPlaceList<string> _changedProperties;

        private struct Dependency {
            public readonly string TargetProperty, SourceProperty;
            public readonly INotifyPropertyChanged? Source; // null if Source == this

            public Dependency(string targetProperty, INotifyPropertyChanged? source, string sourceProperty) =>
                (TargetProperty, Source, SourceProperty) = (targetProperty, source, sourceProperty);
        }

        private InPlaceList<Dependency> _dependencies;

        /// <summary>
        /// Compares the <paramref name="destination"/> to the <paramref name="source"/>, and they are different copies the
        /// <paramref name="source"/> to the <paramref name="destination"/> and fires the
        /// <see cref="PropertyChanged"/> event for the property <paramref name="name"/>.
        /// </summary>
        /// <typeparam name="T">Type of the property</typeparam>
        /// <param name="destination">Destination.</param>
        /// <param name="source">Source.</param>
        /// <param name="name">Name of the property being set.</param>
        protected bool SetProperty<T>(ref T destination, T source, [CallerMemberName] string? name = null) {
            if (name == null) throw new ArgumentNullException(nameof(name));

            if (EqualityComparer<T>.Default.Equals(destination, source)) return false;

            destination = source;
            OnMyPropertyChanged(name);

            return true;
        }


        /// <summary>
        /// Creates a <see cref="ComputedProperty{TObj, TResult}"/> from the input <paramref name="expression"/>.
        /// </summary>
        /// <typeparam name="TObj">Class that contains the property.</typeparam>
        /// <typeparam name="TResult">Type of the property.</typeparam>
        /// <param name="expression">Expression that computes the property.</param>
        /// <returns></returns>
        protected static ComputedProperty<TObj, TResult> Computed<TObj, TResult>(Expression<Func<TObj, TResult>> expression) =>
            new ComputedProperty<TObj, TResult>(expression);

        private void AddPropertyWithDependencies(ref InPlaceList<string> properties, int startIndex, string propertyName) {
            if (properties.Contains(propertyName, startIndex, properties.Count - startIndex)) return;
            
            properties.Add(propertyName);

            foreach (var dep in _dependencies) {
                if (dep.Source == null && dep.SourceProperty == propertyName) {
                    AddPropertyWithDependencies(ref properties, startIndex, dep.TargetProperty);
                }
            }
        }

        protected virtual void OnPropertyChanged(string? name) {
            Debug.Assert(_propertyChanged != null);
            if (_propertyChanged == null) return;

            if (name == null) {
                _propertyChanged.Invoke(this, AllPropertiesChanged.EventArgs);
            }
            else {
                if (_nameToArgs == null) _nameToArgs = new Dictionary<string, PropertyChangedEventArgs>();
                var nameToArgs = _nameToArgs;

                if (!nameToArgs.TryGetValue(name, out var args)) {
                    args = new PropertyChangedEventArgs(name);
                    nameToArgs.Add(name, args);
                }

                _propertyChanged!.Invoke(this, args);
            }
        }

        private void FirePropertyChanged(ReadOnlySpan<string> names) {
            foreach (var name in names) OnPropertyChanged(name);
        }

        private void OnMyPropertyChanged(string? name) {
            if (name == null) {
                OnPropertyChanged(null);
            }
            else {
                ref var properties = ref _changedProperties;
                int startIndex = properties.Count;

                try {
                    AddPropertyWithDependencies(ref properties, startIndex, name);
                    FirePropertyChanged(properties.AsReadOnlySpan().Slice(startIndex));
                }
                finally {
                    properties.RemoveRange(startIndex, properties.Count - startIndex);
                }
            }
        }

        private void OnInputPropertyChanged(object sender, PropertyChangedEventArgs args) {
            Debug.Assert(_propertyChanged != null);

            if (_propertyChanged == null) return;

            ref var properties = ref _changedProperties;
            int startIndex = properties.Count;
            try {
                foreach (var dep in _dependencies) {
                    if (dep.Source != sender) continue;

                    if (args.PropertyName == null || args.PropertyName == dep.SourceProperty) {
                        AddPropertyWithDependencies(ref properties, startIndex, dep.TargetProperty);
                    }
                }

                FirePropertyChanged(properties.AsReadOnlySpan().Slice(startIndex));
            }
            finally {
                properties.RemoveRange(startIndex, properties.Count - startIndex);
            }
        }

        private PropertyChangedEventHandler? _onInputPropertyChangedDelegate;
        private PropertyChangedEventHandler OnInputPropertyChangedDelegate {
            get {
                if (_onInputPropertyChangedDelegate == null) _onInputPropertyChangedDelegate = OnInputPropertyChanged;
                return _onInputPropertyChangedDelegate;
            }
        }

        /// <summary>
        /// Called when the last <see cref="PropertyChanged"/> listener is detached. Detaches from the computed
        /// properties for the objects this object depends on.
        /// </summary>
        protected virtual void OnListenersAttached() {
            for (int x = 0; x < _dependencies.Count; ++x) {
                var source = _dependencies[x].Source;
                if (source == null) continue;

                bool processed = false;
                for (int y = 0; y < x; ++y) {
                    if (_dependencies[y].Source == source) {
                        processed = true;
                        break;
                    }
                }
                if (!processed) {
                    source.PropertyChanged += OnInputPropertyChangedDelegate;
                }
            }
        }

        /// <summary>
        /// Calls when the first listener to the <see cref="PropertyChanged"/> event is attached.
        /// </summary>
        protected virtual void OnListenersDetached() {
            for (int x = 0; x < _dependencies.Count; ++x) {
                var source = _dependencies[x].Source;
                if (source == null) continue;

                bool processed = false;
                for (int y = 0; y < x; ++y) {
                    if (_dependencies[y].Source == source) {
                        processed = true;
                        break;
                    }
                }
                if (!processed) {
                    source.PropertyChanged -= OnInputPropertyChangedDelegate;
                }
            }
        }

        /// <summary>
        /// Fired when any of the properties of this object are changed.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged {
            add {
                bool wasNull = _propertyChanged == null;
                _propertyChanged += value ?? throw new ArgumentNullException(nameof(value));

                if (wasNull) OnListenersAttached();
            }
            remove {
                if (_propertyChanged == null) return;

                _propertyChanged -= value ?? throw new ArgumentNullException(nameof(value));
                if (_propertyChanged == null) OnListenersDetached();
            }
        }

        private (INotifyPropertyChanged? Source, string Property) NullIfThis(in (INotifyPropertyChanged Source, string Property, int Cookie) dep) =>
            dep.Source == this
                ? (null, dep.Property)
                : (dep.Source, dep.Property);

        private INotifyPropertyChanged? NullIfThis(INotifyPropertyChanged source) => source == this ? null : source;

        private static void IncrementInputIndex(ref int inputIndex, List<(INotifyPropertyChanged Source, string Property, int Cookie)> input, int cookie) {
            // increment inputIndex to the next input with the same cookie
            do {
                Debug.Assert(inputIndex < input.Count);
                ++inputIndex;
            } while (inputIndex < input.Count && input[inputIndex].Cookie != cookie);
        }

        private static void RemoveIndices<T>(ref InPlaceList<T> list, ReadOnlySpan<int> indices) {
            if (indices.Length == 0) return;

            var write = 0;
            var read = 0;

            foreach (var index in indices) {
                while (read < index) list[write++] = list[read++];
                read++;
            }

            while (read < list.Count) list[write++] = list[read++];

            list.RemoveRange(write, list.Count - write);
        }

        private void UpdateSubscriptions(string targetProperty, ReadOnlySpan<int> toRemove, List<(INotifyPropertyChanged Source, string Property, int Cookie)> input, int inputStart, int cookie) {
            Debug.Assert(_propertyChanged != null);

            var dependencies = _dependencies.AsReadOnlySpan();

            // subscribe to new sources
            for (int x = inputStart; x < input.Count; ++x) {
                var inp = input[x];
                if (inp.Cookie != cookie) continue;

                var source = NullIfThis(inp.Source);
                if (source == null) continue;

                bool found = false;
                // does any of the existing sources match?
                for (int y = 0; y < dependencies.Length; ++y) {
                    var dep = dependencies[y];
                    if (dep.Source == source) {
                        found = true;
                        break;
                    }
                }

                if (!found) {
                    // does any of the already processed sources match?
                    for (int y = inputStart; y < x; ++y) {
                        var inp2 = input[y];

                        if (inp2.Cookie == cookie && inp2.Source == source) {
                            found = true;
                            break;
                        }
                    }
                }

                if (!found) {
                    source.PropertyChanged += OnInputPropertyChangedDelegate;
                }
            }

            // unsubscribe from any sources that are unused
            foreach (var idx in toRemove) {
                var source = dependencies[idx].Source;
                if (source == null) continue;

                // see if the source to be removed exists in the current or new dependencies
                bool found = false;

                // does any of the new inputs match
                for (int y = inputStart; y < input.Count; ++y) {
                    if (input[y].Cookie == cookie && input[y].Source == source) {
                        found = true;
                        break;
                    }
                }

                if (!found) {
                    // does any of the existing dependencies match
                    for (int y = 0; y < dependencies.Length; ++y) {
                        ref readonly var dep = ref dependencies[y];
                        if (dep.Source == source && (dep.TargetProperty != targetProperty || y < toRemove[0])) {
                            found = true;
                            break;
                        }
                    }
                }

                if (!found) {
                    source.PropertyChanged -= OnInputPropertyChangedDelegate;
                }
            }
        }

        private void ReplaceDependencies(string targetProperty, ReadOnlySpan<int> toRemove, List<(INotifyPropertyChanged Source, string Property, int Cookie)> input, int inputStart, int cookie) {
            ref var dependencies = ref _dependencies;

            if (_propertyChanged != null) {
                UpdateSubscriptions(targetProperty, toRemove, input, inputStart, cookie);
            }

            for (int x = inputStart; x < input.Count; ++x) {
                var inp = input[x];
                if (inp.Cookie != cookie) continue;

                var (source, sourceProperty) = NullIfThis(inp);

                if (toRemove.Length > 0) {
                    dependencies[toRemove[0]] = new Dependency(targetProperty, source, sourceProperty);
                    toRemove = toRemove.Slice(1);
                }
                else {
                    dependencies.Add(new Dependency(targetProperty, source, sourceProperty));
                }
            }

            RemoveIndices(ref dependencies, toRemove);
        }

        void IDependenciesTarget.SetDependencies(string targetProperty, List<(INotifyPropertyChanged Source, string Property, int Cookie)> input, int cookie) {
            int inputIndex = -1;
            IncrementInputIndex(ref inputIndex, input, cookie);

            ref var dependencies = ref _dependencies;

            Span<int> toRemove = stackalloc int [dependencies.Count];
            int count = 0;
            for (int x = 0; x < dependencies.Count; ++x) {
                var dep = dependencies[x];
                if (dep.TargetProperty != targetProperty) continue;

                if (inputIndex < input.Count && (dep.Source, dep.SourceProperty) == NullIfThis(input[inputIndex])) {
                    IncrementInputIndex(ref inputIndex, input, cookie);
                }
                else {
                    toRemove[count++] = x;
                }
            }
            toRemove = toRemove.Slice(0, count);

            if (toRemove.Length > 0 || inputIndex < input.Count) {
                ReplaceDependencies(targetProperty, toRemove, input, inputIndex, cookie);
            }
        }

        /// <summary>
        /// True if the <see cref="PropertyChanged"/> event has any listeners.
        /// </summary>
        public bool HasListeners => _propertyChanged != null;

        /// <summary>
        /// Clears the list of <see cref="PropertyChanged"/> listeners and detaches
        /// from any input objects.
        /// </summary>
        public virtual void Dispose() {
            if (_propertyChanged != null) {
                _propertyChanged = null;
                OnListenersDetached();
            }
        }
    }

}
