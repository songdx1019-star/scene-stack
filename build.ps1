param([switch]$Test)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$buildDir = Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
$program = Join-Path $buildDir '归零现场.exe'
& $compiler /nologo /target:winexe /optimize+ "/out:$program" "/win32icon:$(Join-Path $PSScriptRoot 'assets\requiem.ico')" "/resource:$(Join-Path $PSScriptRoot 'assets\requiem.ico'),Requiem.Icon" "/resource:$(Join-Path $PSScriptRoot 'assets\requiem-widget.png'),Requiem.Widget" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll (Join-Path $PSScriptRoot 'SceneStack.cs')
if ($LASTEXITCODE -ne 0) { throw '编译失败' }
if ($Test) {
    $testExe = Join-Path $buildDir 'Tests.exe'
    & $compiler /nologo /target:exe "/out:$testExe" "/reference:$program" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll (Join-Path $PSScriptRoot 'Tests.cs')
    if ($LASTEXITCODE -ne 0) { throw '测试编译失败' }
    & $testExe (Join-Path $PSScriptRoot '界面预览.png')
    if ($LASTEXITCODE -ne 0) { throw '测试失败' }
}
Write-Output "已构建：$program"
