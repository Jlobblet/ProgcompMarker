namespace Common

open System.Runtime.Serialization

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

[<StructuredFormatDisplay("{StructuredFormatDisplay}"); DataContract>]
type Score =
    | ScoreMaxScore of Score: int * MaxScore: int
    | CaseValidScore of NCases: int * NValid: int * Score: int
    override this.ToString() =
        match this with
        | ScoreMaxScore (score, maxScore) -> $"%i{score}/%i{maxScore}"
        | CaseValidScore (nCases, nValid, score) -> $"%i{score} (%i{nValid} valid out of %i{nCases} cases)"

    member this.StructuredFormatDisplay = this.ToString()

[<CLIMutable; DataContract>]
type MarkResponse =
    { [<field: DataMember(Name = "Id")>]
      Id: uint64
      [<field: DataMember(Name = "Score")>]
      Score: Score }
