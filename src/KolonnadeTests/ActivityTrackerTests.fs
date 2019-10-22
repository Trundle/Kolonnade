namespace KolonnadeTests

open System
open Expecto
open FsCheck
open Kolonnade


module ActivityTrackerTests =
    let internal genTicks n spanInSeconds =
        let tickGen =
            Gen.choose(0, spanInSeconds * 1_000_000_0 - 1)
            |> Gen.map (fun t -> DateTime(int64 t))
        Gen.listOfLength n tickGen
        |> Gen.map List.sort
        |> Gen.map seq

    let internal clockOfTicks (ticks: seq<DateTime>) =
        let enumerator = ticks.GetEnumerator()
        ((fun () -> enumerator.Current), (fun () -> enumerator.MoveNext() |> ignore))

    let tests = testList "ActivityTracker" [
        testProperty "classifies activity as hyperactive" <| (Prop.forAll (genTicks 6 3 |> Arb.fromGen) <|
             fun (ticks: seq<DateTime>) ->
                let (clock, tick) = clockOfTicks ticks
                let tracker = ActivityTracker((3, TimeSpan.FromSeconds(1.0)), clock)
                let tickAndTrack a = tick(); tracker.Track(a)
                let result = [
                    tickAndTrack("boring");
                    tickAndTrack("hyper");
                    tickAndTrack("hyper");
                    tickAndTrack("boring");
                    tickAndTrack("hyper");
                    tickAndTrack("hyper");
                ]
                result = [Boring; Boring; Boring; Boring; Boring; HyperActive])
        ]