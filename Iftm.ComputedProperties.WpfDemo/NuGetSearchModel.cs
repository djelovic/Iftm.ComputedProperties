using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Iftm.ComputedProperties.WpfDemo {

    public class NuGetSearchModel : WithStoredComputedProperties {
        private string _searchString = "";

        public string SearchString {
            get => _searchString;
            set => SetProperty(ref _searchString, value);
        }

        private static async ValueTask<IReadOnlyList<IPackageSearchMetadata>> SearchNuGetAsync(string searchString, CancellationToken ct) {
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

        private static ComputedProperty<NuGetSearchModel, TaskModel<IReadOnlyList<IPackageSearchMetadata>>> _search = Computed(
            (NuGetSearchModel model) => TaskModel.Create(model.SearchString, (searchString, ct) => SearchNuGetAsync(searchString, ct)));

        private StoredComputedProperty<NuGetSearchModel, TaskModel<IReadOnlyList<IPackageSearchMetadata>>> _searchResults =
            _search.Stored;

        public TaskModel<IReadOnlyList<IPackageSearchMetadata>> SearchResults =>
            _searchResults.Eval(this);
    }

}
