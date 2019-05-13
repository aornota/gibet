module Aornota.Gibet.Ui.User.Shared

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Ui.Common.RemoteData
open Aornota.Gibet.Ui.Common.Render.Shared
open Aornota.Gibet.Ui.Common.Render.Theme

open System

open Fulma

type UserData = User * bool * DateTimeOffset option

let [<Literal>] RECENTLY_ACTIVE = 1.<minute> // TODO-NMB: Decide what this should be...

let findUser userId (usersData:RemoteData<UserData list, string>) =
    match usersData |> receivedData with
    | Some(users, _) -> users |> List.tryFind (fun (user, _, _) -> user.UserId = userId)
    | None -> None
let exists userId (usersData:RemoteData<UserData list, string>) = match usersData |> findUser userId with | Some _ -> true | None -> false

let tagTUser theme size (user, signedIn, lastActivity) authUserId =
    let colour =
        if user.UserId = authUserId then IsLink
        else if user.UserType = PersonaNonGrata then IsLight
        else
            if signedIn then
                let recentlyActive =
                    match lastActivity with
                    | Some lastActivity -> lastActivity > DateTimeOffset.UtcNow.AddMinutes(float (RECENTLY_ACTIVE * -1.))
                    | None -> false
                if recentlyActive then IsSuccess else IsPrimary
            else IsDark
    let (UserName userName) = user.UserName
    tagT theme size colour false None [ str userName ]
let tagTUserSmall theme (user, signedIn, lastActivity) authUserId = tagTUser theme IsSmall (user, signedIn, lastActivity) authUserId

let userType userType =
    match userType with
    | BenevolentDictatorForLife -> boldItalic "Benevolent dictator"
    | Administrator -> bold "Administrator"
    | Pleb -> str "User"
    | PersonaNonGrata -> italic "Persona non grata"
