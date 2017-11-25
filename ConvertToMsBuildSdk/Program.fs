﻿
open System.IO
open System.Linq
open System.Xml.Linq

#nowarn "0077" // op_Explicit
let inline (!>) (x : ^a) : ^b = (((^a or ^b) : (static member op_Explicit : ^a -> ^b) x))


let NsMsBuild = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003")
let NsNone = XNamespace.None



let convertPackageConfig (projectFile : FileInfo) =
    let packageFileName = projectFile.Directory.GetFiles("packages.config").SingleOrDefault()
    if packageFileName |> isNull |> not then
        let xdoc = XDocument.Load (packageFileName.FullName)
        let pkgs = xdoc.Descendants(NsNone + "package")
                     |> Seq.map (fun x -> XElement(NsNone + "PackageReference",
                                             XAttribute(NsNone + "Include", (!> x.Attribute(NsNone + "id") : string)),
                                             XAttribute(NsNone + "Version", (!> x.Attribute(NsNone + "version") : string))))
        XElement(NsNone + "ItemGroup", pkgs)
    else 
        null        


// http://www.natemcmaster.com/blog/2017/03/09/vs2015-to-vs2017-upgrade/
let convertProject (projectFile : FileInfo) =
    let projectFileName = projectFile.Name
    let xdoc = XDocument.Load (projectFile.FullName)

    // https://docs.microsoft.com/en-us/dotnet/standard/frameworks
    let xTargetFx = match !> xdoc.Descendants(NsMsBuild + "TargetFrameworkVersion").First() : string with
                    | "v2.0" -> "net20"
                    | "v3.0" -> "net30"
                    | "v3.5" -> "net35"
                    | "v4.0" -> "net40"
                    | "v4.5" -> "net45"
                    | "v4.5.1" -> "net451"
                    | "v4.5.2" -> "net452"
                    | "v4.6" -> "net46"
                    | "v4.6.1" -> "net461"
                    | "v4.6.2" -> "net462"
                    | "v4.7" -> "net47"
                    | "v4.7.1" -> "net471"
                    | x -> x
                    |> (fun x -> XElement(NsNone + "TargetFramework", x))

    let xPlatform = match !> xdoc.Descendants(NsMsBuild + "Platform").FirstOrDefault() : string with
                    | null -> null
                    | "AnyCPU" -> null
                    | x -> XElement(NsNone + "PlatformTarget", x)

    let xunsafe = match !> xdoc.Descendants(NsMsBuild + "AllowUnsafeBlocks").FirstOrDefault() : string with
                  | null -> null
                  | x -> XElement(NsNone + "AllowUnsafeBlocks", x)

    let xSignAss = match !> xdoc.Descendants(NsMsBuild + "SignAssembly").FirstOrDefault() : string with
                   | null -> null
                   | x -> XElement(NsNone + "SignAssembly", x)

    let xOriginatorSign = match !> xdoc.Descendants(NsMsBuild + "AssemblyOriginatorKeyFile").FirstOrDefault() : string with
                          | null -> null
                          | x -> XElement(NsNone + "AssemblyOriginatorKeyFile", x) 

    let xAssName = match !> xdoc.Descendants(NsMsBuild + "AssemblyName").FirstOrDefault() : string with
                   | x when x <> projectFileName -> XElement(NsNone + "AssemblyName", x)
                   | _ -> null

    let xRootNs = match !> xdoc.Descendants(NsMsBuild + "RootNamespace").FirstOrDefault() : string with
                  | x when x <> projectFileName -> XElement(NsNone + "RootNamespace", x)
                  | _ -> null

    let xOutType = match !> xdoc.Descendants(NsMsBuild + "OutputType").FirstOrDefault() : string with
                   | null -> null
                   | "Library" -> null
                   | x -> XElement(NsNone + "OutputType", x)

    let xWarnAsErr = match !> xdoc.Descendants(NsMsBuild + "TreatWarningsAsErrors").FirstOrDefault() : string with
                     | null -> null
                     | x -> XElement(NsNone + "TreatWarningsAsErrors", x)

    let xWarns = match !> xdoc.Descendants(NsMsBuild + "WarningsAsErrors").FirstOrDefault() : string with
                 | null -> null
                 | x -> XElement(NsNone + "WarningsAsErrors", x)

    let src = xdoc.Descendants(NsMsBuild + "Compile")
                    |> Seq.filter (fun x -> (!> x.Attribute(NsNone + "Include") : string).StartsWith(".."))
    let res = xdoc.Descendants(NsMsBuild + "EmbeddedResource")
                    |> Seq.filter (fun x -> (!> x.Attribute(NsNone + "Include") : string).StartsWith(".."))
    let content = xdoc.Descendants(NsMsBuild + "Content")
    let none = xdoc.Descendants(NsMsBuild + "None")
                    |> Seq.filter (fun x -> (!> x.Attribute(NsNone + "Include") : string) <> "packages.config")
    let xSrc = XElement(NsNone + "ItemGroup", src, content, res, none)

    let prjRefs = xdoc.Descendants(NsMsBuild + "ProjectReference")
                     |> Seq.map (fun x -> XElement(NsNone + "ProjectReference", 
                                             XAttribute(NsNone + "Include", (!> x.Attribute(NsNone + "Include") : string))))
    let xPrjRefs = if prjRefs.Any() then XElement(NsNone + "ItemGroup", prjRefs)
                   else null

    let refs = xdoc.Descendants(NsMsBuild + "Reference")
                    |> Seq.filter (fun x -> x.HasElements |> not)
                    |> Seq.map (fun x -> XElement(NsNone + "Reference",
                                            XAttribute(NsNone + "Include", (!> x.Attribute(NsNone + "Include") : string))))
    let xRefs = if refs.Any() then XElement(NsNone + "ItemGroup", refs)
                else null

    let xPkgs = convertPackageConfig projectFile

    XDocument(
        XElement(NsNone + "Project",
            XAttribute(NsNone + "Sdk", "Microsoft.NET.Sdk"),
            XElement(NsNone + "PropertyGroup",
                XElement(NsNone + "AutoGenerateBindingRedirects", "true"),
                XElement(NsNone + "GenerateAssemblyInfo", "false"),
                xTargetFx, 
                xPlatform, 
                xunsafe,
                xSignAss,
                xOriginatorSign,
                xAssName,
                xRootNs,
                xOutType,
                xWarnAsErr,
                xWarns),
            xRefs,
            xPkgs,
            xSrc,
            xPrjRefs))

let saveProject (projectFile : FileInfo, content : XDocument) =
    content.Save (projectFile.FullName)


[<EntryPoint>]
let main argv = 
    let rootFolder = argv.[0] |> DirectoryInfo
    let csprojs = rootFolder.EnumerateFiles("*.csproj", SearchOption.AllDirectories)
    csprojs |> Seq.map (fun x -> x, convertProject x)
            |> Seq.iter saveProject
    0

   