# Builds LayoutFix ByRB.exe, the release zip, and index.html for the website.
# Uses only the C# compiler that ships with Windows (.NET Framework 4.x).
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc  = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
New-Item -ItemType Directory -Force "$root\dist" | Out-Null

& $csc /nologo /codepage:65001 /target:winexe /optimize+ `
    /out:"$root\dist\LayoutFix ByRB.exe" "$root\src\Converter.cs" "$root\src\LayoutFix.cs"
if ($LASTEXITCODE -ne 0) { throw "compile failed" }

Compress-Archive "$root\dist\LayoutFix ByRB.exe" "$root\dist\LayoutFix-ByRB.zip" -Force

# index.html = the guide page wrapped in a full HTML document
$body = Get-Content "$root\LayoutFix-guide.html" -Raw -Encoding UTF8
$html = "<!doctype html>`n<html lang=`"he`" dir=`"rtl`">`n<head>`n<meta charset=`"utf-8`">`n" +
        "<meta name=`"viewport`" content=`"width=device-width, initial-scale=1, viewport-fit=cover`">`n" +
        "<style>body{margin:0}img{max-width:100%}</style>`n</head>`n<body>`n$body`n</body>`n</html>`n"
[IO.File]::WriteAllText("$root\index.html", $html, (New-Object Text.UTF8Encoding $false))
"Built: dist\LayoutFix ByRB.exe, dist\LayoutFix-ByRB.zip, index.html"
