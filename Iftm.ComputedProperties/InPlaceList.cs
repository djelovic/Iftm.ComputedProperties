using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Iftm.ComputedProperties {

    public struct InPlaceList<T> : IEnumerable<T> {
        private T[]? _arr;
        private int _count;

        public void Add(T val) {
            if (_count == Capacity) Expand();
            _arr![_count++] = val;
        }

        public ref T this[int index] {
            get {
                if ((uint)index >= (uint)_count) throw new IndexOutOfRangeException();
                return ref _arr![index];
            }
        }

        public int Count => _count;

        public void Clear() {
            if (_arr != null) {
                Array.Clear(_arr, 0, _count);
                _count = 0;
            }
        }

        public void RemoveRange(int index, int count) {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if ((uint)index > (uint)_count) throw new IndexOutOfRangeException();
            if (index + count > _count) throw new ArgumentOutOfRangeException(nameof(count));

            if (count > 0) {
                Array.Copy(_arr, index + count, _arr, index, _count - (index + count));
                Array.Clear(_arr, _count - count, count);
                _count -= count;
            }
        }

        public void RemoveAt(int index) {
            if ((uint)index >= (uint)_count) throw new IndexOutOfRangeException();

            Array.Copy(_arr, index + 1, _arr, index, _count - (index + 1));
            _arr![_count - 1] = default!;
            --_count;
        }

        public int IndexOf(in T val, int index, int count) {
            var cmp = EqualityComparer<T>.Default;

            var span = AsReadOnlySpan(index, count);
            for (int x = 0; x < span.Length; ++x) {
                if (cmp.Equals(span[x], val)) return index + x;
            }

            return -1;
        }

        public int IndexOf(in T val) => IndexOf(val, 0, _count);

        public bool Contains(in T val, int index, int count) => IndexOf(val, index, count) >= 0;

        public bool Contains(in T val) => Contains(val, 0, _count);

        public ReadOnlySpan<T> AsReadOnlySpan(int start, int length) {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            if ((uint)start > (uint)_count) throw new IndexOutOfRangeException();
            if (start + length > _count) throw new ArgumentOutOfRangeException(nameof(length));

            return _arr.AsSpan(start, length);
        }

        public ReadOnlySpan<T> AsReadOnlySpan(int start) => AsReadOnlySpan(start, _count - start);

        public ReadOnlySpan<T> AsReadOnlySpan() => _arr.AsSpan(0, _count);

        private int Capacity => _arr != null ? _arr.Length : 0;

        private void Expand() {
            if (_arr == null) {
                _arr = new T[4];
            }
            else {
                if (_count == _arr.Length) Array.Resize(ref _arr, _arr.Length * 2);
            }
        }

        public IEnumerator<T> GetEnumerator() {
            for (int x = 0; x < _count; ++x) yield return _arr![x];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

}
