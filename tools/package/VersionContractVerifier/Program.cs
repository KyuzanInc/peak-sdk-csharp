using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace KyuzanInc.Peak.Tools.VersionContractVerifier;

internal static class Program
{
    private const string TurnkeyPackageId = "KyuzanInc.Turnkey.Sdk";
    private const string TurnkeyCentralVersion = "[1.0.0]";
    private const string TurnkeyLockRequest = "[1.0.0, 1.0.0]";
    private const string TurnkeyResolvedVersion = "1.0.0";
    private const string PeakVersion = "1.0.0";

    private static readonly string[] ItemOperationAttributes =
    {
        "Include",
        "Update",
        "Remove",
    };

    private static readonly string[] RequiredLockPaths =
    {
        "packages/peak-sdk-csharp/src/packages.lock.json",
        "packages/peak-sdk-csharp/tests/packages.lock.json",
    };

    public static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("usage: VersionContractVerifier <repository-root>");
            return 1;
        }

        string repositoryRoot = Path.GetFullPath(args[0]);
        var errors = new List<string>();

        ValidateCentralPackageDeclaration(
            Path.Combine(repositoryRoot, "Directory.Packages.props"),
            errors);
        ValidatePeakProject(
            Path.Combine(
                repositoryRoot,
                "packages",
                "peak-sdk-csharp",
                "src",
                "peak-sdk-csharp.csproj"),
            errors);

        foreach (string lockPath in RequiredLockPaths)
        {
            ValidateLockFile(repositoryRoot, lockPath, errors);
        }

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Console.Error.WriteLine(error);
            }

            return 1;
        }

        Console.WriteLine("version-contract verification passed");
        return 0;
    }

    private static void ValidateCentralPackageDeclaration(
        string path,
        ICollection<string> errors)
    {
        XDocument? document = LoadXml(path, "Directory.Packages.props", errors);
        if (document?.Root is null)
        {
            return;
        }

        List<XElement> declarations = FindMatchingPackageItems(
            document.Root,
            "PackageVersion");

        if (declarations.Count != 1)
        {
            errors.Add(
                "Directory.Packages.props must contain exactly one active " +
                $"{TurnkeyPackageId} PackageVersion declaration; found {declarations.Count}.");
            return;
        }

        XElement declaration = declarations[0];
        string? include = (string?)declaration.Attribute("Include");
        string? version = (string?)declaration.Attribute("Version");
        bool hasOverrideOperation =
            declaration.Attribute("Update") is not null ||
            declaration.Attribute("Remove") is not null;

        if (!string.Equals(include, TurnkeyPackageId, StringComparison.Ordinal) ||
            hasOverrideOperation ||
            !string.Equals(version, TurnkeyCentralVersion, StringComparison.Ordinal))
        {
            errors.Add(
                $"{TurnkeyPackageId} PackageVersion must use the single canonical " +
                $"Include form at {TurnkeyCentralVersion} with no Update or Remove.");
        }
    }

    private static void ValidatePeakProject(string path, ICollection<string> errors)
    {
        XDocument? document = LoadXml(path, "Peak SDK project", errors);
        if (document?.Root is null)
        {
            return;
        }

        List<XElement> versionElements = document
            .Root
            .DescendantsAndSelf()
            .Where(element => element.Name.LocalName == "PropertyGroup")
            .SelectMany(group => group.Elements())
            .Where(element => element.Name.LocalName == "Version")
            .ToList();

        if (versionElements.Count != 1 ||
            !string.Equals(
                versionElements.SingleOrDefault()?.Value.Trim(),
                PeakVersion,
                StringComparison.Ordinal))
        {
            errors.Add(
                $"Peak SDK project must contain exactly one active <Version>{PeakVersion}</Version>.");
        }

        List<XElement> references = FindMatchingPackageItems(
            document.Root,
            "PackageReference");

        if (references.Count != 1)
        {
            errors.Add(
                "Peak SDK project must contain exactly one active " +
                $"{TurnkeyPackageId} PackageReference; found {references.Count}.");
            return;
        }

        XElement reference = references[0];
        bool hasCanonicalInclude = string.Equals(
            (string?)reference.Attribute("Include"),
            TurnkeyPackageId,
            StringComparison.Ordinal);
        bool hasOverrideOperation =
            reference.Attribute("Update") is not null ||
            reference.Attribute("Remove") is not null;
        bool hasVersionAttribute = reference
            .Attributes()
            .Any(attribute =>
                attribute.Name.LocalName is "Version" or "VersionOverride");
        bool hasVersionElement = reference
            .Elements()
            .Any(element =>
                element.Name.LocalName is "Version" or "VersionOverride");

        if (!hasCanonicalInclude ||
            hasOverrideOperation ||
            hasVersionAttribute ||
            hasVersionElement)
        {
            errors.Add(
                $"{TurnkeyPackageId} PackageReference must use the single canonical " +
                "unversioned Include form with no Update or Remove.");
        }
    }

    private static List<XElement> FindMatchingPackageItems(
        XElement root,
        string itemName)
    {
        return root
            .DescendantsAndSelf()
            .Where(element =>
                element.Name.LocalName == itemName &&
                ItemOperationAttributes.Any(attributeName =>
                    TargetsTurnkey(element.Attribute(attributeName))))
            .ToList();
    }

    private static bool TargetsTurnkey(XAttribute? attribute)
    {
        if (attribute is null)
        {
            return false;
        }

        return attribute
            .Value
            .Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Any(itemSpec =>
                FileSystemName.MatchesSimpleExpression(
                    itemSpec,
                    TurnkeyPackageId,
                    ignoreCase: true));
    }

    private static XDocument? LoadXml(
        string path,
        string description,
        ICollection<string> errors)
    {
        if (!File.Exists(path))
        {
            errors.Add($"Missing {description}: {path}");
            return null;
        }

        try
        {
            return XDocument.Load(path, LoadOptions.None);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            errors.Add($"Invalid {description}: {exception.Message}");
            return null;
        }
    }

    private static void ValidateLockFile(
        string repositoryRoot,
        string relativePath,
        ICollection<string> errors)
    {
        string path = Path.Combine(repositoryRoot, relativePath);
        if (!File.Exists(path))
        {
            errors.Add($"Missing required lock file: {relativePath}");
            return;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllBytes(path),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                });

            int turnkeyObjectCount = 0;
            ValidateJsonElement(
                document.RootElement,
                relativePath,
                errors,
                ref turnkeyObjectCount);

            if (turnkeyObjectCount == 0)
            {
                errors.Add(
                    $"{relativePath} must contain at least one direct or " +
                    $"CentralTransitive {TurnkeyPackageId} lock object.");
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            errors.Add($"Invalid lock file {relativePath}: {exception.Message}");
        }
    }

    private static void ValidateJsonElement(
        JsonElement element,
        string location,
        ICollection<string> errors,
        ref int turnkeyObjectCount)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var propertyNames = new HashSet<string>(StringComparer.Ordinal);
                int turnkeyIdentityCount = 0;
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (!propertyNames.Add(property.Name))
                    {
                        errors.Add($"{location} contains duplicate JSON property {property.Name}.");
                    }

                    string propertyLocation = $"{location}.{property.Name}";
                    bool isTurnkeyIdentity = string.Equals(
                        property.Name,
                        TurnkeyPackageId,
                        StringComparison.OrdinalIgnoreCase);
                    if (isTurnkeyIdentity)
                    {
                        turnkeyIdentityCount++;
                        if (!string.Equals(
                            property.Name,
                            TurnkeyPackageId,
                            StringComparison.Ordinal))
                        {
                            errors.Add(
                                $"{location} contains noncanonical Turnkey package key " +
                                $"{property.Name}; expected {TurnkeyPackageId}.");
                        }

                        ValidateTurnkeyEntry(
                            property.Value,
                            propertyLocation,
                            errors,
                            ref turnkeyObjectCount);
                    }

                    ValidateJsonElement(
                        property.Value,
                        propertyLocation,
                        errors,
                        ref turnkeyObjectCount);
                }

                if (turnkeyIdentityCount > 1)
                {
                    errors.Add(
                        $"{location} contains multiple case-insensitive Turnkey package keys.");
                }

                break;

            case JsonValueKind.Array:
                int index = 0;
                foreach (JsonElement item in element.EnumerateArray())
                {
                    ValidateJsonElement(
                        item,
                        $"{location}[{index}]",
                        errors,
                        ref turnkeyObjectCount);
                    index++;
                }

                break;
        }
    }

    private static void ValidateTurnkeyEntry(
        JsonElement value,
        string location,
        ICollection<string> errors,
        ref int turnkeyObjectCount)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            string? dependencyVersion = value.GetString();
            if (!string.Equals(
                dependencyVersion,
                TurnkeyLockRequest,
                StringComparison.Ordinal))
            {
                errors.Add(
                    $"{location} dependency range must be exactly " +
                    $"{TurnkeyLockRequest}; found {dependencyVersion ?? "<missing>"}.");
            }

            return;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{location} must be a lock object or dependency version string.");
            return;
        }

        if (!TryGetString(value, "type", out string? type) ||
            type is not ("Direct" or "CentralTransitive"))
        {
            errors.Add(
                $"{location} lock object must have type Direct or CentralTransitive.");
            return;
        }

        turnkeyObjectCount++;
        RequireExactString(
            value,
            "requested",
            TurnkeyLockRequest,
            location,
            errors);
        RequireExactString(
            value,
            "resolved",
            TurnkeyResolvedVersion,
            location,
            errors);
    }

    private static void RequireExactString(
        JsonElement value,
        string propertyName,
        string expected,
        string location,
        ICollection<string> errors)
    {
        if (!TryGetString(value, propertyName, out string? actual) ||
            !string.Equals(actual, expected, StringComparison.Ordinal))
        {
            errors.Add(
                $"{location}.{propertyName} must be exactly {expected}; " +
                $"found {actual ?? "<missing>"}.");
        }
    }

    private static bool TryGetString(
        JsonElement value,
        string propertyName,
        out string? result)
    {
        if (value.TryGetProperty(propertyName, out JsonElement property) &&
            property.ValueKind == JsonValueKind.String)
        {
            result = property.GetString();
            return true;
        }

        result = null;
        return false;
    }
}
