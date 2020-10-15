﻿using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Bonsai.NuGet
{
    public static class SourceRepositoryExtensions
    {
        public static bool Exists(this SourceRepository repository, PackageIdentity identity, CancellationToken token = default)
        {
            var findPackageResource = repository.GetResource<FindLocalPackagesResource>(token);
            return findPackageResource.Exists(identity, NullLogger.Instance, token);
        }

        public static LocalPackageInfo FindLocalPackage(this SourceRepository repository, string id, CancellationToken token = default)
        {
            var findPackageResource = repository.GetResource<FindLocalPackagesResource>(token);
            var packageInfo = findPackageResource.FindPackagesById(id, NullLogger.Instance, token);
            return packageInfo.FirstOrDefault();
        }

        public static async Task<LocalPackageInfo> FindLocalPackageAsync(this SourceRepository repository, string id, CancellationToken token = default)
        {
            var findPackageResource = await repository.GetResourceAsync<FindLocalPackagesResource>(token);
            var packageInfo = findPackageResource.FindPackagesById(id, NullLogger.Instance, token);
            return packageInfo.FirstOrDefault();
        }

        public static LocalPackageInfo GetLocalPackage(this SourceRepository repository, PackageIdentity identity, CancellationToken token = default)
        {
            var findPackageResource = repository.GetResource<FindLocalPackagesResource>(token);
            return findPackageResource.GetPackage(identity, NullLogger.Instance, token);
        }

        public static IEnumerable<LocalPackageInfo> GetLocalPackages(this SourceRepository repository, CancellationToken token = default)
        {
            var findPackageResource = repository.GetResource<FindLocalPackagesResource>(token);
            return findPackageResource.GetPackages(NullLogger.Instance, token);
        }

        public static async Task<IEnumerable<IPackageSearchMetadata>> SearchAsync(this SourceRepository repository, string searchTerm, SearchFilter filters, int skip, int take, CancellationToken token = default)
        {
            var searchPackageResource = await repository.GetResourceAsync<PackageSearchResource>(token);
            return await searchPackageResource.SearchAsync(searchTerm, filters, skip, take, NullLogger.Instance, token);
        }

        public static async Task<IEnumerable<IPackageSearchMetadata>> GetMetadataAsync(this SourceRepository repository, string id, bool includePrerelease, CancellationToken token = default)
        {
            using var cacheContext = new SourceCacheContext { MaxAge = DateTimeOffset.UtcNow };
            var packageMetadataResource = await repository.GetResourceAsync<PackageMetadataResource>(token);
            return await packageMetadataResource.GetMetadataAsync(id, includePrerelease, includeUnlisted: false, cacheContext, NullLogger.Instance, token);
        }

        public static async Task<IEnumerable<IPackageSearchMetadata>> GetUpdatesAsync(this SourceRepository repository, IEnumerable<LocalPackageInfo> localPackages, bool includePrerelease, CancellationToken token = default)
        {
            var updatePackages = new List<IPackageSearchMetadata>();
            using var cacheContext = new SourceCacheContext { MaxAge = DateTimeOffset.UtcNow };
            foreach (var package in localPackages)
            {
                var latestPackage = await GetLatestMetadataAsync(repository, package.Identity, includePrerelease, cacheContext, token);
                if (latestPackage != null)
                {
                    updatePackages.Add(latestPackage);
                }
            }

            return updatePackages;
        }

        public static Task<IPackageSearchMetadata> GetLatestMetadataAsync(this SourceRepository repository, PackageIdentity identity, bool includePrerelease, SourceCacheContext cacheContext, CancellationToken token = default)
        {
            return GetLatestMetadataAsync(repository, identity.Id, new VersionRange(identity.Version, includeMinVersion: false), includePrerelease, cacheContext, token);
        }

        public static async Task<IPackageSearchMetadata> GetLatestMetadataAsync(this SourceRepository repository, string id, VersionRange version, bool includePrerelease, SourceCacheContext cacheContext, CancellationToken token = default)
        {
            var packageMetadataResource = await repository.GetResourceAsync<PackageMetadataResource>(token);
            var packages = await packageMetadataResource.GetMetadataAsync(id, includePrerelease, includeUnlisted: false, cacheContext, NullLogger.Instance, token);
            return packages
                .Where(package => version.Satisfies(package.Identity.Version))
                .OrderByDescending(package => package.Identity.Version, VersionComparer.VersionRelease)
                .FirstOrDefault();
        }
    }
}
