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
    }

}
