module Aornota.Gibet.Ui.User.Shared

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Ui.Common.RemoteData
open Aornota.Gibet.Ui.Common.Render.Shared
open Aornota.Gibet.Ui.Common.Render.Theme

open System

open Fulma

type UserData = User * bool * DateTimeOffset option

let [<Literal>] private RECENTLY_ACTIVE = 1.<minute>

let findUser userId (users:UserData list) = users |> List.tryFind (fun (user, _, _) -> user.UserId = userId)
let exists userId (users:UserData list) = users |> List.exists (fun (user, _, _) -> user.UserId = userId)

let findUserRD userId (usersData:RemoteData<UserData list, string>) = match usersData |> receivedData with | Some(users, _) -> users |> findUser userId | None -> None

let (|Self|RecentlyActive|SignedIn|NotSignedIn|PersonaNonGrata|) (user, signedIn, lastActivity, authUserId) =
    if user.UserId = authUserId then Self
    else if user.UserType = UserType.PersonaNonGrata then PersonaNonGrata
    else
        if signedIn then
            let recentlyActive =
                match lastActivity with
                | Some lastActivity -> lastActivity > DateTimeOffset.UtcNow.AddMinutes(float (RECENTLY_ACTIVE * -1.))
                | None -> false
            if recentlyActive then RecentlyActive else SignedIn
        else NotSignedIn

let tagTUser theme size (user, signedIn, lastActivity) authUserId =
    let colour =
        match (user, signedIn, lastActivity, authUserId) with
        | Self -> IsLink
        | RecentlyActive -> IsSuccess
        | SignedIn -> IsPrimary
        | NotSignedIn -> IsDark
        | PersonaNonGrata -> IsLight
    let (UserName userName) = user.UserName
    tagT theme size colour false None [ str userName ]
let tagTUserSmall theme (user, signedIn, lastActivity) authUserId = tagTUser theme None (user, signedIn, lastActivity) authUserId

let userTypeElement userType =
    match userType with
    | BenevolentDictatorForLife -> boldItalic "Benevolent dictator"
    | Administrator -> bold "Administrator"
    | Pleb -> str "User"
    | UserType.PersonaNonGrata -> italic "Persona non grata"
