module Paket.IntegrationTests.ConvertFromNuGetSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Diagnostics
open Paket
open Paket.Domain
open Paket.Requirements

[<Test>]
let ``#1217 should convert simple C# project``() = 
    use __ = paket "convert-from-nuget" "i001217-convert-simple-project" |> fst
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001217-convert-simple-project","paket.lock"))
    let v = lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Newtonsoft.Json"].Version
    v.Major |> shouldEqual 7u
    v.Minor |> shouldEqual 0u
    v.Patch |> shouldEqual 1u

    let depsFile = DependenciesFile.ReadFromFile(Path.Combine(scenarioTempPath "i001217-convert-simple-project","paket.dependencies"))
    let requirement = depsFile.GetGroup(Constants.MainDependencyGroup).Packages.Head
    requirement.Name |> shouldEqual (PackageName "Newtonsoft.Json")
    requirement.VersionRequirement.ToString() |> shouldEqual "7.0.1"

[<Test>]
let ``#1225 should convert simple C# project with non-matching framework restrictions``() = 
    use __ = paket "convert-from-nuget" "i001225-convert-simple-project-non-matching-restrictions" |> fst
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001225-convert-simple-project-non-matching-restrictions","paket.lock"))
    let v = lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Newtonsoft.Json"].Version
    v.Major |> shouldEqual 7u
    v.Minor |> shouldEqual 0u
    v.Patch |> shouldEqual 1u

    let v2 = lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Castle.Core"].Version
    v2.Major |> shouldEqual 3u
    v2.Minor |> shouldEqual 3u
    v2.Patch |> shouldEqual 3u

    let depsFile = DependenciesFile.ReadFromFile(Path.Combine(scenarioTempPath "i001225-convert-simple-project-non-matching-restrictions","paket.dependencies"))
    let requirement = depsFile.GetGroup(Constants.MainDependencyGroup).Packages.Head
    requirement.Name |> shouldEqual (PackageName "Castle.Core")
    requirement.VersionRequirement.ToString() |> shouldEqual "3.3.3"
    requirement.ResolverStrategyForTransitives |> shouldEqual None
    requirement.Settings.FrameworkRestrictions  |> getExplicitRestriction |> shouldEqual (FrameworkRestriction.AtLeast(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V3_5)))

    let requirement2 = depsFile.GetGroup(Constants.MainDependencyGroup).Packages.Tail.Head
    requirement2.Name |> shouldEqual (PackageName "Newtonsoft.Json")
    requirement2.VersionRequirement.ToString() |> shouldEqual "7.0.1"
    requirement2.ResolverStrategyForTransitives |> shouldEqual None
    requirement2.Settings.FrameworkRestrictions  |> getExplicitRestriction |> shouldEqual (FrameworkRestriction.AtLeast(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V4)))

[<Test>]
let ``#1217 should replace packages.config files in project``() = 
    let originalProjectFile = ProjectFile.loadFromFile(Path.Combine(originalScenarioPath "i001217-convert-simple-project", "ClassLibrary1", "ClassLibrary1.csprojtemplate"))
    originalProjectFile.Document.OuterXml.Contains("packages.config") |> shouldEqual true
    originalProjectFile.Document.OuterXml.Contains("paket.references") |> shouldEqual false

    use __ = paket "convert-from-nuget" "i001217-convert-simple-project" |> fst
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001217-convert-simple-project","paket.lock"))
    let projectFile = ProjectFile.loadFromFile(Path.Combine(scenarioTempPath "i001217-convert-simple-project", "ClassLibrary1", "ClassLibrary1.csproj"))
    projectFile.Document.OuterXml.Contains("packages.config") |> shouldEqual false
    projectFile.Document.OuterXml.Contains("paket.references") |> shouldEqual true

[<Test>]
let ``#1591 should convert denormalized versions``() = 
    use __ = paket "convert-from-nuget" "i001591-convert-denormalized" |> fst
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001591-convert-denormalized","paket.lock"))
    let v = lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "EntityFramework"].Version
    v.Major |> shouldEqual 6u
    v.Minor |> shouldEqual 1u
    v.Patch |> shouldEqual 0u

    let depsFile = File.ReadAllText(Path.Combine(scenarioTempPath "i001591-convert-denormalized","paket.dependencies"))
    depsFile.Contains "6.1.0" |> shouldEqual true

[<Test>]
let ``#1922 should remove references to moved analyzers``() =
    let scenario = "i001922-convert-nuget-with-analyzers"
    use __ = paket "convert-from-nuget" scenario |> fst
    let projectFile = ProjectFile.loadFromFile(Path.Combine(scenarioTempPath scenario, "ConvertFromNugetWithAnalyzers", "ConvertFromNugetWithAnalyzers.csproj"))
    let projectXml = projectFile.Document.OuterXml
    StringAssert.DoesNotContain(@"<Analyzer Include=""..\packages\StyleCop.Analyzers.1.0.0\analyzers\dotnet\cs\Newtonsoft.Json.dll""", projectXml)
    StringAssert.DoesNotContain(@"<Analyzer Include=""..\packages\StyleCop.Analyzers.1.0.0\analyzers\dotnet\cs\StyleCop.Analyzers.CodeFixes.dll""", projectXml)
    StringAssert.DoesNotContain(@"<Analyzer Include=""..\packages\StyleCop.Analyzers.1.0.0\analyzers\dotnet\cs\StyleCop.Analyzers.dll""", projectXml)

    StringAssert.Contains(@"<Analyzer Include=""..\packages\StyleCop.Analyzers\analyzers\dotnet\cs\Newtonsoft.Json.dll"">", projectXml)
    StringAssert.Contains(@"<Analyzer Include=""..\packages\StyleCop.Analyzers\analyzers\dotnet\cs\StyleCop.Analyzers.CodeFixes.dll"">", projectXml)
    StringAssert.Contains(@"<Analyzer Include=""..\packages\StyleCop.Analyzers\analyzers\dotnet\cs\StyleCop.Analyzers.dll"">", projectXml)

let testPaketDependenciesFileInSolution scenario expectedFiles =
    use __ = paket "convert-from-nuget" scenario |> fst

    let expectedLines = 
        [ yield "Project\\(\"{2150E333-8FDC-42A3-9474-1A3956D46DE8}\"\\) = \"\\.paket\", \"\\.paket\", \"{[^}]+}\""
          yield "\s*ProjectSection\\(SolutionItems\\) = preProject"
          for f in expectedFiles do yield sprintf "\s*%s = %s" f f
          yield "\s*EndProjectSection"
          yield "EndProject" ]
        |> String.concat Environment.NewLine

    let slnFileText = File.ReadAllText(Path.Combine(scenarioTempPath scenario, "sln.sln"))
    
    System.Text.RegularExpressions.Regex.IsMatch(slnFileText, expectedLines)
    |> shouldEqual true

[<Test>]
let ``#2161 should put paket.dependencies inside the .paket folder when it's already present in the sln file``() =
    testPaketDependenciesFileInSolution "i002161" ["paket.dependencies"]

[<Test>]
let ``#2512 should put paket.dependencies inside the .paket folder when it's absent in the sln file``() =
    testPaketDependenciesFileInSolution "i002512-1" ["paket.dependencies"]

[<Test>]
let ``#2512 should put paket.dependencies inside the .paket folder that already has files in it``() =
    testPaketDependenciesFileInSolution "i002512-2" ["paket.dependencies"; "SomeFile"]

[<Test>]
let ``#2528 sdk-2: convert-from-nuget should add explicit FSharp.Core``() =
    let scenario = "i002528-1"
    use __ = paket "convert-from-nuget" scenario |> fst
    let dependenciesFile = File.ReadAllLines(Path.Combine(scenarioTempPath scenario,"paket.dependencies"))
    dependenciesFile
    |> shouldContain "nuget FSharp.Core"
    let referencesFile = File.ReadAllLines(Path.Combine(scenarioTempPath scenario,"paket.references"))
    referencesFile
    |> shouldContain "FSharp.Core"

[<Test>]
let ``#2528 sdk-2: convert-from-nuget should not add FSharp.Core for csharp``() =
    let scenario = "i002528-2"
    use __ = paket "convert-from-nuget" scenario |> fst
    let dependenciesFile = File.ReadAllLines(Path.Combine(scenarioTempPath scenario,"paket.dependencies"))
    dependenciesFile
    |> shouldNotContain "nuget FSharp.Core"
    let referencesFile = File.ReadAllLines(Path.Combine(scenarioTempPath scenario,"paket.references"))
    referencesFile
    |> shouldNotContain "FSharp.Core"

[<Test>]
let ``#2528 sdk-2: install should not warn without FSharp.Core for csharp``() =
    let scenario = "i002528-3"
    let cleanup, messages = paket "install" scenario
    use __ = cleanup
    messages
    |> shouldNotContainText "does not reference FSharp.Core"

[<Test>]
let ``#2528 sdk-2: install should not warn with FSharp.Core``() =
    let scenario = "i002528-4"
    let cleanup, messages = paket "install" scenario
    use __ = cleanup
    messages
    |> shouldNotContainText "does not reference FSharp.Core"

[<Test>]
let ``#2528 sdk-2: install should warn without FSharp.Core: not in dep, not in ref``() =
    let scenario = "i002528-5"
    let cleanup, messages = paket "install" scenario
    use __ = cleanup
    messages
    |> shouldContainText "does not reference FSharp.Core"

[<Test>]
let ``#2528 sdk-2: install should warn without FSharp.Core: in dep but not in ref``() =
    let scenario = "i002528-6"
    let cleanup, messages = paket "install" scenario
    use __ = cleanup
    messages
    |> shouldContainText "does not reference FSharp.Core"
