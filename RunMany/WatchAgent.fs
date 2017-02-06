namespace RunMany

module WatchAgent =
    open Model
    open System.IO
    open System.Linq
    open System.Text.RegularExpressions

    type private Event = 
        | Command of Command
        | Changed

    type private State = 
        | Waiting
        | Working
        | Suspended

    type Config = { Path: string; Include: string[]; Exclude: string[] }

    let private wildcardToRegex wildcard = 
        let escaped = Regex.Escape(wildcard).Replace("\\*", ".*").Replace("\\?", ".?")
        Regex(escaped |> sprintf "^%s$")

    let isMatch (includes: Regex[]) (excludes: Regex[]) path = 
        let name = Path.GetFileName(path)
        let matches (rx: Regex) = rx.IsMatch(name)
        let included () = includes.Length = 0 || includes |> Array.exists matches
        let excluded () = excludes |> Array.exists matches
        included () && not (excluded ())

    let private watch name config arguments (inbox: Agent<_>) = async {
        let isMatch = 
            let includes = config.Include |> Array.map wildcardToRegex
            let excludes = config.Exclude |> Array.map wildcardToRegex
            fun (e: FileSystemEventArgs) -> isMatch includes excludes e.Name
        let isFolder (e: FileSystemEventArgs) = Directory.Exists(e.FullPath)
        use watcher = 
            new FileSystemWatcher(Path = config.Path, IncludeSubdirectories = true, Filter = "*.*")
        watcher.NotifyFilter <-
            NotifyFilters.CreationTime |||
            NotifyFilters.DirectoryName |||
            NotifyFilters.FileName |||
            NotifyFilters.LastWrite |||
            NotifyFilters.Size

        // watcher.Changed |> Event.filter (fun e -> isMatch e.Name)
//        watcher.Created 
//        |> Event.filter (fun e -> not (isFolder e) && (isMatch e))
//        |> Event.add (fun _ -> inbox.Post Changed)
//        watcher.Deleted
//        // watcher.Renamed
        watcher.EnableRaisingEvents <- true

        let rec loop () = async {
            let! msg = inbox.Receive ()
            do! loop ()
        }

        do! loop ()
    }