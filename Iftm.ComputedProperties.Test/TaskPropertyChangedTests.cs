using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Iftm.ComputedProperties.Test {
    public class TaskPropertyChangedTests {
        
        [Fact]
        public void TestSimple() {
            Func<CancellationToken, ValueTask<int>> factory = ct => new ValueTask<int>(5);
            var tpc = new TaskModel<int>(factory);

            void OnPropertyChanged(object sender, PropertyChangedEventArgs args) {
            }

            Assert.False(tpc.HasValue);
            Assert.Equal(0, tpc.Value);

            tpc.PropertyChanged += OnPropertyChanged;

            Assert.True(tpc.HasValue);
            Assert.Equal(5, tpc.Value);

            tpc.PropertyChanged -= OnPropertyChanged;

            Assert.True(tpc.HasValue);
            Assert.Equal(5, tpc.Value);
        }

        [Fact]
        public async Task TestWaiting() {
            var giveResult = new SemaphoreSlim(0);
            var gotResult = new SemaphoreSlim(0);

            Func<CancellationToken, ValueTask<int>> factory = async ct => {
                await giveResult.WaitAsync(ct).ConfigureAwait(false);
                return 5;
            };

            void OnPropertyChanged(object sender, PropertyChangedEventArgs args) {
                gotResult.Release();
            }

            var tpc = TaskPropertyChanged.Create(factory);

            Assert.False(tpc.HasValue);
            Assert.Equal(0, tpc.Value);

            tpc.PropertyChanged += OnPropertyChanged;

            Assert.False(tpc.HasValue);
            Assert.Equal(0, tpc.Value);

            giveResult.Release();

            if (!await gotResult.WaitAsync(5_000)) {
                throw new Exception("Deadlock");
            }

            Assert.True(tpc.HasValue);
            Assert.Equal(5, tpc.Value);
        }

        [Fact]
        public async Task TestCancellation() {
            var giveResult = new SemaphoreSlim(0);
            var gotResult = new SemaphoreSlim(0);

            Func<CancellationToken, ValueTask<int>> factory = async ct => {
                await giveResult.WaitAsync(ct).ConfigureAwait(false);
                return 5;
            };

            void OnPropertyChanged(object sender, PropertyChangedEventArgs args) {
                gotResult.Release();
            }

            var tpc = TaskPropertyChanged.Create(factory);

            Assert.False(tpc.HasValue);
            Assert.Equal(0, tpc.Value);

            tpc.PropertyChanged += OnPropertyChanged;

            Assert.False(tpc.HasValue);
            Assert.Equal(0, tpc.Value);

            tpc.PropertyChanged -= OnPropertyChanged;

            Assert.False(tpc.HasValue);
            Assert.Equal(0, tpc.Value);

            giveResult = new SemaphoreSlim(0);

            tpc.PropertyChanged += OnPropertyChanged;

            giveResult.Release();

            if (!await gotResult.WaitAsync(5_000)) {
                throw new Exception("Deadlock");
            }

            Assert.True(tpc.HasValue);
            Assert.Equal(5, tpc.Value);
        }
    }
}
