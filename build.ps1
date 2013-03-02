
Properties {
	$buildDir = Split-Path $psake.build_script_file	
	$buildArtifactsDir = "$buildDir\_build"
	$projectOutputDir = "$buildArtifactsDir\bin\"
	$solutionDir = "$buildDir"
	$version = "0.6.5"
}

Task default -Depends Compile, RunTests, CreatePackage

Task Compile -Depends Clean {
	Write-Host "Building solution" -ForegroundColor Green
	Exec { msbuild "$solutionDir\CsvLogTailer\CsvLogTailer.csproj" /t:Build /p:Configuration=Release /v:quiet /p:OutDir=$projectOutputDir } 
	gci "$projectOutputDir\CsvParser*.*" | rm
	gci "$projectOutputDir\System.Reactive*.*" | rm
	gci "$projectOutputDir\FParsec*.*" | rm
	gci "$projectOutputDir\FSharp.Core.*" | rm
}

Task Clean {
	Write-Host "Creating BuildArtifacts directory" -ForegroundColor Green
	if (Test-Path $buildArtifactsDir) 
	{	
		rd $buildArtifactsDir -rec -force | out-null
	}
 	  
	mkdir $buildArtifactsDir | out-null
	
	Write-Host "Cleaning solution" -ForegroundColor Green
	Exec { msbuild "$solutionDir\CsvLogTailer.sln" /t:Clean /p:Configuration=Release /v:quiet } 
}

Task RunTests {
	"TODO: Run tests"	
}

Task CreatePackage {
	Write-Host "Building NuGet package" -ForegroundColor Green
	&"$solutionDir\.nuget\nuget.exe" pack "$solutionDir\CsvLogTailer.nuspec" -OutputDirectory "$buildArtifactsDir" -Version $version
}
