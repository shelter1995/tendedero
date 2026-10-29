$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$vswhere='C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
$cl=(& $vswhere -latest -products '*' -find 'VC\Tools\MSVC\**\bin\Hostx64\x64\cl.exe' | Select-Object -First 1)
if(-not $cl){throw 'Visual C++ build tools are required to build the compositor adapter.'}
$vcRoot=Split-Path (Split-Path (Split-Path (Split-Path $cl -Parent) -Parent) -Parent) -Parent
$sdkRoot='C:\Program Files (x86)\Windows Kits\10'
$version=(Get-ChildItem -LiteralPath (Join-Path $sdkRoot 'Include') -Directory | Sort-Object Name -Descending | Select-Object -First 1).Name
$includeRoot=Join-Path $sdkRoot ('Include\'+$version)
$libRoot=Join-Path $sdkRoot ('Lib\'+$version)
$out=Join-Path $projectRoot 'native\GlassNative.dll'
$source=Join-Path $projectRoot 'native\GlassNative.cpp'
$arguments=@('/nologo','/LD','/std:c++20','/EHsc','/O2','/MT','/DUNICODE','/D_UNICODE',('/I'+(Join-Path $vcRoot 'include')))
foreach($folder in @('ucrt','shared','um','winrt','cppwinrt')){$arguments+=('/I'+(Join-Path $includeRoot $folder))}
$arguments+=@($source,('/Fo'+(Join-Path $projectRoot 'native\GlassNative.obj')),'/link',('/OUT:'+$out),('/IMPLIB:'+(Join-Path $projectRoot 'native\GlassNative.lib')),('/LIBPATH:'+(Join-Path $vcRoot 'lib\x64')),('/LIBPATH:'+(Join-Path $libRoot 'ucrt\x64')),('/LIBPATH:'+(Join-Path $libRoot 'um\x64')),'windowsapp.lib','CoreMessaging.lib','dwmapi.lib','user32.lib')
& $cl @arguments
if($LASTEXITCODE -ne 0){throw 'Native glass adapter build failed'}

