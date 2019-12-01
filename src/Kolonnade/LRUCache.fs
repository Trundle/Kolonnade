namespace Kolonnade

open System.Collections.Generic

// A simple LRU cache. Makes no use of FP at all. Not thread-safe!
type LRUCache<'K, 'V> when 'K : comparison internal (maxSize) =
    do
        if maxSize <= 0 then raise (System.ArgumentException("zero or negative-sized cache"))

    let lastUsed = LinkedList<'K * 'V>()
    let mutable elements = Map.empty<'K, LinkedListNode<'K * 'V>>

    static member OfSize<'K, 'V> n = LRUCache<'K, 'V>(n)

    member this.Add(key: 'K, value: 'V) =
        match elements.TryFind(key) with
        | Some(element) ->
            element.Value <- (key, value)
            lastUsed.Remove(element)
            lastUsed.AddFirst(element)
        | None ->
            if lastUsed.Count >= maxSize then
                elements <- elements.Remove(fst lastUsed.Last.Value)
                lastUsed.RemoveLast()
            let element = new LinkedListNode<'K * 'V>((key, value))
            lastUsed.AddFirst(element)
            elements <- elements.Add(key, element)

    member this.Get(key) =
        match elements.TryFind(key) with
        | Some(element) ->
            lastUsed.Remove(element)
            lastUsed.AddFirst(element)
            Some(snd element.Value)
        | None -> None

    member this.Remove(key) =
        match elements.TryFind(key) with
        | Some element ->
            lastUsed.Remove(element)
            elements <- elements.Remove(key)
        | None -> ()