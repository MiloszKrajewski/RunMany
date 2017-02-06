namespace RunMany

open System.Diagnostics
open System
open System.Threading.Tasks
open System.Management
open FSharp.Collections.ParallelSeq

module Program = 
    open Model
    open System.IO

    let watcher = new FileSystemWatcher()
    watcher.Path <- "."
    watcher.IncludeSubdirectories <- true
    watcher.NotifyFilter <- 
        NotifyFilters.Attributes ||| 
        NotifyFilters.CreationTime ||| 
        NotifyFilters.DirectoryName ||| 
        NotifyFilters.FileName |||
        NotifyFilters.LastAccess |||
        NotifyFilters.LastWrite |||
        NotifyFilters.Security |||
        NotifyFilters.Size
    watcher.Changed |> Event.add (fun e -> printfn "Changed: %A,%A,%A" e.ChangeType e.FullPath e.Name)
    watcher.Created |> Event.add (fun e -> printfn "Created: %A,%A,%A" e.ChangeType e.FullPath e.Name)
    watcher.Deleted |> Event.add (fun e -> printfn "Deleted: %A,%A,%A" e.ChangeType e.FullPath e.Name)
    watcher.Renamed |> Event.add (fun e -> printfn "Deleted: %A,%A,%A,%A,%A" e.ChangeType e.FullPath e.Name e.OldFullPath e.OldName)
    watcher.EnableRaisingEvents <- true

    [<EntryPoint>]
    let main argv = 
//        let guard = GuardAgent.createGuard "task" "dir /s c:\\Windows\\system32"
//        guard Start
//        Console.ReadLine() |> ignore
//        guard Quit
        Console.ReadLine() |> ignore
        0
