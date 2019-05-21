module Aornota.Gibet.Ui.User.Shared

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Ui.Common.Render

open System

open Fulma

type UserData = User * bool * DateTimeOffset option

let [<Literal>] private RECENTLY_ACTIVE = 1.<minute>

let tryFindUser userId (users:UserData list) = users |> List.tryFind (fun (user, _, _) -> user.UserId = userId)

// #region |Self|RecentlyActive|SignedIn|NotSignedIn|PersonaNonGrata| active pattern
// Note: Type annotation/s needed because of conditional compilation.
let (|Self|RecentlyActive|SignedIn|NotSignedIn|PersonaNonGrata|) (user, signedIn, lastActivity:DateTimeOffset option, authUserId) : Choice<unit, unit, unit, unit, unit> =
    if user.UserId = authUserId then Self
    else if user.UserType = UserType.PersonaNonGrata then PersonaNonGrata
    else
        if signedIn then
#if ACTIVITY
            let recentlyActive = match lastActivity with | Some lastActivity -> lastActivity > DateTimeOffset.UtcNow.AddMinutes(float -RECENTLY_ACTIVE) | None -> false
            if recentlyActive then RecentlyActive else SignedIn
#else
            SignedIn
#endif
        else NotSignedIn
// #endregion

let tagTUser theme size authUserId (user, signedIn, lastActivity) =
    let colour =
        match user, signedIn, lastActivity, authUserId with
        | Self -> IsLink
        | RecentlyActive -> IsSuccess
        | SignedIn -> IsPrimary
        | NotSignedIn -> IsBlack
        | PersonaNonGrata -> IsLight
    let (UserName userName) = user.UserName
    tagT theme size colour false None [ strong userName ]
let tagTUserSmall theme authUserId (user, signedIn, lastActivity) = tagTUser theme None authUserId (user, signedIn, lastActivity)

let userTypeElement userType =
    match userType with
    | BenevolentDictatorForLife -> strongEm "Benevolent dictator"
    | Administrator -> strong "Administrator"
    | Pleb -> str "User"
    | UserType.PersonaNonGrata -> em "Persona non grata"
