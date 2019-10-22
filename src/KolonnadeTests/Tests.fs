open System.Drawing

open Expecto
open Kolonnade
open KolonnadeTests

let properties =
    testList "Stack properties" [
        testProperty "reverse of reverse should be original" <|
            fun (stack: Stack<int>) -> stack.Reverse().Reverse() = stack

        testProperty "list of reversed stack should be reversed list of stack" <|
            fun (stack: Stack<int>) -> stack.Reverse().ToList() = List.rev (stack.ToList())

        testProperty "filter with non-matching predicate should return None" <|
            fun (stack: Stack<int>) -> stack.Filter(fun _ -> false).IsNone

        testProperty "filter with predicate that always matches should return original stack" <|
            fun (stack: Stack<int>) -> stack.Filter(fun _ -> true) = Some stack

        testProperty "filter preserves order" <|
            fun (stack: Stack<int>) ->
                let pred = (fun x -> x <> stack.focus)
                let filtered = List.filter pred (stack.ToList())
                match stack.Filter(pred) with
                | Some newStack -> newStack.ToList() = filtered
                | None -> filtered = []

        testProperty "focusUp (length of stack) times returns same stack again" <|
            fun (stack: Stack<int>) ->
                let result =
                    let rec nTimesUp (input: Stack<int>) = function
                        | 0 -> input
                        | n -> nTimesUp (input.FocusUp()) (n - 1)
                    nTimesUp stack (List.length (stack.ToList()))
                result = stack
    ]

let rotatedLayout = testList "rotated layout" [
    test "Description property returns description" {
        let layout = Rotated(Tall(0.7)) :> Layout
        Expect.equal layout.Description "Rotated(Tall)" "Description = Rotated(…)"
    }

    test "rotates layout" {
        let layout = Rotated(TwoPane(0.5)) :> Layout
        let stack: Stack<int> = { focus = 1; up = []; down = [2; 3] }
        let arrangement = layout.DoLayout(stack, Rectangle(0, 0, 200, 100))
        let expected = [(1, Rectangle(0, 0, 200, 50)); (2, Rectangle(0, 50, 200, 50))]
        Expect.equal arrangement expected "rotated window arrangement"
    }

    test "rotates layout with one window" {
        let layout = Rotated(TwoPane(0.5)) :> Layout
        let stack: Stack<int> = { focus = 1; up = []; down = [] }
        let arrangement = layout.DoLayout(stack, Rectangle(0, 0, 200, 100))
        let expected = [(1, Rectangle(0, 0, 200, 100))]
        Expect.equal arrangement expected "rotated window arrangement"
    }
]

let tests = testList "all tests" [
    properties; rotatedLayout; ActivityTrackerTests.tests;
]

[<EntryPoint>]
let main _ =
    Tests.runTests defaultConfig tests
