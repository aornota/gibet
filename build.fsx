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
    override this.GetWebRequest(uri) =
        let request = base.GetWebRequest(uri)
        request.Timeout <- 30 * 60 * 1_000
        request

let mutable private deploymentOutputs : ArmOutput option = None

let private serverDir = Path.getFullName "./src/server"
let private uiDir = Path.getFullName "./src/ui"
let private uiPublishDir = Path.combine uiDir "publish"
let private publishDir = Path.getFullName "./publish"

let private devConsoleDir = Path.getFullName "./src/dev-console"

let private platformTool tool winTool =
    let tool = if Environment.isUnix then tool else winTool
    match ProcessUtils.tryFindFileOnPath tool with
    | Some t -> t
    | None -> failwithf "%s not found in path. Please install it and make sure it's available from your path. See https://safe-stack.github.io/docs/quickstart/#install-pre-requisites for more info." tool

let private yarnTool = platformTool "yarn" "yarn.cmd"

let private runTool cmd args workingDir =
    let arguments = args |> String.split ' ' |> Arguments.OfArgs
    Command.RawCommand(cmd, arguments)
    |> CreateProcess.fromCommand
    |> CreateProcess.withWorkingDirectory workingDir
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore

let private runDotNet cmd workingDir =
    let result = DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) cmd String.Empty
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s." cmd workingDir

let private openBrowser url =
    Command.ShellCommand url
    |> CreateProcess.fromCommand
    |> CreateProcess.ensureExitCodeWithMessage "Unable to open browser."
    |> Proc.run
    |> ignore

let private createMissingAppSettings forDevelopment dir =
    let settings, requiredSettings = "appsettings.json", Path.combine dir (sprintf "appsettings.%s.json" (if forDevelopment then "development" else "production"))
    if not (File.exists requiredSettings) then
        Shell.copyFile requiredSettings (Path.combine dir settings)
        Trace.traceImportant (sprintf "WARNING -> %s did not exist and has been copied from %s; it will most likely need to be modified" requiredSettings settings)

let private buildUi () = runTool yarnTool "webpack-cli -p" __SOURCE_DIRECTORY__

Target.create "clean-ui-publish" (fun _ -> Shell.cleanDir uiPublishDir)
Target.create "clean-publish" (fun _ -> Shell.cleanDir publishDir) // note: this will delete any .\logs and .\secret folders in publishDir (though should not be running server from publishDir!)

Target.create "restore-ui" (fun _ ->
    printfn "Yarn version:"
    runTool yarnTool "--version" __SOURCE_DIRECTORY__
    runTool yarnTool "install --frozen-lockfile" __SOURCE_DIRECTORY__
    runDotNet "restore" uiDir)

Target.create "run" (fun _ ->
    let server = async {
        createMissingAppSettings true serverDir
        runDotNet "watch run" serverDir }
    let client = async { runTool yarnTool "webpack-dev-server" __SOURCE_DIRECTORY__ }
    let browser = async {
        do! Async.Sleep 2500
        openBrowser "http://localhost:8080" }
    Async.Parallel [ server ; client ; browser ] |> Async.RunSynchronously |> ignore)

Target.create "build-server" (fun _ ->
    createMissingAppSettings false serverDir
    runDotNet "build -c Release" serverDir)
Target.create "build-ui" (fun _ -> buildUi ())
Target.create "build" (fun _ -> ())

Target.create "publish-server" (fun _ ->
    createMissingAppSettings false serverDir
    runDotNet (sprintf "publish -c Release -o \"%s\"" publishDir) serverDir)
Target.create "publish-ui" (fun _ ->
    buildUi ()
    Shell.copyDir (Path.combine publishDir "public") uiPublishDir FileFilter.allFiles)
Target.create "publish" (fun _ -> ())

Target.create "arm-template" (fun _ ->
    let armTemplate = "arm-template.json"
    let environment = "gibet"
    let authCtx =
        let subscriptionId = Guid("9ad207a4-28b9-48a4-b6ba-710c35034343") // azure-djnarration
        let clientId = Guid("14af985d-5718-4398-be45-7563042c7db7") // gibet [Azure AD application]
        Trace.tracefn "Deploying template '%s' to resource group '%s' in subscription '%O'..." armTemplate environment subscriptionId
        authenticateDevice Trace.trace { ClientId = clientId ; TenantId = None } subscriptionId |> Async.RunSynchronously
    let deployment =
        let location = Region.UKSouth.Name
        let pricingTier = "F1"
        { DeploymentName = environment
          ResourceGroup = New(environment, Region.Create location)
          ArmTemplate = IO.File.ReadAllText(armTemplate)
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
        | DeploymentError(statusCode, message) -> Trace.traceError (sprintf "Deployment error: %s -> '%s'" statusCode message)
        | DeploymentCompleted d -> deploymentOutputs <- d))

Target.create "deploy-azure" (fun _ ->
    let zipFile = "deploy.zip"
    IO.File.Delete(zipFile)
    Zip.zip publishDir zipFile !!(publishDir + @"\**\**")
    let appName = deploymentOutputs.Value.WebAppName.value
    let appPassword = deploymentOutputs.Value.WebAppPassword.value
    let destinationUri = sprintf "https://%s.scm.azurewebsites.net/api/zipdeploy" appName
    let client = new TimeoutWebClient(Credentials = NetworkCredential("$" + appName, appPassword))
    Trace.tracefn "Uploading %s to %s..." zipFile destinationUri
    client.UploadData(destinationUri, IO.File.ReadAllBytes(zipFile)) |> ignore)

Target.create "run-dev-console" (fun _ ->
    createMissingAppSettings true devConsoleDir
    runDotNet "run" devConsoleDir)

Target.create "help" (fun _ ->
    printfn "\nThese useful build targets can be run via 'fake build -t {target}':"
    printfn "\n\trun -> builds, runs and watches [Debug] server and [non-production] ui (served via webpack-dev-server)"
    printfn "\n\tbuild -> builds [Release] server and [production] ui (which writes output to .\\src\\ui\\publish)"
    printfn "\tpublish -> builds [Release] server and [production] ui and copies output to .\\publish"
    printfn "\n\tdeploy-azure -> builds [Release] server and [production] ui, copies output to .\\publish and deploys to Azure"
    printfn "\n\trun-dev-console -> builds and runs [Debug] dev-console"
    printfn "\n\thelp -> shows this list of build targets\n")

"restore-ui" ==> "run"
"restore-ui" ==> "clean-ui-publish"

"build-server" ==> "build"
"clean-ui-publish" ==> "build-ui" ==> "build"

"clean-publish" ==> "publish-server" ==> "publish"
"clean-ui-publish" ==> "publish-ui"
"clean-publish" ==> "publish-ui" ==> "publish"

"publish" ==> "arm-template" ==> "deploy-azure"

Target.runOrDefaultWithArguments "help"
