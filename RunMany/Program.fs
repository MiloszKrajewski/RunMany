namespace RunMany

open System.Diagnostics
open System
open System.Threading.Tasks
open System.Management
open FSharp.Collections.ParallelSeq

module Program = 
    open Model

    [<EntryPoint>]
    let main argv = 
        let guard = GuardAgent.createGuard "task" "dir /s c:\\Windows\\system32"
        guard Start
        Console.ReadLine() |> ignore
        guard Quit
        Console.ReadLine() |> ignore
        0
