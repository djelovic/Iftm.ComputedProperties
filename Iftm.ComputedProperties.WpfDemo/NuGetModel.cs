﻿using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Iftm.ComputedProperties.WpfDemo {

    public class NuGetModel : WithCachedProperties {
        private static ComputedProperty<NuGetModel, T> Computed<T>(Expression<Func<NuGetModel, T>> expression) => Computed<NuGetModel, T>(expression);

        private string _searchString = "";

        public string SearchString {
            get => _searchString;
            set => SetProperty(ref _searchString, value);
        }

        private static async ValueTask<IEnumerable<IPackageSearchMetadata>> SearchAsync(string searchString, CancellationToken ct) {
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
            return metadata.ToList();
        }

        private static ComputedProperty<NuGetModel, TaskModel<IEnumerable<IPackageSearchMetadata>>> _searcResults =
            Computed(model => TaskModel.Create(model.SearchString, SearchAsync));

        private TaskModel<IEnumerable<IPackageSearchMetadata>>? _lastSearchResults;

        public TaskModel<IEnumerable<IPackageSearchMetadata>> SearchResults =>
            _searcResults.Eval(this, ref _lastSearchResults);

        private static ComputedProperty<NuGetModel, Visibility> _searchProgressVisibility =
            Computed(model => model.SearchResults.HasValue ? Visibility.Hidden : Visibility.Visible);

        public Visibility SearchProgressVisibility => _searchProgressVisibility.Eval(this);
    }

}
