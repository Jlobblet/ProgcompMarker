namespace Common

open System.Reflection
open System.Runtime.Serialization
open Microsoft.FSharp.Reflection

[<CLIMutable; DataContract>]
type InputResponse =
    { [<field: DataMember(Name = "Id")>]
      Id: uint64
      [<field: DataMember(Name = "Data")>]
      Data: string [] }

[<CLIMutable; DataContract>]
type MarkRequest =
    { [<field: DataMember(Name = "Id")>]
      Id: uint64
      [<field: DataMember(Name = "User")>]
      User: string
      [<field: DataMember(Name = "Data")>]
      Data: string [] }

[<StructuredFormatDisplay("{StructuredFormatDisplay}"); KnownType("KnownTypes")>]
type Score =
    | ScoreMaxScore of Score: int * MaxScore: int
    | CaseValidScore of NCases: int * NValid: int * Score: int
    static member KnownTypes() =
        typeof<Score>.GetNestedTypes (BindingFlags.Public ||| BindingFlags.NonPublic)
        |> Array.filter FSharpType.IsUnion

    override this.ToString() =
        match this with
        | ScoreMaxScore (score, maxScore) -> $"%i{score}/%i{maxScore}"
        | CaseValidScore (nCases, nValid, score) -> $"%i{score} (%i{nValid} valid out of %i{nCases} cases)"

    member this.StructuredFormatDisplay = this.ToString()

[<CLIMutable; DataContract; KnownType(typeof<Score>)>]
type MarkResponse =
    { [<field: DataMember(Name = "Id")>]
      Id: uint64
      [<field: DataMember(Name = "Score")>]
      Score: Score }
