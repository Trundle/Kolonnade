namespace KolonnadeTests

open Expecto
open FsCheck
open Kolonnade

module LRUCacheTests =
    let tests = testList "LRUCache" [
        testProperty "always returns most recently added entry"
        <| (Prop.forAll (Gen.nonEmptyListOf Arb.generate<int> |> Arb.fromGen)
        <| fun (entries: list<int>) ->
                let cache = LRUCache.OfSize(1)
                entries |> List.iter (fun x -> cache.Add(x, x))

                let x = (List.rev entries).Head
                Expect.equal (cache.Get(x)) (Some x) "retrieved item is not most recently added item")

        testProperty "Remove() removes entry" <|
            fun (entries: list<int>, n: uint8) ->
                let cache = LRUCache.OfSize(int n + 1)
                for x in entries do
                    cache.Add(x, x)
                    Expect.equal (cache.Get(x)) (Some x) "retrieved item is not most recently added item"

                    cache.Remove(x)
                    Expect.equal (cache.Get(x)) None "still returns item after Remove()"
    ]