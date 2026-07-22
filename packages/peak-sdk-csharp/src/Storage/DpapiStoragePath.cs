using System;
using System.IO;

namespace KyuzanInc.Peak.Sdk.Storage
{
    internal static class DpapiStoragePath
    {
        internal static string ResolveBaseDirectory(
            string? baseDirectory,
            string? @namespace,
            string localApplicationDataDirectory)
        {
            var safeNamespace = ValidateNamespace(@namespace);
            if (baseDirectory != null)
            {
                // An explicit directory is a consumer-owned override. Preserve
                // its existing semantics and do not append the namespace.
                return baseDirectory;
            }

            var root = Path.GetFullPath(Path.Combine(
                localApplicationDataDirectory,
                "KyuzanInc",
                "PeakSdk"));
            var candidate = Path.GetFullPath(Path.Combine(root, safeNamespace));
            var rootWithSeparator = root.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var pathComparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (!candidate.StartsWith(rootWithSeparator, pathComparison))
            {
                throw new ArgumentException(
                    "DpapiSecureStorage namespace must resolve inside the default PeakSdk directory.",
                    nameof(@namespace));
            }

            return candidate;
        }

        private static string ValidateNamespace(string? @namespace)
        {
            if (string.IsNullOrEmpty(@namespace))
            {
                return "default";
            }

            if (@namespace == "." || @namespace == "..")
            {
                throw new ArgumentException(
                    "DpapiSecureStorage namespace cannot be '.' or '..'.",
                    nameof(@namespace));
            }

            foreach (var character in @namespace!)
            {
                var valid = (character >= 'a' && character <= 'z')
                    || (character >= 'A' && character <= 'Z')
                    || (character >= '0' && character <= '9')
                    || character == '.'
                    || character == '_'
                    || character == '-';
                if (!valid)
                {
                    throw new ArgumentException(
                        "DpapiSecureStorage namespace must match [A-Za-z0-9._-]+.",
                        nameof(@namespace));
                }
            }

            return @namespace!;
        }
    }
}
