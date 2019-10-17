using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Iftm.ComputedProperties.WpfDemo {

    public class NuGetModel : WithCachedProperties {
        private string _searchString = "";

        public string SearchString {
            get => _searchString;
            set => SetProperty(ref _searchString, value);
        }

        private static async ValueTask<IReadOnlyList<IPackageSearchMetadata>> SearchAsync(string searchString, CancellationToken ct) {
            searchString = searchString.Trim();
            if (searchString == "") return Array.Empty<IPackageSearchMetadata>();

            await Task.Delay(800, ct);

            var providers = new List<Lazy<INuGetResourceProvider>>();
            providers.AddRange(Repository.Provider.GetCoreV3()); // Add v3 API support
            var packageSource = new PackageSource("https://api.nuget.org/v3/index.json");
            var source = new SourceRepository(packageSource, providers);

            var filter = new SearchFilter(false);
            var resource = await source.GetResourceAsync<PackageSearchResource>().ConfigureAwait(false);
            var metadata = await resource.SearchAsync(searchString, filter, 0, 10, null, ct).ConfigureAwait(false);
            var ret = metadata.ToList();
            return ret;
        }

        private static ComputedProperty<NuGetModel, TaskModel<IReadOnlyList<IPackageSearchMetadata>>> _searcResults = Computed(
            (NuGetModel model) => TaskModel.Create(model.SearchString, SearchAsync)
        );

        #pragma warning disable 8618
        private TaskModel<IReadOnlyList<IPackageSearchMetadata>> _lastSearchResults;
        #pragma warning restore 8618

        public TaskModel<IReadOnlyList<IPackageSearchMetadata>> SearchResults =>
            _searcResults.Eval(this, ref _lastSearchResults);

        private static ComputedProperty<NuGetModel, Visibility> _searchProgressVisibility = Computed(
            (NuGetModel model) => model.SearchResults.HasValue ? Visibility.Hidden : Visibility.Visible
        );

        public Visibility SearchProgressVisibility => _searchProgressVisibility.Eval(this);



        private int _a;

        public int A {
            get => _a;
            set => SetProperty(ref _a, value);
        }

        private async static ValueTask<int> AsyncFunction(int num, CancellationToken ct) {
            await Task.Delay(2_000, ct);
            return num + 1;
        }

        private ComputedProperty<NuGetModel, TaskModel<int>> _b =>
            Computed((NuGetModel obj) => TaskModel.Create(obj.A, AsyncFunction));

        #nullable disable
        private TaskModel<int> _lastB;
        #nullable enable

        public TaskModel<int> B => _b.Eval(this, ref _lastB);
    }

}
