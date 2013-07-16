﻿namespace SourceLink.Build

open System
open System.IO
open Microsoft.Build.Framework
open LibGit2Sharp
open System.Collections.Generic
open SourceLink

type SourceCheck() =
    inherit Task()

    [<Required>]
    member val ProjectFile = String.Empty with set, get

    [<Required>]
    member val RepoDir = String.Empty with set, get

    member val Exclude = String.Empty with set, get

    member internal x.GetRepoDir() = 
        if Path.IsPathRooted x.RepoDir then
            x.RepoDir.TrimEnd [|'\\'|]
        else
            Path.Combine(Path.GetDirectoryName x.ProjectFile, x.RepoDir).TrimEnd [|'\\'|] |> Path.GetFullPath

    member internal x.GetSourceFiles() =
        let excludes = HashSet<string>(StringComparer.OrdinalIgnoreCase)
        if false = String.IsNullOrEmpty x.Exclude then
            for exclude in x.Exclude.Split [|';'|] do
                excludes.Add exclude |> ignore
        Proj.getCompiles x.ProjectFile excludes

    override x.Execute() =
        try
            let repoDir = x.GetRepoDir()
            let files = x.GetSourceFiles()
            x.MessageNormal "%d source files to check" files.Length
            let committedChecksums = Git.getChecksums repoDir files
            let different = SortedSet(StringComparer.OrdinalIgnoreCase)
            for checksum, file in Git.computeChecksums files do
                if false = committedChecksums.Contains checksum then
                    different.Add file |> ignore
            if different.Count > 0 then
                x.Error "%d source files do not have matching checksums in the git repository" different.Count
                for file in different do
                    x.Error "no checksum match found for %s" file
        with
        | :? RepositoryNotFoundException as ex -> x.Error "%s" ex.Message
        | :? SourceLinkException as ex -> x.Error "%s" ex.Message

        not x.HasErrors
    