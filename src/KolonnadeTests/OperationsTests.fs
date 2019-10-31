namespace KolonnadeTests

open System
open FsCheck
open Expecto
open Kolonnade
open Kolonnade.Operations

type DisplayGen =
    static member Display(): Arbitrary<Display<int, unit>> =
        Gen.map3
            (fun n workspace (display: int) -> { n = n; workspace = workspace; monitor = IntPtr(display) })
            Arb.generate<int>
            Arb.generate<Workspace<int, unit>>
            Arb.generate<int>
        |> Arb.fromGen

module OperationsTests =
    let private config = { FsCheckConfig.defaultConfig with arbitrary = [typeof<DisplayGen>] }

    let tests = testList "Operations" [
        testPropertyWithConfig config "swapping displays twice results in original StackSet again" <|
            fun (stackSet: StackSet<int, unit>) -> swapDisplays (swapDisplays stackSet) = stackSet
    ]