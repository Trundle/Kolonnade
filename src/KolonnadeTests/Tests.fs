open Expecto
open Kolonnade

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

[<EntryPoint>]
let main _ =
    Tests.runTests defaultConfig properties
