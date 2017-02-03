open System.Diagnostics
open System
open System.Threading.Tasks
open System.Management
open FSharp.Collections.ParallelSeq

type Agent<'a> = MailboxProcessor<'a>

let tap func arg = func arg; arg

let fix (proc: Process option) = 
    match proc with
    | None -> None
    | Some p when p.HasExited -> None
    | _ -> proc

let exec command = 
    let comspec = Environment.GetEnvironmentVariable("COMSPEC")
    let arguments = command |> sprintf "/c %s"
    let info = ProcessStartInfo(comspec, arguments, UseShellExecute = false)
    Process.Start(info) |> Some |> fix

let wait (proc: Process option) = 
    match proc |> fix with
    | None -> Task.CompletedTask
    | Some p -> Task.Factory.StartNew((fun () -> p.WaitForExit()), TaskCreationOptions.LongRunning)

let children (proc: Process option) = 
    match proc |> fix with
    | None -> Seq.empty
    | Some p -> 
        let query = p.Id |> sprintf "select * from Win32_Process where ParentProcessID = %d"
        use searcher = new ManagementObjectSearcher(query)
        let collection = searcher.Get() |> Seq.cast<ManagementObject>
        collection |> Seq.map (fun o -> o.["ProcessId"] |> Convert.ToInt32 |> Process.GetProcessById)

let rec kill (proc: Process option) = 
    proc |> fix |> Option.iter (fun p -> 
        p |> Some |> children |> PSeq.iter (Some >> kill)
        p.Kill()
        p.WaitForExit()
    )

type Event = 
    | Exec
    | Kill
    | Reboot
    | Recover of Guid
    | Quit
    | Exited of Guid

type State = 
    | Started
    | Stopped
    | Crashed of Guid

let rec guard arguments (inbox: Agent<_>) = async {
    let delay (ms: int) = Task.Delay(ms)
    let push event = inbox.Post event
    let postpone (task: Task) event = task.ContinueWith(fun _ -> push event) |> ignore

    let await guid proc = postpone (proc |> wait) (Exited guid)

    let rec start () = 
        let guid = Guid.NewGuid()
        loop Started (arguments |> exec |> tap (await guid) |> Some)
    and stop proc = 
        proc |> Option.iter kill
        loop Stopped None
    and reboot proc = 
        let guid = Guid.NewGuid()
        proc |> Option.iter kill
        push (Recover guid)
        loop (Crashed guid) None
    and recover guid = 
        postpone (delay 5000) (Recover guid)
        loop (Crashed guid) None
    and loop state proc = async {
        let! event = inbox.Receive ()
        match state, event with
        | _, Kill -> do! stop proc
        | Started, Reboot -> do! reboot proc
        | Crashed guid, Reboot -> do! start ()
        | Started, Quit -> push Quit; do! stop proc
        | Started, Exited guid -> do! recover guid
        | Stopped, Quit -> ()


    }
    do! loop Stopped None
}

let createGuard arguments = MailboxProcessor.Start (guard arguments)

[<EntryPoint>]
let main argv = 
    let guard = createGuard "dir /s c:\\Windows\\system32"
    guard.Post Exec
    Console.ReadLine() |> ignore
    guard.Post Kill
    Console.ReadLine() |> ignore
    0
