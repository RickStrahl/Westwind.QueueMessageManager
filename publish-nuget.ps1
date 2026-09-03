Set-Location ./Westwind.MessageQueueing
& "./publish-nuget.ps1"

Set-Location ../Westwind.MessageQueueing.Hosting
& "./publish-nuget.ps1"

Set-Location ../Westwind.MessageQueueing.MongoDb
& "./publish-nuget.ps1"

cd ../
