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
    | Auto
    | Kill
    | Reboot
    | Exited
    | Quit
    | Status

type State = 
    | Started
    | Stopped

let rec guard arguments (inbox: Agent<_>) = async {
    let delay (ms: int) = Task.Delay(ms)
    let postpone (task: Task) event = task.ContinueWith(fun _ -> inbox.Post event) |> ignore
    let await proc = postpone (proc |> wait) Exited
    let exec () = arguments |> exec |> tap await |> Some
    let kill proc = proc |> Option.iter kill
    let rec loop state proc = async {
        let! event = inbox.Receive ()
        match event, state, proc with
        | Exec, _, None 
        | Auto, Started, None
        | Reboot, Started, _ -> proc |> kill; do! loop Started (exec ())
        | Kill, _, _ -> proc |> kill; do! loop Stopped None
        | Exited, Started, _ -> postpone (delay 5000) Auto; do! loop Started None
        | Quit, _, _ -> proc |> kill
        | _ -> do! loop state proc
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
