using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace KyuzanInc.Peak.Tools.PackageSymbolVerifier;

internal static class Program
{
    private const string AssemblyName = "KyuzanInc.Peak.Sdk";

    public static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine(
                "usage: PackageSymbolVerifier <nupkg> <snupkg>");
            return 1;
        }

        try
        {
            using ZipArchive package = ZipFile.OpenRead(args[0]);
            using ZipArchive symbols = ZipFile.OpenRead(args[1]);

            List<ZipArchiveEntry> assemblies = package.Entries
                .Where(IsSdkAssembly)
                .OrderBy(entry => entry.FullName, StringComparer.Ordinal)
                .ToList();

            if (assemblies.Count == 0)
            {
                Console.Error.WriteLine(
                    $"package has no lib/<tfm>/{AssemblyName}.dll entries");
                return 1;
            }

            var errors = new List<string>();
            foreach (ZipArchiveEntry assembly in assemblies)
            {
                string pdbPath = assembly.FullName[..^4] + ".pdb";
                ZipArchiveEntry? pdb = symbols.GetEntry(pdbPath);
                if (pdb is null || pdb.FullName.EndsWith("/", StringComparison.Ordinal))
                {
                    errors.Add($"symbol package is missing {pdbPath}");
                    continue;
                }

                ValidatePair(assembly, pdb, errors);
            }

            if (errors.Count > 0)
            {
                foreach (string error in errors)
                {
                    Console.Error.WriteLine(error);
                }

                return 1;
            }

            Console.WriteLine("portable-symbol verification passed");
            return 0;
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or
            IOException or
            InvalidDataException or
            UnauthorizedAccessException)
        {
            Console.Error.WriteLine(
                $"portable-symbol verification failed: {exception.Message}");
            return 1;
        }
    }

    private static bool IsSdkAssembly(ZipArchiveEntry entry)
    {
        string[] parts = entry.FullName.Split('/');
        return parts is ["lib", _, $"{AssemblyName}.dll"];
    }

    private static void ValidatePair(
        ZipArchiveEntry assembly,
        ZipArchiveEntry pdb,
        ICollection<string> errors)
    {
        try
        {
            using var assemblyImage = new MemoryStream(
                ReadEntry(assembly),
                writable: false);
            using var peReader = new PEReader(
                assemblyImage,
                PEStreamOptions.PrefetchEntireImage);

            if (!peReader.HasMetadata)
            {
                errors.Add($"SDK assembly has no managed metadata: {assembly.FullName}");
                return;
            }

            ImmutableArray<DebugDirectoryEntry> codeViewEntries = peReader
                .ReadDebugDirectory()
                .Where(entry => entry.Type == DebugDirectoryEntryType.CodeView)
                .ToImmutableArray();
            if (codeViewEntries.Length != 1)
            {
                errors.Add(
                    $"SDK assembly must contain exactly one CodeView entry: " +
                    $"{assembly.FullName}; found {codeViewEntries.Length}");
                return;
            }

            DebugDirectoryEntry codeViewEntry = codeViewEntries[0];
            if (!codeViewEntry.IsPortableCodeView)
            {
                errors.Add(
                    $"SDK assembly CodeView entry is not portable: {assembly.FullName}");
                return;
            }

            CodeViewDebugDirectoryData codeView =
                peReader.ReadCodeViewDebugDirectoryData(codeViewEntry);
            if (codeView.Age != 1)
            {
                errors.Add(
                    $"SDK assembly CodeView age must be 1: {assembly.FullName}; " +
                    $"found {codeView.Age}");
                return;
            }

            using var pdbImage = new MemoryStream(ReadEntry(pdb), writable: false);
            using MetadataReaderProvider provider =
                MetadataReaderProvider.FromPortablePdbStream(
                    pdbImage,
                    MetadataStreamOptions.PrefetchMetadata);
            MetadataReader metadataReader = provider.GetMetadataReader();
            DebugMetadataHeader? debugHeader = metadataReader.DebugMetadataHeader;
            if (debugHeader is null)
            {
                errors.Add($"portable PDB has no debug metadata header: {pdb.FullName}");
                return;
            }

            var pdbContentId = new BlobContentId(debugHeader.Id);
            if (pdbContentId.IsDefault)
            {
                errors.Add($"portable PDB has an empty content identifier: {pdb.FullName}");
                return;
            }

            if (codeView.Guid != pdbContentId.Guid ||
                codeViewEntry.Stamp != pdbContentId.Stamp)
            {
                errors.Add(
                    $"portable PDB content identifier does not match its DLL: " +
                    $"{assembly.FullName} -> {pdb.FullName}");
            }
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or
            IOException or
            InvalidDataException)
        {
            errors.Add(
                $"invalid DLL/PDB pair {assembly.FullName} -> {pdb.FullName}: " +
                exception.Message);
        }
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry)
    {
        using Stream source = entry.Open();
        using var destination = new MemoryStream(
            entry.Length <= int.MaxValue ? checked((int)entry.Length) : 0);
        source.CopyTo(destination);
        return destination.ToArray();
    }
}
