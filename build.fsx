#r "paket: groupref build //"
#if !FAKE
// See https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095.
#r "netstandard"
#r "Facades/netstandard"
#endif

#load "./.fake/build.fsx/intellisense.fsx"
#load "paket-files/build/CompositionalIT/fshelpers/src/FsHelpers/ArmHelper/ArmHelper.fs"

open System
open System.Net

open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators

open Cit.Helpers.Arm
open Cit.Helpers.Arm.Parameters

open Microsoft.Azure.Management.ResourceManager.Fluent.Core

type ArmOutput = { WebAppName : ParameterValue<string> ; WebAppPassword : ParameterValue<string> }

type TimeoutWebClient() =
    inherit WebClient()
    override this.GetWebRequest uri =
        let request = base.GetWebRequest uri
        request.Timeout <- 30 * 60 * 1000
        request

let mutable deploymentOutputs : ArmOutput option = None

let serverDir = Path.getFullName "./src/server"
let uiDir = Path.getFullName "./src/ui"
let uiDeployDir = Path.combine uiDir "deploy"
let deployDir = Path.getFullName "./deploy"

let platformTool tool winTool =
    let tool = if Environment.isUnix then tool else winTool
    match ProcessUtils.tryFindFileOnPath tool with
    | Some t -> t
    | None -> failwithf "%s not found in path. Please install it and make sure it's available from your path. See https://safe-stack.github.io/docs/quickstart/#install-pre-requisites for more info." tool

let yarnTool = platformTool "yarn" "yarn.cmd"

let runTool cmd args workingDir =
    let arguments = args |> String.split ' ' |> Arguments.OfArgs
    Command.RawCommand(cmd, arguments)
    |> CreateProcess.fromCommand
    |> CreateProcess.withWorkingDirectory workingDir
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore

let runDotNet cmd workingDir =
    let result = DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) cmd ""
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s." cmd workingDir

let openBrowser url =
    Command.ShellCommand url
    |> CreateProcess.fromCommand
    |> CreateProcess.ensureExitCodeWithMessage "Unable to open browser"
    |> Proc.run
    |> ignore

Target.create "clean" (fun _ ->
    [ deployDir ; uiDeployDir ] |> Shell.cleanDirs
)

Target.create "restore-ui" (fun _ ->
    printfn "Yarn version:"
    runTool yarnTool "--version" __SOURCE_DIRECTORY__
    runTool yarnTool "install --frozen-lockfile" __SOURCE_DIRECTORY__
    runDotNet "restore" uiDir
)

Target.create "run" (fun _ ->
    let server = async { runDotNet "watch run" serverDir }
    let client = async { runTool yarnTool "webpack-dev-server" __SOURCE_DIRECTORY__ }
    let browser = async {
        do! Async.Sleep 5000
        openBrowser "http://localhost:8080"
    }
    [ server ; client ; browser ] |> Async.Parallel |> Async.RunSynchronously |> ignore
)

Target.create "build-server" (fun _ ->
    runDotNet "build -c Release" serverDir
)

Target.create "build-ui" (fun _ ->
    runTool yarnTool "webpack-cli -p" __SOURCE_DIRECTORY__
)

Target.create "build" (fun _ -> ())

Target.create "publish-server" (fun _ ->
    runDotNet (sprintf "publish -c Release -o \"%s\"" deployDir) serverDir
)

Target.create "publish-ui" (fun _ ->
    Shell.copyDir (Path.combine deployDir "public") uiDeployDir FileFilter.allFiles
)

Target.create "publish" (fun _ -> ())

#nowarn "52" // TODO-NMB: Obviate need to suppress warning caused by "Guid.NewGuid().ToString().ToLower().Split '-' |> Array.head"...

Target.create "arm-template" (fun _ ->
    let environment = Environment.environVarOrDefault "environment" (Guid.NewGuid().ToString().ToLower().Split '-' |> Array.head)
    let armTemplate = "arm-template.json"
    let resourceGroupName = "safe-" + environment
    let authCtx =
        // You can safely replace these with your own subscription and client IDs hard-coded into this script.
        let subscriptionId = try Environment.environVar "subscriptionId" |> Guid.Parse with _ -> failwith "Invalid Subscription ID. This should be your Azure Subscription ID."
        let clientId = try Environment.environVar "clientId" |> Guid.Parse with _ -> failwith "Invalid Client ID. This should be the Client ID of a Native application registered in Azure with permission to create resources in your subscription."
        Trace.tracefn "Deploying template '%s' to resource group '%s' in subscription '%O'..." armTemplate resourceGroupName subscriptionId
        subscriptionId |> authenticateDevice Trace.trace { ClientId = clientId ; TenantId = None } |> Async.RunSynchronously
    let deployment =
        let location = Environment.environVarOrDefault "location" Region.EuropeWest.Name
        let pricingTier = Environment.environVarOrDefault "pricingTier" "F1" // TODO-NMB...
        { DeploymentName = "SAFE-template-deploy" // TODO-NMB...
          ResourceGroup = New(resourceGroupName, Region.Create location)
          ArmTemplate = IO.File.ReadAllText armTemplate
          Parameters =
              Simple
                  [ "environment", ArmString environment
                    "location", ArmString location
                    "pricingTier", ArmString pricingTier ]
          DeploymentMode = Incremental }
    deployment
    |> deployWithProgress authCtx
    |> Seq.iter (function
        | DeploymentInProgress(state, operations) -> Trace.tracefn "State is %s; completed %d operations" state operations
        | DeploymentError(statusCode, message) -> Trace.traceError <| sprintf "DEPLOYMENT ERROR: %s - '%s'" statusCode message
        | DeploymentCompleted d -> deploymentOutputs <- d)
)

Target.create "deploy-azure" (fun _ ->
    let zipFile = "deploy.zip"
    IO.File.Delete zipFile
    Zip.zip deployDir zipFile !!(deployDir + @"\**\**")
    let appName = deploymentOutputs.Value.WebAppName.value
    let appPassword = deploymentOutputs.Value.WebAppPassword.value
    let destinationUri = sprintf "https://%s.scm.azurewebsites.net/api/zipdeploy" appName
    let client = new TimeoutWebClient(Credentials = NetworkCredential("$" + appName, appPassword))
    Trace.tracefn "Uploading %s to %s..." zipFile destinationUri
    client.UploadData(destinationUri, IO.File.ReadAllBytes zipFile) |> ignore
)

Target.create "help" (fun _ ->
    printfn "\nThese useful build targets can be run via 'fake build -t {target}':"
    printfn "\n\trun -> builds, runs and watches [Debug] server and [non-production] ui (served via webpack-dev-server)"
    printfn "\n\tbuild -> builds [Release] server and [production] ui (which writes output to .\\src\\ui\\deploy)"
    printfn "\tpublish -> builds [Release] server and [production] ui and copies output to .\\deploy"
    // TODO-NMB...printfn "\n\tdeploy-azure -> builds [Release] server and [production] ui, copies output to .\\deploy and deploys to Azure"
    // TODO-NMB...printfn "\n\tdev-console -> builds and runs [Debug] dev-console"
    // TODO-NMB: gh-pages?...
    printfn "\n\thelp -> shows this list of build targets\n"
)

"clean" ==> "restore-ui"

"restore-ui" ==> "run"
"restore-ui" ==> "build-ui"

"build-ui" ==> "build"
"build-server" ==> "build"

"build-ui" ==> "publish-ui" ==> "publish"
"publish-server" ==> "publish"

"publish" ==> "arm-template" ==> "deploy-azure"

Target.runOrDefaultWithArguments "help"
