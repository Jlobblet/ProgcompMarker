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

[<CLIMutable; DataContract>]
type MarkResponse =
    { [<field: DataMember(Name = "Id")>]
      Id: uint64
      [<field: DataMember(Name = "Score")>]
      Score: int
      [<field: DataMember(Name = "MaxScore")>]
      MaxScore: int }
