namespace Kolonnade

open System
open System.Runtime.CompilerServices

[<assembly: InternalsVisibleTo("KolonnadeTests")>]
do ()

type TrackingClassification =
    | HyperActive
    | Boring

type internal ActivityTracker<'A> when 'A: equality (hyperactiveThreshold: int * TimeSpan, clock: unit -> DateTime) =
    let (numberOfTimes, timeSpan) = hyperactiveThreshold
    let mutable activities: List<'A * DateTime> = List.empty

    member this.Track(a: 'A): TrackingClassification =
        let now = clock()
        let shouldBeTracked = fun (_, dt: DateTime) -> dt - now < timeSpan
        activities <- (a, now) :: List.takeWhile shouldBeTracked activities

        match List.groupBy (fun (x, _) -> x) activities |> List.tryFind (fun (x, _) -> x = a) with
        | Some (_, allA) when List.length allA > numberOfTimes -> HyperActive
        | _ -> Boring