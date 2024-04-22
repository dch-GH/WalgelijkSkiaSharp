﻿using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Walgelijk.AssetManager;

public static class Assets
{
    private static readonly ConcurrentDictionary<int, AssetPackage> packageRegistry = [];
    private static readonly ConcurrentDictionary<GlobalAssetId, ConcurrentBag<IDisposable>> disposableChain = [];
    private static readonly ConcurrentDictionary<GlobalAssetId, GlobalAssetId> replacementTable = [];

    private static readonly SemaphoreSlim enumerationLock = new(1);
    private static bool isDisposingPackage = false;

    static Assets()
    {
        Resources.RegisterType(typeof(AssetPackage), AssetPackage.Load);
    }

    public static AssetPackage RegisterPackage(string path)
    {
        enumerationLock.Wait();
        try
        {
            var assetPackage = Resources.Load<AssetPackage>(path, true);
            var numerical = Hashes.MurmurHash1(assetPackage.Metadata.Id);
            if (!packageRegistry.TryAdd(numerical, assetPackage))
            {
                Resources.Unload(assetPackage);
                throw new Exception($"Package with id {numerical} already exists");
            }
            return assetPackage;
        }
        finally
        {
            enumerationLock.Release();
        }
    }

    public static IEnumerable<int> AssetPackages => packageRegistry.Keys;

    public static void ClearRegistry()
    {
        if (isDisposingPackage)
            throw new InvalidOperationException("The package registry cannot be cleared while disposing a package");

        enumerationLock.Wait();
        try
        {
            var keys = new Stack<int>(AssetPackages);
            while (keys.TryPop(out var id))
            {
                if (packageRegistry.TryGetValue(id, out var p))
                {
                    Resources.Unload(p);
                    p.Dispose();
                }
            }
            packageRegistry.Clear();
        }
        finally
        {
            enumerationLock.Release();
        }
    }

    public static bool TryGetPackage(int id, [NotNullWhen(true)] out AssetPackage? assetPackage)
    {
        if (isDisposingPackage)
            throw new InvalidOperationException("Packages cannot be retrieved while disposing a package");

        enumerationLock.Wait();
        try
        {
            return packageRegistry.TryGetValue(id, out assetPackage);
        }
        finally
        {
            enumerationLock.Release();
        }
    }

    public static bool TryGetPackage(in ReadOnlySpan<char> id, [NotNullWhen(true)] out AssetPackage? assetPackage)
        => TryGetPackage(Hashes.MurmurHash1(id), out assetPackage);

    public static bool UnloadPackage(int id)
    {
        enumerationLock.Wait();
        try
        {
            if (TryGetPackage(id, out var p))
            {
                isDisposingPackage = true;
                Resources.Unload(p);
                p.Dispose();
                return true;
            }
            else
                return false;
        }
        finally
        {
            isDisposingPackage = false;
            enumerationLock.Release();
        }
    }

    public static bool UnloadPackage(in ReadOnlySpan<char> id)
        => UnloadPackage(Hashes.MurmurHash1(id));

    public static void AssignLifetime(GlobalAssetId id, ILifetimeOperator lifetimeOperator)
    {
        if (id.External == 0)
            throw new Exception("Id is None");

        lifetimeOperator.Triggered.AddListener(() =>
        {
            DisposeOf(id);
        });
    }

    /// <summary>
    /// Link an <see cref="IDisposable"/> instance to an asset, so that when the asset is disposed, the specified <see cref="IDisposable"/> is disposed as well
    /// </summary>
    public static void LinkDisposal(in GlobalAssetId id, IDisposable d)
    {
        var set = disposableChain.Ensure(id);
        set.Add(d);
    }

    /// <summary>
    /// Add an entry to the replacement table. Note how this will override the existing replacement.
    /// Note how this will dispose of the original asset.
    /// </summary>
    /// <param name="original">The original asset ID - the one that is to be replaced</param>
    /// <param name="replacement">The substitute ID</param>
    public static void SetReplacement(in GlobalAssetId original, in GlobalAssetId replacement)
    {
        if (original.External == 0)
            throw new Exception("Id is None");

        if (replacement.External == 0)
            throw new Exception("Id is None");

        enumerationLock.Wait();
        try
        {
            replacementTable.AddOrSet(original, replacement);
            DisposeOf(original); // we have to dispose the original to force a reload
        }
        finally
        {
            enumerationLock.Release();
        }
    }

    /// <summary>
    /// Clear the set replacement for the given original ID. 
    /// If any replacements were set using <see cref="SetReplacement(GlobalAssetId, GlobalAssetId)"/>, 
    /// they will be undone and the asset will refer to itself again.
    /// Note how this will dispose of the original asset.
    /// </summary>
    public static void ClearReplacement(in GlobalAssetId original)
    {
        if (original.External == 0)
            throw new Exception("Id is None");

        enumerationLock.Wait();
        try
        {
            replacementTable.TryRemove(original, out _);
            DisposeOf(original);
        }
        finally
        {
            enumerationLock.Release();
        }
    }

    /// <summary>
    /// Clear all set replacements.
    /// </summary>
    public static void ClearReplacements()
    {
        enumerationLock.Wait();
        try
        {
            var keys = replacementTable.Keys.ToArray();
            replacementTable.Clear();
            foreach (var k in keys)
                DisposeOf(k);
        }
        finally
        {
            enumerationLock.Release();
        }
    }

    public static bool HasReplacement(in GlobalAssetId original) => replacementTable.ContainsKey(original);

    /// <summary>
    /// If the given ID is remapped in the <see cref="replacementTable"/>
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static GlobalAssetId ApplyReplacement(in GlobalAssetId id)
    {
        if (id.External == 0)
            throw new Exception("Id is None");

        if (replacementTable.TryGetValue(id, out var replacement))
            return replacement;
        return id;
    }

    /// <summary>
    /// Dispose the asset
    /// </summary>
    /// <param name="id"></param>
    public static void DisposeOf(GlobalAssetId id)
    {
        if (id.External == 0)
            throw new Exception("Id is None");

        if (disposableChain.TryRemove(id, out var set))
            while (set.TryTake(out var d))
                try
                {
                    d.Dispose();
                }
                catch (Exception e)
                {
                    Logger.Error($"Disposal chain error while disposing {id}: {e}");
                }

        if (packageRegistry.TryGetValue(id.External, out var assetPackage))
            assetPackage.DisposeOf(id.Internal);
    }

    public static AssetRef<T> Load<T>(in ReadOnlySpan<char> id) => Load<T>(new GlobalAssetId(id));

    /// <summary>
    /// Load the asset with the given ID. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="id"></param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException"></exception>
    public static AssetRef<T> Load<T>(GlobalAssetId id)
    {
        if (id.External == 0)
            throw new Exception("Id is None");

        var replacementId = ApplyReplacement(id);

        if (packageRegistry.TryGetValue(replacementId.External, out var assetPackage))
            return new AssetRef<T>(id);
        throw new KeyNotFoundException($"Asset package {replacementId.External} not found");
    }

    /// <summary>
    /// Directly load the data without caching it. 
    /// Use with caution, as the resulting object will be disconnected from the asset manager
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="id"></param>
    /// <returns></returns>
    public static T LoadNoCache<T>(GlobalAssetId id)
    {
        if (id.External == 0)
            throw new Exception("Id is None");

        var replacementId = ApplyReplacement(id);

        if (packageRegistry.TryGetValue(replacementId.External, out var assetPackage))
            return assetPackage.LoadNoCache<T>(id.Internal);
        throw new KeyNotFoundException($"Asset package {replacementId.External} not found");
    }

    public static AssetMetadata GetMetadata(GlobalAssetId id)
    {
        if (id.External == 0)
            throw new Exception("Id is None");

        var replacementId = ApplyReplacement(id);

        if (packageRegistry.TryGetValue(replacementId.External, out var assetPackage))
            return assetPackage.GetAssetMetadata(id.Internal);

        throw new KeyNotFoundException($"Asset package {replacementId.External} not found");
    }

    public static IEnumerable<GlobalAssetId> EnumerateFolder(string folder)
    {
        foreach (var p in packageRegistry.Values)
            foreach (var a in p.EnumerateFolder(folder))
                yield return new GlobalAssetId(p.Metadata.Id, a);
    }

    public static IEnumerable<GlobalAssetId> QueryTags(string tag)
    {
        foreach (var p in packageRegistry.Values)
            foreach (var a in p.QueryTags(tag))
                yield return new GlobalAssetId(p.Metadata.Id, a);
    }

    internal static T LoadDirect<T>(GlobalAssetId id)
    {
        var replacementId = ApplyReplacement(id);

        if (packageRegistry.TryGetValue(replacementId.External, out var assetPackage))
            return assetPackage.Load<T>(replacementId.Internal);
        throw new KeyNotFoundException($"Asset package {replacementId.External} not found");
    }
}
