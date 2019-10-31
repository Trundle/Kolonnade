namespace Kolonnade

module Operations =
    /// Swaps the workspaces of the first two displays.
    let swapDisplays stackSet =
        match stackSet.visible with
        | firstVisible :: others ->
            let newCurrent = { stackSet.current with workspace = firstVisible.workspace }
            let newFirstVisible = { firstVisible with workspace = stackSet.current.workspace }
            { stackSet with current = newCurrent; visible = newFirstVisible :: others }
        | _ -> stackSet