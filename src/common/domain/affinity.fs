module Aornota.Gibet.Common.Domain.Affinity

open System

type AffinityId = | AffinityId of Guid with static member Create() = AffinityId(Guid.NewGuid())
