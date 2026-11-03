# Builds LayoutFix ByRB.exe, the release zip, and index.html for the website.
# Uses only the C# compiler that ships with Windows (.NET Framework 4.x).
#   .\build.ps1            -> everything
#   .\build.ps1 -PageOnly  -> only index.html (the app can keep running)
param([switch]$PageOnly)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

if (-not $PageOnly) {
    $csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    New-Item -ItemType Directory -Force "$root\dist" | Out-Null
    & $csc /nologo /codepage:65001 /target:winexe /optimize+ `
        /out:"$root\dist\LayoutFix ByRB.exe" "$root\src\Converter.cs" "$root\src\LayoutFix.cs"
    if ($LASTEXITCODE -ne 0) { throw "compile failed" }
    Compress-Archive "$root\dist\LayoutFix ByRB.exe" "$root\dist\LayoutFix-ByRB.zip" -Force
}

# index.html = the guide page as a full HTML document.
# Everything before the first <div> (title, meta, fonts, style) goes into <head>.
$src = Get-Content "$root\LayoutFix-guide.html" -Raw -Encoding UTF8
$i = $src.IndexOf('<div')
$head = $src.Substring(0, $i).Trim()
$body = $src.Substring($i).Trim()
$html = "<!doctype html>`n<html lang=`"he`" dir=`"rtl`">`n<head>`n<meta charset=`"utf-8`">`n" +
        "<meta name=`"viewport`" content=`"width=device-width, initial-scale=1, viewport-fit=cover`">`n" +
        "<style>body{margin:0}img{max-width:100%}[hidden]{display:none!important}</style>`n" +
        "$head`n</head>`n<body>`n$body`n</body>`n</html>`n"
[IO.File]::WriteAllText("$root\index.html", $html, (New-Object Text.UTF8Encoding $false))
if ($PageOnly) { "Built: index.html" } else { "Built: dist\LayoutFix ByRB.exe, dist\LayoutFix-ByRB.zip, index.html" }
