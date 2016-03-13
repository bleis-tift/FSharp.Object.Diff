﻿namespace FSharp.Object.Diff

open System
open System.Text
open Printf

type AppendableBuilder =
  abstract member Element: ElementSelector -> AppendableBuilder
  abstract member PropertyName: string * string [] -> AppendableBuilder
  abstract member CollectionItem: 'T -> AppendableBuilder
  abstract member MapKey: 'T -> AppendableBuilder
  abstract member Build: unit -> NodePath

and internal AppendableBuilderImpl(elementSelectors: ResizeArray<ElementSelector>) =
  interface AppendableBuilder with
    member this.Element(elementSelector) =
      elementSelectors.Add(elementSelector)
      this :> AppendableBuilder
    member this.PropertyName(name, [<ParamArray>] names) =
      elementSelectors.Add(BeanPropertyElementSelector(name))
      for name in names do elementSelectors.Add(BeanPropertyElementSelector(name))
      this :> AppendableBuilder
    member this.CollectionItem(item) =
      elementSelectors.Add(CollectionItemElementSelector(item))
      this :> AppendableBuilder
    member this.MapKey(key) =
      elementSelectors.Add(MapKeyElementSelector(key))
      this :> AppendableBuilder
    member this.Build() =
      match List.ofSeq elementSelectors with
      | [] -> raise <| InvalidOperationException("A property path cannot be empty")
      | x :: _ when not (x :? RootElementSelector) ->
        raise <| InvalidOperationException("A property path must start with a root element")
      | elementSelectors when (elementSelectors |> List.filter (fun x -> x :? RootElementSelector) |> List.length) > 1 ->
        raise <| InvalidOperationException("A property path cannot contain multiple root elements")
      | elementSelectors -> NodePath(elementSelectors)

and NodePath(elementSelectors: ElementSelector list) =

  member __.ElementSelectors = elementSelectors

  member __.LastElementSelector = Seq.last elementSelectors

  member __.IsParentOf(nodePath: NodePath) =
    let other = nodePath.ElementSelectors
    let l = List.length elementSelectors
    if l < List.length other then
      [ for i in 0 .. l - 1 -> List.nth other i ] = elementSelectors
    else false

  member __.IsChildOf(nodePath: NodePath) =
    let other = nodePath.ElementSelectors
    let l = List.length other
    if List.length elementSelectors > l then
      [ for i in 0 .. l - 1 -> List.nth elementSelectors i ] = other
    else false

  override __.GetHashCode() = hash elementSelectors

  override this.Equals(other) =
    match other with
    | null -> false
    | :? NodePath as other ->
      if this = other then true
      else elementSelectors = other.ElementSelectors
    | _ -> false

  override __.ToString() =
    let builder = StringBuilder()
    let rec loop (previous: ElementSelector option) selectors =
      match previous, selectors with
      | _, [] -> ()
      | Some p, x :: xs when (p :? RootElementSelector) ->
        bprintf builder "%O" x
        loop (Some x) xs
      | _, x :: xs when (x :? RootElementSelector) ->
        builder.Append("/") |> ignore
        loop (Some x) xs
      | _, x :: xs ->
        bprintf builder "/%O" x
        loop (Some x) xs
    loop None elementSelectors
    builder.ToString()

  member __.StartBuildingFrom(nodePath: NodePath) =
    AppendableBuilderImpl(ResizeArray(nodePath.ElementSelectors))
    :> AppendableBuilder

  member __.StartBuilding() =
    let elementSelectors = ResizeArray()
    elementSelectors.Add(RootElementSelector.Instance :> ElementSelector)
    AppendableBuilderImpl(elementSelectors)
    :> AppendableBuilder

  member this.With(propertyName: string, [<ParamArray>] additionalPropertyNames) =
    this.StartBuilding()
      .PropertyName(propertyName, additionalPropertyNames)
      .Build()

  member this.WithRoot() = this.StartBuilding().Build()

  member this.Matches(nodePath: NodePath) = nodePath.Equals(this)