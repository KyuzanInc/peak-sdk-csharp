using System.Runtime.CompilerServices;

// The client csproj sets GenerateAssemblyInfo=false, so MSBuild <InternalsVisibleTo>
// items are ignored. Emit the attributes explicitly. This file lives OUTSIDE the
// regenerated src/ tree, so the generation script and drift CI never touch it.
[assembly: InternalsVisibleTo("KyuzanInc.Peak.Sdk.Tests")]
